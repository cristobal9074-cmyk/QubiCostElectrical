using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class EconomicValidation
{
    public IReadOnlyList<EconomicValidationError> Validate(EconomicConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        var errors = new List<EconomicValidationError>();

        var concepts = config.Concepts;
        if (concepts.Count == 0)
        {
            errors.Add(new() { Code = "EMPTY_CONFIG", Message = "No concepts defined", Severity = EconomicValidationSeverity.Error });
            return errors;
        }

        var orderSet = new HashSet<int>();
        foreach (var c in concepts)
        {
            if (string.IsNullOrWhiteSpace(c.Code))
                errors.Add(new() { Code = "MISSING_CODE", Message = $"Concept missing code", Field = "Code", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Error });

            if (!orderSet.Add(c.Order))
                errors.Add(new() { Code = "DUPLICATE_ORDER", Message = $"Duplicate order {c.Order} for '{c.Code}'", Field = "Order", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Error });

            if (c.Value < 0m && c.Category != EconomicCategory.Discount)
                errors.Add(new() { Code = "NEGATIVE_VALUE", Message = $"Negative value {c.Value} for '{c.Code}'", Field = "Value", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Warning });

            if (c.Value > 100m && c.CalculationType == CalculationType.Percentage)
                errors.Add(new() { Code = "EXCESSIVE_PCT", Message = $"Percentage {c.Value}% exceeds 100% for '{c.Code}'", Field = "Value", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Warning });

            if (c.CalculationType == CalculationType.FixedAmount && c.Value == 0m)
                errors.Add(new() { Code = "ZERO_AMOUNT", Message = $"Fixed amount concept '{c.Code}' has zero value", Field = "Value", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Warning });

            if (c.IsInformative && c.AffectsFinalPrice)
                errors.Add(new() { Code = "INFORMATIVE_AFFECTS_PRICE", Message = $"Informative concept '{c.Code}' should not affect final price", Field = "AffectsFinalPrice", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Error });

            switch (c.Category)
            {
                case EconomicCategory.Tax when c.Value > 100m:
                    errors.Add(new() { Code = "TAX_OVER_100", Message = $"Tax '{c.Code}' rate {c.Value}% exceeds 100%", Field = "Value", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Error });
                    break;
                case EconomicCategory.Discount when c.Value > 100m:
                    errors.Add(new() { Code = "DISCOUNT_OVER_100", Message = $"Discount '{c.Code}' rate {c.Value}% exceeds 100%", Field = "Value", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Warning });
                    break;
            }
        }

        var baseCycleErrors = DetectBaseCycles(concepts);
        errors.AddRange(baseCycleErrors);

        return errors.AsReadOnly();
    }

    private static List<EconomicValidationError> DetectBaseCycles(IReadOnlyList<EconomicConcept> concepts)
    {
        var errors = new List<EconomicValidationError>();
        var futureBases = new HashSet<CalculationBase>
        {
            CalculationBase.PreviousSubtotal,
            CalculationBase.SubtotalBeforeProfit,
            CalculationBase.SubtotalBeforeTax,
            CalculationBase.PriceBeforeDiscounts,
            CalculationBase.FinalPreviousPrice
        };

        foreach (var c in concepts)
        {
            if (futureBases.Contains(c.CalculationBase) && c.Order <= 1)
                errors.Add(new() { Code = "BASE_BEFORE_EXISTS", Message = $"'{c.Code}' uses {c.CalculationBase} at order {c.Order} before it exists", Field = "Order", ConceptCode = c.Code, Severity = EconomicValidationSeverity.Error });
        }

        return errors;
    }

    public EconomicValidationResult ValidateForCalculation(EconomicConfig config)
    {
        var errors = Validate(config);
        return new EconomicValidationResult
        {
            IsValid = errors.All(e => e.Severity == EconomicValidationSeverity.Warning),
            Errors = errors.Where(e => e.Severity == EconomicValidationSeverity.Error).ToList().AsReadOnly(),
            Warnings = errors.Where(e => e.Severity == EconomicValidationSeverity.Warning).ToList().AsReadOnly()
        };
    }
}

public sealed record EconomicValidationError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Field { get; init; } = "";
    public string ConceptCode { get; init; } = "";
    public string ScenarioId { get; init; } = "";
    public EconomicValidationSeverity Severity { get; init; } = EconomicValidationSeverity.Error;
    public string SafeMetadata { get; init; } = "";
}

public enum EconomicValidationSeverity { Error, Warning }

public sealed record EconomicValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<EconomicValidationError> Errors { get; init; } = Array.Empty<EconomicValidationError>();
    public IReadOnlyList<EconomicValidationError> Warnings { get; init; } = Array.Empty<EconomicValidationError>();
}
