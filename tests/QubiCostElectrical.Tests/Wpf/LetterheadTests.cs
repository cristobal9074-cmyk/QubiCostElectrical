using System.Drawing;
using System.Drawing.Imaging;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class LetterheadTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"lh_wpf_{Guid.NewGuid():N}", "preferences.json");

    private static string CreateSmallPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lh_img_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "logo.png");
        using var bmp = new Bitmap(10, 10);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    private static string CreateSmallJpg()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lh_img_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "firma.jpg");
        using var bmp = new Bitmap(10, 10);
        bmp.Save(path, ImageFormat.Jpeg);
        return path;
    }

    // ─── 1. Carga de identidad existente ─────────────────────────────────

    [Fact]
    public void InitialLoad_GlobalIdentity_LoadsFromStore()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences
        {
            LetterheadCompanyName = "Empresa Cargada",
            LetterheadRut = "76.123.456-7",
            ProfessionalName = "Ing. Pérez"
        };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("Empresa Cargada", vm.CompanyName);
        Assert.Equal("76.123.456-7", vm.Rut);
        Assert.Equal("Ing. Pérez", vm.ProfessionalName);
    }

    // ─── 2. Valores predeterminados ───────────────────────────────────────

    [Fact]
    public void InitialLoad_NewStore_DefaultsAreEmpty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);

        Assert.Equal("", vm.CompanyName);
        Assert.Equal("", vm.LogoPath);
        Assert.Equal("", vm.ProfessionalSignatureImagePath);
    }

    // ─── 3. Modificación marca el borrador ───────────────────────────────

    [Fact]
    public void Modify_CompanyName_SetsDirty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Nueva Empresa";
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void Modify_SignaturePath_SetsDirty()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.ProfessionalSignatureImagePath = "/ruta/firma.png";
        Assert.True(vm.HasPendingChanges);
    }

    // ─── 4. Cancelar no persiste ──────────────────────────────────────────

    [Fact]
    public void Cancel_DoesNotPersistLetterhead()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "No debe guardarse";
        vm.Cancel();

        var loaded = UserPreferencesStore.Load();
        Assert.NotEqual("No debe guardarse", loaded.LetterheadCompanyName);
    }

    // ─── 5. Aplicar actualiza preferencias ────────────────────────────────

    [Fact]
    public void Apply_PersistsLetterhead()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Aplicada";
        vm.Address = "Av. Principal 123";
        var ok = vm.ApplyChanges();

        Assert.True(ok);
        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Empresa Aplicada", loaded.LetterheadCompanyName);
        Assert.Equal("Av. Principal 123", loaded.LetterheadAddress);
    }

    // ─── 6. Guardar persiste ─────────────────────────────────────────────

    [Fact]
    public void Save_PersistsAllLetterheadFields()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Constructora XYZ";
        vm.Rut = "77.000.001-1";
        vm.Phone = "+56 2 2345 6789";
        vm.Email = "contacto@xyz.cl";
        vm.Website = "www.xyz.cl";
        vm.ProfessionalName = "Carlos Gómez";
        vm.ProfessionalRole = "Ingeniero Eléctrico";
        vm.ProfessionalRegistroSec = "SEC-9876";
        vm.TrySave();

        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Constructora XYZ", loaded.LetterheadCompanyName);
        Assert.Equal("77.000.001-1", loaded.LetterheadRut);
        Assert.Equal("Carlos Gómez", loaded.ProfessionalName);
        Assert.Equal("Ingeniero Eléctrico", loaded.ProfessionalRole);
        Assert.Equal("SEC-9876", loaded.ProfessionalRegistroSec);
    }

    // ─── 7. Reabrir recupera identidad ───────────────────────────────────

    [Fact]
    public void SaveAndReopen_PersistsLetterhead()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Persistente";
        vm.TrySave();

        var vm2 = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Equal("Empresa Persistente", vm2.CompanyName);
    }

    // ─── 8. Restaurar predeterminados solo modifica borrador ──────────────

    [Fact]
    public void RestoreDefaults_ResetsLetterheadInDraftOnly()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Guardada" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.RestoreDefaults(() => true);

        Assert.Equal("", vm.CompanyName);
        Assert.True(vm.HasPendingChanges);

        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Empresa Guardada", loaded.LetterheadCompanyName);
    }

    // ─── 9. Logo válido ───────────────────────────────────────────────────

    [Fact]
    public void ValidateLogo_ValidPng_ReturnsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var logoPath = CreateSmallPng();
        Assert.Null(vm.ValidateLogoPath(logoPath));
    }

    [Fact]
    public void ValidateLogo_ValidJpg_ReturnsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var logoPath = CreateSmallJpg();
        Assert.Null(vm.ValidateLogoPath(logoPath));
    }

    // ─── 10. Logo inexistente ─────────────────────────────────────────────

    [Fact]
    public void ValidateLogo_Nonexistent_ReturnsError()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var error = vm.ValidateLogoPath(@"C:\no\existe\logo.png");
        Assert.NotNull(error);
        Assert.Contains("no existe", error, StringComparison.OrdinalIgnoreCase);
    }

    // ─── 11. Extensión no soportada ───────────────────────────────────────

    [Fact]
    public void ValidateLogo_UnsupportedExtension_ReturnsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lh_ext_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "logo.bmp");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var error = vm.ValidateLogoPath(path);
        Assert.NotNull(error);
        Assert.Contains("xtensión", error, StringComparison.OrdinalIgnoreCase);
    }

    // ─── 12. Imagen corrupta ──────────────────────────────────────────────

    [Fact]
    public void ValidateLogo_EmptyFile_ReturnsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lh_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "empty.png");
        File.WriteAllBytes(path, Array.Empty<byte>());

        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var error = vm.ValidateLogoPath(path);
        Assert.NotNull(error);
    }

    // ─── 13. Firma válida ─────────────────────────────────────────────────

    [Fact]
    public void ValidateSignature_ValidPng_ReturnsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var sigPath = CreateSmallPng();
        Assert.Null(vm.ValidateSignaturePath(sigPath));
    }

    // ─── 14. Firma inexistente ────────────────────────────────────────────

    [Fact]
    public void ValidateSignature_Nonexistent_ReturnsError()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var error = vm.ValidateSignaturePath(@"C:\no\firma.jpg");
        Assert.NotNull(error);
    }

    // ─── 15. Logo vacío es aceptable ─────────────────────────────────────

    [Fact]
    public void ValidateLogo_Empty_ReturnsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Null(vm.ValidateLogoPath(""));
        Assert.Null(vm.ValidateLogoPath(null));
    }

    // ─── 16. Firma vacía es aceptable ────────────────────────────────────

    [Fact]
    public void ValidateSignature_Empty_ReturnsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        Assert.Null(vm.ValidateSignaturePath(""));
        Assert.Null(vm.ValidateSignaturePath(null));
    }

    // ─── 17. Datos opcionales vacíos ─────────────────────────────────────

    [Fact]
    public void Apply_WithEmptyOptionalFields_Succeeds()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Solo Nombre";
        var ok = vm.ApplyChanges();

        Assert.True(ok);
        Assert.Null(vm.ValidationError);
    }

    // ─── 18. Fallo de guardado conserva preferencias anteriores ──────────

    [Fact]
    public void TrySave_InvalidLogo_DoesNotPersistAndKeepsOldData()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Original" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = @"C:\no\existe\logo.bmp";
        vm.TrySave();

        Assert.NotNull(vm.ValidationError);
        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Empresa Original", loaded.LetterheadCompanyName);
    }

    // ─── 19. Identidad global no modifica el proyecto ────────────────────

    [Fact]
    public void SaveLetterhead_DoesNotMarkProjectDirty()
    {
        WpfTestHost.EnsureInitialized();
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var host = WpfAppHost.Current;
        host.Session.NewProject("membrete-isolation-test");

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Global";
        vm.TrySave();

        Assert.False(host.Session.IsDirty);
    }

    // ─── 20. OfferPdfSettings no es sobrescrito antes de exportar ─────────

    [Fact]
    public void OfferPdfSettings_NotOverwrittenByLetterheadSave()
    {
        WpfTestHost.EnsureInitialized();
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var host = WpfAppHost.Current;
        host.Session.NewProject("offer-isolation-test");
        host.Session.OfferPdfSettings.CompanyName = "Empresa del Proyecto";

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.CompanyName = "Empresa Global";
        vm.TrySave();

        Assert.Equal("Empresa del Proyecto", host.Session.OfferPdfSettings.CompanyName);
    }

    // ─── 21. Precedencia: global (Preferencias) sobre proyecto/oferta ─────

    [Fact]
    public void LetterheadApplicator_GlobalValuesWinOverProject()
    {
        var prefs = new UserPreferences
        {
            ProfessionalName = "Global Ing.",
            LetterheadCompanyName = "Empresa Global",
            LetterheadRut = "11.111.111-1",
            LetterheadLogoPath = "/global/logo.png"
        };
        var offer = new OfferPdfSettings
        {
            SignatureName = "Ing. Proyecto",
            CompanyName = "Empresa del Proyecto",
            CompanyRut = "22.222.222-2",
            LogoPath = "/proyecto/logo_proj.png"
        };

        LetterheadApplicator.Apply(prefs, offer);

        // Global gana: la identidad de Preferencias es la misma en todos los proyectos
        Assert.Equal("Global Ing.", offer.SignatureName);
        Assert.Equal("Empresa Global", offer.CompanyName);
        Assert.Equal("11.111.111-1", offer.CompanyRut);
        Assert.Equal("/global/logo.png", offer.LogoPath);
    }

    [Fact]
    public void LetterheadApplicator_EmptyGlobalKeepsProjectValues()
    {
        var prefs = new UserPreferences
        {
            LetterheadCompanyName = "  ",
            LetterheadLogoPath = ""
        };
        var offer = new OfferPdfSettings
        {
            CompanyName = "Empresa del Proyecto",
            LogoPath = "/proyecto/logo_proj.png"
        };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("Empresa del Proyecto", offer.CompanyName);
        Assert.Equal("/proyecto/logo_proj.png", offer.LogoPath);
    }

    // ─── 22. Global sobre valor predeterminado ────────────────────────────

    [Fact]
    public void LetterheadApplicator_GlobalFillsEmptySignature()
    {
        var prefs = new UserPreferences
        {
            ProfessionalName = "Ing. Global",
            LetterheadRut = "12.345.678-9"
        };
        var offer = new OfferPdfSettings();

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("Ing. Global", offer.SignatureName);
        Assert.Equal("12.345.678-9", offer.CompanyRut);
    }

    // ─── 23. Snapshot no cambia al modificar prefs después ───────────────

    [Fact]
    public void LetterheadApplicator_SnapshotImmutableAfterApply()
    {
        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Inicial" };
        var offer = new OfferPdfSettings();
        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("Empresa Inicial", offer.CompanyName);

        prefs.LetterheadCompanyName = "Empresa Modificada";
        Assert.Equal("Empresa Inicial", offer.CompanyName);
    }

    // ─── 24. Applicator propaga logo al offer ────────────────────────────

    [Fact]
    public void LetterheadApplicator_LogoPathPropagatedToOffer()
    {
        var prefs = new UserPreferences { LetterheadLogoPath = "/ruta/logo.png" };
        var offer = new OfferPdfSettings();
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("/ruta/logo.png", offer.LogoPath);
    }

    // ─── 25. Applicator propaga firma al offer ────────────────────────────

    [Fact]
    public void LetterheadApplicator_SignatureImagePathPropagatedToOffer()
    {
        var prefs = new UserPreferences { ProfessionalSignatureImagePath = "/ruta/firma.png" };
        var offer = new OfferPdfSettings();
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("/ruta/firma.png", offer.SignatureImagePath);
    }

    // ─── 26. Extensiones soportadas ──────────────────────────────────────

    [Fact]
    public void SupportedExtensions_IncludesPngAndJpg()
    {
        Assert.Contains(".png", LetterheadApplicator.SupportedImageExtensions);
        Assert.Contains(".jpg", LetterheadApplicator.SupportedImageExtensions);
        Assert.Contains(".jpeg", LetterheadApplicator.SupportedImageExtensions);
    }

    // ─── 27. Tests no escriben en %LOCALAPPDATA% real ────────────────────

    [Fact]
    public void Tests_DoNotWriteToRealLocalAppData()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var activePath = UserPreferencesStore.PreferencesFilePath;
        Assert.DoesNotContain("QubiCostElectricalPRO", activePath, StringComparison.OrdinalIgnoreCase);
    }

    // ─── 28. Archivos temporales de imagen no quedan en la ruta de prefs ─

    [Fact]
    public void LogoPath_InTemp_IsAccepted_ButNotCopied()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var logoPath = CreateSmallPng();
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = logoPath;
        vm.TrySave();

        var loaded = UserPreferencesStore.Load();
        Assert.Equal(logoPath, loaded.LetterheadLogoPath);
        Assert.True(File.Exists(logoPath), "El archivo original no debe ser eliminado.");
    }

    // ─── 29. Vista previa no modifica preferencias ────────────────────────

    [Fact]
    public void PreviewDisplay_DoesNotModifyPreferences()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(path);

        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa Sin Tocar" };
        UserPreferencesStore.Save(prefs);

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        var _2 = vm.CompanyName;
        var _3 = vm.LogoPath;

        Assert.False(vm.HasPendingChanges);
        var loaded = UserPreferencesStore.Load();
        Assert.Equal("Empresa Sin Tocar", loaded.LetterheadCompanyName);
    }

    // ─── 30. WPF no referencia WinForms ──────────────────────────────────

    [Fact]
    public void WpfAssembly_DoesNotReferenceWinFormsAssembly()
    {
        var wpfAssembly = typeof(PreferencesViewModel).Assembly;
        var refs = wpfAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name != null && r.Name.Contains("QubiCostElectrical", StringComparison.OrdinalIgnoreCase) &&
            !r.Name.Contains("Domain") &&
            !r.Name.Contains("Application") &&
            !r.Name.Contains("Infrastructure") &&
            !r.Name.Contains("Composition"));
    }

    // ─── PRECEDENCIA: GLOBAL (PREFERENCIAS) MANDA ─────────────────────────

    [Fact]
    public void LetterheadApplicator_GlobalCompanyWinsOverProject()
    {
        var prefs = new UserPreferences { LetterheadCompanyName = "Global SA" };
        var offer = new OfferPdfSettings { CompanyName = "Proyecto Ltda." };
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("Global SA", offer.CompanyName);
    }

    [Fact]
    public void LetterheadApplicator_EmptyCompanyGetsGlobalValue()
    {
        var prefs = new UserPreferences { LetterheadCompanyName = "Global SA" };
        var offer = new OfferPdfSettings { CompanyName = "" };
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("Global SA", offer.CompanyName);
    }

    [Fact]
    public void LetterheadApplicator_GlobalLogoWinsOverProject()
    {
        var prefs = new UserPreferences { LetterheadLogoPath = "/global/logo.png" };
        var offer = new OfferPdfSettings { LogoPath = "/proyecto/logo_proj.png" };
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("/global/logo.png", offer.LogoPath);
    }

    [Fact]
    public void LetterheadApplicator_EmptyLogoGetsGlobalValue()
    {
        var prefs = new UserPreferences { LetterheadLogoPath = "/global/logo.png" };
        var offer = new OfferPdfSettings { LogoPath = "" };
        LetterheadApplicator.Apply(prefs, offer);
        Assert.Equal("/global/logo.png", offer.LogoPath);
    }

    [Fact]
    public void LetterheadApplicator_NullOfferDoesNotThrow()
    {
        var prefs = new UserPreferences { LetterheadCompanyName = "Empresa" };
        LetterheadApplicator.Apply(prefs, null!);
    }

    [Fact]
    public void LetterheadApplicator_NullPrefsDoesNotThrow()
    {
        var offer = new OfferPdfSettings();
        LetterheadApplicator.Apply(null!, offer);
        Assert.Equal("", offer.CompanyName);
    }

    // ─── VISTA PREVIA DE LOGO ────────────────────────────────────────────

    [Fact]
    public void LoadImageSafe_ValidPng_ReturnsNonNull()
    {
        var logoPath = CreateSmallPng();
        var bmp = PreferencesViewModel.LoadImageSafe(logoPath);
        Assert.NotNull(bmp);
    }

    [Fact]
    public void LoadImageSafe_Nonexistent_ReturnsNull()
    {
        var bmp = PreferencesViewModel.LoadImageSafe(@"C:\no\existe\logo.png");
        Assert.Null(bmp);
    }

    [Fact]
    public void LoadImageSafe_EmptyPath_ReturnsNull()
    {
        var bmp = PreferencesViewModel.LoadImageSafe("");
        Assert.Null(bmp);
        bmp = PreferencesViewModel.LoadImageSafe(null);
        Assert.Null(bmp);
    }

    [Fact]
    public void LoadImageSafe_CorruptFile_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lh_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "corrupt.png");
        File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4, 5 });
        var bmp = PreferencesViewModel.LoadImageSafe(path);
        Assert.Null(bmp);
    }

    [Fact]
    public void SetLogoPath_ValidPng_LogoImageSourceNotNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = CreateSmallPng();
        Assert.NotNull(vm.LogoImageSource);
    }

    [Fact]
    public void SetLogoPath_Empty_LogoImageSourceIsNull()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = CreateSmallPng();
        vm.LogoPath = "";
        Assert.Null(vm.LogoImageSource);
    }

    [Fact]
    public void SetLogoPath_DoesNotLockFile()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var logoPath = CreateSmallPng();
        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = logoPath;
        Assert.NotNull(vm.LogoImageSource);
        File.Delete(logoPath);
        Assert.False(File.Exists(logoPath), "El archivo debe poder eliminarse: no debe estar bloqueado.");
    }

    [Fact]
    public void SetLogoPath_ChangeLogo_DoesNotModifyProject()
    {
        WpfTestHost.EnsureInitialized();
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        var host = WpfAppHost.Current;
        host.Session.NewProject("logo-isolation");

        var vm = new PreferencesViewModel(WpfAppHost.Current);
        vm.LogoPath = CreateSmallPng();

        Assert.False(host.Session.IsDirty);
    }

    // ─── Nuevos: teléfono/email/logo mandante/tamaño de firma ────────────

    [Fact]
    public void LetterheadApplicator_GlobalFillsPhoneEmailAndClientLogo()
    {
        var prefs = new UserPreferences
        {
            ProfessionalPhone = "9 1234 5678",
            ProfessionalEmail = "firma@empresa.cl",
            LetterheadClientLogoPath = "/global/mandante.png"
        };
        var offer = new OfferPdfSettings();

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("9 1234 5678", offer.SignaturePhone);
        Assert.Equal("firma@empresa.cl", offer.SignatureEmail);
        Assert.Equal("/global/mandante.png", offer.ClientLogoPath);
    }

    [Fact]
    public void LetterheadApplicator_GlobalPhoneAndClientLogoWinOverProject()
    {
        var prefs = new UserPreferences
        {
            ProfessionalPhone = "Global",
            LetterheadClientLogoPath = "/global/logo.png"
        };
        var offer = new OfferPdfSettings
        {
            SignaturePhone = "Proyecto",
            ClientLogoPath = "/proyecto/logo.png"
        };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("Global", offer.SignaturePhone);
        Assert.Equal("/global/logo.png", offer.ClientLogoPath);
    }

    [Fact]
    public void LetterheadApplicator_EmptyPhoneKeepsProjectValue()
    {
        var prefs = new UserPreferences { ProfessionalPhone = "" };
        var offer = new OfferPdfSettings { SignaturePhone = "Proyecto" };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal("Proyecto", offer.SignaturePhone);
    }

    [Fact]
    public void LetterheadApplicator_GlobalFillsSignatureSize_WhenProjectUnset()
    {
        var prefs = new UserPreferences { ProfessionalSignatureWidthMm = 70, ProfessionalSignatureHeightMm = 25 };
        var offer = new OfferPdfSettings { SignatureWidthMm = 0, SignatureHeightMm = 0 };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal(70, offer.SignatureWidthMm);
        Assert.Equal(25, offer.SignatureHeightMm);
    }

    [Fact]
    public void LetterheadApplicator_GlobalSignatureSizeWinsOverProject()
    {
        var prefs = new UserPreferences { ProfessionalSignatureWidthMm = 70, ProfessionalSignatureHeightMm = 25 };
        var offer = new OfferPdfSettings { SignatureWidthMm = 50, SignatureHeightMm = 20 };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal(70, offer.SignatureWidthMm);
        Assert.Equal(25, offer.SignatureHeightMm);
    }

    [Fact]
    public void LetterheadApplicator_EmptySignatureSizeKeepsProjectValue()
    {
        var prefs = new UserPreferences { ProfessionalSignatureWidthMm = 0, ProfessionalSignatureHeightMm = 0 };
        var offer = new OfferPdfSettings { SignatureWidthMm = 50, SignatureHeightMm = 20 };

        LetterheadApplicator.Apply(prefs, offer);

        Assert.Equal(50, offer.SignatureWidthMm);
        Assert.Equal(20, offer.SignatureHeightMm);
    }
}
