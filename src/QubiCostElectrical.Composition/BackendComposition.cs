using System;
using Microsoft.Extensions.DependencyInjection;

namespace QubiCostElectrical;

/// <summary>
/// Ensamblador de servicios backend (Domain, Application e Infrastructure) para la capa de presentación.
/// </summary>
public static class BackendComposition
{
    public static IServiceCollection AddBackendServices(this IServiceCollection services, string? priceBankPathOverride = null)
    {
        var prefs = UserPreferencesStore.Load();
        var priceBankPath = priceBankPathOverride
            ?? ProductPaths.ResolvePriceBankDatabaseFile(prefs.PriceBankDatabasePath);

        services.AddSingleton(new PriceBankPath(priceBankPath));
        services.AddDomainServices();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        return services;
    }

    public static void InitializeBackend(IServiceProvider provider)
    {
        StartupSeeder.SeedPriceBank(provider.GetRequiredService<IPriceBankStore>());
        // Fuerza la suscripción de ProjectDbContext a Session.Changed.
        _ = provider.GetRequiredService<IProjectDbContext>();
    }
}
