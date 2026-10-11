using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

public partial class PlantillasView : UserControl
{
    public static ApuTipoRecurso[] ApuTiposRecurso { get; } = (ApuTipoRecurso[])Enum.GetValues(typeof(ApuTipoRecurso));
    public static TipoCalculoParametrico[] TiposCalculo { get; } = (TipoCalculoParametrico[])Enum.GetValues(typeof(TipoCalculoParametrico));
    public static string[] RolesRecurso { get; } = { "Maestro", "Esclavo", "Operación" };

    public PlantillasView(PlantillasVm vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Cultura de los bindings = cultura del sistema (es-CL): sin esto WPF usa en-US,
        // rechaza "1,5" al teclear y muestra puntos donde el resto de la app muestra comas.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        // SelectedItems no es DependencyProperty: la selección (multi) viaja a la VM por code-behind,
        // mismo patrón que MaterialPickerDialog. alimenta la barra contextual ámbar.
        ResourcesGrid.SelectionChanged += ResourcesGrid_SelectionChanged;

        // El guard de teclado se suscribe ANTES de activar DataGridExcelNavigation (los handlers
        // comparten evento por orden de suscripción): Backspace/Delete no vacía la fila y las
        // flechas no secuestran el cursor mientras se edita una fórmula en vivo.
        ResourcesGrid.PreviewKeyDown += ResourcesGrid_PreviewKeyDownGuard;
        DataGridExcelNavigation.SetEnable(ResourcesGrid, true);
    }

    /// <summary>
    /// Fuera de edición, Backspace/Delete con la fila seleccionada llegaría a
    /// <c>DataGridExcelNavigation.ClearSelection</c> y vaciaría Descripción/Código
    /// («al cambiar el valor se elimina la descripción»). Política de esta vista:
    /// no se borra contenido desde la navegación; el borrado se hace editando la celda.
    /// Las flechas/Home/End y el borrado dentro de una celda viva (fórmula) los resuelve
    /// el nav compartido — este handler corre primero y solo pone la barrera.
    /// </summary>
    private void ResourcesGrid_PreviewKeyDownGuard(object sender, KeyEventArgs e)
    {
        if (DataGridExcelNavigation.IsEditing(ResourcesGrid))
        {
            return; // editor nativo (columna Valor): sus propios hooks manejan las teclas
        }

        bool borrado = e.Key is Key.Delete or Key.Back;
        if (borrado && e.OriginalSource is not TextBox)
        {
            e.Handled = true;
        }
    }

    private void ResourcesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Las combos vivas dentro de celdas (Regla, Tipo, Rol, Dividir de) participan en
        // el BindingGroup de la fila de la DataGrid: su traslado al source queda diferido
        // hasta el commit de celda y se pierde si el usuario sale de la grilla sin pasar
        // por una celda editable (bug: «cambiar Regla a Espaciamiento no se guarda; al
        // cambiar de pestaña o de plantilla se queda en Factor»). Forzar el traslado en
        // cuanto cambia la selección.
        if (e.OriginalSource is ComboBox combo)
        {
            System.Windows.Data.BindingOperations
                .GetBindingExpression(combo, System.Windows.Controls.Primitives.Selector.SelectedItemProperty)
                ?.UpdateSource();
            System.Windows.Data.BindingOperations
                .GetBindingExpression(combo, System.Windows.Controls.Primitives.Selector.SelectedValueProperty)
                ?.UpdateSource();
        }

        if (DataContext is not PlantillasVm vm) return;
        vm.SetSelectedRecursos(ResourcesGrid.SelectedItems.OfType<ConjuntoRecursoVm>());
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ResourcesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        ResourcesGrid.SelectedIndex = -1;
        ResourcesGrid.Focus();
    }

    /// <summary>
    /// «Esclavo de:» de la barra contextual: al elegir un maestro se aplica de inmediato a las
    /// filas seleccionadas (AsignarMaestroSeleccionCommand) y se limpia el combo para poder
    /// elegir el mismo maestro otra vez en otra selección.
    /// </summary>
    private void AsignarMaestroCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return; // también el reset a null posterior a aplicar
        if (DataContext is not PlantillasVm vm) return;
        if (e.AddedItems[0] is not MaestroOptionVm opcion) return;

        if (vm.AsignarMaestroSeleccionCommand.CanExecute(opcion.Id))
            vm.AsignarMaestroSeleccionCommand.Execute(opcion.Id);

        if (sender is ComboBox combo && ReferenceEquals(combo.SelectedItem, opcion))
            combo.SelectedItem = null;
    }
}
