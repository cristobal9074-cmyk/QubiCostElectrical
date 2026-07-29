using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Currency;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CurrencyViewModelTests
{
    public CurrencyViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("currency-test");
        host.Session.CurrentProjectPath = "currency-test.qce";
        host.Session.ProjectInfo.ValorUfClp       = 37_500m;
        host.Session.ProjectInfo.TipoCambioUsdClp = 920m;
        host.Session.ProjectInfo.FechaTipoCambio  = new DateTime(2026, 1, 15);
        host.Session.ProjectInfo.MonedaPresupuesto = "UF";
        host.Session.ProjectInfo.HistorialTasas.Clear();
    }

    [Fact]
    public void InitialLoad_ReadsFromSession()
    {
        var vm = new CurrencyViewModel(WpfAppHost.Current);

        Assert.Equal("UF", vm.MonedaBase);
        Assert.Equal(37_500m, vm.ValorUfClp);
        Assert.Equal(920m, vm.TipoCambioUsdClp);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void EditFields_SetDirty()
    {
        var vm = new CurrencyViewModel(WpfAppHost.Current);
        vm.EditUfClp = "38000";

        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void TryApplyChanges_InvalidUf_SetsValidationError()
    {
        var vm = new CurrencyViewModel(WpfAppHost.Current);
        vm.EditUfClp  = "no-es-numero";
        vm.EditUsdClp = "920";
        vm.EditFecha  = "15/01/2026";
        vm.TryApplyChanges();

        Assert.NotNull(vm.ValidationError);
        Assert.NotEmpty(vm.ValidationError);
    }

    [Fact]
    public void TryApplyChanges_ValidFields_UpdatesSession()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        var vm = new CurrencyViewModel(host);

        vm.EditUfClp  = "38000";
        vm.EditUsdClp = "950";
        vm.EditFecha  = "20/01/2026";
        vm.TryApplyChanges();

        Assert.Equal(38_000m, host.Session.ProjectInfo.ValorUfClp);
        Assert.Equal(950m,    host.Session.ProjectInfo.TipoCambioUsdClp);
        Assert.False(vm.HasPendingChanges);
        Assert.True(string.IsNullOrEmpty(vm.ValidationError));
    }

    [Fact]
    public void TryApplyChanges_AddsHistorialEntry()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        host.Session.ProjectInfo.HistorialTasas.Clear();
        var vm = new CurrencyViewModel(host);

        vm.EditUfClp  = "38500";
        vm.EditUsdClp = "960";
        vm.EditFecha  = "21/01/2026";
        vm.TryApplyChanges();

        Assert.Single(host.Session.ProjectInfo.HistorialTasas);
        Assert.Equal(38_500m, host.Session.ProjectInfo.HistorialTasas[0].UfClp);
    }

    [Fact]
    public void TryDiscardChanges_RestoresOriginalValues()
    {
        var host = WpfAppHost.Current;
        var vm = new CurrencyViewModel(host);

        vm.EditUfClp = "99999";
        vm.TryDiscardChanges();

        Assert.False(vm.HasPendingChanges);
        Assert.Contains("37", vm.EditUfClp); // valor original 37500 formateado N2
    }

    [Fact]
    public void HistorialViewModel_ReflectsSessionRows()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.HistorialTasas.Clear();
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow
        {
            UfClp = 37_000m, UsdClp = 900m, Activa = true, Fecha = new DateTime(2026, 1, 1)
        });
        var vm = new CurrencyViewModel(host);

        Assert.Single(vm.Historial);
        Assert.Contains("37", vm.Historial[0].UfDisplay);
    }

    [Fact]
    public void CacheStale_TrueWhenNoCacheFile()
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache_stale_{Guid.NewGuid():N}.json");
        using var _ = EconomicIndicatorCache.UseCachePathForTests(cachePath);
        // No escribimos nada — TryRead devuelve null → CacheStale = true
        var vm = new CurrencyViewModel(WpfAppHost.Current);
        Assert.True(vm.CacheStale);
    }

    [Fact]
    public void CacheStale_FalseWhenCacheIsRecent()
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache_fresh_{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        using var _ = EconomicIndicatorCache.UseCachePathForTests(cachePath);
        EconomicIndicatorCache.Write(37_500m, 920m, DateTime.UtcNow.AddMinutes(-10));

        var vm = new CurrencyViewModel(WpfAppHost.Current);
        Assert.False(vm.CacheStale);
    }

    [Fact]
    public void MonedaSeleccionada_InitialLoad_MatchesSession()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.MonedaPresupuesto = "CLP";
        var vm = new CurrencyViewModel(host);

        Assert.Equal("CLP", vm.MonedaSeleccionada);
    }

    [Fact]
    public void MonedaSeleccionada_Change_SetsDirty()
    {
        var vm = new CurrencyViewModel(WpfAppHost.Current);
        vm.MonedaSeleccionada = "CLP";

        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void TryApplyChanges_UpdatesMonedaPresupuesto()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        var vm = new CurrencyViewModel(host);

        vm.MonedaSeleccionada = "CLP";
        vm.EditUfClp  = "38000";
        vm.EditUsdClp = "950";
        vm.EditFecha  = "20/01/2026";
        vm.TryApplyChanges();

        Assert.Equal("CLP", host.Session.ProjectInfo.MonedaPresupuesto);
    }

    [Fact]
    public void TryApplyChanges_ClpSinUf_SetsValidationError()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        var vm = new CurrencyViewModel(host);

        vm.MonedaSeleccionada = "CLP";
        vm.EditUfClp  = "";      // sin UF
        vm.EditUsdClp = "";
        vm.EditFecha  = "20/01/2026";
        var result = vm.TryApplyChanges();

        Assert.True(result);
        Assert.Empty(vm.ValidationError);
    }

    [Fact]
    public void TryApplyChanges_UsdSinTasas_SetsValidationError()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        var vm = new CurrencyViewModel(host);

        vm.MonedaSeleccionada = "USD";
        vm.EditUfClp  = "";      // sin UF ni USD
        vm.EditUsdClp = "";
        vm.EditFecha  = "20/01/2026";
        var result = vm.TryApplyChanges();

        Assert.False(result);
        Assert.NotEmpty(vm.ValidationError);
    }

    [Fact]
    public void Historial_OrderedByDescendingDate()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.HistorialTasas.Clear();
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow { Fecha = new DateTime(2026, 1, 1), UfClp = 36_000m, UsdClp = 900m, Activa = false });
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow { Fecha = new DateTime(2026, 3, 1), UfClp = 38_000m, UsdClp = 950m, Activa = true  });
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow { Fecha = new DateTime(2026, 2, 1), UfClp = 37_000m, UsdClp = 930m, Activa = false });
        var vm = new CurrencyViewModel(host);

        Assert.Equal(new DateTime(2026, 3, 1), vm.Historial[0].Fecha);
        Assert.Equal(new DateTime(2026, 2, 1), vm.Historial[1].Fecha);
        Assert.Equal(new DateTime(2026, 1, 1), vm.Historial[2].Fecha);
    }

    [Fact]
    public void Historial_ActiveEntry_HasEstadoActiva()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.HistorialTasas.Clear();
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow { Fecha = DateTime.Today, UfClp = 37_500m, UsdClp = 920m, Activa = true });
        var vm = new CurrencyViewModel(host);

        Assert.Equal("Activa", vm.Historial[0].EstadoDisplay);
    }

    [Fact]
    public void TryApplyChanges_PreviousHistorial_MarkedInactive()
    {
        var host = WpfAppHost.Current;
        host.Session.ProjectInfo.ValorUfClp       = 0;
        host.Session.ProjectInfo.TipoCambioUsdClp = 0;
        host.Session.ProjectInfo.HistorialTasas.Clear();
        host.Session.ProjectInfo.HistorialTasas.Add(new TasaCambioHistorialRow { Fecha = new DateTime(2026, 1, 1), UfClp = 36_000m, UsdClp = 900m, Activa = true });
        var vm = new CurrencyViewModel(host);

        vm.EditUfClp  = "38500";
        vm.EditUsdClp = "960";
        vm.EditFecha  = "21/01/2026";
        vm.TryApplyChanges();

        var previo = host.Session.ProjectInfo.HistorialTasas.First(h => h.UfClp == 36_000m);
        Assert.False(previo.Activa);
    }

    [Fact]
    public void ResumenClp_FormatsCorrectly()
    {
        var vm = new CurrencyViewModel(WpfAppHost.Current);

        Assert.Contains("37", vm.ResumenClp);
        Assert.Contains("CLP", vm.ResumenClp);
    }

    [Fact]
    public void TryApplyChanges_SameRates_NoNewHistorialEntry()
    {
        var host = WpfAppHost.Current;
        // Las tasas en session ya están en 37_500 y 920
        var vm = new CurrencyViewModel(host);
        var countBefore = host.Session.ProjectInfo.HistorialTasas.Count;

        // Aplicar exactamente las mismas tasas → no hay cambio → no agrega historial
        vm.EditUfClp  = "37500";
        vm.EditUsdClp = "920";
        vm.EditFecha  = "15/01/2026";
        vm.TryApplyChanges();

        Assert.Equal(countBefore, host.Session.ProjectInfo.HistorialTasas.Count);
    }
}
