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
        private double _comprimentoPeca = 3000.0;

        [ObservableProperty]
        private Chapa? _chapaSelecionada;

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

        private double? _proximoGrauPersonalizado;

        // Galeria de Geradores de Peças
        public ObservableCollection<string> GeradoresDisponiveis { get; } = new() { "Boiadeira" };

        [ObservableProperty]
        private string _geradorSelecionado = "Boiadeira";

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

            // Segmentos default (um perfil em L simples de teste)
            Segmentos.Add(new Segmento { Direcao = "S", Angulo = 90, Medida = 50, TipoMedida = "e" });
            Segmentos.Add(new Segmento { Direcao = "E", Angulo = 90, Medida = 50, TipoMedida = "e" });

            Segmentos.CollectionChanged += Segmentos_CollectionChanged;
            
            AtualizarPreview();
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
        partial void OnComprimentoPecaChanged(double value) => AtualizarPreview();

        public void CarregarPecaDoModelo(ModeloPeca peca)
        {
            NomePeca = peca.Nome;
            ComprimentoPeca = peca.Comprimento ?? 3000.0;
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
            if (seg != null && Segmentos.Count > 1)
            {
                Segmentos.Remove(seg);
            }
        }

        [RelayCommand]
        private void LimparSegmentos()
        {
            Segmentos.Clear();
            // Mantém sempre pelo menos um de segurança
            Segmentos.Add(new Segmento { Direcao = "E", Angulo = 90, Medida = 50, TipoMedida = "e" });
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

            double angulo = _proximoGrauPersonalizado ?? 90.0;
            _proximoGrauPersonalizado = null;

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

            GrauRapidoAtual = _proximoGrauPersonalizado ?? 90.0;
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

        private void AtualizarStatusModoRapido()
        {
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
        private void SalvarNaBiblioteca()
        {
            if (ChapaSelecionada == null) return;

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

        [RelayCommand]
        private void AdicionarAoPedido()
        {
            if (ChapaSelecionada == null) return;

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
                lotes = new List<(double Quantidade, double Comprimento)> { (PedidoQuantidade, ComprimentoPeca) };
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

            try
            {
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(ChapaSelecionada.Codigo, ComprimentoPeca, Segmentos.ToList());
                var caminho = _pdfGeneratorService.GerarRelatorioDobra(polar, NomePeca, ChapaSelecionada.Codigo, ComprimentoPeca);

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
                return;

            try
            {
                // Converter para polares
                var listSegs = Segmentos.ToList();
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(ChapaSelecionada.Codigo, ComprimentoPeca, listSegs);

                // Renderizar preview 520x400, usando os tamanhos de fonte configurados
                var config = _configService.ObterConfiguracao();
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
                if (_geometryService.PerfilCruzaASiMesmo(ChapaSelecionada.Codigo, ComprimentoPeca, listSegs))
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
