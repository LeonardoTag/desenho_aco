using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapitalAco.DrawingMacro.App.Models;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class ComprimentoLoteInput : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private int _quantidade = 1;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private double _comprimento = 0;
    }

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
        private readonly ISkiaRenderer _skiaRenderer;

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

        [ObservableProperty]
        private string _segCurvaCampoDerivado = "Comprimento";

        public bool CurvaRaioEhDerivado        => SegCurvaCampoDerivado == "Raio";
        public bool CurvaComprimentoEhDerivado => SegCurvaCampoDerivado == "Comprimento";
        public bool CurvaAnguloEhDerivado      => SegCurvaCampoDerivado == "Angulo";

        partial void OnSegCurvaCampoDerivadoChanged(string value)
        {
            OnPropertyChanged(nameof(CurvaRaioEhDerivado));
            OnPropertyChanged(nameof(CurvaComprimentoEhDerivado));
            OnPropertyChanged(nameof(CurvaAnguloEhDerivado));
            RecalcularCampoDerivadoCurva();
        }

        partial void OnSegCurvaRaioChanged(double value)       => RecalcularCampoDerivadoCurva();
        partial void OnSegCurvaComprimentoChanged(double value) => RecalcularCampoDerivadoCurva();
        partial void OnSegCurvaAnguloChanged(double value)     => RecalcularCampoDerivadoCurva();
        partial void OnSegCurvaTipoRaioChanged(string value)   => RecalcularCampoDerivadoCurva();

        private bool _recalculandoCurva = false;
        private void RecalcularCampoDerivadoCurva()
        {
            if (_recalculandoCurva || !SegEhCurvo) return;
            _recalculandoCurva = true;
            try
            {
                double raio = SegCurvaRaio;
                double ang  = SegCurvaAngulo;
                double comp = SegCurvaComprimento;
                switch (SegCurvaCampoDerivado)
                {
                    case "Comprimento" when raio > 0 && ang > 0:
                        SegCurvaComprimento = Math.Round(raio * ang * Math.PI / 180.0, 1);
                        break;
                    case "Angulo" when raio > 0 && comp > 0:
                        SegCurvaAngulo = Math.Round(comp / raio * 180.0 / Math.PI, 2);
                        break;
                    case "Raio" when comp > 0 && ang > 0:
                        SegCurvaRaio = Math.Round(comp / (ang * Math.PI / 180.0), 1);
                        break;
                }
            }
            finally { _recalculandoCurva = false; }
        }

        // Modo Rápido de Desenho (teclado): desenha o esqueleto por direção e depois preenche as medidas em sequência
        [ObservableProperty]
        private bool _modoRapidoAtivo = false;

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

        public ObservableCollection<ComprimentoLoteInput> LotesComprimento { get; } = new();

        // O comprimento único só faz sentido quando os múltiplos comprimentos estão desligados.
        public bool ComprimentoUnicoHabilitado => !MultiplosComprimentosHabilitado;

        partial void OnMultiplosComprimentosHabilitadoChanged(bool value)
        {
            OnPropertyChanged(nameof(ComprimentoUnicoHabilitado));
            OnPropertyChanged(nameof(ComprimentoInvalido));
            if (value && LotesComprimento.Count == 0)
                LotesComprimento.Add(new ComprimentoLoteInput { Quantidade = 1, Comprimento = ComprimentoPeca ?? 0 });
        }

        // Evento para enviar itens ao carrinho do pedido
        public event Action<PecaPedidoItem>? EnviarAoPedido;

        // Evento para atualizar item existente no pedido (modo edição)
        public event Action<PecaPedidoItem, PecaPedidoItem>? AtualizarNoPedido;

        // Sinaliza à View que a adição concluiu — View devolve foco ao UserControl
        public event Action? PecaAdicionadaAoPedido;

        // Sinaliza que um modelo foi salvo na biblioteca (MainViewModel recarrega a lista)
        public event Action? BibliotecaSalva;

        [ObservableProperty]
        private string _mensagemStatus = string.Empty;

        public bool StatusVisivel => !string.IsNullOrEmpty(MensagemStatus);

        partial void OnMensagemStatusChanged(string value) => OnPropertyChanged(nameof(StatusVisivel));

        private System.Windows.Threading.DispatcherTimer? _timerStatus;
        private System.Windows.Threading.DispatcherTimer? _timerPreview;

        private PecaPedidoItem? _itemEditando;

        [ObservableProperty]
        private bool _modoEdicao;

        public string TextoBotaoAddPedido => ModoEdicao ? "Salvar no Pedido" : "Adicionar à Ordem (Shift+Enter)";

        public EditorPecaViewModel(
            IGeometryService geometryService,
            IGeradorPecaService geradorPecaService,
            IBibliotecaPecasService bibliotecaService,
            ICsvService csvService,
            IPdfGeneratorService pdfGeneratorService,
            IConfigService configService,
            ISkiaRenderer skiaRenderer)
        {
            _geometryService = geometryService;
            _geradorPecaService = geradorPecaService;
            _bibliotecaService = bibliotecaService;
            _csvService = csvService;
            _pdfGeneratorService = pdfGeneratorService;
            _skiaRenderer = skiaRenderer;
            _configService = configService;

            // Inicializar chapas
            CarregarChapas();

            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            // Inicializa via setter para disparar PropertyChanged("ModoRapidoAtivo") e os DataTriggers do XAML.
            // Usar inicializador de campo (= true) contornaria o setter gerado e o painel do Modo Rápido
            // permaneceria colapsado até o usuário desmarcar/marcar manualmente o checkbox.
            ModoRapidoAtivo = true;
        }

        private void CarregarChapas()
        {
            try
            {
                Chapas.Clear();
                var lista = _csvService.CarregarChapas();
                foreach (var chapa in lista)
                    Chapas.Add(chapa);
                ChapaSelecionada = Chapas.FirstOrDefault(c => c.Codigo == "#14") ?? Chapas.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Não foi possível carregar a lista de chapas.\n\nVerifique o arquivo chapas.csv.\n\n{ex.Message}",
                    "Erro ao Carregar Chapas", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Segmentos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AtualizarPreview();
        }

        public bool ComprimentoInvalido => ComprimentoUnicoHabilitado && (ComprimentoPeca == null || ComprimentoPeca <= 0);

        partial void OnChapaSelecionadaChanged(Chapa? value)
        {
            if (value != null && Segmentos.Count > 0)
                MostrarStatus($"Chapa alterada para {value.Codigo} — verifique os avisos de dobra mínima.");
            AtualizarPreview();
        }
        partial void OnComprimentoPecaChanged(double? value)
        {
            OnPropertyChanged(nameof(ComprimentoInvalido));
            AtualizarPreview();
        }
        partial void OnModoEdicaoChanged(bool value) => OnPropertyChanged(nameof(TextoBotaoAddPedido));

        public void CarregarPecaDoModelo(ModeloPeca peca)
        {
            if (Segmentos.Count > 0)
            {
                var r = MessageBox.Show(
                    $"O editor tem uma peça em andamento. Carregar \"{peca.Nome}\" vai substituí-la.\n\nDeseja continuar?",
                    "Carregar da Biblioteca", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

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

        public void EditarItemDoPedido(PecaPedidoItem item)
        {
            _itemEditando = item;
            ModoEdicao = true;

            NomePeca = item.NomePeca;
            _nomeEditadoManualmente = true;
            ComprimentoPeca = item.Comprimento;
            ChapaSelecionada = Chapas.FirstOrDefault(c => c.Codigo == item.ChapaCodigo) ?? ChapaSelecionada;
            PedidoQuantidade = item.Quantidade;
            PedidoObservacao = item.Observacao;
            MultiplosComprimentosHabilitado = false;
            LotesComprimento.Clear();

            _historicoDesfazer.Clear();
            PodeDesfazer = false;

            Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
            Segmentos.Clear();
            foreach (var seg in item.Segmentos)
                Segmentos.Add(ClonarSegmento(seg));
            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            AtualizarPreview();
        }

        [RelayCommand]
        private void NovaPeca()
        {
            if (ModoEdicao)
            {
                _itemEditando = null;
                ModoEdicao = false;
            }

            _historicoDesfazer.Clear();
            PodeDesfazer = false;

            Segmentos.CollectionChanged -= Segmentos_CollectionChanged;
            Segmentos.Clear();
            Segmentos.CollectionChanged += Segmentos_CollectionChanged;

            NomePeca = "Peça Nova";
            _nomeEditadoManualmente = false;
            ComprimentoPeca = null;
            PedidoQuantidade = 1;
            PedidoObservacao = string.Empty;
            LotesComprimento.Clear();
            MultiplosComprimentosHabilitado = false;

            ModoRapidoAtivo = true;
            FaseRapida = FaseModoRapido.Desenho;
            _proximoGrauPersonalizado = null;
            AtualizarStatusModoRapido();
            AtualizarPreview();
        }

        [RelayCommand]
        private void CancelarEdicao()
        {
            _itemEditando = null;
            ModoEdicao = false;
        }

        [RelayCommand]
        private void AdicionarSegmento()
        {
            if (DirecaoInvalidaAposUltimoSegmento(SegDirecao, SegEhCurvo, SegAngulo))
            {
                MessageBox.Show("Não é possível adicionar um segmento na mesma direção ou na direção oposta ao anterior (dobra de 0° ou 180°).", "Direção inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!SegEhCurvo && SegMedida <= 0)
            {
                MessageBox.Show("A medida do segmento deve ser maior que zero.", "Medida inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SegEhCurvo && (SegCurvaRaio <= 0 || SegCurvaAngulo <= 0))
            {
                MessageBox.Show("O raio e o ângulo da curva devem ser maiores que zero.", "Curva inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        // Dobra inválida se o azimuto resultante for igual (0°) ou oposto (180°) ao azimuto anterior.
        // Usa azimutos reais calculados, não strings de Direcao, para funcionar com ângulos não-90°.
        private bool DirecaoInvalidaAposUltimoSegmento(string novaDirecao, bool novoEhCurvo, double novoAngulo = 90.0)
        {
            if (novoEhCurvo || Segmentos.Count == 0) return false;
            if (Segmentos[^1].EhCurvo) return false;

            var azimutes = _geometryService.ObterAzimutesDeSegmentos(Segmentos.ToList());
            double azAnterior = azimutes.Last();
            double azNovo = _geometryService.DefinirAzimute(novaDirecao, novoAngulo, azAnterior);

            double delta = Math.Abs(azNovo - azAnterior) % 360.0;
            if (delta > 180.0) delta = 360.0 - delta;

            const double tol = 0.5;
            return delta < tol || Math.Abs(delta - 180.0) < tol;
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
            AtualizarPreview();
        }

        partial void OnIndiceMedidaRapidaChanged(int value) => AtualizarPreview();

        partial void OnModoRapidoAtivoChanged(bool value)
        {
            if (value)
            {
                FaseRapida = FaseModoRapido.Desenho;
                _proximoGrauPersonalizado = null;
            }
            AtualizarStatusModoRapido();
        }

        [RelayCommand]
        public void AdicionarSegmentoRapido(string direcao)
        {
            if (!ModoRapidoAtivo || FaseRapida != FaseModoRapido.Desenho) return;

            // O ângulo personalizado (via G) persiste para os próximos segmentos até ser alterado novamente.
            double angulo = _proximoGrauPersonalizado ?? 90.0;

            if (DirecaoInvalidaAposUltimoSegmento(direcao, false, angulo))
            {
                StatusModoRapido = "Direção inválida: seria uma dobra de 0° ou 180°. Escolha outra direção.";
                return;
            }

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

            if (GrauRapidoAtual is <= 0 or >= 180)
            {
                MostrarStatus($"Ângulo inválido ({GrauRapidoAtual:F0}°). Use um valor entre 1° e 179°.");
                return;
            }

            _proximoGrauPersonalizado = GrauRapidoAtual;
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

        private void MostrarStatus(string mensagem, int duracaoMs = 3000)
        {
            MensagemStatus = mensagem;
            _timerStatus?.Stop();
            _timerStatus = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(duracaoMs)
            };
            _timerStatus.Tick += (_, _) =>
            {
                MensagemStatus = string.Empty;
                _timerStatus?.Stop();
            };
            _timerStatus.Start();
        }

        [RelayCommand]
        private void SalvarNaBiblioteca()
        {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;

            if (!_nomeEditadoManualmente)
            {
                var r = MessageBox.Show(
                    $"O nome \"{NomePeca}\" foi gerado automaticamente.\n\nDeseja salvar com esse nome mesmo assim?",
                    "Salvar na Biblioteca", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            try
            {
                var modelo = _bibliotecaService.SalvarModelo(
                    NomePeca,
                    ChapaSelecionada.Codigo,
                    ComprimentoPeca,
                    Segmentos.ToList()
                );

                BibliotecaSalva?.Invoke();
                MostrarStatus($"'{modelo.Nome}' salva na biblioteca.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro ao Salvar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Exibe aviso de dobras abaixo do mínimo e pergunta se o usuário quer continuar.
        // Deve ser chamado APÓS ValidarPecaPronta() — nesse ponto auto-interseção já foi barrada.
        private bool ChecarAvisosAntesDeProsseguir()
        {
            var avisosDobra = Avisos.Where(a => a.StartsWith("⚠") && !a.Contains("cruza a si mesmo")).ToList();
            if (avisosDobra.Count == 0) return true;
            var msg = string.Join("\n", avisosDobra) + "\n\nDeseja continuar mesmo assim?";
            return MessageBox.Show(msg, "Atenção: Problema no Desenho", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                   == MessageBoxResult.Yes;
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

        private bool _adicionandoAoPedido = false;

        [RelayCommand]
        private void AdicionarAoPedido()
        {
            if (_adicionandoAoPedido) return;
            _adicionandoAoPedido = true;
            try
            {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;
            if (!ChecarAvisosAntesDeProsseguir()) return;

            List<(double Quantidade, double Comprimento)> lotes;

            if (MultiplosComprimentosHabilitado)
            {
                if (LotesComprimento.Count == 0 || LotesComprimento.All(l => l.Comprimento <= 0))
                {
                    MessageBox.Show("Adicione ao menos uma linha com comprimento válido.", "Comprimentos inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                lotes = LotesComprimento
                    .Where(l => l.Comprimento > 0 && l.Quantidade > 0)
                    .Select(l => ((double)l.Quantidade, l.Comprimento))
                    .ToList();

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

            if (_itemEditando != null)
            {
                var (qtdEdit, compEdit) = lotes[0];
                var itemOriginal = _itemEditando;
                var novoItem = new PecaPedidoItem
                {
                    ChapaCodigo = ChapaSelecionada.Codigo,
                    Comprimento = compEdit,
                    Quantidade = (int)qtdEdit,
                    NomePeca = NomePeca,
                    Segmentos = segmentosAtuais,
                    Observacao = PedidoObservacao
                };
                novoItem.ImagemPerfil = RenderizarThumbnail(segmentosAtuais, ChapaSelecionada.Codigo, compEdit);

                _itemEditando = null;
                ModoEdicao = false;
                AtualizarNoPedido?.Invoke(itemOriginal, novoItem);

                PedidoQuantidade = 1;
                PedidoObservacao = string.Empty;
                LotesComprimento.Clear();
                PecaAdicionadaAoPedido?.Invoke();
                return;
            }

            // Múltiplos comprimentos → agrupa num único PecaPedidoItem com VariantesComprimento
            if (MultiplosComprimentosHabilitado && lotes.Count > 1)
            {
                double compPrincipal = lotes[0].Comprimento;
                var novoItemMulti = new PecaPedidoItem
                {
                    ChapaCodigo = ChapaSelecionada.Codigo,
                    Comprimento = compPrincipal,
                    Quantidade  = lotes.Sum(l => (int)l.Quantidade),
                    NomePeca    = NomePeca,
                    Segmentos   = segmentosAtuais,
                    Observacao  = PedidoObservacao,
                    VariantesComprimento = lotes.Select(l => new LoteComprimento { Quantidade = (int)l.Quantidade, Comprimento = l.Comprimento }).ToList()
                };
                novoItemMulti.ImagemPerfil = RenderizarThumbnail(segmentosAtuais, ChapaSelecionada.Codigo, compPrincipal);
                EnviarAoPedido?.Invoke(novoItemMulti);
            }
            else
            {
                foreach (var (quantidade, comprimento) in lotes)
                {
                    var item = new PecaPedidoItem
                    {
                        ChapaCodigo = ChapaSelecionada.Codigo,
                        Comprimento = comprimento,
                        Quantidade  = (int)quantidade,
                        NomePeca    = NomePeca,
                        Segmentos   = segmentosAtuais,
                        Observacao  = PedidoObservacao
                    };
                    item.ImagemPerfil = RenderizarThumbnail(segmentosAtuais, ChapaSelecionada.Codigo, comprimento);
                    EnviarAoPedido?.Invoke(item);
                }
            }

            // Limpa formulário de quantidade do pedido
            PedidoQuantidade = 1;
            PedidoObservacao = string.Empty;
            LotesComprimento.Clear();

            MostrarStatus("Peça adicionada à Ordem de Produção.");
            PecaAdicionadaAoPedido?.Invoke();
            }
            finally { _adicionandoAoPedido = false; }
        }

        private System.Windows.Media.ImageSource? RenderizarThumbnail(List<Segmento> segmentos, string chapaCodigo, double comprimento)
        {
            try
            {
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(chapaCodigo, comprimento, segmentos);
                return _skiaRenderer.RenderToImageSource(polar, 80, 60, _geometryService, mostrarMedidas: false);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private void AdicionarLote()
        {
            LotesComprimento.Add(new ComprimentoLoteInput { Quantidade = 1, Comprimento = ComprimentoPeca ?? 0 });
        }

        [RelayCommand]
        private void RemoverLote(ComprimentoLoteInput? lote)
        {
            if (lote != null) LotesComprimento.Remove(lote);
        }

        [RelayCommand]
        private async Task GerarFichaDobra()
        {
            if (ChapaSelecionada == null) return;
            if (!ValidarPecaPronta()) return;
            if (!ChecarAvisosAntesDeProsseguir()) return;

            if (ComprimentoPeca is not double comprimentoDefinido || comprimentoDefinido <= 0)
            {
                MessageBox.Show("Defina o comprimento da peça antes de gerar a ficha de dobra.", "Comprimento obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Captura estado da UI antes de ir para o background
            var chapaCodigo   = ChapaSelecionada.Codigo;
            var segmentos     = Segmentos.ToList();
            var nomePeca      = NomePeca;
            string caminho;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                caminho = await Task.Run(() =>
                {
                    var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(
                        chapaCodigo, comprimentoDefinido, segmentos);
                    return _pdfGeneratorService.GerarRelatorioDobra(polar, nomePeca, chapaCodigo, comprimentoDefinido);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar ficha de dobra: {ex.Message}", "Erro de PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

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
        // Toda classificação usa apenas diferenças entre azimutes consecutivos — nunca os valores
        // absolutos de Direcao/Angulo — para ser invariante à rotação da peça (0°/90°/180°/270°).
        private string SugerirNomePeca(IReadOnlyList<Segmento> segmentos)
        {
            if (segmentos.Count == 0) return "Peça Nova";
            if (segmentos.Any(s => s.EhCurvo)) return "Perfil Calandrado";

            string M(double v) => NumericUtils.FormatarCompacto(v);
            string generico() => $"Perfil com {segmentos.Count - 1} Dobras";

            // Calcula os azimutes uma única vez para todas as verificações abaixo.
            var az = _geometryService.ObterAzimutesDeSegmentos(segmentos.ToList());

            // Ângulo geométrico real da j-ésima dobra (joint entre seg[j] e seg[j+1]).
            // Não usa segmentos[j].Angulo (parâmetro de entrada), usa a diferença entre azimutes.
            double AnguloJoint(int j)
            {
                double diff = (az[j + 1] - az[j] + 360.0) % 360.0;
                return diff > 180.0 ? 360.0 - diff : diff;
            }
            bool EhDobraReta(int j) => Math.Abs(AnguloJoint(j) - 90.0) < 0.5;

            // True se os giros nos joints firstJoint e firstJoint+1 têm o mesmo sentido (U vs Z).
            bool MesmoSentido(int firstJoint)
            {
                double d0 = (az[firstJoint + 1] - az[firstJoint] + 360.0) % 360.0;
                double d1 = (az[firstJoint + 2] - az[firstJoint + 1] + 360.0) % 360.0;
                return (d0 < 180.0) == (d1 < 180.0);
            }

            // True se o joint j é horário (d < 180°), False se antihorário (d > 180°).
            bool EhHorario(int j)
            {
                double d = (az[j + 1] - az[j] + 360.0) % 360.0;
                return d < 180.0;
            }

            switch (segmentos.Count)
            {
                case 1:
                    return $"Chapa Plana {M(segmentos[0].Medida)}";

                case 2:
                    if (!EhDobraReta(0)) return generico();
                    return $"Cantoneira {M(segmentos[0].Medida)}x{M(segmentos[1].Medida)}";

                case 3:
                {
                    if (!EhDobraReta(0) || !EhDobraReta(1)) return generico();

                    bool ehU = MesmoSentido(0);
                    double aba1 = segmentos[0].Medida, alma = segmentos[1].Medida, aba2 = segmentos[2].Medida;
                    bool simetrico = Math.Abs(aba1 - aba2) < 1.0;

                    if (ehU)
                        return simetrico
                            ? $"Perfil U simples {M(alma)}x{M(aba1)}"
                            : $"Perfil U manco {M(aba1)}x{M(alma)}x{M(aba2)}";
                    else
                        return simetrico
                            ? $"Perfil Z simples {M(alma)}x{M(aba1)}"
                            : $"Perfil Z simples {M(aba1)}x{M(alma)}x{M(aba2)}";
                }

                case 4:
                {
                    if (!EhDobraReta(0) || !EhDobraReta(1) || !EhDobraReta(2)) return generico();

                    // Três dobras a 90° todas no mesmo sentido → Meia terça.
                    if (!MesmoSentido(0) || !MesmoSentido(1)) return generico();

                    double a1 = segmentos[0].Medida, alma = segmentos[1].Medida;
                    double a2 = segmentos[2].Medida, labio = segmentos[3].Medida;
                    return $"Meia terça {M(a1)}x{M(alma)}x{M(a2)}x{M(labio)}";
                }

                case 5:
                {
                    // As dobras internas (2ª e 3ª) devem ser retas em todos os perfis de 5 segmentos;
                    // só as dobras externas do Z Enrijecido admitem ângulo diferente de 90°.
                    if (!EhDobraReta(1) || !EhDobraReta(2)) return generico();

                    double s0 = segmentos[0].Medida, s1 = segmentos[1].Medida, s2 = segmentos[2].Medida;
                    double s3 = segmentos[3].Medida, s4 = segmentos[4].Medida;

                    bool h0 = EhHorario(0), h1 = EhHorario(1), h2 = EhHorario(2), h3 = EhHorario(3);

                    // Simetria: 1°=5° segmentos e 2°=4° segmentos (exigida por U/Z enrijecido e Cartola 3M).
                    bool simSeg = Math.Abs(s0 - s4) < 1.0 && Math.Abs(s1 - s3) < 1.0;

                    // ── Perfil Cartola ──────────────────────────────────────────────────────────────
                    // Lei: 1ª e 4ª dobras de 90° e mesmo sentido; 2ª e 3ª dobras de 90° e mesmo sentido
                    // (contrário ao das dobras 1ª e 4ª).
                    // Padrão de sentidos: [A,B,B,A] onde A≠B, com todas as dobras a 90°.
                    if (h0 == h3 && h1 == h2 && h0 != h1)
                    {
                        if (!EhDobraReta(0) || !EhDobraReta(3)) return generico();
                        // Variante simétrica (1°=5° e 2°=4°) → 3 medidas: alma × meio × ponta.
                        if (simSeg)
                            return $"Perfil Cartola {M(s2)}x{M(s1)}x{M(s0)}";
                        // Variante assimétrica → 5 medidas na ordem dos segmentos.
                        return $"Perfil Cartola {M(s0)}x{M(s1)}x{M(s2)}x{M(s3)}x{M(s4)}";
                    }

                    // ── Perfil U enrijecido ─────────────────────────────────────────────────────────
                    // Lei: 1°=5° segmentos; 2°=4° segmentos; todas as dobras de 90° e mesmo sentido.
                    // Padrão de sentidos: [A,A,A,A].
                    if (h0 == h1 && h1 == h2 && h2 == h3)
                    {
                        if (!EhDobraReta(0) || !EhDobraReta(3)) return generico();
                        if (!simSeg) return generico();
                        return $"Perfil U enrijecido {M(s2)}x{M(s1)}x{M(s0)}";
                    }

                    // ── Perfil Z enrijecido ─────────────────────────────────────────────────────────
                    // Lei: 1°=5° segmentos; 2°=4° segmentos; 1ª e 2ª dobras mesmo sentido; 3ª e 4ª
                    // dobras mesmo sentido (contrário); 2ª e 3ª dobras a 90° (já verificado); ângulos
                    // das dobras externas (1ª e 4ª) iguais entre si.
                    // Padrão de sentidos: [A,A,B,B] onde A≠B.
                    if (h0 == h1 && h2 == h3 && h0 != h2)
                    {
                        if (!simSeg) return generico();
                        double angLabioA = AnguloJoint(0);
                        double angLabioB = AnguloJoint(3);
                        if (Math.Abs(angLabioA - angLabioB) > 1.0) return generico();
                        int angGraus = (int)Math.Round(angLabioA);
                        return $"Perfil Z enrijecido a {angGraus}° {M(s2)}x{M(s1)}x{M(s0)}";
                    }

                    return generico();
                }

                default:
                    return generico();
            }
        }

        public void AtualizarPreview()
        {
            var debounceMs = _configService.ObterConfiguracao().PreviewDebounceMs;
            if (debounceMs <= 0)
            {
                ExecutarPreview();
                return;
            }

            _timerPreview?.Stop();
            _timerPreview = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(debounceMs)
            };
            _timerPreview.Tick += (_, _) =>
            {
                _timerPreview?.Stop();
                ExecutarPreview();
            };
            _timerPreview.Start();
        }

        private void ExecutarPreview()
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
                int? destaque = EstaNaFaseMedidas ? IndiceMedidaRapida : (int?)null;
                bool mostrarOrigem = ModoRapidoAtivo && EstaNaFaseDesenho && Segmentos.Count > 0;
                PreviewImage = _skiaRenderer.RenderToImageSource(polar, 780, 600, _geometryService, fonteCota, fonteAngulo, segmentoDestacado: destaque, destacarProximaOrigem: mostrarOrigem, forcarDesenho3D: true);

                // Dimensões totais acabadas da peça
                var dimensoes = _geometryService.CalcularDimensoesAcabadas(polar);
                DimensoesTotaisTexto = dimensoes != null
                    ? $"{dimensoes.Value.Largura:F0} × {dimensoes.Value.Altura:F0} mm  (L × A)"
                    : string.Empty;

                // Atualizar avisos (só indicadores na UI — popups somente ao adicionar ao pedido ou abrir detalhamento)
                Avisos.Clear();

                foreach (var av in _geometryService.VerificarDobrasAbaixoMinima(polar, ChapaSelecionada))
                    Avisos.Add($"⚠ {av}");

                if (_geometryService.PerfilCruzaASiMesmo(ChapaSelecionada.Codigo, comprimentoPreview, listSegs))
                    Avisos.Add("⚠ ATENÇÃO: O perfil cruza a si mesmo!");
            }
            catch (Exception ex)
            {
                // Silencia exceções provisórias de digitação incompleta
                System.Diagnostics.Debug.WriteLine($"Erro no preview: {ex.Message}");
            }
        }
    }
}
