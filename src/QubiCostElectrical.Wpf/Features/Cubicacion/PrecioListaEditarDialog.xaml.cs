using System;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class PrecioListaEditarDialog : Window
{
    private readonly PrecioListaResumenVm vm;

    public PrecioListaEditarDialog(PrecioListaResumenVm vm, PrecioListaMaterialVm? _)
    {
        InitializeComponent();
        this.vm = vm;
        FechaPicker.SelectedDate = DateTime.Today;
    }

    private void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        var codigo = CodigoBox.Text.Trim();
        var descripcion = DescripcionBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            WpfDialogService.Info("Campo requerido", "El código del material es obligatorio.");
            CodigoBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            WpfDialogService.Info("Campo requerido", "La descripción del material es obligatoria.");
            DescripcionBox.Focus();
            return;
        }

        if (!decimal.TryParse(MatClpBox.Text, out var matClp))
            matClp = 0m;
        if (!decimal.TryParse(MoClpBox.Text, out var moClp))
            moClp = 0m;

        var unidad = UnidadCombo.SelectedItem is ComboBoxItem item
            ? (item.Content as string) ?? "un"
            : "un";

        vm.AgregarMaterial(
            codigo,
            descripcion,
            unidad,
            CategoriaBox.Text.Trim(),
            matClp,
            moClp,
            ProveedorBox.Text.Trim(),
            FechaPicker.SelectedDate ?? DateTime.Today);
        DialogResult = true;
    }
}
