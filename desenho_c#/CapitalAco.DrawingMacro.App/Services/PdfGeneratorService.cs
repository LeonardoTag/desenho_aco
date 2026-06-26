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

            // Seleciona orientação automaticamente: peças mais altas que largas usam portrait
            // para aproveitar o espaço vertical do A4; demais usam landscape (mais área horizontal).
            bool portrait = dimensoes.HasValue && dimensoes.Value.Altura > dimensoes.Value.Largura;
            var tamanho          = portrait ? PageSizes.A4 : PageSizes.A4.Landscape();
            float alturaDesenho  = portrait ? 420f : 215f;
            // Planificação precisa de ~50pt acima da barra (acumuladas) + 28pt de barra + ~48pt abaixo (cadeia).
            // Mínimo seguro: 130pt. Os valores anteriores (100/120) faziam as cotas transbordarem para fora da caixa.
            float alturaPlani    = portrait ? 130f : 150f;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(tamanho);
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
                                if (dimensoes.HasValue)
                                    col.Item().Text($"Dimensões Acabadas: {dimensoes.Value.Largura:F0} × {dimensoes.Value.Altura:F0} mm").FontSize((float)config.RelatorioDobraFonteTexto).Bold();
                            });

                            row.ConstantItem(200).AlignRight().Column(col =>
                            {
                                col.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize((float)config.RelatorioDobraFonteTexto);
                                col.Item().Text($"Responsável: {config.RelatorioNomeResponsavel}").FontSize((float)config.RelatorioDobraFonteTexto);
                            });
                        });

                        headerCol.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(Colors.Indigo.Darken4);
                    });

                    // Corpo: desenho da peça em cima, planificação embaixo
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Text("DESENHO DA PEÇA").FontSize((float)config.RelatorioDobraFonteSecao).Bold().FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(2).Height(alturaDesenho).Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPeca(canvas, new SKSize(size.Width, size.Height), polar, dimensoes, true, _geometryService,
                                    (float)config.RelatorioDobraFonteCota, (float)config.RelatorioDobraFonteAngulo)));

                        col.Item().PaddingTop(10).Text("PLANIFICAÇÃO").FontSize((float)config.RelatorioDobraFonteSecao).Bold().FontColor(Colors.Grey.Darken2);

                        col.Item().PaddingTop(2).Height(alturaPlani).Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten2).Svg(size =>
                            RenderizarComoSvg(size.Width, size.Height, canvas =>
                                SkiaRenderer.RenderizarPlanificacao(canvas, new SKSize(size.Width, size.Height), dadosPlan,
                                    (float)config.RelatorioDobraFonteAngulo, (float)config.RelatorioDobraFonteSentido, (float)config.RelatorioDobraFonteCota)));

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

            Log.Information("PDF de Detalhamento de Dobra gerado ({Orientacao}) em: {Path}",
                portrait ? "Portrait" : "Landscape", caminhoPdf);
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
                .OrderBy(i => ordemChapas.TryGetValue(i.ChapaCodigo, out var ord) ? ord : int.MaxValue)
                .ToList();

            // Pré-computa geometria de cada peça fora das lambdas do QuestPDF para evitar
            // qualquer risco de avaliação deferida capturar o contexto errado.
            var dadosPecas = itens.Select(item =>
            {
                InstrucoesPolares? polar = null;
                double corte = 0.0;
                double peso = 0.0;
                DimensoesAcabadas? dim = null;

                try
                {
                    polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(
                        item.ChapaCodigo, item.Comprimento, item.Segmentos);
                    corte = _geometryService.CalcularLarguraCorte(polar);
                    dim = _geometryService.CalcularDimensoesAcabadas(polar);

                    var chapaInfo = chapas.Find(x => string.Equals(
                        x.Codigo,
                        item.ChapaCodigo.StartsWith("#") ? item.ChapaCodigo : $"#{item.ChapaCodigo}",
                        StringComparison.OrdinalIgnoreCase));
                    if (chapaInfo != null)
                        peso = _geometryService.CalcularPesoKg(polar, item.Quantidade, chapaInfo);
                }
                catch { }

                return (Item: item, Polar: polar, Corte: corte, Peso: peso, Dim: dim);
            }).ToList();

            int quantidadeTotalPecas = dadosPecas.Sum(d => d.Item.Quantidade);
            double pesoTotalGeral = dadosPecas.Sum(d => d.Peso);

            float AlturaParaPeca(PecaPedidoItem it, DimensoesAcabadas? dim)
            {
                const float alturaBase              = 90f;
                const float alturaPorSegmentoExtra  = 14f;
                const int   segmentosBase           = 3;
                const float alturaMaxima            = 420f;

                // Tamanho mínimo que qualquer segmento pode ocupar no PDF (em pontos).
                // Abaixo disso as cotas ficam ilegíveis.
                const float minSegmentoPts = 25f;

                // Estimativa conservadora da área de desenho disponível após colunas de dados e padding.
                // (A4 portrait - margens - coluna de número - coluna de dados = ~330pt efetivos)
                const float drawWidthEst = 330f;
                // Espaço consumido pelas cotas/anotações ao redor do desenho em cada eixo.
                const float overhead = 60f;

                // 1. Altura base pelo número de segmentos
                int numSegmentos = it.Segmentos?.Count ?? 0;
                float altura = alturaBase + Math.Max(0, numSegmentos - segmentosBase) * alturaPorSegmentoExtra;
                if (it.Segmentos != null && it.Segmentos.Any(s => s.EhCurvo)) altura += 18f;

                // 2. Altura geométrica: garante que o menor segmento renderize com pelo menos minSegmentoPts.
                if (dim.HasValue && it.Segmentos?.Count > 0)
                {
                    double dimW = Math.Max(dim.Value.Largura, 1.0);
                    double dimH = Math.Max(dim.Value.Altura,  1.0);

                    // Menor segmento reto (em mm) — determina a escala mínima necessária.
                    double minSegMm = it.Segmentos
                        .Where(s => !s.EhCurvo && s.Medida > 1)
                        .Select(s => s.Medida)
                        .DefaultIfEmpty(50.0)
                        .Min();

                    // Escala máxima limitada pela largura fixa da coluna de desenho.
                    double maxEscalaW = (drawWidthEst - overhead) / dimW;

                    // Escala necessária para o menor segmento atingir minSegmentoPts.
                    double escalaAlvo = minSegmentoPts / minSegMm;

                    // Se a largura já limita abaixo do alvo, não adianta aumentar a altura.
                    double escalaEfetiva = Math.Min(escalaAlvo, maxEscalaW);

                    // Altura necessária para atingir escalaEfetiva na dimensão vertical da peça.
                    float alturaCalc = (float)(escalaEfetiva * dimH + overhead);
                    altura = Math.Max(altura, alturaCalc);
                }

                return Math.Min(altura, alturaMaxima);
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.0f, Unit.Centimetre);
                    page.PageColor(Colors.White);

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

                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        for (int idx = 0; idx < dadosPecas.Count; idx++)
                        {
                            // Captura local imutável — garantia extra além do 'var' no for.
                            var dado = dadosPecas[idx];
                            var item   = dado.Item;
                            var polar  = dado.Polar;
                            var corte  = dado.Corte;
                            var peso   = dado.Peso;
                            var dim    = dado.Dim;
                            int numero = idx + 1;

                            col.Item().PaddingBottom(6)
                                .Background(idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten5)
                                .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Height(AlturaParaPeca(item, dim))
                                .Row(row =>
                                {
                                    row.ConstantItem(22).AlignMiddle().AlignCenter()
                                        .Text($"{numero:D2}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque).FontColor(Colors.Indigo.Darken4);

                                    row.ConstantItem(1).LineVertical(0.5f).LineColor(Colors.Grey.Lighten2);

                                    // Desenho (SVG) — usa polar já calculado, sem chamar geometryService aqui
                                    row.RelativeItem(7).Padding(3).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas =>
                                    {
                                        if (polar != null)
                                        {
                                            try
                                            {
                                                SkiaRenderer.RenderizarPeca(canvas, new SKSize(size.Width, size.Height), polar, dim, true, _geometryService, fonteCota: 7f, fonteAngulo: 6.5f);
                                            }
                                            catch { canvas.Clear(SKColors.White); }
                                        }
                                        else
                                        {
                                            canvas.Clear(SKColors.White);
                                        }
                                    }));

                                    row.ConstantItem(1).LineVertical(0.5f).LineColor(Colors.Grey.Lighten2);

                                    row.RelativeItem(2).Padding(5).Column(c =>
                                    {
                                        c.Item().Text(item.NomePeca).Bold().FontSize((float)config.RelatorioPedidoFonteDestaque).FontColor(Colors.Indigo.Darken4);

                                        c.Item().PaddingTop(2).Row(r =>
                                        {
                                            r.RelativeItem().Text($"Qtde: {item.Quantidade}").Bold().FontSize((float)config.RelatorioPedidoFonteDestaque);
                                            r.RelativeItem().Text($"Chapa: #{item.ChapaCodigo.Replace("#", "")}").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                        });

                                        c.Item().PaddingTop(3).Text(t =>
                                        {
                                            t.Span("Corte: ").Bold().FontSize((float)config.RelatorioPedidoFonteRotuloCampo);
                                            t.Span($"{corte:F0} mm").Bold().FontColor(Colors.Red.Medium).FontSize((float)config.RelatorioPedidoFonteDestaque);
                                        });

                                        c.Item().Text($"Comprimento: {item.Comprimento:F0} mm").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                        if (dim.HasValue)
                                            c.Item().Text($"Dim.: {dim.Value.Largura:F0} × {dim.Value.Altura:F0} mm").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                        c.Item().Text($"Peso: {peso:F0} kg").FontSize((float)config.RelatorioPedidoFonteTexto).Bold();
                                        c.Item().PaddingTop(4).Row(r =>
                                        {
                                            r.ConstantItem(12).Border(1).BorderColor(Colors.Grey.Darken1).Height(12).Svg(size => RenderizarComoSvg(size.Width, size.Height, canvas => { }));
                                            r.AutoItem().PaddingLeft(4).Text("Cortado").FontSize((float)config.RelatorioPedidoFonteRotuloPeca);
                                        });
                                        if (!string.IsNullOrWhiteSpace(item.Observacao))
                                            c.Item().PaddingTop(2).Text($"Obs.: {item.Observacao}").FontSize((float)config.RelatorioPedidoFonteRotuloCampo).Italic();
                                    });
                                });
                        }

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
