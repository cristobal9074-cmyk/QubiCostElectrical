namespace QubiCostElectrical;

public interface IResourceRepository
{
    Task<List<Resource>> GetAllAsync(CancellationToken ct = default);
    Task<Resource?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Resource?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task SaveAsync(Resource resource, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, string? excludeId = null, CancellationToken ct = default);
}

public interface IPriceHistoryRepository
{
    Task<List<ResourcePriceHistoryEntry>> GetByResourceIdAsync(string resourceId, CancellationToken ct = default);
    Task<ResourcePriceHistoryEntry?> GetLatestAsync(string resourceId, CancellationToken ct = default);
    Task AddAsync(ResourcePriceHistoryEntry entry, CancellationToken ct = default);
    Task<List<ResourcePriceHistoryEntry>> GetByDateRangeAsync(string resourceId, DateTime from, DateTime to, CancellationToken ct = default);
}

public interface IExchangeRateRepository
{
    Task<List<ExchangeRate>> GetAllAsync(CancellationToken ct = default);
    Task<ExchangeRate?> GetLatestAsync(string source, string target, CancellationToken ct = default);
    Task<ExchangeRate?> GetByDateAsync(string source, string target, DateTime date, CancellationToken ct = default);
    Task AddAsync(ExchangeRate rate, CancellationToken ct = default);
}
