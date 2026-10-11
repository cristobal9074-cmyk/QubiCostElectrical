using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ItemizadoGridState(
    IReadOnlyList<ItemizadoRow> Rows,
    PartidaCompletionSummary Completion);

public sealed record ItemizadoImportPlan(
    IReadOnlyList<ItemizadoRow> Rows,
    int Partidas,
    int Titulos,
    bool RequiresReplaceConfirmation,
    int CurrentRowCount,
    string ActionLabel);

public sealed record ItemizadoCubicacionNavigation(
    string ItemId,
    string ItemCode,
    string Description);

public sealed class ItemizadoPresenter
{
    private readonly ItemizadoPresenterServices _services;
    private readonly BudgetProjectSession _session;

    public ItemizadoPresenter(ItemizadoPresenterServices services, BudgetProjectSession session)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public ItemizadoGridState BuildGridState()
    {
        var rows = _session.Itemizado;
        var completion = _services.PartidaCompletion.Build(rows, _session.Cubicacion);
        return new ItemizadoGridState(rows, completion);
    }

    public CostBreakdown BuildCostBreakdown()
        => _services.BudgetCalculation.CalculateBudget(
            _session.Cubicacion,
            BudgetSettings.FromProject(_session.ProjectInfo),
            _session.Apus,
            _session.Itemizado);

    public IReadOnlyList<IReadOnlyList<string>> ReadExcelTable(string filePath)
    {
        return _services.ItemizadoSpreadsheet.ReadFirstWorksheet(filePath);
    }

    public (ItemizadoImportPlan? Plan, string? Error) TryBuildImportPlan(string filePath, string actionLabel = "Importar itemizado")
    {
        var table = _services.ItemizadoSpreadsheet.ReadFirstWorksheet(filePath);
        return TryBuildImportPlanFromTable(table, actionLabel);
    }

    public (ItemizadoImportPlan? Plan, string? Error) TryBuildImportPlanFromTable(
        IReadOnlyList<IReadOnlyList<string>> table,
        string actionLabel = "Importar itemizado")
    {
        var mutable = table.Select(row => row.ToList()).ToList();
        var result = _services.ImportItemizado.Execute(mutable);
        if (!string.IsNullOrWhiteSpace(result.Error))
            return (null, result.Error);

        return (BuildImportPlan(result.Rows, actionLabel), null);
    }

    public (ItemizadoImportPlan? Plan, string? Error) TryBuildImportPlanWithMapping(
        IReadOnlyList<IReadOnlyList<string>> table,
        int headerRow,
        int cItem,
        int cDesc,
        int cUnidad,
        int cCant,
        int dataStartRow,
        string actionLabel = "Importar itemizado")
    {
        var mutable = table.Select(row => row.ToList()).ToList();
        var result = _services.ImportItemizado.Execute(
            mutable,
            headerRow,
            cItem,
            cDesc,
            cUnidad,
            cCant,
            dataStartRow);
        if (!string.IsNullOrWhiteSpace(result.Error))
            return (null, result.Error);

        return (BuildImportPlan(result.Rows, actionLabel), null);
    }

    public (PegarItemizadoResult Result, ItemizadoImportPlan? Plan) TryBuildPastePlan(string clipboardText)
    {
        var result = _services.PasteItemizado.Execute(clipboardText);
        if (result.Rows.Count == 0)
            return (result, null);

        return (result, BuildImportPlan(result.Rows, "Pegar itemizado"));
    }

    public void CommitReplace(IReadOnlyList<ItemizadoRow> rows)
    {
        _session.EnsureWritable("reemplazar el itemizado");
        _session.ClearAllApus();
        _services.Itemizado.ReplaceAll(
            _session.Itemizado,
            _session.Cubicacion,
            _session.QtyHeadersByItem,
            rows);
        _session.ClearCubicacionCompositionLayers();
        _session.MarkDirty(ProjectDirtyScope.Itemizado, ProjectDirtyScope.Cubicacion, ProjectDirtyScope.QtyHeaders, ProjectDirtyScope.Apu);
    }

    public string BuildReplaceConfirmationMessage(int currentRowCount, string actionLabel)
        => $"Esto reemplazará el itemizado actual ({currentRowCount:N0} filas), borrará la cubicación asociada y eliminará los APU vinculados.\n\n¿Continuar?";

    public string BuildDeleteConfirmationMessage(string description, int affectedRows)
        => affectedRows > 1
            ? $"¿Eliminar {affectedRows:N0} filas?\n\nSe eliminarán también cubicación y APU asociados.\n\n{description}"
            : $"¿Eliminar?\n\nSe eliminarán también cubicación y APU asociados.\n\n{description}";

    public string BuildClearConfirmationMessage(int rowCount)
        => $"¿Vaciar todo el itemizado ({rowCount:N0} filas)?\n\nEsto también eliminará la cubicación y los APU asociados.";

    public string BuildImportStatusMessage(int partidas, int titulos)
    {
        string detail = titulos > 0
            ? $"{partidas:N0} partidas · {titulos:N0} títulos"
            : $"{partidas:N0} partidas";
        return detail;
    }

    public ItemizadoMutationResult AddBelow(int selectedIndex, string desc, string unidad)
    {
        _session.EnsureWritable("agregar una fila al itemizado");
        var result = _services.Itemizado.AddBelow(_session.Itemizado, selectedIndex, desc, unidad);
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
        return result;
    }

    public ItemizadoMutationResult AddTitle(int selectedIndex = -1)
    {
        _session.EnsureWritable("agregar un título al itemizado");
        var result = _services.Itemizado.AddTitle(_session.Itemizado, selectedIndex);
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
        return result;
    }

