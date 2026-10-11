using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed class KeyboardShortcutPreferenceRowVm : ViewModelBase
{
    private string effectiveGesture = "";
    private string captureHint = "";
    private bool isCapturing;

    public KeyboardShortcutPreferenceRowVm(string actionId, string label, string? description, string defaultGesture)
        : this(actionId, "cubicacion", "Cubicación", "📐", label, description, defaultGesture, isConfigurable: true)
    {
    }

    public KeyboardShortcutPreferenceRowVm(
        string actionId,
        string scope,
        string scopeLabel,
        string scopeIcon,
        string label,
        string? description,
        string defaultGesture,
        bool isConfigurable = true)
    {
        ActionId = actionId;
        Scope = scope;
        ScopeLabel = scopeLabel;
        ScopeIcon = scopeIcon;
        Label = label;
        Description = description ?? "";
        DefaultGesture = defaultGesture;
        effectiveGesture = defaultGesture;
        IsConfigurable = isConfigurable;
    }

    public string ActionId { get; }
    public string Scope { get; }
    public string ScopeLabel { get; }
    public string ScopeIcon { get; }
    public string Label { get; }
    public string Description { get; }
    public string DefaultGesture { get; }
    public bool IsConfigurable { get; }

    public string EffectiveGesture
    {
        get => effectiveGesture;
        set
        {
            if (SetProperty(ref effectiveGesture, value))
            {
                OnPropertyChanged(nameof(IsCustomized));
                OnPropertyChanged(nameof(GestureDisplay));
                OnPropertyChanged(nameof(StatusBadge));
            }
        }
    }

    public string GestureDisplay =>
        KeyboardShortcutGesture.TryParse(EffectiveGesture, out var gesture) && gesture != null
            ? gesture.ToDisplayString()
            : EffectiveGesture;

    public bool IsCustomized =>
        IsConfigurable && !string.Equals(EffectiveGesture, DefaultGesture, StringComparison.OrdinalIgnoreCase);

    public string StatusBadge
    {
        get
        {
            if (!IsConfigurable) return "Nativo";
            return IsCustomized ? "Personalizado" : "Por defecto";
        }
    }

    public bool IsCapturing
    {
        get => isCapturing;
        set
        {
            if (SetProperty(ref isCapturing, value))
            {
                CaptureHint = value ? "Presione la combinación deseada… (Esc cancela)" : "";
            }
        }
    }

    public string CaptureHint
    {
        get => captureHint;
        private set => SetProperty(ref captureHint, value);
    }

    public void ResetToDefault()
    {
        if (IsConfigurable)
            EffectiveGesture = DefaultGesture;
    }
}
