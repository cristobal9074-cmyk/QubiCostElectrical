using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ApuFromCubicacionServiceTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static ItemizadoRow Item(string id = "ID1", decimal cantidad = 10m) => new()
    {
        Id = id, Item = "1.1", Descripcion = "Instalación", Unidad = "GL", Cantidad = cantidad, EsTitulo = false
    };

    private static CubicacionRow Cub(string itemId, decimal qty, decimal mat, decimal mo = 0m, string codigo = "TUB-001") => new()
    {
        ItemId = itemId, Codigo = codigo, Descripcion = "Tubería PVC 20mm", Unidad = "ML",
        Cantidad = qty, MatUf = mat, MoUf = mo
    };

    private static MaterialRow Mat(string codigo, decimal matUf, decimal moUf = 0m) => new()
    {
        Codigo = codigo, Descripcion = "desc", MatUf = matUf, MoUf = moUf, FechaPrecio = DateTime.Today
    };

    private sealed class FakeApuService : IApuService
    {
        public ApuRow CrearDesdeItem(ItemizadoRow item) => new()
        {
            ItemId = item.Id, Descripcion = item.Descripcion, Unidad = item.Unidad
        };
        public ApuRow MigrarDesde(CubicacionRow cub, string descripcionItem, string unidadItem) => new();
        public (decimal MatUf, decimal MoUf) GetPreciosParaCubicacion(ApuRow apu) => (0m, 0m);
        public List<ApuResumenRow> GetResumenProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado) => new();
        public (decimal Mat, decimal Mo, decimal Equipo, decimal Sc, decimal Herramientas, decimal Imprevistos, decimal Total) GetTotalesProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado) => (0, 0, 0, 0, 0, 0, 0);
        public List<string> Validar(ApuRow apu) => new();
    }

    private static readonly IApuService FakeSvc = new FakeApuService();

    // ── IsAutoManagedApu ──────────────────────────────────────────────────────

    [Fact]
    public void IsAutoManagedApu_FuenteCubicacion_RetornaTrue()
    {
        var apu = new ApuRow { FuentePrecio = "Cubicacion valorizada" };
        Assert.True(ApuFromCubicacionService.IsAutoManagedApu(apu));
    }

    [Fact]
    public void IsAutoManagedApu_FuenteManual_RetornaFalse()
    {
        var apu = new ApuRow { FuentePrecio = "Manual" };
        Assert.False(ApuFromCubicacionService.IsAutoManagedApu(apu));
    }

    [Fact]
    public void IsAutoManagedApu_ConAcento_RetornaTrue()
    {
        var apu = new ApuRow { FuentePrecio = "Cubicación valorizada" };
        Assert.True(ApuFromCubicacionService.IsAutoManagedApu(apu));
    }

    // ── GetProfessionalStatus ─────────────────────────────────────────────────

    [Fact]
    public void GetProfessionalStatus_ApuNull_RetornaFaltaPrecio()
    {
        Assert.Equal("Falta precio", ApuFromCubicacionService.GetProfessionalStatus(null));
    }

    [Fact]
    public void GetProfessionalStatus_SinRecursos_RetornaFaltaPrecio()
    {
        Assert.Equal("Falta precio", ApuFromCubicacionService.GetProfessionalStatus(new ApuRow()));
    }

    [Fact]
    public void GetProfessionalStatus_RecursoSinPrecio_RetornaFaltaPrecio()
    {
        var apu = new ApuRow();
        apu.Recursos.Add(new ApuRecursoRow { PrecioUf = 0m, Rendimiento = 1m, Estado = "Falta precio" });
        Assert.Equal("Falta precio", ApuFromCubicacionService.GetProfessionalStatus(apu));
    }

    [Fact]
    public void GetProfessionalStatus_TodoOK_RetornaOK()
    {
        var apu = new ApuRow();
        apu.Recursos.Add(new ApuRecursoRow { PrecioUf = 1m, Rendimiento = 1m, Estado = "OK" });
        Assert.Equal("OK", ApuFromCubicacionService.GetProfessionalStatus(apu));
    }

    // ── CalculateSaleUnitUf ───────────────────────────────────────────────────

    [Fact]
    public void CalculateSaleUnitUf_SinGastosNiUtilidad_RetornaCostoDirecto()
    {
        var apu = new ApuRow();
        apu.Recursos.Add(new ApuRecursoRow { PrecioUf = 100m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT });
        decimal result = ApuFromCubicacionService.CalculateSaleUnitUf(apu, 0m, 0m);
        Assert.Equal(apu.CostoDirectoUf, result);
    }

    [Fact]
    public void CalculateSaleUnitUf_ConGastosYUtilidad_AplicaFormula()
    {
        var apu = new ApuRow();
        apu.Recursos.Add(new ApuRecursoRow { PrecioUf = 100m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT });
        decimal directo = apu.CostoDirectoUf;
        decimal subtotal = directo + directo * 10m / 100m;  // 110
        decimal expected = subtotal / (1m - 20m / 100m);   // 137.5 (SobreVenta)

        decimal result = ApuFromCubicacionService.CalculateSaleUnitUf(apu, 10m, 20m);
        Assert.Equal(expected, result);
    }

    // ── BuildFromCubicacion ───────────────────────────────────────────────────

    [Fact]
    public void BuildFromCubicacion_SinCubicacion_RetornaApuSinRecursos()
    {
        var item = Item("X1");
        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, new List<CubicacionRow>(), FakeSvc);
        Assert.Empty(apu.Recursos);
        Assert.Equal("Cubicacion valorizada", apu.FuentePrecio);
    }

    [Fact]
    public void BuildFromCubicacion_ConCubicacion_AgregaRecursoMAT()
    {
        var item = Item("X1", cantidad: 10m);
        var cubicacion = new List<CubicacionRow> { Cub("X1", qty: 100m, mat: 2.5m) };

        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, cubicacion, FakeSvc);

        Assert.Single(apu.Recursos);
        Assert.Equal(ApuTipoRecurso.MAT, apu.Recursos[0].Tipo);
        Assert.Equal(10m, apu.Recursos[0].Rendimiento); // 100/10
        Assert.Equal(2.5m, apu.Recursos[0].PrecioUf);
    }

    [Fact]
    public void BuildFromCubicacion_ConMoPositivo_AgregaRecursoMO()
    {
        var item = Item("X1", cantidad: 10m);
        var cubicacion = new List<CubicacionRow> { Cub("X1", qty: 10m, mat: 1m, mo: 0.5m) };

        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, cubicacion, FakeSvc);

        Assert.Equal(2, apu.Recursos.Count);
        Assert.Contains(apu.Recursos, r => r.Tipo == ApuTipoRecurso.MO);
    }

    [Fact]
    public void BuildFromCubicacion_IgnoraCubicacionDeOtroItem()
    {
        var item = Item("X1");
        var cubicacion = new List<CubicacionRow> { Cub("OTRO", qty: 10m, mat: 1m) };

        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, cubicacion, FakeSvc);
        Assert.Empty(apu.Recursos);
    }

    // ── GenerateBulk ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateBulk_ItemSinApu_CreaApu()
    {
        var item = Item("X1");
        var cubicacion = new List<CubicacionRow> { Cub("X1", qty: 10m, mat: 1m) };
        var apus = new List<ApuRow>();

        var (creados, regenerados, omitidos) = ApuFromCubicacionService.GenerateBulk(
            new[] { item }, apus, cubicacion, FakeSvc);

        Assert.Equal(1, creados);
        Assert.Equal(0, regenerados);
        Assert.Single(apus);
    }

    [Fact]
    public void GenerateBulk_ItemConApuAutomatic_Regenera()
    {
        var item = Item("X1");
        var cubicacion = new List<CubicacionRow> { Cub("X1", qty: 10m, mat: 1m) };
        var apus = new List<ApuRow>
        {
            new() { ItemId = "X1", FuentePrecio = "Cubicacion valorizada" }
        };

        var (creados, regenerados, omitidos) = ApuFromCubicacionService.GenerateBulk(
            new[] { item }, apus, cubicacion, FakeSvc);

        Assert.Equal(0, creados);
        Assert.Equal(1, regenerados);
        Assert.Single(apus);
    }

    [Fact]
    public void GenerateBulk_ItemConApuManual_Omite()
    {
        var item = Item("X1");
        var cubicacion = new List<CubicacionRow> { Cub("X1", qty: 10m, mat: 1m) };
        var apus = new List<ApuRow>
        {
            new() { ItemId = "X1", FuentePrecio = "Manual" }
        };

        var (creados, regenerados, omitidos) = ApuFromCubicacionService.GenerateBulk(
            new[] { item }, apus, cubicacion, FakeSvc);

        Assert.Equal(0, creados);
        Assert.Equal(0, regenerados);
        Assert.Equal(1, omitidos);
    }

    // ── RefreshPricesFromCatalog ──────────────────────────────────────────────

    [Fact]
    public void RefreshPricesFromCatalog_PreciosCongelados_NoCambia()
    {
        var apus = new List<ApuRow>
        {
            new() { FuentePrecio = "Manual", Recursos = new() { new() { CodigoMaterial = "TUB-001", PrecioUf = 1m } } }
        };
        var mats = new List<MaterialRow> { Mat("TUB-001", 2m) };

        int cambios = ApuFromCubicacionService.RefreshPricesFromCatalog(apus, mats, preciosCongelados: true);

        Assert.Equal(0, cambios);
        Assert.Equal(1m, apus[0].Recursos[0].PrecioUf);
    }

    [Fact]
    public void RefreshPricesFromCatalog_PrecioActualizado_CuentaCambios()
    {
        var apu = new ApuRow { FuentePrecio = "Manual" };
        apu.Recursos.Add(new ApuRecursoRow { CodigoMaterial = "TUB-001", PrecioUf = 1m, Tipo = ApuTipoRecurso.MAT });
        var apus = new List<ApuRow> { apu };
        var mats = new List<MaterialRow> { Mat("TUB-001", 2.5m) };

        int cambios = ApuFromCubicacionService.RefreshPricesFromCatalog(apus, mats, preciosCongelados: false);

        Assert.Equal(1, cambios);
        Assert.Equal(2.5m, apu.Recursos[0].PrecioUf);
    }

    [Fact]
    public void RefreshPricesFromCatalog_PrecioIgual_NoCambia()
    {
        var apu = new ApuRow();
        apu.Recursos.Add(new ApuRecursoRow { CodigoMaterial = "TUB-001", PrecioUf = 2.5m, Tipo = ApuTipoRecurso.MAT });
        var apus = new List<ApuRow> { apu };
        var mats = new List<MaterialRow> { Mat("TUB-001", 2.5m) };

        int cambios = ApuFromCubicacionService.RefreshPricesFromCatalog(apus, mats, preciosCongelados: false);

        Assert.Equal(0, cambios);
    }
}
