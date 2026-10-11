using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Delivery;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class OfferSettingsEditorVmTests
{
    private static string TempPrefsPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"osvm_{Guid.NewGuid():N}", "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static BudgetProjectSession NewSession()
    {
        var session = new BudgetProjectSession();
        session.NewProject("offer-settings-test", "Tester");
        return session;
    }

    [Fact]
    public void LoadFromSession_GlobalDefaultsFillEmptyProjectFields()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());
        UserPreferencesStore.Save(new UserPreferences
        {
            LetterheadCompanyName = "Empresa Global",
            ProfessionalName = "Ing. Global",
            ProfessionalSignatureWidthMm = 70
        });

        var session = NewSession();
        var vm = new OfferSettingsEditorVm(session);
        vm.LoadFromSession();

        Assert.Equal("Empresa Global", vm.CompanyName);
        Assert.Equal("Ing. Global", vm.SignatureName);
        Assert.Equal(70, vm.SignatureWidthMm);
    }

    [Fact]
    public void LoadFromSession_GlobalIdentityWinsOverProject()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());
        UserPreferencesStore.Save(new UserPreferences { LetterheadCompanyName = "Empresa Global" });

        var session = NewSession();
        session.OfferPdfSettings.CompanyName = "Empresa Proyecto";

        var vm = new OfferSettingsEditorVm(session);
        vm.LoadFromSession();

        Assert.Equal("Empresa Global", vm.CompanyName);
    }

    [Fact]
    public void LoadFromSession_EmptyGlobalKeepsProjectIdentity()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());
        UserPreferencesStore.Save(new UserPreferences { LetterheadCompanyName = "" });

        var session = NewSession();
        session.OfferPdfSettings.CompanyName = "Empresa Proyecto";

        var vm = new OfferSettingsEditorVm(session);
        vm.LoadFromSession();

        Assert.Equal("Empresa Proyecto", vm.CompanyName);
    }

    [Fact]
    public void SaveAsDefaults_PersistsToGlobalPreferences()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());

        var session = NewSession();
        var vm = new OfferSettingsEditorVm(session)
        {
            CompanyName = "Empresa Nueva",
            ClientLogoPath = @"C:\logos\mandante.png",
            SignatureName = "Ing. Firma",
            SignaturePhone = "9 111 222 333",
            SignatureEmail = "firma@empresa.cl",
            SignatureImagePath = @"C:\logos\firma.png",
            SignatureWidthMm = 80,
            SignatureHeightMm = 30
        };

        vm.SaveAsDefaultsCommand.Execute(null);

        var prefs = UserPreferencesStore.Load();
        Assert.Equal("Empresa Nueva", prefs.LetterheadCompanyName);
        Assert.Equal(@"C:\logos\mandante.png", prefs.LetterheadClientLogoPath);
        Assert.Equal("Ing. Firma", prefs.ProfessionalName);
        Assert.Equal("9 111 222 333", prefs.ProfessionalPhone);
        Assert.Equal("firma@empresa.cl", prefs.ProfessionalEmail);
        Assert.Equal(@"C:\logos\firma.png", prefs.ProfessionalSignatureImagePath);
        Assert.Equal(80, prefs.ProfessionalSignatureWidthMm);
        Assert.Equal(30, prefs.ProfessionalSignatureHeightMm);
        Assert.False(string.IsNullOrEmpty(vm.DefaultsHint));
    }

    [Fact]
    public void SaveAsDefaults_WithoutPermission_DoesNotPersist()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());

        var session = NewSession();
        var vm = new OfferSettingsEditorVm(session, null, () => false)
        {
            CompanyName = "No debe guardarse"
        };

        vm.SaveAsDefaultsCommand.Execute(null);

        var prefs = UserPreferencesStore.Load();
        Assert.Equal("", prefs.LetterheadCompanyName);
        Assert.True(string.IsNullOrEmpty(vm.DefaultsHint));
    }

    [Fact]
    public void ApplyToSession_PersistsSignatureSize()
    {
        var session = NewSession();
        var vm = new OfferSettingsEditorVm(session)
        {
            SignatureWidthMm = 65,
            SignatureHeightMm = 22
        };

        vm.ApplyToSession();

        Assert.Equal(65, session.OfferPdfSettings.SignatureWidthMm);
        Assert.Equal(22, session.OfferPdfSettings.SignatureHeightMm);
    }

    [Fact]
    public void LoadFromSession_UnsetSignatureSize_ShowsDefaults()
    {
        using var _ = UserPreferencesStore.UsePreferencesFilePathForTests(TempPrefsPath());

        var session = NewSession();
        var vm = new OfferSettingsEditorVm(session);
        vm.LoadFromSession();

        Assert.Equal(46, vm.SignatureWidthMm);
        Assert.Equal(16, vm.SignatureHeightMm);
    }

    [Fact]
    public void OfferSignatureSizing_UsesMmDefaultsWhenUnset()
    {
        var offer = new OfferPdfSettings();

        Assert.Equal(46f * 72f / 25.4f, OfferSignatureSizing.WidthPt(offer), 2);
        Assert.Equal(16f * 72f / 25.4f, OfferSignatureSizing.HeightPt(offer), 2);
    }

    [Fact]
    public void OfferSignatureSizing_ClampsOutOfRangeValues()
    {
        var offer = new OfferPdfSettings { SignatureWidthMm = 500, SignatureHeightMm = 1 };

        Assert.Equal(130f * 72f / 25.4f, OfferSignatureSizing.WidthPt(offer), 2);
        Assert.Equal(8f * 72f / 25.4f, OfferSignatureSizing.HeightPt(offer), 2);
    }
}
