using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed class KeyboardShortcutPreferenceRowVm : ViewModelBase
{
    private string effectiveGesture = "";
    private string captureHint = "";
    private bool isCapturing;

    public KeyboardShortcutPreferenceRowVm(string actionId, string label, string? description, string defaultGesture)
    {
        ActionId = actionId;
        Label = label;
        Description = description ?? "";
        DefaultGesture = defaultGesture;
        effectiveGesture = defaultGesture;
    }

    public string ActionId { get; }
    public string Label { get; }
    public string Description { get; }
    public string DefaultGesture { get; }

    public string EffectiveGesture
    {
        get => effectiveGesture;
        set
        {
            if (SetProperty(ref effectiveGesture, value))
            {
                OnPropertyChanged(nameof(IsCustomized));
                OnPropertyChanged(nameof(GestureDisplay));
            }
        }
    }

    public string GestureDisplay =>
        KeyboardShortcutGesture.TryParse(EffectiveGesture, out var gesture) && gesture != null
            ? gesture.ToDisplayString()
            : EffectiveGesture;

    public bool IsCustomized =>
        !string.Equals(EffectiveGesture, DefaultGesture, StringComparison.OrdinalIgnoreCase);

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

    public void ResetToDefault() => EffectiveGesture = DefaultGesture;
}
