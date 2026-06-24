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
    public partial class EditorPecaViewModel : ObservableObject
    {
        private readonly IGeometryService _geometryService;
        private readonly IGeradorPecaService _geradorPecaService;
        private readonly IBibliotecaPecasService _bibliotecaService;
        private readonly ICsvService _csvService;
        private readonly IPdfGeneratorService _pdfGeneratorService;

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

        // Evento para enviar itens ao carrinho do pedido
        public event Action<PecaPedidoItem>? EnviarAoPedido;

        public EditorPecaViewModel(
            IGeometryService geometryService,
            IGeradorPecaService geradorPecaService,
            IBibliotecaPecasService bibliotecaService,
            ICsvService csvService,
            IPdfGeneratorService pdfGeneratorService)
        {
            _geometryService = geometryService;
            _geradorPecaService = geradorPecaService;
            _bibliotecaService = bibliotecaService;
            _csvService = csvService;
            _pdfGeneratorService = pdfGeneratorService;

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

            var item = new PecaPedidoItem
            {
                ChapaCodigo = ChapaSelecionada.Codigo,
                Comprimento = ComprimentoPeca,
                Quantidade = PedidoQuantidade,
                NomePeca = NomePeca,
                Segmentos = Segmentos.ToList(),
                Observacao = PedidoObservacao
            };

            EnviarAoPedido?.Invoke(item);
            
            // Limpa formulário de quantidade do pedido
            PedidoQuantidade = 1;
            PedidoObservacao = string.Empty;
            
            MessageBox.Show("Peça adicionada ao carrinho da Ordem de Produção!", "Carrinho", MessageBoxButton.OK, MessageBoxImage.Information);
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

                // Renderizar preview 520x400
                PreviewImage = SkiaRenderer.RenderToImageSource(polar, 520, 400, _geometryService);

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
