using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class EconomicValidation
{
    public IReadOnlyList<EconomicValidationError> Validate(EconomicConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException("config");
        }
        List<EconomicValidationError> list = new List<EconomicValidationError>();
        IReadOnlyList<EconomicConcept> concepts = config.Concepts;
        if (concepts.Count == 0)
        {
            list.Add(new EconomicValidationError
            {
                Code = "EMPTY_CONFIG",
                Message = "No concepts defined",
                Severity = EconomicValidationSeverity.Error
            });
            return list;
        }
        HashSet<int> hashSet = new HashSet<int>();
        foreach (EconomicConcept item in concepts)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                list.Add(new EconomicValidationError
                {
                    Code = "MISSING_CODE",
                    Message = "Concept missing code",
                    Field = "Code",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Error
                });
            }
            if (!hashSet.Add(item.Order))
            {
                list.Add(new EconomicValidationError
                {
                    Code = "DUPLICATE_ORDER",
                    Message = $"Duplicate order {item.Order} for '{item.Code}'",
                    Field = "Order",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Error
                });
            }
            if (item.Value < 0m && item.Category != EconomicCategory.Discount)
            {
                list.Add(new EconomicValidationError
                {
                    Code = "NEGATIVE_VALUE",
                    Message = $"Negative value {item.Value} for '{item.Code}'",
                    Field = "Value",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Warning
                });
            }
            if (item.Value > 100m && item.CalculationType == CalculationType.Percentage)
            {
                list.Add(new EconomicValidationError
                {
                    Code = "EXCESSIVE_PCT",
                    Message = $"Percentage {item.Value}% exceeds 100% for '{item.Code}'",
                    Field = "Value",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Warning
                });
            }
            if (item.CalculationType == CalculationType.FixedAmount && item.Value == 0m)
            {
                list.Add(new EconomicValidationError
                {
                    Code = "ZERO_AMOUNT",
                    Message = "Fixed amount concept '" + item.Code + "' has zero value",
                    Field = "Value",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Warning
                });
            }
            if (item.IsInformative && item.AffectsFinalPrice)
            {
                list.Add(new EconomicValidationError
                {
                    Code = "INFORMATIVE_AFFECTS_PRICE",
                    Message = "Informative concept '" + item.Code + "' should not affect final price",
                    Field = "AffectsFinalPrice",
                    ConceptCode = item.Code,
                    Severity = EconomicValidationSeverity.Error
                });
            }
            switch (item.Category)
            {
                case EconomicCategory.Tax:
                    if (item.Value > 100m)
                    {
                        list.Add(new EconomicValidationError
                        {
                            Code = "TAX_OVER_100",
                            Message = $"Tax '{item.Code}' rate {item.Value}% exceeds 100%",
                            Field = "Value",
                            ConceptCode = item.Code,
                            Severity = EconomicValidationSeverity.Error
                        });
                    }
                    break;
                case EconomicCategory.Discount:
                    if (item.Value > 100m)
                    {
                        list.Add(new EconomicValidationError
                        {
                            Code = "DISCOUNT_OVER_100",
                            Message = $"Discount '{item.Code}' rate {item.Value}% exceeds 100%",
                            Field = "Value",
                            ConceptCode = item.Code,
                            Severity = EconomicValidationSeverity.Warning
                        });
                    }
                    break;
            }
        }
        List<EconomicValidationError> collection = DetectBaseCycles(concepts);
        list.AddRange(collection);
        return list.AsReadOnly();
    }

    private static List<EconomicValidationError> DetectBaseCycles(IReadOnlyList<EconomicConcept> concepts)
    {
        List<EconomicValidationError> list = new List<EconomicValidationError>();
        HashSet<CalculationBase> hashSet = new HashSet<CalculationBase>
        {
            CalculationBase.PreviousSubtotal,
            CalculationBase.SubtotalBeforeProfit,
            CalculationBase.SubtotalBeforeTax,
            CalculationBase.PriceBeforeDiscounts,
            CalculationBase.FinalPreviousPrice
        };
        foreach (EconomicConcept concept in concepts)
        {
            if (hashSet.Contains(concept.CalculationBase) && concept.Order <= 1)
            {
                list.Add(new EconomicValidationError
                {
                    Code = "BASE_BEFORE_EXISTS",
                    Message = $"'{concept.Code}' uses {concept.CalculationBase} at order {concept.Order} before it exists",
                    Field = "Order",
                    ConceptCode = concept.Code,
                    Severity = EconomicValidationSeverity.Error
                });
            }
        }
        return list;
    }

    public EconomicValidationResult ValidateForCalculation(EconomicConfig config)
    {
        IReadOnlyList<EconomicValidationError> source = Validate(config);
        return new EconomicValidationResult
        {
            IsValid = source.All((EconomicValidationError e) => e.Severity == EconomicValidationSeverity.Warning),
            Errors = source.Where((EconomicValidationError e) => e.Severity == EconomicValidationSeverity.Error).ToList().AsReadOnly(),
            Warnings = source.Where((EconomicValidationError e) => e.Severity == EconomicValidationSeverity.Warning).ToList().AsReadOnly()
        };
    }
}
