using CommunityToolkit.Mvvm.ComponentModel;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _selectedTabIndex = 0;

        public EditorPecaViewModel Editor { get; }
        public BibliotecaViewModel Biblioteca { get; }
        public PedidoViewModel Pedido { get; }
        public ConfiguracaoViewModel Configuracao { get; }

        public MainViewModel(
            EditorPecaViewModel editor,
            BibliotecaViewModel biblioteca,
            PedidoViewModel pedido,
            ConfiguracaoViewModel configuracao)
        {
            Editor = editor;
            Biblioteca = biblioteca;
            Pedido = pedido;
            Configuracao = configuracao;

            // Fazer a ligação reativa dos eventos entre ViewModels filhos
            Biblioteca.PecaSelecionadaParaEdicao += (peca) =>
            {
                Editor.CarregarPecaDoModelo(peca);
                SelectedTabIndex = 0; // Alterna para a aba do editor
            };

            Editor.EnviarAoPedido += (item) =>
            {
                Pedido.Itens.Add(item);
            };
        }
    }
}
