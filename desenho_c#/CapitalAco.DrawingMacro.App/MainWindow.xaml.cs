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
    }
}
