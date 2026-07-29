using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class BudgetKpiService : IBudgetKpiService
{
    private readonly IBudgetCalculationService budgetCalculationService;

    public BudgetKpiService(IBudgetCalculationService budgetCalculationService)
    {
        this.budgetCalculationService = budgetCalculationService;
    }

    public IReadOnlyList<DashboardKpiViewModel> BuildDashboardKpis(
        decimal totalVentaUf,
        decimal materialesUf,
        decimal manoObraUf,
        decimal ufM2,
        bool metrosCuadradosConfigurados)
    {
        return new[]
        {
            new DashboardKpiViewModel("TOTAL VENTA", "Venta", totalVentaUf, FormatKpiUf(totalVentaUf), "vs versión anterior"),
            new DashboardKpiViewModel("MATERIALES", "Material", materialesUf, FormatKpiUf(materialesUf), "costo directo"),
            new DashboardKpiViewModel("MANO DE OBRA", "ManoObra", manoObraUf, FormatKpiUf(manoObraUf), "costo directo"),
            new DashboardKpiViewModel("UF / M²", "UfM2", ufM2, ufM2 > 0m ? ufM2.ToString("N2") : "0,00", metrosCuadradosConfigurados ? "sobre objetivo" : "configurar m²")
        };
    }

    public string FormatKpiUf(decimal uf)
    {
        decimal abs = Math.Abs(uf);
        if (abs >= 100m) return $"{uf:N0} UF";
        if (abs >= 10m) return $"{uf:N1} UF";
        return $"{uf:N2} UF";
    }

    public string FormatKpiDelta(decimal pct)
    {
        string arrow = pct >= 0 ? "↑" : "↓";
        return $"{arrow} {Math.Abs(pct):N1}%";
    }

    public (decimal Pct, string Code)? GetDelta(string metricKey, decimal currentValue, IEnumerable<BudgetVersionRow> versions)
    {
        var previous = versions.LastOrDefault(v => v.Snapshot != null);

        if (previous == null) return null;

        decimal previousValue = GetMetricValue(metricKey, previous.Snapshot);
        if (previousValue == 0m) return null;

        decimal pct = (currentValue - previousValue) / previousValue * 100m;
        string code = string.IsNullOrWhiteSpace(previous.Codigo) ? "versión anterior" : previous.Codigo;
        return (pct, code);
    }

    public List<decimal> GetHistoryValues(string metricKey, decimal currentValue, IEnumerable<BudgetVersionRow> versions)
    {
        var values = versions
            .Where(v => v.Snapshot != null)
            .OrderBy(v => v.FechaCreacion)
            .ThenBy(v => v.FechaModificacion)
            .TakeLast(6)
            .Select(v => GetMetricValue(metricKey, v.Snapshot))
            .ToList();

        values.Add(currentValue);

        if (values.Count == 1)
        {
            decimal seed = currentValue == 0m ? 1m : currentValue;
            values.Insert(0, seed * 0.92m);
            values.Insert(1, seed * 0.97m);
            values.Insert(2, seed * 0.94m);
            values.Insert(3, seed * 1.01m);
            values.Insert(4, seed * 0.99m);
            values.Insert(5, seed * 1.03m);
        }

        return values;
    }

    public decimal GetMetricValue(string metricKey, BudgetVersionData snapshot)
    {
        var cost = budgetCalculationService.CalculateBudget(
            snapshot.Cubicacion,
            BudgetSettings.FromProject(snapshot.Proyecto),
            snapshot.Apus,
            snapshot.Itemizado);

        return metricKey switch
        {
            "Material" => cost.MaterialesUf,
            "ManoObra" => cost.ManoObraUf,
            "UfM2" => cost.UfM2,
            "Venta" => cost.TotalVentaUf,
            _ => cost.CostoDirectoUf
        };
    }
}
