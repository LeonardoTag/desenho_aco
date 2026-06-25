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
        private bool _modoRapidoAtivo;

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

            Segmentos.Add(novo);
        }

        [RelayCommand]
        private void RemoverSegmento(Segmento? seg)
        {
            if (seg != null)
            {
                Segmentos.Remove(seg);
            }
        }

        [RelayCommand]
        private void LimparSegmentos()
        {
            Segmentos.Clear();
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

        [RelayCommand]
        public void DesfazerModoRapido()
        {
            if (!ModoRapidoAtivo) return;

            switch (FaseRapida)
            {
                case FaseModoRapido.Desenho:
                    if (Segmentos.Count > 0)
                    {
                        Segmentos.RemoveAt(Segmentos.Count - 1);
                    }
                    break;

                case FaseModoRapido.Grau:
                    FaseRapida = FaseModoRapido.Desenho;
                    break;

                case FaseModoRapido.Medidas:
                    if (IndiceMedidaRapida > 0)
                    {
                        IndiceMedidaRapida--;
                        DesfazerMedidaDoSegmento(IndiceMedidaRapida);
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
                        DesfazerMedidaDoSegmento(IndiceMedidaRapida);
                    }
                    break;
            }

            AtualizarStatusModoRapido();
        }

        private void DesfazerMedidaDoSegmento(int indice)
        {
            var seg = Segmentos[indice];
            seg.MedidaDefinida = false;
            Segmentos[indice] = seg; // força notificação (refresh do DataGrid + prévia)
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

        public void AtualizarPreview()
        {
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
                PreviewImage = SkiaRenderer.RenderToImageSource(polar, 520, 400, _geometryService, fonteCota, fonteAngulo);

                // Dimensões totais acabadas da peça
                var dimensoes = _geometryService.CalcularDimensoesAcabadas(polar);
                DimensoesTotaisTexto = dimensoes != null
                    ? $"Dimensões Totais: {dimensoes.Value.Largura:F0} mm (L) x {dimensoes.Value.Altura:F0} mm (A)"
                    : string.Empty;

                // Atualizar avisos
                Avisos.Clear();

                // 1. Dobras abaixo da mínima
                var avisosDobra = _geometryService.VerificarDobrasAbaixoMinima(polar, ChapaSelecionada);
                foreach (var av in avisosDobra)
                {
                    Avisos.Add(av);
                }

                // 2. Auto-interseção
                if (_geometryService.PerfilCruzaASiMesmo(ChapaSelecionada.Codigo, comprimentoPreview, listSegs))
                {
                    Avisos.Add("ATENÇÃO: O perfil cruza a si mesmo!");
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
