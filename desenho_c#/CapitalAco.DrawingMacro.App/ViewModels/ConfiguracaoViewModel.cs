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

        // Tamanho de Fontes - Desenho/Preview
        [ObservableProperty]
        private double _fonteDesenhoBaseMinima = 12.0;

        [ObservableProperty]
        private double _fonteRelatorioMinima = 13.0;

        [ObservableProperty]
        private double _fonteDobraMinima = 11.0;

        // Tamanho de Fontes - Detalhamento de Dobra (PDF)
        [ObservableProperty]
        private double _dobraFonteTitulo = 15.5;

        [ObservableProperty]
        private double _dobraFonteSecao = 10.0;

        [ObservableProperty]
        private double _dobraFonteTexto = 10.0;

        [ObservableProperty]
        private double _dobraFonteCota = 7.65;

        [ObservableProperty]
        private double _dobraFonteAngulo = 7.225;

        [ObservableProperty]
        private double _dobraFonteSentido = 6.375;

        // Tamanho de Fontes - Ordem de Produção (PDF)
        [ObservableProperty]
        private double _pedidoFonteTitulo = 16.0;

        [ObservableProperty]
        private double _pedidoFonteSubtitulo = 11.0;

        [ObservableProperty]
        private double _pedidoFonteTexto = 9.0;

        [ObservableProperty]
        private double _pedidoFonteDestaque = 10.0;

        [ObservableProperty]
        private double _pedidoFonteRotuloPeca = 7.0;

        [ObservableProperty]
        private double _pedidoFonteRotuloCampo = 8.0;

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

            FonteDesenhoBaseMinima = config.DesenhoFonteBaseMinima;
            FonteRelatorioMinima = config.DesenhoFonteRelatorioMinima;
            FonteDobraMinima = config.DesenhoFonteDobraMinima;

            DobraFonteTitulo = config.RelatorioDobraFonteTitulo;
            DobraFonteSecao = config.RelatorioDobraFonteSecao;
            DobraFonteTexto = config.RelatorioDobraFonteTexto;
            DobraFonteCota = config.RelatorioDobraFonteCota;
            DobraFonteAngulo = config.RelatorioDobraFonteAngulo;
            DobraFonteSentido = config.RelatorioDobraFonteSentido;

            PedidoFonteTitulo = config.RelatorioPedidoFonteTitulo;
            PedidoFonteSubtitulo = config.RelatorioPedidoFonteSubtitulo;
            PedidoFonteTexto = config.RelatorioPedidoFonteTexto;
            PedidoFonteDestaque = config.RelatorioPedidoFonteDestaque;
            PedidoFonteRotuloPeca = config.RelatorioPedidoFonteRotuloPeca;
            PedidoFonteRotuloCampo = config.RelatorioPedidoFonteRotuloCampo;
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

            config.DesenhoFonteBaseMinima = FonteDesenhoBaseMinima;
            config.DesenhoFonteRelatorioMinima = FonteRelatorioMinima;
            config.DesenhoFonteDobraMinima = FonteDobraMinima;

            config.RelatorioDobraFonteTitulo = DobraFonteTitulo;
            config.RelatorioDobraFonteSecao = DobraFonteSecao;
            config.RelatorioDobraFonteTexto = DobraFonteTexto;
            config.RelatorioDobraFonteCota = DobraFonteCota;
            config.RelatorioDobraFonteAngulo = DobraFonteAngulo;
            config.RelatorioDobraFonteSentido = DobraFonteSentido;

            config.RelatorioPedidoFonteTitulo = PedidoFonteTitulo;
            config.RelatorioPedidoFonteSubtitulo = PedidoFonteSubtitulo;
            config.RelatorioPedidoFonteTexto = PedidoFonteTexto;
            config.RelatorioPedidoFonteDestaque = PedidoFonteDestaque;
            config.RelatorioPedidoFonteRotuloPeca = PedidoFonteRotuloPeca;
            config.RelatorioPedidoFonteRotuloCampo = PedidoFonteRotuloCampo;

            _configService.SalvarConfiguracao(config);
        }
    }
}
