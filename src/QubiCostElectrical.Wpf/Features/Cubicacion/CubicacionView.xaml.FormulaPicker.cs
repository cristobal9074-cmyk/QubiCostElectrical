using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView : UserControl
{
    private void SetFieldEditingState(TextBox tb, bool isEditing)
    {
        _fieldEditingState[tb] = isEditing;
        if (tb == CantHeaderBox || tb.Tag as string == "PartidaCant") _cantEditingStarted = isEditing;
    }

    private bool IsFieldEditing(TextBox tb) => _fieldEditingState.TryGetValue(tb, out var isEditing) && isEditing;

    // ── Cantidad oficial con fórmula tipo Excel (CantHeaderBox, modo Detalle) ──────────────
    // Esta celda es la Cantidad oficial de la partida y admite sumar/restar/multiplicar/dividir
    // los recursos de abajo (solo la columna CANT es "pickeable"), con el mismo modelo que una
    // planilla: clic/Tab SELECCIONA la celda (todo el texto resaltado, ↓/↑ todavía no hace
    // nada); recién al escribir el primer carácter empieza la EDICIÓN, y ahí sí:
    //   · ↓/↑ inserta o mueve una referencia (R1, R2...) en el punto donde está el cursor, y
    //     resalta esa fila en la grilla — sin robarle nunca el foco al cuadro de fórmula.
    //   · Escribir cualquier otra tecla (+, -, *, /, un número, borrar...) "confirma" esa
    //     referencia en el texto; el siguiente ↓/↑ empieza una referencia nueva. Así se arma
    //     "R1+R2-R3" tecleando sin soltar el cuadro de fórmula.
    //   · Clic directo en una celda CANT de un recurso hace lo mismo que ↓/↑ + tecla, en un
    //     solo paso (ver OnResourceGridPreviewMouseLeftButtonDown).
    // Enter evalúa, confirma y CIERRA la edición (↓/↑ ya no vuelve a agregar nada hasta que se
    // escriba de nuevo). Escape descarta los cambios. Supr/Backspace con todo seleccionado (recién
    // entrado a la celda) borra todo de un tiro, en vez de tener que borrar dígito por dígito.
    // Ctrl+←/→ recorre las 3 casillas editables de la fila de partida (Unidad, Cantidad,
    // Observaciones) — las únicas editables de esa fila; todo lo demás es solo texto. Al llegar
    // a una casilla queda todo el texto seleccionado, igual que al hacer clic en ella.

    // Unidad y Observaciones de la partida: al entrar queda todo seleccionado (como Excel) y
    // se navega con flechas directas (o Ctrl+←/→). No arman fórmula (eso es exclusivo de
    // Cantidad, ver OnCantHeaderBoxPreviewKeyDown).
    private void ClearGridSelection()
    {
        ResourceGrid.SelectedCells.Clear();
        ResourceGrid.SelectedItem = null;
        ResourceGrid.CurrentCell = default;
    }

    private void OnPartidaFieldGotFocus(object sender, RoutedEventArgs e) { ClearGridSelection(); if (sender is TextBox tb) { SetFieldEditingState(tb, false); SelectAllDeferred(tb); } }
    private void OnPartidaFieldPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
            DispatchCell(ReadField(CubicacionInputSource.ObservacionHeader, e, tb), e, tb);
    }
    private static void SelectAllDeferred(TextBox tb) => tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);

    private TextBox? _templateUnidadBox;
    private TextBox? _templateCantBox;
    private void OnUnidadHeaderBoxLoaded(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.Tag as string == "PartidaUnidad") _templateUnidadBox = tb; }
    private void OnCantHeaderBoxLoaded(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.Tag as string == "PartidaCant") _templateCantBox = tb; }
    private TextBox ActiveUnidadBox => (Keyboard.FocusedElement as TextBox) is { } tb && (tb == UnidadHeaderBox || tb.Tag as string == "PartidaUnidad") ? tb : (Vm.IsSectorMatrixMode ? (_templateUnidadBox ?? UnidadHeaderBox) : UnidadHeaderBox);
    private TextBox ActiveCantBox => (Keyboard.FocusedElement as TextBox) is { } tb && (tb == CantHeaderBox || tb.Tag as string == "PartidaCant") ? tb : (Vm.IsSectorMatrixMode ? (_templateCantBox ?? CantHeaderBox) : CantHeaderBox);

    private void OnHeaderTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is TextBox tb && !tb.IsKeyboardFocused) { ClearGridSelection(); tb.Focus(); SelectAllDeferred(tb); e.Handled = true; } }
    private void OnUnidadHeaderBoxGotFocus(object sender, RoutedEventArgs e) { var tb = sender as TextBox ?? ActiveUnidadBox; ClearGridSelection(); CubicacionFormulaPicker.SetActiveUnidadBox(tb, Vm); SetFieldEditingState(tb, false); SelectAllDeferred(tb); }
    private void OnUnidadHeaderBoxLostFocus(object sender, RoutedEventArgs e)
    {
        UnidadSuggestionsPopup.IsOpen = false;
        CubicacionFormulaPicker.CommitUnidadPick(sender as TextBox ?? ActiveUnidadBox, Vm);
    }

    private void OnUnidadHeaderBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsKeyboardFocused)
            CubicacionFormulaPicker.ShowUnidadSuggestions(tb, UnidadSuggestionsPopup, UnidadSuggestionsList);
    }

    private void OnUnidadSuggestionClick(object sender, MouseButtonEventArgs e)
    {
        if (UnidadSuggestionsList.SelectedItem is CubicacionFormulaPicker.UnidadSuggestion sug) ApplyUnidadSuggestion(sug);
    }

    private void ApplyUnidadSuggestion(CubicacionFormulaPicker.UnidadSuggestion sug)
    {
        var tb = ActiveUnidadBox;
        if (tb != null) { tb.Text = sug.Code; if (Vm != null) Vm.PartidaUnidadText = sug.Code; }
        UnidadSuggestionsPopup.IsOpen = false;
        ActiveCantBox.Focus();
        ActiveCantBox.SelectAll();
    }

    private void OnUnidadHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox ?? ActiveUnidadBox;
        DispatchCell(ReadField(CubicacionInputSource.UnidadHeader, e, tb), e, tb);
    }

    private void OnCantHeaderBoxGotFocus(object sender, RoutedEventArgs e) { var tb = sender as TextBox ?? ActiveCantBox; ClearGridSelection(); CubicacionFormulaPicker.SetActiveCantBox(tb, Vm); SetFieldEditingState(tb, false); SelectAllDeferred(tb); }
    private void OnCantHeaderBoxLostFocus(object sender, RoutedEventArgs e) => CommitCantHeaderBox(sender as TextBox ?? ActiveCantBox);
    private void OnCantHeaderBoxPreviewTextInput(object sender, TextCompositionEventArgs e) => CubicacionFormulaPicker.NotifyCantTextInput(e.Text);

    private void OnCantHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox ?? ActiveCantBox;
        CubicacionFormulaPicker.NotifyCantKeyDown(e.Key);
        DispatchCell(ReadField(CubicacionInputSource.CantHeader, e, tb), e, tb);
    }

    private bool _isCommittingCant;
    private bool _isCancellingCant;
    private void CommitCantHeaderBox(TextBox? target = null, bool force = false)
    {
        if (_isCommittingCant || _isCancellingCant || (!force && CubicacionFormulaPicker.SuppressLostFocusCommit)) return;
        _isCommittingCant = true;
        try
        {
            var box = target ?? ActiveCantBox;
            FinalizeCantPick();
            _cantEditingStarted = false;
            if (box != null && Vm != null && !CubicacionOpenCellRules.TryAcceptQuantityText(box.Text, Vm.BuildPartidaCantidadVariables(), out string error))
            {
                Vm.ReportFormulaIssue(error);
                var keep = box;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    keep.Focus();
                    Keyboard.Focus(keep);
                }));
                return;
            }
            if (box != null && Vm != null) { Vm.PartidaCantidadText = box.Text ?? ""; box.Text = Vm.PartidaCantidadText; }
            if (box != null) BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateTarget();
            if (box != CantHeaderBox && CantHeaderBox != null) { CantHeaderBox.Text = Vm?.PartidaCantidadText ?? ""; BindingOperations.GetBindingExpression(CantHeaderBox, TextBox.TextProperty)?.UpdateTarget(); }
            if (_templateCantBox != null && _templateCantBox != box) { _templateCantBox.Text = Vm?.PartidaCantidadText ?? ""; BindingOperations.GetBindingExpression(_templateCantBox, TextBox.TextProperty)?.UpdateTarget(); }
        }
        finally { _isCommittingCant = false; }
    }

    private void ApplyCantPick(int newIndex) => CubicacionFormulaPicker.ApplyCantPick(ActiveCantBox, newIndex, Vm.Rows, ResourceGrid, CantidadColumn);
    private void FinalizeCantPick() => CubicacionFormulaPicker.FinalizeCantPick(Vm.Rows, ResourceGrid);
    private void CancelCantPick()
    {
        _isCancellingCant = true;
        try { CubicacionFormulaPicker.CancelCantPick(ActiveCantBox, Vm.Rows, Vm, ResourceGrid); }
        finally { Dispatcher.BeginInvoke(new Action(() => _isCancellingCant = false), System.Windows.Threading.DispatcherPriority.Background); }
    }
    private void ClearCantPickHighlight() => CubicacionFormulaPicker.ClearCantPickHighlight(Vm.Rows, ResourceGrid);

    // Clic directo en una celda CANT de un recurso mientras CantHeaderBox tiene el foco →
    // inserta/reemplaza su referencia y la confirma en un solo paso (equivale a ↓/↑ hasta esa
    // fila y luego escribir un operador). Cualquier otro clic sigue el comportamiento normal.
    private void OnResourceGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ApplyClickDecision(e);


    internal static int FindSectorDataEdge(string[]? expressions, int current, int direction, int max)
        => CubicacionSectorNavigation.FindDataEdge(expressions, current, direction, max);

    private void ClearAllSectorReferenceHighlights() => _sectorFormulaController.ClearAllSectorReferenceHighlights(Vm);

    private void FinalizeSectorPick() => _sectorFormulaController.FinalizeSectorPick();
}
