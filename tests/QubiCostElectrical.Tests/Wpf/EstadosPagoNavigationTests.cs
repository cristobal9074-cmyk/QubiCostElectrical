using System;
using QubiCostElectrical.Wpf.Navigation;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>Navegación y catálogo de la pantalla Estados de pago (fase Ejecución).</summary>
public sealed class EstadosPagoNavigationTests
{
    // Registrada en ShellRouteCatalog con grupo Ejecución
    [Fact]
    public void EstadosPagos_RegisteredInShellRouteCatalog()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.EstadosPagos);
        Assert.Equal("Ejecución", info.Group);
        Assert.Contains("Estados de pago", info.Title, StringComparison.OrdinalIgnoreCase);
    }

    // Visible y navegable por la política de visibilidad del shell
    [Fact]
    public void EstadosPagos_IsVisibleAndNavigable()
    {
        Assert.Equal(NavVisibility.Visible, NavVisibilityPolicy.Get(ShellRoute.EstadosPagos));
        Assert.True(NavVisibilityPolicy.IsListed(ShellRoute.EstadosPagos));
        Assert.True(NavVisibilityPolicy.IsNavigable(ShellRoute.EstadosPagos));
    }
}
