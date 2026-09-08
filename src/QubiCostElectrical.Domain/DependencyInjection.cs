using System;
using Microsoft.Extensions.DependencyInjection;

namespace QubiCostElectrical;

/// <summary>
/// Registro DI de la capa de dominio. El dominio es en su mayoría puro (motores sin estado),
/// así que aquí solo vive lo transversal: el reloj y una fábrica del motor de presupuesto para
/// quien necesite una instancia fresca por cálculo.
/// </summary>
public static class DomainServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Func<BudgetEngine>>(_ => static () => new BudgetEngine());
        return services;
    }
}
