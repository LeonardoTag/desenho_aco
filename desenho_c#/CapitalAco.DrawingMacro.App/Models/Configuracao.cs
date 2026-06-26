using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    public class Configuracao
    {
        // Versão do schema do config.json. Incrementar quando campos obrigatórios forem
        // adicionados ou renomeados, para detectar arquivos de configuração desatualizados.
        [JsonPropertyName("versao_schema")]
        public int VersaoSchema { get; set; } = 1;

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

        [JsonPropertyName("relatorio_ordenar_por_espessura")]
        public bool RelatorioOrdenarPorEspessura { get; set; } = true;

        public static Configuracao CreateDefault() => new Configuracao();

        /// <summary>
        /// Garante que campos numéricos críticos estejam dentro de faixas seguras,
        /// protegendo contra config.json editado manualmente com valores inválidos.
        /// </summary>
        public void Sanitizar()
        {
            // Janela
            JanelaLargura              = Math.Clamp(JanelaLargura, 800, 7680);
            JanelaAltura               = Math.Clamp(JanelaAltura, 600, 4320);
            JanelaLarguraMin           = Math.Clamp(JanelaLarguraMin, 800, 3840);
            JanelaAlturaMin            = Math.Clamp(JanelaAlturaMin, 600, 2160);

            // Preview
            PreviewTamanhoMax          = Math.Clamp(PreviewTamanhoMax, 200, 2000);
            PreviewTamanhoMin          = Math.Clamp(PreviewTamanhoMin, 100, PreviewTamanhoMax);
            PreviewDebounceMs          = Math.Clamp(PreviewDebounceMs, 10, 5000);
            PreviewDebounceAlteracaoMs = Math.Clamp(PreviewDebounceAlteracaoMs, 10, 5000);
            ComprimentoPreviewPlaceholder = Math.Clamp(ComprimentoPreviewPlaceholder, 1.0, 100_000.0);
            MedidaPlaceholder          = Math.Clamp(MedidaPlaceholder, 1.0, 10_000.0);

            // Renderização
            DesenhoSupersampling       = Math.Clamp(DesenhoSupersampling, 1, 4);
            MargemCanvas               = Math.Clamp(MargemCanvas, 0.01, 0.5);
            MargemDesenhoPct           = Math.Clamp(MargemDesenhoPct, 0.01, 0.5);
            MargemFundo                = Math.Clamp(MargemFundo, 0, 20);
            CasasDecimaisCalculo       = Math.Clamp(CasasDecimaisCalculo, 0, 6);
            CasasDecimaisMostradas     = Math.Clamp(CasasDecimaisMostradas, 0, 6);
            DesenhoFonteBaseFator      = Math.Clamp(DesenhoFonteBaseFator, 0.001, 0.2);
            DesenhoFonteBaseMinima     = Math.Clamp(DesenhoFonteBaseMinima, 4.0, 48.0);
            DesenhoFonteRelatorioMinima = Math.Clamp(DesenhoFonteRelatorioMinima, 4.0, 48.0);
            DesenhoFonteDobraMinima    = Math.Clamp(DesenhoFonteDobraMinima, 4.0, 48.0);

            // Relatórios
            RelatorioPecasPorPagina    = Math.Clamp(RelatorioPecasPorPagina, 1, 50);
            RelatorioImagemTamanhoPedido = Math.Clamp(RelatorioImagemTamanhoPedido, 100, 3000);
            RelatorioImagemTamanhoDobra  = Math.Clamp(RelatorioImagemTamanhoDobra, 100, 3000);
            RelatorioLarguraDesenhoPct   = Math.Clamp(RelatorioLarguraDesenhoPct, 0.1, 0.9);
            RelatorioDobraAlturaImagemPct = Math.Clamp(RelatorioDobraAlturaImagemPct, 0.1, 0.9);
            RelatorioDobraLarguraPerfilPct = Math.Clamp(RelatorioDobraLarguraPerfilPct, 0.1, 0.9);

            // Fontes do relatório de dobra
            RelatorioDobraFonteTitulo  = Math.Clamp(RelatorioDobraFonteTitulo, 4.0, 72.0);
            RelatorioDobraFonteSecao   = Math.Clamp(RelatorioDobraFonteSecao, 4.0, 72.0);
            RelatorioDobraFonteTexto   = Math.Clamp(RelatorioDobraFonteTexto, 4.0, 72.0);
            RelatorioDobraFonteCota    = Math.Clamp(RelatorioDobraFonteCota, 4.0, 72.0);
            RelatorioDobraFonteAngulo  = Math.Clamp(RelatorioDobraFonteAngulo, 4.0, 72.0);
            RelatorioDobraFonteSentido = Math.Clamp(RelatorioDobraFonteSentido, 4.0, 72.0);

            // Fontes do relatório de pedido
            RelatorioPedidoFonteTitulo    = Math.Clamp(RelatorioPedidoFonteTitulo, 4.0, 72.0);
            RelatorioPedidoFonteSubtitulo = Math.Clamp(RelatorioPedidoFonteSubtitulo, 4.0, 72.0);
            RelatorioPedidoFonteTexto     = Math.Clamp(RelatorioPedidoFonteTexto, 4.0, 72.0);
            RelatorioPedidoFonteDestaque  = Math.Clamp(RelatorioPedidoFonteDestaque, 4.0, 72.0);
            RelatorioPedidoFonteRotuloPeca  = Math.Clamp(RelatorioPedidoFonteRotuloPeca, 4.0, 72.0);
            RelatorioPedidoFonteRotuloCampo = Math.Clamp(RelatorioPedidoFonteRotuloCampo, 4.0, 72.0);

            // Boiadeira
            BoiadeiraAlturaAbaPadrao    = Math.Clamp(BoiadeiraAlturaAbaPadrao, 1.0, 500.0);
            BoiadeiraLarguraTotalPadrao = Math.Clamp(BoiadeiraLarguraTotalPadrao, 1.0, 2000.0);
            BoiadeiraPrimeiroGomoPadrao = Math.Clamp(BoiadeiraPrimeiroGomoPadrao, 1.0, 500.0);
            BoiadeiraGomoSuperiorPadrao = Math.Clamp(BoiadeiraGomoSuperiorPadrao, 1.0, 500.0);
            BoiadeiraGomoInferiorPadrao = Math.Clamp(BoiadeiraGomoInferiorPadrao, 1.0, 500.0);
            BoiadeiraNumGomosPadrao     = Math.Clamp(BoiadeiraNumGomosPadrao, 1, 20);
            BoiadeiraComprimentoPadrao  = Math.Clamp(BoiadeiraComprimentoPadrao, 1.0, 30_000.0);
            BoiadeiraToleranciaLargura  = Math.Clamp(BoiadeiraToleranciaLargura, 0.01, 50.0);
            BoiadeiraToleranciaAltura   = Math.Clamp(BoiadeiraToleranciaAltura, 0.01, 50.0);
            BoiadeiraToleranciaTopo     = Math.Clamp(BoiadeiraToleranciaTopo, 0.01, 50.0);
        }
    }
}
