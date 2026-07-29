using QubiCostElectrical.Wpf.Features.Quotations;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.TechnicalEvaluation;

public sealed class TechnicalEvaluationWorkspaceViewModel : SessionAwareViewModelBase
{
    public TechnicalEvaluationWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host) =>
        Workspace = new QuotationWorkspaceViewModel(shell, host);

    public QuotationWorkspaceViewModel Workspace { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) { }
}
