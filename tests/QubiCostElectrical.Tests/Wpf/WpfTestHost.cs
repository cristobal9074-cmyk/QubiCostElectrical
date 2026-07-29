using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Tests.Application;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Bootstrap del host WPF para pruebas unitarias (sesión admin, sin login UI).
/// </summary>
public static class WpfTestHost
{
    public static void EnsureInitialized()
    {
        // Aísla los logs de tests de los del usuario real: sin esto, cada corrida de
        // `dotnet test` escribe en %LOCALAPPDATA%\QubiCostElectrical\Logs\ (mismo archivo
        // que usa la app de producción), mezclando ruido de tests con logs reales.
        var testLogsFolder = Path.Combine(Path.GetTempPath(), "QubiCostElectrical-Tests", "Logs");

        // Mismo problema con preferences.json: WpfAppHost.Initialize() llama a
        // UserPreferencesStore.Load(), que sin este override lee/escribe el archivo real
        // del usuario. Los tests que necesitan control fino sobre un path propio siguen
        // pudiendo sobreescribir esto con su propio `using UsePreferencesFilePathForTests(...)`.
        var testPreferencesPath = Path.Combine(Path.GetTempPath(), "QubiCostElectrical-Tests", "preferences.json");
        UserPreferencesStore.UsePreferencesFilePathForTests(testPreferencesPath);

        // Mismo problema con el banco de precios y la base de autenticación: sin esto,
        // WpfAppHost.Initialize() abre banco_precios.db y auth.db reales del usuario.
        var testDataFolder = Path.Combine(Path.GetTempPath(), "QubiCostElectrical-Tests");
        var testPriceBankPath = Path.Combine(testDataFolder, "banco_precios.db");
        var testAuthPath = Path.Combine(testDataFolder, "auth.db");
        var testAuth = new AuthService(new SqliteAuthStore($"Data Source={testAuthPath}"));

        WpfAppHost.Initialize(testLogsFolder, testPriceBankPath, testAuth);
        SessionManager.SetSession(new UserSession
        {
            Account = new UserAccount
            {
                DisplayName = "Pruebas unitarias",
                Email = "test@qubicost.local",
                Role = UserRole.Admin,
                IsActive = true
            }
        });

        // Registrar un PaymentCertificateService stub para que las pruebas WPF
        // puedan cargar el workspace sin error "Servicio no disponible".
        // WpfAppHost.Initialize() llama a new AppServicesBuilder().Build() que
        // NO registra PaymentCertificateService (se queda en null). Usamos
        // SetPaymentCertificateServices() que es pública y asigna el servicio.
        var pcDeps = new StubPaymentCertificateDependencies();
        var pcService = pcDeps.CreateService();
        WpfAppHost.Current.Services.SetPaymentCertificateServices(
            pcDeps.CertRepo,
            pcDeps.LineRepo,
            pcDeps.AdvanceRepo,
            pcDeps.AmortRepo,
            pcDeps.RetentionRepo,
            pcDeps.RetAppRepo,
            pcDeps.RetReleaseRepo,
            pcDeps.ObservationRepo,
            pcDeps.RevisionRepo,
            pcDeps.AuditRepo,
            pcService);
    }
}

public sealed class WpfHostFixture
{
    public WpfHostFixture() => WpfTestHost.EnsureInitialized();
}
