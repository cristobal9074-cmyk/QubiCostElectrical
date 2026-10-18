using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PreferencesViewModelTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"prefs_wpf_{Guid.NewGuid():N}", "preferences.json");

    // ─── Carga ────────────────────────────────────────────────────────────

    [Fact]
    public void InitialLoad_DoesNotThrow()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.NotNull(vm);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void InitialLoad_DefaultProjectFolder_IsEmpty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.Equal("", vm.DefaultProjectFolder);
    }

    [Fact]
    public void InitialLoad_FromExistingFile_RestoresValues()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Guardada", DefaultProjectFolder = "" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("Empresa Guardada", vm.CompanyName);
    }

    // ─── Cambios pendientes ───────────────────────────────────────────────

    [Fact]
    public void SettingProperty_SetsPendingChanges()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Test SA";

        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void SettingDefaultProjectFolder_SetsPendingChanges()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.DefaultProjectFolder = Path.GetTempPath();

        Assert.True(vm.HasPendingChanges);
    }

    // ─── Guardar / TrySave ────────────────────────────────────────────────

    [Fact]
    public void TrySave_ValidData_PersistsToStore()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Constructora ABC Ltda.";
        vm.Rut = "12.345.678-9";
        vm.Email = "contacto@abc.cl";
        vm.TrySave();

        Assert.False(vm.HasPendingChanges);
        Assert.Null(vm.ValidationError);

        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Constructora ABC Ltda.", loaded.LetterheadCompanyName);
        Assert.Equal("12.345.678-9", loaded.LetterheadRut);
    }

    [Fact]
    public void TrySave_Persists_DefaultProjectFolder()
    {
        var path = TempPath();
        var folder = Path.GetTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.DefaultProjectFolder = folder;
        vm.TrySave();

        var loaded = UserPreferencesStore.Load();
        Assert.Equal(folder, loaded.DefaultProjectFolder);
    }

    [Fact]
    public void SaveAndReopen_PersistsValues()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Reabierta";
        vm.TrySave();

        var vm2 = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("Empresa Reabierta", vm2.CompanyName);
    }

    // ─── ApplyChanges ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyChanges_ValidData_ReturnsTrueAndSetsStatus()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Test Apply";
        var result = vm.ApplyChanges();

        Assert.True(result);
        Assert.False(vm.HasPendingChanges);
        Assert.NotEmpty(vm.SaveStatus);
    }

    [Fact]
    public void ApplyChanges_InvalidEmail_ReturnsFalse()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.Email = "no-es-email";
        var result = vm.ApplyChanges();

        Assert.False(result);
        Assert.NotNull(vm.ValidationError);
    }

    // ─── Cancelar / Discard ───────────────────────────────────────────────

    [Fact]
    public void Discard_RestoresOriginalValues()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Cambio temporal";
        vm.Discard();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("", vm.CompanyName);
    }

    [Fact]
    public void Cancel_DoesNotPersistChanges()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "No debe persistir";
        vm.Cancel();

        var loaded = UserPreferencesStore.Load();
        Assert.NotEqual("No debe persistir", loaded.LetterheadCompanyName);
    }

    // ─── Restaurar predeterminados ────────────────────────────────────────

    [Fact]
    public void RestoreDefaults_WhenNotConfirmed_NoChange()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Existente";
        vm.TrySave();

        var vm2 = new PreferencesViewModel(WpfAppHost.Current);
        var result = vm2.RestoreDefaults(() => false);

        Assert.False(result);
        Assert.Equal("Empresa Existente", vm2.CompanyName);
    }

    [Fact]
    public void RestoreDefaults_WhenConfirmed_ResetsDraft()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Original" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.RestoreDefaults(() => true);

        Assert.Equal("", vm.CompanyName);
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void RestoreDefaults_BeforeSave_DoesNotPersist()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Persistida" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.RestoreDefaults(() => true); // reset draft but no save

        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Persistida", loaded.LetterheadCompanyName);
    }

    // ─── Validaciones ─────────────────────────────────────────────────────

    [Fact]
    public void TrySave_InvalidEmail_SetsValidationError()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.Email = "no-es-correo";
        vm.TrySave();

        Assert.NotNull(vm.ValidationError);
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void DefaultProjectFolder_EmptyString_NoValidationError()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.DefaultProjectFolder = "";
        vm.TrySave();

        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void DefaultProjectFolder_ValidExistingPath_NoError()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.DefaultProjectFolder = Path.GetTempPath();
        var ok = vm.ApplyChanges();

        Assert.True(ok);
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void DefaultProjectFolder_NonexistentPath_SetsValidationError()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.DefaultProjectFolder = @"C:\no\existe\carpeta_xyz_99999";
        vm.TrySave();

        Assert.NotNull(vm.ValidationError);
        Assert.True(vm.HasPendingChanges);
    }

    // ─── Archivo corrupto ─────────────────────────────────────────────────

    [Fact]
    public void CorruptedPreferencesFile_LoadsDefaults()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);
        File.WriteAllText(path, "{ invalid json ::::");

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("", vm.CompanyName);
        Assert.False(vm.HasPendingChanges);
    }

    // ─── Diagnóstico ─────────────────────────────────────────────────────

    [Fact]
    public void AppVersion_IsNotEmpty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.NotEmpty(vm.AppVersion);
        Assert.Contains("v", vm.AppVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogsFolderPath_ContainsAppName()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.NotEmpty(vm.LogsFolderPath);
        Assert.Contains("QubiCostElectrical", vm.LogsFolderPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecoveryFolderPath_IsNotEmpty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.NotEmpty(vm.RecoveryFolderPath);
    }

    [Fact]
    public void OpenLogsFolder_CallsFileOpenService()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        string? calledPath = null;
        var fakeOpener = new FakeFileOpener(path => calledPath = path);

        vm.OpenLogsFolder(fakeOpener);

        Assert.NotNull(calledPath);
        Assert.Contains("QubiCostElectrical", calledPath!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenRecoveryFolder_CallsFileOpenService()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        string? calledPath = null;
        var fakeOpener = new FakeFileOpener(path => calledPath = path);

        vm.OpenRecoveryFolder(fakeOpener);

        Assert.NotNull(calledPath);
    }

    // ─── Aislamiento de proyecto ──────────────────────────────────────────

    [Fact]
    public void PreferencesViewModel_DoesNotModifyProjectInfo()
    {
        WpfTestHost.EnsureInitialized();
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var host = WpfAppHost.Current;
        host.Session.NewProject("prefs-isolation-test");
        var projectNameBefore = host.Session.ProjectInfo.Nombre;

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa que no debe tocar el proyecto";
        vm.TrySave();

        Assert.Equal(projectNameBefore, host.Session.ProjectInfo.Nombre);
        Assert.False(host.Session.IsDirty);
    }

    // ─── Navegación y Quick Search ────────────────────────────────────────

    [Fact]
    public void NavigationRoute_Preferences_IsRegistered()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.Preferences);
        Assert.Equal(ShellRoute.Preferences, info.Route);
        Assert.NotEmpty(info.Title);
    }

    [Fact]
    public void QuickSearch_ContainsPreferences()
    {
        WpfTestHost.EnsureInitialized();
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var svm = new ShellViewModel(WpfAppHost.Current);
        svm.PostCloseNavigate = () => { };

        svm.SearchText = "Prefer";

        Assert.Contains(svm.QuickSearchResults, r =>
            r.Title.Contains("Prefer", StringComparison.OrdinalIgnoreCase) ||
            r.Subtitle.Contains("Prefer", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Aislamiento de tests ─────────────────────────────────────────────

    [Fact]
    public void Tests_DoNotWriteToRealLocalAppData()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var activePath = UserPreferencesStore.PreferencesFilePath;
        Assert.DoesNotContain("QubiCostElectricalPRO", activePath, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Membrete profesional (Bloque 3 — campos presentes) ──────────────

    [Fact]
    public void ProfessionalFields_PersistAndReload()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.ProfessionalName = "Juan Pérez";
        vm.ProfessionalRole = "Ingeniero Eléctrico";
        vm.ProfessionalRegistroSec = "SEC-1234";
        vm.TrySave();

        var vm2 = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("Juan Pérez", vm2.ProfessionalName);
        Assert.Equal("Ingeniero Eléctrico", vm2.ProfessionalRole);
        Assert.Equal("SEC-1234", vm2.ProfessionalRegistroSec);
    }

    // ─── PIN de seguridad (regresión) ──────────────────────────────────────
    // Clone() (llamado en el constructor y en Discard()) accedía a src.PinSecurity.MinLength
    // sin "?." — si preferences.json venía de una versión anterior a este campo (o fue editado
    // a mano) y PinSecurity quedaba null, la pantalla de Preferencias crasheaba con
    // NullReferenceException apenas se abría.

    [Fact]
    public void InitialLoad_WithNullPinSecurity_DoesNotThrowAndUsesDefaults()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { PinSecurity = null! };
        UserPreferencesStore.Save(prefs);

        var ex = Record.Exception(() => new PreferencesViewModel(WpfAppHost.Current));

        Assert.Null(ex);
    }

    [Fact]
    public void InitialLoad_WithNullPinSecurity_LoadsDefaultPinValues()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { PinSecurity = null! };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.Equal(4, vm.PinMinLength);
        Assert.Equal(6, vm.PinMaxLength);
        Assert.True(vm.PinBlockSequences);
        Assert.True(vm.PinBlockRepeats);
    }

    [Fact]
    public void InitialLoad_WithPinSecurity_PreservesConfiguredValues()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences
        {
            PinSecurity = new PinSecurityPolicy { MinLength = 5, MaxLength = 8, BlockSequences = false, BlockRepeats = false }
        };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.Equal(5, vm.PinMinLength);
        Assert.Equal(8, vm.PinMaxLength);
        Assert.False(vm.PinBlockSequences);
        Assert.False(vm.PinBlockRepeats);
    }

    [Fact]
    public void PreferencesViewModel_LoadsAllCubicacionAndShellShortcuts()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        // Verify that newly added shortcuts exist in the collection
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.AddPartida);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.DeletePartida);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.FillDown);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.FillRight);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.RepeatMeasurement);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.QuickJump);
        Assert.Contains(vm.CubicacionShortcuts, r => r.ActionId == CubicacionShortcutCatalog.SectorMatrix);

        Assert.True(vm.CubicacionShortcuts.Count >= 18);
        Assert.True(vm.ShellShortcuts.Count >= 4);
    }

    [Fact]
    public void PreferencesViewModel_ShortcutCategories_InitializedAndFilteredCorrectly()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        // Categorías inicializadas
        Assert.NotEmpty(vm.ShortcutCategories);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == "all");
        Assert.Contains(vm.ShortcutCategories, c => c.Id == ItemizadoShortcutCatalog.Scope);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == CubicacionShortcutCatalog.Scope);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == CubicacionScShortcutCatalog.Scope);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == ApuShortcutCatalog.Scope);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == PricesShortcutCatalog.Scope);
        Assert.Contains(vm.ShortcutCategories, c => c.Id == ShellShortcutCatalog.Scope);

        // Todas las categorías tienen conteos mayores a cero
        foreach (var cat in vm.ShortcutCategories)
            Assert.True(cat.Count > 0, $"Category {cat.Id} count should be > 0");

        // Conmutar a Itemizado
        vm.SelectShortcutCategory(ItemizadoShortcutCatalog.Scope);
        Assert.All(vm.FilteredShortcuts, r => Assert.Equal(ItemizadoShortcutCatalog.Scope, r.Scope));
        Assert.Contains(vm.FilteredShortcuts, r => r.ActionId == ItemizadoShortcutCatalog.AddPartida);

        // Conmutar a APU
        vm.SelectShortcutCategory(ApuShortcutCatalog.Scope);
        Assert.All(vm.FilteredShortcuts, r => Assert.Equal(ApuShortcutCatalog.Scope, r.Scope));
        Assert.Contains(vm.FilteredShortcuts, r => r.ActionId == ApuShortcutCatalog.AddMaterial);

        // Conmutar a Cubicación SC
        vm.SelectShortcutCategory(CubicacionScShortcutCatalog.Scope);
        Assert.All(vm.FilteredShortcuts, r => Assert.Equal(CubicacionScShortcutCatalog.Scope, r.Scope));
        Assert.Contains(vm.FilteredShortcuts, r => r.ActionId == CubicacionScShortcutCatalog.AddTablero);

        // Filtrado por texto (ej: "IntelliSense")
        vm.SelectShortcutCategory("all");
        vm.ShortcutSearchFilter = "IntelliSense";
        Assert.NotEmpty(vm.FilteredShortcuts);
        Assert.All(vm.FilteredShortcuts, r =>
            Assert.Contains("IntelliSense", r.Description + " " + r.Label, StringComparison.OrdinalIgnoreCase));

        // Filtrado por texto de tecla (ej: "Ctrl+T")
        vm.ShortcutSearchFilter = "Ctrl+T";
        Assert.NotEmpty(vm.FilteredShortcuts);
        Assert.Contains(vm.FilteredShortcuts, r => r.ActionId == CubicacionScShortcutCatalog.AddTablero);
    }

    [Fact]
    public void NavSections_HaveShortcutCues()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.Equal(7, vm.NavSections.Count);
        Assert.Equal("Ctrl+1", vm.NavSections[0].ShortcutCue);
        Assert.Equal("Ctrl+2", vm.NavSections[1].ShortcutCue);
        Assert.Equal("Ctrl+3", vm.NavSections[2].ShortcutCue);
        Assert.Equal("Ctrl+4", vm.NavSections[3].ShortcutCue);
        Assert.Equal("Ctrl+5", vm.NavSections[4].ShortcutCue);
        Assert.Equal("Ctrl+6", vm.NavSections[5].ShortcutCue);
        Assert.Equal("Ctrl+7", vm.NavSections[6].ShortcutCue);
    }

    // ─── Helper ───────────────────────────────────────────────────────────

    private sealed class FakeFileOpener(Action<string> onOpenFolder) : IFileOpenService
    {
        public FileOpenResult OpenFile(string filePath) => FileOpenResult.Success;
        public void OpenFolder(string folderPath) => onOpenFolder(folderPath);
    }
}
