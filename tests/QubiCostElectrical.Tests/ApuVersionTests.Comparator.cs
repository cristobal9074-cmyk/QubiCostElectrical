using System;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ApuVersionComparatorTests
{
    private readonly ApuVersionComparator _sut = new();

    [Fact]
    public void Compare_identical_versions_returns_no_changes()
    {
        var id = Guid.NewGuid().ToString("N");
        var v = CreateDefaultVersion(id);
        var summary = _sut.Compare(v, v);
        Assert.True(summary.IsIdentical);
        Assert.Equal(0, summary.TotalChanges);
    }

    [Fact]
    public void Compare_versions_with_different_component_added()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            Components = left.Components.Concat(new[]
            {
                new ApuVersionComponent { ReferenceId = "C003", Description = "New", Yield = 2m }
            }).ToList().AsReadOnly()
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.AddedCount);
        Assert.Equal(1, summary.TotalChanges);
        Assert.Contains(summary.Changes, c => c.Identifier == "C003" && c.Type == DiffType.Added);
    }

    [Fact]
    public void Compare_versions_with_component_removed_is_breaking()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            Components = left.Components.Take(1).ToList().AsReadOnly()
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.RemovedCount);
        Assert.Equal(1, summary.BreakingChanges);
        Assert.True(summary.Changes.Any(c => c.IsBreaking));
    }

    [Fact]
    public void Compare_different_yield_detected_as_modified()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            Components = left.Components.Select(c =>
                c.ReferenceId == "C001" ? c with { Yield = 5m } : c
            ).ToList().AsReadOnly()
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.ModifiedCount);
        Assert.Contains(summary.Changes, c => c.Identifier == "C001" && c.Property == "Yield" && c.OldNumericValue == 1m && c.NewNumericValue == 5m);
    }

    [Fact]
    public void Compare_different_final_price_detected()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            CalculatedFinalPrice = 99999m
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.TotalChanges);
        Assert.Contains(summary.Changes, c => c.Dimension == ComparisonDimension.FinalPrice);
    }

    [Fact]
    public void Compare_entirely_different_versions_reports_all_changes()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            Components = Array.Empty<ApuVersionComponent>().AsReadOnly(),
            ResourceRefs = Array.Empty<ApuVersionResourceRef>().AsReadOnly(),
            EconomicConcepts = Array.Empty<ApuVersionEconomicConcept>().AsReadOnly(),
            Scenarios = Array.Empty<ApuVersionScenario>().AsReadOnly(),
            CalculatedDirectCost = 0,
            CalculatedFinalPrice = 0
        };

        var summary = _sut.Compare(left, right);
        Assert.True(summary.TotalChanges > 0);
        Assert.Equal(2, summary.RemovedCount);
    }

    [Fact]
    public void Compare_different_apu_id_throws()
    {
        var left = CreateDefaultVersion("apu-001");
        var right = CreateDefaultVersion("apu-002");
        Assert.Throws<InvalidOperationException>(() => _sut.Compare(left, right));
    }

    [Fact]
    public void Compare_summary_has_correct_metadata()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with { VersionId = Guid.NewGuid().ToString("N"), VersionNumber = 2 };

        var summary = _sut.Compare(left, right);
        Assert.Equal(id, summary.ApuId);
        Assert.Equal(left.VersionId, summary.LeftVersionId);
        Assert.Equal(right.VersionId, summary.RightVersionId);
        Assert.Equal(1, summary.LeftVersionNumber);
        Assert.Equal(2, summary.RightVersionNumber);
        Assert.True(summary.ComparedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Compare_resource_price_change_detected()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            ResourceRefs = left.ResourceRefs.Select(r =>
                r.ResourceId == "R001" ? r with { Price = 9999m } : r
            ).ToList().AsReadOnly()
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.ModifiedCount);
        Assert.Contains(summary.Changes, c => c.Dimension == ComparisonDimension.Resources && c.Identifier == "R001");
    }

    [Fact]
    public void Compare_economic_concept_value_change_detected()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            EconomicConcepts = left.EconomicConcepts.Select(c =>
                c.Code == "EC001" ? c with { Value = 50m } : c
            ).ToList().AsReadOnly()
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(1, summary.ModifiedCount);
        Assert.Contains(summary.Changes, c => c.Dimension == ComparisonDimension.EconomicConcepts && c.Identifier == "EC001");
    }

    [Fact]
    public void Compare_direct_cost_delta_calculated_correctly()
    {
        var id = Guid.NewGuid().ToString("N");
        var left = CreateDefaultVersion(id);
        var right = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            CalculatedDirectCost = 5000m,
            CalculatedFinalPrice = 7500m
        };

        var summary = _sut.Compare(left, right);
        Assert.Equal(2500m, summary.DirectCostDelta);
        Assert.Equal(2500m, summary.PriceDelta);
    }

    private static ApuVersion CreateDefaultVersion(string apuId)
    {
        return new ApuVersion
        {
            ApuId = apuId,
            VersionId = Guid.NewGuid().ToString("N"),
            Code = "APU-001",
            Name = "Test APU",
            VersionNumber = 1,
            CalculatedDirectCost = 2500m,
            CalculatedFinalPrice = 5000m,
            CalculatedCurrency = "CLP",
            Components = new[]
            {
                new ApuVersionComponent { ReferenceId = "C001", Description = "Material A", Yield = 1m, ComponentType = "Material" },
                new ApuVersionComponent { ReferenceId = "C002", Description = "Labor B", Yield = 2m, ComponentType = "Labor" }
            }.AsReadOnly(),
            ResourceRefs = new[]
            {
                new ApuVersionResourceRef { ResourceId = "R001", Code = "MAT-001", Description = "Concrete", Price = 15000m, Currency = "CLP" }
            }.AsReadOnly(),
            EconomicConcepts = new[]
            {
                new ApuVersionEconomicConcept { Code = "EC001", Category = "Indirect", Value = 15m }
            }.AsReadOnly(),
            Scenarios = new[]
            {
                new ApuVersionScenario { ScenarioId = "S001", Name = "Optimistic", OverrideCount = 2 }
            }.AsReadOnly()
        };
    }
}
