using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QubiCostElectrical;

public static class EconomicEngine
{
    public static EconomicResult Calculate(EconomicInput input, CancellationToken ct = default(CancellationToken))
    {
        if (input == null)
        {
            throw new ArgumentNullException("input");
        }
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<EconomicConcept> activeConcepts = input.Config.GetActiveConcepts();
        List<EconomicStep> list = new List<EconomicStep>();
        List<ConceptResult> list2 = new List<ConceptResult>();
        List<string> list3 = new List<string>();
        List<string> list4 = new List<string>();
        List<string> list5 = new List<string>();
        List<RoundingDetail> list6 = new List<RoundingDetail>();
        DateTime utcNow = DateTime.UtcNow;
        ValidateOrder(activeConcepts, list4);
        decimal num = input.UnitDirectCost;
        decimal indirect = 0m;
        decimal overhead = 0m;
        decimal admin = 0m;
        decimal supervision = 0m;
        decimal contingency = 0m;
        decimal risk = 0m;
        decimal profit = 0m;
        decimal surcharges = 0m;
        decimal discounts = 0m;
        decimal taxes = 0m;
        decimal withholdings = 0m;
        int num2 = 0;
        foreach (EconomicConcept item4 in activeConcepts)
        {
            ct.ThrowIfCancellationRequested();
            num2++;
            decimal num3 = ResolveBase(item4.CalculationBase, input, num, indirect);
            (decimal value, string formula) tuple = ComputeValue(item4, num3, num);
            decimal item = tuple.value;
            string item2 = tuple.formula;
            int roundingDecimals = item4.RoundingDecimals;
            decimal num4 = PrecisionPolicy.Apply(item, roundingDecimals);
            list6.Add(new RoundingDetail
            {
                ConceptCode = item4.Code,
                UnroundedValue = item,
                RoundedValue = num4,
                Decimals = roundingDecimals,
                RoundingMode = "ToEven"
            });
            decimal num5 = (item4.IsInformative ? num : (num + ((item4.Category == EconomicCategory.Discount) ? (-num4) : num4)));
            if (num5 < 0m && item4.Category != EconomicCategory.Discount)
            {
                list4.Add($"Negative total after applying '{item4.Code}': {num5}");
            }
            if (item4.Category == EconomicCategory.Discount && num4 > Math.Abs(num))
            {
                list5.Add($"Discount '{item4.Code}' ({num4}) exceeds previous subtotal ({num}); clamping to zero");
                num5 = 0m;
            }
            string explanation = ((item4.Category == EconomicCategory.Discount) ? $"{item4.Name}: {num3} × {item4.Value}% = {num4} (discount)" : $"{item4.Name}: {num3} × {item4.Value}% = {num4}");
            ConceptResult item3 = new ConceptResult
            {
                ConceptId = item4.Id,
                Code = item4.Code,
                Category = item4.Category,
                UsedBase = item4.CalculationBase,
                BaseValue = PrecisionPolicy.RoundPrecioUnitario(num3),
                CalcType = item4.CalculationType,
                RateOrAmount = item4.Value,
                UnroundedValue = item,
                RoundedValue = num4,
                PreviousSubtotal = PrecisionPolicy.RoundImporte(num),
                PostSubtotal = PrecisionPolicy.RoundImporte(num5),
                AffectsPrice = item4.AffectsFinalPrice,
                IsInformative = item4.IsInformative,
                Order = item4.Order,
                Explanation = explanation
            };
            list2.Add(item3);
            list.Add(new EconomicStep
            {
                StepNumber = num2,
                ConceptCode = item4.Code,
                Formula = item2,
                BaseValue = PrecisionPolicy.RoundPrecioUnitario(num3),
                AppliedValue = num4,
                RoundingDecimals = roundingDecimals,
                SubtotalBefore = PrecisionPolicy.RoundImporte(num),
                SubtotalAfter = PrecisionPolicy.RoundImporte(num5),
                Explanation = explanation
            });
            if (!item4.IsInformative)
            {
                num = num5;
            }
            AccumulateCategory(item4.Category, num4, ref indirect, ref overhead, ref admin, ref supervision, ref contingency, ref risk, ref profit, ref surcharges, ref discounts, ref taxes, ref withholdings);
        }
        TimeSpan elapsed = DateTime.UtcNow - utcNow;
        decimal num6 = PrecisionPolicy.RoundPrecioUnitario(num);
        decimal totalPartidaCost = PrecisionPolicy.RoundImporte(num6 * input.PartidaQuantity);
        return new EconomicResult
        {
            UnitDirectCost = PrecisionPolicy.RoundImporte(input.UnitDirectCost),
            TotalIndirectCosts = PrecisionPolicy.RoundImporte(indirect),
            Overhead = PrecisionPolicy.RoundImporte(overhead),
            Administration = PrecisionPolicy.RoundImporte(admin),
            Supervision = PrecisionPolicy.RoundImporte(supervision),
            Contingency = PrecisionPolicy.RoundImporte(contingency),
            Risk = PrecisionPolicy.RoundImporte(risk),
            Profit = PrecisionPolicy.RoundImporte(profit),
            TotalSurcharges = PrecisionPolicy.RoundImporte(surcharges),
            TotalDiscounts = PrecisionPolicy.RoundImporte(discounts),
            TotalTaxes = PrecisionPolicy.RoundImporte(taxes),
            TotalInformativeWithholdings = PrecisionPolicy.RoundImporte(withholdings),
            FinalUnitPrice = num6,
            PartidaQuantity = input.PartidaQuantity,
            TotalPartidaCost = totalPartidaCost,
            Currency = input.Currency,
            CalculationDate = DateTime.UtcNow,
            ConceptsApplied = list2.AsReadOnly(),
            ConceptsOmitted = list3.AsReadOnly(),
            Errors = list4.AsReadOnly(),
            Warnings = list5.AsReadOnly(),
            RoundingDetails = list6.AsReadOnly(),
            Trace = new EconomicCalculationTrace
            {
                StepsExecuted = num2,
                Elapsed = elapsed,
                Steps = list.AsReadOnly()
            },
            EngineVersion = input.EngineVersion,
            DeterministicHash = ComputeHash(list2)
        };
    }

