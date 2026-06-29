using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapitalAco.DrawingMacro.App.Views
{
    public partial class PedidoView : UserControl
    {
        public PedidoView()
        {
            InitializeComponent();
        }

        private void DataGridCell_SingleClickEdit(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridCell cell && !cell.IsEditing && !cell.IsReadOnly)
            {
                cell.Focus();
                DependencyObject parent = cell;
                while (parent != null && parent is not DataGrid)
                    parent = VisualTreeHelper.GetParent(parent);
                (parent as DataGrid)?.BeginEdit(e);
            }
        }
    }
}
