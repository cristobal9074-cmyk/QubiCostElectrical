namespace QubiCostElectrical;

public sealed record ApuCatalogSearchRequest
{
    public string Query { get; init; } = "";

    public ApuCatalogFilter Filter { get; init; } = ApuCatalogFilter.ActiveOnly;

    public string Category { get; init; } = "";

    public string Owner { get; init; } = "";

    public string Tags { get; init; } = "";

    public int MaxResults { get; init; } = 100;

    public int Offset { get; init; }
}
