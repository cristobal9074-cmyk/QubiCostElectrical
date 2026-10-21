using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView
{
    private bool _commitOpenCellBusy;

    /// <summary>
    /// Escribe el editor abierto al modelo. Si la fórmula no calcula, deja el foco ahí y devuelve false.
    /// </summary>
    public bool CommitOpenCell()
    {
        if (_commitOpenCellBusy)
            return true;
        if (Vm == null || Vm.IsReadOnly)
        {
            Vm?.ClearOpenEditorError();
            return true;
        }

        _commitOpenCellBusy = true;
        try
        {
            Vm.ClearOpenEditorError();
            Vm.ClearFormulaIssue();
            PushPendingText(PartidaCodigoBox, Vm.PartidaCodigo);
            PushPendingText(PartidaDescripcionBox, Vm.PartidaDescripcion);
            CommitOpenUnidad();
            if (!TryCommitOpenCant())
                return false;
            PushPendingText(ObservacionHeaderBox, Vm.PartidaObservacionesText);
            return TryCommitGridEditor();
        }
        finally
        {
            _commitOpenCellBusy = false;
        }
    }

    private void CommitOpenUnidad()
    {
        var box = PendingBox(UnidadHeaderBox, _templateUnidadBox, Vm.PartidaUnidadText);
        if (box == null)
            return;
        CubicacionFormulaPicker.CommitUnidadPick(box, Vm, ResourceGrid, force: true);
    }

    private bool TryCommitOpenCant()
    {
        var box = PendingBox(CantHeaderBox, _templateCantBox, Vm.PartidaCantidadText);
        if (box == null)
            return true;
        if (!CubicacionOpenCellRules.TryAcceptQuantityText(box.Text, Vm.BuildPartidaCantidadVariables(), out string error))
        {
            RejectEditor(box, error);
            return false;
        }

        CommitCantHeaderBox(box, force: true);
        return true;
    }

    private bool TryCommitGridEditor()
    {
        var box = DataGridExcelNavigation.FindEditingTextBox(ResourceGrid);
        if (box == null)
            return true;

        var column = ResourceGrid.CurrentCell.Column;
        if (ResourceGrid.CurrentCell.Item is not CubicacionRowVm row || column == null)
        {
            CommitGridEdit();
            return true;
        }

        int slot = _sectorColumns.IndexOf(column);
        bool isSector = slot >= 0;
        if ((isSector || ReferenceEquals(column, CantidadColumn))
            && !TryAcceptGridFormula(row, slot, box.Text, isSector))
        {
            KeepEditor(box);
            return false;
        }

        CommitGridEdit();
        return true;
    }

    private bool TryAcceptGridFormula(CubicacionRowVm row, int slot, string? text, bool isSector)
    {
        if (Vm == null || !QuantityFormulaService.IsFormulaLike(text))
            return true;

        string error;
        if (isSector)
        {
            var result = Vm.EvaluateOpenSectorExpression(row, slot, text);
            if (result.Success)
                return true;
            error = string.IsNullOrWhiteSpace(result.Error) ? "La fórmula no se puede calcular." : result.Error;
        }
        else if (CubicacionOpenCellRules.TryAcceptQuantityText(text, Vm.BuildPartidaCantidadVariables(), out error))
        {
            return true;
        }

        Vm.ReportFormulaIssue(error);
        return false;
    }

    private TextBox? PendingBox(TextBox? primary, TextBox? alternate, string? current)
    {
        foreach (var box in new[] { primary, alternate })
        {
            if (box == null)
                continue;
            if (IsEditorPending(box, current))
                return box;
        }

        return null;
    }

    private static bool IsEditorPending(TextBox box, string? current)
    {
        if (!box.IsVisible)
            return false;

        bool focused = box.IsKeyboardFocusWithin || ReferenceEquals(Keyboard.FocusedElement, box);
        if (focused)
            return true;

        string text = box.Text ?? "";
        string model = current ?? "";
        if (string.Equals(text, model, StringComparison.Ordinal))
            return false;

        // Casillas y Detalle tienen cada uno su caja. La que no se editó puede seguir en blanco
        // y, al autoguardar, borraba la unidad y la cantidad que el usuario ya había puesto.
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(model))
            return false;

        return true;
    }

    private static void PushPendingText(TextBox? box, string? current)
    {
        if (box == null || !IsEditorPending(box, current))
            return;
        BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
    }

    private void RejectEditor(TextBox box, string error)
    {
        Vm.ReportFormulaIssue(error);
        KeepEditor(box);
    }

    private static void KeepEditor(TextBox box)
    {
        box.Focus();
        Keyboard.Focus(box);
        box.SelectAll();
    }
}
