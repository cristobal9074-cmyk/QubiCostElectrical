using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed record ApuNavContext(string ItemId, string CodigoItem, string DescripcionItem)
    : WorkspaceNavigationContext;
