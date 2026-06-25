using System.Windows.Controls;
using System.Windows.Input;
using CapitalAco.DrawingMacro.App.ViewModels;

namespace CapitalAco.DrawingMacro.App.Views
{
    public partial class EditorPecaView : UserControl
    {
        public EditorPecaView()
        {
            InitializeComponent();
        }

        private void EditorPecaView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not EditorPecaViewModel vm) return;

            bool focoEmGrauOuMedida = ReferenceEquals(Keyboard.FocusedElement, GrauRapidoTextBox)
                || ReferenceEquals(Keyboard.FocusedElement, MedidaRapidaTextBox);
            bool focoEmCampoDeTexto = Keyboard.FocusedElement is TextBox or ComboBox;

            // Ctrl+Backspace: desfaz o último passo do Modo Rápido, mesmo durante a digitação da medida/ângulo.
            if (e.Key == Key.Back && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (vm.ModoRapidoAtivo)
                {
                    vm.DesfazerModoRapido();
                    e.Handled = true;
                }
                return;
            }

            // "E": foca o seletor de chapa (atalho global, exceto durante digitação em outro campo de texto).
            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.None && !focoEmCampoDeTexto)
            {
                ChapaComboBox.Focus();
                e.Handled = true;
                return;
            }

            if (!vm.ModoRapidoAtivo) return;

            // Enter confirma a fase atual do Modo Rápido (esqueleto, ângulo ou medida).
            if (e.Key == Key.Enter && (!focoEmCampoDeTexto || focoEmGrauOuMedida))
            {
                if (vm.EstaNaFaseDesenho) vm.ConfirmarEsqueletoRapido();
                else if (vm.EstaNaFaseGrau) vm.ConfirmarGrauPersonalizado();
                else if (vm.EstaNaFaseMedidas) vm.ConfirmarMedidaRapida();
                e.Handled = true;
                return;
            }

            // Setas/WASD, G e Backspace simples não devem interferir enquanto o usuário digita em algum campo.
            if (focoEmCampoDeTexto) return;

            switch (e.Key)
            {
                case Key.Up or Key.W:
                    vm.AdicionarSegmentoRapido("N");
                    e.Handled = true;
                    break;
                case Key.Down or Key.S:
                    vm.AdicionarSegmentoRapido("S");
                    e.Handled = true;
                    break;
                case Key.Left or Key.A:
                    vm.AdicionarSegmentoRapido("W");
                    e.Handled = true;
                    break;
                case Key.Right or Key.D:
                    vm.AdicionarSegmentoRapido("E");
                    e.Handled = true;
                    break;
                case Key.G:
                    vm.EntrarFaseGrau();
                    e.Handled = true;
                    break;
                case Key.Back:
                    vm.DesfazerModoRapido();
                    e.Handled = true;
                    break;
            }
        }
    }
}
