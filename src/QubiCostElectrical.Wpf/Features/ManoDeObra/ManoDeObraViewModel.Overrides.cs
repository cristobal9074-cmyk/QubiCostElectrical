using System;
using System.Linq;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public sealed partial class ManoDeObraViewModel
{
    private bool staffingEditsPending;
    private bool dotPicoPending;
    private string? pendingDotCargo;
    private int pendingDotPico;

    public bool HasOverride(string cargoId, int monthIdx)
        => cellOverrides.ContainsKey($"{cargoId}_{monthIdx}");

    public void SetOverride(string cargoId, int monthIdx, int count)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        cellOverrides[$"{cargoId}_{monthIdx}"] = Math.Max(0, count);
        Recalcular();
        PersistToSession();
    }

    public void ClearOverride(string cargoId, int monthIdx)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        if (!cellOverrides.Remove($"{cargoId}_{monthIdx}"))
            return;
        Recalcular();
        PersistToSession();
    }

    /// <summary>Deja el número en memoria. La grilla se recalcula una sola vez al dejar de escribir.</summary>
    public void StageOverride(string cargoId, int monthIdx, int count)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        var key = $"{cargoId}_{monthIdx}";
        count = Math.Max(0, count);
        if (cellOverrides.TryGetValue(key, out var current) && current == count)
            return;
        cellOverrides[key] = count;
        staffingEditsPending = true;
    }

    public bool StageClearOverride(string cargoId, int monthIdx)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return false;
        if (!cellOverrides.Remove($"{cargoId}_{monthIdx}"))
            return false;
        staffingEditsPending = true;
        return true;
    }

    public void FlushStaffingEdits()
    {
        if (!staffingEditsPending)
            return;
        staffingEditsPending = false;
        ApplyStagedDotPico();
        Recalcular();
        PersistToSession();
    }

    // ─── IPageWithPendingChanges / IPageWithCommitChanges ────────────────────
    public bool HasPendingChanges => staffingEditsPending || dotPicoPending;

    bool IPageWithPendingChanges.HasPendingChanges => HasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() => TryDiscardChanges();
    bool IPageWithCommitChanges.TryApplyChanges() => TryApplyChanges();

    public bool TryDiscardChanges()
    {
        dotPicoPending = false;
        pendingDotCargo = null;
        staffingEditsPending = false;
        cellOverrides.Clear();
        Load();
        return true;
    }

    public bool TryApplyChanges()
    {
        FlushStaffingEdits();
        return true;
    }

    /// <summary>Deja la dotación pico de un rol indirecto en memoria; se aplica al dejar de escribir.</summary>
    public void StageIndirectDotPico(string cargoId, int value)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId))
            return;
        pendingDotCargo = cargoId;
        pendingDotPico = Math.Max(0, value);
        dotPicoPending = true;
        staffingEditsPending = true;
    }

    /// <summary>Cancela la dotación staged para ese rol (Escape). Devuelve false si no había nada staged.</summary>
    public bool StageClearDotPico(string cargoId)
    {
        if (!dotPicoPending || !string.Equals(pendingDotCargo, cargoId, StringComparison.Ordinal))
            return false;
        dotPicoPending = false;
        pendingDotCargo = null;
        return true;
    }

    /// <summary>
    /// Fija la dotación de un rol indirecto en todos los meses: deja de usar la curva/overrides
    /// y limpia las ediciones mes a mes de ese cargo.
    /// </summary>
    public void SetIndirectDotPico(string cargoId, int value)
    {
        if (!TrySetIndirectDotPico(cargoId, value))
            return;
        Recalcular();
        PersistToSession();
    }

    /// <summary>
    /// Establece la fracción de obra (dedicación) para un cargo indirecto (ej: 0.3 o 1.0)
    /// y actualiza su editor, cálculos y persistencia.
    /// </summary>
    public void SetCargoFraccion(string cargoId, decimal fraccion)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId))
            return;
        var cargo = CargosDef.FirstOrDefault(c => c.Id == cargoId && !c.EsDirecta);
        if (cargo == null)
            return;
        fraccion = ClampFraccion(fraccion);
        if (cargo.FraccionObra == fraccion && cargo.DotPico == 1)
            return;
        cargo.FraccionObra = fraccion;
        cargo.DotPico = 1;
        var editor = IndirectaEditors.FirstOrDefault(e => e.Id == cargoId);
        if (editor != null)
        {
            editor.FraccionEditText = FraccionStr(fraccion);
            editor.CommitEdit();
        }
        else
        {
            Recalcular();
            PersistToSession();
        }
    }

    private void ApplyStagedDotPico()
    {
        if (!dotPicoPending)
            return;
        dotPicoPending = false;
        var cargoId = pendingDotCargo;
        pendingDotCargo = null;
        TrySetIndirectDotPico(cargoId, pendingDotPico);
    }

    private bool TrySetIndirectDotPico(string? cargoId, int value)
    {
        if (string.IsNullOrWhiteSpace(cargoId))
            return false;
        var cargo = CargosDef.FirstOrDefault(c => c.Id == cargoId && !c.EsDirecta);
        if (cargo == null)
            return false;
        value = Math.Max(0, value);
        if (cargo.DotPico == value)
            return false;
        cargo.DotPico = value;
        ClearMonthlyOverrides(cargoId);
        return true;
    }

    private void ClearMonthlyOverrides(string cargoId)
    {
        var prefix = cargoId + "_";
        foreach (var key in cellOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            var mes = key.Substring(prefix.Length);
            if (mes.Length > 0 && mes.All(char.IsDigit))
                cellOverrides.Remove(key);
        }
    }
}
