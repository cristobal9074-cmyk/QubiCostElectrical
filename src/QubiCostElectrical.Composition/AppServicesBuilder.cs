using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace QubiCostElectrical;

/// <summary>
/// Punto de composición transitorio. Arma un <see cref="IServiceCollection"/> con los módulos
/// de cada capa, aplica overrides opcionales (usados por tests) y devuelve la fachada
/// <see cref="AppServices"/> resuelta desde el contenedor. Se eliminará cuando `App.xaml.cs`
/// construya el provider directamente.
/// </summary>
public sealed class AppServicesBuilder
{
    private readonly List<Action<IServiceCollection>> _overrides = new();

    public AppServicesBuilder WithItemizado(IItemizadoService value)
    {
        _overrides.Add(s => s.AddSingleton(value));
        return this;
    }

    public AppServicesBuilder WithCubicacion(ICubicacionService value)
    {
        _overrides.Add(s => s.AddSingleton(value));
        return this;
    }

    /// <summary>Sustituye cualquier servicio del grafo (para tests).</summary>
    public AppServicesBuilder With<TService>(TService instance) where TService : class
    {
        _overrides.Add(s => s.AddSingleton(instance));
        return this;
    }

    /// <param name="validate">
    /// Verifica todo el grafo al construir (usado por el test de fitness). En el arranque real
    /// y en la mayoría de tests se deja en <c>false</c>: la resolución perezosa evita construir
    /// los exportadores pesados (QuestPDF/ClosedXML) hasta que se necesitan.
    /// </param>
    public IServiceProvider BuildProvider(bool validate = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new PriceBankPath(
            ProductPaths.ResolvePriceBankDatabaseFile(null)));
        services.AddDomainServices();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        foreach (var apply in _overrides)
            apply(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = validate,
            ValidateScopes = true,
        });
        StartupSeeder.SeedPriceBank(provider.GetRequiredService<IPriceBankStore>());
        return provider;
    }

    public AppServices Build() => new(BuildProvider());
}
