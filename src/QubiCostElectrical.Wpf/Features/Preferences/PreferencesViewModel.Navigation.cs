using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public enum PreferencesSection
{
    Identity,
    General,
    Theme,
    Updates,
    Diagnostics,
    Users,
    Shortcuts
}

public sealed class PreferencesNavItemVm
{
    public PreferencesSection Section { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed partial class PreferencesViewModel
{
    private PreferencesSection selectedSection = PreferencesSection.Identity;

    public ObservableCollection<PreferencesNavItemVm> NavSections { get; } = new()
    {
        new() { Section = PreferencesSection.Identity, Title = "Identidad", Description = "Empresa y membrete" },
        new() { Section = PreferencesSection.General, Title = "General", Description = "Carpetas y rutas" },
        new() { Section = PreferencesSection.Theme, Title = "Apariencia", Description = "Tema claro/oscuro" },
        new() { Section = PreferencesSection.Updates, Title = "Actualizaciones", Description = "Canal y versión" },
        new() { Section = PreferencesSection.Diagnostics, Title = "Diagnóstico", Description = "Logs y recuperación" },
        new() { Section = PreferencesSection.Users, Title = "Usuarios", Description = "Cuentas y roles" },
        new() { Section = PreferencesSection.Shortcuts, Title = "Atajos", Description = "Teclado en cubicación" },
    };

    public PreferencesSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (!SetProperty(ref selectedSection, value))
                return;
            NotifySectionFlags();
        }
    }

    public PreferencesNavItemVm? SelectedNavItem
    {
        get => NavSections.FirstOrDefault(x => x.Section == SelectedSection);
        set
        {
            if (value == null) return;
            SelectedSection = value.Section;
        }
    }

    public ICommand SelectSectionCommand =>
        selectSectionCommand ??= new RelayCommand<PreferencesSection>(section => SelectedSection = section);

    private RelayCommand<PreferencesSection>? selectSectionCommand;

    public bool IsIdentitySection => SelectedSection == PreferencesSection.Identity;
    public bool IsGeneralSection => SelectedSection == PreferencesSection.General;
    public bool IsThemeSection => SelectedSection == PreferencesSection.Theme;
    public bool IsUpdatesSection => SelectedSection == PreferencesSection.Updates;
    public bool IsDiagnosticsSection => SelectedSection == PreferencesSection.Diagnostics;
    public bool IsUsersSection => SelectedSection == PreferencesSection.Users;
    public bool IsShortcutsSection => SelectedSection == PreferencesSection.Shortcuts;

    private void InitializeNavigation()
    {
        NotifySectionFlags();
    }

    private void NotifySectionFlags()
    {
        OnPropertyChanged(nameof(IsIdentitySection));
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsThemeSection));
        OnPropertyChanged(nameof(IsUpdatesSection));
        OnPropertyChanged(nameof(IsDiagnosticsSection));
        OnPropertyChanged(nameof(IsUsersSection));
        OnPropertyChanged(nameof(IsShortcutsSection));
        OnPropertyChanged(nameof(SelectedNavItem));
    }
}
