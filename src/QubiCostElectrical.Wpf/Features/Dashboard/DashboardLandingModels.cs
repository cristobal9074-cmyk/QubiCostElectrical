using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public enum DashboardProjectStateKind
{
    Draft,
    InProgress,
    Complete,
    Unknown
}

public sealed class DashboardMetricVm
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Caption { get; init; } = "";
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
}

public sealed class DashboardRecentProjectVm
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string DisplayPath => Path;
    public string StatusText { get; init; } = "";
    public string StatusCaption { get; init; } = "";
    public string DisplayDate { get; init; } = "";
    public bool IsMissing { get; init; }
    public bool IsPinned { get; init; }
    public DashboardProjectStateKind StateKind { get; init; }
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
    public Brush StatusForeground { get; init; } = DashboardKpiBrushes.Blue;

    public ICommand? OpenCommand { get; init; }
    public ICommand? OpenFolderCommand { get; init; }
    public ICommand? CopyPathCommand { get; init; }
    public ICommand? TogglePinCommand { get; init; }
    public ICommand? RemoveRecentCommand { get; init; }
}