    public ItemizadoMutationResult Move(int selectedIndex, int direction)
    {
        _session.EnsureWritable("mover filas del itemizado");
        var result = _services.Itemizado.Move(_session.Itemizado, selectedIndex, direction);
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
        return result;
    }

    public ItemizadoMutationResult DeleteAt(int selectedIndex)
    {
        _session.EnsureWritable("eliminar filas del itemizado");
        var result = _services.Itemizado.DeleteAt(
            _session.Itemizado,
            _session.Cubicacion,
            _session.QtyHeadersByItem,
            selectedIndex);
        _session.RemoveApusForItemIds(result.RemovedItemIds);
        _session.RemoveCubicacionForItemIds(result.RemovedItemIds);
        _session.MarkDirty(ProjectDirtyScope.Itemizado, ProjectDirtyScope.Cubicacion, ProjectDirtyScope.QtyHeaders, ProjectDirtyScope.Apu);
        return result;
    }

    public ItemizadoMutationResult DuplicateAt(int selectedIndex)
    {
        _session.EnsureWritable("duplicar filas del itemizado");
        var result = _services.Itemizado.DuplicateAt(_session.Itemizado, selectedIndex);
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
        return result;
    }

    public void ClearAll()
    {
        _session.EnsureWritable("vaciar el itemizado");
        _services.Itemizado.Clear(_session.Itemizado, _session.Cubicacion, _session.QtyHeadersByItem);
        _session.ClearAllApus();
        _session.ClearCubicacionCompositionLayers();
        _session.MarkDirty(ProjectDirtyScope.Itemizado, ProjectDirtyScope.Cubicacion, ProjectDirtyScope.QtyHeaders, ProjectDirtyScope.Apu);
    }

    public void ApplyAssistedPartida(int rowIndex, string code, string description, string unit)
    {
        _session.EnsureWritable("asistir una partida del itemizado");
        _services.Itemizado.ApplyAssistedPartidaDetails(_session.Itemizado, rowIndex, code, description, unit);
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
    }

    public bool HasChildPartidas(string? titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId))
            return false;
        return _session.Itemizado.Any(x =>
            !string.Equals(x.Id, titleId, StringComparison.OrdinalIgnoreCase) &&
            (!x.EsTitulo && (
                string.Equals(x.ParentId, titleId, StringComparison.OrdinalIgnoreCase) ||
                (x.Id ?? "").StartsWith(titleId + ".", StringComparison.Ordinal) ||
                string.Equals(ItemizadoHierarchyRules.ParentIdOf(x.Id, _session.Itemizado), titleId, StringComparison.OrdinalIgnoreCase)
            )));
    }

    public bool TryPromoteLeafTitleToPartida(ItemizadoRow? item)
    {
        if (item == null) return false;
        if (!item.EsTitulo) return true;
        if (HasChildPartidas(item.Id)) return false;
        item.EsTitulo = false;
        if (item.Cantidad <= 0m) item.Cantidad = 1m;
        if (string.IsNullOrWhiteSpace(item.Unidad)) item.Unidad = Unidades.GL;
        _session.MarkDirty(ProjectDirtyScope.Itemizado);
        return true;
    }

    public ItemizadoCubicacionNavigation? TryBuildCubicacionNavigation(string itemId)
    {
        var item = _session.Itemizado.FirstOrDefault(x =>
            string.Equals(x.Id, itemId ?? "", StringComparison.OrdinalIgnoreCase));
        if (item == null || item.ExcluidoCubicacion)
            return null;

        if (item.EsTitulo && !TryPromoteLeafTitleToPartida(item))
            return null;

        return new ItemizadoCubicacionNavigation(item.Id, item.Item ?? "", item.Descripcion ?? "");
    }

    public string? ValidateCubicacionNavigation(string itemId)
    {
        var item = _session.Itemizado.FirstOrDefault(x =>
            string.Equals(x.Id, itemId ?? "", StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return "Partida no encontrada.";

        if (item.EsTitulo && HasChildPartidas(item.Id))
            return "Selecciona una partida o un ítem sin subpartidas agrupadas para cubicar.";

        if (item.ExcluidoCubicacion)
        {
            var motivo = (item.MotivoNoCubicar ?? "").Trim();
            return string.IsNullOrEmpty(motivo)
                ? "Esta partida está marcada como «No cubicar» (fuera de alcance)."
                : $"Esta partida está marcada como «No cubicar»: {motivo}";
        }
        return null;
    }

    public int CountRows() => _session.Itemizado.Count;

    public ItemizadoRow? GetRowAt(int index)
        => index >= 0 && index < _session.Itemizado.Count ? _session.Itemizado[index] : null;

    public int CountAffectedRows(int selectedIndex)
    {
        var row = GetRowAt(selectedIndex);
        if (row == null)
            return 0;
        if (!row.EsTitulo)
            return 1;
        return _session.Itemizado.Count(x =>
            string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase) ||
            (x.Id ?? "").StartsWith(row.Id + ".", StringComparison.Ordinal));
    }

    private ItemizadoImportPlan BuildImportPlan(IReadOnlyList<ItemizadoRow> rows, string actionLabel)
    {
        int partidas = rows.Count(r => !r.EsTitulo);
        int titulos = rows.Count(r => r.EsTitulo);
        return new ItemizadoImportPlan(
            rows,
            partidas,
            titulos,
            _session.Itemizado.Count > 0,
            _session.Itemizado.Count,
            actionLabel);
    }
}
