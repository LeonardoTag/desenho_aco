using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CapitalAco.DrawingMacro.App.ViewModels;

namespace CapitalAco.DrawingMacro.App.Views
{
    public partial class EditorPecaView : UserControl
    {
        public EditorPecaView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is EditorPecaViewModel oldVm) oldVm.PropertyChanged -= Vm_PropertyChanged;
            if (e.NewValue is EditorPecaViewModel newVm) newVm.PropertyChanged += Vm_PropertyChanged;
        }

        // Ao entrar nas fases de Ângulo/Medida do Modo Rápido, foca e seleciona o campo correspondente
        // para que o usuário possa digitar imediatamente, sem precisar usar o mouse.
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // IndiceMedidaRapida muda a cada Enter dentro da própria fase de Medidas (FaseRapida não muda),
            // então também precisa disparar o reforco/seleção do campo para sobrescrever o valor seguinte.
            if (e.PropertyName != nameof(EditorPecaViewModel.FaseRapida) && e.PropertyName != nameof(EditorPecaViewModel.IndiceMedidaRapida)) return;
            if (sender is not EditorPecaViewModel vm) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (vm.EstaNaFaseGrau)
                {
                    GrauRapidoTextBox.Focus();
                    GrauRapidoTextBox.SelectAll();
                }
                else if (vm.EstaNaFaseMedidas)
                {
                    MedidaRapidaTextBox.Focus();
                    MedidaRapidaTextBox.SelectAll();
                }
            }), DispatcherPriority.Input);
        }

        private void EditorPecaView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not EditorPecaViewModel vm) return;

            bool focoEmGrauOuMedida = ReferenceEquals(Keyboard.FocusedElement, GrauRapidoTextBox)
                || ReferenceEquals(Keyboard.FocusedElement, MedidaRapidaTextBox);
            bool focoEmCampoDeTexto = Keyboard.FocusedElement is TextBox or ComboBox;

            // Esc: sai um nível do modo/sub-fase atual do Modo Rápido (não faz nada no Modo Clássico).
            if (e.Key == Key.Escape)
            {
                if (vm.ModoRapidoAtivo)
                {
                    vm.SairDoModoAtual();
                    e.Handled = true;
                }
                return;
            }

            // Shift+Enter: adiciona a peça atual à ordem de produção, de qualquer lugar do editor.
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                vm.AdicionarAoPedidoCommand.Execute(null);
                e.Handled = true;
                return;
            }

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

            // "C": foca e seleciona o campo de comprimento (atalho global, exceto durante digitação em outro campo de texto).
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.None && !focoEmCampoDeTexto)
            {
                ComprimentoTextBox.Focus();
                ComprimentoTextBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (!vm.ModoRapidoAtivo) return;

            // Enter (sem Shift) confirma a fase atual do Modo Rápido (esqueleto, ângulo ou medida).
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && (!focoEmCampoDeTexto || focoEmGrauOuMedida))
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
