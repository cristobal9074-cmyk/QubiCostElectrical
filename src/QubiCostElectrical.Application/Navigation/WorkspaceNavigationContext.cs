namespace QubiCostElectrical;

public abstract record WorkspaceNavigationContext;

public sealed record TakeoffNavigationContext(
    string ItemId,
    string ItemCode,
    string Description,
    string? TargetRowId = null,
    string? TargetCode = null) : WorkspaceNavigationContext;

public sealed record PriceBankNavigationContext(
    string SearchTerm) : WorkspaceNavigationContext;

public sealed record ItemizadoNavigationContext(
    string? TargetRowId = null,
    string? TargetCode = null,
    ItemizadoFilterMode? FilterMode = null) : WorkspaceNavigationContext;

public sealed record CubicacionScNavigationContext(
    string? CuadroId = null,
    string? CuadroNombre = null,
    string? CircuitoId = null) : WorkspaceNavigationContext;
