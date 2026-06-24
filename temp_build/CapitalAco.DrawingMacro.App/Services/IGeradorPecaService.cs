using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public interface IGeradorPecaService
    {
        ModeloPeca GerarPerfilBoiadeira(
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
            double? toleranciaTopo = null);
    }
}
