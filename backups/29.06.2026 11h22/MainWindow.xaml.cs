using System.ComponentModel;
using System.Windows;
using CapitalAco.DrawingMacro.App.ViewModels;

namespace CapitalAco.DrawingMacro.App
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is MainViewModel vm && vm.Pedido.TemAlteracoesNaoSalvas)
            {
                var result = MessageBox.Show(
                    "O pedido tem alterações não salvas. Deseja sair sem salvar?",
                    "Confirmar Saída",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }
        }
    }
}
