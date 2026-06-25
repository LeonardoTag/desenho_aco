using System.Collections.Generic;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class PecaPedidoItem
    {
        public string ChapaCodigo { get; set; } = string.Empty;
        public double Comprimento { get; set; }
        public int Quantidade { get; set; }
        public string NomePeca { get; set; } = string.Empty;
        public List<Segmento> Segmentos { get; set; } = new();
        public string Observacao { get; set; } = string.Empty;
        public System.Windows.Media.ImageSource? ImagemPerfil { get; set; }
    }

    public interface IPdfGeneratorService
    {
        string GerarRelatorioDobra(InstrucoesPolares polar, string nomePeca, string chapaCodigo, double comprimento);
        string GerarRelatorioPedido(List<PecaPedidoItem> itens, string observacao = "");
    }
}
