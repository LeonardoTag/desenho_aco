using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    public class Configuracao
    {
        [JsonPropertyName("versao_app")]
        public string VersaoApp { get; set; } = "1.2.4";

        [JsonPropertyName("titulo_app")]
        public string TituloApp { get; set; } = "Capital Aço — Construção de Perfil";

        [JsonPropertyName("janela_largura")]
        public int JanelaLargura { get; set; } = 1280;

        [JsonPropertyName("janela_altura")]
        public int JanelaAltura { get; set; } = 720;

        [JsonPropertyName("janela_largura_min")]
        public int JanelaLarguraMin { get; set; } = 1024;

        [JsonPropertyName("janela_altura_min")]
        public int JanelaAlturaMin { get; set; } = 640;

        [JsonPropertyName("preview_tamanho_max")]
        public int PreviewTamanhoMax { get; set; } = 520;

        [JsonPropertyName("preview_tamanho_min")]
        public int PreviewTamanhoMin { get; set; } = 400;

        [JsonPropertyName("preview_debounce_ms")]
        public int PreviewDebounceMs { get; set; } = 100;

        [JsonPropertyName("preview_debounce_alteracao_ms")]
        public int PreviewDebounceAlteracaoMs { get; set; } = 350;

        [JsonPropertyName("comprimento_preview_placeholder")]
        public double ComprimentoPreviewPlaceholder { get; set; } = 500.0;

        [JsonPropertyName("medida_placeholder")]
        public double MedidaPlaceholder { get; set; } = 50.0;

        [JsonPropertyName("margem_canvas")]
        public double MargemCanvas { get; set; } = 0.04;

        [JsonPropertyName("margem_desenho_pct")]
        public double MargemDesenhoPct { get; set; } = 0.15;

        [JsonPropertyName("margem_fundo")]
        public int MargemFundo { get; set; } = 3;

        [JsonPropertyName("desenho_supersampling")]
        public int DesenhoSupersampling { get; set; } = 2;

        [JsonPropertyName("desenho_fator_comprimento")]
        public double DesenhoFatorComprimento { get; set; } = 0.35;

        [JsonPropertyName("desenho_angulo_comprimento")]
        public double DesenhoAnguloComprimento { get; set; } = 55.0;

        [JsonPropertyName("casas_decimais_calculo")]
        public int CasasDecimaisCalculo { get; set; } = 3;

        [JsonPropertyName("casas_decimais_mostradas")]
        public int CasasDecimaisMostradas { get; set; } = 0;

        [JsonPropertyName("arquivo_chapas")]
        public string ArquivoChapas { get; set; } = "data/chapas.csv";

        [JsonPropertyName("arquivo_biblioteca")]
        public string ArquivoBiblioteca { get; set; } = "data/biblioteca_pecas.json";

        [JsonPropertyName("pasta_fontes")]
        public string PastaFontes { get; set; } = "assets/Noto_Sans";

        [JsonPropertyName("fonte_desenho")]
        public string FonteDesenho { get; set; } = "NotoSans-Black.ttf";

        [JsonPropertyName("pasta_logs")]
        public string PastaLogs { get; set; } = "logs";

        [JsonPropertyName("nome_log")]
        public string NomeLog { get; set; } = "app_log.txt";

        [JsonPropertyName("relatorio_nome_responsavel")]
        public string RelatorioNomeResponsavel { get; set; } = "Leonardo";

        [JsonPropertyName("relatorio_observacao")]
        public string RelatorioObservacao { get; set; } = "";

        [JsonPropertyName("relatorio_pasta_imagens")]
        public string RelatorioPastaImagens { get; set; } = "images";

        [JsonPropertyName("relatorio_logo_principal")]
        public string RelatorioLogoPrincipal { get; set; } = "Capital Azul Recortado.png";

        [JsonPropertyName("relatorio_logo_secundario")]
        public string RelatorioLogoSecundario { get; set; } = "só logo 100px.png";

        [JsonPropertyName("relatorio_pecas_por_pagina")]
        public int RelatorioPecasPorPagina { get; set; } = 9;

        [JsonPropertyName("pasta_saida_relatorios")]
        public string PastaSaidaRelatorios { get; set; } = "files";

        [JsonPropertyName("boiadeira_altura_aba_padrao")]
        public double BoiadeiraAlturaAbaPadrao { get; set; } = 20.0;

        [JsonPropertyName("boiadeira_largura_total_padrao")]
        public double BoiadeiraLarguraTotalPadrao { get; set; } = 230.0;

        [JsonPropertyName("boiadeira_primeiro_gomo_padrao")]
        public double BoiadeiraPrimeiroGomoPadrao { get; set; } = 30.0;

        [JsonPropertyName("boiadeira_gomo_superior_padrao")]
        public double BoiadeiraGomoSuperiorPadrao { get; set; } = 30.0;

        [JsonPropertyName("boiadeira_gomo_inferior_padrao")]
        public double BoiadeiraGomoInferiorPadrao { get; set; } = 30.0;

        [JsonPropertyName("boiadeira_num_gomos_padrao")]
        public int BoiadeiraNumGomosPadrao { get; set; } = 2;

        [JsonPropertyName("boiadeira_comprimento_padrao")]
        public double BoiadeiraComprimentoPadrao { get; set; } = 3000.0;

        [JsonPropertyName("boiadeira_tolerancia_largura")]
        public double BoiadeiraToleranciaLargura { get; set; } = 0.5;

        [JsonPropertyName("boiadeira_tolerancia_altura")]
        public double BoiadeiraToleranciaAltura { get; set; } = 0.5;

        [JsonPropertyName("boiadeira_tolerancia_topo")]
        public double BoiadeiraToleranciaTopo { get; set; } = 0.5;

        [JsonPropertyName("desenho_cota_distancia_preview")]
        public double DesenhoCotaDistanciaPreview { get; set; } = 0.88;

        [JsonPropertyName("desenho_cota_distancia_relatorio")]
        public double DesenhoCotaDistanciaRelatorio { get; set; } = 0.82;

        [JsonPropertyName("desenho_fonte_relatorio_fator")]
        public double DesenhoFonteRelatorioFator { get; set; } = 1.55;

        [JsonPropertyName("desenho_fonte_detalhamento_dobra_fator")]
        public double DesenhoFonteDetalhamentoDobraFator { get; set; } = 0.8;

        [JsonPropertyName("relatorio_imagem_tamanho_pedido")]
        public int RelatorioImagemTamanhoPedido { get; set; } = 680;

        [JsonPropertyName("relatorio_imagem_tamanho_dobra")]
        public int RelatorioImagemTamanhoDobra { get; set; } = 1150;

        [JsonPropertyName("relatorio_largura_desenho_pct")]
        public double RelatorioLarguraDesenhoPct { get; set; } = 0.64;

        [JsonPropertyName("relatorio_dobra_altura_imagem_pct")]
        public double RelatorioDobraAlturaImagemPct { get; set; } = 0.50;

        [JsonPropertyName("relatorio_dobra_largura_perfil_pct")]
        public double RelatorioDobraLarguraPerfilPct { get; set; } = 0.50;

        [JsonPropertyName("desenho_fonte_base_fator")]
        public double DesenhoFonteBaseFator { get; set; } = 0.028;

        [JsonPropertyName("desenho_fonte_base_minima")]
        public double DesenhoFonteBaseMinima { get; set; } = 12.0;

        [JsonPropertyName("desenho_fonte_relatorio_minima")]
        public double DesenhoFonteRelatorioMinima { get; set; } = 13.0;

        [JsonPropertyName("desenho_fonte_dobra_minima")]
        public double DesenhoFonteDobraMinima { get; set; } = 11.0;

        [JsonPropertyName("relatorio_dobra_fonte_titulo")]
        public double RelatorioDobraFonteTitulo { get; set; } = 15.5;

        [JsonPropertyName("relatorio_dobra_fonte_secao")]
        public double RelatorioDobraFonteSecao { get; set; } = 10.0;

        [JsonPropertyName("relatorio_dobra_fonte_texto")]
        public double RelatorioDobraFonteTexto { get; set; } = 10.0;

        [JsonPropertyName("relatorio_dobra_fonte_cota")]
        public double RelatorioDobraFonteCota { get; set; } = 7.65;

        [JsonPropertyName("relatorio_dobra_fonte_angulo")]
        public double RelatorioDobraFonteAngulo { get; set; } = 7.225;

        [JsonPropertyName("relatorio_dobra_fonte_sentido")]
        public double RelatorioDobraFonteSentido { get; set; } = 6.375;

        [JsonPropertyName("relatorio_pedido_fonte_titulo")]
        public double RelatorioPedidoFonteTitulo { get; set; } = 16.0;

        [JsonPropertyName("relatorio_pedido_fonte_subtitulo")]
        public double RelatorioPedidoFonteSubtitulo { get; set; } = 11.0;

        [JsonPropertyName("relatorio_pedido_fonte_texto")]
        public double RelatorioPedidoFonteTexto { get; set; } = 9.0;

        [JsonPropertyName("relatorio_pedido_fonte_destaque")]
        public double RelatorioPedidoFonteDestaque { get; set; } = 10.0;

        [JsonPropertyName("relatorio_pedido_fonte_rotulo_peca")]
        public double RelatorioPedidoFonteRotuloPeca { get; set; } = 7.0;

        [JsonPropertyName("relatorio_pedido_fonte_rotulo_campo")]
        public double RelatorioPedidoFonteRotuloCampo { get; set; } = 8.0;

        public static Configuracao CreateDefault() => new Configuracao();
    }
}
