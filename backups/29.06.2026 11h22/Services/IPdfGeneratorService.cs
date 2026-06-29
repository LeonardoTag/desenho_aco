using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class LoteComprimento
    {
        [JsonPropertyName("quantidade")]
        public int Quantidade { get; set; }

        [JsonPropertyName("comprimento")]
        public double Comprimento { get; set; }

        [JsonIgnore]
        public string Descricao => $"{Quantidade}x  {Comprimento:F0} mm";
    }

    public class PecaPedidoItem
    {
        public string ChapaCodigo { get; set; } = string.Empty;
        public double Comprimento { get; set; }
        public int Quantidade { get; set; }
        public string NomePeca { get; set; } = string.Empty;
        public List<Segmento> Segmentos { get; set; } = new();
        public string Observacao { get; set; } = string.Empty;
        public bool AnexarDetalhamentoDobra { get; set; } = false;

        [JsonPropertyName("variantes_comprimento")]
        public List<LoteComprimento>? VariantesComprimento { get; set; }

        [JsonIgnore]
        public bool TemMultiplosComprimentos => VariantesComprimento != null && VariantesComprimento.Count > 1;

        [JsonIgnore]
        public int QuantidadeTotal => TemMultiplosComprimentos
            ? VariantesComprimento!.Sum(v => v.Quantidade)
            : Quantidade;

        [JsonIgnore]
        public System.Windows.Media.ImageSource? ImagemPerfil { get; set; }
    }

    public interface IPdfGeneratorService
    {
        string GerarRelatorioDobra(InstrucoesPolares polar, string nomePeca, string chapaCodigo, double comprimento);
        string GerarRelatorioPedido(List<PecaPedidoItem> itens, string observacao = "");
    }
}
