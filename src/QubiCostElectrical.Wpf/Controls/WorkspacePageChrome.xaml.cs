using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Controls;

/// <summary>Envoltorio estándar: contenido + empty + skeleton. Ver docs/UI_SCREEN_CONTRACT.md.</summary>
public partial class WorkspacePageChrome : UserControl
{
    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(WorkspacePageChrome));

    public static readonly DependencyProperty ShowEmptyStateProperty =
        DependencyProperty.Register(nameof(ShowEmptyState), typeof(bool), typeof(WorkspacePageChrome));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(WorkspacePageChrome));

    public static readonly DependencyProperty EmptyTitleProperty =
        DependencyProperty.Register(nameof(EmptyTitle), typeof(string), typeof(WorkspacePageChrome),
            new PropertyMetadata("Sin proyecto abierto"));

    public static readonly DependencyProperty EmptyDescriptionProperty =
        DependencyProperty.Register(nameof(EmptyDescription), typeof(string), typeof(WorkspacePageChrome),
            new PropertyMetadata("Abra o cree un proyecto para continuar."));

    public static readonly DependencyProperty EmptyIconProperty =
        DependencyProperty.Register(nameof(EmptyIcon), typeof(string), typeof(WorkspacePageChrome));

    public static readonly DependencyProperty EmptyActionsProperty =
        DependencyProperty.Register(nameof(EmptyActions), typeof(object), typeof(WorkspacePageChrome));

    public static readonly DependencyProperty LoadingMessageProperty =
        DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(WorkspacePageChrome),
            new PropertyMetadata("Cargando…"));

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool ShowEmptyState
    {
        get => (bool)GetValue(ShowEmptyStateProperty);
        set => SetValue(ShowEmptyStateProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string EmptyTitle
    {
        get => (string)GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string EmptyDescription
    {
        get => (string)GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    public string? EmptyIcon
    {
        get => (string?)GetValue(EmptyIconProperty);
        set => SetValue(EmptyIconProperty, value);
    }

    public object? EmptyActions
    {
        get => GetValue(EmptyActionsProperty);
        set => SetValue(EmptyActionsProperty, value);
    }

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public WorkspacePageChrome() => InitializeComponent();
}