    private static decimal ResolveBase(CalculationBase calcBase, EconomicInput input, decimal previousSubtotal, decimal totalIndirect)
    {
        return calcBase switch
        {
            CalculationBase.DirectCost => input.UnitDirectCost,
            CalculationBase.Materials => input.Materials,
            CalculationBase.Labor => input.Labor,
            CalculationBase.Equipment => input.Equipment,
            CalculationBase.Tools => input.Tools,
            CalculationBase.Subcontract => input.Subcontract,
            CalculationBase.Transport => input.Transport,
            CalculationBase.Auxiliaries => input.Auxiliaries,
            CalculationBase.OtherResources => 0m,
            CalculationBase.DirectCostPlusIndirect => input.UnitDirectCost + totalIndirect,
            CalculationBase.PreviousSubtotal => previousSubtotal,
            CalculationBase.SubtotalBeforeProfit => previousSubtotal,
            CalculationBase.SubtotalBeforeTax => previousSubtotal,
            CalculationBase.PriceBeforeDiscounts => previousSubtotal,
            CalculationBase.FinalPreviousPrice => previousSubtotal,
            CalculationBase.Custom => previousSubtotal,
            _ => previousSubtotal,
        };
    }

    private static (decimal value, string formula) ComputeValue(EconomicConcept concept, decimal baseValue, decimal previousSubtotal)
    {
        return concept.CalculationType switch
        {
            CalculationType.Percentage => (value: baseValue * concept.Value / 100m, formula: $"{baseValue} × {concept.Value}%"),
            CalculationType.FixedAmount => (value: concept.Value, formula: $"{concept.Value} (fixed)"),
            CalculationType.AmountPerUnit => (value: concept.Value * previousSubtotal, formula: $"{concept.Value} × {previousSubtotal}"),
            CalculationType.AmountPerQuantity => (value: concept.Value, formula: $"{concept.Value} (per qty)"),
            CalculationType.CumulativePercentage => (value: previousSubtotal * concept.Value / 100m, formula: $"{previousSubtotal} × {concept.Value}%"),
            CalculationType.PercentageOnSelectedBase => (value: baseValue * concept.Value / 100m, formula: $"{baseValue} × {concept.Value}%"),
            _ => (value: 0m, formula: "unknown"),
        };
    }

    private static void AccumulateCategory(EconomicCategory cat, decimal value, ref decimal indirect, ref decimal overhead, ref decimal admin, ref decimal supervision, ref decimal contingency, ref decimal risk, ref decimal profit, ref decimal surcharges, ref decimal discounts, ref decimal taxes, ref decimal withholdings)
    {
        switch (cat)
        {
            case EconomicCategory.IndirectCost:
                indirect += value;
                break;
            case EconomicCategory.Overhead:
                overhead += value;
                break;
            case EconomicCategory.Administration:
                admin += value;
                break;
            case EconomicCategory.Supervision:
                supervision += value;
                break;
            case EconomicCategory.Contingency:
                contingency += value;
                break;
            case EconomicCategory.Risk:
                risk += value;
                break;
            case EconomicCategory.Profit:
                profit += value;
                break;
            case EconomicCategory.Surcharge:
                surcharges += value;
                break;
            case EconomicCategory.Discount:
                discounts += value;
                break;
            case EconomicCategory.Tax:
                taxes += value;
                break;
            case EconomicCategory.InformativeWithholding:
                withholdings += value;
                break;
        }
    }

    private static void ValidateOrder(IReadOnlyList<EconomicConcept> concepts, List<string> errors)
    {
        HashSet<int> hashSet = new HashSet<int>();
        foreach (EconomicConcept concept in concepts)
        {
            if (!hashSet.Add(concept.Order))
            {
                errors.Add($"Duplicate order '{concept.Order}' for concept '{concept.Code}'");
            }
        }
    }

    private static string ComputeHash(IReadOnlyList<ConceptResult> concepts)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (ConceptResult concept in concepts)
        {
            stringBuilder.Append(concept.Code).Append(':').Append(concept.RoundedValue)
                .Append(';');
        }
        using SHA256 sHA = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
        return Convert.ToHexString(sHA.ComputeHash(bytes)).ToLowerInvariant();
    }
}
