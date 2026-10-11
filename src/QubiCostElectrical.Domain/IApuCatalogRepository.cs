using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IApuCatalogRepository
{
    Task<ApuCatalogEntry?> GetByApuIdAsync(string apuId);

    Task<ApuCatalogSearchResult> SearchAsync(ApuCatalogSearchRequest request);

    Task<ApuCatalogStats> GetStatsAsync();

    Task SaveAsync(ApuCatalogEntry entry);

    Task DeleteAsync(string apuId);

    Task<bool> ExistsAsync(string apuId);
}
