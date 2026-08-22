using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public static class ScenarioEngine
{
	public static EconomicResult CalculateScenario(Scenario scenario, EconomicInput baseInput, CancellationToken ct = default(CancellationToken))
	{
		if (scenario == null)
		{
			throw new ArgumentNullException("scenario");
		}
		if (baseInput == null)
		{
			throw new ArgumentNullException("baseInput");
		}
		ct.ThrowIfCancellationRequested();
		return EconomicEngine.Calculate(ApplyOverrides(scenario, baseInput, ct), ct);
	}

	public static ScenarioComparison Compare(EconomicResult baseResult, EconomicResult comparedResult, string baseName, string comparedName)
	{
		if (baseResult == null)
		{
			throw new ArgumentNullException("baseResult");
		}
		if (comparedResult == null)
		{
			throw new ArgumentNullException("comparedResult");
		}
		decimal value = comparedResult.UnitDirectCost - baseResult.UnitDirectCost;
		decimal value2 = comparedResult.TotalIndirectCosts - baseResult.TotalIndirectCosts;
		decimal value3 = comparedResult.Profit - baseResult.Profit;
		decimal value4 = comparedResult.TotalTaxes - baseResult.TotalTaxes;
		decimal num = comparedResult.FinalUnitPrice - baseResult.FinalUnitPrice;
		decimal value5 = comparedResult.TotalPartidaCost - baseResult.TotalPartidaCost;
		decimal value6 = ((baseResult.FinalUnitPrice > 0m) ? (num / baseResult.FinalUnitPrice * 100m) : 0m);
		IReadOnlyList<ConceptDifference> readOnlyList = BuildConceptDifferences(baseResult, comparedResult);
		ReadOnlyCollection<string> modifiedConcepts = (from d in readOnlyList
			where d.Difference != 0m
			select d.Code).Distinct().ToList().AsReadOnly();
		return new ScenarioComparison
		{
			BaseScenarioName = baseName,
			ComparedScenarioName = comparedName,
			DirectCostDifference = PrecisionPolicy.RoundImporte(value),
			IndirectDifference = PrecisionPolicy.RoundImporte(value2),
			ProfitDifference = PrecisionPolicy.RoundImporte(value3),
			TaxDifference = PrecisionPolicy.RoundImporte(value4),
			UnitPriceDifference = PrecisionPolicy.RoundPrecioUnitario(num),
			TotalDifference = PrecisionPolicy.RoundImporte(value5),
			VariationPercent = PrecisionPolicy.RoundPorcentaje(value6),
			ConceptDifferences = readOnlyList,
			ModifiedConcepts = modifiedConcepts,
			TopImpactResources = Array.Empty<ResourceImpactRow>()
		};
	}

	private static EconomicInput ApplyOverrides(Scenario scenario, EconomicInput baseInput, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		List<EconomicConcept> list = baseInput.Config.Concepts.Select((EconomicConcept c) => c).ToList();
		foreach (ScenarioOverride @override in scenario.Overrides)
		{
			ct.ThrowIfCancellationRequested();
			if (@override.TargetType != OverrideTargetType.EconomicConcept && @override.TargetType != OverrideTargetType.ConceptRate && @override.TargetType != OverrideTargetType.ConceptAmount && @override.TargetType != OverrideTargetType.ConceptToggle)
			{
				continue;
			}
			for (int num = 0; num < list.Count; num++)
			{
				if (list[num].Code == @override.TargetId)
				{
					EconomicConcept economicConcept = list[num];
					List<EconomicConcept> list2 = list;
					int index = num;
					list2[index] = @override.Field switch
					{
						OverrideField.Value => economicConcept with
						{
							Value = (@override.NumericValue ?? economicConcept.Value)
						}, 
						OverrideField.Rate => economicConcept with
						{
							Value = (@override.NumericValue ?? economicConcept.Value)
						}, 
						OverrideField.IsActive => economicConcept with
						{
							IsActive = (@override.BoolValue ?? economicConcept.IsActive)
						}, 
						_ => economicConcept, 
					};
				}
			}
		}
		return baseInput with
		{
			Config = baseInput.Config with
			{
				Concepts = list.AsReadOnly()
			}
		};
	}

	private static IReadOnlyList<ConceptDifference> BuildConceptDifferences(EconomicResult baseResult, EconomicResult comparedResult)
	{
		Dictionary<string, ConceptResult> dictionary = baseResult.ConceptsApplied.ToDictionary((ConceptResult c) => c.Code);
		Dictionary<string, ConceptResult> dictionary2 = comparedResult.ConceptsApplied.ToDictionary((ConceptResult c) => c.Code);
		IEnumerable<string> enumerable = dictionary.Keys.Union(dictionary2.Keys);
		List<ConceptDifference> list = new List<ConceptDifference>();
		foreach (string item in enumerable)
		{
			decimal num = (dictionary.TryGetValue(item, out var value) ? value.RoundedValue : 0m);
			decimal num2 = (dictionary2.TryGetValue(item, out var value2) ? value2.RoundedValue : 0m);
			list.Add(new ConceptDifference
			{
				Code = item,
				BaseValue = num,
				ComparedValue = num2,
				Difference = PrecisionPolicy.RoundImporte(num2 - num)
			});
		}
		return list;
	}
}
