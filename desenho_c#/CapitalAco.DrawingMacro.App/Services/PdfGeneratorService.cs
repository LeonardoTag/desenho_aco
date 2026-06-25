using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("DETALHAMENTO DE DOBRA").FontSize((float)config.RelatorioDobraFonteTitulo).Bold().FontColor(Colors.Indigo.Darken4);
                                col.Item().PaddingTop(2).Text($"Peça: {nomePeca}").FontSize((float)config.RelatorioDobraFonteTexto).Bold();
                                col.Item().Text($"Chapa: #{chapaCodigo.Replace("#", "")} (Esp.: {dadosPlan.Espessura:F2} mm)").FontSize((float)config.RelatorioDobraFonteTexto).Bold();
                                col.Item().Text($"Comprimento da Peça: {comprimento:F0} mm").FontSize((float)config.RelatorioDobraFonteTexto).Bold();
                                col.Item().Text($"Desenvolvimento Plano: {dadosPlan.CorteTotal} mm").FontSize((float)config.RelatorioDobraFonteTexto).Bold().FontColor(Colors.Indigo.Darken4);
                            });

                            row.ConstantItem(200).AlignRight().Column(col =>
                            {
                                col.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize((float)config.RelatorioDobraFonteTexto);
                                col.Item().Text($"Responsável: {config.RelatorioNomeResponsavel}").FontSize((float)config.RelatorioDobraFonteTexto);
                            });
                        });

                        headerCol.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(Colors.Indigo.Darken4);
                    });

                    // Corpo do Relatório: desenho da peça em cima, planificação embaixo (melhor visualização)
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        // Desenho 3D do Perfil (em cima)
                        col.Item().Text("DESENHO DA PEÇA").FontSize((float)config.RelatorioDobraFonteSecao).Bold().FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(2).Height(215).Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPeca(canvas, new SKSize(size.Width, size.Height), polar, dimensoes, true, _geometryService,
                                    (float)config.RelatorioDobraFonteCota, (float)config.RelatorioDobraFonteAngulo)));

                        col.Item().PaddingTop(10).Text("PLANIFICAÇÃO").FontSize((float)config.RelatorioDobraFonteSecao).Bold().FontColor(Colors.Grey.Darken2);

                        // Planificação (embaixo)
                        col.Item().PaddingTop(2).Height(120).Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPlanificacao(canvas, new SKSize(size.Width, size.Height), dadosPlan,
                                    (float)config.RelatorioDobraFonteAngulo, (float)config.RelatorioDobraFonteSentido, (float)config.RelatorioDobraFonteCota)));

                        // Legenda: discreta e compacta (sempre igual em todo relatório, não merece destaque),
                        // numa única linha por tópico para nunca empurrar o conteúdo para uma segunda página.
                        const float fonteLegenda = 6.5f;
                        col.Item().PaddingTop(6).Text("Legenda: fundo escuro/letra branca = medida externa  ·  fundo claro/letra escura = medida interna  ·  ângulo sublinhado = dobra não-reta  ·  linha tracejada = dobra p/ cima  ·  linha contínua = dobra p/ baixo")
                            .FontSize(fonteLegenda).FontColor(Colors.Grey.Darken1);
                        col.Item().Text("Cotas no topo da planificação = acumuladas desde o início da chapa  ·  cotas na base = entre dobras sucessivas")
                            .FontSize(fonteLegenda).FontColor(Colors.Grey.Darken1);
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
            var ordemChapas = chapas.Select((c, i) => (c.Codigo, i)).ToDictionary(x => x.Codigo, x => x.i);
            itens = itens
                .OrderBy(i => ordemChapas.TryGetValue(i.ChapaCodigo, out var idx) ? idx : int.MaxValue)
                .ToList();

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
                        int quantidadeTotalPecas = 0;
                        double pesoTotalGeral = 0.0;

                        // Peças com mais segmentos (mais cotas/ângulos para encaixar no desenho) recebem um
                        // container mais alto, senão as medidas ficam amontoadas e ilegíveis em peças complexas.
                        double AlturaParaPeca(PecaPedidoItem it)
                        {
                            const double alturaBase = 95;
                            const double alturaPorSegmentoExtra = 16;
                            const int segmentosBase = 3;
                            const double alturaMaxima = 260;

                            int numSegmentos = it.Segmentos?.Count ?? 0;
                            double altura = alturaBase + Math.Max(0, numSegmentos - segmentosBase) * alturaPorSegmentoExtra;

                            // Segmentos curvos somam cotas extras (raio, ângulo de curva, calandragem)
                            if (it.Segmentos != null && it.Segmentos.Any(s => s.EhCurvo)) altura += 20;

                            return Math.Min(altura, alturaMaxima);
                        }

                        // Dividimos as peças em slots, numerados e com fundo alternado (zebra) para facilitar a leitura
                        for (int idx = 0; idx < itens.Count; idx++)
                        {
                            var item = itens[idx];
                            quantidadeTotalPecas += item.Quantidade;

                            col.Item().PaddingBottom(10).Background(idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5)
                                .Border(0.5f).BorderColor(Colors.Grey.Lighten1).Height((float)AlturaParaPeca(item)).Row(row =>
                            {
                                // Número do item
                                row.ConstantItem(22).AlignMiddle().AlignCenter()
                                    .Text($"{idx + 1:D2}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque).FontColor(Colors.Indigo.Darken4);

                                row.ConstantItem(1).LineVertical(0.5f).LineColor(Colors.Grey.Lighten2);

                                // 1. Imagem de Preview à esquerda (Skia via SVG)
                                row.RelativeItem(7).Padding(3).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas =>
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
                                row.RelativeItem(2).Padding(5).Column(c =>
                                {
                                    c.Item().Text(item.NomePeca).Bold().FontSize((float)config.RelatorioPedidoFonteDestaque).FontColor(Colors.Indigo.Darken4);

                                    c.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.RelativeItem().Text($"Qtde: {item.Quantidade}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque);
                                        r.RelativeItem().Text($"Chapa: #{item.ChapaCodigo.Replace("#", "")}").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
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

                                    pesoTotalGeral += peso;

                                    c.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Corte: ").Bold().FontSize((float)config.RelatorioPedidoFonteRotuloCampo);
                                        t.Span($"{corte:F0} mm").Bold().FontColor(Colors.Red.Medium).FontSize((float)config.RelatorioPedidoFonteDestaque);
                                    });

                                    c.Item().Text($"Comprimento: {item.Comprimento:F0} mm").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                    c.Item().Text($"Peso: {peso:F0} kg").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                    c.Item().PaddingTop(5).Row(r =>
                                    {
                                        r.ConstantItem(15).Border(1).BorderColor(Colors.Grey.Darken1).Height(15).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas => { }));
                                        r.AutoItem().PaddingLeft(4).Text("Cortado").FontSize((float)config.RelatorioPedidoFonteRotuloPeca);
                                    });
                                    if (!string.IsNullOrWhiteSpace(item.Observacao))
                                    {
                                        c.Item().PaddingTop(2).Text($"Obs.: {item.Observacao}").FontSize((float)config.RelatorioPedidoFonteRotuloCampo).Italic();
                                    }
                                });
                            });
                        }

                        // Resumo Geral do Pedido
                        col.Item().PaddingTop(4).Background(Colors.Indigo.Lighten5).Border(1).BorderColor(Colors.Indigo.Lighten3).Padding(8).Row(r =>
                        {
                            r.RelativeItem().Text($"Total de Itens: {itens.Count}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque);
                            r.RelativeItem().Text($"Total de Peças: {quantidadeTotalPecas}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque);
                            r.RelativeItem().AlignRight().Text($"Peso Total Estimado: {pesoTotalGeral:F0} kg").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque).FontColor(Colors.Indigo.Darken4);
                        });
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
