using System;
using System.Collections.Generic;
using System.IO;
using Serilog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    /*
     * CADEIA DE PENSAMENTO (CHAIN OF THOUGHT) - PdfGeneratorService:
     * 1. A geração de PDFs precisa ser extremamente rápida, limpa e exata. Adotamos o QuestPDF para design declarativo strongly-typed.
     * 2. Na inicialização do serviço, registramos a licença de comunidade da QuestPDF para impedir exceções de licenciamento.
     * 3. Relatório de Dobra (Ficha de Produção):
     *    - Usamos layout horizontal (Landscape A4) e margens estreitas.
     *    - Integramos as funções do SkiaRenderer usando o recurso de Canvas nativo do QuestPDF (`.Canvas(...)`), que desenha
     *      diretamente no PDF vetorial, poupando operações de IO em disco e maximizando a performance.
     * 4. Ordem de Produção (Relatório de Pedido):
     *    - Usamos layout vertical (Portrait A4) e criamos um grid vertical de slots de peças (reutilizando a lógica do Python de 9 peças).
     *    - Cada slot exibe o preview 3D da peça gerado pelo SkiaRenderer à esquerda, e os dados numéricos de produção à direita
     *      (Quantidade, Chapa, Corte destacado em vermelho, Comprimento, Peso e checkbox de corte executado).
     */
    public class PdfGeneratorService : IPdfGeneratorService
    {
        private readonly IConfigService _configService;
        private readonly IGeometryService _geometryService;
        private readonly ICsvService _csvService;

        public PdfGeneratorService(IConfigService configService, IGeometryService geometryService, ICsvService csvService)
        {
            _configService = configService;
            _geometryService = geometryService;
            _csvService = csvService;

            // Registrar licença da comunidade
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string GerarRelatorioDobra(InstrucoesPolares polar, string nomePeca, string chapaCodigo, double comprimento)
        {
            var config = _configService.ObterConfiguracao();
            var pastaSaida = _configService.ObterCaminhoSaidaRelatorios();
            var nomeArquivo = $"DETALHAMENTO_DOBRA_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var caminhoPdf = Path.Combine(pastaSaida, nomeArquivo);

            var dadosPlan = _geometryService.GerarDadosPlanificacao(polar);
            var dimensoes = _geometryService.CalcularDimensoesAcabadas(polar);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.0f, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    // Cabeçalho
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("DETALHAMENTO DE DOBRA").FontSize(16).Bold().FontColor(Colors.Grey.Darken3);
                            col.Item().Text($"Peça: {nomePeca}").FontSize(10);
                            col.Item().Text($"Chapa: #{chapaCodigo.Replace("#", "")} (Esp.: {dadosPlan.Espessura:F2} mm)").FontSize(10);
                            col.Item().Text($"Comprimento da Peça: {comprimento:F0} mm").FontSize(10);
                            col.Item().Text($"Desenvolvimento Plano: {dadosPlan.CorteTotal} mm").FontSize(10).Bold();
                        });

                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                            col.Item().Text($"Responsável: {config.RelatorioNomeResponsavel}").FontSize(9);
                        });
                    });

                    // Corpo do Relatório
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        // Linha com Desenhos
                        col.Item().Row(drawRow =>
                        {
                            // Preview 3D do Perfil
                            drawRow.RelativeItem(4).Height(240).Border(1).BorderColor(Colors.Grey.Lighten2).Canvas((canvas, size) =>
                            {
                                SkiaRenderer.RenderizarPeca(canvas, size, polar, dimensoes, true, _geometryService);
                            });

                            drawRow.ConstantItem(15);

                            // Planificação
                            drawRow.RelativeItem(5).Height(240).Border(1).BorderColor(Colors.Grey.Lighten2).Canvas((canvas, size) =>
                            {
                                SkiaRenderer.RenderizarPlanificacao(canvas, size, dadosPlan);
                            });
                        });

                        col.Item().PaddingTop(15).Row(infoRow =>
                        {
                            infoRow.RelativeItem().Column(textCol =>
                            {
                                textCol.Item().Text("Instruções de Planificação").FontSize(11).Bold();
                                textCol.Item().Text("· Cotas ordenadas (topo): medidas acumuladas a partir do início da chapa plana.").FontSize(9);
                                textCol.Item().Text("· Cotas em cadeia (base): medidas individuais entre centros de dobras sucessivas.").FontSize(9);
                                textCol.Item().Text("· Linha vertical tracejada = dobra para cima. Linha vertical contínua = dobra para baixo.").FontSize(9);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" / ").FontSize(9);
                        t.TotalPages().FontSize(9);
                    });
                });
            }).GeneratePdf(caminhoPdf);

            Log.Information("PDF de Detalhamento de Dobra gerado com sucesso em: {Path}", caminhoPdf);
            return caminhoPdf;
        }

        public string GerarRelatorioPedido(List<PecaPedidoItem> itens, string observacao = "")
        {
            var config = _configService.ObterConfiguracao();
            var pastaSaida = _configService.ObterCaminhoSaidaRelatorios();
            var nomeArquivo = $"ORDEM_PRODUCAO_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var caminhoPdf = Path.Combine(pastaSaida, nomeArquivo);

            var chapas = _csvService.CarregarChapas();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.0f, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    // Cabeçalho do Pedido
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(config.RelatorioNomeResponsavel.ToUpper()).FontSize(14).Bold().FontColor(Colors.Indigo.Darken4);
                                c.Item().Text("ORDEM DE PRODUÇÃO").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(250).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                                c.Item().Text("PRAZO DE ENTREGA: ____________________").FontSize(9).Bold();
                            });
                        });

                        col.Item().PaddingVertical(5).LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);

                        if (!string.IsNullOrWhiteSpace(observacao))
                        {
                            col.Item().Text(t =>
                            {
                                t.Span("Observação: ").Bold().FontSize(9);
                                t.Span(observacao).FontSize(9);
                            });
                            col.Item().PaddingBottom(5);
                        }
                    });

                    // Lista de Peças
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        // Dividimos as peças em slots
                        foreach (var item in itens)
                        {
                            col.Item().PaddingBottom(10).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Height(95).Row(row =>
                            {
                                // 1. Imagem de Preview à esquerda (Canvas Skia)
                                row.RelativeItem(5).Padding(3).Canvas((canvas, size) =>
                                {
                                    try
                                    {
                                        var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(item.ChapaCodigo, item.Comprimento, item.Segmentos);
                                        var dim = _geometryService.CalcularDimensoesAcabadas(polar);
                                        SkiaRenderer.RenderizarPeca(canvas, size, polar, dim, false, _geometryService);
                                    }
                                    catch
                                    {
                                        canvas.Clear(Colors.White);
                                    }
                                });

                                // Divisor
                                row.ConstantItem(1).LineVertical(0.5f).LineColor(Colors.Grey.Lighten2);

                                // 2. Dados numéricos de produção à direita
                                row.RelativeItem(3).Padding(5).Column(c =>
                                {
                                    c.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text($"Qtde: {item.Quantidade}").Bold().FontSize(11);
                                        r.RelativeItem().Text($"Chapa: #{item.ChapaCodigo.Replace("#", "")}").FontSize(10);
                                    });

                                    double corte = 0.0;
                                    double peso = 0.0;
                                    try
                                    {
                                        var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(item.ChapaCodigo, item.Comprimento, item.Segmentos);
                                        corte = _geometryService.CalcularLarguraCorte(polar);
                                        
                                        var chapaInfo = chapas.Find(x => string.Equals(x.Codigo, item.ChapaCodigo.StartsWith("#") ? item.ChapaCodigo : $"#{item.ChapaCodigo}", StringComparison.OrdinalIgnoreCase));
                                        if (chapaInfo != null)
                                        {
                                            peso = _geometryService.CalcularPesoKg(polar, item.Quantidade, chapaInfo);
                                        }
                                    }
                                    catch { }

                                    c.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Corte: ").Bold().FontSize(9);
                                        t.Span($"{corte:F0} mm").Bold().FontColor(Colors.Red.Medium).FontSize(10);
                                    });

                                    c.Item().Text($"Comprimento: {item.Comprimento:F0} mm").FontSize(9);
                                    c.Item().Text($"Peso: {peso:F0} kg").FontSize(9);
                                });

                                // Checkbox de controle de fábrica
                                row.ConstantItem(60).PaddingRight(5).AlignMiddle().AlignCenter().Column(c =>
                                {
                                    c.Item().Border(1).BorderColor(Colors.Grey.Darken1).Width(15).Height(15).Canvas((canvas, size) => { });
                                    c.Item().PaddingTop(3).Text("Cortado").FontSize(7).AlignCenter();
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" / ").FontSize(9);
                        t.TotalPages().FontSize(9);
                    });
                });
            }).GeneratePdf(caminhoPdf);

            Log.Information("PDF de Ordem de Produção gerado com sucesso em: {Path}", caminhoPdf);
            return caminhoPdf;
        }
    }
}
