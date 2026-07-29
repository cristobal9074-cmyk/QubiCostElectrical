using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ScenarioEngineTests
{
    private static EconomicConcept MakeConcept(string code, EconomicCategory cat, CalculationBase calcBase, decimal value, int order, bool affectsPrice = true)
        => new() { Code = code, Name = code, Category = cat, CalculationBase = calcBase, Value = value, Order = order, AffectsFinalPrice = affectsPrice };

    private static EconomicInput MakeBaseInput(decimal directCost = 10000m, params EconomicConcept[] extras)
    {
        var concepts = new List<EconomicConcept>
        {
            MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 14m, 1),
            MakeConcept("PROFIT", EconomicCategory.Profit, CalculationBase.PreviousSubtotal, 18m, 2),
            MakeConcept("VAT", EconomicCategory.Tax, CalculationBase.PreviousSubtotal, 19m, 3)
        };
        concepts.AddRange(extras);
        return new()
        {
            UnitDirectCost = directCost,
            Materials = 5000m,
            Labor = 3000m,
            Equipment = 1000m,
            PartidaQuantity = 1m,
            Config = new EconomicConfig { Concepts = concepts.AsReadOnly() }
        };
    }

    // ────────────────────────── Scenario Calculation ──────────────────────────

    [Fact]
    public void BaseScenario_CalculatesCorrectly()
    {
        var scenario = new Scenario
        {
            Name = "Base",
            Type = ScenarioType.Base,
            Overrides = Array.Empty<ScenarioOverride>()
        };
        var result = ScenarioEngine.CalculateScenario(scenario, MakeBaseInput());
        Assert.True(result.FinalUnitPrice > 0m);
        Assert.Equal(ScenarioStatus.Draft, scenario.Status);
    }

    [Fact]
    public void Scenario_WithOverrideEconomicConcept_ChangesResult()
    {
        var scenario = new Scenario
        {
            Name = "GG Reducido",
            Type = ScenarioType.Conservative,
            Overrides = new[]
            {
                new ScenarioOverride
                {
                    TargetType = OverrideTargetType.ConceptRate,
                    TargetId = "GG",
                    Field = OverrideField.Value,
                    NumericValue = 10m,
                    Source = "Override test"
                }
            }
        };
        var baseInput = MakeBaseInput(10000m);
        var baseResult = ScenarioEngine.CalculateScenario(new Scenario { Name = "Base", Type = ScenarioType.Base }, baseInput);
        var overridden = ScenarioEngine.CalculateScenario(scenario, baseInput);
        Assert.True(overridden.FinalUnitPrice < baseResult.FinalUnitPrice);
    }

    [Fact]
    public void Scenario_OverrideDisablesConcept()
    {
        var scenario = new Scenario
        {
            Name = "Sin IVA",
            Type = ScenarioType.Custom,
            Overrides = new[]
            {
                new ScenarioOverride
                {
                    TargetType = OverrideTargetType.ConceptToggle,
                    TargetId = "VAT",
                    Field = OverrideField.IsActive,
                    BoolValue = false,
                    Source = "Test"
                }
            }
        };
        var baseInput = MakeBaseInput(10000m);
        var baseResult = ScenarioEngine.CalculateScenario(new Scenario { Name = "Base", Type = ScenarioType.Base }, baseInput);
        var withoutVat = ScenarioEngine.CalculateScenario(scenario, baseInput);
        Assert.True(withoutVat.FinalUnitPrice < baseResult.FinalUnitPrice);
        Assert.Equal(0m, withoutVat.TotalTaxes);
    }

    // ────────────────────────── Comparison ──────────────────────────

    [Fact]
    public void Compare_TwoScenarios_ShowsDifference()
    {
        var baseInput = MakeBaseInput(10000m);
        var baseResult = ScenarioEngine.CalculateScenario(
            new Scenario { Name = "Base", Type = ScenarioType.Base }, baseInput);
        var optResult = ScenarioEngine.CalculateScenario(
            new Scenario { Name = "Optimista", Type = ScenarioType.Optimistic }, baseInput);

        var comparison = ScenarioEngine.Compare(baseResult, optResult, "Base", "Optimista");
        Assert.Equal("Base", comparison.BaseScenarioName);
        Assert.Equal("Optimista", comparison.ComparedScenarioName);
        Assert.Equal(0m, comparison.DirectCostDifference); // same input
        Assert.Equal(0m, comparison.TotalDifference);
    }

    [Fact]
    public void Compare_DifferentScenarios_ShowsVariation()
    {
        var baseInput = MakeBaseInput(10000m);
        var scenario = new Scenario
        {
            Name = "Optimista",
            Type = ScenarioType.Optimistic,
            Overrides = new[]
            {
                new ScenarioOverride
                {
                    TargetType = OverrideTargetType.ConceptRate,
                    TargetId = "GG",
                    Field = OverrideField.Value,
                    NumericValue = 10m,
                    Source = "Menos GG"
                }
            }
        };
        var baseResult = ScenarioEngine.CalculateScenario(new Scenario { Name = "Base", Type = ScenarioType.Base }, baseInput);
        var optResult = ScenarioEngine.CalculateScenario(scenario, baseInput);
        var comparison = ScenarioEngine.Compare(baseResult, optResult, "Base", "Optimista");
        Assert.NotEqual(0m, comparison.VariationPercent);
        Assert.NotEqual(0m, comparison.UnitPriceDifference);
    }

    [Fact]
    public void Compare_IdentifiesModifiedConcepts()
    {
        var baseInput = MakeBaseInput(10000m);
        var scenario = new Scenario
        {
            Name = "Profit+",
            Overrides = new[]
            {
                new ScenarioOverride
                {
                    TargetType = OverrideTargetType.ConceptRate,
                    TargetId = "PROFIT",
                    Field = OverrideField.Value,
                    NumericValue = 25m,
                    Source = "Mayor utilidad"
                }
            }
        };
        var baseResult = ScenarioEngine.CalculateScenario(new Scenario { Name = "Base", Type = ScenarioType.Base }, baseInput);
        var changed = ScenarioEngine.CalculateScenario(scenario, baseInput);
        var comparison = ScenarioEngine.Compare(baseResult, changed, "Base", "Profit+");
        Assert.Contains("PROFIT", comparison.ModifiedConcepts);
        Assert.NotEqual(0m, comparison.ProfitDifference);
    }

    // ────────────────────────── Scenario Override Types ──────────────────────────

    [Fact]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScenarioEngine.CalculateScenario(null!, MakeBaseInput()));
        Assert.Throws<ArgumentNullException>(() => ScenarioEngine.CalculateScenario(new Scenario(), null!));
    }

    [Fact]
    public void NullResultsForCompare_Throws()
    {
        var result = ScenarioEngine.CalculateScenario(new Scenario { Name = "B", Type = ScenarioType.Base }, MakeBaseInput());
        Assert.Throws<ArgumentNullException>(() => ScenarioEngine.Compare(null!, result, "", ""));
        Assert.Throws<ArgumentNullException>(() => ScenarioEngine.Compare(result, null!, "", ""));
    }

    // ────────────────────────── Service Layer ──────────────────────────

    [Fact]
    public void Service_Calculate()
    {
        var svc = new EconomicCalculationService();
        var result = svc.Calculate(MakeBaseInput(5000m));
        Assert.True(result.FinalUnitPrice > 5000m);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Service_Validate()
    {
        var svc = new EconomicCalculationService();
        var validation = svc.Validate(EconomicConfig.CreateDefault());
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void Service_CalculateScenario()
    {
        var svc = new EconomicCalculationService();
        var scenario = new Scenario { Name = "Test", Type = ScenarioType.Custom };
        var result = svc.CalculateScenario(scenario, MakeBaseInput(10000m));
        Assert.NotNull(result);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Service_Compare()
    {
        var svc = new EconomicCalculationService();
        var input = MakeBaseInput(10000m);
        var baseR = svc.Calculate(input);
        var compR = svc.Calculate(input);
        var comparison = svc.Compare(baseR, compR, "Base", "Mismo");
        Assert.Equal(0m, comparison.TotalDifference);
    }

    // ────────────────────────── Determinism ──────────────────────────

    [Fact]
    public void Scenario_DeterministicHash()
    {
        var input = MakeBaseInput(10000m);
        var s1 = new Scenario { Name = "S1", Type = ScenarioType.Base };
        var s2 = new Scenario { Name = "S2", Type = ScenarioType.Base };
        var r1 = ScenarioEngine.CalculateScenario(s1, input);
        var r2 = ScenarioEngine.CalculateScenario(s2, input);
        Assert.Equal(r1.DeterministicHash, r2.DeterministicHash);
    }

    // ────────────────────────── Cancellation ──────────────────────────

    [Fact]
    public void Scenario_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ScenarioEngine.CalculateScenario(new Scenario(), MakeBaseInput(), cts.Token));
    }

    // ────────────────────────── Edge Cases ──────────────────────────

    [Fact]
    public void Scenario_EmptyOverrides_ReturnsBase()
    {
        var scenario = new Scenario { Name = "Empty", Type = ScenarioType.Base, Overrides = Array.Empty<ScenarioOverride>() };
        var result = ScenarioEngine.CalculateScenario(scenario, MakeBaseInput(100m));
        Assert.Equal(100m, result.UnitDirectCost);
        Assert.NotEqual(0m, result.FinalUnitPrice);
    }

    [Fact]
    public void Scenario_OverrideNonexistentConcept_NoEffect()
    {
        var scenario = new Scenario
        {
            Name = "BadOverride",
            Overrides = new[]
            {
                new ScenarioOverride
                {
                    TargetType = OverrideTargetType.ConceptRate,
                    TargetId = "NONEXISTENT",
                    Field = OverrideField.Value,
                    NumericValue = 999m
                }
            }
        };
        var baseResult = ScenarioEngine.CalculateScenario(new Scenario { Name = "Base", Type = ScenarioType.Base }, MakeBaseInput(5000m));
        var overridden = ScenarioEngine.CalculateScenario(scenario, MakeBaseInput(5000m));
        Assert.Equal(baseResult.FinalUnitPrice, overridden.FinalUnitPrice);
    }
}
