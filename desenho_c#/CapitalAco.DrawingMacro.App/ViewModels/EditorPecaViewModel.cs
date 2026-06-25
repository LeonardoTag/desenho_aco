using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapitalAco.DrawingMacro.App.Models;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public enum FaseModoRapido
    {
        Desenho,
        Grau,
        Medidas,
        Concluido
    }

    public partial class EditorPecaViewModel : ObservableObject
    {
        private readonly IGeometryService _geometryService;
        private readonly IGeradorPecaService _geradorPecaService;
        private readonly IBibliotecaPecasService _bibliotecaService;
        private readonly ICsvService _csvService;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly IConfigService _configService;

        // Propriedades da Peça
        [ObservableProperty]
        private string _nomePeca = "Peça Nova";

        // Enquanto o usuário não digitar (ou aceitar de um gerador/biblioteca) um nome próprio, o nome da
        // peça é sugerido automaticamente a partir do formato dos segmentos desenhados (ver SugerirNomePeca).
        private bool _nomeEditadoManualmente;
        private string _ultimoNomeAutomatico = "Peça Nova";

        partial void OnNomePecaChanged(string value)
        {
            if (value != _ultimoNomeAutomatico)
            {
                _nomeEditadoManualmente = true;
            }
        }

        [ObservableProperty]
        private double? _comprimentoPeca = null;

        [ObservableProperty]
        private Chapa? _chapaSelecionada;

        // Indica se há algo desenhado para mostrar; quando false, a prévia exibe a mensagem de "comece a desenhar"
        [ObservableProperty]
        private bool _temDesenho;

        // Coleções
        public ObservableCollection<Chapa> Chapas { get; } = new();
        public ObservableCollection<Segmento> Segmentos { get; } = new();
        public ObservableCollection<string> Avisos { get; } = new();

        // Linha selecionada na tabela de segmentos (Modo Clássico), usada por "Remover Sel."
        [ObservableProperty]
        private Segmento? _selectedSegmento;

        // Imagem do Desenho (Preview)
        [ObservableProperty]
        private ImageSource? _previewImage;

        // Dimensões totais acabadas da peça (largura x altura), exibidas junto à prévia
        [ObservableProperty]
        private string _dimensoesTotaisTexto = string.Empty;

        // Campos do Segmento Ativo (Sendo editado)
        [ObservableProperty]
        private string _segDirecao = "E";

        [ObservableProperty]
        private double _segAngulo = 90.0;

        [ObservableProperty]
        private double _segMedida = 100.0;

        [ObservableProperty]
        private string _segTipoMedida = "e";

        [ObservableProperty]
        private bool _segEhCurvo;

        [ObservableProperty]
        private double _segCurvaRaio = 100.0;

        [ObservableProperty]
        private double _segCurvaComprimento = 157.0;

        [ObservableProperty]
        private double _segCurvaAngulo = 90.0;

        [ObservableProperty]
        private string _segCurvaTipoRaio = "externo";

        // Modo Rápido de Desenho (teclado): desenha o esqueleto por direção e depois preenche as medidas em sequência
        [ObservableProperty]
        private bool _modoRapidoAtivo = true;

        [ObservableProperty]
        private FaseModoRapido _faseRapida = FaseModoRapido.Desenho;

        [ObservableProperty]
        private int _indiceMedidaRapida;

        [ObservableProperty]
        private double _medidaRapidaAtual = 100.0;

        [ObservableProperty]
        private double _grauRapidoAtual = 90.0;

        [ObservableProperty]
        private string _statusModoRapido = string.Empty;

        // Tarja visível com o modo atual (clássico/rápido e sub-fase), para orientar o usuário e dar contexto ao ESC.
        [ObservableProperty]
        private string _modoAtualTexto = "MODO CLÁSSICO — edição manual de segmentos";

        [ObservableProperty]
        private Brush _modoAtualCor = new SolidColorBrush(Color.FromRgb(0x34, 0x49, 0x5E));

        private double? _proximoGrauPersonalizado;

        // Controla quais avisos já geraram um alerta em tela, para soar só uma vez por problema novo
        // (não a cada nova tecla digitada enquanto o mesmo segmento continuar abaixo do mínimo).
        private readonly HashSet<string> _avisosDobraAlertados = new();
        private bool _autoIntersecaoAlertada;

        // Histórico para Ctrl+Z: cada entrada é uma cópia completa da lista de segmentos capturada
        // imediatamente ANTES de uma mutação (adicionar/remover/limpar/editar medida/gerar/carregar),
        // de forma que desfazer sempre restaure o estado exato anterior, em qualquer modo (clássico ou rápido).
        private const int LimiteHistoricoDesfazer = 50;
        private readonly List<List<Segmento>> _historicoDesfazer = new();

        [ObservableProperty]
        private bool _podeDesfazer;

        // Galeria de Geradores de Peças
        public ObservableCollection<string> GeradoresDisponiveis { get; } = new() { "Boiadeira", "Tubo Redondo" };

        [ObservableProperty]
        private string _geradorSelecionado = "Boiadeira";

        public bool GeradorEhBoiadeira => GeradorSelecionado == "Boiadeira";
        public bool GeradorEhTuboRedondo => GeradorSelecionado == "Tubo Redondo";

        partial void OnGeradorSelecionadoChanged(string value)
        {
            OnPropertyChanged(nameof(GeradorEhBoiadeira));
            OnPropertyChanged(nameof(GeradorEhTuboRedondo));
        }

        // Parâmetros do Gerador Tubo Redondo (calandrado 360°)
        [ObservableProperty]
        private double _tuboDiametro = 100.0;

        [ObservableProperty]
        private string _tuboTipoDiametro = "externo";

        // Parâmetros do Gerador Boiadeira
        [ObservableProperty]
        private double _boiadeiraAltura = 20.0;

        [ObservableProperty]
        private double _boiadeiraLargura = 230.0;

        [ObservableProperty]
        private double _boiadeiraPrimeiroGomo = 30.0;

        [ObservableProperty]
        private double _boiadeiraGomoSuperior = 30.0;

        [ObservableProperty]
        private double _boiadeiraGomoInferior = 30.0;

        [ObservableProperty]
        private int _boiadeiraNumGomos = 2;

        // Adicionar ao Pedido (Campos do diálogo)
        [ObservableProperty]
        private int _pedidoQuantidade = 1;

        [ObservableProperty]
        private string _pedidoObservacao = string.Empty;

        // Múltiplos comprimentos: permite adicionar a mesma peça em vários lotes de comprimento/quantidade de uma vez
        [ObservableProperty]
        private bool _multiplosComprimentosHabilitado;

        [ObservableProperty]
        private string _comprimentosMultiplosTexto = string.Empty;

        // O comprimento único só faz sentido quando os múltiplos comprimentos estão desligados.
        public bool ComprimentoUnicoHabilitado => !MultiplosComprimentosHabilitado;

        partial void OnMultiplosComprimentosHabilitadoChanged(bool value) => OnPropertyChanged(nameof(ComprimentoUnicoHabilitado));

        // Evento para enviar itens ao carrinho do pedido
        public event Action<PecaPedidoItem>? EnviarAoPedido;

        public EditorPecaViewModel(
            IGeometryService geometryService,
            IGeradorPecaService geradorPecaService,
            IBibliotecaPecasService bibliotecaService,
            ICsvService csvService,
            IPdfGeneratorService pdfGeneratorService,
            IConfigService configService)
        {
            _geometryService = geometryService;
            _geradorPecaService = geradorPecaService;
            _bibliotecaService = bibliotecaService;
            _csvService = csvService;
            _pdfGeneratorService = pdfGeneratorService;
            _configService = configService;

            // Inicializar chapas
            CarregarChapas();

            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            // ModoRapidoAtivo é inicializado via inicializador de campo (true por padrão), o que NÃO
            // dispara o callback OnModoRapidoAtivoChanged (inicializadores de campo atribuem o campo
            // diretamente, sem passar pelo setter gerado). Por isso a tarja de modo atual e o texto de
            // status ficavam com os valores padrão de "Modo Clássico" mesmo com o Modo Rápido já marcado,
            // até o usuário desmarcar/marcar a opção manualmente. Sincroniza aqui, uma única vez, no início.
            AtualizarStatusModoRapido();
        }

        private void CarregarChapas()
        {
            Chapas.Clear();
            var lista = _csvService.CarregarChapas();
            foreach (var chapa in lista)
            {
                Chapas.Add(chapa);
            }
            ChapaSelecionada = Chapas.FirstOrDefault(c => c.Codigo == "#14") ?? Chapas.FirstOrDefault();
        }

        private void Segmentos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AtualizarPreview();
        }

        partial void OnChapaSelecionadaChanged(Chapa? value) => AtualizarPreview();
        partial void OnComprimentoPecaChanged(double? value) => AtualizarPreview();

        public void CarregarPecaDoModelo(ModeloPeca peca)
        {
            NomePeca = peca.Nome;
            ComprimentoPeca = peca.Comprimento;
            ChapaSelecionada = Chapas.FirstOrDefault(c => string.Equals(c.Codigo, peca.Chapa, StringComparison.OrdinalIgnoreCase)) ?? ChapaSelecionada;

            if (Segmentos.Count > 0) RegistrarEstadoParaDesfazer();

            Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
            Segmentos.Clear();
            foreach (var seg in peca.Segmentos)
            {
                Segmentos.Add(seg);
            }
            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            AtualizarPreview();
        }

        [RelayCommand]
        private void AdicionarSegmento()
        {
            if (DirecaoInvalidaAposUltimoSegmento(SegDirecao, SegEhCurvo))
            {
                MessageBox.Show("Não é possível adicionar um segmento na mesma direção ou na direção oposta ao anterior (dobra de 0° ou 180°).", "Direção inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var novo = new Segmento
            {
                Direcao = SegDirecao,
                Angulo = SegAngulo,
                Medida = SegMedida,
                TipoMedida = SegTipoMedida,
                EhCurvo = SegEhCurvo
            };

            if (SegEhCurvo)
            {
                novo.CurvaInfo = new Segmento.InformacaoCurva
                {
                    Raio = SegCurvaRaio,
                    ComprimentoCurva = SegCurvaComprimento,
                    AnguloCurva = SegCurvaAngulo,
                    TipoRaio = SegCurvaTipoRaio
                };
            }

            RegistrarEstadoParaDesfazer();
            Segmentos.Add(novo);
        }

        [RelayCommand]
        private void RemoverSegmento(Segmento? seg)
        {
            if (seg != null)
            {
                RegistrarEstadoParaDesfazer();
                Segmentos.Remove(seg);
            }
        }

        [RelayCommand]
        private void LimparSegmentos()
        {
            if (Segmentos.Count == 0) return;

            RegistrarEstadoParaDesfazer();
            Segmentos.Clear();
        }

        private static Segmento ClonarSegmento(Segmento s) => new Segmento
        {
            Direcao = s.Direcao,
            Angulo = s.Angulo,
            Medida = s.Medida,
            TipoMedida = s.TipoMedida,
            EhCurvo = s.EhCurvo,
            MedidaDefinida = s.MedidaDefinida,
            CurvaInfo = s.CurvaInfo == null ? null : new Segmento.InformacaoCurva
            {
                Raio = s.CurvaInfo.Raio,
                ComprimentoCurva = s.CurvaInfo.ComprimentoCurva,
                AnguloCurva = s.CurvaInfo.AnguloCurva,
                TipoRaio = s.CurvaInfo.TipoRaio
            }
        };

        // Captura uma cópia do estado atual dos segmentos antes de uma mutação, para permitir desfazer (Ctrl+Z).
        // Público para que o code-behind da View também possa registrar o estado antes de edições diretas na
        // tabela (DataGrid), que alteram propriedades de um Segmento já existente sem disparar CollectionChanged.
        public void RegistrarEstadoParaDesfazer()
        {
            _historicoDesfazer.Add(Segmentos.Select(ClonarSegmento).ToList());
            if (_historicoDesfazer.Count > LimiteHistoricoDesfazer)
            {
                _historicoDesfazer.RemoveAt(0);
            }
            PodeDesfazer = true;
        }

        [RelayCommand]
        private void Desfazer()
        {
            if (_historicoDesfazer.Count == 0) return;

            var estadoAnterior = _historicoDesfazer[^1];
            _historicoDesfazer.RemoveAt(_historicoDesfazer.Count - 1);
            PodeDesfazer = _historicoDesfazer.Count > 0;

            Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
            Segmentos.Clear();
            foreach (var seg in estadoAnterior)
            {
                Segmentos.Add(seg);
            }
            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            AtualizarPreview();
        }

        // Direção igual (0°) ou oposta (180°) ao último segmento reto não é uma dobra válida.
        private bool DirecaoInvalidaAposUltimoSegmento(string novaDirecao, bool novoEhCurvo)
        {
            if (novoEhCurvo || Segmentos.Count == 0) return false;

            var anterior = Segmentos[^1];
            if (anterior.EhCurvo) return false;

            if (anterior.Direcao == novaDirecao) return true;

            var opostos = new Dictionary<string, string> { ["N"] = "S", ["S"] = "N", ["E"] = "W", ["W"] = "E" };
            return opostos.TryGetValue(anterior.Direcao, out var oposta) && oposta == novaDirecao;
        }

        public bool EstaNaFaseDesenho => FaseRapida == FaseModoRapido.Desenho;
        public bool EstaNaFaseGrau => FaseRapida == FaseModoRapido.Grau;
        public bool EstaNaFaseMedidas => FaseRapida == FaseModoRapido.Medidas;
        public bool EstaConcluido => FaseRapida == FaseModoRapido.Concluido;

        partial void OnFaseRapidaChanged(FaseModoRapido value)
        {
            OnPropertyChanged(nameof(EstaNaFaseDesenho));
            OnPropertyChanged(nameof(EstaNaFaseGrau));
            OnPropertyChanged(nameof(EstaNaFaseMedidas));
            OnPropertyChanged(nameof(EstaConcluido));
        }

        partial void OnModoRapidoAtivoChanged(bool value)
        {
            if (value)
            {
                if (Segmentos.Count > 0) RegistrarEstadoParaDesfazer();
                Segmentos.Clear();
                FaseRapida = FaseModoRapido.Desenho;
                _proximoGrauPersonalizado = null;
            }
            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void AdicionarSegmentoRapido(string direcao)
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Desenho) return;

            if (DirecaoInvalidaAposUltimoSegmento(direcao, false))
            {
                StatusModoRapido = "Direção inválida: seria uma dobra de 0° ou 180°. Escolha outra direção.";
                return;
            }

            // O ângulo personalizado (via G) persiste para os próximos segmentos até ser alterado novamente.
            double angulo = _proximoGrauPersonalizado ?? 90.0;

            var config = _configService.ObterConfiguracao();
            RegistrarEstadoParaDesfazer();
            Segmentos.Add(new Segmento
            {
                Direcao = direcao,
                Angulo = angulo,
                Medida = config.MedidaPlaceholder,
                TipoMedida = "e",
                MedidaDefinida = false
            });

            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void EntrarFaseGrau()
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Desenho) return;
            if (Segmentos.Count == 0) return;

            // Sempre reinicia em 90° e seleciona o campo: voltar ao padrão é só G + Enter,
            // e mudar para outro ângulo é só digitar por cima do valor já selecionado.
            GrauRapidoAtual = 90.0;
            FaseRapida = FaseModoRapido.Grau;
            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void ConfirmarGrauPersonalizado()
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Grau) return;

            _proximoGrauPersonalizado = GrauRapidoAtual is > 0 and < 180 ? GrauRapidoAtual : 90.0;
            FaseRapida = FaseModoRapido.Desenho;
            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void ConfirmarEsqueletoRapido()
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Desenho) return;
            if (Segmentos.Count == 0) return;

            FaseRapida = FaseModoRapido.Medidas;
            IndiceMedidaRapida = 0;
            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void ConfirmarMedidaRapida()
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Medidas) return;
            if (IndiceMedidaRapida < 0 || IndiceMedidaRapida >= Segmentos.Count) return;
            if (MedidaRapidaAtual <= 0) return;

            RegistrarEstadoParaDesfazer();
            var segAtual = Segmentos[IndiceMedidaRapida];
            segAtual.Medida = MedidaRapidaAtual;
            segAtual.MedidaDefinida = true;
            Segmentos[IndiceMedidaRapida] = segAtual; // força notificação (refresh do DataGrid + prévia)
            IndiceMedidaRapida++;

            if (IndiceMedidaRapida >= Segmentos.Count)
            {
                FaseRapida = FaseModoRapido.Concluido;
            }

            AtualizarStatusModoRapido();
        }

        // Desfaz o último passo do Modo Rápido. As mutações de conteúdo dos segmentos (desenhar/medir) são
        // revertidas via o histórico genérico de Ctrl+Z (Desfazer); aqui tratamos apenas as transições de
        // sub-fase que não alteram os segmentos em si.
        [RelayCommand]
        public void DesfazerModoRapido()
        {
            if (!ModoRapidoAtivo) return;

            switch (FaseRapida)
            {
                case FaseModoRapido.Desenho:
                    Desfazer();
                    break;

                case FaseModoRapido.Grau:
                    FaseRapida = FaseModoRapido.Desenho;
                    break;

                case FaseModoRapido.Medidas:
                    if (IndiceMedidaRapida > 0)
                    {
                        IndiceMedidaRapida--;
                        Desfazer();
                    }
                    else
                    {
                        FaseRapida = FaseModoRapido.Desenho;
                    }
                    break;

                case FaseModoRapido.Concluido:
                    FaseRapida = FaseModoRapido.Medidas;
                    IndiceMedidaRapida = Segmentos.Count - 1;
                    if (IndiceMedidaRapida >= 0)
                    {
                        Desfazer();
                    }
                    break;
            }

            AtualizarStatusModoRapido();
        }

        // ESC: sai um nível do modo/sub-fase atual, sem apagar dados já confirmados (diferente do Ctrl+Backspace).
        [RelayCommand]
        public void SairDoModoAtual()
        {
            if (!ModoRapidoAtivo) return;

            switch (FaseRapida)
            {
                case FaseModoRapido.Grau:
                case FaseModoRapido.Medidas:
                    FaseRapida = FaseModoRapido.Desenho;
                    break;

                case FaseModoRapido.Concluido:
                    FaseRapida = FaseModoRapido.Medidas;
                    IndiceMedidaRapida = Math.Max(Segmentos.Count - 1, 0);
                    break;

                case FaseModoRapido.Desenho:
                    ModoRapidoAtivo = false;
                    break;
            }

            AtualizarStatusModoRapido();
        }

        private void AtualizarModoAtual()
        {
            if (!ModoRapidoAtivo)
            {
                ModoAtualTexto = "MODO CLÁSSICO — edição manual de segmentos";
                ModoAtualCor = new SolidColorBrush(Color.FromRgb(0x34, 0x49, 0x5E));
                return;
            }

            switch (FaseRapida)
            {
                case FaseModoRapido.Desenho:
                    ModoAtualTexto = "MODO RÁPIDO — Desenhando forma (Esc sai para o Modo Clássico)";
                    ModoAtualCor = new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9));
                    break;
                case FaseModoRapido.Grau:
                    ModoAtualTexto = "MODO RÁPIDO — Alterando ângulo (Esc cancela)";
                    ModoAtualCor = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));
                    break;
                case FaseModoRapido.Medidas:
                    ModoAtualTexto = $"MODO RÁPIDO — Inserindo medida {IndiceMedidaRapida + 1}/{Segmentos.Count} (Esc volta)";
                    ModoAtualCor = new SolidColorBrush(Color.FromRgb(0x8E, 0x44, 0xAD));
                    break;
                case FaseModoRapido.Concluido:
                    ModoAtualTexto = "MODO RÁPIDO — Peça concluída (Esc revisa última medida)";
                    ModoAtualCor = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                    break;
            }
        }

        private void AtualizarStatusModoRapido()
        {
            AtualizarModoAtual();

            if (!ModoRapidoAtivo)
            {
                StatusModoRapido = string.Empty;
                return;
            }

            switch (FaseRapida)
            {
                case FaseModoRapido.Desenho:
                    StatusModoRapido = Segmentos.Count == 0
                        ? "Modo Rápido: use as setas (ou WASD) para desenhar a forma da peça."
                        : $"Modo Rápido: Forma ({Segmentos.Count} segmento(s)). Setas/WASD para continuar, G para ângulo, Enter para ir às medidas.";
                    break;

                case FaseModoRapido.Grau:
                    StatusModoRapido = "Modo Rápido: digite o ângulo de deflexão do próximo segmento e confirme.";
                    break;

                case FaseModoRapido.Medidas:
                    MedidaRapidaAtual = _configService.ObterConfiguracao().MedidaPlaceholder;
                    StatusModoRapido = $"Modo Rápido: Medida {IndiceMedidaRapida + 1}/{Segmentos.Count}. Digite o valor e Enter.";
                    break;

                case FaseModoRapido.Concluido:
                    StatusModoRapido = "Modo Rápido: peça concluída. Backspace para revisar a última medida.";
                    break;
            }
        }

        [RelayCommand]
        private void GerarBoiadeira()
        {
            if (ChapaSelecionada == null) return;

            try
            {
                var boiadeira = _geradorPecaService.GerarPerfilBoiadeira(
                    BoiadeiraAltura,
                    BoiadeiraLargura,
                    ChapaSelecionada.Codigo,
                    BoiadeiraPrimeiroGomo,
                    BoiadeiraGomoSuperior,
                    BoiadeiraGomoInferior,
                    BoiadeiraNumGomos,
                    ComprimentoPeca
                );

                NomePeca = boiadeira.Nome;

                if (Segmentos.Count > 0) RegistrarEstadoParaDesfazer();

                Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
                Segmentos.Clear();
                foreach (var seg in boiadeira.Segmentos)
                {
                    Segmentos.Add(seg);
                }
                Segmentos.CollectionChanged += Segmentos_CollectionChanged;

                AtualizarPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Falha no Otimizador Boiadeira", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void GerarTuboRedondo()
        {
            if (ChapaSelecionada == null) return;

            try
            {
                var tubo = _geradorPecaService.GerarTuboRedondo(TuboDiametro, TuboTipoDiametro, ChapaSelecionada.Codigo, ComprimentoPeca);

                NomePeca = tubo.Nome;

                if (Segmentos.Count > 0) RegistrarEstadoParaDesfazer();

                Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
                Segmentos.Clear();
                foreach (var seg in tubo.Segmentos)
                {
                    Segmentos.Add(seg);
                }
                Segmentos.CollectionChanged += Segmentos_CollectionChanged;

                AtualizarPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Falha no Gerador de Tubo Redondo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SalvarNaBiblioteca()
        {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;

            try
            {
                var modelo = _bibliotecaService.SalvarModelo(
                    NomePeca,
                    ChapaSelecionada.Codigo,
                    ComprimentoPeca,
                    Segmentos.ToList()
                );

                MessageBox.Show($"Peça '{modelo.Nome}' salva na biblioteca local com sucesso!", "Salvar Biblioteca", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro ao Salvar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Garante que a peça tenha ao menos um segmento e que o perfil não colida consigo mesmo.
        private bool ValidarPecaPronta()
        {
            if (Segmentos.Count == 0)
            {
                MessageBox.Show("Desenhe ao menos um segmento antes de continuar.", "Peça incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ChapaSelecionada != null)
            {
                double comprimentoCheck = ComprimentoPeca ?? _configService.ObterConfiguracao().ComprimentoPreviewPlaceholder;
                if (_geometryService.PerfilCruzaASiMesmo(ChapaSelecionada.Codigo, comprimentoCheck, Segmentos.ToList()))
                {
                    MessageBox.Show("O perfil desenhado cruza a si mesmo (dobras colidem). Corrija o desenho antes de continuar.", "Perfil inválido", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        [RelayCommand]
        private void AdicionarAoPedido()
        {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;

            List<(double Quantidade, double Comprimento)> lotes;

            if (MultiplosComprimentosHabilitado)
            {
                if (string.IsNullOrWhiteSpace(ComprimentosMultiplosTexto))
                {
                    MessageBox.Show("Informe pelo menos um comprimento e quantidade.", "Comprimentos inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    lotes = ParseComprimentosMultiplos(ComprimentosMultiplosTexto);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Comprimentos inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (lotes.Count == 0)
                {
                    MessageBox.Show("Informe pelo menos um comprimento e quantidade válido.", "Comprimentos inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                if (ComprimentoPeca is not double comprimentoDefinido || comprimentoDefinido <= 0)
                {
                    MessageBox.Show("Defina o comprimento da peça antes de adicionar à ordem de produção.", "Comprimento obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                lotes = new List<(double Quantidade, double Comprimento)> { (PedidoQuantidade, comprimentoDefinido) };
            }

            var segmentosAtuais = Segmentos.ToList();
            foreach (var (quantidade, comprimento) in lotes)
            {
                string sufixo = lotes.Count > 1 ? $" ({comprimento:F0})" : string.Empty;
                var item = new PecaPedidoItem
                {
                    ChapaCodigo = ChapaSelecionada.Codigo,
                    Comprimento = comprimento,
                    Quantidade = (int)quantidade,
                    NomePeca = NomePeca + sufixo,
                    Segmentos = segmentosAtuais,
                    Observacao = PedidoObservacao
                };

                EnviarAoPedido?.Invoke(item);
            }

            // Limpa formulário de quantidade do pedido
            PedidoQuantidade = 1;
            PedidoObservacao = string.Empty;
            ComprimentosMultiplosTexto = string.Empty;

            MessageBox.Show("Peça adicionada ao carrinho da Ordem de Produção!", "Carrinho", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static List<(double Quantidade, double Comprimento)> ParseComprimentosMultiplos(string texto)
        {
            var resultados = new List<(double Quantidade, double Comprimento)>();
            var regex = new System.Text.RegularExpressions.Regex(@"^(\d+)\s*(?:[xX*]|\s+)\s*(\d+(?:[.,]\d+)?)$");

            foreach (var parteCrua in texto.Split(','))
            {
                var parte = parteCrua.Trim();
                if (parte.Length == 0) continue;

                var match = regex.Match(parte);
                if (match.Success)
                {
                    double qtd = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double comp = NumericUtils.ParseNumero(match.Groups[2].Value) ?? -1;
                    if (comp <= 0 || qtd <= 0)
                        throw new InvalidOperationException($"Item inválido: \"{parte}\"");
                    resultados.Add((qtd, comp));
                }
                else
                {
                    double comp = NumericUtils.ParseNumero(parte) ?? -1;
                    if (comp <= 0)
                        throw new InvalidOperationException($"Item inválido: \"{parte}\"");
                    resultados.Add((1, comp));
                }
            }

            return resultados;
        }

        [RelayCommand]
        private void GerarFichaDobra()
        {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;

            if (ComprimentoPeca is not double comprimentoDefinido || comprimentoDefinido <= 0)
            {
                MessageBox.Show("Defina o comprimento da peça antes de gerar a ficha de dobra.", "Comprimento obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(ChapaSelecionada.Codigo, comprimentoDefinido, Segmentos.ToList());
                var caminho = _pdfGeneratorService.GerarRelatorioDobra(polar, NomePeca, ChapaSelecionada.Codigo, comprimentoDefinido);

                if (File.Exists(caminho))
                {
                    FileShellHelper.CopiarArquivoParaAreaDeTransferencia(caminho);

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = caminho,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar ficha de dobra: {ex.Message}", "Erro de PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AbrirPastaRelatorios()
        {
            FileShellHelper.AbrirPasta(_configService.ObterCaminhoSaidaRelatorios());
        }

        // Enquanto o nome não tiver sido definido manualmente (nem vindo de um gerador ou da biblioteca),
        // mantém o campo "Nome da Peça" atualizado com uma sugestão baseada no formato atual dos segmentos.
        private void AtualizarNomeAutomatico()
        {
            if (_nomeEditadoManualmente) return;

            _ultimoNomeAutomatico = SugerirNomePeca(Segmentos);
            NomePeca = _ultimoNomeAutomatico;
        }

        // Heurística de nomenclatura por formato: reconhece os perfis mais comuns de chapa dobrada pela
        // quantidade de segmentos, pelo sentido de giro das dobras (distingue U/gancho de Z/zigue-zague)
        // e pela proporção entre eles, com um nome genérico de reserva para formatos não reconhecidos.
        //
        // Cantoneira, U, U Enrijecido e Z são, por definição, perfis de dobra reta (90°); o único caso
        // que admite ângulo diferente de 90° é o lábio do Z Enrijecido (clássico nas terças Z, justamente
        // para permitir o encaixe/empilhamento entre peças — ver literatura de perfis formados a frio).
        // Sem essas dobras a 90° onde exigido, o formato não corresponde a nenhum desses perfis nomeados.
        private string SugerirNomePeca(IReadOnlyList<Segmento> segmentos)
        {
            if (segmentos.Count == 0) return "Peça Nova";
            if (segmentos.Any(s => s.EhCurvo)) return "Perfil Calandrado";

            string M(double v) => NumericUtils.FormatarCompacto(v);
            bool Eh90(double angulo) => Math.Abs(angulo - 90.0) < 0.5;
            string generico() => $"Perfil com {segmentos.Count - 1} Dobras";

            switch (segmentos.Count)
            {
                case 1:
                    return $"Chapa Plana {M(segmentos[0].Medida)}";

                case 2:
                    if (!Eh90(segmentos[1].Angulo)) return generico();
                    return $"Perfil Cantoneira {M(segmentos[0].Medida)}x{M(segmentos[1].Medida)}";

                case 3:
                {
                    if (!Eh90(segmentos[1].Angulo) || !Eh90(segmentos[2].Angulo)) return generico();

                    // Mesmo sentido de giro nas duas dobras = formato côncavo em gancho (U); sentidos
                    // opostos = formato em zigue-zague (Z). Antes disso, qualquer perfil de 3 segmentos
                    // (inclusive Z) era erroneamente chamado de U.
                    string familia = MesmoSentidoDeGiro(segmentos, 0) ? "U" : "Z";

                    double aba1 = segmentos[0].Medida, alma = segmentos[1].Medida, aba2 = segmentos[2].Medida;
                    bool simetrico = Math.Abs(aba1 - aba2) < 1.0;
                    return simetrico
                        ? $"Perfil {familia} {M(alma)}x{M(aba1)}"
                        : $"Perfil {familia} Manco {M(aba1)}x{M(alma)}x{M(aba2)}";
                }

                case 5:
                {
                    // O núcleo (aba-alma-aba) tem que ser reto em qualquer um destes perfis, inclusive
                    // no Z Enrijecido — só o lábio (dobra das pontas) pode escapar de 90° nesse caso.
                    if (!Eh90(segmentos[2].Angulo) || !Eh90(segmentos[3].Angulo)) return generico();

                    double pontaA = segmentos[0].Medida, pontaB = segmentos[4].Medida;
                    double meio1 = segmentos[1].Medida, alma = segmentos[2].Medida, meio2 = segmentos[3].Medida;
                    double mediaMeio = (meio1 + meio2) / 2.0;

                    // Lábios de reforço são tipicamente bem mais curtos que as abas/alma adjacentes;
                    // sem esse padrão, 5 segmentos formam antes um perfil Cartola.
                    bool temLabios = mediaMeio > 0 && pontaA < mediaMeio * 0.5 && pontaB < mediaMeio * 0.5;
                    if (!temLabios) return $"Perfil Cartola {M(alma)}x{M(meio1)}";

                    // O núcleo (aba-alma-aba, ignorando os lábios das pontas) define se é um U ou um Z
                    // enrijecido — os lábios em si não mudam essa classificação.
                    string familiaNucleo = MesmoSentidoDeGiro(segmentos, 1) ? "U" : "Z";

                    // No U Enrijecido o lábio é sempre reto (90°); só o Z Enrijecido admite o lábio
                    // dobrado num ângulo diferente.
                    bool labiosRetos = Eh90(segmentos[1].Angulo) && Eh90(segmentos[4].Angulo);
                    if (familiaNucleo == "U" && !labiosRetos) return generico();

                    return $"Perfil {familiaNucleo} Enrijecido {M(alma)}x{M(meio1)}x{M(pontaA)}";
                }

                default:
                    return generico();
            }
        }

        // Compara o sentido de giro (horário/anti-horário) das dobras entre segmentos[i]→[i+1] e
        // [i+1]→[i+2], usando os azimutes reais (que já tratam ângulos customizados, não só 90°).
        private bool MesmoSentidoDeGiro(IReadOnlyList<Segmento> segmentos, int indiceInicial)
        {
            var azimutes = _geometryService.ObterAzimutesDeSegmentos(segmentos.ToList());

            bool SentidoHorario(int i)
            {
                double diff = (azimutes[i + 1] - azimutes[i]) % 360.0;
                if (diff < 0) diff += 360.0;
                return diff < 180.0;
            }

            return SentidoHorario(indiceInicial) == SentidoHorario(indiceInicial + 1);
        }

        public void AtualizarPreview()
        {
            AtualizarNomeAutomatico();

            if (ChapaSelecionada == null || Segmentos.Count == 0)
            {
                TemDesenho = false;
                PreviewImage = null;
                DimensoesTotaisTexto = string.Empty;
                Avisos.Clear();
                return;
            }

            try
            {
                var config = _configService.ObterConfiguracao();

                // Converter para polares (usa o placeholder de comprimento apenas para gerar a prévia, sem alterar o valor digitado)
                var listSegs = Segmentos.ToList();
                double comprimentoPreview = ComprimentoPeca ?? config.ComprimentoPreviewPlaceholder;
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(ChapaSelecionada.Codigo, comprimentoPreview, listSegs);
                TemDesenho = true;

                // Renderizar preview 520x400, usando os tamanhos de fonte configurados
                float fonteCota = (float)config.DesenhoFonteBaseMinima;
                float fonteAngulo = (float)Math.Max(config.DesenhoFonteBaseMinima - 1.0, 8.0);
                PreviewImage = SkiaRenderer.RenderToImageSource(polar, 780, 600, _geometryService, fonteCota, fonteAngulo);

                // Dimensões totais acabadas da peça
                var dimensoes = _geometryService.CalcularDimensoesAcabadas(polar);
                DimensoesTotaisTexto = dimensoes != null
                    ? $"Dimensões Totais: {dimensoes.Value.Largura:F0} mm (L) x {dimensoes.Value.Altura:F0} mm (A)"
                    : string.Empty;

                // Atualizar avisos
                Avisos.Clear();
                var novosAlertas = new List<string>();

                // 1. Dobras abaixo da mínima — alerta só na primeira vez que CADA segmento entra em
                // violação (a chave é o nº do segmento, não o texto completo, que muda a cada dígito
                // enquanto o usuário ainda está digitando a medida).
                var avisosDobra = _geometryService.VerificarDobrasAbaixoMinima(polar, ChapaSelecionada);
                var chavesAtuais = new HashSet<string>();
                foreach (var av in avisosDobra)
                {
                    Avisos.Add($"⚠ {av}");
                    var chave = av.Split(':')[0];
                    chavesAtuais.Add(chave);
                    if (!_avisosDobraAlertados.Contains(chave))
                    {
                        novosAlertas.Add(av);
                    }
                }
                _avisosDobraAlertados.Clear();
                foreach (var chave in chavesAtuais) _avisosDobraAlertados.Add(chave);

                // 2. Auto-interseção
                bool autoIntersecao = _geometryService.PerfilCruzaASiMesmo(ChapaSelecionada.Codigo, comprimentoPreview, listSegs);
                if (autoIntersecao)
                {
                    Avisos.Add("⚠ ATENÇÃO: O perfil cruza a si mesmo!");
                    if (!_autoIntersecaoAlertada) novosAlertas.Add("O perfil desenhado cruza a si mesmo (dobras colidem).");
                }
                _autoIntersecaoAlertada = autoIntersecao;

                if (novosAlertas.Count > 0)
                {
                    MessageBox.Show(string.Join("\n", novosAlertas), "Atenção: Problema no Desenho", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // Silencia exceções provisórias de digitação incompleta
                System.Diagnostics.Debug.WriteLine($"Erro no preview: {ex.Message}");
            }
        }
    }
}
