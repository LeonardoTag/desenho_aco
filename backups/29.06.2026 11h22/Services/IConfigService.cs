using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public interface IConfigService
    {
        Configuracao ObterConfiguracao();
        void SalvarConfiguracao(Configuracao novaConfig);
        void CarregarConfiguracao();
        string ResolverCaminho(string caminhoRelativo);
        string ObterCaminhoChapas();
        string ObterCaminhoBiblioteca();
        string ObterCaminhoLog();
        string ObterCaminhoSaidaRelatorios();
        string ObterTituloAplicacao(string nomePeca = "");
    }
}
