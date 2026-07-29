using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class ProfessionalBudgetStoreIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CancellationToken _ct = CancellationToken.None;

    public ProfessionalBudgetStoreIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_prof_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ────────────────────────────── Migration tests ──────────────────────────────

    [Fact]
    public void Migration028_creates_professional_resources_tables()
    {
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        Assert.True(TableExists("professional_resources"));
        Assert.True(TableExists("professional_price_history"));
        Assert.True(TableExists("professional_exchange_rates"));
        AssertHasColumns("professional_resources", "id", "code", "type", "json");
        AssertHasColumns("professional_price_history", "id", "resource_id", "price", "json");
        AssertHasColumns("professional_exchange_rates", "id", "source_currency", "target_currency", "rate", "json");
    }

    [Fact]
    public void Migration029_creates_apu_versioning_tables()
    {
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        Assert.True(TableExists("professional_apu_versions"));
        Assert.True(TableExists("professional_apu_snapshots"));
        Assert.True(TableExists("professional_apu_catalog"));
        AssertHasColumns("professional_apu_versions", "version_id", "apu_id", "status", "json");
        AssertHasColumns("professional_apu_snapshots", "snapshot_id", "source_version_id", "integrity_hash", "json");
        AssertHasColumns("professional_apu_catalog", "apu_id", "code", "name", "json");
    }

    [Fact]
    public void Migration030_creates_economic_config_tables()
    {
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        Assert.True(TableExists("professional_economic_configs"));
        Assert.True(TableExists("professional_scenarios"));
        AssertHasColumns("professional_economic_configs", "id", "concept_code", "category", "json_data");
        AssertHasColumns("professional_scenarios", "id", "name", "scenario_type", "json_data");
    }

    [Fact]
    public void Schema_version_is_34_after_migration()
    {
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        int version = ReadUserVersion();
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, version);
    }

    [Fact]
    public void Migrations_are_idempotent()
    {
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        int version = ReadUserVersion();
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, version);
    }

    [Fact]
    public void Applies_incrementally_from_v27()
    {
        SQLiteSchemaMigratorTestHelper.RunToVersion(_dbPath, 27);
        SQLiteSchemaMigratorTestHelper.RunToLatest(_dbPath);
        Assert.True(TableExists("professional_resources"));
        Assert.True(TableExists("professional_apu_versions"));
        Assert.True(TableExists("professional_economic_configs"));
    }

    // ────────────────────────────── Resource Repository ──────────────────────────────

    [Fact]
    public async Task Resource_repo_saves_and_loads()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        var r = new Resource { Code = "RES001", Description = "Test resource", Unit = "UN", BasePrice = 1500m };
        await repo.SaveAsync(r, _ct);
        var loaded = await repo.GetByIdAsync(r.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal(r.Code, loaded.Code);
        Assert.Equal(r.Description, loaded.Description);
        Assert.Equal(r.BasePrice, loaded.BasePrice);
    }

    [Fact]
    public async Task Resource_repo_get_by_code()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        var r = new Resource { Code = "RES-ABC", Description = "Find me" };
        await repo.SaveAsync(r, _ct);
        var loaded = await repo.GetByCodeAsync("RES-ABC", _ct);
        Assert.NotNull(loaded);
        Assert.Equal(r.Id, loaded.Id);
    }

    [Fact]
    public async Task Resource_repo_get_all()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        await repo.SaveAsync(new Resource { Code = "R1" }, _ct);
        await repo.SaveAsync(new Resource { Code = "R2" }, _ct);
        await repo.SaveAsync(new Resource { Code = "R3" }, _ct);
        var all = await repo.GetAllAsync(_ct);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Resource_repo_update_overwrites()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        var r = new Resource { Code = "UPD", Description = "Original", Unit = "UN" };
        await repo.SaveAsync(r, _ct);
        var updated = r with { Description = "Modified", BasePrice = 999m };
        await repo.SaveAsync(updated, _ct);
        var loaded = await repo.GetByIdAsync(r.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal("Modified", loaded.Description);
        Assert.Equal(999m, loaded.BasePrice);
    }

    [Fact]
    public async Task Resource_repo_delete_removes()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        var r = new Resource { Code = "DEL" };
        await repo.SaveAsync(r, _ct);
        await repo.DeleteAsync(r.Id, _ct);
        var loaded = await repo.GetByIdAsync(r.Id, _ct);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Resource_repo_confirms_existence()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        await repo.SaveAsync(new Resource { Code = "EXIST" }, _ct);
        Assert.True(await repo.ExistsByCodeAsync("EXIST", null, _ct));
        Assert.False(await repo.ExistsByCodeAsync("NONEXIST", null, _ct));
    }

    [Fact]
    public async Task Resource_repo_persists_after_reopen()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        await repo.SaveAsync(new Resource { Code = "PERSIST", Description = "Should survive close" }, _ct);
        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteResourceRepository(_dbPath);
        var all = await repo2.GetAllAsync(_ct);
        Assert.Single(all);
        Assert.Equal("PERSIST", all[0].Code);
    }

    [Fact]
    public async Task Resource_repo_duplicate_code_detected()
    {
        var repo = new SqliteResourceRepository(_dbPath);
        await repo.SaveAsync(new Resource { Code = "DUP" }, _ct);
        Assert.True(await repo.ExistsByCodeAsync("DUP", null, _ct));
    }

    // ────────────────────────────── Price History Repository ──────────────────────────────

    [Fact]
    public async Task Price_history_add_and_get_by_resource()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var resource = new Resource { Code = "PH-RES" };
        await resourceRepo.SaveAsync(resource, _ct);

        var repo = new SqlitePriceHistoryRepository(_dbPath);
        var entry = new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 2500m,
            EffectiveDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Supplier = "Proveedor A"
        };
        await repo.AddAsync(entry, _ct);

        var history = await repo.GetByResourceIdAsync(resource.Id, _ct);
        Assert.Single(history);
        Assert.Equal(2500m, history[0].Price);
    }

    [Fact]
    public async Task Price_history_get_latest()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var resource = new Resource { Code = "PH-LATEST" };
        await resourceRepo.SaveAsync(resource, _ct);

        var repo = new SqlitePriceHistoryRepository(_dbPath);
        var oldEntry = new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 1000m,
            EffectiveDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newEntry = new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 2000m,
            EffectiveDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await repo.AddAsync(oldEntry, _ct);
        await repo.AddAsync(newEntry, _ct);

        var latest = await repo.GetLatestAsync(resource.Id, _ct);
        Assert.NotNull(latest);
        Assert.Equal(2000m, latest.Price);
    }

    [Fact]
    public async Task Price_history_date_range()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var resource = new Resource { Code = "PH-RANGE" };
        await resourceRepo.SaveAsync(resource, _ct);

        var repo = new SqlitePriceHistoryRepository(_dbPath);
        await repo.AddAsync(new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 100m, EffectiveDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await repo.AddAsync(new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 200m, EffectiveDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await repo.AddAsync(new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 300m, EffectiveDate = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        var range = await repo.GetByDateRangeAsync(resource.Id,
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), _ct);
        Assert.Equal(1, range.Count);
        Assert.Equal(200m, range[0].Price);
    }

    [Fact]
    public async Task Price_history_persists_after_reopen()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var resource = new Resource { Code = "PH-REOPEN" };
        await resourceRepo.SaveAsync(resource, _ct);

        var repo = new SqlitePriceHistoryRepository(_dbPath);
        await repo.AddAsync(new ResourcePriceHistoryEntry
        {
            ResourceId = resource.Id,
            Price = 5000m,
            EffectiveDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        SqliteConnection.ClearAllPools();
        var repo2 = new SqlitePriceHistoryRepository(_dbPath);
        var history = await repo2.GetByResourceIdAsync(resource.Id, _ct);
        Assert.Single(history);
        Assert.Equal(5000m, history[0].Price);
    }

    // ────────────────────────────── Exchange Rate Repository ──────────────────────────────

    [Fact]
    public async Task Exchange_rate_add_and_get_all()
    {
        var repo = new SqliteExchangeRateRepository(_dbPath);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 950m,
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "EUR",
            TargetCurrency = "CLP",
            Rate = 1050m,
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        var all = await repo.GetAllAsync(_ct);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Exchange_rate_get_latest()
    {
        var repo = new SqliteExchangeRateRepository(_dbPath);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 900m,
            Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 950m,
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        var latest = await repo.GetLatestAsync("USD", "CLP", _ct);
        Assert.NotNull(latest);
        Assert.Equal(950m, latest.Rate);
    }

    [Fact]
    public async Task Exchange_rate_get_by_date()
    {
        var repo = new SqliteExchangeRateRepository(_dbPath);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 900m,
            Date = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 950m,
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        var atDate = await repo.GetByDateAsync("USD", "CLP",
            new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc), _ct);
        Assert.NotNull(atDate);
        Assert.Equal(900m, atDate.Rate);
    }

    [Fact]
    public async Task Exchange_rate_persists_after_reopen()
    {
        var repo = new SqliteExchangeRateRepository(_dbPath);
        await repo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 950m,
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteExchangeRateRepository(_dbPath);
        var all = await repo2.GetAllAsync(_ct);
        Assert.Single(all);
        Assert.Equal(950m, all[0].Rate);
    }

    // ────────────────────────────── APU Version Repository ──────────────────────────────

    [Fact]
    public async Task Apu_version_save_and_get_by_id()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-1", 1);
        await repo.SaveAsync(v);
        var loaded = await repo.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal(v.VersionId, loaded.VersionId);
        Assert.Equal(v.Code, loaded.Code);
    }

    [Fact]
    public async Task Apu_version_get_by_apu_id()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var apuId = Guid.NewGuid().ToString("N");
        await repo.SaveAsync(CreateApuVersion(apuId, 1));
        await repo.SaveAsync(CreateApuVersion(apuId, 2));
        var versions = await repo.GetByApuIdAsync(apuId);
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task Apu_version_get_all()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        await repo.SaveAsync(CreateApuVersion("A1", 1));
        await repo.SaveAsync(CreateApuVersion("A2", 1));
        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Apu_version_delete_removes()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-DEL", 1);
        await repo.SaveAsync(v);
        await repo.DeleteAsync(v.VersionId);
        Assert.Null(await repo.GetByIdAsync(v.VersionId));
    }

    [Fact]
    public async Task Apu_version_next_number_increments()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var apuId = Guid.NewGuid().ToString("N");
        Assert.Equal(1, await repo.GetNextVersionNumberAsync(apuId));
        await repo.SaveAsync(CreateApuVersion(apuId, 1));
        Assert.Equal(2, await repo.GetNextVersionNumberAsync(apuId));
    }

    [Fact]
    public async Task Apu_version_exists_returns_true()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-EXIST", 1);
        await repo.SaveAsync(v);
        Assert.True(await repo.ExistsAsync(v.VersionId));
        Assert.False(await repo.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task Apu_version_update_keeps_data()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-UPD", 1);
        await repo.SaveAsync(v);
        var updated = v with { Status = ApuVersionStatus.Approved, Notes = "Approved after review" };
        await repo.SaveAsync(updated);
        var loaded = await repo.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal(ApuVersionStatus.Approved, loaded.Status);
        Assert.Equal("Approved after review", loaded.Notes);
    }

    [Fact]
    public async Task Apu_version_persists_after_reopen()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-REOPEN", 1);
        await repo.SaveAsync(v);
        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteApuVersionRepository(_dbPath);
        var loaded = await repo2.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal(v.Code, loaded.Code);
    }

    [Fact]
    public async Task Apu_version_integrity_hash_preserved()
    {
        var repo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("APU-HASH", 1) with { IntegrityHash = "sha256-abc123" };
        await repo.SaveAsync(v);
        var loaded = await repo.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal("sha256-abc123", loaded.IntegrityHash);
    }

    // ────────────────────────────── APU Snapshot Repository ──────────────────────────────

    [Fact]
    public async Task Apu_snapshot_save_and_get()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("SNAP-APU", 1);
        await versionRepo.SaveAsync(v);

        var repo = new SqliteApuSnapshotRepository(_dbPath);
        var snap = CreateSnapshot(v);
        await repo.SaveAsync(snap);
        var loaded = await repo.GetByIdAsync(snap.SnapshotId);
        Assert.NotNull(loaded);
        Assert.Equal(snap.SnapshotId, loaded.SnapshotId);
    }

    [Fact]
    public async Task Apu_snapshot_get_by_version()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("SNAP-VR", 1);
        await versionRepo.SaveAsync(v);

        var repo = new SqliteApuSnapshotRepository(_dbPath);
        await repo.SaveAsync(CreateSnapshot(v));
        await repo.SaveAsync(CreateSnapshot(v));

        var snaps = await repo.GetByVersionIdAsync(v.VersionId);
        Assert.Equal(2, snaps.Count);
    }

    [Fact]
    public async Task Apu_snapshot_integrity_hash_preserved()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("SNAP-HASH", 1);
        await versionRepo.SaveAsync(v);

        var repo = new SqliteApuSnapshotRepository(_dbPath);
        var snap = CreateSnapshot(v) with { IntegrityHash = "sha256-verified" };
        await repo.SaveAsync(snap);
        var loaded = await repo.GetByIdAsync(snap.SnapshotId);
        Assert.NotNull(loaded);
        Assert.Equal("sha256-verified", loaded.IntegrityHash);
    }

    [Fact]
    public async Task Apu_snapshot_exists_returns_true()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("SNAP-EXIST", 1);
        await versionRepo.SaveAsync(v);

        var repo = new SqliteApuSnapshotRepository(_dbPath);
        var snap = CreateSnapshot(v);
        await repo.SaveAsync(snap);
        Assert.True(await repo.ExistsAsync(snap.SnapshotId));
    }

    [Fact]
    public async Task Apu_snapshot_persists_after_reopen()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("SNAP-REOPEN", 1);
        await versionRepo.SaveAsync(v);

        var repo = new SqliteApuSnapshotRepository(_dbPath);
        var snap = CreateSnapshot(v);
        await repo.SaveAsync(snap);

        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteApuSnapshotRepository(_dbPath);
        var loaded = await repo2.GetByIdAsync(snap.SnapshotId);
        Assert.NotNull(loaded);
        Assert.Equal(snap.SnapshotId, loaded.SnapshotId);
    }

    // ────────────────────────────── APU Catalog Repository ──────────────────────────────

    [Fact]
    public async Task Apu_catalog_save_and_get()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        var entry = CreateCatalogEntry("CAT-001");
        await repo.SaveAsync(entry);
        var loaded = await repo.GetByApuIdAsync(entry.ApuId);
        Assert.NotNull(loaded);
        Assert.Equal(entry.Code, loaded.Code);
    }

    [Fact]
    public async Task Apu_catalog_search_by_code()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        await repo.SaveAsync(CreateCatalogEntry("SEARCH-001"));
        await repo.SaveAsync(CreateCatalogEntry("OTHER-002"));

        var result = await repo.SearchAsync(new ApuCatalogSearchRequest
        {
            Query = "SEARCH",
            MaxResults = 10
        });
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task Apu_catalog_search_by_category()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        await repo.SaveAsync(CreateCatalogEntry("CAT-A") with { Category = "Electrica" });
        await repo.SaveAsync(CreateCatalogEntry("CAT-B") with { Category = "Electrica" });
        await repo.SaveAsync(CreateCatalogEntry("CAT-C") with { Category = "Mecanica" });

        var result = await repo.SearchAsync(new ApuCatalogSearchRequest
        {
            Category = "Electrica",
            MaxResults = 10
        });
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public async Task Apu_catalog_pagination()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        for (int i = 0; i < 5; i++)
            await repo.SaveAsync(CreateCatalogEntry($"PAG-{i:D3}"));

        var page1 = await repo.SearchAsync(new ApuCatalogSearchRequest { MaxResults = 2, Offset = 0 });
        var page2 = await repo.SearchAsync(new ApuCatalogSearchRequest { MaxResults = 2, Offset = 2 });
        Assert.Equal(2, page1.Entries.Count);
        Assert.Equal(2, page2.Entries.Count);
        Assert.Equal(5, page1.TotalCount);
    }

    [Fact]
    public async Task Apu_catalog_get_stats()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        await repo.SaveAsync(CreateCatalogEntry("STATS-001") with { IsActive = true });
        await repo.SaveAsync(CreateCatalogEntry("STATS-002") with { IsActive = true });
        await repo.SaveAsync(CreateCatalogEntry("STATS-003") with { IsActive = false });

        var stats = await repo.GetStatsAsync();
        Assert.Equal(3, stats.TotalEntries);
        Assert.Equal(2, stats.ActiveEntries);
    }

    [Fact]
    public async Task Apu_catalog_delete_removes()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        var entry = CreateCatalogEntry("DEL-001");
        await repo.SaveAsync(entry);
        await repo.DeleteAsync(entry.ApuId);
        Assert.Null(await repo.GetByApuIdAsync(entry.ApuId));
    }

    [Fact]
    public async Task Apu_catalog_exists_returns_true()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        var entry = CreateCatalogEntry("EXIST-001");
        await repo.SaveAsync(entry);
        Assert.True(await repo.ExistsAsync(entry.ApuId));
        Assert.False(await repo.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task Apu_catalog_persists_after_reopen()
    {
        var repo = new SqliteApuCatalogRepository(_dbPath);
        var entry = CreateCatalogEntry("REOPEN-001");
        await repo.SaveAsync(entry);

        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteApuCatalogRepository(_dbPath);
        var loaded = await repo2.GetByApuIdAsync(entry.ApuId);
        Assert.NotNull(loaded);
        Assert.Equal(entry.Code, loaded.Code);
    }

    // ────────────────────────────── Service Integration Tests ──────────────────────────────

    [Fact]
    public async Task Full_flow_create_resource_and_price()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var priceRepo = new SqlitePriceHistoryRepository(_dbPath);

        var r = new Resource { Code = "FULL-001", Description = "Full flow resource", BasePrice = 1000m };
        await resourceRepo.SaveAsync(r, _ct);

        await priceRepo.AddAsync(new ResourcePriceHistoryEntry
        {
            ResourceId = r.Id, Price = 1200m,
            EffectiveDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Supplier = "Supplier A"
        }, _ct);

        var latest = await priceRepo.GetLatestAsync(r.Id, _ct);
        Assert.NotNull(latest);
        Assert.Equal(1200m, latest.Price);
    }

    [Fact]
    public async Task Full_flow_apu_version_and_catalog()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var catalogRepo = new SqliteApuCatalogRepository(_dbPath);

        var apuId = Guid.NewGuid().ToString("N");
        var v = CreateApuVersion(apuId, 1) with { Status = ApuVersionStatus.Draft };
        await versionRepo.SaveAsync(v);

        var entry = CreateCatalogEntry("CAT-CODE") with
        {
            ActiveVersionId = v.VersionId,
            ActiveVersionNumber = v.VersionNumber,
            ActiveVersionStatus = v.Status
        };
        await catalogRepo.SaveAsync(entry);

        var loadedEntry = await catalogRepo.GetByApuIdAsync(entry.ApuId);
        Assert.NotNull(loadedEntry);
        Assert.Equal(v.VersionId, loadedEntry.ActiveVersionId);
    }

    [Fact]
    public async Task Full_flow_snapshot_after_approval()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var snapshotRepo = new SqliteApuSnapshotRepository(_dbPath);

        var v = CreateApuVersion("SNAP-FLOW", 1) with { Status = ApuVersionStatus.Approved };
        await versionRepo.SaveAsync(v);

        var snap = CreateSnapshot(v);
        await snapshotRepo.SaveAsync(snap);

        var loadedSnap = await snapshotRepo.GetByIdAsync(snap.SnapshotId);
        Assert.NotNull(loadedSnap);
        Assert.Equal(v.VersionId, loadedSnap.SourceVersionId);
    }

    [Fact]
    public async Task Full_flow_reopen_preserves_everything()
    {
        var resourceRepo = new SqliteResourceRepository(_dbPath);
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var catalogRepo = new SqliteApuCatalogRepository(_dbPath);

        var r = new Resource { Code = "REOPEN-FLOW", Description = "Should survive restart" };
        await resourceRepo.SaveAsync(r, _ct);

        var v = CreateApuVersion("REOPEN-V", 1);
        await versionRepo.SaveAsync(v);

        var entry = CreateCatalogEntry("REOPEN-C") with { ActiveVersionId = v.VersionId };
        await catalogRepo.SaveAsync(entry);

        SqliteConnection.ClearAllPools();

        var resourceRepo2 = new SqliteResourceRepository(_dbPath);
        var versionRepo2 = new SqliteApuVersionRepository(_dbPath);
        var catalogRepo2 = new SqliteApuCatalogRepository(_dbPath);

        var loadedResource = await resourceRepo2.GetByCodeAsync("REOPEN-FLOW", _ct);
        Assert.NotNull(loadedResource);

        var loadedVersion = await versionRepo2.GetByIdAsync(v.VersionId);
        Assert.NotNull(loadedVersion);

        var loadedEntry = await catalogRepo2.GetByApuIdAsync(entry.ApuId);
        Assert.NotNull(loadedEntry);
    }

    [Fact]
    public async Task Full_flow_comparison_of_two_persisted_versions()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var apuId = Guid.NewGuid().ToString("N");

        var v1 = CreateApuVersion(apuId, 1) with
        {
            CalculatedDirectCost = 10000m,
            CalculatedFinalPrice = 15000m,
            Status = ApuVersionStatus.Approved
        };
        var v2 = CreateApuVersion(apuId, 2) with
        {
            CalculatedDirectCost = 12000m,
            CalculatedFinalPrice = 18000m,
            Status = ApuVersionStatus.Draft,
            PreviousVersionId = v1.VersionId
        };
        await versionRepo.SaveAsync(v1);
        await versionRepo.SaveAsync(v2);

        var loadedV1 = await versionRepo.GetByIdAsync(v1.VersionId);
        var loadedV2 = await versionRepo.GetByIdAsync(v2.VersionId);
        Assert.NotNull(loadedV1);
        Assert.NotNull(loadedV2);
        Assert.True(loadedV2.CalculatedDirectCost > loadedV1.CalculatedDirectCost);
    }

    [Fact]
    public async Task Full_flow_exchange_rate_for_conversion()
    {
        var rateRepo = new SqliteExchangeRateRepository(_dbPath);
        await rateRepo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 950m,
            Date = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);
        await rateRepo.AddAsync(new ExchangeRate
        {
            SourceCurrency = "USD",
            TargetCurrency = "CLP",
            Rate = 970m,
            Date = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        }, _ct);

        var atDate = await rateRepo.GetByDateAsync("USD", "CLP",
            new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc), _ct);
        Assert.NotNull(atDate);
        Assert.Equal(950m, atDate.Rate);

        var latest = await rateRepo.GetLatestAsync("USD", "CLP", _ct);
        Assert.NotNull(latest);
        Assert.Equal(970m, latest.Rate);
    }

    [Fact]
    public async Task Full_flow_catalog_stats_with_versions()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var catalogRepo = new SqliteApuCatalogRepository(_dbPath);

        var apu1 = Guid.NewGuid().ToString("N");
        var apu2 = Guid.NewGuid().ToString("N");

        var v1 = CreateApuVersion(apu1, 1) with { Status = ApuVersionStatus.Approved };
        var v2a = CreateApuVersion(apu2, 1) with { Status = ApuVersionStatus.Draft };
        var v2b = CreateApuVersion(apu2, 2) with { Status = ApuVersionStatus.Approved };
        await versionRepo.SaveAsync(v1);
        await versionRepo.SaveAsync(v2a);
        await versionRepo.SaveAsync(v2b);

        await catalogRepo.SaveAsync(CreateCatalogEntry(apu1) with
        {
            IsActive = true,
            ApprovedVersionId = v1.VersionId,
            ActiveVersionStatus = ApuVersionStatus.Approved
        });
        await catalogRepo.SaveAsync(CreateCatalogEntry(apu2) with
        {
            IsActive = true,
            ApprovedVersionId = v2b.VersionId,
            ActiveVersionStatus = ApuVersionStatus.Approved
        });

        var stats = await catalogRepo.GetStatsAsync();
        Assert.Equal(2, stats.TotalEntries);
        Assert.Equal(2, stats.ActiveEntries);
        Assert.Equal(2, stats.WithApprovedVersion);
    }

    [Fact]
    public async Task Full_flow_integrity_hash_across_reopen()
    {
        var versionRepo = new SqliteApuVersionRepository(_dbPath);
        var v = CreateApuVersion("HASH-FLOW", 1) with { IntegrityHash = "sha256-preserved-across-reopen" };
        await versionRepo.SaveAsync(v);

        SqliteConnection.ClearAllPools();
        var versionRepo2 = new SqliteApuVersionRepository(_dbPath);
        var loaded = await versionRepo2.GetByIdAsync(v.VersionId);
        Assert.NotNull(loaded);
        Assert.Equal("sha256-preserved-across-reopen", loaded.IntegrityHash);
    }

    // ────────────────────────────── Helpers ──────────────────────────────

    private static ApuVersion CreateApuVersion(string apuId, int versionNumber)
    {
        var now = DateTime.UtcNow;
        return new ApuVersion
        {
            VersionId = Guid.NewGuid().ToString("N"),
            ApuId = apuId,
            Code = $"APU-{apuId}-v{versionNumber}",
            Name = $"APU {apuId} v{versionNumber}",
            Description = $"Version {versionNumber} of {apuId}",
            Category = "General",
            Unit = "UN",
            Status = ApuVersionStatus.Draft,
            VersionNumber = versionNumber,
            Author = "test",
            CalculatedDirectCost = 0m,
            CalculatedFinalPrice = 0m,
            CalculatedCurrency = "CLP",
            EffectiveDate = now,
            CreatedAt = now,
            IsActive = true,
            IntegrityHash = ""
        };
    }

    private static ApuVersionSnapshot CreateSnapshot(ApuVersion v)
    {
        return new ApuVersionSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            SourceVersionId = v.VersionId,
            CapturedAt = DateTime.UtcNow,
            VersionNumber = v.VersionNumber,
            Author = v.Author,
            CapturedBy = "test",
            TotalDirectCost = v.CalculatedDirectCost,
            TotalFinalPrice = v.CalculatedFinalPrice,
            Currency = v.CalculatedCurrency,
            ComponentCount = 0,
            ResourceRefCount = 0,
            EconomicConceptCount = 0,
            ScenarioCount = 0,
            IntegrityHash = Guid.NewGuid().ToString("N")
        };
    }

    private static ApuCatalogEntry CreateCatalogEntry(string code)
    {
        return new ApuCatalogEntry
        {
            ApuId = Guid.NewGuid().ToString("N"),
            Code = code,
            Name = $"Catalog entry {code}",
            Description = "",
            Category = "General",
            Unit = "UN",
            VersionCount = 1,
            ActiveVersionNumber = 1,
            ActiveVersionId = "",
            ActiveVersionStatus = ApuVersionStatus.Draft,
            LastModifiedAt = DateTime.UtcNow,
            LastPrice = 0m,
            ApprovedVersionId = "",
            ApprovedVersionNumber = 0,
            Owner = "test",
            Tags = "",
            IsActive = true
        };
    }

    private bool TableExists(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", name);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void AssertHasColumns(string table, params string[] expected)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        var actual = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) actual.Add(reader.GetString(1));
        foreach (var col in expected)
            Assert.True(actual.Contains(col), $"Table {table} should have column '{col}'");
    }

    private int ReadUserVersion()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;");
        conn.Open();
        return conn;
    }
}

internal static class SQLiteSchemaMigratorTestHelper
{
    internal static void RunToLatest(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    internal static void RunToVersion(string dbPath, int targetVersion)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {targetVersion};";
        cmd.ExecuteNonQuery();
    }
}
