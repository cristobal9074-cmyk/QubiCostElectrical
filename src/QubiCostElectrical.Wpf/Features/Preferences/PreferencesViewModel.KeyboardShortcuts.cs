using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed class ShortcutCategoryItemVm : ViewModelBase
{
    private bool isSelected;
    private int count;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Icon { get; init; }

    public int Count
    {
        get => count;
        set
        {
            if (SetProperty(ref count, value))
                OnPropertyChanged(nameof(DisplayText));
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string DisplayText => $"{Icon} {Title} ({Count})";
}

public sealed partial class PreferencesViewModel
{
    private static readonly HashSet<string> ShellReservedGestures = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alt+1", "Alt+2", "Alt+3", "Alt+Shift+3", "Alt+4", "Alt+5", "Alt+6", "Alt+7", "Alt+8", "Alt+9", "Alt+0",
        "Alt+P", "Alt+O", "Alt+M", "Alt+C", "Ctrl+K", "F1"
    };

    public ObservableCollection<ShortcutCategoryItemVm> ShortcutCategories { get; } = new();
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> FilteredShortcuts { get; } = new();
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> AllShortcutRows { get; } = new();

    // Compatibilidad previa
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> CubicacionShortcuts { get; } = new();
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> ShellShortcuts { get; } = new();

    private ShortcutCategoryItemVm? selectedShortcutCategory;
    private string shortcutSearchFilter = "";
    private KeyboardShortcutPreferenceRowVm? activeCaptureRow;
    private string shortcutCaptureMessage = "";

    public ShortcutCategoryItemVm? SelectedShortcutCategory
    {
        get => selectedShortcutCategory;
        set
        {
            if (SetProperty(ref selectedShortcutCategory, value))
            {
                foreach (var cat in ShortcutCategories)
                    cat.IsSelected = ReferenceEquals(cat, value);
                ApplyShortcutFilter();
            }
        }
    }

    public string ShortcutSearchFilter
    {
        get => shortcutSearchFilter;
        set
        {
            if (SetProperty(ref shortcutSearchFilter, value))
            {
                ApplyShortcutFilter();
            }
        }
    }

    public string ShortcutCaptureMessage
    {
        get => shortcutCaptureMessage;
        private set => SetProperty(ref shortcutCaptureMessage, value);
    }

    public bool IsShortcutCaptureActive => activeCaptureRow != null;

    private void InitializeShortcutCategories()
    {
        if (ShortcutCategories.Count > 0) return;

        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = "all", Title = "Todos", Icon = "📑" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = ItemizadoShortcutCatalog.Scope, Title = "Itemizado", Icon = "📋" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = CubicacionShortcutCatalog.Scope, Title = "Cubicación", Icon = "📐" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = CubicacionScShortcutCatalog.Scope, Title = "Cubicación SC", Icon = "⚡" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = ApuShortcutCatalog.Scope, Title = "APU", Icon = "📊" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = PricesShortcutCatalog.Scope, Title = "Precios y Cotizaciones", Icon = "💰" });
        ShortcutCategories.Add(new ShortcutCategoryItemVm { Id = ShellShortcutCatalog.Scope, Title = "Global / Shell", Icon = "🌐" });
    }

    private void LoadShortcutsFromDraft()
    {
        InitializeShortcutCategories();

        AllShortcutRows.Clear();
        CubicacionShortcuts.Clear();
        ShellShortcuts.Clear();

        foreach (var def in KeyboardShortcutCatalogRegistry.All)
        {
            var effective = KeyboardShortcutPreferences.GetEffective(def.Id, _draft);
            var row = new KeyboardShortcutPreferenceRowVm(
                def.Id,
                def.Scope,
                KeyboardShortcutCatalogRegistry.GetScopeLabel(def.Scope),
                KeyboardShortcutCatalogRegistry.GetScopeIcon(def.Scope),
                def.Label,
                def.Description,
                def.DefaultGesture,
                def.IsConfigurable)
            {
                EffectiveGesture = effective
            };

            AllShortcutRows.Add(row);

            if (def.Scope == CubicacionShortcutCatalog.Scope)
                CubicacionShortcuts.Add(row);
            else if (def.Scope == ShellShortcutCatalog.Scope)
                ShellShortcuts.Add(row);
        }

        // Actualizar contadores
        UpdateCategoryCounts();

        if (SelectedShortcutCategory == null)
            SelectedShortcutCategory = ShortcutCategories.FirstOrDefault(c => c.Id == "all") ?? ShortcutCategories.FirstOrDefault();
        else
            ApplyShortcutFilter();
    }

    private void UpdateCategoryCounts()
    {
        foreach (var cat in ShortcutCategories)
        {
            if (cat.Id == "all")
                cat.Count = AllShortcutRows.Count;
            else
                cat.Count = AllShortcutRows.Count(r => string.Equals(r.Scope, cat.Id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void SelectShortcutCategory(string categoryId)
    {
        var found = ShortcutCategories.FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));
        if (found != null)
            SelectedShortcutCategory = found;
    }

    public void ApplyShortcutFilter()
    {
        FilteredShortcuts.Clear();
        var selectedScope = SelectedShortcutCategory?.Id ?? "all";
        var query = (ShortcutSearchFilter ?? "").Trim().ToLowerInvariant();
        var queryNoSpaces = query.Replace(" ", "", StringComparison.Ordinal);

        foreach (var row in AllShortcutRows)
        {
            if (selectedScope != "all" && !string.Equals(row.Scope, selectedScope, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(query))
            {
                var gestureDisplayNoSpaces = row.GestureDisplay.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
                var match = row.Label.ToLowerInvariant().Contains(query)
                    || row.Description.ToLowerInvariant().Contains(query)
                    || row.ScopeLabel.ToLowerInvariant().Contains(query)
                    || row.EffectiveGesture.ToLowerInvariant().Contains(query)
                    || row.GestureDisplay.ToLowerInvariant().Contains(query)
                    || gestureDisplayNoSpaces.Contains(queryNoSpaces);
                if (!match)
                    continue;
            }

            FilteredShortcuts.Add(row);
        }
    }

    private void SyncShortcutsToDraft()
    {
        _draft.KeyboardShortcuts ??= new Dictionary<string, string>(StringComparer.Ordinal);
        _draft.KeyboardShortcuts.Clear();

        foreach (var row in AllShortcutRows.Where(r => r.IsConfigurable))
        {
            if (string.Equals(row.EffectiveGesture, row.DefaultGesture, StringComparison.OrdinalIgnoreCase))
                continue;
            _draft.KeyboardShortcuts[row.ActionId] = row.EffectiveGesture;
        }
    }

    public void BeginShortcutCapture(KeyboardShortcutPreferenceRowVm row)
    {
        if (!row.IsConfigurable)
            return;

        activeCaptureRow = row;
        foreach (var item in AllShortcutRows)
            item.IsCapturing = ReferenceEquals(item, row);
        ShortcutCaptureMessage = $"Capturando atajo para «{row.Label}» ({row.ScopeLabel}). Presione la combinación deseada (Esc cancela).";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
    }

    public void CancelShortcutCapture()
    {
        activeCaptureRow = null;
        foreach (var item in AllShortcutRows)
            item.IsCapturing = false;
        ShortcutCaptureMessage = "";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
    }

    public bool HandleShortcutCaptureKey(KeyEventArgs e)
    {
        if (activeCaptureRow == null)
            return false;

        if (e.Key == Key.Escape)
        {
            CancelShortcutCapture();
            e.Handled = true;
            return true;
        }

        if (!WpfKeyboardShortcutExtensions.TryCapture(e, out var gestureText) || gestureText == null)
        {
            e.Handled = true;
            return true;
        }

        if (!TryApplyCapturedShortcut(activeCaptureRow, gestureText))
        {
            var shellConflict = ShellReservedGestures.Contains(gestureText);
            var conflicting = AllShortcutRows.FirstOrDefault(r => !ReferenceEquals(r, activeCaptureRow)
                && string.Equals(r.EffectiveGesture, gestureText, StringComparison.OrdinalIgnoreCase));

            string reason = shellConflict
                ? "Esa combinación está reservada para la navegación global del shell (Alt+1…0, Alt+P, Ctrl+K, F1). Elija otra."
                : conflicting != null
                    ? $"Esa combinación ya está asignada a «{conflicting.Label}» en el módulo {conflicting.ScopeLabel}. Elija otra."
                    : "No se pudo asignar el atajo de teclado seleccionado.";

            WpfDialogService.Warning("Atajo no disponible", reason);
            e.Handled = true;
            return true;
        }

        ShortcutCaptureMessage = "";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
        e.Handled = true;
        return true;
    }

    public bool TryApplyCapturedShortcut(KeyboardShortcutPreferenceRowVm row, string gestureText)
    {
        if (!KeyboardShortcutGesture.TryParse(gestureText, out _))
            return false;

        if (ShellReservedGestures.Contains(gestureText))
            return false;

        foreach (var other in AllShortcutRows)
        {
            if (ReferenceEquals(other, row)) continue;
            if (string.Equals(other.EffectiveGesture, gestureText, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        row.EffectiveGesture = gestureText;
        row.IsCapturing = false;
        activeCaptureRow = null;
        HasPendingChanges = true;
        return true;
    }

    public void ResetShortcut(KeyboardShortcutPreferenceRowVm row)
    {
        row.ResetToDefault();
        HasPendingChanges = true;
    }

    public void ResetCurrentCategoryShortcuts()
    {
        var targetScope = SelectedShortcutCategory?.Id ?? "all";
        foreach (var row in AllShortcutRows.Where(r => r.IsConfigurable))
        {
            if (targetScope == "all" || string.Equals(row.Scope, targetScope, StringComparison.OrdinalIgnoreCase))
                row.ResetToDefault();
        }
        HasPendingChanges = true;
    }

    public void ResetAllCubicacionShortcuts()
    {
        foreach (var row in CubicacionShortcuts.Where(r => r.IsConfigurable))
            row.ResetToDefault();
        HasPendingChanges = true;
    }

    public void ResetAllShellShortcuts()
    {
        foreach (var row in ShellShortcuts.Where(r => r.IsConfigurable))
            row.ResetToDefault();
        HasPendingChanges = true;
    }

    public void ResetAllShortcuts()
    {
        foreach (var row in AllShortcutRows.Where(r => r.IsConfigurable))
            row.ResetToDefault();
        HasPendingChanges = true;
    }
}
