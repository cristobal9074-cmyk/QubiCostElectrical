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
    public static ServiceProvider Build(string? priceBankPathOverride, IAuthService? authOverride = null)
    {
        var services = new ServiceCollection();
        services.AddBackendServices(priceBankPathOverride);
        services.AddWpfServices();
        if (authOverride != null)
            services.AddSingleton<IAuthService>(authOverride);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

        BackendComposition.InitializeBackend(provider);
        SessionManager.BindPermissions(provider.GetRequiredService<IPermissionService>());

        return provider;
    }
}
