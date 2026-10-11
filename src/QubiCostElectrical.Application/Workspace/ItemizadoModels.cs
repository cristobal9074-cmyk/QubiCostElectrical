namespace QubiCostElectrical;

public enum ItemizadoFilterMode
{
    Todos,
    Incompletos,
    Completos,
    SinCubicar,
    SinPrecio
}

public sealed record ItemizadoSummaryMetrics(
    int TotalPartidas,
    int Completas,
    int Incompletas,
    int SinCubicar,
    int SinPrecio,
    decimal CompletedPct,
    decimal TotalUfCubicadas);

public sealed class ItemizadoRowPresentation
{
    public required int SourceIndex { get; init; }
    public required string Id { get; init; }
    public required string ParentId { get; init; }
    public required string Item { get; init; }
    public required string Descripcion { get; init; }
    public required string Unidad { get; init; }
    public required decimal Cantidad { get; init; }
    public required bool EsTitulo { get; init; }
    public required bool ExcluidoCubicacion { get; init; }
    public required PartidaCompletionState CompletionState { get; init; }
    public required decimal CompletionPct { get; init; }
    public required decimal UnitPriceUf { get; init; }
    public decimal TotalUf { get; set; }
    public bool HasPrice { get; set; }
    public required string StatusLabel { get; init; }
    public required string StatusKind { get; init; }
}

public sealed record ItemizadoPageState(
    IReadOnlyList<ItemizadoRowPresentation> Rows,
    ItemizadoSummaryMetrics Summary,
    PartidaCompletionSummary Completion);
