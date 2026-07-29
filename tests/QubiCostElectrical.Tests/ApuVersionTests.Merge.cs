using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ApuVersionMergeTests
{
    private readonly ApuVersionMerger _sut = new();

    [Fact]
    public void Merge_identical_versions_produces_no_conflicts()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var result = _sut.Merge(baseV, baseV, baseV, "author", "no changes");
        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        Assert.Empty(result.AppliedChanges);
    }

    [Fact]
    public void Merge_with_right_change_only_uses_right()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var right = baseV with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            CalculatedFinalPrice = 9999m
        };

        var result = _sut.Merge(baseV, right, baseV, "author", "right change");
        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        Assert.Equal(9999m, result.MergedVersion.CalculatedFinalPrice);
    }

    [Fact]
    public void Merge_with_left_change_only_uses_left()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 2,
            CalculatedDirectCost = 7777m
        };

        var result = _sut.Merge(left, baseV, baseV, "author", "left change");
        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        Assert.Equal(7777m, result.MergedVersion.CalculatedDirectCost);
    }

    [Fact]
    public void Merge_with_both_same_change_no_conflict()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var both = baseV with { CalculatedFinalPrice = 8888m };
        var result = _sut.Merge(both, both, baseV, "author", "both same");
        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        Assert.Equal(8888m, result.MergedVersion.CalculatedFinalPrice);
    }

    [Fact]
    public void Merge_with_divergent_changes_creates_conflict()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with { CalculatedFinalPrice = 1500m };
        var right = baseV with { CalculatedFinalPrice = 2500m };
        var base2 = baseV with { CalculatedFinalPrice = 2000m };

        var result = _sut.Merge(left, right, base2, "author", "divergent");
        Assert.False(result.Success);
        Assert.NotEmpty(result.Conflicts);
    }

    [Fact]
    public void Merge_with_auto_resolution_resolves_conflict()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with { CalculatedFinalPrice = 1000m };
        var right = baseV with { CalculatedFinalPrice = 2000m };

        var resolutions = new Dictionary<string, MergeConflictResolution>
        {
            ["Numeric:CalculatedFinalPrice"] = MergeConflictResolution.UseLeft
        };

        var result = _sut.Merge(left, right, baseV, "author", "auto-resolved", resolutions);
        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        Assert.Equal(1000m, result.MergedVersion.CalculatedFinalPrice);
    }

    [Fact]
    public void Merge_added_component_appears_in_merged()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var right = baseV with
        {
            Components = baseV.Components.Concat(new[]
            {
                new ApuVersionComponent { ReferenceId = "C-NEW", Description = "Added", Yield = 3m }
            }).ToList().AsReadOnly()
        };

        var result = _sut.Merge(baseV, right, baseV, "author", "add component");
        Assert.True(result.Success);
        Assert.Contains(result.MergedVersion.Components, c => c.ReferenceId == "C-NEW");
    }

    [Fact]
    public void Merge_different_components_both_sides_three_way()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with
        {
            Components = baseV.Components.Select(c =>
                c.ReferenceId == "C001" ? c with { Yield = 10m } : c
            ).ToList().AsReadOnly()
        };
        var right = baseV with
        {
            Components = baseV.Components.Select(c =>
                c.ReferenceId == "C002" ? c with { WastePercent = 5m } : c
            ).ToList().AsReadOnly()
        };

        var result = _sut.Merge(left, right, baseV, "author", "both sides");
        Assert.True(result.Success);
        var merged = result.MergedVersion;
        Assert.Equal(10m, merged.Components.First(c => c.ReferenceId == "C001").Yield);
        Assert.Equal(5m, merged.Components.First(c => c.ReferenceId == "C002").WastePercent);
    }

    [Fact]
    public void Merge_conflicting_component_yield_detected()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with
        {
            Components = baseV.Components.Select(c =>
                c.ReferenceId == "C001" ? c with { Yield = 10m } : c
            ).ToList().AsReadOnly()
        };
        var right = baseV with
        {
            Components = baseV.Components.Select(c =>
                c.ReferenceId == "C001" ? c with { Yield = 20m } : c
            ).ToList().AsReadOnly()
        };

        var result = _sut.Merge(left, right, baseV, "author", "conflict");
        Assert.NotEmpty(result.Conflicts);
        Assert.Contains(result.Conflicts, c => c.Identifier == "C001" && c.Property == "Yield");
    }

    [Fact]
    public void Merge_removed_component_in_left_creates_conflict()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with
        {
            Components = baseV.Components.Where(c => c.ReferenceId != "C001").ToList().AsReadOnly()
        };

        var result = _sut.Merge(left, baseV, baseV, "author", "remove");
        Assert.NotEmpty(result.Conflicts);
        Assert.Contains(result.Conflicts, c => c.Identifier == "C001");
    }

    [Fact]
    public void Merge_resource_with_different_prices_creates_conflict()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var left = baseV with
        {
            ResourceRefs = baseV.ResourceRefs.Select(r =>
                r.ResourceId == "R001" ? r with { Price = 100m } : r
            ).ToList().AsReadOnly()
        };
        var right = baseV with
        {
            ResourceRefs = baseV.ResourceRefs.Select(r =>
                r.ResourceId == "R001" ? r with { Price = 200m } : r
            ).ToList().AsReadOnly()
        };

        var result = _sut.Merge(left, right, baseV, "author", "price conflict");
        Assert.NotEmpty(result.Conflicts);
        Assert.Contains(result.Conflicts, c => c.Identifier == "R001" && c.Property == "Price");
    }

    [Fact]
    public void Merge_creates_new_version_with_incremented_number()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 3);
        var result = _sut.Merge(baseV, baseV, baseV, "author", "test");
        Assert.Equal(4, result.MergedVersion.VersionNumber);
    }

    [Fact]
    public void Merge_merges_scenarios_from_both_branches()
    {
        var apuId = Guid.NewGuid().ToString("N");
        var baseV = CreateVersion(apuId, 1);
        var right = baseV with
        {
            Scenarios = baseV.Scenarios.Concat(new[]
            {
                new ApuVersionScenario { ScenarioId = "S-NEW", Name = "New Scenario" }
            }).ToList().AsReadOnly()
        };

        var result = _sut.Merge(baseV, right, baseV, "author", "add scenario");
        Assert.Equal(2, result.MergedVersion.Scenarios.Count);
        Assert.Contains(result.MergedVersion.Scenarios, s => s.ScenarioId == "S-NEW");
    }

    private static ApuVersion CreateVersion(string apuId, int versionNumber)
    {
        return new ApuVersion
        {
            ApuId = apuId,
            VersionId = Guid.NewGuid().ToString("N"),
            Code = $"APU-{versionNumber}",
            Name = $"Version {versionNumber}",
            VersionNumber = versionNumber,
            CalculatedDirectCost = 1000m * versionNumber,
            CalculatedFinalPrice = 2000m * versionNumber,
            Components = new[]
            {
                new ApuVersionComponent { ReferenceId = "C001", Description = "Material", Yield = 2m },
                new ApuVersionComponent { ReferenceId = "C002", Description = "Labor", Yield = 1m }
            }.AsReadOnly(),
            ResourceRefs = new[]
            {
                new ApuVersionResourceRef { ResourceId = "R001", Code = "MAT-A", Description = "Cemento", Price = 10000m, Currency = "CLP" }
            }.AsReadOnly(),
            EconomicConcepts = new[]
            {
                new ApuVersionEconomicConcept { Code = "EC001", Category = "GastosGenerales", Value = 10m }
            }.AsReadOnly(),
            Scenarios = new[]
            {
                new ApuVersionScenario { ScenarioId = "S001", Name = "Base", OverrideCount = 0 }
            }.AsReadOnly()
        };
    }
}
