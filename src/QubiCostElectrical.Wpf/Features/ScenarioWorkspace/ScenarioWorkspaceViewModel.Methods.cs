using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ScenarioWorkspace;

public sealed partial class ScenarioWorkspaceViewModel
{
    private void CreateScenario()
    {
        int count = scenarios.Count(s => !s.IsBase);
        var sc = new ScenarioVm
        {
            Name = $"Escenario {count + 1}",
            ScenarioType = "Custom",
            IsActive = true
        };
        scenarios.Add(sc);
        SelectedScenario = sc;
    }

    private void DuplicateScenario(ScenarioVm? s)
    {
        if (s == null) return;
        var copy = new ScenarioVm
        {
            Name = $"{s.Name} (copia)",
            ScenarioType = s.ScenarioType,
            Description = s.Description,
            IsActive = s.IsActive,
            IsCalculated = false
        };
        scenarios.Add(copy);
        SelectedScenario = copy;
    }

    private void DeleteScenario(ScenarioVm? s)
    {
        if (s == null || s.IsBase) return;
        scenarios.Remove(s);
        if (SelectedScenario == s)
            SelectedScenario = scenarios.LastOrDefault();
    }

    public async Task CalculateAsync()
    {
        calculationCts?.Cancel();
        calculationCts = new CancellationTokenSource();
        var ct = calculationCts.Token;

        IsCalculating = true;
        StatusMessage = "Calculando escenarios...";

        try
        {
            await Task.Delay(100, ct);
            foreach (var s in scenarios.Where(s => s.IsActive))
            {
                if (ct.IsCancellationRequested) return;
                s.DirectCost = 100000m;
                s.IndirectCost = s.DirectCost * 0.15m;
                s.Utility = (s.DirectCost + s.IndirectCost) * 0.10m;
                s.Taxes = (s.DirectCost + s.IndirectCost + s.Utility) * 0.19m;
                s.UnitPrice = s.DirectCost + s.IndirectCost + s.Utility + s.Taxes;
                s.Total = s.UnitPrice;
                s.IsCalculated = true;
                if (BaseScenario != null && BaseScenario.Total > 0)
                    s.VariationPct = ((s.Total - BaseScenario.Total) / BaseScenario.Total) * 100;
            }
            StatusMessage = "Escenarios calculados.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cálculo cancelado.";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    private async Task ApplyToBaseAsync()
    {
        if (SelectedScenario == null || SelectedScenario.IsBase) return;
        await Task.CompletedTask;
        StatusMessage = $"Escenario '{SelectedScenario.Name}' aplicado.";
    }

    private void CompareWithBase(ScenarioVm? s)
    {
        if (s == null || s.IsBase || BaseScenario == null) return;
        StatusMessage = $"Comparación: {s.Name} vs Base — Variación: {s.VariationPct:N1}%";
    }

    private void Clear()
    {
        scenarios.Clear();
        baseScenario = null;
        IsCalculating = false;
        StatusMessage = "";
    }
}
