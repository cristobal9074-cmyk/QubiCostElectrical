using System;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ApuCatalogServiceTests
{
    private readonly ApuCatalogService _sut;
    private readonly InMemoryApuVersionRepository _versionRepo = new();
    private readonly InMemoryApuSnapshotRepository _snapshotRepo = new();
    private readonly InMemoryApuCatalogRepository _catalogRepo = new();

    public ApuCatalogServiceTests()
    {
        _sut = new ApuCatalogService(_versionRepo, _snapshotRepo, _catalogRepo);
    }

    [Fact]
    public async Task Create_draft_returns_new_version()
    {
        var draft = await _sut.CreateDraftAsync("apu-001", "author", "initial");
        Assert.NotNull(draft);
        Assert.Equal(ApuVersionStatus.Draft, draft.Status);
        Assert.Equal(1, draft.VersionNumber);
        Assert.Equal("apu-001", draft.ApuId);
    }

    [Fact]
    public async Task Create_draft_increments_version_number()
    {
        await _sut.CreateDraftAsync("apu-002", "author", "v1");
        var v2 = await _sut.CreateDraftAsync("apu-002", "author", "v2");
        Assert.Equal(2, v2!.VersionNumber);
    }

    [Fact]
    public async Task Create_draft_updates_catalog_entry()
    {
        await _sut.CreateDraftAsync("apu-003", "author", "first");
        var entry = await _sut.GetCatalogEntryAsync("apu-003");
        Assert.NotNull(entry);
        Assert.Equal(1, entry.VersionCount);
    }

    [Fact]
    public async Task Create_draft_with_base_version_copies_data()
    {
        var v1 = await _sut.CreateDraftAsync("apu-004", "author", "base");
        var v2 = await _sut.CreateDraftAsync("apu-004", "author", "derived", v1!.VersionId);
        Assert.NotNull(v2);
        Assert.NotEqual(v1.VersionId, v2!.VersionId);
        Assert.Equal(2, v2.VersionNumber);
    }

    [Fact]
    public async Task Create_snapshot_stores_integrity_hash()
    {
        var draft = await _sut.CreateDraftAsync("apu-005", "author", "test snapshot");
        var snap = await _sut.CreateSnapshotAsync(draft!.VersionId, "tester", "checkpoint");

        Assert.NotNull(snap);
        Assert.False(string.IsNullOrEmpty(snap.IntegrityHash));
        Assert.Equal(draft.VersionId, snap.SourceVersionId);
    }

    [Fact]
    public async Task Create_snapshot_and_verify_integrity()
    {
        var draft = await _sut.CreateDraftAsync("apu-006", "author", "test");
        var snap = await _sut.CreateSnapshotAsync(draft!.VersionId, "tester", "verify");

        var stored = await _snapshotRepo.GetByIdAsync(snap.SnapshotId);
        Assert.NotNull(stored);
        Assert.False(stored.IsTampered);
    }

    [Fact]
    public async Task Double_snapshot_same_version_works()
    {
        var draft = await _sut.CreateDraftAsync("apu-007", "author", "test");
        var snap1 = await _sut.CreateSnapshotAsync(draft!.VersionId, "tester", "checkpoint");
        var snap2 = await _sut.CreateSnapshotAsync(draft.VersionId, "tester", "checkpoint");

        Assert.NotEqual(snap1.SnapshotId, snap2.SnapshotId);
        Assert.Equal(snap1.ComputeHash(), snap2.ComputeHash());
    }

    [Fact]
    public async Task Approve_version_sets_approved_status()
    {
        var draft = await _sut.CreateDraftAsync("apu-008", "author", "to approve");
        var approved = await _sut.ApproveVersionAsync(draft!.VersionId, "approver", "looks good");

        Assert.NotNull(approved);
        Assert.Equal(ApuVersionStatus.Approved, approved.Status);
        Assert.False(string.IsNullOrEmpty(approved.IntegrityHash));
    }

    [Fact]
    public async Task Catalog_entry_updated_after_approval()
    {
        var draft = await _sut.CreateDraftAsync("apu-009", "author", "approve and check");
        await _sut.ApproveVersionAsync(draft!.VersionId, "approver", "ok");
        var entry = await _sut.GetCatalogEntryAsync("apu-009");

        Assert.NotNull(entry);
        Assert.Equal(draft.VersionId, entry.ApprovedVersionId);
    }

    [Fact]
    public async Task Get_version_history_returns_ordered_versions()
    {
        await _sut.CreateDraftAsync("apu-010", "author", "v1");
        await _sut.CreateDraftAsync("apu-010", "author", "v2");
        var history = await _sut.GetVersionHistoryAsync("apu-010");

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history[0].VersionNumber);
    }

    [Fact]
    public async Task Search_catalog_by_code()
    {
        var draft = await _sut.CreateDraftAsync("apu-011", "author", "test");
        var catalogEntry = await _sut.GetCatalogEntryAsync("apu-011");
        var updatedEntry = catalogEntry! with { Code = "APU-CUSTOM", Name = "Custom APU" };
        var service = _sut;
        await _catalogRepo.SaveAsync(updatedEntry);

        var result = await _sut.SearchCatalogAsync(new ApuCatalogSearchRequest { Query = "APU-CUSTOM", Filter = ApuCatalogFilter.All });
        Assert.NotEmpty(result.Entries);
        Assert.True(result.TotalCount >= 1);
    }

    [Fact]
    public async Task Get_catalog_stats()
    {
        await _sut.CreateDraftAsync("apu-012", "author", "stats");
        var stats = await _sut.GetCatalogStatsAsync();
        Assert.True(stats.TotalEntries >= 1);
    }

    [Fact]
    public async Task Compare_versions_through_service()
    {
        var v1 = await _sut.CreateDraftAsync("apu-013", "author", "first");
        var v2 = await _sut.CreateDraftAsync("apu-013", "author", "second", v1!.VersionId);

        var summary = await _sut.CompareVersionsAsync(v1.VersionId, v2!.VersionId);
        Assert.NotNull(summary);
    }
}
