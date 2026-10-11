using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>
/// Modelo y cálculo del análisis ABC de Pareto 80/20 para el Cockpit Financiero del Dashboard.
/// Clasifica partidas en Tipo A (80% del costo), Tipo B (15%) y Tipo C (5%).
/// </summary>
public sealed class DashboardParetoSummary
{
    public int CountA { get; init; }
    public int CountB { get; init; }
    public int CountC { get; init; }

    public decimal CostA_Clp { get; init; }
    public decimal CostB_Clp { get; init; }
    public decimal CostC_Clp { get; init; }

    public decimal CostA_Uf { get; init; }
    public decimal CostB_Uf { get; init; }
    public decimal CostC_Uf { get; init; }

    public double PctA { get; init; }
    public double PctB { get; init; }
    public double PctC { get; init; }

    public bool HasData => (CountA + CountB + CountC) > 0;

    public string BadgeA => $"Tipo A: {PctA:N1}% ({CountA})";
    public string BadgeB => $"Tipo B: {PctB:N1}% ({CountB})";
    public string BadgeC => $"Tipo C: {PctC:N1}% ({CountC})";

    public string SummaryHint => CountA > 0
        ? $"80% del costo concentrado en {CountA} partida(s) críticas Tipo A"
        : "Sin partidas críticas";

    public static DashboardParetoSummary Empty { get; } = new();

    public static DashboardParetoSummary Calculate(IEnumerable<DashboardPartidaVm> partidas, decimal directCostClp, decimal valorUf)
    {
        if (partidas == null)
            return Empty;

        var list = partidas.ToList();
        if (list.Count == 0 || directCostClp <= 0m)
            return Empty;

        int countA = 0, countB = 0, countC = 0;
        double sumPctA = 0, sumPctB = 0, sumPctC = 0;

        foreach (var p in list)
        {
            switch (p.ParetoClass)
            {
                case "A":
                    countA++;
                    sumPctA += p.SharePct;
                    break;
                case "B":
                    countB++;
                    sumPctB += p.SharePct;
                    break;
                default:
                    countC++;
                    sumPctC += p.SharePct;
                    break;
            }
        }

        decimal costA = Math.Round(directCostClp * (decimal)(sumPctA / 100.0), 0);
        decimal costB = Math.Round(directCostClp * (decimal)(sumPctB / 100.0), 0);
        decimal costC = Math.Round(directCostClp * (decimal)(sumPctC / 100.0), 0);

        decimal costAUf = PriceConversion.ClpToUf(costA, valorUf);
        decimal costBUf = PriceConversion.ClpToUf(costB, valorUf);
        decimal costCUf = PriceConversion.ClpToUf(costC, valorUf);

        return new DashboardParetoSummary
        {
            CountA = countA,
            CountB = countB,
            CountC = countC,
            CostA_Clp = costA,
            CostB_Clp = costB,
            CostC_Clp = costC,
            CostA_Uf = costAUf,
            CostB_Uf = costBUf,
            CostC_Uf = costCUf,
            PctA = sumPctA,
            PctB = sumPctB,
            PctC = sumPctC
        };
    }
}
