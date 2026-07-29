using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.EconomicEditor;

public sealed partial class EconomicEditorViewModel
{
    private void AddConcept()
    {
        var maxOrder = concepts.Count > 0 ? concepts.Max(c => c.SortOrder) : 0;
        concepts.Add(new EconomicConceptVm
        {
            ConceptCode = $"CON-{maxOrder + 1}",
            ConceptName = "Nuevo concepto",
            SortOrder = maxOrder + 1
        });
        IsDirty = true;
    }

    private void RemoveConcept(EconomicConceptVm? concept)
    {
        if (concept == null || !concepts.Remove(concept)) return;
        Reindex();
        IsDirty = true;
    }

    private void Reindex()
    {
        int i = 1;
        foreach (var c in concepts.OrderBy(c => c.SortOrder).ThenBy(c => c.ConceptCode))
            c.SortOrder = i++;
    }

    public async Task CalculateAsync()
    {
        calculationCts?.Cancel();
        calculationCts = new CancellationTokenSource();
        var ct = calculationCts.Token;

        IsCalculating = true;
        StatusMessage = "Calculando...";

        try
        {
            await Task.Delay(50, ct);
            if (ct.IsCancellationRequested) return;

            decimal runningTotal = DirectCost;
            foreach (var concept in concepts.Where(c => c.IsActive).OrderBy(c => c.SortOrder))
            {
                if (ct.IsCancellationRequested) return;
                concept.SubtotalBefore = runningTotal;
                concept.CalculatedValue = concept.ConceptType switch
                {
                    "Porcentaje" => PrecisionPolicy.RoundImporte(runningTotal * concept.RateOrAmount / 100m),
                    "Fijo" => concept.RateOrAmount,
                    _ => concept.RateOrAmount
                };
                if (concept.IncludedInTotal)
                    runningTotal += concept.CalculatedValue;
                concept.SubtotalAfter = runningTotal;
            }

            TotalCost = runningTotal;
            StatusMessage = "Cálculo completado.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cálculo cancelado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    private void Clear()
    {
        concepts.Clear();
        DirectCost = 0;
        TotalCost = 0;
        IsDirty = false;
        StatusMessage = "";
    }
}
