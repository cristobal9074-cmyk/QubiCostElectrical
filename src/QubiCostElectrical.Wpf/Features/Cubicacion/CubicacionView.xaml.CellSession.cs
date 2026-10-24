using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView
{
    private void DispatchCell(CubicacionCellInput input, KeyEventArgs e, TextBox? box = null)
    {
        var decision = CubicacionCellSession.Decide(input);
        if (decision.Action == CubicacionCellAction.RunShortcut)
        {
            if (decision.CommitEditBefore)
                CommitGridEdit();
            if (CubicacionShortcutRouting.TryHandle(e, Vm))
            {
                e.Handled = true;
                return;
            }

            decision = CubicacionCellSession.Decide(input with { IgnoreShortcut = true });
        }

        if (decision.FinalizeSectorPick || IsSectorDecision(decision.Action))
        {
            _suppressSelectionSync = true;
            try
            {
                if (decision.FinalizeSectorPick)
                    FinalizeSectorPick();
                ApplyCellDecision(decision, e, box);
            }
            finally
            {
                _suppressSelectionSync = false;
            }

            SyncGridSelectionFromGrid();
        }
        else
        {
            ApplyCellDecision(decision, e, box);
        }

        if (decision.Handled)
            e.Handled = true;
    }

    private static bool IsSectorDecision(CubicacionCellAction action) => action is
        CubicacionCellAction.SectorPickArrow or
        CubicacionCellAction.FocusSectorSlot or
        CubicacionCellAction.FocusCodigoColumn or
        CubicacionCellAction.SectorEdge;

    private void ApplyCellDecision(CubicacionCellDecision decision, KeyEventArgs? e, TextBox? box = null, MouseButtonEventArgs? click = null)
    {
        switch (decision.Action)
        {
            case CubicacionCellAction.PassThrough:
            case CubicacionCellAction.RunShortcut:
                break;
            case CubicacionCellAction.Swallow:
                break;
            case CubicacionCellAction.StartEdit:
                if (box != null)
                {
                    SetFieldEditingState(box, true);
                    box.SelectionLength = 0;
                    box.CaretIndex = box.Text.Length;
                }
                break;
            case CubicacionCellAction.BeginEditing:
                if (box != null)
                    SetFieldEditingState(box, true);
                break;
            case CubicacionCellAction.ClearField:
                if (box != null)
                {
                    box.Text = "";
                    SetFieldEditingState(box, true);
                }
                break;
            case CubicacionCellAction.CancelStay:
                CancelActiveField(box);
                break;
            case CubicacionCellAction.CancelAndFocusGrid:
                CancelCantPick();
                SetFieldEditingState(box ?? ActiveCantBox, false);
                FocusDecisionTarget(decision);
                break;
            case CubicacionCellAction.RevertAndFocusGrid:
                if (box != null)
                    BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateTarget();
                ResourceGrid?.Focus();
                break;
            case CubicacionCellAction.CommitAndMove:
                CommitActiveField(box);
                if (box != null)
                    SetFieldEditingState(box, false);
                FocusDecisionTarget(decision);
                if (box != null && decision.Target == CubicacionMoveTarget.None)
                    box.SelectAll();
                break;
            case CubicacionCellAction.MoveFocus:
                if (box != null)
                    SetFieldEditingState(box, false);
                FocusDecisionTarget(decision);
                break;
            case CubicacionCellAction.InsertReference:
                if (box == ActiveCantBox || box?.Tag as string == "PartidaCant" || ReferenceEquals(box, CantHeaderBox))
                    ApplyCantPick(decision.RowIndex);
                else
                    CubicacionFormulaPicker.ApplyUnidadPick(box ?? ActiveUnidadBox, decision.RowIndex, Vm.Rows, ResourceGrid, UnidadColumn);
                break;
            case CubicacionCellAction.CloseSuggestions:
                UnidadSuggestionsPopup.IsOpen = false;
                break;
            case CubicacionCellAction.MoveSuggestion:
                MoveUnidadSuggestion(decision.Delta);
                break;
            case CubicacionCellAction.ApplySuggestion:
                if (UnidadSuggestionsList.SelectedItem is CubicacionFormulaPicker.UnidadSuggestion sug)
                    ApplyUnidadSuggestion(sug);
                break;
            case CubicacionCellAction.FocusHeader:
                CommitGridEdit();
                ResourceGrid.SelectedCells.Clear();
                ResourceGrid.SelectedItem = null;
                ResourceGrid.CurrentCell = default;
                var header = decision.Target == CubicacionMoveTarget.CantHeader ? ActiveCantBox : ActiveUnidadBox;
                header.Focus();
                header.SelectAll();
                break;
            case CubicacionCellAction.Paste:
                OnPasteKeyDown(ResourceGrid, e!);
                break;
            case CubicacionCellAction.Copy:
                DataGridExcelNavigation.CopySelectionToClipboard(ResourceGrid);
                break;
            case CubicacionCellAction.Cut:
                CutGridSelection();
                break;
            case CubicacionCellAction.ClearCells:
                ClearDeletableCells();
                break;
            case CubicacionCellAction.AddRow:
                if (Vm.AddRowCommand.CanExecute(null))
                    Vm.AddRowCommand.Execute(null);
                break;
            case CubicacionCellAction.DeleteRows:
                if (decision.CommitEditBefore)
                    CommitGridEdit();
                if (Vm.DeleteRowCommand.CanExecute(null))
                    Vm.DeleteRowCommand.Execute(null);
                break;
            case CubicacionCellAction.DuplicateRow:
                if (Vm.DuplicateRowCommand.CanExecute(null))
                    Vm.DuplicateRowCommand.Execute(null);
                break;
            case CubicacionCellAction.InsertRow:
                if (Vm.InsertRowCommand.CanExecute(null))
                {
                    if (decision.CommitEditBefore)
                        CommitGridEdit();
                    Vm.InsertRowCommand.Execute(null);
                }
                break;
            case CubicacionCellAction.AddPartida:
                if (Vm.AddPartidaCommand.CanExecute(null))
                    Vm.AddPartidaCommand.Execute(null);
                break;
            case CubicacionCellAction.DeletePartida:
                if (Vm.DeletePartidaCommand.CanExecute(null))
                    Vm.DeletePartidaCommand.Execute(null);
                break;
            case CubicacionCellAction.Save:
                if (Vm.SaveCommand.CanExecute(null))
                    ((ICommand)Vm.SaveCommand).Execute(null);
                break;
            case CubicacionCellAction.ClearPartidaFilter:
                if (Vm.ClearPartidaFilterCommand.CanExecute(null))
                    Vm.ClearPartidaFilterCommand.Execute(null);
                break;
            case CubicacionCellAction.TogglePartidaTag:
                if (Vm.SelectedPartida is { EsPartida: true } partida)
                    partida.IsTagged = !partida.IsTagged;
                break;
            case CubicacionCellAction.CloseAutocomplete:
                if (Vm.AutocompleteVisible)
                {
                    Vm.AutocompleteVisible = false;
                    _codigoEditor?.Focus();
                }
                break;
            case CubicacionCellAction.MoveAutocomplete:
                MoveAutocompleteSelection(decision.Delta);
                break;
            case CubicacionCellAction.ApplyAutocomplete:
                ApplyAutocompleteSelection();
                break;
            case CubicacionCellAction.CommitTypedCodigo:
                CommitTypedCodigo(box);
                break;
            case CubicacionCellAction.ApplyAutocompleteTab:
                ApplyAutocompleteTab();
                break;
            case CubicacionCellAction.OpenHoja:
                if (click?.OriginalSource is DependencyObject altDep && FindParent<DataGridCell>(altDep)?.DataContext is CubicacionRowVm sectorRow)
                    Vm.PickSectorValueFromHojaCubicacion(sectorRow, decision.Delta);
                break;
            case CubicacionCellAction.PickUnidadFromRow:
                PickUnidadFromClick(decision, commit: false);
                break;
            case CubicacionCellAction.PickUnidadIntoEditor:
                if (click?.OriginalSource is DependencyObject unidadDep &&
                    FindParent<DataGridCell>(unidadDep)?.DataContext is CubicacionRowVm unidadRow &&
                    Keyboard.FocusedElement is TextBox unidadEditor)
                    CubicacionFormulaPicker.TryPickUnidad(unidadEditor, unidadRow, UnidadHeaderBox, Vm);
                break;
            case CubicacionCellAction.PickCantCell:
                PickCantFromClick(decision.RowIndex);
                break;
            case CubicacionCellAction.PickSectorReference:
                PickSectorFromClick(decision);
                break;
            case CubicacionCellAction.BeginGridEdit:
                BeginEditClickedCell(click);
                break;
            case CubicacionCellAction.SectorPickArrow:
                if (e != null && Keyboard.FocusedElement is TextBox sectorTb)
                {
                    var key = e.Key == Key.System ? e.SystemKey : e.Key;
                    _sectorFormulaController.HandleSectorPick(key, sectorTb, Vm, ResourceGrid, _sectorColumns);
                }
                break;
            case CubicacionCellAction.FocusSectorSlot:
                FocusSectorColumn(ResourceGrid.CurrentCell.Item ?? ResourceGrid.SelectedItem, 0);
                break;
            case CubicacionCellAction.FocusCodigoColumn:
                FocusCodigoColumn(ResourceGrid.CurrentCell.Item ?? ResourceGrid.SelectedItem);
                break;
            case CubicacionCellAction.SectorEdge:
                JumpSectorEdge(decision.Delta);
                break;
        }
    }

    private CubicacionCellInput ReadField(CubicacionInputSource source, KeyEventArgs e, TextBox box) => new()
    {
        Source = source,
        Key = CubicacionCellKeyMap.From(e),
        Modifiers = CubicacionCellKeyMap.ModifiersOf(e),
        EditingField = IsFieldEditing(box),
        FormulaMode = source == CubicacionInputSource.UnidadHeader
            ? CubicacionFormulaPicker.IsUnidadFormulaMode(box)
            : CubicacionFormulaPicker.IsFormulaMode(box),
        SuggestionsOpen = source == CubicacionInputSource.UnidadHeader && UnidadSuggestionsPopup.IsOpen,
        SuggestionsHaveSelection = UnidadSuggestionsList.SelectedItem != null,
        SelectionLength = box.SelectionLength,
        CaretIndex = box.CaretIndex,
        TextLength = box.Text?.Length ?? 0,
        PickIndex = source == CubicacionInputSource.CantHeader ? CubicacionFormulaPicker.CantPickIndex : CubicacionFormulaPicker.UnidadPickIndex,
        ResolvedRefIndex = source == CubicacionInputSource.CantHeader
            ? CubicacionFormulaPicker.GetCurrentCantIndex(box)
            : CubicacionFormulaPicker.GetCurrentUnidadIndex(box),
        RowCount = Vm.Rows.Count,
        HasSectorColumns = _sectorColumns.Count > 0
    };

    private CubicacionCellInput ReadGridKey(KeyEventArgs e)
    {
        var column = ResourceGrid.CurrentCell.Column;
        int rowIndex = ResourceGrid.CurrentCell.IsValid && ResourceGrid.CurrentCell.Item != null
            ? ResourceGrid.Items.IndexOf(ResourceGrid.CurrentCell.Item)
            : ResourceGrid.SelectedIndex;
        bool outside = e.OriginalSource is DependencyObject dep
            && dep != ResourceGrid
            && FindParent<DataGridCell>(dep) == null
            && FindParent<DataGridRow>(dep) == null;
        var editor = Keyboard.FocusedElement as TextBox;
        return new CubicacionCellInput
        {
            Source = CubicacionInputSource.Grid,
            Key = CubicacionCellKeyMap.From(e),
            Modifiers = CubicacionCellKeyMap.ModifiersOf(e),
            GridEditing = IsGridEditing(),
            ShortcutMatches = MatchesConfiguredShortcut(e, CubicacionShortcutCatalog.PartidaPrevious, CubicacionShortcutCatalog.PartidaNext, CubicacionShortcutCatalog.QuickJump),
            EventFromOutsideCell = outside,
            RowIndex = rowIndex,
            QtyColumn = (CantidadColumn != null && column == CantidadColumn) || (column != null && _sectorColumns.Contains(column)),
            HasDeletableCells = ResourceGrid.SelectedCells.Any(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly),
            SelectedItemCount = ResourceGrid.SelectedItems.Count,
            SelectedCellCount = ResourceGrid.SelectedCells.Count,
            SectorMatrixReady = Vm.IsSectorMatrixMode && _sectorColumns.Count > 0,
            SectorColumnActive = column != null && _sectorColumns.Contains(column),
            SectorPicking = _sectorFormulaController.IsPickingInProgress,
            CanStartSectorPick = editor != null && CubicacionSectorFormulaController.CanStartSectorPick(editor),
            FocusedEditorIsTextBox = editor != null,
            SectorSlot = column == null ? -1 : _sectorColumns.IndexOf(column),
            OnCodigoColumn = ReferenceEquals(column, CodigoColumn),
            AutocompleteOpen = Vm.AutocompleteVisible
        };
    }

    private CubicacionCellInput ReadShellKey(KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;
        return new CubicacionCellInput
        {
            Source = CubicacionInputSource.Shell,
            Key = CubicacionCellKeyMap.From(e),
            Modifiers = CubicacionCellKeyMap.ModifiersOf(e),
            PasteAllowed = focused is not TextBoxBase || ReferenceEquals(focused, ResourceGrid),
            CanAddPartida = Vm.AddPartidaCommand.CanExecute(null),
            CanDeletePartida = Vm.DeletePartidaCommand.CanExecute(null)
        };
    }

    private CubicacionCellInput ReadClick(MouseButtonEventArgs e)
    {
        var cell = e.OriginalSource is DependencyObject dep ? FindParent<DataGridCell>(dep) : null;
        var row = cell?.DataContext as CubicacionRowVm;
        int rowIndex = row == null ? -1 : ResourceGrid.Items.IndexOf(row);
        int slot = cell == null ? -1 : _sectorColumns.IndexOf(cell.Column);
        var focused = Keyboard.FocusedElement as TextBox;
        var editingCell = focused == null ? null : FindParent<DataGridCell>(focused);
        bool unidadHeader = CubicacionFormulaPicker.IsUnidadActive ||
            (focused != null && (focused == UnidadHeaderBox || focused.Tag as string == "PartidaUnidad"));
        bool cantHeader = (focused != null && (focused == CantHeaderBox || focused.Tag as string == "PartidaCant"))
            || _cantEditingStarted || CubicacionFormulaPicker.IsCantActive;
        return new CubicacionCellInput
        {
            Source = CubicacionInputSource.ResourceClick,
            Modifiers = e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Alt ? CubicacionModifiers.Alt : CubicacionModifiers.None,
            ReadOnly = Vm.IsReadOnly,
            RowIndex = rowIndex,
            ClickCount = e.ClickCount,
            SectorSlot = slot,
            AltClickSector = Vm.IsSectorMatrixMode && Keyboard.Modifiers == ModifierKeys.Alt && slot >= 0 && row != null,
            UnidadActive = unidadHeader,
            CantActive = cantHeader,
            ClickOnCantColumn = cell?.Column == CantidadColumn,
            EditingUnidadCell = editingCell?.Column == UnidadColumn,
            CanPickUnidadFromRow = row != null && !string.IsNullOrWhiteSpace(row.Unidad),
            SectorEditorClickOther = editingCell != null && _sectorColumns.Contains(editingCell.Column) && cell != editingCell,
            SectorFormulaArmed = focused != null && (focused.Text.StartsWith("=") || focused.Text.StartsWith("+") || CubicacionSectorFormulaController.EndsWithFormulaOperator(focused)),
            CanPickSector = rowIndex >= 0 && slot >= 0,
            CellEditable = cell?.Column != null && !cell.Column.IsReadOnly,
            CellIsEditing = cell?.IsEditing == true
        };
    }

    private void CommitActiveField(TextBox? box)
    {
        if (box == CantHeaderBox || box?.Tag as string == "PartidaCant")
            CommitCantHeaderBox(box, force: true);
        else if (box == UnidadHeaderBox || box?.Tag as string == "PartidaUnidad")
            CubicacionFormulaPicker.CommitUnidadPick(box, Vm, ResourceGrid, force: true);
        else if (box != null)
            BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
    }

    private void CancelActiveField(TextBox? box)
    {
        if (box == CantHeaderBox || box?.Tag as string == "PartidaCant")
            CancelCantPick();
        else if (box == UnidadHeaderBox || box?.Tag as string == "PartidaUnidad")
            CubicacionFormulaPicker.CancelUnidadPick(box, Vm.Rows, Vm, ResourceGrid);
        if (box != null)
        {
            SetFieldEditingState(box, false);
            box.SelectAll();
        }
    }

    private void FocusDecisionTarget(CubicacionCellDecision decision)
    {
        switch (decision.Target)
        {
            case CubicacionMoveTarget.CantHeader:
                ActiveCantBox.Focus();
                ActiveCantBox.SelectAll();
                break;
            case CubicacionMoveTarget.UnidadHeader:
                ActiveUnidadBox.Focus();
                ActiveUnidadBox.SelectAll();
                break;
            case CubicacionMoveTarget.PartidaDescripcion:
                PartidaDescripcionBox?.Focus();
                PartidaDescripcionBox?.SelectAll();
                break;
            case CubicacionMoveTarget.Grid:
                ResourceGrid?.Focus();
                break;
            case CubicacionMoveTarget.GridCodigo:
                CubicacionFormulaPicker.FocusGridCell(ResourceGrid, decision.RowIndex, CodigoColumn ?? CantidadColumn);
                break;
            case CubicacionMoveTarget.GridCant:
                CubicacionFormulaPicker.FocusGridCell(ResourceGrid, decision.RowIndex, CantidadColumn ?? CodigoColumn);
                break;
            case CubicacionMoveTarget.GridUnidad:
                CubicacionFormulaPicker.FocusGridCell(ResourceGrid, decision.RowIndex, UnidadColumn ?? CodigoColumn);
                break;
            case CubicacionMoveTarget.GridFirstSector:
                CubicacionFormulaPicker.FocusFirstCubicacionCell(ResourceGrid, _sectorColumns, CantidadColumn);
                break;
        }
    }

    private void CommitGridEdit()
    {
        ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void CutGridSelection()
    {
        DataGridExcelNavigation.CopySelectionToClipboard(ResourceGrid);
        var cells = ResourceGrid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        var currentColumn = ResourceGrid.CurrentCell.Column;
        if (currentColumn != null && (_sectorColumns.Contains(currentColumn) || currentColumn == CantidadColumn))
        {
            cells = cells.Where(c => _sectorColumns.Contains(c.Column) || c.Column == CantidadColumn).ToList();
        }
        if (cells.Count == 0)
            return;
        var rows = cells.Select(c => c.Item as CubicacionRowVm).Where(r => r != null).Distinct().ToList();
        Vm.RunBatchedEdit("Cortar celdas", () =>
        {
            foreach (var group in cells.GroupBy(c => c.Item))
            {
                var rowItem = group.Key;
                DataGridCellAccessor.BeginBatchUpdate(rowItem);
                try
                {
                    foreach (var c in group)
                        DataGridCellAccessor.SetCellText(c.Item, c.Column, string.Empty);
                }
                finally
                {
                    DataGridCellAccessor.EndBatchUpdate(rowItem);
                }
            }
            foreach (var row in rows)
            {
                if (row != null)
                    Vm.OnRowEdited(row);
            }
        });
        CommandManager.InvalidateRequerySuggested();
    }

    private void ClearDeletableCells()
    {
        var cells = ResourceGrid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        var currentColumn = ResourceGrid.CurrentCell.Column;
        if (currentColumn != null && (_sectorColumns.Contains(currentColumn) || currentColumn == CantidadColumn))
        {
            cells = cells.Where(c => _sectorColumns.Contains(c.Column) || c.Column == CantidadColumn).ToList();
        }
        if (cells.Count == 0)
            return;
        var rows = cells.Select(c => c.Item as CubicacionRowVm).Where(r => r != null).Distinct().ToList();
        Vm.RunBatchedEdit("Borrar celdas", () =>
        {
            foreach (var group in cells.GroupBy(c => c.Item))
            {
                var rowItem = group.Key;
                DataGridCellAccessor.BeginBatchUpdate(rowItem);
                try
                {
                    foreach (var c in group)
                        DataGridCellAccessor.SetCellText(c.Item, c.Column, string.Empty);
                }
                finally
                {
                    DataGridCellAccessor.EndBatchUpdate(rowItem);
                }
            }
            foreach (var row in rows)
            {
                if (row != null)
                    Vm.OnRowEdited(row);
            }
        });
        CommandManager.InvalidateRequerySuggested();
    }

    private void MoveUnidadSuggestion(int delta)
    {
        int count = UnidadSuggestionsList.Items.Count;
        if (count == 0)
            return;
        int next = Math.Clamp(UnidadSuggestionsList.SelectedIndex + delta, 0, count - 1);
        UnidadSuggestionsList.SelectedIndex = next;
    }

    private void ApplyAutocompleteSelection()
    {
        var target = AutocompleteList.SelectedItem as AutocompleteSuggestionVm ?? Vm.AutocompleteFirstItem;
        if (target == null)
            return;
        Vm.ApplyMaterialCommand.Execute(target.Material);
        ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        MoveCodigoEditorDown();
    }

    private void ApplyAutocompleteTab()
    {
        if (AutocompleteList.SelectedItem is AutocompleteSuggestionVm first)
            Vm.ApplyMaterialCommand.Execute(first.Material);
        else if (Vm.AutocompleteFirstItemIsExactMatch && Vm.AutocompleteFirstItem is { } fallback)
            Vm.ApplyMaterialCommand.Execute(fallback.Material);
    }

    private void CommitTypedCodigo(TextBox? box)
    {
        if (box == null || ResourceGrid.CurrentItem is not CubicacionRowVm row)
            return;
        row.Codigo = box.Text;
        Vm.AutocompleteVisible = false;
        CommitGridEdit();
        MoveCodigoEditorDown();
    }

    private void MoveCodigoEditorDown()
    {
        int nextIdx = ResourceGrid.SelectedIndex + 1;
        var col = CodigoColumn ?? ResourceGrid.CurrentCell.Column;
        if (nextIdx > 0 && nextIdx < ResourceGrid.Items.Count && col != null)
            ResourceGrid.Dispatcher.BeginInvoke(new Action(() => DataGridExcelNavigation.FocusCell(ResourceGrid, nextIdx, col)), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void PickUnidadFromClick(CubicacionCellDecision decision, bool commit)
    {
        var focused = Keyboard.FocusedElement as TextBox;
        var pickBox = CubicacionFormulaPicker.ActiveUnidadBox ?? focused ?? UnidadHeaderBox;
        CubicacionFormulaPicker.SetActiveUnidadBox(pickBox, Vm);
        CubicacionFormulaPicker.ApplyUnidadPick(pickBox, decision.RowIndex, Vm.Rows, ResourceGrid, UnidadColumn);
        if (commit)
        {
            CubicacionFormulaPicker.CommitUnidadPick(pickBox, Vm, ResourceGrid, force: true);
            ActiveCantBox.Focus();
            ActiveCantBox.SelectAll();
            return;
        }
        pickBox.Focus();
    }

    private void PickCantFromClick(int rowIndex)
    {
        var focused = Keyboard.FocusedElement as TextBox;
        var pickBox = CubicacionFormulaPicker.ActiveCantBox ?? focused ?? ActiveCantBox;
        CubicacionFormulaPicker.SetActiveCantBox(pickBox, Vm);
        CubicacionFormulaPicker.PickCantCell(pickBox, rowIndex, Vm.Rows, ResourceGrid, CantidadColumn);
        _cantEditingStarted = true;
        pickBox.Focus();
    }

    private void PickSectorFromClick(CubicacionCellDecision decision)
    {
        if (Keyboard.FocusedElement is not TextBox focused)
            return;
        if (CubicacionFormulaPicker.TryPickSector(focused, decision.RowIndex, decision.Delta))
            _sectorFormulaController.RefreshSectorReferenceHighlights(focused, Vm);
    }

    private void BeginEditClickedCell(MouseButtonEventArgs? click)
    {
        if (click?.OriginalSource is not DependencyObject dep)
            return;
        var cell = FindParent<DataGridCell>(dep);
        if (cell == null)
            return;
        if (!cell.IsFocused)
            cell.Focus();
        ResourceGrid.CurrentCell = new DataGridCellInfo(cell);
        ResourceGrid.BeginEdit();
    }

    private void JumpSectorEdge(int direction)
    {
        var activeRow = ResourceGrid.CurrentCell.Item;
        int currentSlot = _sectorColumns.IndexOf(ResourceGrid.CurrentCell.Column);
        var currentRow = activeRow as CubicacionRowVm;
        int targetAbsolute = FindSectorDataEdge(currentRow?.Source.QtyExpressions, currentSlot, direction, CubicacionViewModel.MaximumSectorCount);
        CommitGridEdit();
        FocusSectorColumn(activeRow, targetAbsolute);
    }

    private void ApplyClickDecision(MouseButtonEventArgs e)
    {
        var decision = CubicacionCellSession.Decide(ReadClick(e));
        if (decision.Action == CubicacionCellAction.CommitAndMove && decision.Target == CubicacionMoveTarget.CantHeader)
        {
            PickUnidadFromClick(decision, commit: true);
            e.Handled = true;
            return;
        }

        ApplyCellDecision(decision, null, click: e);
        if (decision.Handled)
            e.Handled = true;
    }
}
