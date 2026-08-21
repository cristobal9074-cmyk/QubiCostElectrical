using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Shell;

public sealed class ShellWorkflowStepVm
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public int StepNumber { get; init; }
    public bool IsComplete { get; init; }
    public bool IsActive { get; init; }
    public bool IsAuxiliary { get; init; }
    public ICommand? NavigateCommand { get; init; }
}
