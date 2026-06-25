using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class PedidoViewModel : ObservableObject
    {
        private readonly IPdfGeneratorService _pdfGenerator;
        private readonly IConfigService _configService;

        [ObservableProperty]
        private string _observacao = string.Empty;

        public ObservableCollection<PecaPedidoItem> Itens { get; } = new();

        public PedidoViewModel(IPdfGeneratorService pdfGenerator, IConfigService configService)
        {
            _pdfGenerator = pdfGenerator;
            _configService = configService;
            
            // Pega a observação padrão das configurações
            Observacao = configService.ObterConfiguracao().RelatorioObservacao;
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
