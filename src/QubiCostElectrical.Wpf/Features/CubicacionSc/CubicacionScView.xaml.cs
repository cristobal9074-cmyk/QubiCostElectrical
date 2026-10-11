using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView : UserControl, IInGridSearchable
{
    public void OpenGridSearch() => CircuitosSearchOverlay.Open();
    public void StepGridSearch(bool backward)
    {
        if (backward) CircuitosSearchOverlay.FindPrevious();
        else CircuitosSearchOverlay.FindNext();
    }
    public void CloseGridSearch() => CircuitosSearchOverlay.Close();
    public bool IsGridSearchOpen => CircuitosSearchOverlay.IsOpen;

    // Catálogo maestro por ComboBox (no usar Tag: Tag lleva el nombre de propiedad a confirmar).
    internal static readonly ConditionalWeakTable<ComboBox, List<string>> ComboMasters = new();
    private readonly HashSet<ComboBox> _cancelledFilterCombos = new();
    private bool _isNavigatingFilterCombo;
    private CubicacionScViewModel Vm => (CubicacionScViewModel)DataContext;

    private void OnOpenSearchClick(object sender, RoutedEventArgs e) => CircuitosSearchOverlay.Open();

    private void OnFindExecuted(object sender, ExecutedRoutedEventArgs e) => CircuitosSearchOverlay.Open();

    public void ApplyNavContext(CubicacionScNavigationContext ctx)
    {
        SelectCuadroAndCircuito(ctx.CuadroId ?? ctx.CuadroNombre, ctx.CircuitoId);
    }

    public void SelectCuadroAndCircuito(string? cuadroId, string? circuitoId)
    {
        if (Vm == null) return;

        if (!string.IsNullOrWhiteSpace(cuadroId))
        {
            var matchCuadro = Vm.Cuadros.FirstOrDefault(c =>
                string.Equals(c.Id, cuadroId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Nombre, cuadroId, StringComparison.OrdinalIgnoreCase));
            if (matchCuadro != null)
            {
                Vm.SelectedCuadro = matchCuadro;
            }
        }

        if (!string.IsNullOrWhiteSpace(circuitoId))
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (Vm == null) return;
                var matchCircuito = Vm.CircuitosFiltrados.FirstOrDefault(c =>
                    string.Equals(c.Id, circuitoId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Numero.ToString(), circuitoId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"C{c.Numero}", circuitoId, StringComparison.OrdinalIgnoreCase) ||
                    c.Descripcion.Contains(circuitoId, StringComparison.OrdinalIgnoreCase));
                if (matchCircuito != null)
                {
                    Vm.SelectedCircuito = matchCircuito;
                }
            });
        }
    }
}
