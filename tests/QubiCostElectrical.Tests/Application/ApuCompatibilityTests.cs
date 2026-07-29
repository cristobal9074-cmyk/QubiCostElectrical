using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Verifica que el motor de cálculo APU del dominio sea la única fuente de verdad.
/// Ningún sistema externo (WPF, WinForms) debe recalcular totales propios.
/// </summary>
public sealed class ApuCompatibilityTests
{
    private static ApuRecursoRow MakeMatRow(decimal rend, decimal precio, decimal merma = 0m) => new()
    {
        Tipo = ApuTipoRecurso.MAT, Rendimiento = rend, PrecioUf = precio, MermaPct = merma,
        Descripcion = "Material", Unidad = "UN",
    };

    private static ApuRecursoRow MakeMoRow(decimal rend, decimal valorHH) => new()
    {
        Tipo = ApuTipoRecurso.MO, Rendimiento = rend, ValorHHUf = valorHH,
        Descripcion = "Mano de obra", Unidad = "HH",
    };

    private static ApuRow BuildApu(params ApuRecursoRow[] recursos) => new()
    {
        ItemId = "TEST",
        Descripcion = "Test partida",
        Unidad = "GL",
        Recursos = recursos.ToList(),
        HerramientasPct = 3m,
        ImprevistosAlzaPct = 2m,
        FechaPrecio = DateTime.Today,
    };

    // ── Propiedad calculada en ApuRow (dominio) ───────────────────────────────

    [Fact]
    public void CostoDirectoUf_is_gte_sum_of_material_categories()
    {
        var mat = MakeMatRow(2m, 1m, 0m);
        var mo = MakeMoRow(4m, 0.25m);
        var apu = BuildApu(mat, mo);

        // CostoDirectoUf incluye herramientas calculadas además de categorías base
        var sumBase = apu.CostoMaterialUf + apu.CostoMoUf + apu.CostoEquipoUf + apu.CostoScUf;
        Assert.True(apu.CostoDirectoUf >= sumBase,
            "CostoDirectoUf debe incluir al menos la suma de categorías base");
    }

    [Fact]
    public void PrecioUnitUf_includes_herramientas_and_imprevistos()
    {
        var mat = MakeMatRow(1m, 1m, 0m);
        var apu = BuildApu(mat);

        Assert.True(apu.PrecioUnitUf >= apu.CostoDirectoUf,
            "PrecioUnitUf debe ser >= CostoDirectoUf por herramientas e imprevistos");
    }

    [Fact]
    public void TotalPartidaUf_is_PrecioUnit_times_Cantidad()
    {
        var mat = MakeMatRow(1m, 2m, 0m);
        var apu = BuildApu(mat);
        apu.CantidadPartida = 5m;

        var expected = apu.PrecioUnitUf * apu.CantidadPartida;
        Assert.Equal(expected, apu.TotalPartidaUf);
    }

    // ── CalculateSaleUnitUf (sin duplicar lógica) ─────────────────────────────

    [Fact]
    public void CalculateSaleUnitUf_returns_value_gte_PrecioUnit()
    {
        var mat = MakeMatRow(1m, 1m, 0m);
        var apu = BuildApu(mat);

        var sale = ApuFromCubicacionService.CalculateSaleUnitUf(apu, 10m, 5m);
        Assert.True(sale >= apu.PrecioUnitUf,
            "Precio de venta con GG y utilidad debe ser >= precio unitario de costo");
    }

    [Fact]
    public void CalculateSaleUnitUf_with_zero_gg_and_util_equals_costo_directo()
    {
        var mat = MakeMatRow(1m, 1m, 0m);
        var apu = BuildApu(mat);

        // Con GG=0 y utilidad=0, CalculateSaleUnitUf retorna CostoDirectoUf (sin imprevistos de venta)
        var sale = ApuFromCubicacionService.CalculateSaleUnitUf(apu, 0m, 0m);
        Assert.Equal(apu.CostoDirectoUf, sale);
    }

    // ── GetProfessionalStatus: no recalcula, lee del dominio ─────────────────

    [Fact]
    public void GetProfessionalStatus_OK_when_all_recursos_have_price()
    {
        var recursos = new[]
        {
            MakeMatRow(1m, 1m),
            MakeMoRow(4m, 0.25m),
        };
        var apu = BuildApu(recursos);
        Assert.Equal("OK", ApuFromCubicacionService.GetProfessionalStatus(apu));
    }

    [Fact]
    public void GetProfessionalStatus_not_OK_when_any_recurso_has_zero_price()
    {
        var recursos = new[]
        {
            MakeMatRow(1m, 0m),
            MakeMoRow(4m, 0.25m),
        };
        var apu = BuildApu(recursos);
        Assert.NotEqual("OK", ApuFromCubicacionService.GetProfessionalStatus(apu));
    }

    // ── No duplicación: HerramientasCalculadoUf es del dominio ───────────────

    [Fact]
    public void HerramientasCalculadoUf_is_nonzero_when_herramientasPct_gt_zero()
    {
        var mo = MakeMoRow(8m, 0.3m);
        var apu = BuildApu(mo);
        apu.HerramientasPct = 5m;

        // HerramientasCalculadoUf aplica % sobre MO cuando no hay recursos HERR explícitos
        Assert.True(apu.HerramientasCalculadoUf > 0m,
            "HerramientasCalculadoUf debe ser positivo cuando hay MO y HerramientasPct > 0");
    }

    // ── ConjuntoElectricoService: expansión a cubicación ─────────────────────

    [Fact]
    public void ConjuntoService_expand_produces_cubicacion_rows_for_partida()
    {
        var svc = new ConjuntoElectricoService();
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-01", Nombre = "Test", Unidad = Unidades.UN,
            TipoConjunto = ConjuntoTipo.PuntoEnchufe,
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Codigo = "M1", Descripcion = "Cable", Unidad = "ML", Rendimiento = 3m, PrecioUf = 0.1m },
            }
        };
        var partida = new ItemizadoRow { Id = "P1", Descripcion = "Test", Cantidad = 2m, EsTitulo = false };

        var result = svc.ExpandToCubicacion(conjunto, partida, Enumerable.Empty<MaterialRow>());

        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, r => Assert.Equal("P1", r.ItemId));
    }

    [Fact]
    public void ConjuntoService_expand_does_not_alter_ItemizadoRow()
    {
        var svc = new ConjuntoElectricoService();
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-01", Nombre = "Test", Unidad = Unidades.UN,
            TipoConjunto = ConjuntoTipo.General,
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Codigo = "X", Rendimiento = 1m }
            }
        };
        var partida = new ItemizadoRow { Id = "P1", Descripcion = "Test", Cantidad = 5m };
        var cantidadOriginal = partida.Cantidad;

        svc.ExpandToCubicacion(conjunto, partida, null);

        Assert.Equal(cantidadOriginal, partida.Cantidad);
    }
}
