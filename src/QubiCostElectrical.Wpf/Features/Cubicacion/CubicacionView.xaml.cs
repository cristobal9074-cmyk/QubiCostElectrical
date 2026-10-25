using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView : UserControl, IInGridSearchable
{
    public void OpenGridSearch() => ResourceSearchOverlay.Open();
    public void StepGridSearch(bool backward) { if (backward) ResourceSearchOverlay.FindPrevious(); else ResourceSearchOverlay.FindNext(); }
    public void CloseGridSearch() => ResourceSearchOverlay.Close();
    public bool IsGridSearchOpen => ResourceSearchOverlay.IsOpen;

    private CubicacionViewModel Vm => (CubicacionViewModel)DataContext;

    private TextBox? _codigoEditor;
    private bool _autocompleteHoverArmed;
    private Point _autocompleteHoverFreezePos;
    private readonly List<DataGridColumn> _sectorColumns = new();
    private int _zonaLibreVisibleCount = 25;
    private bool _suppressSelectionSync;
    private bool _isSyncingFromGrid;
    private bool _cantEditingStarted;
    private readonly Dictionary<TextBox, bool> _fieldEditingState = new();
    private readonly CubicacionSectorFormulaController _sectorFormulaController = new();

    private void OnOpenSearchClick(object sender, RoutedEventArgs e) => ResourceSearchOverlay.Open();
    private void OnFindExecuted(object sender, ExecutedRoutedEventArgs e) => ResourceSearchOverlay.Open();
}
