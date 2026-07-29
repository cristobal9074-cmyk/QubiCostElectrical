# Apu Calculation Specification — Fase 5B

## Objective

Implement a deterministic, immutable, precision-controlled APU (Análisis de Precios Unitarios) calculation engine that operates on a professional APU model (`ApuDefinition` + `ApuComponent`) and produces structured, auditable results.

## Model

### ApuDefinition
- Represents a professional APU: code, description, unit, quantity, components
- Components are ordered by `SortOrder`
- `Quantity` is used to compute unit price: `TotalUnitPrice = TotalDirectCost / Quantity`

### ApuComponent
- `Resource` type: references a `Resource.Id` from the price bank
- `AuxiliaryApu` type: references another `ApuDefinition.Id` for nested APU composition
- Modifiers: `Yield` (rendimiento), `WastePercent` (desperdicio %), `AdjustmentFactor`
- Effective quantity = `RoundCantidad(Yield × (1 + WastePercent/100) × AdjustmentFactor)`

## Calculation Rules

### Resource Components
1. Look up `Resource` by `ReferenceId` in `input.Resources`
2. If not found → cost 0, explanation reports "not found"
3. Get `Resource.EffectivePrice` = `RoundPrecioUnitario(BasePrice × AdjustmentFactor)`
4. Convert currency if needed: amount × latest `ExchangeRate`
5. If `Resource.Type == Labor && input.OverrideLaborRate.HasValue` → use override
6. Subtotal = `RoundImporte(convertedPrice × effectiveQuantity)`

### Auxiliary APU Components
1. Look up `ApuDefinition` by `ReferenceId` in `input.ApuDefinitions`
2. If not found → cost 0, explanation reports "not found"
3. Recursive: `Calculate(childApu, input, ct)`
4. Apply component modifiers to child's `TotalUnitPrice`
5. Subtotal = `RoundImporte(childUnitPrice × effectiveQuantity)`
6. Include child's `Components` in `AuxiliaryComponents` (sub-tree)

### Cycle Detection
- DFS with 3-color marking: white (unvisited), gray (in-progress), black (done)
- `HasCycle()` returns bool
- `FindCyclePath()` returns the cycle path for diagnostics
- Cycle → result `TotalDirectCost = 0`, trace reports "CYCLE DETECTED"
- `ValidateNoCycles()` pre-check before batch calculation

### Cost Categories
Each component maps to one of 7 categories via `ResourceType`:
| ResourceType | Category |
|---|---|
| Material | Materials |
| Labor | Labor |
| Equipment | Equipment |
| Tool | Tools |
| Subcontract | Subcontract |
| Transport | Transport |
| Auxiliary | AuxiliaryApus |

### Precision (PrecisionPolicy)
| Context | Decimals | Method |
|---|---|---|
| Quantities | 4 | RoundCantidad |
| Unit prices | 4 | RoundPrecioUnitario |
| Totals | 2 | RoundImporte |
| Percentages | 2 | RoundPorcentaje |
| Factors | 6 | RoundFactor |
| Exchange rates | 6 | RoundTasaCambio |

All use `MidpointRounding.ToEven` (Banker's rounding).

### Currency Conversion
- Same currency → no conversion
- Different currency → find latest `ExchangeRate` by `Date` descending
- Rate from `SourceCurrency` → `TargetCurrency`

### Cancellation
- `CancellationToken` propagated to every step
- Check before resource lookup, auxiliary resolution, and at component iteration boundaries

## Results

### ApuCalculationResult
- `TotalDirectCost`: sum of all component subtotals (rounded to 2 decimals)
- `TotalUnitPrice`: `RoundPrecioUnitario(totalDirect / quantity)`
- `TotalLinePrice`: same as `TotalDirectCost` (scaled by quantity)
- `CategoryBreakdown`: per-category subtotals
- `Components`: ordered list with full detail
- `Trace`: component count, auxiliary count, elapsed, steps (one per component)

### ComponentCostDetail
- Every number is rounded per `PrecisionPolicy`
- `Explanation`: human-readable formula, e.g. "CABLE-001: 123.4567 × 3.3333 (yield=2, waste=10%, factor=1.5) = 411.52"
- `AuxiliaryComponents`: sub-tree for auxiliary APU components

## Validation

| Check | Method | Effect |
|---|---|---|
| Cycles | `ApuCycleDetector.HasCycle()` | Cost = 0, trace warns |
| Missing resource | `input.Resources.FirstOrDefault()` | Cost = 0, trace warns |
| Missing auxiliary definition | `input.ApuDefinitions.FirstOrDefault()` | Cost = 0, trace warns |
| Null args | ArgumentNullException | Throws |
| Cancelled token | OperationCanceledException | Throws |

## Test Coverage (63 tests)

| Group | Count | What |
|---|---|---|
| Basic calculation | 4 | Empty, single, multiple, sort order |
| Resource lookup | 7 | Found, not found, yield, waste, factor, all modifiers, zero yield |
| Resource type | 9 | 7 categories + multiple categories + category total |
| Auxiliary APU | 6 | Single, yield/waste, nested, missing, sub-components, category |
| Cycle detection | 7 | Self-ref, mutual, 3-cycle, chain, ValidateNoCycles, paths |
| Precision | 6 | Quantity, price, import, bankers rounding, chain, many comps |
| Currency conversion | 5 | Same, different, missing rate, date order, labor override |
| Cancellation | 3 | Pre-cancelled, resource, auxiliary |
| Service layer | 6 | ById, All, Many, Validate, MissingId, NullInput |
| Edge cases | 8 | Null args, empty defs, zero price, unit price, explanation, trace, total, immutability |
| UI helpers | 3 | Category total, immutability, with-expressions |

## File Structure

```
src/QubiCostElectrical.Domain/BudgetEngine/
  ApuComponent.cs          (21 lines)
  ApuDefinition.cs         (17 lines)
  ApuCalculationInput.cs   (11 lines)
  ApuCalculationResult.cs  (40 lines)
  ApuCycleDetector.cs      (82 lines)
  ApuCalculationEngine.cs  (196 lines)
src/QubiCostElectrical.Application/Services/
  IApuCalculationService.cs  (8 lines)
  ApuCalculationService.cs   (76 lines)
tests/QubiCostElectrical.Tests/Domain/
  ApuCalculationEngineTests.cs  (691 lines, 63 tests)
```

## Constraints
- Domain has zero dependencies on Application/Infrastructure/WPF
- No mutation of input objects
- Deterministic: same input, same output, every time
- Thread-safe: no shared mutable state
- Culture-independent numeric formatting via `MidpointRounding.ToEven`, but decimal string formatting uses current culture (UI concern)
