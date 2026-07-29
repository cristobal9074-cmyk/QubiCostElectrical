namespace QubiCostElectrical;

public sealed class ResourceService
{
    private readonly IResourceRepository repository;
    private readonly IPriceHistoryRepository priceHistory;
    private readonly IExchangeRateRepository exchangeRates;

    public ResourceService(IResourceRepository repository, IPriceHistoryRepository priceHistory, IExchangeRateRepository exchangeRates)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.priceHistory = priceHistory ?? throw new ArgumentNullException(nameof(priceHistory));
        this.exchangeRates = exchangeRates ?? throw new ArgumentNullException(nameof(exchangeRates));
    }

    public async Task<Resource> CreateResourceAsync(Resource resource, CancellationToken ct = default)
    {
        var validation = ResourceValidator.Validate(resource);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Error al crear recurso: {string.Join("; ", validation.Errors)}");

        var exists = await repository.ExistsByCodeAsync(resource.Code, ct: ct);
        if (exists)
            throw new InvalidOperationException($"Ya existe un recurso con el código '{resource.Code}'.");

        await repository.SaveAsync(resource, ct);
        return resource;
    }

    public async Task<Resource> UpdateResourceAsync(Resource resource, CancellationToken ct = default)
    {
        var existing = await repository.GetByIdAsync(resource.Id, ct);
        if (existing == null)
            throw new InvalidOperationException($"Recurso '{resource.Id}' no encontrado.");

        var validation = ResourceValidator.Validate(resource);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Error al actualizar recurso: {string.Join("; ", validation.Errors)}");

        var duplicate = await repository.ExistsByCodeAsync(resource.Code, resource.Id, ct);
        if (duplicate)
            throw new InvalidOperationException($"Ya existe otro recurso con el código '{resource.Code}'.");

        await repository.SaveAsync(resource, ct);
        return resource;
    }

    public async Task<Resource> DeactivateResourceAsync(string id, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(id, ct);
        if (resource == null)
            throw new InvalidOperationException($"Recurso '{id}' no encontrado.");

        resource = resource with { IsActive = false, ModifiedAt = DateTime.UtcNow };
        await repository.SaveAsync(resource, ct);
        return resource;
    }

    public async Task<List<Resource>> SearchAsync(string? query = null, ResourceType? type = null, CancellationToken ct = default)
    {
        var all = await repository.GetAllAsync(ct);
        var result = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToUpperInvariant();
            result = result.Where(r =>
                r.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (type.HasValue)
            result = result.Where(r => r.Type == type.Value);

        return result.OrderBy(r => r.Code).ToList();
    }

    public async Task<ResourcePriceHistoryEntry> RecordPriceAsync(string resourceId, decimal price, string currency,
        DateTime effectiveDate, string userId, string source = "Manual", string? comment = null, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource == null)
            throw new InvalidOperationException($"Recurso '{resourceId}' no encontrado.");

        var entry = new ResourcePriceHistoryEntry
        {
            ResourceId = resourceId,
            Price = price,
            Currency = currency,
            EffectiveDate = effectiveDate,
            UserId = userId,
            Source = source,
            Comment = comment ?? "",
            CreatedAt = DateTime.UtcNow
        };

        var validation = ResourceValidator.ValidatePriceHistory(entry);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Error al registrar precio: {string.Join("; ", validation.Errors)}");

        await priceHistory.AddAsync(entry, ct);

        var updated = resource with
        {
            BasePrice = price,
            Currency = currency,
            PriceDate = effectiveDate,
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = userId
        };
        await repository.SaveAsync(updated, ct);

        return entry;
    }

    public async Task<List<ResourcePriceHistoryEntry>> GetPriceHistoryAsync(string resourceId, CancellationToken ct = default)
    {
        return await priceHistory.GetByResourceIdAsync(resourceId, ct);
    }

    public async Task<decimal> ConvertCurrencyAsync(decimal amount, string from, string to, DateTime? date = null, CancellationToken ct = default)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return amount;

        var rate = date.HasValue
            ? await exchangeRates.GetByDateAsync(from, to, date.Value, ct)
            : await exchangeRates.GetLatestAsync(from, to, ct);

        if (rate == null)
            throw new InvalidOperationException($"No hay tipo de cambio disponible de {from} a {to}.");

        return PrecisionPolicy.RoundImporte(amount * rate.Rate);
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesAsync(CancellationToken ct = default)
    {
        return await exchangeRates.GetAllAsync(ct);
    }

    public async Task<ExchangeRate> AddExchangeRateAsync(ExchangeRate rate, CancellationToken ct = default)
    {
        var validation = ResourceValidator.ValidateExchangeRate(rate);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Error al registrar tipo de cambio: {string.Join("; ", validation.Errors)}");

        await exchangeRates.AddAsync(rate, ct);
        return rate;
    }
}
