using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionSCBudgetServiceTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static CubicacionScConfig DefaultConfig() => CubicacionScConfig.Default();

    private static IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> Totals(params (string Nombre, decimal Qty, string Unidad)[] items)
    {
        var d = new Dictionary<string, CubicacionSCEngine.MaterialSC>(StringComparer.OrdinalIgnoreCase);
        foreach (var (n, q, u) in items)
            d[n] = new CubicacionSCEngine.MaterialSC(n, q, u);
        return d;
    }

    private static MaterialRow Mat(string desc, string codigo, string unidad = "ML", decimal matUf = 1m, decimal moUf = 0.5m)
        => new() { Codigo = codigo, Descripcion = desc, Unidad = unidad, MatUf = matUf, MoUf = moUf };

    // ── BuildResumenHash ──────────────────────────────────────────────────────

    [Fact]
    public void BuildResumenHash_EmptyTotals_ReturnsEmptyString()
    {
        var hash = CubicacionSCBudgetService.BuildResumenHash(
            new Dictionary<string, CubicacionSCEngine.MaterialSC>());
        Assert.Equal("", hash);
    }

    [Fact]
    public void BuildResumenHash_SameDataSameOrder_IsDeterministic()
    {
        var totals = Totals(("TUBERIA PVC 20mm", 100m, "ML"), ("CONDUCTOR H07V-U 1.5mm2", 50m, "ML"));
        string h1 = CubicacionSCBudgetService.BuildResumenHash(totals);
        string h2 = CubicacionSCBudgetService.BuildResumenHash(totals);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void BuildResumenHash_DifferentQuantity_ProducesDifferentHash()
    {
        var t1 = Totals(("TUBERIA PVC 20mm", 100m, "ML"));
        var t2 = Totals(("TUBERIA PVC 20mm", 101m, "ML"));
        Assert.NotEqual(
            CubicacionSCBudgetService.BuildResumenHash(t1),
            CubicacionSCBudgetService.BuildResumenHash(t2));
    }

    [Fact]
    public void BuildResumenHash_OrderIndependent()
    {
        var t1 = Totals(("A", 1m, "ML"), ("B", 2m, "ML"));
        var t2 = Totals(("B", 2m, "ML"), ("A", 1m, "ML"));
        Assert.Equal(
            CubicacionSCBudgetService.BuildResumenHash(t1),
            CubicacionSCBudgetService.BuildResumenHash(t2));
    }

    // ── BuildResult ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildResult_EmptyTotals_ReturnsNull()
    {
        var result = CubicacionSCBudgetService.BuildResult(
            new Dictionary<string, CubicacionSCEngine.MaterialSC>(),
            new List<CuadroCargaSCRow>(),
            DefaultConfig());
        Assert.Null(result);
    }

    [Fact]
    public void BuildResult_NonEmptyTotals_ReturnsResult()
    {
        var totals = Totals(("TUBERIA PVC 20mm", 10m, "ML"));
        var result = CubicacionSCBudgetService.BuildResult(totals, new List<CuadroCargaSCRow>(), DefaultConfig());
        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalMateriales);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_CreatesPartidaWhenNotExists()
    {
        var totals    = Totals(("TUBERIA PVC 20mm", 10m, "ML"));
        var itemizado = new List<ItemizadoRow>();
        var cubicacion = new List<CubicacionRow>();
        var mats = Array.Empty<MaterialRow>();

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, mats);

        Assert.Single(itemizado);
        Assert.Equal("CUBICACION SERVICIOS COMUNES (C.SC)", itemizado[0].Descripcion);
        Assert.Equal("C.SC", itemizado[0].Item);
    }

    [Fact]
    public void Apply_ReusesExistingPartida()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals     = Totals(("TUBERIA PVC 20mm", 10m, "ML"));
        var itemizado  = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, Array.Empty<MaterialRow>());

        Assert.Single(itemizado);
        Assert.Equal(existingId, itemizado[0].Id);
        Assert.Single(cubicacion);
        Assert.Equal(existingId, cubicacion[0].ItemId);
    }

    [Fact]
    public void Apply_ReplacesExistingRows()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals     = Totals(("TUBERIA PVC 20mm", 10m, "ML"), ("CONDUCTOR H07V-U 1.5mm2", 50m, "ML"));
        var itemizado  = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>
        {
            new() { Id = Guid.NewGuid().ToString("N"), ItemId = existingId, Descripcion = "FILA VIEJA" }
        };

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, Array.Empty<MaterialRow>());

        Assert.Equal(2, cubicacion.Count);
        Assert.DoesNotContain(cubicacion, r => r.Descripcion == "FILA VIEJA");
    }

    [Fact]
    public void Apply_UsesMatFromCatalogo_WhenFound()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals     = Totals(("TUBERIA PVC 20mm", 10m, "ML"));
        var itemizado  = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();
        var mats = new[] { Mat("TUBERIA PVC 20mm", "TUB-001", "ML", matUf: 2.5m, moUf: 0.3m) };

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, mats);

        var row = cubicacion.Single();
        Assert.Equal("TUB-001", row.Codigo);
        Assert.Equal(2.5m, row.MatUf);
        Assert.Equal(0.3m, row.MoUf);
    }

    [Fact]
    public void Apply_CountsPrecioCorrectly()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals = Totals(
            ("TUBERIA PVC 20mm", 10m, "ML"),     // sin precio
            ("CONDUCTOR H07V-U 1.5mm2", 50m, "ML") // con precio
        );
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();
        var mats = new[] { Mat("CONDUCTOR H07V-U 1.5mm2", "COND-001") };

        var result = CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, mats);

        Assert.Equal(2, result.TotalMateriales);
        Assert.Equal(1, result.ConPrecio);
        Assert.Equal(1, result.SinPrecio);
    }

    [Fact]
    public void Apply_RoundsCantidadTo2Decimals()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals = Totals(("TUBERIA PVC 20mm", 10.123456m, "ML"));
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, Array.Empty<MaterialRow>());

        Assert.Equal(10.12m, cubicacion[0].Cantidad);
    }

    [Fact]
    public void Apply_AssignsAreaCSC()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals = Totals(("TUBERIA PVC 20mm", 10m, "ML"));
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, Array.Empty<MaterialRow>());

        Assert.All(cubicacion, r => Assert.Equal("C.SC", r.Area));
    }

    [Fact]
    public void Apply_MatchesMaterialByCodigo_WhenKeyIsCode()
    {
        var existingId = Guid.NewGuid().ToString("N");
        var totals = Totals(("TUB-001", 10m, "ML"));
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = existingId, Item = "C.SC", Descripcion = "CUBICACION SERVICIOS COMUNES (C.SC)", Unidad = "GL", Cantidad = 1 }
        };
        var cubicacion = new List<CubicacionRow>();
        var mats = new[] { Mat("TUBERIA PVC 20mm", "TUB-001", "ML", matUf: 2.5m, moUf: 0.3m) };

        CubicacionSCBudgetService.Apply(totals, new List<CuadroCargaSCRow>(), DefaultConfig(), itemizado, cubicacion, mats);

        var row = cubicacion.Single();
        Assert.Equal("TUB-001", row.Codigo);
        Assert.Equal(2.5m, row.MatUf);
        Assert.Equal(0.3m, row.MoUf);
    }
}
