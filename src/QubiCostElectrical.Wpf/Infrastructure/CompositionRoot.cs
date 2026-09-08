using System;
using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf;
using QubiCostElectrical.Wpf.Auth;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Raíz de composición de la aplicación WPF: arma el <see cref="IServiceProvider"/> con los
/// módulos de las 4 capas y ejecuta el sembrado de arranque. Reemplaza a
/// <c>AppServicesBuilder</c> + <c>WpfAppHostBuilder</c>.
/// </summary>
public static class CompositionRoot
{
    public static ServiceProvider Build(string? priceBankPathOverride, AuthService? authOverride = null)
    {
        var prefs = UserPreferencesStore.Load();
        var priceBankPath = priceBankPathOverride
            ?? ProductPaths.ResolvePriceBankDatabaseFile(prefs.PriceBankDatabasePath);

        var services = new ServiceCollection();
        services.AddSingleton(new PriceBankPath(priceBankPath));
        services.AddDomainServices();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        services.AddWpfServices();
        if (authOverride != null)
            services.AddSingleton(authOverride);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

        StartupSeeder.SeedPriceBank(provider.GetRequiredService<IPriceBankStore>());
        SessionManager.BindPermissions(provider.GetRequiredService<IPermissionService>());
        // Fuerza la suscripción de ProjectDbContext a Session.Changed.
        _ = provider.GetRequiredService<IProjectDbContext>();

        return provider;
    }
}
