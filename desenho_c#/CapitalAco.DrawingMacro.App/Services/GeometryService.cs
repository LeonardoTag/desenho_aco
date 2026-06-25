using System;
using System.Collections.Generic;
using System.Linq;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    /*
     * CADEIA DE PENSAMENTO (CHAIN OF THOUGHT) - GeometryService:
     * A precisão matemática para corte CNC é inegociável. Por isso:
     * 1. Usamos double para todas as operações trigonométricas para espelhar o motor Python e evitar discrepâncias em ponto flutuante.
     * 2. As conversões de coordenadas usam a lógica de azimutes consecutivos (N=0, E=90, S=180, W=270 graus).
     * 3. A planificação desconta o encurtamento do material em cada dobra (Dedução de Dobra) usando a fórmula:
     *    desconto = tan(angulo / 2) * (raio + espessura / 2).
     * 4. O comprimento desenvolvido total (corte) é a soma dos comprimentos retos mais a compensação de dobra (Bend Allowance):
     *    BA = angulo * (pi / 180) * (raio + K-factor * espessura).
     * 5. O algoritmo de auto-interseção (PerfilCruzaASiMesmo) verifica se quaisquer dois segmentos não adjacentes da linha de centro
     *    se cruzam em 2D usando testes de orientação geométrica (produto vetorial).
     */
    public class GeometryService : IGeometryService
    {
        private readonly ICsvService _csvService;

        public GeometryService(ICsvService csvService)
        {
            _csvService = csvService;
        }

        private Chapa ConsultarChapa(string codigo)
        {
            var codigoFormatado = codigo.StartsWith("#") ? codigo : $"#{codigo.TrimStart('#')}";
            var chapas = _csvService.CarregarChapas();
            var chapa = chapas.FirstOrDefault(c => string.Equals(c.Codigo, codigoFormatado, StringComparison.OrdinalIgnoreCase));
            if (chapa == null)
            {
                throw new InvalidOperationException($"Chapa não encontrada na lista: {codigoFormatado}");
            }
            return chapa;
        }

        public double GetBendAllowance(double anguloDobra, double raioDeDobra, double kFactor, double espessura)
        {
            return anguloDobra * (Math.PI / 180.0) * (raioDeDobra + (kFactor * espessura));
        }

        public double MedidaCentroDeExterna(double medidaExterna, double grau, double espessura, double raioDeDobra)
        {
            if (Math.Abs(grau - 90.0) < 0.001)
            {
                return medidaExterna - espessura / 2.0;
            }
            else
            {
                double grauRad = grau * (Math.PI / 180.0);
                return medidaExterna
                       - (Math.Sin(grauRad / 2.0) * (raioDeDobra + espessura))
                       + Math.Tan(grauRad / 2.0) * (raioDeDobra + (espessura / 2.0));
            }
        }

        public double MedidaCentroDeInterna(double medidaInterna, double grau, double espessura, double raioDeDobra)
        {
            if (Math.Abs(grau - 90.0) < 0.001)
            {
                return medidaInterna + espessura / 2.0;
            }
            else
            {
                double grauRad = grau * (Math.PI / 180.0);
                return medidaInterna
                       - (Math.Tan(grauRad / 2.0) * raioDeDobra)
                       + (Math.Tan(grauRad / 2.0) * (raioDeDobra + (espessura / 2.0)));
            }
        }

        public double DefinirAzimute(string direcao, double grau, double? azimuteAnterior)
        {
            double azimuteDirecao = direcao switch
            {
                "N" => 0,
                "S" => 180,
                "E" => 90,
                "W" => 270,
                _ => 0
            };

            double azimute;
            if (Math.Abs(grau - 90.0) < 0.001)
            {
                azimute = azimuteDirecao;
            }
            else
            {
                if (azimuteAnterior == null)
                {
                    azimute = azimuteDirecao - (90.0 - grau);
                }
                else
                {
                    // Escolhe entre dobrar à direita (ant+grau) ou à esquerda (ant-grau) selecionando
                    // o resultado cujo azimute é angularmente mais próximo da direção indicada pelo usuário.
                    double ant = azimuteAnterior.Value;
                    double optA = (ant + grau) % 360.0;
                    double optB = (ant - grau + 360.0) % 360.0;
                    double distA = Math.Abs(optA - azimuteDirecao);
                    if (distA > 180.0) distA = 360.0 - distA;
                    double distB = Math.Abs(optB - azimuteDirecao);
                    if (distB > 180.0) distB = 360.0 - distB;
                    azimute = distA <= distB ? optA : optB;
                }
            }

            while (azimute < 0)
            {
                azimute += 360.0;
            }
            return azimute % 360.0;
        }

        public List<double> ObterAzimutesDeSegmentos(List<Segmento> segmentos)
        {
            var azimutes = new List<double>();
            double? azimuteAnterior = null;
            foreach (var seg in segmentos)
            {
                double azimute = DefinirAzimute(seg.Direcao, seg.Angulo, azimuteAnterior);
                azimutes.Add(azimute);
                azimuteAnterior = azimute;
            }
            return azimutes;
        }

        public List<double> ObterAngulosDobraDeAzimutes(List<double> azimutes)
        {
            var angulos = new List<double>();
            for (int i = 1; i < azimutes.Count; i++)
            {
                double diff = azimutes[i] - azimutes[i - 1];
                if (diff < 0)
                {
                    diff += 360.0;
                }
                if (diff < 180.0)
                {
                    angulos.Add(diff);
                }
                else
                {
                    angulos.Add(360.0 - diff);
                }
            }
            return angulos;
        }

        public InstrucoesPolares ConverterInstrucoesParaCoordenadasPolares(string chapaCodigo, double comprimento, List<Segmento> segmentos, double? espessuraInformada = null)
        {
            var chapa = ConsultarChapa(chapaCodigo);
            double espessura = chapa.Espessura;
            double raioDeDobra = chapa.RaioDeDobra;
            double kFactor = chapa.KFactor;

            if (espessuraInformada != null && espessuraInformada.Value > 0 && Math.Abs(espessuraInformada.Value - espessura) > 0.0001)
            {
                double proporcao = espessuraInformada.Value / espessura;
                espessura = espessuraInformada.Value;
                raioDeDobra = raioDeDobra * proporcao;
            }

            var coordenadasPolares = new List<(double Azimute, double Comprimento)>();
            int totalSegmentos = segmentos.Count;
            var azimutes = ObterAzimutesDeSegmentos(segmentos);
            var angulosDobra = ObterAngulosDobraDeAzimutes(azimutes);

            for (int n = 0; n < totalSegmentos; n++)
            {
                var seg = segmentos[n];
                double azimute = azimutes[n];
                double dh;

                if (seg.EhCurvo && seg.CurvaInfo != null)
                {
                    var info = seg.CurvaInfo;
                    double r = info.Raio;
                    double a = info.AnguloCurva;
                    string tipoR = info.TipoRaio;

                    double rInner = tipoR == "interno" ? r : r - espessura;
                    double rNeutral = rInner + kFactor * espessura;
                    double aRad = a * (Math.PI / 180.0);

                    double lChord = (a == 360.0) ? 0.0 : 2.0 * rNeutral * Math.Sin(aRad / 2.0);

                    double azStart;
                    bool isCw = true;
                    if (n > 0)
                    {
                        double diff = (azimutes[n] - azimutes[n - 1]) % 360.0;
                        if (diff < 0) diff += 360.0;
                        isCw = diff < 180.0;
                        azStart = azimutes[n - 1];
                    }
                    else
                    {
                        if (segmentos.Count > 1)
                        {
                            double diff = (azimutes[1] - azimutes[0]) % 360.0;
                            if (diff < 0) diff += 360.0;
                            isCw = diff < 180.0;
                        }
                        double aSigned = isCw ? a : -a;
                        azStart = (azimutes[0] - aSigned) % 360.0;
                        if (azStart < 0) azStart += 360.0;
                    }

                    double azimuteChord = (azStart + (isCw ? a : -a) / 2.0) % 360.0;
                    if (azimuteChord < 0) azimuteChord += 360.0;

                    dh = lChord;
                    azimute = azimuteChord;
                }
                else
                {
                    double grauEntrada = 0.0;
                    if (n > 0 && !segmentos[n - 1].EhCurvo)
                    {
                        grauEntrada = angulosDobra[n - 1];
                    }

                    double grauSaida = 0.0;
                    if (n < totalSegmentos - 1 && !segmentos[n + 1].EhCurvo)
                    {
                        grauSaida = angulosDobra[n];
                    }

                    if (Math.Abs(grauEntrada) < 0.001 && Math.Abs(grauSaida) < 0.001)
                    {
                        dh = seg.Medida;
                    }
                    else
                    {
                        double medidaTemp = seg.Medida;
                        if (seg.TipoMedida == "e")
                        {
                            if (Math.Abs(grauEntrada) > 0.001)
                                medidaTemp = MedidaCentroDeExterna(medidaTemp, grauEntrada, espessura, raioDeDobra);
                            if (Math.Abs(grauSaida) > 0.001)
                                medidaTemp = MedidaCentroDeExterna(medidaTemp, grauSaida, espessura, raioDeDobra);
                        }
                        else
                        {
                            if (Math.Abs(grauEntrada) > 0.001)
                                medidaTemp = MedidaCentroDeInterna(medidaTemp, grauEntrada, espessura, raioDeDobra);
                            if (Math.Abs(grauSaida) > 0.001)
                                medidaTemp = MedidaCentroDeInterna(medidaTemp, grauSaida, espessura, raioDeDobra);
                        }
                        dh = medidaTemp;
                    }
                }

                coordenadasPolares.Add((azimute, dh));
            }

            if (coordenadasPolares.Count == 1 && !segmentos[0].EhCurvo)
            {
                coordenadasPolares[0] = (coordenadasPolares[0].Azimute, segmentos[0].Medida);
            }

            return new InstrucoesPolares
            {
                CoordenadasPolares = coordenadasPolares,
                Comprimento = comprimento,
                Espessura = espessura,
                RaioDeDobra = raioDeDobra,
                KFactor = kFactor,
                SegmentosOriginal = segmentos
            };
        }

        public double CalcularLarguraCorte(InstrucoesPolares instrucoes)
        {
            var dados = GerarDadosPlanificacao(instrucoes);
            return Math.Round((double)dados.CorteTotal, 3);
        }

        public double CalcularPesoKg(InstrucoesPolares instrucoes, int quantidade, Chapa chapaInfo)
        {
            double corte = CalcularLarguraCorte(instrucoes);
            double comp = instrucoes.Comprimento;
            double coef = chapaInfo.Coeficiente;
            int qtda = Math.Max(1, quantidade);

            double peso = corte * comp * coef / 1000000.0 * qtda;
            return Math.Ceiling(peso); // Equivalente ao ceil(peso) com arredondamento seguro
        }

        public List<string> VerificarDobrasAbaixoMinima(InstrucoesPolares instrucoes, Chapa chapaInfo)
        {
            double minima = chapaInfo.DobraMinima;
            var avisos = new List<string>();
            if (minima <= 0)
                return avisos;

            var segmentos = instrucoes.SegmentosOriginal;
            var medidas = GerarMedidasInternaExterna(instrucoes);
            for (int segIdx = 0; segIdx < medidas.Count && segIdx < segmentos.Count; segIdx++)
            {
                if (segmentos[segIdx].EhCurvo) continue;

                double interna = medidas[segIdx].Interna;
                if (interna < minima)
                {
                    avisos.Add($"Segmento {segIdx + 1}: medida interna {NumericUtils.FormatarNumero(interna, 1)} mm (mínimo da chapa: {NumericUtils.FormatarNumero(minima, 1)} mm)");
                }
            }

            return avisos;
        }

        public List<(double Livre, double Interna, double Externa)> GerarMedidasInternaExterna(InstrucoesPolares instrucoes)
        {
            var dados = GerarDadosPlanificacao(instrucoes);
            var segmentos = instrucoes.SegmentosOriginal;
            int totalSegmentos = segmentos.Count;
            const int casas = 3;

            var resultado = new List<(double Livre, double Interna, double Externa)>();

            for (int i = 0; i < dados.Trechos.Count; i += 2)
            {
                int segIdx = i / 2;
                var trecho = dados.Trechos[i];
                double medidaLivre = trecho.Comprimento;
                double interna, externa;

                if (trecho.Tipo == "curvo" && trecho.CurvaInfo != null)
                {
                    var info = trecho.CurvaInfo;
                    double rInterno = info.TipoRaio == "interno" ? info.Raio : info.Raio - instrucoes.Espessura;
                    double rExterno = rInterno + instrucoes.Espessura;
                    double aRad = info.AnguloCurva * (Math.PI / 180.0);
                    interna = rInterno * aRad;
                    externa = rExterno * aRad;
                }
                else if (dados.Dobras.Count == 0)
                {
                    interna = medidaLivre;
                    externa = medidaLivre;
                }
                else
                {
                    List<double> angulosSegmento;
                    if (segIdx == 0)
                        angulosSegmento = new List<double> { dados.Dobras[0].AnguloDobra };
                    else if (segIdx == totalSegmentos - 1)
                        angulosSegmento = new List<double> { dados.Dobras[^1].AnguloDobra };
                    else
                        angulosSegmento = new List<double> { dados.Dobras[segIdx - 1].AnguloDobra, dados.Dobras[segIdx].AnguloDobra };

                    (interna, externa) = GerarMedidaInternaExterna(medidaLivre, angulosSegmento, instrucoes.RaioDeDobra, instrucoes.Espessura);
                    (interna, externa) = NormalizarMedidasCota90(interna, externa, angulosSegmento, instrucoes.Espessura, casas);
                }

                resultado.Add((Math.Round(medidaLivre, casas), Math.Round(interna, casas), Math.Round(externa, casas)));
            }

            return resultado;
        }

        private (double Interna, double Externa) GerarMedidaInternaExterna(double medidaLivre, List<double> angulosDobra, double raioDeDobra, double espessura)
        {
            double somaInterna = 0.0;
            double somaExterna = 0.0;
            foreach (double angulo in angulosDobra)
            {
                if (angulo < 90.0)
                {
                    somaInterna += (angulo * Math.PI * raioDeDobra) / 360.0;
                    somaExterna += (angulo * Math.PI * (raioDeDobra + espessura)) / 360.0;
                }
                else
                {
                    somaInterna += raioDeDobra;
                    somaExterna += raioDeDobra + espessura;
                }
            }
            return (medidaLivre + somaInterna, medidaLivre + somaExterna);
        }

        private (double Interna, double Externa) NormalizarMedidasCota90(double interna, double externa, List<double> angulosDobra, double espessura, int casas)
        {
            if (angulosDobra.Count == 0 || !angulosDobra.All(a => Math.Abs(a - 90.0) < 0.5))
                return (interna, externa);

            if (angulosDobra.Count == 1)
                interna = Math.Round(externa - espessura, casas);
            else if (angulosDobra.Count == 2)
                interna = Math.Round(externa - 2.0 * espessura, casas);

            return (interna, externa);
        }

        public List<(double X, double Y)> GerarCoordenadasRetangularesParciais(List<(double Azimute, double Comprimento)> coordenadasPolares)
        {
            var parciais = new List<(double X, double Y)>();
            foreach (var (azimute, dh) in coordenadasPolares)
            {
                double rad = azimute * (Math.PI / 180.0);
                double xParc = Math.Sin(rad) * dh;
                double yParc = -Math.Cos(rad) * dh;
                parciais.Add((xParc, yParc));
            }
            return parciais;
        }

        public List<(double X, double Y)> GerarCoordenadasRetangularesAbsolutas(List<(double X, double Y)> coordenadasParciais)
        {
            var absolutas = new List<(double X, double Y)> { (0.0, 0.0) };
            foreach (var (xParc, yParc) in coordenadasParciais)
            {
                var (xAtual, yAtual) = absolutas[^1];
                absolutas.Add((xAtual + xParc, yAtual + yParc));
            }
            return absolutas;
        }

        public DimensoesAcabadas? CalcularDimensoesAcabadas(InstrucoesPolares instrucoes)
        {
            if (instrucoes.CoordenadasPolares.Count == 0)
                return null;

            var parciais = GerarCoordenadasRetangularesParciais(instrucoes.CoordenadasPolares);
            var absolutas = GerarCoordenadasRetangularesAbsolutas(parciais);

            if (instrucoes.Espessura > 0 && absolutas.Count >= 2)
            {
                absolutas = CoordenadasExternasPerfil(absolutas, instrucoes.Espessura);
            }

            // Segmentos curvos podem se afastar da corda (no caso de um tubo calandrado a 360°, a corda mede zero),
            // então inflamos a caixa delimitadora pelo raio externo da curva antes de medir.
            var pontosParaDimensao = new List<(double X, double Y)>(absolutas);
            for (int i = 0; i < instrucoes.SegmentosOriginal.Count && i < absolutas.Count; i++)
            {
                var seg = instrucoes.SegmentosOriginal[i];
                if (!seg.EhCurvo || seg.CurvaInfo == null) continue;

                double rExterno = seg.CurvaInfo.TipoRaio == "interno" ? seg.CurvaInfo.Raio + instrucoes.Espessura : seg.CurvaInfo.Raio;
                var (cx, cy) = absolutas[i];
                pontosParaDimensao.Add((cx - rExterno, cy - rExterno));
                pontosParaDimensao.Add((cx + rExterno, cy + rExterno));
            }

            var (dim, _) = GerarDimensoesTotaisEPontoInicial(pontosParaDimensao);
            return new DimensoesAcabadas { Largura = Math.Round(dim.Largura, 1), Altura = Math.Round(dim.Altura, 1) };
        }

        public bool PerfilCruzaASiMesmo(string chapaCodigo, double comprimento, List<Segmento> segmentos)
        {
            try
            {
                var polar = ConverterInstrucoesParaCoordenadasPolares(chapaCodigo, comprimento, segmentos);
                var parciais = GerarCoordenadasRetangularesParciais(polar.CoordenadasPolares);
                var coords = GerarCoordenadasRetangularesAbsolutas(parciais);

                int n = coords.Count - 1;
                if (n < 3)
                    return false;

                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 2; j < n; j++)
                    {
                        if (i == 0 && j == n - 1)
                            continue;

                        if (SegmentosCentroCruzam(coords[i], coords[i + 1], coords[j], coords[j + 1]))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public DadosPlanificacao GerarDadosPlanificacao(InstrucoesPolares instrucoes)
        {
            var coordenadasPolares = instrucoes.CoordenadasPolares;
            double espessura = instrucoes.Espessura;
            double raioDeDobra = instrucoes.RaioDeDobra;
            double kFactor = instrucoes.KFactor;
            var segmentosOriginal = instrucoes.SegmentosOriginal;

            var dobrasPolares = ObterAngulosDobraDeAzimutes(coordenadasPolares.Select(cp => cp.Azimute).ToList());
            var azimutes = ObterAzimutesDeSegmentos(segmentosOriginal);

            var dobras = new List<DobraInfo>();
            for (int i = 0; i < dobrasPolares.Count; i++)
            {
                double az0 = azimutes[i];
                double az1 = azimutes[i + 1];
                double diff = az1 - az0;
                if (diff < 0) diff += 360.0;
                string sentido = diff < 180.0 ? "h" : "a";
                dobras.Add(new DobraInfo { AnguloDobra = dobrasPolares[i], Sentido = sentido });
            }

            var seccoesCorte = CalcularSeccoesCorte(coordenadasPolares, dobras, raioDeDobra, espessura, kFactor, segmentosOriginal);

            int nSegs = coordenadasPolares.Count;
            var trechos = new List<TrechoPlanificacao>();
            int indiceDobra = 0;

            for (int i = 0; i < seccoesCorte.Count; i++)
            {
                if (i % 2 == 0)
                {
                    int segIdx = i / 2;
                    bool isCurv = segIdx < segmentosOriginal.Count && segmentosOriginal[segIdx].EhCurvo;
                    trechos.Add(new TrechoPlanificacao
                    {
                        Tipo = isCurv ? "curvo" : "reta",
                        Comprimento = seccoesCorte[i],
                        CurvaInfo = isCurv ? segmentosOriginal[segIdx].CurvaInfo : null
                    });
                }
                else
                {
                    var dobra = dobras[indiceDobra++];
                    trechos.Add(new TrechoPlanificacao
                    {
                        Tipo = "dobra",
                        Comprimento = seccoesCorte[i],
                        AnguloDobra = dobra.AnguloDobra,
                        Sentido = dobra.Sentido
                    });
                }
            }

            double acumulado = 0.0;
            var marcasDobra = new List<MarcaDobra>();
            var marcasCalandragem = new List<MarcaCalandragem>();
            var posicoesReferencia = new List<double> { 0.0 };

            foreach (var trecho in trechos)
            {
                if (trecho.Tipo == "dobra")
                {
                    if (trecho.Comprimento > 0)
                    {
                        double centro = acumulado + trecho.Comprimento / 2.0;
                        marcasDobra.Add(new MarcaDobra
                        {
                            Posicao = (int)Math.Round(centro),
                            AnguloDobra = trecho.AnguloDobra ?? 0.0,
                            Sentido = trecho.Sentido ?? "h"
                        });
                        posicoesReferencia.Add(centro);
                    }
                }
                else if (trecho.Tipo == "curvo" && trecho.CurvaInfo != null)
                {
                    double startPos = acumulado;
                    double endPos = acumulado + trecho.Comprimento;
                    marcasCalandragem.Add(new MarcaCalandragem
                    {
                        PosicaoInicio = (int)Math.Round(startPos),
                        PosicaoFim = (int)Math.Round(endPos),
                        Raio = trecho.CurvaInfo.Raio,
                        AnguloCurva = trecho.CurvaInfo.AnguloCurva,
                        TipoRaio = trecho.CurvaInfo.TipoRaio
                    });
                    posicoesReferencia.Add(startPos);
                    posicoesReferencia.Add(endPos);
                }

                acumulado += trecho.Comprimento;
            }

            posicoesReferencia.Add(acumulado);
            posicoesReferencia = posicoesReferencia.Select(p => Math.Round(p, 0)).Distinct().OrderBy(p => p).ToList();

            var trechosCadeia = new List<TrechoCadeia>();
            for (int i = 0; i < posicoesReferencia.Count - 1; i++)
            {
                double inicioF = posicoesReferencia[i];
                double fimF = posicoesReferencia[i + 1];
                int comp = (int)Math.Round(fimF - inicioF);
                if (comp > 0)
                {
                    trechosCadeia.Add(new TrechoCadeia
                    {
                        Inicio = 0,
                        Fim = 0,
                        Comprimento = comp
                    });
                }
            }

            var posicoesOrdenadas = new List<int> { 0 };
            foreach (var trecho in trechosCadeia)
            {
                posicoesOrdenadas.Add(posicoesOrdenadas[posicoesOrdenadas.Count - 1] + trecho.Comprimento);
            }
            int corteTotal = posicoesOrdenadas[posicoesOrdenadas.Count - 1];

            for (int i = 0; i < trechosCadeia.Count; i++)
            {
                trechosCadeia[i].Inicio = posicoesOrdenadas[i];
                trechosCadeia[i].Fim = posicoesOrdenadas[i + 1];
            }

            int MapPos(double rawPos)
            {
                if (acumulado == 0) return 0;
                return (int)Math.Round((rawPos / acumulado) * corteTotal);
            }

            foreach (var marca in marcasDobra)
            {
                marca.Posicao = MapPos(marca.Posicao);
            }

            foreach (var mc in marcasCalandragem)
            {
                mc.PosicaoInicio = MapPos(mc.PosicaoInicio);
                mc.PosicaoFim = MapPos(mc.PosicaoFim);
            }

            return new DadosPlanificacao
            {
                CorteTotal = corteTotal,
                Trechos = trechos,
                TrechosCadeia = trechosCadeia,
                Cadeia = trechosCadeia.Select(t => t.Comprimento).ToList(),
                PosicoesOrdenadas = posicoesOrdenadas,
                Dobras = dobras,
                MarcasDobra = marcasDobra,
                MarcasCalandragem = marcasCalandragem,
                Espessura = espessura,
                RaioDeDobra = raioDeDobra
            };
        }

        private List<double> CalcularSeccoesCorte(
            List<(double Azimute, double Comprimento)> coordenadasPolares,
            List<DobraInfo> dobras,
            double raioDeDobra,
            double espessura,
            double kFactor,
            List<Segmento> segmentosOriginal)
        {
            var seccoesCorte = new List<double>();
            if (coordenadasPolares.Count == 0)
                return seccoesCorte;

            int n = coordenadasPolares.Count;
            var bendAllowances = new double[n - 1];
            var anteriorDeductions = new double[n];
            var posteriorDeductions = new double[n];

            var isCurved = new bool[n];
            for (int i = 0; i < Math.Min(n, segmentosOriginal.Count); i++)
            {
                isCurved[i] = segmentosOriginal[i].EhCurvo;
            }

            for (int j = 0; j < n - 1; j++)
            {
                if (isCurved[j] || isCurved[j + 1])
                {
                    bendAllowances[j] = 0.0;
                    posteriorDeductions[j] = 0.0;
                    anteriorDeductions[j + 1] = 0.0;
                }
                else
                {
                    double anguloDobra = dobras[j].AnguloDobra;
                    bendAllowances[j] = GetBendAllowance(anguloDobra, raioDeDobra, kFactor, espessura);
                    double desconto = Math.Tan((anguloDobra / 2.0) * (Math.PI / 180.0)) * (raioDeDobra + espessura / 2.0);
                    posteriorDeductions[j] = desconto;
                    anteriorDeductions[j + 1] = desconto;
                }
            }

            for (int i = 0; i < n; i++)
            {
                double dh = coordenadasPolares[i].Comprimento;
                double flatLen;
                if (isCurved[i])
                {
                    flatLen = i < segmentosOriginal.Count ? segmentosOriginal[i].Medida : dh;
                }
                else
                {
                    flatLen = dh - anteriorDeductions[i] - posteriorDeductions[i];
                }

                seccoesCorte.Add(Math.Round(flatLen, 3));
                if (i < n - 1)
                {
                    seccoesCorte.Add(Math.Round(bendAllowances[i], 3));
                }
            }

            return seccoesCorte;
        }

        private List<(double X, double Y)> CoordenadasExternasPerfil(List<(double X, double Y)> coordenadas, double espessura)
        {
            double meiaEspessura = espessura / 2.0;
            var externas = new List<(double X, double Y)>();

            for (int i = 0; i < coordenadas.Count; i++)
            {
                var deslocamentos = new List<(double DX, double DY)>();
                if (i > 0)
                {
                    deslocamentos.Add(DeslocamentoExternoSegmento(i - 1, coordenadas, meiaEspessura));
                }
                if (i < coordenadas.Count - 1)
                {
                    deslocamentos.Add(DeslocamentoExternoSegmento(i, coordenadas, meiaEspessura));
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

        private (double DX, double DY) DeslocamentoExternoSegmento(int index, List<(double X, double Y)> coordenadas, double meiaEspessura)
        {
            var (x0, y0) = coordenadas[index];
            var (x1, y1) = coordenadas[index + 1];
            var (nx, ny) = NormalUnitariaEsquerda(x1 - x0, y1 - y0);
            int ladoInterno = DeterminarLadoInternoSegmento(index, coordenadas);
            double fator = -ladoInterno * meiaEspessura;
            return (nx * fator, ny * fator);
        }

        public int DeterminarLadoInternoSegmento(int n, List<(double X, double Y)> coordenadas)
        {
            var (x0, y0) = coordenadas[n];
            var (x1, y1) = coordenadas[n + 1];
            double mx = (x0 + x1) / 2.0;
            double my = (y0 + y1) / 2.0;
            double dx = x1 - x0;
            double dy = y1 - y0;
            var (nx, ny) = NormalUnitariaEsquerda(dx, dy);

            var scores = new List<double>();
            if (n > 0)
            {
                scores.Add((coordenadas[n - 1].X - mx) * nx + (coordenadas[n - 1].Y - my) * ny);
            }
            if (n + 2 < coordenadas.Count)
            {
                scores.Add((coordenadas[n + 2].X - mx) * nx + (coordenadas[n + 2].Y - my) * ny);
            }

            if (scores.Count == 0)
                return 1;

            return scores.Average() >= 0 ? 1 : -1;
        }

        private (double X, double Y) NormalUnitariaEsquerda(double dx, double dy)
        {
            double comp = Math.Sqrt(dx * dx + dy * dy);
            if (comp == 0)
                return (0.0, 0.0);
            return (-dy / comp, dx / comp);
        }

        private (DimensoesAcabadas BBox, (double MinX, double MinY) BMin) GerarDimensoesTotaisEPontoInicial(List<(double X, double Y)> coordenadas)
        {
            if (coordenadas.Count == 0)
            {
                return (new DimensoesAcabadas(), (0.0, 0.0));
            }

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

            return (new DimensoesAcabadas { Largura = maxX - minX, Altura = maxY - minY }, (minX, minY));
        }

        private bool SegmentosCentroCruzam((double X, double Y) a1, (double X, double Y) a2, (double X, double Y) b1, (double X, double Y) b2)
        {
            double o1 = Orientacao2d(a1, a2, b1);
            double o2 = Orientacao2d(a1, a2, b2);
            double o3 = Orientacao2d(b1, b2, a1);
            double o4 = Orientacao2d(b1, b2, a2);

            if (Math.Abs(o1) < 1e-9 && Math.Abs(o2) < 1e-9 && Math.Abs(o3) < 1e-9 && Math.Abs(o4) < 1e-9)
            {
                return false;
            }

            return (o1 > 0 != o2 > 0) && (o3 > 0 != o4 > 0);
        }

        private double Orientacao2d((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }
}
