using System;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ApuVersionSnapshotTests
{
    [Fact]
    public void Snapshot_compute_hash_returns_non_empty()
    {
        var snap = new ApuVersionSnapshot
        {
            SourceVersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 1,
            Author = "test",
            CapturedBy = "tester",
            Reason = "test snapshot",
            TotalDirectCost = 5000m,
            TotalFinalPrice = 10000m,
            ComponentCount = 3,
            ResourceRefCount = 5,
            EconomicConceptCount = 2,
            ScenarioCount = 1
        };

        var hash = snap.ComputeHash();
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Snapshot_hash_is_deterministic()
    {
        var id = Guid.NewGuid().ToString("N");
        var snap1 = new ApuVersionSnapshot { SourceVersionId = id, VersionNumber = 1, Author = "a", TotalDirectCost = 100m, TotalFinalPrice = 200m, CapturedBy = "tester", Reason = "test", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 };
        var snap2 = new ApuVersionSnapshot { SourceVersionId = id, VersionNumber = 1, Author = "a", TotalDirectCost = 100m, TotalFinalPrice = 200m, CapturedBy = "tester", Reason = "test", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 };

        Assert.Equal(snap1.ComputeHash(), snap2.ComputeHash());
    }

    [Fact]
    public void Snapshot_hash_changes_when_data_changes()
    {
        var snap1 = new ApuVersionSnapshot { SourceVersionId = "v1", VersionNumber = 1, Author = "a", TotalDirectCost = 100m, TotalFinalPrice = 200m, CapturedBy = "tester", Reason = "test", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 };
        var snap2 = snap1 with { TotalDirectCost = 999m };

        Assert.NotEqual(snap1.ComputeHash(), snap2.ComputeHash());
    }

    [Fact]
    public void Snapshot_is_tampered_when_hash_mismatch()
    {
        var snap = new ApuVersionSnapshot
        {
            SourceVersionId = "v1",
            VersionNumber = 1,
            Author = "a",
            TotalDirectCost = 100m,
            TotalFinalPrice = 200m,
            CapturedBy = "tester",
            Reason = "test",
            ComponentCount = 1,
            ResourceRefCount = 1,
            EconomicConceptCount = 1,
            ScenarioCount = 1
        };

        var realHash = snap.ComputeHash();
        snap = snap with { IntegrityHash = "TAMPERED_HASH" };
        Assert.True(snap.IsTampered);

        snap = snap with { IntegrityHash = realHash };
        Assert.False(snap.IsTampered);
    }

    [Fact]
    public void Snapshot_with_empty_hash_not_tampered()
    {
        var snap = new ApuVersionSnapshot
        {
            SourceVersionId = "v1",
            VersionNumber = 1,
            Author = "a",
            TotalDirectCost = 100m,
            TotalFinalPrice = 200m,
            CapturedBy = "tester",
            Reason = "test",
            ComponentCount = 1,
            ResourceRefCount = 1,
            EconomicConceptCount = 1,
            ScenarioCount = 1
        };

        Assert.False(snap.IsTampered);
    }

    [Fact]
    public void Snapshot_roundtrip_integrity()
    {
        var snap = new ApuVersionSnapshot
        {
            SourceVersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = 5,
            Author = "jperez",
            CapturedBy = "mrodriguez",
            Reason = "Pre-approval checkpoint",
            TotalDirectCost = 12500.50m,
            TotalFinalPrice = 25000.75m,
            Currency = "UF",
            ComponentCount = 8,
            ResourceRefCount = 12,
            EconomicConceptCount = 4,
            ScenarioCount = 2
        };

        var hash = snap.ComputeHash();
        snap = snap with { IntegrityHash = hash };

        Assert.False(snap.IsTampered);
        Assert.Equal(hash, snap.ComputeHash());
    }
}
