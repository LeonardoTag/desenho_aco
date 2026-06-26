using System;
using System.Collections.Generic;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class InstrucoesPolares
    {
        public List<(double Azimute, double Comprimento)> CoordenadasPolares { get; set; } = new();
        public double Comprimento { get; set; }
        public double Espessura { get; set; }
        public double RaioDeDobra { get; set; }
        public double KFactor { get; set; }
        public List<Segmento> SegmentosOriginal { get; set; } = new();
    }

    public struct DimensoesAcabadas
    {
        public double Largura { get; set; }
        public double Altura { get; set; }
    }

    public class DadosPlanificacao
    {
        public int CorteTotal { get; set; }
        public List<TrechoPlanificacao> Trechos { get; set; } = new();
        public List<TrechoCadeia> TrechosCadeia { get; set; } = new();
        public List<int> Cadeia { get; set; } = new();
        public List<int> PosicoesOrdenadas { get; set; } = new();
        public List<DobraInfo> Dobras { get; set; } = new();
        public List<MarcaDobra> MarcasDobra { get; set; } = new();
        public List<MarcaCalandragem> MarcasCalandragem { get; set; } = new();
        public double Espessura { get; set; }
        public double RaioDeDobra { get; set; }
    }

    public class TrechoPlanificacao
    {
        public string Tipo { get; set; } = "reta"; // "reta", "dobra", "curvo"
        public double Comprimento { get; set; }
        public double? AnguloDobra { get; set; }
        public string? Sentido { get; set; }
        public Segmento.InformacaoCurva? CurvaInfo { get; set; }
    }

    public class TrechoCadeia
    {
        public int Inicio { get; set; }
        public int Fim { get; set; }
        public int Comprimento { get; set; }
    }

    public class DobraInfo
    {
        public double AnguloDobra { get; set; }
        public string Sentido { get; set; } = "h"; // "h" = horaria/baixo, "a" = anti-horaria/cima
    }

    public class MarcaDobra
    {
        public int Posicao { get; set; }
        public double AnguloDobra { get; set; }
        public string Sentido { get; set; } = "h";
    }

    public class MarcaCalandragem
    {
        public int PosicaoInicio { get; set; }
        public int PosicaoFim { get; set; }
        public double Raio { get; set; }
        public double AnguloCurva { get; set; }
        public string TipoRaio { get; set; } = "externo";
    }

    public interface IGeometryService
    {
        InstrucoesPolares ConverterInstrucoesParaCoordenadasPolares(string chapaCodigo, double comprimento, List<Segmento> segmentos, double? espessuraInformada = null);
        double CalcularLarguraCorte(InstrucoesPolares instrucoes);
        double CalcularPesoKg(InstrucoesPolares instrucoes, int quantidade, Chapa chapaInfo);
        DimensoesAcabadas? CalcularDimensoesAcabadas(InstrucoesPolares instrucoes);
        List<string> VerificarDobrasAbaixoMinima(InstrucoesPolares instrucoes, Chapa chapaInfo);
        bool PerfilCruzaASiMesmo(string chapaCodigo, double comprimento, List<Segmento> segmentos);
        DadosPlanificacao GerarDadosPlanificacao(InstrucoesPolares instrucoes);
        
        // Utilitários de Azimute e compensações compartilhados
        double DefinirAzimute(string direcao, double grau, double? azimuteAnterior);
        List<double> ObterAzimutesDeSegmentos(List<Segmento> segmentos);
        List<double> ObterAngulosDobraDeAzimutes(List<double> azimutes);
        double GetBendAllowance(double anguloDobra, double raioDeDobra, double kFactor, double espessura);
        double MedidaCentroDeExterna(double medidaExterna, double grau, double espessura, double raioDeDobra);
        double MedidaCentroDeInterna(double medidaInterna, double grau, double espessura, double raioDeDobra);

        // Medidas internas/externas (cotas vermelha/azul) e lado interno do perfil para anotação de desenhos
        List<(double Livre, double Interna, double Externa)> GerarMedidasInternaExterna(InstrucoesPolares instrucoes);
        int DeterminarLadoInternoSegmento(int n, List<(double X, double Y)> coordenadas);

        // Conversão de coordenadas polares → retangulares (usada por SkiaRenderer e GeradorPecaService)
        List<(double X, double Y)> GerarCoordenadasRetangularesParciais(List<(double Azimute, double Comprimento)> coordenadasPolares);
        List<(double X, double Y)> GerarCoordenadasRetangularesAbsolutas(List<(double X, double Y)> coordenadasParciais);
        List<(double X, double Y)> CalcularCoordenadasExternas(List<(double X, double Y)> coordenadas, double espessura);
    }
}
