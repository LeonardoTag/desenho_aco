using System;
using System.Collections.Generic;
using Serilog;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public static class GeometryTests
    {
        public static void ExecutarTestes(IGeometryService geometryService, IGeradorPecaService geradorPecaService)
        {
            Log.Information(">>> INICIANDO TESTES GEOMÉTRICOS E MATEMÁTICOS DE REGRESSÃO (FASE 3) <<<");

            try
            {
                TestarNumericUtils();
                TestarCompensacaoDobra(geometryService);
                TestarPlanificacaoPerfilL(geometryService);
                TestarGeradorBoiadeira(geradorPecaService, geometryService);

                Log.Information(">>> TODOS OS TESTES GEOMÉTRICOS PASSARAM COM EXCELÊNCIA <<<");
            }
            catch (Exception ex)
            {
                Log.Error(ex, ">>> FALHA CRÍTICA NOS TESTES GEOMÉTRICOS DE REGRESSÃO <<<");
                throw;
            }
        }

        private static void TestarNumericUtils()
        {
            Log.Information("Testando NumericUtils...");

            Assert(NumericUtils.ParseNumero("12.5") == 12.5, "ParseNumero '12.5'");
            Assert(NumericUtils.ParseNumero("12,5") == 12.5, "ParseNumero '12,5'");
            Assert(NumericUtils.ParseNumero("1.250,55") == 1250.55, "ParseNumero '1.250,55'");
            Assert(NumericUtils.ParseNumero("") == null, "ParseNumero vazio");

            Assert(NumericUtils.FormatarNumero(12.5, 1) == "12,5", "FormatarNumero '12,5'");
            Assert(NumericUtils.FormatarNumero(12.00, 0) == "12", "FormatarNumero '12'");
            Assert(NumericUtils.FormatarCompacto(10.0) == "10", "FormatarCompacto '10'");
            Assert(NumericUtils.FormatarCompacto(10.25) == "10,25", "FormatarCompacto '10,25'");
        }

        private static void TestarCompensacaoDobra(IGeometryService geo)
        {
            Log.Information("Testando formulas de dobra...");

            // Teste de Bend Allowance (BA)
            // Para 90 graus, R=1.2, K=0.165, T=2.0
            double ba = geo.GetBendAllowance(90.0, 1.2, 0.165, 2.0);
            double esperadoBa = 90.0 * (Math.PI / 180.0) * (1.2 + (0.165 * 2.0));
            Assert(Math.Abs(ba - esperadoBa) < 1e-9, $"GetBendAllowance esperado {esperadoBa}, obtido {ba}");

            // Teste de dedução de dobra em 90 graus (Medida Centro De Externa)
            double centroExterna = geo.MedidaCentroDeExterna(50.0, 90.0, 2.0, 1.2);
            double esperadoCentroExt = 50.0 - 2.0 / 2.0; // 49.0
            Assert(Math.Abs(centroExterna - esperadoCentroExt) < 1e-9, $"MedidaCentroDeExterna esperado {esperadoCentroExt}, obtido {centroExterna}");

            // Teste de dedução de dobra em 90 graus (Medida Centro De Interna)
            double centroInterna = geo.MedidaCentroDeInterna(50.0, 90.0, 2.0, 1.2);
            double esperadoCentroInt = 50.0 + 2.0 / 2.0; // 51.0
            Assert(Math.Abs(centroInterna - esperadoCentroInt) < 1e-9, $"MedidaCentroDeInterna esperado {esperadoCentroInt}, obtido {centroInterna}");
        }

        private static void TestarPlanificacaoPerfilL(IGeometryService geo)
        {
            Log.Information("Testando planificacao de perfil em L (90 graus)...");

            // Perfil em L: Aba 1 = 50.0 externa, Aba 2 = 50.0 externa. Chapa #14 (Espessura=2.0, Raio=1.2, K=0.165, Coef=16.0)
            var segmentos = new List<Segmento>
            {
                new Segmento { Direcao = "S", Angulo = 90.0, Medida = 50.0, TipoMedida = "e" },
                new Segmento { Direcao = "E", Angulo = 90.0, Medida = 50.0, TipoMedida = "e" }
            };

            var polar = geo.ConverterInstrucoesParaCoordenadasPolares("#14", 3000.0, segmentos);
            
            // Comprimento reto recalculado no centro:
            // dh1 = 50 - 0 (ponta) - desconto(dobra)
            // desconto = tan(45) * (1.2 + 2/2) = 1.0 * 2.2 = 2.2
            // dh1 = 50 - 2.2 = 47.8
            // dh2 = 50 - 2.2 = 47.8
            Assert(Math.Abs(polar.CoordenadasPolares[0].Comprimento - 47.8) < 1e-6, $"Polar Comprimento 1 esperado 47.8, obtido {polar.CoordenadasPolares[0].Comprimento}");
            Assert(Math.Abs(polar.CoordenadasPolares[1].Comprimento - 47.8) < 1e-6, $"Polar Comprimento 2 esperado 47.8, obtido {polar.CoordenadasPolares[1].Comprimento}");

            var plano = geo.GerarDadosPlanificacao(polar);
            
            // Corte total = reto1 + reto2 + BA
            // BA = 90 * (pi / 180) * (1.2 + 0.165 * 2.0) = 1.570796 * 1.53 = 2.4033
            // Corte total = 47.8 + 47.8 + 2.4033 = 98.0033...
            // O mapeamento de posições arredonda para milímetros
            int corteEsperado = (int)Math.Round(47.8 + 47.8 + 2.4033); // 98
            Assert(plano.CorteTotal == corteEsperado, $"Corte total esperado {corteEsperado}, obtido {plano.CorteTotal}");
            Log.Information("Corte total do perfil L 50x50 na chapa #14: {Corte} mm.", plano.CorteTotal);
        }

        private static void TestarGeradorBoiadeira(IGeradorPecaService gerador, IGeometryService geo)
        {
            Log.Information("Testando gerador procedural Boiadeira com Nelder-Mead...");

            // Gera um perfil boiadeira padrão:
            // altura aba = 20, largura total = 230, primeiro gomo = 30, gomo superior = 30, gomo inferior = 30, num gomos = 2, chapa = #14 (2.0 mm)
            var boiadeira = gerador.GerarPerfilBoiadeira(
                alturaAba: 20.0,
                larguraTotal: 230.0,
                chapa: "#14",
                primeiroGomo: 30.0,
                tamanhoGomoSuperior: 30.0,
                tamanhoGomoInferior: 30.0,
                numGomos: 2,
                comprimento: 3000.0
            );

            Assert(boiadeira != null, "Boiadeira gerada nula");
            Assert(boiadeira.Segmentos.Count == 11, $"Quantidade de segmentos esperada 11, obtida {boiadeira.Segmentos.Count}");

            var polar = geo.ConverterInstrucoesParaCoordenadasPolares(boiadeira.Chapa, boiadeira.Comprimento ?? 3000.0, boiadeira.Segmentos);
            var dim = geo.CalcularDimensoesAcabadas(polar);

            Assert(dim != null, "Dimensões acabadas nulas");
            
            // A largura gerada deve bater exatamente a larguraTotal target (230 mm) respeitando a tolerância (0.5 mm)
            Assert(Math.Abs(dim.Value.Largura - 230.0) <= 0.5, $"Largura acabada esperada 230.0, obtida {dim.Value.Largura}");
            Assert(Math.Abs(dim.Value.Altura - 20.0) <= 0.5, $"Altura acabada esperada 20.0, obtida {dim.Value.Altura}");

            Log.Information("Perfil Boiadeira 230 gerado com sucesso. Dimensoes acabadas: X={X} mm, Y={Y} mm.", dim.Value.Largura, dim.Value.Altura);
        }

        private static void Assert(bool condicao, string mensagem)
        {
            if (!condicao)
            {
                throw new InvalidOperationException($"Falha no teste: {mensagem}");
            }
            Log.Information("  [OK] {Mensagem}", mensagem);
        }
    }
}
