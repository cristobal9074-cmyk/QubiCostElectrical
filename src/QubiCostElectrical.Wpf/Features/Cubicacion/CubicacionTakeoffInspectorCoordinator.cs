using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Coordinador de lógica y estado para el Panel Lateral Acoplado (Docked Inspector) de la Hoja de Cubicación.
/// Sustituye la ventana modal flotante por un drawer integrado a la derecha (100% alto) que transfiere
/// valores o sectores directo a la fila activa del presupuesto (Alt+T).
/// </summary>
public sealed class CubicacionTakeoffInspectorCoordinator : ViewModelBase
{
    private readonly BudgetProjectSession _session;
    private readonly CubicacionViewModel _parent;

    private bool _isInspectorOpen;
    private HojaCubicacionItemVm? _selectedItem;
    private CubicacionRowVm? _targetRow;
    private int _targetSectorSlot = -1; // -1 = Cantidad (CANT), >= 0 = sector index
    private string _targetSectorName = "CANT";
    private string _targetRowText = "Sin selección";

    public ObservableCollection<HojaCubicacionItemVm> Items { get; } = new();

    public bool IsInspectorOpen
    {
        get => _isInspectorOpen;
        set
        {
            if (SetProperty(ref _isInspectorOpen, value))
            {
                if (value)
                {
                    if (_parent.IsScInspectorOpen)
                    {
                        _parent.IsScInspectorOpen = false;
                    }
                    ReloadItemsFromSession();
                    UpdateTargetInfo();
                }
                _parent.NotifyProperty(nameof(CubicacionViewModel.IsTakeoffInspectorOpen));
            }
        }
    }

