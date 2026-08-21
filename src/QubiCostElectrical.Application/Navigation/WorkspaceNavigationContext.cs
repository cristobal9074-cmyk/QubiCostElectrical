namespace QubiCostElectrical;

public abstract record WorkspaceNavigationContext;

public sealed record TakeoffNavigationContext(
    string ItemId,
    string ItemCode,
    string Description) : WorkspaceNavigationContext;

public sealed record PriceBankNavigationContext(
    string SearchTerm) : WorkspaceNavigationContext;
