using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapitalAco.DrawingMacro.App.Models;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class ConfiguracaoViewModel : ObservableObject
    {
        private readonly IConfigService _configService;

        [ObservableProperty]
        private string _responsavel = string.Empty;

        [ObservableProperty]
        private string _observacaoPadrao = string.Empty;

        [ObservableProperty]
        private double _boiadeiraAltura = 20;

        [ObservableProperty]
        private double _boiadeiraLargura = 230;

        [ObservableProperty]
        private double _boiadeiraPrimeiroGomo = 30;

        [ObservableProperty]
        private double _boiadeiraGomoSuperior = 30;

        [ObservableProperty]
        private double _boiadeiraGomoInferior = 30;

        [ObservableProperty]
        private int _boiadeiraNumGomos = 2;

        public ConfiguracaoViewModel(IConfigService configService)
        {
            _configService = configService;
            CarregarConfiguracoes();
        }

        private void CarregarConfiguracoes()
        {
            var config = _configService.ObterConfiguracao();
            Responsavel = config.RelatorioNomeResponsavel;
            ObservacaoPadrao = config.RelatorioObservacao;

            BoiadeiraAltura = config.BoiadeiraAlturaAbaPadrao;
            BoiadeiraLargura = config.BoiadeiraLarguraTotalPadrao;
            BoiadeiraPrimeiroGomo = config.BoiadeiraPrimeiroGomoPadrao;
            BoiadeiraGomoSuperior = config.BoiadeiraGomoSuperiorPadrao;
            BoiadeiraGomoInferior = config.BoiadeiraGomoInferiorPadrao;
            BoiadeiraNumGomos = config.BoiadeiraNumGomosPadrao;
        }

        [RelayCommand]
        private void SalvarConfiguracoes()
        {
            var config = _configService.ObterConfiguracao();
            config.RelatorioNomeResponsavel = Responsavel;
            config.RelatorioObservacao = ObservacaoPadrao;

            config.BoiadeiraAlturaAbaPadrao = BoiadeiraAltura;
            config.BoiadeiraLarguraTotalPadrao = BoiadeiraLargura;
            config.BoiadeiraPrimeiroGomoPadrao = BoiadeiraPrimeiroGomo;
            config.BoiadeiraGomoSuperiorPadrao = BoiadeiraGomoSuperior;
            config.BoiadeiraGomoInferiorPadrao = BoiadeiraGomoInferior;
            config.BoiadeiraNumGomosPadrao = BoiadeiraNumGomos;

            _configService.SalvarConfiguracao(config);
        }
    }
}
