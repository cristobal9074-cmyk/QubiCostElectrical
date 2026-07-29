using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IApuVersionRepository
{
    Task<ApuVersion?> GetByIdAsync(string versionId);
    Task<IReadOnlyList<ApuVersion>> GetByApuIdAsync(string apuId);
    Task<IReadOnlyList<ApuVersion>> GetAllAsync();
    Task SaveAsync(ApuVersion version);
    Task DeleteAsync(string versionId);
    Task<bool> ExistsAsync(string versionId);
    Task<int> GetNextVersionNumberAsync(string apuId);
}

public interface IApuSnapshotRepository
{
    Task<ApuVersionSnapshot?> GetByIdAsync(string snapshotId);
    Task<IReadOnlyList<ApuVersionSnapshot>> GetByVersionIdAsync(string versionId);
    Task SaveAsync(ApuVersionSnapshot snapshot);
    Task<bool> ExistsAsync(string snapshotId);
    Task<ApuVersionSnapshot?> GetLatestByApuIdAsync(string apuId);
}

public interface IApuCatalogRepository
{
    Task<ApuCatalogEntry?> GetByApuIdAsync(string apuId);
    Task<ApuCatalogSearchResult> SearchAsync(ApuCatalogSearchRequest request);
    Task<ApuCatalogStats> GetStatsAsync();
    Task SaveAsync(ApuCatalogEntry entry);
    Task DeleteAsync(string apuId);
    Task<bool> ExistsAsync(string apuId);
}
