using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// ApuService no tenía tests propios pese a ser el punto de entrada de la Aplicación para
/// creación, migración, validación y agregación de costos de APU en todo el proyecto.
/// </summary>
public sealed class ApuServiceTests
{
    private static readonly ApuService Service = new();

    private static ApuRow MakeApu(string itemId, decimal precioUf, string descripcion = "APU", string unidad = "un")
    {
        return new ApuRow
        {
            ItemId = itemId,
            Descripcion = descripcion,
            Unidad = unidad,
            HerramientasPct = 0m,
            MermaPct = 0m,
            TransportePct = 0m,
            ImprevistosAlzaPct = 0m,
            Recursos = new List<ApuRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = precioUf }
            }
        };
    }

    private static ItemizadoRow MakeItem(string id, decimal cantidad, bool esTitulo = false) => new()
    {
        Id = id,
        Cantidad = cantidad,
        EsTitulo = esTitulo
    };

    // ─── CrearDesdeItem / MigrarDesde / GetPreciosParaCubicacion / Validar: delegan en
    // ApuEngine (ya testeado en ApuEngineTests) — se confirma solo que la delegación conecta
    // bien los parámetros, no se re-testea el motor.

    [Fact]
    public void CrearDesdeItem_DelegatesToEngine_CarryingItemFields()
    {
        var item = new ItemizadoRow { Id = "1.1", Descripcion = "Circuito", Unidad = "ml" };

        var apu = Service.CrearDesdeItem(item);

        Assert.Equal("1.1", apu.ItemId);
        Assert.Equal("Circuito", apu.Descripcion);
        Assert.Equal("ml", apu.Unidad);
    }

    [Fact]
    public void MigrarDesde_DelegatesToEngine_CarryingCubicacionFields()
    {
        // El motor (ApuEngine.CreateFromCubicacion, ya testeado en ApuEngineTests) siempre
        // arma un recurso MAT + un recurso MO desde una fila de cubicación migrada.
        var cub = new CubicacionRow { ItemId = "2.2", Codigo = "MAT-1", Cantidad = 5m, MatUf = 1m, MoUf = 0.5m };

        var apu = Service.MigrarDesde(cub, "Descripción del item", "un");

        Assert.Equal("2.2", apu.ItemId);
        Assert.Equal("Descripción del item", apu.Descripcion);
        Assert.Equal("un", apu.Unidad);
        Assert.Equal(2, apu.Recursos.Count);
        Assert.Contains(apu.Recursos, r => r.Tipo == ApuTipoRecurso.MAT && r.CodigoMaterial == "MAT-1");
        Assert.Contains(apu.Recursos, r => r.Tipo == ApuTipoRecurso.MO);
    }

    [Fact]
    public void GetPreciosParaCubicacion_ReturnsMaterialAndManoDeObraSeparately()
    {
        var apu = new ApuRow
        {
            HerramientasPct = 0m,
            MermaPct = 0m,
            TransportePct = 0m,
            ImprevistosAlzaPct = 0m,
            Recursos = new List<ApuRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 3m },
                new() { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, PrecioUf = 2m }
            }
        };

        var (matUf, moUf) = Service.GetPreciosParaCubicacion(apu);

        Assert.Equal(3m, matUf);
        Assert.Equal(2m, moUf);
    }

    [Fact]
    public void Validar_ReturnsEngineValidationMessages()
    {
        var apu = new ApuRow { Recursos = new List<ApuRecursoRow>() };

        var errores = Service.Validar(apu);

        Assert.NotEmpty(errores);
    }

    // ─── GetResumenProyecto / GetTotalesProyecto: lógica propia de ApuService (join entre
    // APUs e Itemizado por cantidad cubicada) — el motivo real de este archivo.

    [Fact]
    public void GetResumenProyecto_MultipliesPrecioUnitByCantidadFromItemizado()
    {
        var apus = new[] { MakeApu("1.1", precioUf: 10m) };
        var itemizado = new[] { MakeItem("1.1", cantidad: 4m) };

        var resumen = Service.GetResumenProyecto(apus, itemizado);

        var row = Assert.Single(resumen);
        Assert.Equal("1.1", row.ItemId);
        Assert.Equal(4m, row.CantidadItem);
        Assert.Equal(10m, row.PrecioUnit);
        Assert.Equal(40m, row.TotalUf);
    }

    [Fact]
    public void GetResumenProyecto_DefaultsCantidadToZero_WhenItemNotFoundInItemizado()
    {
        var apus = new[] { MakeApu("1.1", precioUf: 10m) };
        var itemizado = new[] { MakeItem("otro-item", cantidad: 99m) };

        var row = Assert.Single(Service.GetResumenProyecto(apus, itemizado));

        Assert.Equal(0m, row.CantidadItem);
        Assert.Equal(0m, row.TotalUf);
    }

    [Fact]
    public void GetResumenProyecto_ExcludesApusWithBlankItemId()
    {
        var apus = new[] { MakeApu("", precioUf: 10m), MakeApu("1.1", precioUf: 5m) };
        var itemizado = new[] { MakeItem("1.1", cantidad: 1m) };

        var resumen = Service.GetResumenProyecto(apus, itemizado);

        Assert.Single(resumen);
        Assert.Equal("1.1", resumen[0].ItemId);
    }

    [Fact]
    public void GetResumenProyecto_IgnoresTituloRows_WhenLookingUpCantidad()
    {
        // Un título (EsTitulo=true) con el mismo Id que una partida real no debe pisar su cantidad.
        var apus = new[] { MakeApu("1.1", precioUf: 10m) };
        var itemizado = new[] { MakeItem("1.1", cantidad: 7m, esTitulo: true) };

        var row = Assert.Single(Service.GetResumenProyecto(apus, itemizado));

        // Al ser título, no entra al diccionario de cantidades → cae al default 0.
        Assert.Equal(0m, row.CantidadItem);
    }

    [Fact]
    public void GetResumenProyecto_OrdersByTotalUfDescending()
    {
        var apus = new[] { MakeApu("A", precioUf: 1m), MakeApu("B", precioUf: 10m), MakeApu("C", precioUf: 5m) };
        var itemizado = new[] { MakeItem("A", 1m), MakeItem("B", 1m), MakeItem("C", 1m) };

        var resumen = Service.GetResumenProyecto(apus, itemizado);

        Assert.Equal(new[] { "B", "C", "A" }, resumen.Select(r => r.ItemId));
    }

    [Fact]
    public void GetTotalesProyecto_SumsAcrossAllApus_WeightedByCantidad()
    {
        var apus = new[] { MakeApu("1.1", precioUf: 10m), MakeApu("1.2", precioUf: 4m) };
        var itemizado = new[] { MakeItem("1.1", cantidad: 2m), MakeItem("1.2", cantidad: 3m) };

        var totales = Service.GetTotalesProyecto(apus, itemizado);

        // Cada APU tiene un único recurso MAT sin merma/imprevistos/herramientas: CostoMaterialUf == PrecioUf.
        Assert.Equal(10m * 2m + 4m * 3m, totales.Mat);
        Assert.Equal(0m, totales.Mo);
        Assert.Equal(totales.Mat, totales.Total);
    }

    [Fact]
    public void GetTotalesProyecto_SkipsApusWithBlankItemId()
    {
        var apus = new[] { MakeApu("", precioUf: 100m) };
        var itemizado = System.Array.Empty<ItemizadoRow>();

        var totales = Service.GetTotalesProyecto(apus, itemizado);

        Assert.Equal(0m, totales.Total);
    }

    [Fact]
    public void GetResumenProyecto_And_GetTotalesProyecto_HandleEmptyInputs()
    {
        var resumen = Service.GetResumenProyecto(System.Array.Empty<ApuRow>(), System.Array.Empty<ItemizadoRow>());
        var totales = Service.GetTotalesProyecto(System.Array.Empty<ApuRow>(), System.Array.Empty<ItemizadoRow>());

        Assert.Empty(resumen);
        Assert.Equal(0m, totales.Total);
    }
}
