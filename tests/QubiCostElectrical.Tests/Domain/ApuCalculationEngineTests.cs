using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ApuCalculationEngineTests
{
    private static Resource MakeResource(string id, string code, ResourceType type, decimal price, string currency = "CLP", decimal factor = 1m)
        => new() { Id = id, Code = code, Description = $"Resource {code}", Type = type, Unit = "UN", BasePrice = price, Currency = currency, AdjustmentFactor = factor };

    private static ApuComponent MakeComponent(string refId, ApuComponentType type = ApuComponentType.Resource, decimal yield = 1m, decimal waste = 0m, decimal factor = 1m, string? desc = null)
        => new() { ReferenceId = refId, ComponentType = type, Yield = yield, WastePercent = waste, AdjustmentFactor = factor, Description = desc ?? $"Comp {refId}" };

    private static ApuDefinition MakeApu(string id, string code, List<ApuComponent>? comps = null)
        => new() { Id = id, Code = code, Description = $"APU {code}", Unit = "UN", Components = comps ?? new() };

    // ────────────────────────── Basic Calculation ──────────────────────────

    [Fact]
    public void EmptyApu_ReturnsZero()
    {
        var apu = MakeApu("a1", "EMPTY");
        var input = new ApuCalculationInput();
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(0m, result.TotalDirectCost);
        Assert.Equal(0m, result.TotalUnitPrice);
        Assert.Empty(result.Components);
    }

    [Fact]
    public void SingleResource_ComputesDirectCost()
    {
        var res = MakeResource("r1", "CABLE", ResourceType.Material, 1000m);
        var apu = MakeApu("a1", "APU-1", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { res }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(1000m, result.TotalDirectCost);
        Assert.Single(result.Components);
        Assert.Equal(1000m, result.CategoryBreakdown.Materials);
    }

    [Fact]
    public void MultipleResources_SumCorrectly()
    {
        var r1 = MakeResource("r1", "MAT-A", ResourceType.Material, 100m);
        var r2 = MakeResource("r2", "MAT-B", ResourceType.Material, 200m);
        var apu = MakeApu("a1", "APU-1", new() { MakeComponent("r1"), MakeComponent("r2") });
        var input = new ApuCalculationInput { Resources = new[] { r1, r2 }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(300m, result.TotalDirectCost);
    }

    [Fact]
    public void SortOrder_ProcessesComponentsInOrder()
    {
        var r1 = MakeResource("r1", "A", ResourceType.Material, 100m);
        var r2 = MakeResource("r2", "B", ResourceType.Material, 200m);
        var c1 = MakeComponent("r1") with { SortOrder = 2 };
        var c2 = MakeComponent("r2") with { SortOrder = 1 };
        var apu = MakeApu("a1", "APU", new() { c1, c2 });
        var input = new ApuCalculationInput { Resources = new[] { r1, r2 }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal("r2", result.Components[0].ReferenceId);
        Assert.Equal("r1", result.Components[1].ReferenceId);
    }

    // ────────────────────────── Resource Lookup ──────────────────────────

    [Fact]
    public void ResourceNotFound_ReturnsZeroCostWithMessage()
    {
        var apu = MakeApu("a1", "APU", new() { MakeComponent("missing") });
        var input = new ApuCalculationInput();
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(0m, result.TotalDirectCost);
        Assert.Contains("not found", result.Components[0].Explanation);
    }

    [Fact]
    public void ResourceWithYield_MultipliesPrice()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 500m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", yield: 3m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(1500m, result.TotalDirectCost);
    }

    [Fact]
    public void ResourceWithWastePercent_IncreasesCost()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", waste: 15m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(115m, result.TotalDirectCost);
    }

    [Fact]
    public void ResourceWithAdjustmentFactor_ScalesPrice()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m, factor: 1.1m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", factor: 2m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(220m, result.TotalDirectCost);
    }

    [Fact]
    public void ResourceWithAllModifiers_ComputesCorrectly()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 200m, factor: 0.5m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", yield: 2m, waste: 10m, factor: 1.5m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);

        // effective price = 200 * 0.5 = 100
        // effective qty = 2 * (1 + 10/100) * 1.5 = 2 * 1.1 * 1.5 = 3.3
        // subtotal = 100 * 3.3 = 330
        Assert.Equal(330m, result.TotalDirectCost);
        Assert.Equal(100m, result.Components[0].BasePrice);
        Assert.Equal(3.3m, result.Components[0].EffectiveQuantity);
    }

    [Fact]
    public void ResourceWithZeroYield_HandlesGracefully()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", yield: 0m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(0m, result.TotalDirectCost);
    }

    // ────────────────────────── Resource Type Categorization ──────────────────────────

    [Theory]
    [InlineData(ResourceType.Material, "Materials")]
    [InlineData(ResourceType.Labor, "Labor")]
    [InlineData(ResourceType.Equipment, "Equipment")]
    [InlineData(ResourceType.Tool, "Tools")]
    [InlineData(ResourceType.Subcontract, "Subcontract")]
    [InlineData(ResourceType.Transport, "Transport")]
    [InlineData(ResourceType.Auxiliary, "AuxiliaryApus")]
    public void ResourceType_CategorizedCorrectly(ResourceType type, string category)
    {
        var r = MakeResource("r1", "RES", type, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);

        var breakdown = result.CategoryBreakdown;
        var value = category switch
        {
            "Materials" => breakdown.Materials,
            "Labor" => breakdown.Labor,
            "Equipment" => breakdown.Equipment,
            "Tools" => breakdown.Tools,
            "Subcontract" => breakdown.Subcontract,
            "Transport" => breakdown.Transport,
            "AuxiliaryApus" => breakdown.AuxiliaryApus,
            _ => 0m
        };
        Assert.Equal(100m, value);
    }

    [Fact]
    public void MultipleCategories_TrackedSeparately()
    {
        var r1 = MakeResource("r1", "MAT", ResourceType.Material, 100m);
        var r2 = MakeResource("r2", "MO", ResourceType.Labor, 200m);
        var r3 = MakeResource("r3", "EQ", ResourceType.Equipment, 300m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1"), MakeComponent("r2"), MakeComponent("r3") });
        var input = new ApuCalculationInput { Resources = new[] { r1, r2, r3 }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(100m, result.CategoryBreakdown.Materials);
        Assert.Equal(200m, result.CategoryBreakdown.Labor);
        Assert.Equal(300m, result.CategoryBreakdown.Equipment);
        Assert.Equal(600m, result.TotalDirectCost);
    }

    // ────────────────────────── Auxiliary APU ──────────────────────────

    [Fact]
    public void AuxiliaryApu_IncludesChildCost()
    {
        var r1 = MakeResource("r1", "CABLE", ResourceType.Material, 500m);
        var child = MakeApu("child", "AUX-1", new() { MakeComponent("r1") });
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("child", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { child, parent } };
        var result = ApuCalculationEngine.Calculate(parent, input);
        Assert.Equal(500m, result.TotalDirectCost);
        Assert.Single(result.Components);
        Assert.Equal(ApuComponentType.AuxiliaryApu, result.Components[0].ComponentType);
    }

    [Fact]
    public void AuxiliaryApu_WithYieldAndWaste()
    {
        var r1 = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var child = MakeApu("child", "AUX-1", new() { MakeComponent("r1") });
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("child", ApuComponentType.AuxiliaryApu, yield: 2m, waste: 10m) });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { child, parent } };
        var result = ApuCalculationEngine.Calculate(parent, input);
        Assert.Equal(220m, result.TotalDirectCost); // 100 * 2 * 1.1
    }

    [Fact]
    public void AuxiliaryApu_NestedTwoLevels()
    {
        var r1 = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var l1 = MakeApu("l1", "L1", new() { MakeComponent("r1") });
        var l2 = MakeApu("l2", "L2", new() { MakeComponent("l1", ApuComponentType.AuxiliaryApu) });
        var l3 = MakeApu("l3", "L3", new() { MakeComponent("l2", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput
        {
            Resources = new[] { r1 },
            ApuDefinitions = new[] { l1, l2, l3 }
        };
        var result = ApuCalculationEngine.Calculate(l3, input);
        Assert.Equal(100m, result.TotalDirectCost);
        Assert.Equal(1, result.Trace.AuxiliaryApusResolved);
        Assert.NotNull(result.Components[0].AuxiliaryComponents);
        Assert.Single(result.Components[0].AuxiliaryComponents!);
    }

    [Fact]
    public void AuxiliaryApu_MissingDefinition_ReportsError()
    {
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("missing", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { ApuDefinitions = new[] { parent } };
        var result = ApuCalculationEngine.Calculate(parent, input);
        Assert.Equal(0m, result.TotalDirectCost);
        Assert.Contains("not found", result.Components[0].Explanation);
    }

    [Fact]
    public void AuxiliaryApu_IncludesSubComponentsInResult()
    {
        var r1 = MakeResource("r1", "CABLE", ResourceType.Material, 300m);
        var child = MakeApu("child", "AUX-1", new() { MakeComponent("r1") });
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("child", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { child, parent } };
        var result = ApuCalculationEngine.Calculate(parent, input);
        Assert.NotNull(result.Components[0].AuxiliaryComponents);
        Assert.Single(result.Components[0].AuxiliaryComponents!);
        Assert.Equal("r1", result.Components[0].AuxiliaryComponents![0].ReferenceId);
    }

    [Fact]
    public void AuxiliaryApu_CategoryIncludesSubtotals()
    {
        var r1 = MakeResource("r1", "MAT", ResourceType.Material, 200m);
        var child = MakeApu("child", "AUX", new() { MakeComponent("r1") });
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("child", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { child, parent } };
        var result = ApuCalculationEngine.Calculate(parent, input);

        Assert.Equal(200m, result.CategoryBreakdown.AuxiliaryApus);
        Assert.Equal(0m, result.CategoryBreakdown.Materials);
    }

    // ────────────────────────── Cycle Detection ──────────────────────────

    [Fact]
    public void SelfReferencingApu_DetectsCycle()
    {
        var apu = MakeApu("a1", "SELF", new() { MakeComponent("a1", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(0m, result.TotalDirectCost);
        Assert.Contains("CYCLE", result.Trace.Steps[0]);
    }

    [Fact]
    public void MutualReference_DetectsCycle()
    {
        var a1 = MakeApu("a1", "A", new() { MakeComponent("a2", ApuComponentType.AuxiliaryApu) });
        var a2 = MakeApu("a2", "B", new() { MakeComponent("a1", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { ApuDefinitions = new[] { a1, a2 } };
        var result = ApuCalculationEngine.Calculate(a1, input);
        Assert.Equal(0m, result.TotalDirectCost);
        Assert.Contains("CYCLE", result.Trace.Steps[0]);
    }

    [Fact]
    public void ThreeApuCycle_Detected()
    {
        var a1 = MakeApu("a1", "A", new() { MakeComponent("a2", ApuComponentType.AuxiliaryApu) });
        var a2 = MakeApu("a2", "B", new() { MakeComponent("a3", ApuComponentType.AuxiliaryApu) });
        var a3 = MakeApu("a3", "C", new() { MakeComponent("a1", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { ApuDefinitions = new[] { a1, a2, a3 } };
        var result = ApuCalculationEngine.Calculate(a1, input);
        Assert.Contains("CYCLE", result.Trace.Steps[0]);
    }

    [Fact]
    public void ChainNoCycle_CalculatesCorrectly()
    {
        var r1 = MakeResource("r1", "BASE", ResourceType.Material, 100m);
        var l1 = MakeApu("l1", "L1", new() { MakeComponent("r1") });
        var l2 = MakeApu("l2", "L2", new() { MakeComponent("l1", ApuComponentType.AuxiliaryApu) });
        var l3 = MakeApu("l3", "L3", new() { MakeComponent("l2", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { l1, l2, l3 } };
        var result = ApuCalculationEngine.Calculate(l3, input);
        Assert.Equal(100m, result.TotalDirectCost);
    }

    [Fact]
    public void ValidateNoCycles_ReturnsAffectedApUs()
    {
        var a1 = MakeApu("a1", "A", new() { MakeComponent("a2", ApuComponentType.AuxiliaryApu) });
        var a2 = MakeApu("a2", "B", new() { MakeComponent("a1", ApuComponentType.AuxiliaryApu) });
        var input = new ApuCalculationInput { ApuDefinitions = new[] { a1, a2 } };
        var validation = ApuCalculationEngine.ValidateNoCycles(input.ApuDefinitions);
        Assert.True(validation.HasCycles);
        Assert.NotEmpty(validation.AffectedApuIds);
    }

    [Fact]
    public void ValidateNoCycles_CleanGraph_ReturnsNoCycles()
    {
        var a1 = MakeApu("a1", "A");
        var a2 = MakeApu("a2", "B");
        var validation = ApuCalculationEngine.ValidateNoCycles(new[] { a1, a2 });
        Assert.False(validation.HasCycles);
        Assert.Empty(validation.AffectedApuIds);
    }

    // ────────────────────────── Cycle Path Detection ──────────────────────────

    [Fact]
    public void CycleDetector_FindsCyclePath()
    {
        var a1 = MakeApu("a1", "A", new() { MakeComponent("a2", ApuComponentType.AuxiliaryApu) });
        var a2 = MakeApu("a2", "B", new() { MakeComponent("a1", ApuComponentType.AuxiliaryApu) });
        var detector = new ApuCycleDetector();
        var path = detector.FindCyclePath("a1", a1.Components, new[] { a1, a2 });
        Assert.NotEmpty(path);
        Assert.Contains("a1", path);
        Assert.Contains("a2", path);
    }

    [Fact]
    public void CycleDetector_NoCycle_ReturnsEmptyPath()
    {
        var a1 = MakeApu("a1", "A");
        var a2 = MakeApu("a2", "B");
        var detector = new ApuCycleDetector();
        var path = detector.FindCyclePath("a1", a1.Components, new[] { a1, a2 });
        Assert.Empty(path);
    }

    // ────────────────────────── Precision ──────────────────────────

    [Fact]
    public void Precision_RoundsCantidadTo4Decimals()
    {
        var result = PrecisionPolicy.RoundCantidad(3.14159265m);
        Assert.Equal(3.1416m, result);
    }

    [Fact]
    public void Precision_RoundsPrecioUnitarioTo4Decimals()
    {
        var result = PrecisionPolicy.RoundPrecioUnitario(12.3456789m);
        Assert.Equal(12.3457m, result);
    }

    [Fact]
    public void Precision_RoundsImporteTo2Decimals()
    {
        var result = PrecisionPolicy.RoundImporte(123.4567m);
        Assert.Equal(123.46m, result);
    }

    [Fact]
    public void Precision_BankersRounding_MidpointToEven()
    {
        Assert.Equal(2.46m, PrecisionPolicy.RoundImporte(2.455m));
        Assert.Equal(2.44m, PrecisionPolicy.RoundImporte(2.445m));
    }

    [Fact]
    public void Precision_AfterMultiplicationChain()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 123.4567m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", yield: 3.3333m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);

        // base price = round to 4 = 123.4567
        // yield = round to 4 = 3.3333
        // effective qty = round to 4 = 3.3333
        // subtotal = round to 2 = 123.4567 * 3.3333 = 411.5171 -> 411.52
        Assert.Equal(411.52m, result.TotalDirectCost);
    }

    [Fact]
    public void Precision_ManyComponents()
    {
        var res = MakeResource("r1", "R1", ResourceType.Material, 100.1234m);
        var comps = Enumerable.Range(1, 20).Select(i => MakeComponent("r1", yield: 1.2345m)).ToList();
        var apu = MakeApu("a1", "APU", comps);
        var input = new ApuCalculationInput { Resources = new[] { res }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);

        // each component: 100.1234 * 1.2345 = 123.60... -> 123.60
        // 20 components: 2472.00
        Assert.Equal(2472.00m, result.TotalDirectCost);
    }

    // ────────────────────────── Currency Conversion ──────────────────────────

    [Fact]
    public void SameCurrency_NoConversionApplied()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m, "CLP");
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(100m, result.TotalDirectCost);
        Assert.False(result.Trace.CurrencyConversionApplied);
    }

    [Fact]
    public void DifferentCurrency_WithRate_Converts()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m, "USD");
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 950m, Date = DateTime.UtcNow };
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu }, ExchangeRates = new[] { rate } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(95000m, result.TotalDirectCost);
    }

    [Fact]
    public void MissingExchangeRate_ReturnsOriginalPrice()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m, "USD");
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(100m, result.TotalDirectCost);
    }

    [Fact]
    public void RateWithDifferentDates_UsesLatest()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m, "USD");
        var rateOld = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 800m, Date = DateTime.UtcNow.AddDays(-10) };
        var rateNew = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 950m, Date = DateTime.UtcNow };
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu }, ExchangeRates = new[] { rateOld, rateNew } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(95000m, result.TotalDirectCost);
    }

    [Fact]
    public void LaborRateOverride_Applied()
    {
        var r = MakeResource("r1", "MO-1", ResourceType.Labor, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput
        {
            Resources = new[] { r },
            ApuDefinitions = new[] { apu },
            OverrideLaborRate = 150m
        };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(150m, result.TotalDirectCost);
    }

    // ────────────────────────── Cancellation ──────────────────────────

    [Fact]
    public void CancelledToken_Throws()
    {
        var apu = MakeApu("a1", "APU");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ApuCalculationEngine.Calculate(apu, new ApuCalculationInput(), cts.Token));
    }

    [Fact]
    public void CancelWithResources_Throws_WhenCancelled()
    {
        var r = MakeResource("r1", "R1", ResourceType.Material, 1000m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        Assert.Throws<OperationCanceledException>(() =>
            ApuCalculationEngine.Calculate(apu, input, cts.Token));
    }

    [Fact]
    public void CancelWithAuxiliaryApu_Throws_WhenCancelled()
    {
        var r = MakeResource("r1", "R1", ResourceType.Material, 100m);
        var child = MakeApu("child", "AUX", new() { MakeComponent("r1") });
        var parent = MakeApu("parent", "MAIN", new() { MakeComponent("child", ApuComponentType.AuxiliaryApu) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var input = new ApuCalculationInput
        {
            Resources = new[] { r },
            ApuDefinitions = new[] { child, parent }
        };
        Assert.Throws<OperationCanceledException>(() =>
            ApuCalculationEngine.Calculate(parent, input, cts.Token));
    }

    // ────────────────────────── Service Layer ──────────────────────────

    [Fact]
    public void Service_CalculateById()
    {
        var r1 = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var apu = MakeApu("a1", "APU-1", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r1 }, ApuDefinitions = new[] { apu } };
        var service = new ApuCalculationService();
        var result = service.Calculate("a1", input);
        Assert.Equal(100m, result.TotalDirectCost);
    }

    [Fact]
    public void Service_CalculateAll()
    {
        var r1 = MakeResource("r1", "A", ResourceType.Material, 100m);
        var r2 = MakeResource("r2", "B", ResourceType.Material, 200m);
        var a1 = MakeApu("a1", "APU-1", new() { MakeComponent("r1") });
        var a2 = MakeApu("a2", "APU-2", new() { MakeComponent("r2") });
        var input = new ApuCalculationInput { Resources = new[] { r1, r2 }, ApuDefinitions = new[] { a1, a2 } };
        var service = new ApuCalculationService();
        var results = service.CalculateAll(input);
        Assert.Equal(2, results.Count);
        Assert.Equal(100m, results[0].TotalDirectCost);
        Assert.Equal(200m, results[1].TotalDirectCost);
    }

    [Fact]
    public void Service_CalculateMany()
    {
        var r1 = MakeResource("r1", "A", ResourceType.Material, 100m);
        var r2 = MakeResource("r2", "B", ResourceType.Material, 200m);
        var a1 = MakeApu("a1", "APU-1", new() { MakeComponent("r1") });
        var a2 = MakeApu("a2", "APU-2", new() { MakeComponent("r2") });
        var input = new ApuCalculationInput { Resources = new[] { r1, r2 }, ApuDefinitions = new[] { a1, a2 } };
        var service = new ApuCalculationService();
        var results = service.CalculateMany(new[] { "a2", "a1" }, input);
        Assert.Equal(2, results.Count);
        Assert.Equal(200m, results[0].TotalDirectCost);
        Assert.Equal(100m, results[1].TotalDirectCost);
    }

    [Fact]
    public void Service_ValidateAll()
    {
        var a1 = MakeApu("a1", "A", new() { MakeComponent("a2", ApuComponentType.AuxiliaryApu) });
        var a2 = MakeApu("a2", "B");
        var input = new ApuCalculationInput { ApuDefinitions = new[] { a1, a2 } };
        var service = new ApuCalculationService();
        var validation = service.ValidateAll(input);
        Assert.False(validation.HasCycles);
    }

    [Fact]
    public void Service_MissingId_Throws()
    {
        var service = new ApuCalculationService();
        var input = new ApuCalculationInput();
        Assert.Throws<InvalidOperationException>(() => service.Calculate("missing", input));
    }

    [Fact]
    public void Service_NullInput_Throws()
    {
        var service = new ApuCalculationService();
        Assert.Throws<ArgumentNullException>(() => service.Calculate("a1", null!));
        Assert.Throws<ArgumentNullException>(() => service.CalculateAll(null!));
    }

    // ────────────────────────── Edge Cases ──────────────────────────

    [Fact]
    public void NullApu_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ApuCalculationEngine.Calculate(null!, new ApuCalculationInput()));
    }

    [Fact]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ApuCalculationEngine.Calculate(MakeApu("a1", "APU"), null!));
    }

    [Fact]
    public void EmptyDefinitions_ValidateOk()
    {
        var validation = ApuCalculationEngine.ValidateNoCycles(Array.Empty<ApuDefinition>());
        Assert.False(validation.HasCycles);
    }

    [Fact]
    public void SingleComponentZeroPrice_ReturnsZero()
    {
        var r = MakeResource("r1", "FREE", ResourceType.Material, 0m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(0m, result.TotalDirectCost);
    }

    [Fact]
    public void UnitPriceComputesCorrectly()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 500m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") }) with { Quantity = 4m };
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(500m, result.TotalDirectCost);
        Assert.Equal(125m, result.TotalUnitPrice); // 500 / 4
    }

    [Fact]
    public void Explanation_ContainsFormula()
    {
        var r = MakeResource("r1", "CABLE", ResourceType.Material, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1", yield: 2m, waste: 10m, factor: 1.5m) });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Contains("CABLE", result.Components[0].Explanation);
        Assert.Contains("yield=", result.Components[0].Explanation);
        Assert.Contains("waste=", result.Components[0].Explanation);
        Assert.Contains("factor=", result.Components[0].Explanation);
        Assert.Contains("330", result.Components[0].Explanation);
    }

    [Fact]
    public void Trace_ContainsComponentCount()
    {
        var r = MakeResource("r1", "A", ResourceType.Material, 100m);
        var apu = MakeApu("a1", "APU", new() { MakeComponent("r1") });
        var input = new ApuCalculationInput { Resources = new[] { r }, ApuDefinitions = new[] { apu } };
        var result = ApuCalculationEngine.Calculate(apu, input);
        Assert.Equal(1, result.Trace.ComponentsProcessed);
    }

    // ────────────────────────── UI Helper Formatting ──────────────────────────

    [Fact]
    public void CostCategoryBreakdown_Total_SumAll()
    {
        var b = new CostCategoryBreakdown
        {
            Materials = 100m,
            Labor = 200m,
            Equipment = 50m,
            Tools = 10m,
            Subcontract = 300m,
            Transport = 40m,
            AuxiliaryApus = 100m
        };
        Assert.Equal(800m, b.Total);
    }

    [Fact]
    public void Input_IsImmutable()
    {
        var input = new ApuCalculationInput
        {
            ApuDefinitions = new[] { MakeApu("a1", "APU") },
            Resources = Array.Empty<Resource>()
        };
        Assert.Single(input.ApuDefinitions);
        Assert.Empty(input.Resources);
    }

    [Theory]
    [InlineData("", 100, ApuComponentType.Resource)]
    [InlineData("", 100, ApuComponentType.AuxiliaryApu)]
    public void ComponentRecord_SupportsWithExpressions(string refId, decimal yield, ApuComponentType type)
    {
        var comp = MakeComponent("r1");
        var modified = comp with { ReferenceId = refId, Yield = yield, ComponentType = type };
        Assert.Equal(refId, modified.ReferenceId);
        Assert.Equal(yield, modified.Yield);
        Assert.Equal(type, modified.ComponentType);
    }
}
