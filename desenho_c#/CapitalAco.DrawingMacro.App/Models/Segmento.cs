using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    [JsonConverter(typeof(SegmentoConverter))]
    public class Segmento
    {
        public string Direcao { get; set; } = "E";
        public double Angulo { get; set; }
        public double Medida { get; set; }
        public string TipoMedida { get; set; } = "e"; // "e" = externa, "i" = interna
        public bool EhCurvo { get; set; }
        public InformacaoCurva? CurvaInfo { get; set; }

        public class InformacaoCurva
        {
            [JsonPropertyName("raio")]
            public double Raio { get; set; }

            [JsonPropertyName("comprimento_curva")]
            public double ComprimentoCurva { get; set; }

            [JsonPropertyName("angulo_curva")]
            public double AnguloCurva { get; set; }

            [JsonPropertyName("tipo_raio")]
            public string TipoRaio { get; set; } = "externo"; // "externo" ou "interno"
        }
    }
}
