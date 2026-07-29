# Economic Calculation Specification — Fase 5C

## Overview

Deterministic, stateless engine that applies a configurable sequence of economic concepts (indirect costs, overhead, contingency, risk, profit, taxes, discounts, surcharges, withholdings) on top of a direct cost from the APU engine (Fase 5B).

## Architecture

```
EconomicInput
  ├── UnitDirectCost (from ApuCalculationResult.TotalUnitPrice)
  ├── Category subtotals (Materials, Labor, Equipment, ...)
  ├── PartidaQuantity
  ├── EconomicConfig (ordered list of EconomicConcept)
  └── EngineVersion
       │
       ▼
EconomicEngine.Calculate(input, ct)
       │
       ▼
EconomicResult
  ├── Per-category totals
  ├── FinalUnitPrice / TotalPartidaCost
  ├── ConceptsApplied (IReadOnlyList<ConceptResult>)
  ├── Trace (EconomicCalculationTrace)
  └── DeterministicHash
```

## EconomicConcept

| Field | Description |
|---|---|
| Id | Stable GUID |
| Code | Unique code (e.g. "OVERHEAD", "VAT") |
| Name | Display name |
| Category | EconomicCategory enum (11 categories) |
| CalculationType | Percentage, FixedAmount, AmountPerUnit, AmountPerQuantity, CumulativePercentage, PercentageOnSelectedBase |
| CalculationBase | CalculationBase enum (16 bases) |
| Value | Rate (%) or amount |
| Order | Application order (1-based) |
| IsActive | Enable/disable |
| AffectsFinalPrice | Whether concept impacts the running subtotal |
| IsInformative | If true, calculated but does not affect total |
| RoundingDecimals | Decimal places for rounding (default 4) |
| EffectiveDate | When this concept applies |

## Calculation Bases

| Base | Computed As |
|---|---|
| DirectCost | input.UnitDirectCost |
| Materials | input.Materials |
| Labor | input.Labor |
| Equipment | input.Equipment |
| Tools | input.Tools |
| Subcontract | input.Subcontract |
| Transport | input.Transport |
| Auxiliaries | input.Auxiliaries |
| DirectCostPlusIndirect | UnitDirectCost + cumulative indirect costs so far |
| PreviousSubtotal | Running subtotal before this concept |
| SubtotalBeforeProfit | Same as PreviousSubtotal |
| SubtotalBeforeTax | Same as PreviousSubtotal |
| PriceBeforeDiscounts | Same as PreviousSubtotal |
| FinalPreviousPrice | Same as PreviousSubtotal |
| Custom | Same as PreviousSubtotal (for future extension) |

## Calculation Types

| Type | Formula |
|---|---|
| Percentage | baseValue × value% / 100 |
| FixedAmount | value |
| AmountPerUnit | value × previousSubtotal |
| AmountPerQuantity | value |
| CumulativePercentage | previousSubtotal × value% / 100 |
| PercentageOnSelectedBase | baseValue × value% / 100 |

## Application Order

Default order (configurable):
1. IndirectCost
2. Overhead
3. Administration
4. Supervision
5. Contingency
6. Risk
7. Profit
8. Surcharge
9. Discount
10. Tax
11. InformativeWithholding

Each concept is applied sequentially using the running subtotal. Discounts subtract; all others add.

## Default Configuration

Equivalent to the legacy MarkupPolicy defaults:
- IndirectCost 8% of DirectCost
- Overhead 14% of DirectCost
- Contingency 3% of DirectCost+Indirect
- Risk 2% of PreviousSubtotal
- Profit 18% of PreviousSubtotal
- Discount 0% of PriceBeforeDiscounts
- VAT 19% of PreviousSubtotal (2 decimals)
- InformativeWithholding 10% of FinalPreviousPrice

## Precision

All rounding uses `PrecisionPolicy.Apply(value, decimals)` = `Math.Round(value, decimals, MidpointRounding.ToEven)`.

Each concept specifies its own `RoundingDecimals`. Default: 4.

## EconomicCategory (11 categories)

CostIndirect, Overhead, Administration, Supervision, Contingency, Risk, Profit, Tax, Surcharge, Discount, InformativeWithholding, Other

## Validation

| Code | Severity | Condition |
|---|---|---|
| EMPTY_CONFIG | Error | No concepts defined |
| MISSING_CODE | Error | Concept without code |
| DUPLICATE_ORDER | Error | Same order for two concepts |
| NEGATIVE_VALUE | Warning | Negative value for non-discount |
| EXCESSIVE_PCT | Warning | Percentage > 100% |
| ZERO_AMOUNT | Warning | Fixed amount concept with zero value |
| INFORMATIVE_AFFECTS_PRICE | Error | Informative concept that alters total |
| TAX_OVER_100 | Error | Tax rate > 100% |
| DISCOUNT_OVER_100 | Warning | Discount > 100% |
| BASE_BEFORE_EXISTS | Error | Future base used at order ≤ 1 |

## Security

- No dynamic expressions, scripts, reflection, or arbitrary code
- No `double` usage
- Decimal overflow is controlled by `decimal` type range (~±79e28)
- Discount clamping at zero prevents negative final price
- CancellationToken at every step
- Stateless: no shared mutable state
- Thread-safe: all inputs are immutable records; outputs are new objects

## Files (Fase 5C Domain)

| File | Description |
|---|---|
| `EconomicConcept.cs` | Concept definition (category, type, base, order, value) |
| `EconomicConfig.cs` | Configuration (list of concepts, version) + factory methods |
| `EconomicInput.cs` | Immutable input (direct cost, categories, quantity, config) |
| `EconomicResult.cs` | Structured output with per-category totals |
| `EconomicEngine.cs` | Deterministic calculation engine |
| `EconomicValidation.cs` | Validations + error/warning types |
| `Scenario.cs` | Scenario, ScenarioOverride, ScenarioComparison |
| `ScenarioEngine.cs` | Scenario calculation + comparison |
| `IEconomicCalculationService.cs` | Application service interface |
| `EconomicCalculationService.cs` | Application service implementation |

## Tests (62 total)

| Group | Count | Description |
|---|---|---|
| EconomicEngine basic | 3 | Empty config, single %, null input |
| Indirect costs | 4 | % of DC, fixed, multiple, by category |
| Overhead | 4 | On DC, on DC+indirect, fixed, order |
| Contingency/Risk | 3 | % of subtotal, % of DC, informative |
| Profit | 5 | On DC, on previous, fixed, zero, after indirects |
| Taxes | 5 | Simple IVA, multiple, duplicate, informative |
| Discounts | 3 | Percentage, fixed, excessive clamping |
| Withholdings | 2 | Displayed, not in total |
| Surcharges | 2 | Percentage, multiple |
| Order/sequence | 3 | Order tracking, sequential %, full chain |
| Precision | 3 | Per-concept rounding, bankers, quantity scaling |
| Default config | 2 | CreateDefault, FromLegacyMarkup |
| Validation | 6 | Null, empty, duplicate order, informative flag, missing code, result |
| Trace | 2 | Steps, formulas |
| Determinism | 1 | Same input, same hash |
| Cancellation | 1 | Cancelled token |
| Scenario engine | 11 | Calculation, override concept, disable concept, comparison, variation, modified concepts, null checks |
| Service layer | 4 | Calculate, validate, scenario, compare |
| Scenario edge cases | 3 | Empty overrides, nonexistent target, determinism |
| Cancellation (scenario) | 1 | Cancelled token |