    public HojaCubicacionItemVm? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                foreach (var it in Items) it.IsSelected = (it == value);
                OnPropertyChanged(nameof(GrandTotal));
                SelectedItemChanged?.Invoke(value);
            }
        }
    }

    public bool IsSectorTarget => TargetSectorSlot >= 0;

    public event Action<HojaCubicacionItemVm?>? SelectedItemChanged;

    public string TargetRowText
    {
        get => _targetRowText;
        private set => SetProperty(ref _targetRowText, value);
    }

    public string TargetSectorName
    {
        get => _targetSectorName;
        private set => SetProperty(ref _targetSectorName, value);
    }

    public int TargetSectorSlot
    {
        get => _targetSectorSlot;
        private set => SetProperty(ref _targetSectorSlot, value);
    }

    public decimal GrandTotal => Items.Sum(i => i.Subtotal);

    public ICommand CloseInspectorCommand { get; }
    public ICommand AddPartidaCommand { get; }
    public ICommand AddFilaCommand { get; }
    public ICommand AddColumnaCommand { get; }
    public ICommand ClearPickedCommand { get; }
    public ICommand TransferSubtotalCommand { get; }

    public CubicacionTakeoffInspectorCoordinator(BudgetProjectSession session, CubicacionViewModel parent)
    {
        _session = session;
        _parent = parent;

        CloseInspectorCommand = new RelayCommand(CloseInspector);
        AddPartidaCommand = new RelayCommand(AddPartida, () => _parent.CanEdit);
        AddFilaCommand = new RelayCommand(AddFila, () => _parent.CanEdit && SelectedItem != null);
        AddColumnaCommand = new RelayCommand(AddColumna, () => _parent.CanEdit && SelectedItem != null);
        ClearPickedCommand = new RelayCommand(ClearAllPicked, () => _parent.CanEdit);
        TransferSubtotalCommand = new RelayCommand(TransferSelectedItemSubtotal, () => _parent.CanEdit && SelectedItem != null);
    }

    public void ToggleInspector()
    {
        IsInspectorOpen = !IsInspectorOpen;
    }

    public void CloseInspector()
    {
        IsInspectorOpen = false;
    }

    public void OpenForSector(CubicacionRowVm row, int absoluteOffset)
    {
        _targetRow = row;
        TargetSectorSlot = absoluteOffset;
        UpdateTargetInfo();
        IsInspectorOpen = true;
    }

    public void OpenForQuantity(CubicacionRowVm row)
    {
        _targetRow = row;
        TargetSectorSlot = -1;
        UpdateTargetInfo();
        IsInspectorOpen = true;
    }

    public void UpdateTargetInfo()
    {
        var row = _targetRow ?? _parent.SelectedRow ?? _parent.Rows.FirstOrDefault(r => r.IsSelected) ?? _parent.Rows.FirstOrDefault();
        _targetRow = row;

        if (row == null)
        {
            TargetRowText = "Ninguna fila seleccionada en presupuesto";
            TargetSectorName = "CANT";
            return;
        }

        int index = _parent.Rows.IndexOf(row) + 1;
        string desc = string.IsNullOrWhiteSpace(row.Descripcion) ? row.Codigo : row.Descripcion;

        if (TargetSectorSlot >= 0)
        {
            var zonas = _session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
            string secName = TargetSectorSlot < zonas.Count ? zonas[TargetSectorSlot].Nombre : $"Sector {TargetSectorSlot + 1}";
            TargetSectorName = secName;
            TargetRowText = $"Fila #{index} · {(string.IsNullOrWhiteSpace(row.Codigo) ? "" : $"[{row.Codigo}] ")}{desc}";
        }
        else
        {
            TargetSectorName = "CANT";
            TargetRowText = $"Fila #{index} · {(string.IsNullOrWhiteSpace(row.Codigo) ? "" : $"[{row.Codigo}] ")}{desc}";
        }
    }

    public void ReloadItemsFromSession()
    {
        Items.Clear();
        var saved = _session.HojaCubicacion;
        if (saved != null && saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var itemVm = HojaCubicacionItemVm.FromSaved(s);
                itemVm.PropertyChanged += OnItemPropertyChanged;
                Items.Add(itemVm);
            }
        }
        else
        {
            var defaultItem = new HojaCubicacionItemVm("1.1", "Canalizaciones y Tuberías", "ml", 20);
            var zonas = _session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
            if (zonas.Count > 0 && defaultItem.Filas.Count > 0)
            {
                var labelRow = defaultItem.Filas[0];
                for (int i = 0; i < zonas.Count && i < labelRow.Valores.Count; i++)
                {
                    labelRow.Valores[i] = zonas[i].Nombre;
                }
            }
            defaultItem.PropertyChanged += OnItemPropertyChanged;
            Items.Add(defaultItem);
            SaveToSession();
        }

        SelectedItem = Items.FirstOrDefault();
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HojaCubicacionItemVm.Subtotal))
        {
            OnPropertyChanged(nameof(GrandTotal));
            SaveToSession();
        }
    }

    public void AddPartida()
    {
        int nextNum = Items.Count + 1;
        var newItem = new HojaCubicacionItemVm($"1.{nextNum}", $"Partida {nextNum}", "ml", 20);
        var zonas = _session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        if (zonas.Count > 0 && newItem.Filas.Count > 0)
        {
            var labelRow = newItem.Filas[0];
            for (int i = 0; i < zonas.Count && i < labelRow.Valores.Count; i++)
            {
                labelRow.Valores[i] = zonas[i].Nombre;
            }
        }
        newItem.PropertyChanged += OnItemPropertyChanged;
        Items.Add(newItem);
        SelectedItem = newItem;
        SaveToSession();
    }

    public void AddFila()
    {
        if (SelectedItem == null) return;
        SelectedItem.AddFila(SelectedItem.Filas.FirstOrDefault()?.Valores.Count ?? 20, "");
        SaveToSession();
    }

    public void AddColumna()
    {
        if (SelectedItem == null) return;
        SelectedItem.AddColumn();
        SaveToSession();
    }

    public void ClearAllPicked()
    {
        foreach (var it in Items)
        {
            it.IsPicked = false;
            it.PickedTargetKey = null;
            foreach (var f in it.Filas)
            {
                f.IsPicked = false;
                f.PickedTargetKey = null;
            }
        }
        SaveToSession();
    }

    public void TransferFilaValue(HojaCubicacionFilaVm fila, int? columnIndex = null)
    {
        if (fila == null || fila.IsLabelRow) return;

        var row = _targetRow ?? _parent.SelectedRow ?? _parent.Rows.FirstOrDefault(r => r.IsSelected) ?? _parent.Rows.FirstOrDefault();
        if (row == null) return;

        string targetKey = TargetSectorSlot >= 0 ? $"row:{row.Source.Id}:sec:{TargetSectorSlot}" : $"row:{row.Source.Id}:qty";

        foreach (var it in Items)
        {
            foreach (var f in it.Filas)
            {
                if (f.PickedTargetKey == targetKey)
                {
                    f.IsPicked = false;
                    f.PickedTargetKey = null;
                }
            }
        }

        fila.IsPicked = true;
        fila.PickedTargetKey = targetKey;

        decimal valorTransferir;
        string exprTransferir;

        if (columnIndex.HasValue && columnIndex.Value >= 0 && columnIndex.Value < fila.Valores.Count)
        {
            string rawVal = fila.Valores[columnIndex.Value] ?? "";
            string dispVal = columnIndex.Value < fila.ValoresDisplay.Count ? fila.ValoresDisplay[columnIndex.Value] : rawVal;
            exprTransferir = string.IsNullOrWhiteSpace(rawVal) ? "0" : rawVal;
            if (decimal.TryParse(dispVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                valorTransferir = parsed;
            else if (decimal.TryParse(dispVal, NumberStyles.Any, CultureInfo.GetCultureInfo("es-CL"), out parsed))
                valorTransferir = parsed;
            else
                valorTransferir = 0m;
        }
        else
        {
            valorTransferir = fila.Suma;
            exprTransferir = valorTransferir.ToString("0.####", CultureInfo.InvariantCulture);
        }

        if (TargetSectorSlot >= 0)
        {
            _parent.EnsureRowInSession(row);
            _parent.SetSectorExpressionDirect(row, TargetSectorSlot, exprTransferir);
        }
        else
        {
            _parent.EnsureRowInSession(row);
            row.Cantidad = valorTransferir;
            row.Source.TotalExpression = exprTransferir;
            _parent.OnRowEdited(row);
        }

        _parent.HasPendingChanges = true;
        _parent.SaveStatusText = "Cambios sin guardar";
        SaveToSession();
        row.RefreshDisplayProperties();
        _parent.RightPanel.LoadRow(row);
        _parent.ValidateRow(row);
        _parent.MarkDirty();
        _parent.NotifyCubicacionSummaryChanged();
    }

    public void TransferSelectedItemSubtotal()
    {
        if (SelectedItem == null) return;
        var row = _targetRow ?? _parent.SelectedRow ?? _parent.Rows.FirstOrDefault(r => r.IsSelected) ?? _parent.Rows.FirstOrDefault();
        if (row == null) return;

        string targetKey = TargetSectorSlot >= 0 ? $"row:{row.Source.Id}:sec:{TargetSectorSlot}" : $"row:{row.Source.Id}:qty";

        foreach (var it in Items)
        {
            if (it.PickedTargetKey == targetKey)
            {
                it.IsPicked = false;
                it.PickedTargetKey = null;
            }
        }

        SelectedItem.IsPicked = true;
        SelectedItem.PickedTargetKey = targetKey;

        decimal valor = SelectedItem.Subtotal;
        string expr = valor.ToString("0.####", CultureInfo.InvariantCulture);

        if (TargetSectorSlot >= 0)
        {
            _parent.EnsureRowInSession(row);
            _parent.SetSectorExpressionDirect(row, TargetSectorSlot, expr);
        }
        else
        {
            _parent.EnsureRowInSession(row);
            row.Cantidad = valor;
            row.Source.TotalExpression = expr;
            _parent.OnRowEdited(row);
        }

        _parent.HasPendingChanges = true;
        _parent.SaveStatusText = "Cambios sin guardar";
        SaveToSession();
        row.RefreshDisplayProperties();
        _parent.RightPanel.LoadRow(row);
        _parent.ValidateRow(row);
        _parent.MarkDirty();
        _parent.NotifyCubicacionSummaryChanged();
    }

    public void SaveToSession()
    {
        if (_session.IsReadOnly) return;
        _session.HojaCubicacion.Clear();
        _session.HojaCubicacion.AddRange(Items.Select(vm => vm.ToSaved()));
        _session.MarkDirty("hoja_cubicacion");
        _parent.HasPendingChanges = true;
    }
}
