namespace QubiCostElectrical;

public sealed record Resource
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public ResourceType Type { get; init; } = ResourceType.Material;
    public string Unit { get; init; } = "UN";
    public decimal BasePrice { get; init; }
    public string Currency { get; init; } = "CLP";
    public DateTime? PriceDate { get; init; }
    public string Supplier { get; init; } = "";
    public decimal Yield { get; init; } = 1m;
    public decimal Waste { get; init; }
    public decimal AdjustmentFactor { get; init; } = 1m;
    public bool IsActive { get; init; } = true;
    public string Notes { get; init; } = "";
    public string PriceSource { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = "";
    public string ModifiedBy { get; init; } = "";

    public decimal EffectivePrice => PrecisionPolicy.RoundPrecioUnitario(BasePrice * AdjustmentFactor);
    public decimal EffectiveYield => Yield > 0 ? Yield : 1m;
    public bool HasPrice => BasePrice > 0;
    public bool IsValidForUse => IsActive && HasPrice && UnitOfMeasureCatalog.IsValid(Unit);
}

public sealed record ResourcePriceHistoryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ResourceId { get; init; } = "";
    public decimal Price { get; init; }
    public string Currency { get; init; } = "CLP";
    public DateTime EffectiveDate { get; init; } = DateTime.UtcNow;
    public string Supplier { get; init; } = "";
    public string Source { get; init; } = "";
    public string UserId { get; init; } = "";
    public string Comment { get; init; } = "";
    public string OriginProject { get; init; } = "";
    public string Status { get; init; } = "Vigente";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
