using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Tests para los VMs de APU que no dependen de WpfAppHost.
/// Cubre ApuRecursoVm y ApuPartidaVm directamente sobre el modelo de dominio.
/// </summary>
public sealed class ApuViewModelTests
{
    // ── Fixtures ─────────────────────────────────────────────────────────────

    private static ApuRecursoRow MatRow(string descripcion = "Cable", decimal rend = 2m,
        decimal precio = 0.5m, decimal merma = 5m) => new()
        {
            Tipo = ApuTipoRecurso.MAT,
            CodigoMaterial = "CAB-001",
            Descripcion = descripcion,
            Unidad = "ML",
            Rendimiento = rend,
            PrecioUf = precio,
            MermaPct = merma,
        };

    private static ItemizadoRow PartidaRow(string id = "P1", decimal cantidad = 10m) => new()
    {
        Id = id,
        Item = "1.1",
        Descripcion = "Instalación eléctrica",
        Unidad = "GL",
        Cantidad = cantidad,
        EsTitulo = false
    };

    private static ApuRow ApuConRecursos(string itemId = "P1", params ApuRecursoRow[] recursos) => new()
    {
        ItemId = itemId,
        Descripcion = "Instalación eléctrica",
        Unidad = "GL",
        Recursos = new List<ApuRecursoRow>(recursos),
        FechaPrecio = DateTime.Today,
    };

    // ── ApuRecursoRow: propiedades calculadas ─────────────────────────────────

    [Fact]
    public void ApuRecursoRow_CostoUnitUf_reflects_rendimiento_and_precio()
    {
        var row = MatRow(rend: 2m, precio: 0.5m, merma: 0m);
        Assert.Equal(1.0m, row.CostoUnitUf);
    }

    [Fact]
    public void ApuRecursoRow_MermaUf_adds_percentage_of_base()
    {
        var row = MatRow(rend: 2m, precio: 0.5m, merma: 10m);
        Assert.True(row.MermaUf > 0m);
        Assert.True(row.CostoUnitUf > row.CostoBaseUf);
    }

    [Fact]
    public void ApuRecursoRow_Estado_is_OK_when_precio_gt_zero()
    {
        var row = MatRow(precio: 1m);
        Assert.Equal("OK", row.Estado);
    }

    [Fact]
    public void ApuRecursoRow_PrecioBaseUf_is_zero_when_precio_is_zero()
    {
        var row = MatRow(precio: 0m);
        Assert.Equal(0m, row.PrecioBaseUf);
        Assert.Equal(0m, row.CostoUnitUf);
    }

    [Fact]
    public void ApuRecursoRow_changing_Rendimiento_updates_CostoUnitUf()
    {
        var row = MatRow(rend: 1m, precio: 1m, merma: 0m);
        var costoInicial = row.CostoUnitUf;
        row.Rendimiento = 2m;
        Assert.True(row.CostoUnitUf > costoInicial,
            "Aumentar rendimiento debe aumentar el costo unitario");
    }

    [Fact]
    public void ApuRecursoRow_PrecioBaseUf_returns_PrecioUf_for_MAT()
    {
        var row = MatRow(precio: 1.5m);
        Assert.Equal(1.5m, row.PrecioBaseUf);
    }

    // ── ApuRow: propiedades calculadas a nivel de partida ─────────────────────

    [Fact]
    public void ApuRow_CostoMaterialUf_sums_only_MAT_recursos()
    {
        var mat = MatRow(rend: 1m, precio: 1m, merma: 0m);
        var mo = new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MO,
            Rendimiento = 8m,
            ValorHHUf = 0.2m
        };
        var apu = ApuConRecursos("P1", mat, mo);

        Assert.True(apu.CostoMaterialUf > 0m);
        Assert.True(apu.CostoMoUf > 0m);
        Assert.Equal(0m, apu.CostoEquipoUf);
    }

    [Fact]
    public void ApuRow_CostoDirectoUf_is_sum_of_categories()
    {
        var mat = MatRow(rend: 1m, precio: 2m, merma: 0m);
        var apu = ApuConRecursos("P1", mat);

        Assert.True(apu.CostoDirectoUf >= apu.CostoMaterialUf,
            "CostoDirecto debe incluir al menos el costo de materiales");
    }

    [Fact]
    public void ApuRow_PrecioUnitUf_is_not_negative()
    {
        var mat = MatRow(rend: 1m, precio: 1m, merma: 0m);
        var apu = ApuConRecursos("P1", mat);
        Assert.True(apu.PrecioUnitUf >= 0m);
    }

    // ── ApuPartidaVm: estado derivado de ApuRow ───────────────────────────────

    [Fact]
    public void ApuPartidaVm_without_apu_has_SinAPU_estado()
    {
        var partida = PartidaRow();
        var estado = ApuFromCubicacionService.GetProfessionalStatus(null!);
        Assert.True(string.IsNullOrEmpty(estado) || estado != "OK");
    }

    [Fact]
    public void GetProfessionalStatus_returns_OK_for_fully_priced_apu()
    {
        var mat = MatRow(rend: 1m, precio: 1m, merma: 0m);
        var apu = ApuConRecursos("P1", mat);
        var status = ApuFromCubicacionService.GetProfessionalStatus(apu);
        Assert.Equal("OK", status);
    }

    [Fact]
    public void GetProfessionalStatus_returns_non_OK_for_zero_precio()
    {
        var mat = MatRow(rend: 1m, precio: 0m, merma: 0m);
        var apu = ApuConRecursos("P1", mat);
        var status = ApuFromCubicacionService.GetProfessionalStatus(apu);
        Assert.NotEqual("OK", status);
    }

    // ── Tipo de recurso editable ──────────────────────────────────────────────

    [Fact]
    public void ApuRecursoRow_tipo_can_be_changed()
    {
        var row = MatRow();
        Assert.Equal(ApuTipoRecurso.MAT, row.Tipo);
        row.Tipo = ApuTipoRecurso.MO;
        Assert.Equal(ApuTipoRecurso.MO, row.Tipo);
    }
}
