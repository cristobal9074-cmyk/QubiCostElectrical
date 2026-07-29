using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class CubicacionViewModelService : ICubicacionViewModelService
{
    private readonly IBudgetCalculationService budgetCalculationService;

    public CubicacionViewModelService(IBudgetCalculationService budgetCalculationService)
    {
        this.budgetCalculationService = budgetCalculationService;
    }

    public CubicacionViewModel Build(
        IEnumerable<CubicacionRow> cubicacion,
        string itemId = "",
        decimal gastosGeneralesPct = 0m,
        decimal utilidadPct = 0m,
        decimal metrosCuadrados = 0m)
    {
        IEnumerable<CubicacionRow> source = cubicacion ?? Enumerable.Empty<CubicacionRow>();

        if (!string.IsNullOrWhiteSpace(itemId))
            source = source.Where(x => string.Equals(x.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

        var rows = source
            .Select(ToLine)
            .ToList();

        var validRows = rows.Where(x => !string.IsNullOrWhiteSpace(x.Codigo)).ToList();
        var summary = BuildSummary(validRows, gastosGeneralesPct, utilidadPct, metrosCuadrados);

        return new CubicacionViewModel(
            itemId ?? "",
            rows,
            validRows.Count,
            validRows.Sum(x => x.Cantidad),
            summary);
    }

    public CubicacionLineViewModel ToLine(CubicacionRow row)
    {
        decimal unit = row.MatUf + row.MoUf;
        decimal total = row.Cantidad * unit;

        return new CubicacionLineViewModel(
            row.Id,
            row.ItemId,
            row.Tipo,
            row.Codigo,
            row.Descripcion,
            row.Unidad,
            row.Cantidad,
            row.MatUf,
            row.MoUf,
            unit,
            total,
            string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area.Trim());
    }

    public CostSummaryViewModel BuildSummary(
        IEnumerable<CubicacionLineViewModel> rows,
        decimal gastosGeneralesPct,
        decimal utilidadPct,
        decimal metrosCuadrados)
    {
        var cubicacionRows = (rows ?? Enumerable.Empty<CubicacionLineViewModel>())
            .Select(row => new CubicacionRow
            {
                Id = row.Id,
                ItemId = row.ItemId,
                Tipo = row.Tipo,
                Codigo = row.Codigo,
                Descripcion = row.Descripcion,
                Unidad = row.Unidad,
                Cantidad = row.Cantidad,
                MatUf = row.MaterialUf,
                MoUf = row.ManoObraUf,
                Area = row.Area
            })
            .ToList();

        var cost = budgetCalculationService.CalculateBudget(
            cubicacionRows,
            BudgetSettings.FromValues(gastosGeneralesPct, utilidadPct, metrosCuadrados));

        return new CostSummaryViewModel(
            cost.MaterialesUf,
            cost.ManoObraUf,
            cost.EquiposUf,
            cost.SubcontratosUf + cost.HerramientasUf + cost.MermasUf + cost.OrdenesCambioUf,
            cost.CostoDirectoUf,
            cost.GastosGeneralesUf,
            cost.SubtotalUf,
            cost.UtilidadUf,
            cost.TotalVentaUf,
            cost.UfM2);
    }
}
