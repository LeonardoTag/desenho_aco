using System;
using System.Collections.Generic;
using System.IO;
using Serilog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CapitalAco.DrawingMacro.App.Models;
using SkiaSharp;

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

        // QuestPDF 2024.3.0 removeu o suporte a .Canvas(...) (lança NotImplementedException em runtime).
        // Renderizamos via SkiaSharp.SKSvgCanvas e injetamos o resultado com .Svg(...).
        private static string RenderizarComoSvg(float width, float height, Action<SKCanvas> desenhar)
        {
            using var stream = new MemoryStream();
            using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
            {
                desenhar(canvas);
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
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
                            col.Item().Text("DETALHAMENTO DE DOBRA").FontSize((float)config.RelatorioDobraFonteTitulo).Bold().FontColor(Colors.Grey.Darken3);
                            col.Item().Text($"Peça: {nomePeca}").FontSize((float)config.RelatorioDobraFonteTexto);
                            col.Item().Text($"Chapa: #{chapaCodigo.Replace("#", "")} (Esp.: {dadosPlan.Espessura:F2} mm)").FontSize((float)config.RelatorioDobraFonteTexto);
                            col.Item().Text($"Comprimento da Peça: {comprimento:F0} mm").FontSize((float)config.RelatorioDobraFonteTexto);
                            col.Item().Text($"Desenvolvimento Plano: {dadosPlan.CorteTotal} mm").FontSize((float)config.RelatorioDobraFonteTexto).Bold();
                        });

                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize((float)config.RelatorioDobraFonteTexto);
                            col.Item().Text($"Responsável: {config.RelatorioNomeResponsavel}").FontSize((float)config.RelatorioDobraFonteTexto);
                        });
                    });

                    // Corpo do Relatório: desenho da peça em cima, planificação embaixo (melhor visualização)
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        // Desenho 3D do Perfil (em cima)
                        col.Item().Height(215).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPeca(canvas, new SKSize(size.Width, size.Height), polar, dimensoes, true, _geometryService,
                                    (float)config.RelatorioDobraFonteCota, (float)config.RelatorioDobraFonteAngulo)));

                        col.Item().PaddingTop(6);

                        // Planificação (embaixo)
                        col.Item().Height(120).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPlanificacao(canvas, new SKSize(size.Width, size.Height), dadosPlan,
                                    (float)config.RelatorioDobraFonteAngulo, (float)config.RelatorioDobraFonteSentido, (float)config.RelatorioDobraFonteCota)));

                        col.Item().PaddingTop(6).Row(infoRow =>
                        {
                            infoRow.RelativeItem().Column(textCol =>
                            {
                                textCol.Item().Text("Instruções de Planificação").FontSize((float)config.RelatorioDobraFonteSecao).Bold();
                                textCol.Item().Text("· Cotas ordenadas (topo): medidas acumuladas a partir do início da chapa plana. · Cotas em cadeia (base): medidas individuais entre centros de dobras sucessivas.").FontSize((float)config.RelatorioDobraFonteTexto);
                                textCol.Item().Text("· Linha vertical tracejada = dobra para cima. Linha vertical contínua = dobra para baixo. · Cota vermelha = medida interna. Cota azul = medida externa.").FontSize((float)config.RelatorioDobraFonteTexto);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize((float)config.RelatorioDobraFonteTexto);
                        t.Span(" / ").FontSize((float)config.RelatorioDobraFonteTexto);
                        t.TotalPages().FontSize((float)config.RelatorioDobraFonteTexto);
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
                                c.Item().Text(config.RelatorioNomeResponsavel.ToUpper()).FontSize((float)config.RelatorioPedidoFonteTitulo).Bold().FontColor(Colors.Indigo.Darken4);
                                c.Item().Text("ORDEM DE PRODUÇÃO").FontSize((float)config.RelatorioPedidoFonteSubtitulo).Bold().FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(250).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize((float)config.RelatorioPedidoFonteTexto);
                                c.Item().Text("PRAZO DE ENTREGA: ____________________").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                            });
                        });

                        col.Item().PaddingVertical(5).LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);

                        if (!string.IsNullOrWhiteSpace(observacao))
                        {
                            col.Item().Text(t =>
                            {
                                t.Span("Observação: ").Bold().FontSize((float)config.RelatorioPedidoFonteTexto);
                                t.Span(observacao).FontSize((float)config.RelatorioPedidoFonteTexto);
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
                                // 1. Imagem de Preview à esquerda (Skia via SVG)
                                row.RelativeItem(5).Padding(3).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas =>
                                {
                                    try
                                    {
                                        var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(item.ChapaCodigo, item.Comprimento, item.Segmentos);
                                        var dim = _geometryService.CalcularDimensoesAcabadas(polar);
                                        SkiaRenderer.RenderizarPeca(canvas, new SKSize(size.Width, size.Height), polar, dim, true, _geometryService, fonteCota: 7f, fonteAngulo: 6.5f);
                                    }
                                    catch
                                    {
                                        canvas.Clear(SKColors.White);
                                    }
                                }));

                                // Divisor
                                row.ConstantItem(1).LineVertical(0.5f).LineColor(Colors.Grey.Lighten2);

                                // 2. Dados numéricos de produção à direita
                                row.RelativeItem(3).Padding(5).Column(c =>
                                {
                                    c.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text($"Qtde: {item.Quantidade}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque);
                                        r.RelativeItem().Text($"Chapa: #{item.ChapaCodigo.Replace("#", "")}").FontSize((float)config.RelatorioPedidoFonteTexto);
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
                                        t.Span("Corte: ").Bold().FontSize((float)config.RelatorioPedidoFonteRotuloCampo);
                                        t.Span($"{corte:F0} mm").Bold().FontColor(Colors.Red.Medium).FontSize((float)config.RelatorioPedidoFonteDestaque);
                                    });

                                    c.Item().Text($"Comprimento: {item.Comprimento:F0} mm").FontSize((float)config.RelatorioPedidoFonteTexto);
                                    c.Item().Text($"Peso: {peso:F0} kg").FontSize((float)config.RelatorioPedidoFonteTexto);
                                });

                                // Checkbox de controle de fábrica
                                row.ConstantItem(60).PaddingRight(5).AlignMiddle().AlignCenter().Column(c =>
                                {
                                    c.Item().Border(1).BorderColor(Colors.Grey.Darken1).Width(15).Height(15).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas => { }));
                                    c.Item().PaddingTop(3).AlignCenter().Text("Cortado").FontSize((float)config.RelatorioPedidoFonteRotuloPeca);
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize((float)config.RelatorioPedidoFonteTexto);
                        t.Span(" / ").FontSize((float)config.RelatorioPedidoFonteTexto);
                        t.TotalPages().FontSize((float)config.RelatorioPedidoFonteTexto);
                    });
                });
            }).GeneratePdf(caminhoPdf);

            Log.Information("PDF de Ordem de Produção gerado com sucesso em: {Path}", caminhoPdf);
            return caminhoPdf;
        }
    }
}
