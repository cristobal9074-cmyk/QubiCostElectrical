using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public static class ScenarioEngine
{
    public static EconomicResult CalculateScenario(
        Scenario scenario,
        EconomicInput baseInput,
        CancellationToken ct = default)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));
        if (baseInput == null) throw new ArgumentNullException(nameof(baseInput));
        ct.ThrowIfCancellationRequested();
        var modifiedInput = ApplyOverrides(scenario, baseInput, ct);
        return EconomicEngine.Calculate(modifiedInput, ct);
    }

    public static ScenarioComparison Compare(
        EconomicResult baseResult,
        EconomicResult comparedResult,
        string baseName,
        string comparedName)
    {
        if (baseResult == null) throw new ArgumentNullException(nameof(baseResult));
        if (comparedResult == null) throw new ArgumentNullException(nameof(comparedResult));

        var directDiff = comparedResult.UnitDirectCost - baseResult.UnitDirectCost;
        var indirectDiff = comparedResult.TotalIndirectCosts - baseResult.TotalIndirectCosts;
        var profitDiff = comparedResult.Profit - baseResult.Profit;
        var taxDiff = comparedResult.TotalTaxes - baseResult.TotalTaxes;
        var unitPriceDiff = comparedResult.FinalUnitPrice - baseResult.FinalUnitPrice;
        var totalDiff = comparedResult.TotalPartidaCost - baseResult.TotalPartidaCost;
        var varPct = baseResult.FinalUnitPrice > 0m
            ? (unitPriceDiff / baseResult.FinalUnitPrice) * 100m
            : 0m;

        var conceptDiffs = BuildConceptDifferences(baseResult, comparedResult);
        var modifiedConcepts = conceptDiffs
            .Where(d => d.Difference != 0m)
            .Select(d => d.Code)
            .Distinct()
            .ToList()
            .AsReadOnly();

        return new ScenarioComparison
        {
            BaseScenarioName = baseName,
            ComparedScenarioName = comparedName,
            DirectCostDifference = PrecisionPolicy.RoundImporte(directDiff),
            IndirectDifference = PrecisionPolicy.RoundImporte(indirectDiff),
            ProfitDifference = PrecisionPolicy.RoundImporte(profitDiff),
            TaxDifference = PrecisionPolicy.RoundImporte(taxDiff),
            UnitPriceDifference = PrecisionPolicy.RoundPrecioUnitario(unitPriceDiff),
            TotalDifference = PrecisionPolicy.RoundImporte(totalDiff),
            VariationPercent = PrecisionPolicy.RoundPorcentaje(varPct),
            ConceptDifferences = conceptDiffs,
            ModifiedConcepts = modifiedConcepts,
            TopImpactResources = Array.Empty<ResourceImpactRow>()
        };
    }

    private static EconomicInput ApplyOverrides(
        Scenario scenario,
        EconomicInput baseInput,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var configConcepts = baseInput.Config.Concepts.Select(c => c).ToList();

        foreach (var ov in scenario.Overrides)
        {
            ct.ThrowIfCancellationRequested();

            if (ov.TargetType == OverrideTargetType.EconomicConcept ||
                ov.TargetType == OverrideTargetType.ConceptRate ||
                ov.TargetType == OverrideTargetType.ConceptAmount ||
                ov.TargetType == OverrideTargetType.ConceptToggle)
            {
                for (int i = 0; i < configConcepts.Count; i++)
                {
                    if (configConcepts[i].Code == ov.TargetId)
                    {
                        var c = configConcepts[i];
                        configConcepts[i] = ov.Field switch
                        {
                            OverrideField.Value => c with { Value = ov.NumericValue ?? c.Value },
                            OverrideField.Rate => c with { Value = ov.NumericValue ?? c.Value },
                            OverrideField.IsActive => c with { IsActive = ov.BoolValue ?? c.IsActive },
                            _ => c
                        };
                    }
                }
            }
        }

        return baseInput with
        {
            Config = baseInput.Config with
            {
                Concepts = configConcepts.AsReadOnly()
            }
        };
    }

    private static IReadOnlyList<ConceptDifference> BuildConceptDifferences(
        EconomicResult baseResult,
        EconomicResult comparedResult)
    {
        var baseConcepts = baseResult.ConceptsApplied.ToDictionary(c => c.Code);
        var comparedConcepts = comparedResult.ConceptsApplied.ToDictionary(c => c.Code);

        var allCodes = baseConcepts.Keys.Union(comparedConcepts.Keys);
        var diffs = new List<ConceptDifference>();

        foreach (var code in allCodes)
        {
            var bv = baseConcepts.TryGetValue(code, out var bc) ? bc.RoundedValue : 0m;
            var cv = comparedConcepts.TryGetValue(code, out var cc) ? cc.RoundedValue : 0m;
            diffs.Add(new()
            {
                Code = code,
                BaseValue = bv,
                ComparedValue = cv,
                Difference = PrecisionPolicy.RoundImporte(cv - bv)
            });
        }

        return diffs;
    }
}
