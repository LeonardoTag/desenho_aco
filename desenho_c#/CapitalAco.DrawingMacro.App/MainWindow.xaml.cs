using System.Windows;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App
{
    public partial class MainWindow : Window
    {
        public MainWindow(ICsvService csvService, IBibliotecaPecasService bibliotecaService)
        {
            InitializeComponent();

            try
            {
                // Carrega e vincula dados para verificação visual da Fase 2
                lstChapas.ItemsSource = csvService.CarregarChapas();
                lstBiblioteca.ItemsSource = bibliotecaService.ListarModelos();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao carregar dados na interface: {ex.Message}",
                    "Erro de Carregamento",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
