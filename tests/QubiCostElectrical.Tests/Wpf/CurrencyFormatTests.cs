using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class CurrencyFormatTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1500)]
    [InlineData(1234567)]
    [InlineData(-500)]
    public void Clp_PrefixesDollarSignAndMatchesN0Grouping(decimal amount)
    {
        // No se fija el separador de miles: depende de la cultura del hilo, igual que el propio
        // helper (usa CurrentCulture vía interpolación estándar) — se compara contra la misma
        // fórmula ("N0") en vez de un string codificado para no acoplar el test a una cultura.
        Assert.Equal($"$ {amount:N0}", CurrencyFormat.Clp(amount));
        Assert.StartsWith("$ ", CurrencyFormat.Clp(amount));
    }
}
