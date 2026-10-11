using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public partial class ChangeOrderEditorDialog : Window
{
    private readonly ChangeOrderRow _draft;
    private readonly ChangeOrderEditorViewModel _vm;

    public ChangeOrderEditorDialog(ChangeOrderRow draft)
    {
        _draft = draft;
        _vm = new ChangeOrderEditorViewModel();
        _vm.LoadFrom(draft);
        InitializeComponent();
        DataContext = _vm;
        Owner = Application.Current?.MainWindow;
    }

    public void Configure(bool isNew)
    {
        TitleBlock.Text = isNew ? "Nueva orden de cambio" : "Editar orden de cambio";
        Loaded += (_, _) => TituloBox.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave(_draft))
            DialogResult = true;
    }

    public static bool Show(ChangeOrderRow oc, bool isNew)
    {
        var dlg = new ChangeOrderEditorDialog(oc);
        dlg.Configure(isNew);
        return dlg.ShowDialog() == true;
    }
}
