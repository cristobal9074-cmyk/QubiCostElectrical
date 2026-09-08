using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Fachada compatible con las pantallas existentes. Internamente todo se calcula
/// mediante BudgetEngine para mantener una sola fuente de verdad.
/// </summary>
public sealed class BudgetCalculationService : IBudgetCalculationService
{
    private readonly BudgetEngine engine;
    private CostBreakdown? lastBudget;

    public BudgetCalculationService() : this(null) { }

    public BudgetCalculationService(Func<BudgetEngine>? engineFactory)
        => engine = (engineFactory ?? (static () => new BudgetEngine()))();

    public void MarkDirtyItem(string? itemId) => engine.MarkDirtyItem(itemId);
    public void MarkDirtyItems(IEnumerable<string>? itemIds) => engine.MarkDirtyItems(itemIds);
    public void ResetIncrementalCache()
    {
        lastBudget = null;
        engine.ClearDirtyItems();
    }

    public CostBreakdown CalculateBudget(
        IEnumerable<CubicacionRow> cubicacion,
        BudgetSettings settings,
        IEnumerable<ApuRow>? apus = null,
        IEnumerable<ItemizadoRow>? itemizado = null,
        IEnumerable<ChangeOrderRow>? changeOrders = null)
    {
        var result = engine.CalculateIncremental(cubicacion, settings, apus, itemizado, changeOrders, lastBudget);
        lastBudget = result;
        return result;
    }

    public (decimal Material, decimal ManoObra, decimal Total) GetProjectTotals(IEnumerable<CubicacionRow> cubicacion)
    {
        var direct = engine.CalculateDirectCosts(cubicacion ?? Enumerable.Empty<CubicacionRow>());
        return (direct.MaterialesUf, direct.ManoObraUf, direct.TotalUf);
    }

    public (decimal Material, decimal ManoObra, decimal Total) GetDirectCostTotals(IEnumerable<CubicacionRow> rows)
    {
        var direct = engine.CalculateDirectCosts(rows ?? Enumerable.Empty<CubicacionRow>());
        return (direct.MaterialesUf, direct.ManoObraUf, direct.TotalUf);
    }

    public List<(string Area, decimal Material, decimal ManoObra, decimal Total)> GetAreaCostBreakdown(IEnumerable<CubicacionRow> cubicacion)
    {
        return engine.CalculateByArea(cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Select(x => (x.Area, x.MaterialesUf, x.ManoObraUf, x.TotalUf))
            .ToList();
    }

    public (decimal Directo, decimal GastosGenerales, decimal Subtotal, decimal Utilidad, decimal Venta, decimal UfM2) GetCostBreakdown(
        IEnumerable<CubicacionRow> cubicacion,
        decimal gastosGeneralesPct,
        decimal utilidadPct,
        decimal metrosCuadrados)
    {
        var cost = engine.Calculate(
            cubicacion ?? Enumerable.Empty<CubicacionRow>(),
            BudgetSettings.FromValues(gastosGeneralesPct, utilidadPct, metrosCuadrados));

        return (cost.CostoDirectoUf, cost.GastosGeneralesUf, cost.SubtotalUf, cost.UtilidadUf, cost.TotalVentaUf, cost.UfM2);
    }

    public List<(string Area, decimal Total)> GetAreaTotals(IEnumerable<CubicacionRow> cubicacion)
    {
        return engine.CalculateByArea(cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Select(x => (x.Area, x.TotalUf))
            .ToList();
    }

    public List<(string Codigo, string Descripcion, string Unidad, decimal Cantidad, decimal Total)> GetMaterialTotals(IEnumerable<CubicacionRow> cubicacion)
    {
        return engine.CalculateMaterialTotals(cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Select(x => (x.Codigo, x.Descripcion, x.Unidad, x.Cantidad, x.TotalUf))
            .ToList();
    }
}
