using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public static class EconomicEngine
{
    public static EconomicResult Calculate(EconomicInput input, CancellationToken ct = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        ct.ThrowIfCancellationRequested();

        var config = input.Config;
        var activeConcepts = config.GetActiveConcepts();
        var steps = new List<EconomicStep>();
        var conceptsApplied = new List<ConceptResult>();
        var conceptsOmitted = new List<string>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var roundingDetails = new List<RoundingDetail>();
        var start = DateTime.UtcNow;

        ValidateOrder(activeConcepts, errors);

        decimal previousSubtotal = input.UnitDirectCost;
        decimal totalIndirect = 0m, overhead = 0m, admin = 0m, supervision = 0m;
        decimal contingency = 0m, risk = 0m, profit = 0m;
        decimal totalSurcharges = 0m, totalDiscounts = 0m, totalTaxes = 0m;
        decimal totalWithholdings = 0m;
        int stepNum = 0;

        foreach (var concept in activeConcepts)
        {
            ct.ThrowIfCancellationRequested();
            stepNum++;

            var baseValue = ResolveBase(concept.CalculationBase, input, previousSubtotal, totalIndirect);
            var (unrounded, formula) = ComputeValue(concept, baseValue, previousSubtotal);

            var roundingDecimals = concept.RoundingDecimals;
            var rounded = PrecisionPolicy.Apply(unrounded, roundingDecimals);

            roundingDetails.Add(new()
            {
                ConceptCode = concept.Code,
                UnroundedValue = unrounded,
                RoundedValue = rounded,
                Decimals = roundingDecimals,
                RoundingMode = "ToEven"
            });

            decimal postSubtotal = concept.IsInformative
                ? previousSubtotal
                : previousSubtotal + (concept.Category == EconomicCategory.Discount ? -rounded : rounded);

            if (postSubtotal < 0m && concept.Category != EconomicCategory.Discount)
            {
                errors.Add($"Negative total after applying '{concept.Code}': {postSubtotal}");
            }

            if (concept.Category == EconomicCategory.Discount && rounded > Math.Abs(previousSubtotal))
            {
                warnings.Add($"Discount '{concept.Code}' ({rounded}) exceeds previous subtotal ({previousSubtotal}); clamping to zero");
                postSubtotal = 0m;
            }

            string explanation = concept.Category == EconomicCategory.Discount
                ? $"{concept.Name}: {baseValue} × {concept.Value}% = {rounded} (discount)"
                : $"{concept.Name}: {baseValue} × {concept.Value}% = {rounded}";

            var conceptResult = new ConceptResult
            {
                ConceptId = concept.Id,
                Code = concept.Code,
                Category = concept.Category,
                UsedBase = concept.CalculationBase,
                BaseValue = PrecisionPolicy.RoundPrecioUnitario(baseValue),
                CalcType = concept.CalculationType,
                RateOrAmount = concept.Value,
                UnroundedValue = unrounded,
                RoundedValue = rounded,
                PreviousSubtotal = PrecisionPolicy.RoundImporte(previousSubtotal),
                PostSubtotal = PrecisionPolicy.RoundImporte(postSubtotal),
                AffectsPrice = concept.AffectsFinalPrice,
                IsInformative = concept.IsInformative,
                Order = concept.Order,
                Explanation = explanation
            };
            conceptsApplied.Add(conceptResult);

            steps.Add(new()
            {
                StepNumber = stepNum,
                ConceptCode = concept.Code,
                Formula = formula,
                BaseValue = PrecisionPolicy.RoundPrecioUnitario(baseValue),
                AppliedValue = rounded,
                RoundingDecimals = roundingDecimals,
                SubtotalBefore = PrecisionPolicy.RoundImporte(previousSubtotal),
                SubtotalAfter = PrecisionPolicy.RoundImporte(postSubtotal),
                Explanation = explanation
            });

            if (!concept.IsInformative)
                previousSubtotal = postSubtotal;

            AccumulateCategory(concept.Category, rounded, ref totalIndirect, ref overhead, ref admin, ref supervision, ref contingency, ref risk, ref profit, ref totalSurcharges, ref totalDiscounts, ref totalTaxes, ref totalWithholdings);
        }

        var elapsed = DateTime.UtcNow - start;
        var finalUnitPrice = PrecisionPolicy.RoundPrecioUnitario(previousSubtotal);
        var totalPartida = PrecisionPolicy.RoundImporte(finalUnitPrice * input.PartidaQuantity);

        return new EconomicResult
        {
            UnitDirectCost = PrecisionPolicy.RoundImporte(input.UnitDirectCost),
            TotalIndirectCosts = PrecisionPolicy.RoundImporte(totalIndirect),
            Overhead = PrecisionPolicy.RoundImporte(overhead),
            Administration = PrecisionPolicy.RoundImporte(admin),
            Supervision = PrecisionPolicy.RoundImporte(supervision),
            Contingency = PrecisionPolicy.RoundImporte(contingency),
            Risk = PrecisionPolicy.RoundImporte(risk),
            Profit = PrecisionPolicy.RoundImporte(profit),
            TotalSurcharges = PrecisionPolicy.RoundImporte(totalSurcharges),
            TotalDiscounts = PrecisionPolicy.RoundImporte(totalDiscounts),
            TotalTaxes = PrecisionPolicy.RoundImporte(totalTaxes),
            TotalInformativeWithholdings = PrecisionPolicy.RoundImporte(totalWithholdings),
            FinalUnitPrice = finalUnitPrice,
            PartidaQuantity = input.PartidaQuantity,
            TotalPartidaCost = totalPartida,
            Currency = input.Currency,
            CalculationDate = DateTime.UtcNow,
            ConceptsApplied = conceptsApplied.AsReadOnly(),
            ConceptsOmitted = conceptsOmitted.AsReadOnly(),
            Errors = errors.AsReadOnly(),
            Warnings = warnings.AsReadOnly(),
            RoundingDetails = roundingDetails.AsReadOnly(),
            Trace = new EconomicCalculationTrace
            {
                StepsExecuted = stepNum,
                Elapsed = elapsed,
                Steps = steps.AsReadOnly()
            },
            EngineVersion = input.EngineVersion,
            DeterministicHash = ComputeHash(conceptsApplied)
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
            _ => previousSubtotal
        };
    }

    private static (decimal value, string formula) ComputeValue(EconomicConcept concept, decimal baseValue, decimal previousSubtotal)
    {
        return concept.CalculationType switch
        {
            CalculationType.Percentage => (
                baseValue * concept.Value / 100m,
                $"{baseValue} × {concept.Value}%"
            ),
            CalculationType.FixedAmount => (
                concept.Value,
                $"{concept.Value} (fixed)"
            ),
            CalculationType.AmountPerUnit => (
                concept.Value * previousSubtotal,
                $"{concept.Value} × {previousSubtotal}"
            ),
            CalculationType.AmountPerQuantity => (
                concept.Value,
                $"{concept.Value} (per qty)"
            ),
            CalculationType.CumulativePercentage => (
                previousSubtotal * concept.Value / 100m,
                $"{previousSubtotal} × {concept.Value}%"
            ),
            CalculationType.PercentageOnSelectedBase => (
                baseValue * concept.Value / 100m,
                $"{baseValue} × {concept.Value}%"
            ),
            _ => (0m, "unknown")
        };
    }

    private static void AccumulateCategory(
        EconomicCategory cat, decimal value,
        ref decimal indirect, ref decimal overhead, ref decimal admin, ref decimal supervision,
        ref decimal contingency, ref decimal risk, ref decimal profit,
        ref decimal surcharges, ref decimal discounts, ref decimal taxes,
        ref decimal withholdings)
    {
        switch (cat)
        {
            case EconomicCategory.IndirectCost: indirect += value; break;
            case EconomicCategory.Overhead: overhead += value; break;
            case EconomicCategory.Administration: admin += value; break;
            case EconomicCategory.Supervision: supervision += value; break;
            case EconomicCategory.Contingency: contingency += value; break;
            case EconomicCategory.Risk: risk += value; break;
            case EconomicCategory.Profit: profit += value; break;
            case EconomicCategory.Surcharge: surcharges += value; break;
            case EconomicCategory.Discount: discounts += value; break;
            case EconomicCategory.Tax: taxes += value; break;
            case EconomicCategory.InformativeWithholding: withholdings += value; break;
        }
    }

    private static void ValidateOrder(IReadOnlyList<EconomicConcept> concepts, List<string> errors)
    {
        var orders = new HashSet<int>();
        foreach (var c in concepts)
        {
            if (!orders.Add(c.Order))
                errors.Add($"Duplicate order '{c.Order}' for concept '{c.Code}'");
        }
    }

    private static string ComputeHash(IReadOnlyList<ConceptResult> concepts)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in concepts)
        {
            sb.Append(c.Code).Append(':').Append(c.RoundedValue).Append(';');
        }
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
