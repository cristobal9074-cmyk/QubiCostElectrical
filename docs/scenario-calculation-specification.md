# Scenario Calculation Specification — Fase 5C

## Overview

Scenarios allow calculating the same project under different economic assumptions without duplicating data. Each scenario is a set of immutable overrides applied to a base calculation.

## Scenario

| Field | Description |
|---|---|
| Id | Stable GUID |
| Name | Human-readable name |
| Description | Optional description |
| Type | Base, Conservative, Optimistic, Pessimistic, Custom |
| BaseScenarioId | Reference to the scenario this derives from |
| CreatedAt | Timestamp |
| Author | Username |
| Overrides | List of ScenarioOverride |
| Status | Draft, Calculated, Applied, Archived |
| Currency | Currency code |
| Comment | Optional comment |
| Fingerprint | Hash of overrides for change detection |
| CalculationDate | When last calculated |
| Snapshot | EconomicResult snapshot (optional) |

## Scenario Types

| Type | Purpose |
|---|---|
| Base | Reference scenario — the current project configuration |
| Conservative | Higher costs, lower profit (worst case) |
| Optimistic | Lower costs, higher profit (best case) |
| Pessimistic | Similar to conservative |
| Custom | User-defined assumptions |

## Override Types

| TargetType | What it modifies |
|---|---|
| ResourcePrice | Resource.BasePrice |
| ExchangeRate | ExchangeRate.Rate |
| ResourceYield | Resource.Yield |
| ResourceWaste | Resource.Waste |
| ResourceFactor | Resource.AdjustmentFactor |
| EconomicConcept | EconomicConcept.* |
| ConceptRate | EconomicConcept.Value as rate |
| ConceptAmount | EconomicConcept.Value as amount |
| ConceptToggle | EconomicConcept.IsActive |

## Override Fields

| Field | Type | Applies to |
|---|---|---|
| Value | decimal | Concept rate/amount, resource price |
| Rate | decimal | Exchange rate |
| IsActive | bool | Concept toggle |
| Yield | decimal | Resource yield |
| Waste | decimal | Resource waste |
| Factor | decimal | Resource factor |
| BasePrice | decimal | Resource base price |
| ExchangeRateValue | decimal | Exchange rate value |

## Calculation

```csharp
ScenarioEngine.CalculateScenario(scenario, baseInput, ct)
```

Process:
1. Clone base input
2. Apply each override:
   - `EconomicConcept`/`ConceptRate`/`ConceptAmount`/`ConceptToggle`: modify or toggle concept in config
3. Call `EconomicEngine.Calculate` with modified input
4. Return `EconomicResult` (same structure as direct calculation)

## Comparison

```csharp
ScenarioEngine.Compare(baseResult, comparedResult, baseName, comparedName)
```

Returns `ScenarioComparison` with:
- Per-field differences (direct cost, indirect, profit, tax, unit price, total)
- VariationPercent
- ConceptDifferences: per-concept value diff
- ModifiedConcepts: codes of concepts that changed
- TopImpactResources: placeholder for future resource-level impact analysis

## Application

To apply a scenario to working budget:
1. Calculate scenario
2. Present diff to user
3. User confirms with required permissions
4. Audit trail entry created
5. Previous snapshot preserved
6. Approved versions are never modified

## Constraints

- Scenarios never mutate base input (immutable records)
- All overrides reference stable IDs
- No stringly-typed dictionaries
- Audit trail for creation, modification, application
- CancellationToken propagated through all operations
