using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QubiCostElectrical.Tests;

internal sealed class InMemoryApuVersionRepository : IApuVersionRepository
{
    private readonly ConcurrentDictionary<string, ApuVersion> _store = new();
    private readonly ConcurrentDictionary<string, List<ApuVersion>> _byApu = new();

    public Task<ApuVersion?> GetByIdAsync(string versionId) =>
        Task.FromResult(_store.TryGetValue(versionId, out var v) ? v : null);

    public Task<IReadOnlyList<ApuVersion>> GetByApuIdAsync(string apuId) =>
        Task.FromResult<IReadOnlyList<ApuVersion>>(
            _byApu.TryGetValue(apuId, out var list) ? list.OrderByDescending(v => v.VersionNumber).ToList() : new List<ApuVersion>());

    public Task<IReadOnlyList<ApuVersion>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<ApuVersion>>(_store.Values.ToList());

    public Task SaveAsync(ApuVersion version)
    {
        _store[version.VersionId] = version;
        var list = _byApu.GetOrAdd(version.ApuId, _ => new List<ApuVersion>());
        lock (list) { list.RemoveAll(v => v.VersionId == version.VersionId); list.Add(version); }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string versionId)
    {
        if (_store.TryRemove(versionId, out var v))
        {
            if (_byApu.TryGetValue(v.ApuId, out var list))
                lock (list) list.RemoveAll(x => x.VersionId == versionId);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string versionId) =>
        Task.FromResult(_store.ContainsKey(versionId));

    public Task<int> GetNextVersionNumberAsync(string apuId) =>
        Task.FromResult(
            _byApu.TryGetValue(apuId, out var list) && list.Count > 0
                ? list.Max(v => v.VersionNumber) + 1 : 1);
}

internal sealed class InMemoryApuSnapshotRepository : IApuSnapshotRepository
{
    private readonly ConcurrentDictionary<string, ApuVersionSnapshot> _store = new();
    private readonly ConcurrentDictionary<string, List<ApuVersionSnapshot>> _byVersion = new();

    public Task<ApuVersionSnapshot?> GetByIdAsync(string snapshotId) =>
        Task.FromResult(_store.TryGetValue(snapshotId, out var s) ? s : null);

    public Task<IReadOnlyList<ApuVersionSnapshot>> GetByVersionIdAsync(string versionId) =>
        Task.FromResult<IReadOnlyList<ApuVersionSnapshot>>(
            _byVersion.TryGetValue(versionId, out var list) ? list.OrderByDescending(s => s.CapturedAt).ToList() : new List<ApuVersionSnapshot>());

    public Task SaveAsync(ApuVersionSnapshot snapshot)
    {
        _store[snapshot.SnapshotId] = snapshot;
        var list = _byVersion.GetOrAdd(snapshot.SourceVersionId, _ => new List<ApuVersionSnapshot>());
        lock (list) list.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string snapshotId) =>
        Task.FromResult(_store.ContainsKey(snapshotId));

    public Task<ApuVersionSnapshot?> GetLatestByApuIdAsync(string apuId) =>
        Task.FromResult(_store.Values
            .Where(s => !string.IsNullOrEmpty(s.SourceVersionId))
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault());
}

internal sealed class InMemoryApuCatalogRepository : IApuCatalogRepository
{
    private readonly ConcurrentDictionary<string, ApuCatalogEntry> _store = new();

    public Task<ApuCatalogEntry?> GetByApuIdAsync(string apuId) =>
        Task.FromResult(_store.TryGetValue(apuId, out var e) ? e : null);

    public Task<ApuCatalogSearchResult> SearchAsync(ApuCatalogSearchRequest request)
    {
        var query = _store.Values.AsEnumerable();

        if (request.Filter == ApuCatalogFilter.ActiveOnly)
            query = query.Where(e => e.IsActive);
        else if (request.Filter == ApuCatalogFilter.OnlyWithApprovedVersion)
            query = query.Where(e => !string.IsNullOrEmpty(e.ApprovedVersionId));
        else if (request.Filter == ApuCatalogFilter.OnlyWithoutApprovedVersion)
            query = query.Where(e => string.IsNullOrEmpty(e.ApprovedVersionId));

        if (!string.IsNullOrEmpty(request.Category))
            query = query.Where(e => e.Category == request.Category);
        if (!string.IsNullOrEmpty(request.Owner))
            query = query.Where(e => e.Owner == request.Owner);
        if (!string.IsNullOrEmpty(request.Query))
            query = query.Where(e =>
                (e.Name?.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Code?.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ?? false));

        var filtered = query.ToList();
        var total = _store.Count;
        var paged = filtered.Skip(request.Offset).Take(request.MaxResults).ToList();

        return Task.FromResult(new ApuCatalogSearchResult
        {
            Entries = paged.AsReadOnly(),
            TotalCount = total,
            FilteredCount = filtered.Count,
            Offset = request.Offset,
            MaxResults = request.MaxResults
        });
    }

    public Task<ApuCatalogStats> GetStatsAsync()
    {
        var all = _store.Values.ToList();
        return Task.FromResult(new ApuCatalogStats
        {
            TotalEntries = all.Count,
            ActiveEntries = all.Count(e => e.IsActive),
            WithApprovedVersion = all.Count(e => !string.IsNullOrEmpty(e.ApprovedVersionId)),
            WithDraftVersion = all.Count(e => e.ActiveVersionStatus == ApuVersionStatus.Draft),
            TotalVersions = all.Sum(e => e.VersionCount)
        });
    }

    public Task SaveAsync(ApuCatalogEntry entry)
    {
        _store[entry.ApuId] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string apuId)
    {
        _store.TryRemove(apuId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string apuId) =>
        Task.FromResult(_store.ContainsKey(apuId));
}
