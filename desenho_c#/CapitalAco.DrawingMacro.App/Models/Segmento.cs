using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    [JsonConverter(typeof(SegmentoConverter))]
    public class Segmento : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private string _direcao = "E";
        public string Direcao { get => _direcao; set { if (_direcao != value) { _direcao = value; Notify(); } } }

        private double _angulo;
        public double Angulo { get => _angulo; set { if (_angulo != value) { _angulo = value; Notify(); } } }

        private double _medida;
        public double Medida { get => _medida; set { if (_medida != value) { _medida = value; Notify(); } } }

        private string _tipoMedida = "e";
        public string TipoMedida { get => _tipoMedida; set { if (_tipoMedida != value) { _tipoMedida = value; Notify(); } } }

        private bool _ehCurvo;
        public bool EhCurvo { get => _ehCurvo; set { if (_ehCurvo != value) { _ehCurvo = value; Notify(); } } }

        private InformacaoCurva? _curvaInfo;
        public InformacaoCurva? CurvaInfo { get => _curvaInfo; set { if (_curvaInfo != value) { _curvaInfo = value; Notify(); } } }

        [JsonIgnore]
        public bool MedidaDefinida { get; set; } = true;

        public class InformacaoCurva
        {
            [JsonPropertyName("raio")]
            public double Raio { get; set; }

            [JsonPropertyName("comprimento_curva")]
            public double ComprimentoCurva { get; set; }

            [JsonPropertyName("angulo_curva")]
            public double AnguloCurva { get; set; }

            [JsonPropertyName("tipo_raio")]
            public string TipoRaio { get; set; } = "externo";
        }
    }
}
