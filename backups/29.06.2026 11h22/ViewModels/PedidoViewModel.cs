using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using CapitalAco.DrawingMacro.App.Models;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class PedidoViewModel : ObservableObject
    {
        private readonly IPdfGeneratorService _pdfGenerator;
        private readonly IConfigService _configService;
        private readonly IGeometryService _geometryService;
        private readonly ISkiaRenderer _skiaRenderer;

        [ObservableProperty]
        private string _observacao = string.Empty;

        private bool _alteradoDesdeUltimoSalvamento = false;
        public bool TemAlteracoesNaoSalvas => _alteradoDesdeUltimoSalvamento;

        partial void OnObservacaoChanged(string value) => _alteradoDesdeUltimoSalvamento = true;

        public ObservableCollection<PecaPedidoItem> Itens { get; } = new();

        public int ContagemItens => Itens.Count;

        public string TituloOrdemProducao => Itens.Count > 0
            ? $"Ordem de Produção ({Itens.Count})"
            : "Ordem de Produção";

        public PedidoViewModel(IPdfGeneratorService pdfGenerator, IConfigService configService, IGeometryService geometryService, ISkiaRenderer skiaRenderer)
        {
            _pdfGenerator = pdfGenerator;
            _configService = configService;
            _geometryService = geometryService;
            _skiaRenderer = skiaRenderer;

            Observacao = configService.ObterConfiguracao().RelatorioObservacao;
            _alteradoDesdeUltimoSalvamento = false;

            Itens.CollectionChanged += (_, _) =>
            {
                _alteradoDesdeUltimoSalvamento = true;
                OnPropertyChanged(nameof(ContagemItens));
                OnPropertyChanged(nameof(TituloOrdemProducao));
            };
        }

        private class PedidoArquivo
        {
            [JsonPropertyName("versao")]    public int Versao { get; set; } = 1;
            [JsonPropertyName("gerado_em")] public DateTime GeradoEm { get; set; }
            [JsonPropertyName("observacao")] public string Observacao { get; set; } = "";
            [JsonPropertyName("itens")]     public List<PecaPedidoItem> Itens { get; set; } = new();
            [JsonPropertyName("hash")]      public string? Hash { get; set; }
        }

        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        private static string ComputarHash(PedidoArquivo arquivo)
        {
            var semHash = new PedidoArquivo
            {
                Versao = arquivo.Versao,
                GeradoEm = arquivo.GeradoEm,
                Observacao = arquivo.Observacao,
                Itens = arquivo.Itens,
                Hash = null
            };
            var payload = JsonSerializer.Serialize(semHash, _jsonOpts);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private System.Windows.Media.ImageSource? GerarThumbnail(PecaPedidoItem item)
        {
            try
            {
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(
                    item.ChapaCodigo, item.Comprimento, item.Segmentos);
                return _skiaRenderer.RenderToImageSource(polar, 80, 60, _geometryService, mostrarMedidas: false);
            }
            catch { return null; }
        }

        [RelayCommand]
        private void SalvarPedido()
        {
            if (Itens.Count == 0)
            {
                MessageBox.Show("O pedido está vazio.", "Salvar Pedido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Salvar Pedido",
                Filter = "Pedido Capital Aço (*.pedido)|*.pedido",
                DefaultExt = "pedido",
                FileName = $"Pedido_{DateTime.Now:yyyyMMdd_HHmm}"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var arquivo = new PedidoArquivo
                {
                    GeradoEm = DateTime.Now,
                    Observacao = Observacao,
                    Itens = Itens.ToList()
                };
                arquivo.Hash = ComputarHash(arquivo);
                var json = JsonSerializer.Serialize(arquivo, _jsonOpts);
                File.WriteAllText(dlg.FileName, json);
                _alteradoDesdeUltimoSalvamento = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AbrirPedido()
        {
            if (_alteradoDesdeUltimoSalvamento)
            {
                var r = MessageBox.Show(
                    "O pedido atual tem alterações não salvas. Deseja descartá-las e abrir outro?",
                    "Abrir Pedido", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Abrir Pedido",
                Filter = "Pedido Capital Aço (*.pedido)|*.pedido"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var arquivo = JsonSerializer.Deserialize<PedidoArquivo>(json);
                if (arquivo == null) throw new InvalidDataException("Arquivo inválido.");

                const int VersaoSuportada = 1;
                if (arquivo.Versao > VersaoSuportada)
                {
                    var r = MessageBox.Show(
                        $"Este arquivo foi salvo com uma versão mais nova do aplicativo (v{arquivo.Versao}). " +
                        $"Abrir pode resultar em dados incompletos.\n\nDeseja continuar?",
                        "Versão Incompatível", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r != MessageBoxResult.Yes) return;
                }

                if (arquivo.Hash != null)
                {
                    var hashEsperado = ComputarHash(arquivo);
                    if (!string.Equals(arquivo.Hash, hashEsperado, StringComparison.OrdinalIgnoreCase))
                    {
                        var continuar = MessageBox.Show(
                            "O hash de integridade do arquivo não corresponde. O arquivo pode ter sido editado manualmente ou estar corrompido.\n\nDeseja abri-lo mesmo assim?",
                            "Integridade do Arquivo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (continuar != MessageBoxResult.Yes) return;
                    }
                }

                Itens.Clear();
                Observacao = arquivo.Observacao;
                foreach (var item in arquivo.Itens)
                {
                    item.ImagemPerfil = GerarThumbnail(item);
                    Itens.Add(item);
                }
                _alteradoDesdeUltimoSalvamento = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event Action<PecaPedidoItem>? EditarItemSolicitado;

        [RelayCommand]
        private void EditarItem(PecaPedidoItem? item)
        {
            if (item != null)
                EditarItemSolicitado?.Invoke(item);
        }

        public void AtualizarItem(PecaPedidoItem original, PecaPedidoItem novo)
        {
            int idx = Itens.IndexOf(original);
            if (idx >= 0)
                Itens[idx] = novo;
        }

        [RelayCommand]
        private void RemoverItem(PecaPedidoItem? item)
        {
            if (item != null)
            {
                Itens.Remove(item);
            }
        }

        [RelayCommand]
        private void LimparPedido()
        {
            Itens.Clear();
        }

        [RelayCommand]
        private async Task VisualizarPdf()
        {
            if (Itens.Count == 0)
            {
                MessageBox.Show("Adicione pelo menos uma peça ao pedido para gerar a Ordem de Produção.", "Carrinho Vazio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Captura dados na thread de UI antes de ir para o background
            var list = Itens.ToList();
            var observacao = Observacao;
            string caminho;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                caminho = await Task.Run(() => _pdfGenerator.GerarRelatorioPedido(list, observacao));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar PDF da Ordem de Produção: {ex.Message}", "Erro de Geração", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private async Task ImprimirPedido()
        {
            if (Itens.Count == 0)
            {
                MessageBox.Show("Adicione pelo menos uma peça ao pedido para imprimir a Ordem de Produção.", "Carrinho Vazio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var list = Itens.ToList();
            var observacao = Observacao;
            string caminho;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                caminho = await Task.Run(() => _pdfGenerator.GerarRelatorioPedido(list, observacao));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao imprimir a Ordem de Produção: {ex.Message}", "Erro de Impressão", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (File.Exists(caminho))
                FileShellHelper.ImprimirArquivo(caminho);
        }

        [RelayCommand]
        private void AbrirPastaRelatorios()
        {
            FileShellHelper.AbrirPasta(_configService.ObterCaminhoSaidaRelatorios());
        }
    }
}
