using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ApuVersionRepositoryTests
{
    private readonly InMemoryApuVersionRepository _repo = new();

    [Fact]
    public async Task Save_and_get_by_id()
    {
        var v = CreateTestVersion("apu-1", 1);
        await _repo.SaveAsync(v);
        var loaded = await _repo.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal(v.VersionId, loaded.VersionId);
    }

    [Fact]
    public async Task Get_versions_by_apu_id()
    {
        var apuId = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(CreateTestVersion(apuId, 1));
        await _repo.SaveAsync(CreateTestVersion(apuId, 2));
        var versions = await _repo.GetByApuIdAsync(apuId);
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task Delete_removes_version()
    {
        var v = CreateTestVersion("apu-del", 1);
        await _repo.SaveAsync(v);
        await _repo.DeleteAsync(v.VersionId);
        var loaded = await _repo.GetByIdAsync(v.VersionId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Exists_returns_true_for_existing()
    {
        var v = CreateTestVersion("apu-exists", 1);
        await _repo.SaveAsync(v);
        Assert.True(await _repo.ExistsAsync(v.VersionId));
        Assert.False(await _repo.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task Get_next_version_number_for_new_apu()
    {
        var n = await _repo.GetNextVersionNumberAsync("new-apu");
        Assert.Equal(1, n);
    }

    [Fact]
    public async Task Get_next_version_number_increments()
    {
        var apuId = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(CreateTestVersion(apuId, 1));
        await _repo.SaveAsync(CreateTestVersion(apuId, 2));
        var n = await _repo.GetNextVersionNumberAsync(apuId);
        Assert.Equal(3, n);
    }

    [Fact]
    public async Task Get_all_returns_all_versions()
    {
        await _repo.SaveAsync(CreateTestVersion("a1", 1));
        await _repo.SaveAsync(CreateTestVersion("a2", 1));
        var all = await _repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Update_existing_version()
    {
        var v = CreateTestVersion("apu-upd", 1);
        await _repo.SaveAsync(v);
        var updated = v with { CalculatedFinalPrice = 9999m };
        await _repo.SaveAsync(updated);
        var loaded = await _repo.GetByIdAsync(v.VersionId);
        Assert.Equal(9999m, loaded!.CalculatedFinalPrice);
    }

    private static ApuVersion CreateTestVersion(string apuId, int versionNumber)
    {
        return new ApuVersion
        {
            ApuId = apuId,
            VersionId = Guid.NewGuid().ToString("N"),
            Code = $"APU-{versionNumber}",
            Name = $"Test {versionNumber}",
            VersionNumber = versionNumber,
            Author = "test",
            CreatedAt = DateTime.UtcNow
        };
    }
}

public sealed class ApuSnapshotRepositoryTests
{
    private readonly InMemoryApuSnapshotRepository _repo = new();

    [Fact]
    public async Task Save_and_get_snapshot()
    {
        var s = new ApuVersionSnapshot { SourceVersionId = "v1", VersionNumber = 1, Author = "a", CapturedBy = "t", Reason = "r", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 };
        await _repo.SaveAsync(s);
        var loaded = await _repo.GetByIdAsync(s.SnapshotId);
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task Get_snapshots_by_version_id()
    {
        var vId = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(new ApuVersionSnapshot { SourceVersionId = vId, VersionNumber = 1, Author = "a", CapturedBy = "t", Reason = "r", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 });
        await _repo.SaveAsync(new ApuVersionSnapshot { SourceVersionId = vId, VersionNumber = 1, Author = "a", CapturedBy = "t", Reason = "r", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 });
        var list = await _repo.GetByVersionIdAsync(vId);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task Exists_returns_correctly()
    {
        var s = new ApuVersionSnapshot { SourceVersionId = "v1", VersionNumber = 1, Author = "a", CapturedBy = "t", Reason = "r", ComponentCount = 1, ResourceRefCount = 1, EconomicConceptCount = 1, ScenarioCount = 1 };
        await _repo.SaveAsync(s);
        Assert.True(await _repo.ExistsAsync(s.SnapshotId));
        Assert.False(await _repo.ExistsAsync("bad"));
    }
}

public sealed class ApuCatalogRepositoryTests
{
    private readonly InMemoryApuCatalogRepository _repo = new();

    [Fact]
    public async Task Save_and_get_entry()
    {
        var e = new ApuCatalogEntry { ApuId = "apu-cat-1", Code = "C001", Name = "Test", IsActive = true };
        await _repo.SaveAsync(e);
        var loaded = await _repo.GetByApuIdAsync("apu-cat-1");
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task Search_by_filter_active_only()
    {
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "a1", Code = "A1", Name = "Active", IsActive = true });
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "a2", Code = "A2", Name = "Inactive", IsActive = false });
        var result = await _repo.SearchAsync(new ApuCatalogSearchRequest { Filter = ApuCatalogFilter.ActiveOnly });
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task Search_by_query()
    {
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "a1", Code = "CONC-01", Name = "Concrete", IsActive = true });
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "a2", Code = "STL-01", Name = "Steel", IsActive = true });
        var result = await _repo.SearchAsync(new ApuCatalogSearchRequest { Query = "concrete", Filter = ApuCatalogFilter.All });
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task Stats_are_correct()
    {
        var localRepo = new InMemoryApuCatalogRepository();
        await localRepo.SaveAsync(new ApuCatalogEntry { ApuId = "stats-a1", IsActive = true, ApprovedVersionId = "app1", VersionCount = 3, ActiveVersionStatus = ApuVersionStatus.Approved });
        await localRepo.SaveAsync(new ApuCatalogEntry { ApuId = "stats-a2", IsActive = true, VersionCount = 1, ActiveVersionStatus = ApuVersionStatus.Draft });
        await localRepo.SaveAsync(new ApuCatalogEntry { ApuId = "stats-a3", IsActive = false, VersionCount = 2 });

        var stats = await localRepo.GetStatsAsync();
        Assert.Equal(3, stats.TotalEntries);
        Assert.True(stats.ActiveEntries >= 1);
        Assert.True(stats.WithApprovedVersion >= 0);
        Assert.True(stats.WithDraftVersion >= 0);
        Assert.Equal(6, stats.TotalVersions);
    }

    [Fact]
    public async Task Delete_removes_entry()
    {
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "del-me", Name = "Delete me" });
        await _repo.DeleteAsync("del-me");
        var loaded = await _repo.GetByApuIdAsync("del-me");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Exists_returns_correctly()
    {
        await _repo.SaveAsync(new ApuCatalogEntry { ApuId = "exists-yes", Name = "Exists" });
        Assert.True(await _repo.ExistsAsync("exists-yes"));
        Assert.False(await _repo.ExistsAsync("exists-no"));
    }
}
