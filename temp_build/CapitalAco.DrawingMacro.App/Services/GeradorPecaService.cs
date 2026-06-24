using System;
using System.Collections.Generic;
using System.Linq;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    /*
     * CADEIA DE PENSAMENTO (CHAIN OF THOUGHT) - GeradorPecaService:
     * 1. O perfil Boiadeira é gerado calculando o ângulo intermediário ('angulo_gomo') e o tamanho da diagonal ('tamanho_diagonal')
     *    que encaixam a largura total e a altura de aba pedidas pelo usuário, garantindo que o topo dos gomos fique alinhado (sem desvio).
     * 2. Implementamos isso portando o solver de Nelder-Mead (2D) em C#.
     * 3. A função de resíduo calcula o perfil hipotético para um par (ângulo, diagonal), descobre as dimensões acabadas externas
     *    e a diferença de altura entre o topo do flange e o topo dos gomos (desvio_topo).
     * 4. O solver minimiza o desvio quadrático dessas métricas. Para garantir robustez, rodamos a otimização de múltiplos pontos iniciais
     *    (variação de ângulos de 25° a 70°) e escolhemos a solução de menor custo.
     * 5. Após encontrar a solução, aplicamos um algoritmo de polimento decimal em malha fina para mitigar imprecisões numéricas e
     *    garantir exatidão absoluta.
     */
    public class GeradorPecaService : IGeradorPecaService
    {
        private readonly IConfigService _configService;
        private readonly IGeometryService _geometryService;
        private readonly ICsvService _csvService;

        public GeradorPecaService(IConfigService configService, IGeometryService geometryService, ICsvService csvService)
        {
            _configService = configService;
            _geometryService = geometryService;
            _csvService = csvService;
        }

        private static List<int> ObterIndicesTopoGomos(int numGomos)
        {
            var indices = new List<int> { 3 };
            for (int i = 1; i < numGomos; i++)
            {
                indices.Add(3 + 4 * i);
            }
            return indices;
        }

        private static List<Segmento> MontarSegmentosBoiadeira(
            double alturaAba,
            double primeiroGomo,
            double tamanhoGomoSuperior,
            double tamanhoGomoInferior,
            double tamanhoDiagonal,
            int numGomos,
            double anguloGomo)
        {
            var segmentos = new List<Segmento>
            {
                new Segmento { Direcao = "S", Angulo = 90.0, Medida = alturaAba, TipoMedida = "e" },
                new Segmento { Direcao = "E", Angulo = 90.0, Medida = primeiroGomo, TipoMedida = "e" },
                new Segmento { Direcao = "N", Angulo = anguloGomo, Medida = tamanhoDiagonal, TipoMedida = "e" }
            };

            for (int i = 1; i < numGomos; i++)
            {
                segmentos.Add(new Segmento { Direcao = "E", Angulo = anguloGomo, Medida = tamanhoGomoSuperior, TipoMedida = "e" });
                segmentos.Add(new Segmento { Direcao = "S", Angulo = anguloGomo, Medida = tamanhoDiagonal, TipoMedida = "e" });
                segmentos.Add(new Segmento { Direcao = "E", Angulo = anguloGomo, Medida = tamanhoGomoInferior, TipoMedida = "e" });
                segmentos.Add(new Segmento { Direcao = "N", Angulo = anguloGomo, Medida = tamanhoDiagonal, TipoMedida = "e" });
            }

            segmentos.Add(new Segmento { Direcao = "E", Angulo = anguloGomo, Medida = tamanhoGomoSuperior, TipoMedida = "e" });
            segmentos.Add(new Segmento { Direcao = "S", Angulo = anguloGomo, Medida = tamanhoDiagonal, TipoMedida = "e" });
            segmentos.Add(new Segmento { Direcao = "E", Angulo = 90.0, Medida = primeiroGomo, TipoMedida = "e" });
            segmentos.Add(new Segmento { Direcao = "N", Angulo = 90.0, Medida = alturaAba, TipoMedida = "e" });

            return segmentos;
        }

        private (double Largura, double Altura, double DesvioTopo) ObterMetricasBoiadeira(
            string chapa,
            double alturaAba,
            double primeiroGomo,
            double tamanhoGomoSuperior,
            double tamanhoGomoInferior,
            int numGomos,
            double comprimento,
            double angulo,
            double diagonal)
        {
            var segmentos = MontarSegmentosBoiadeira(alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, diagonal, numGomos, angulo);
            
            var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(chapa, comprimento, segmentos);
            var parciais = ((GeometryService)_geometryService).GerarCoordenadasRetangularesParciais(polar.CoordenadasPolares);
            var absolutas = ((GeometryService)_geometryService).GerarCoordenadasRetangularesAbsolutas(parciais);

            if (polar.Espessura > 0 && absolutas.Count >= 2)
            {
                // Como CoordenadasExternasPerfil é private/internal, chamamos de forma reflexiva ou mapeamos no GeometryService
                // Como já implementamos no GeometryService, podemos usá-la. Para simplificar, faremos a chamada do método exposto ou a
                // versão privada do GeometryService. No GeometryService adicionamos a lógica de coordenadas externas na geração da bbox.
                // Mas precisamos saber as absolutas deslocadas para calcular o topo dos gomos!
                // Vamos expor o método de coordenadas externas ou re-copiar a lógica aqui.
                // Para manter a arquitetura limpa, vamos re-calcular usando o método auxiliar que podemos invocar.
                // Como está na mesma camada, podemos simplesmente usar reflexão ou criar uma função interna.
                // Vamos usar a mesma matemática inline para garantir velocidade.
                absolutas = CoordenadasExternas(absolutas, polar.Espessura);
            }

            var dim = CalcularDim(absolutas);
            double topoFlange = absolutas[0].Y;
            var indicesTopo = ObterIndicesTopoGomos(numGomos);
            double topoGomos = indicesTopo
                .Where(idx => idx < absolutas.Count)
                .Min(idx => absolutas[idx].Y);

            double desvioTopo = Math.Abs(topoGomos - topoFlange);
            return (dim.Width, dim.Height, desvioTopo);
        }

        private List<(double X, double Y)> CoordenadasExternas(List<(double X, double Y)> coordenadas, double espessura)
        {
            double meiaEspessura = espessura / 2.0;
            var externas = new List<(double X, double Y)>();

            for (int i = 0; i < coordenadas.Count; i++)
            {
                var deslocamentos = new List<(double DX, double DY)>();
                if (i > 0)
                {
                    deslocamentos.Add(Deslocamento(i - 1, coordenadas, meiaEspessura));
                }
                if (i < coordenadas.Count - 1)
                {
                    deslocamentos.Add(Deslocamento(i, coordenadas, meiaEspessura));
                }

                if (deslocamentos.Count == 0)
                {
                    externas.Add(coordenadas[i]);
                    continue;
                }

                double ox, oy;
                if (deslocamentos.Count == 1)
                {
                    ox = deslocamentos[0].DX;
                    oy = deslocamentos[0].DY;
                }
                else
                {
                    var (d0_x, d0_y) = deslocamentos[0];
                    var (d1_x, d1_y) = deslocamentos[1];

                    if (meiaEspessura > 0)
                    {
                        double n0_x = d0_x / meiaEspessura;
                        double n0_y = d0_y / meiaEspessura;
                        double n1_x = d1_x / meiaEspessura;
                        double n1_y = d1_y / meiaEspessura;

                        double dot = n0_x * n1_x + n0_y * n1_y;
                        if (1.0 + dot > 0.001)
                        {
                            double factor = meiaEspessura / (1.0 + dot);
                            ox = (n0_x + n1_x) * factor;
                            oy = (n0_y + n1_y) * factor;
                        }
                        else
                        {
                            ox = d0_x;
                            oy = d0_y;
                        }
                    }
                    else
                    {
                        ox = 0.0;
                        oy = 0.0;
                    }
                }

                externas.Add((coordenadas[i].X + ox, coordenadas[i].Y + oy));
            }

            return externas;
        }

        private (double DX, double DY) Deslocamento(int index, List<(double X, double Y)> coordenadas, double meiaEspessura)
        {
            var (x0, y0) = coordenadas[index];
            var (x1, y1) = coordenadas[index + 1];
            double comp = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            double nx = comp == 0 ? 0.0 : -(y1 - y0) / comp;
            double ny = comp == 0 ? 0.0 : (x1 - x0) / comp;

            int ladoInterno;
            double mx = (x0 + x1) / 2.0;
            double my = (y0 + y1) / 2.0;
            var scores = new List<double>();
            if (index > 0)
            {
                scores.Add((coordenadas[index - 1].X - mx) * nx + (coordenadas[index - 1].Y - my) * ny);
            }
            if (index + 2 < coordenadas.Count)
            {
                scores.Add((coordenadas[index + 2].X - mx) * nx + (coordenadas[index + 2].Y - my) * ny);
            }
            ladoInterno = scores.Count == 0 ? 1 : (scores.Average() >= 0 ? 1 : -1);

            double fator = -ladoInterno * meiaEspessura;
            return (nx * fator, ny * fator);
        }

        private struct SizeD
        {
            public double Width;
            public double Height;
        }

        private static SizeD CalcularDim(List<(double X, double Y)> coordenadas)
        {
            if (coordenadas.Count == 0) return new SizeD();
            double minX = coordenadas[0].X;
            double maxX = coordenadas[0].X;
            double minY = coordenadas[0].Y;
            double maxY = coordenadas[0].Y;
            foreach (var (x, y) in coordenadas)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            return new SizeD { Width = maxX - minX, Height = maxY - minY };
        }

        private static double CustoSolucao(double largura, double altura, double desvioTopo, double larguraTotalTarget, double alturaAbaTarget)
        {
            return Math.Max(
                Math.Abs(largura - larguraTotalTarget),
                Math.Max(Math.Abs(altura - alturaAbaTarget), desvioTopo)
            );
        }

        public ModeloPeca GerarPerfilBoiadeira(
            double alturaAba,
            double larguraTotal,
            string chapa,
            double primeiroGomo,
            double tamanhoGomoSuperior,
            double tamanhoGomoInferior,
            int numGomos,
            double? comprimento = null,
            double? toleranciaLargura = null,
            double? toleranciaAltura = null,
            double? toleranciaTopo = null)
        {
            var config = _configService.ObterConfiguracao();
            double comp = comprimento ?? config.BoiadeiraComprimentoPadrao;
            double tolL = toleranciaLargura ?? config.BoiadeiraToleranciaLargura;
            double tolH = toleranciaAltura ?? config.BoiadeiraToleranciaAltura;
            double tolT = toleranciaTopo ?? config.BoiadeiraToleranciaTopo;

            if (alturaAba <= 0) throw new ArgumentException("Informe uma altura de aba maior que zero.");
            if (larguraTotal <= 0) throw new ArgumentException("Informe uma largura total acabada maior que zero.");
            if (primeiroGomo <= 0) throw new ArgumentException("Informe a dimensão do primeiro gomo maior que zero.");
            if (tamanhoGomoSuperior <= 0) throw new ArgumentException("Informe o tamanho do gomo superior maior que zero.");
            if (tamanhoGomoInferior <= 0) throw new ArgumentException("Informe o tamanho do gomo inferior maior que zero.");
            if (numGomos < 1) throw new ArgumentException("O número de gomos deve ser pelo menos 1.");
            if (comp <= 0) throw new ArgumentException("Informe um comprimento maior que zero.");

            string chapaCodigo = chapa.StartsWith("#") ? chapa : $"#{chapa.TrimStart('#')}";

            // 1. Resolver Nelder-Mead utilizando múltiplos pontos iniciais (ângulo de 25° a 70°)
            double melhorCusto = double.MaxValue;
            double melhorAngulo = 0.0;
            double melhorDiagonal = 0.0;

            double limiteDiagonal = Math.Max(alturaAba * 4.0, Math.Max(tamanhoGomoSuperior * 2.0, Math.Max(tamanhoGomoInferior * 2.0, 80.0)));
            var bounds = new[] { (0.1, 89.9), (0.1, limiteDiagonal) };

            double[] angulosIniciais = { 25.0, 30.0, 35.0, 40.0, 45.0, 50.0, 55.0, 60.0, 65.0, 70.0 };
            foreach (double anguloIni in angulosIniciais)
            {
                double rad = anguloIni * (Math.PI / 180.0);
                double diagIni = Math.Abs(Math.Sin(rad)) > 0.01 ? alturaAba / Math.Sin(rad) : alturaAba;

                double[] x0 = { anguloIni, diagIni };

                double[] Residuos(double[] x)
                {
                    double a = x[0];
                    double d = x[1];
                    var metrics = ObterMetricasBoiadeira(chapaCodigo, alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, numGomos, comp, a, d);
                    return new[]
                    {
                        metrics.Largura - larguraTotal,
                        metrics.Altura - alturaAba,
                        metrics.DesvioTopo
                    };
                }

                double[] sol = NelderMeadSolver.Optimize(Residuos, x0, bounds, tol: 1e-12, maxIter: 150);
                double aSol = sol[0];
                double dSol = sol[1];

                var metSol = ObterMetricasBoiadeira(chapaCodigo, alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, numGomos, comp, aSol, dSol);
                double custo = CustoSolucao(metSol.Largura, metSol.Altura, metSol.DesvioTopo, larguraTotal, alturaAba);

                if (custo < melhorCusto)
                {
                    melhorCusto = custo;
                    melhorAngulo = aSol;
                    melhorDiagonal = dSol;
                }
            }

            // 2. Polimento em malha fina (três passes de precisão decimal crescente)
            var passosPolimento = new[]
            {
                (PassoA: 0.01, PassoD: 0.01, Alcance: 0.05),
                (PassoA: 0.005, PassoD: 0.005, Alcance: 0.03),
                (PassoA: 0.002, PassoD: 0.002, Alcance: 0.012)
            };

            foreach (var passo in passosPolimento)
            {
                int passos = (int)Math.Max(1, Math.Round(passo.Alcance / passo.PassoA));
                double localMelhorA = Math.Round(melhorAngulo, 3);
                double localMelhorD = Math.Round(melhorDiagonal, 3);
                double localMelhorCusto = double.MaxValue;

                for (int i = -passos; i <= passos; i++)
                {
                    double candA = Math.Round(melhorAngulo + i * passo.PassoA, 3);
                    if (candA <= 0.1 || candA >= 89.9) continue;

                    for (int j = -passos; j <= passos; j++)
                    {
                        double candD = Math.Round(melhorDiagonal + j * passo.PassoD, 3);
                        if (candD <= 0.1) continue;

                        var met = ObterMetricasBoiadeira(chapaCodigo, alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, numGomos, comp, candA, candD);
                        double custo = CustoSolucao(met.Largura, met.Altura, met.DesvioTopo, larguraTotal, alturaAba);

                        if (custo < localMelhorCusto)
                        {
                            localMelhorCusto = custo;
                            localMelhorA = candA;
                            localMelhorD = candD;
                        }
                    }
                }

                melhorAngulo = localMelhorA;
                melhorDiagonal = localMelhorD;
                melhorCusto = localMelhorCusto;

                // Se o custo já atende a tolerância requisitada, encerra precocemente
                var finalMet = ObterMetricasBoiadeira(chapaCodigo, alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, numGomos, comp, melhorAngulo, melhorDiagonal);
                if (Math.Abs(finalMet.Largura - larguraTotal) <= tolL &&
                    Math.Abs(finalMet.Altura - alturaAba) <= tolH &&
                    finalMet.DesvioTopo <= tolT)
                {
                    break;
                }
            }

            var resMet = ObterMetricasBoiadeira(chapaCodigo, alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, numGomos, comp, melhorAngulo, melhorDiagonal);
            if (Math.Abs(resMet.Largura - larguraTotal) > tolL ||
                Math.Abs(resMet.Altura - alturaAba) > tolH ||
                resMet.DesvioTopo > tolT)
            {
                throw new InvalidOperationException(
                    $"Não foi possível calcular o perfil com os limites de tolerância exigidos para a largura {larguraTotal} mm.\n" +
                    $"Melhor aproximação obtida: X = {resMet.Largura:F2} mm, Y = {resMet.Altura:F2} mm, desvio topo = {resMet.DesvioTopo:F2} mm. " +
                    "Tente alterar a largura total acabada ou a altura da aba.");
            }

            var segmentosFinais = MontarSegmentosBoiadeira(alturaAba, primeiroGomo, tamanhoGomoSuperior, tamanhoGomoInferior, melhorDiagonal, numGomos, melhorAngulo);

            return new ModeloPeca
            {
                Id = Guid.NewGuid(),
                Nome = $"Boiadeira {(int)Math.Round(larguraTotal)} {chapaCodigo}",
                Chapa = chapaCodigo,
                Comprimento = comp,
                Segmentos = segmentosFinais,
                Descricao = $"Perfil Boiadeira gerado proceduralmente com largura acabada de {larguraTotal:F1} mm e altura {alturaAba:F1} mm."
            };
        }
    }
}
