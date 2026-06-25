using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
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

        [ObservableProperty]
        private string _observacao = string.Empty;

        public ObservableCollection<PecaPedidoItem> Itens { get; } = new();

        public PedidoViewModel(IPdfGeneratorService pdfGenerator, IConfigService configService, IGeometryService geometryService)
        {
            _pdfGenerator = pdfGenerator;
            _configService = configService;
            _geometryService = geometryService;

            Observacao = configService.ObterConfiguracao().RelatorioObservacao;
        }

        private class PedidoArquivo
        {
            [JsonPropertyName("versao")]    public int Versao { get; set; } = 1;
            [JsonPropertyName("gerado_em")] public DateTime GeradoEm { get; set; }
            [JsonPropertyName("observacao")] public string Observacao { get; set; } = "";
            [JsonPropertyName("itens")]     public List<PecaPedidoItem> Itens { get; set; } = new();
        }

        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        private System.Windows.Media.ImageSource? GerarThumbnail(PecaPedidoItem item)
        {
            try
            {
                var polar = _geometryService.ConverterInstrucoesParaCoordenadasPolares(
                    item.ChapaCodigo, item.Comprimento, item.Segmentos);
                return SkiaRenderer.RenderToImageSource(polar, 80, 60, _geometryService, mostrarMedidas: false);
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
                var json = JsonSerializer.Serialize(arquivo, _jsonOpts);
                File.WriteAllText(dlg.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AbrirPedido()
        {
            if (Itens.Count > 0)
            {
                var r = MessageBox.Show(
                    "Isso substituirá o pedido atual. Deseja continuar?",
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

                Itens.Clear();
                Observacao = arquivo.Observacao;
                foreach (var item in arquivo.Itens)
                {
                    item.ImagemPerfil = GerarThumbnail(item);
                    Itens.Add(item);
                }
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
        private void VisualizarPdf()
        {
            if (Itens.Count == 0)
            {
                MessageBox.Show("Adicione pelo menos uma peça ao pedido para gerar a Ordem de Produção.", "Carrinho Vazio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var list = Itens.ToList();
                var caminho = _pdfGenerator.GerarRelatorioPedido(list, Observacao);

                if (File.Exists(caminho))
                {
                    FileShellHelper.CopiarArquivoParaAreaDeTransferencia(caminho);

                    // Abrir PDF automaticamente
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = caminho,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar PDF da Ordem de Produção: {ex.Message}", "Erro de Geração", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ImprimirPedido()
        {
            if (Itens.Count == 0)
            {
                MessageBox.Show("Adicione pelo menos uma peça ao pedido para imprimir a Ordem de Produção.", "Carrinho Vazio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var list = Itens.ToList();
                var caminho = _pdfGenerator.GerarRelatorioPedido(list, Observacao);

                if (File.Exists(caminho))
                {
                    FileShellHelper.ImprimirArquivo(caminho);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao imprimir a Ordem de Produção: {ex.Message}", "Erro de Impressão", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AbrirPastaRelatorios()
        {
            FileShellHelper.AbrirPasta(_configService.ObterCaminhoSaidaRelatorios());
        }
    }
}
