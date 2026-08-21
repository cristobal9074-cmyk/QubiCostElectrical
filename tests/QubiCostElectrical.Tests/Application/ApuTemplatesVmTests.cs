using System;
using System.Collections.Generic;
using System.Text.Json;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Tests para las funciones de plantillas APU y conjuntos eléctricos
/// que son accesibles sin WpfAppHost.
/// </summary>
public sealed class ApuTemplatesVmTests
{
    // ── ApuTemplateRow: serialización de recursos ─────────────────────────────

    [Fact]
    public void ApuTemplateRow_JsonRecursos_can_round_trip_resources()
    {
        var recursos = new List<ApuRecursoRow>
        {
            new() { Tipo = ApuTipoRecurso.MAT, Descripcion = "Cable", Unidad = "ML", Rendimiento = 2m, PrecioUf = 0.5m },
            new() { Tipo = ApuTipoRecurso.MO, Descripcion = "Instalador", Unidad = "HH", Rendimiento = 4m, ValorHHUf = 0.3m },
        };

        var json = JsonSerializer.Serialize(recursos);
        var row = new ApuTemplateRow { JsonRecursos = json, Nombre = "Test" };

        var deserialized = JsonSerializer.Deserialize<List<ApuRecursoRow>>(row.JsonRecursos,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("Cable", deserialized[0].Descripcion);
        Assert.Equal(ApuTipoRecurso.MO, deserialized[1].Tipo);
    }

    [Fact]
    public void ApuTemplateRow_empty_json_deserializes_to_empty_list()
    {
        var row = new ApuTemplateRow { JsonRecursos = "[]" };
        var deserialized = JsonSerializer.Deserialize<List<ApuRecursoRow>>(row.JsonRecursos,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized!);
    }

    // ── ConjuntoElectricoRow: estructura ──────────────────────────────────────

    [Fact]
    public void ConjuntoElectricoRow_defaults_are_valid()
    {
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-001",
            Nombre = "Punto de enchufe simple",
            TipoConjunto = ConjuntoTipo.PuntoEnchufe,
            Unidad = Unidades.UN,
        };

        Assert.Equal("CE-001", conjunto.Codigo);
        Assert.Equal(ConjuntoTipo.PuntoEnchufe, conjunto.TipoConjunto);
        Assert.Equal(3m, conjunto.HerramientasPct);
        Assert.Equal(2m, conjunto.ImprevistosAlzaPct);
        Assert.Empty(conjunto.Recursos);
    }

    [Fact]
    public void ConjuntoElectricoRow_can_hold_multiple_recurso_tipos()
    {
        var conjunto = new ConjuntoElectricoRow { Nombre = "Test" };
        conjunto.Recursos.Add(new ConjuntoRecursoRow { Tipo = ApuTipoRecurso.MAT, Codigo = "M1" });
        conjunto.Recursos.Add(new ConjuntoRecursoRow { Tipo = ApuTipoRecurso.MO, Codigo = "MO1" });
        conjunto.Recursos.Add(new ConjuntoRecursoRow { Tipo = ApuTipoRecurso.EQ, Codigo = "EQ1" });

        Assert.Equal(3, conjunto.Recursos.Count);
        Assert.Equal(ApuTipoRecurso.MAT, conjunto.Recursos[0].Tipo);
        Assert.Equal(ApuTipoRecurso.MO, conjunto.Recursos[1].Tipo);
    }

    // ── Conversión de recursos APU → ConjuntoRecursoRow ─────────────────────

    [Fact]
    public void ApuRecursoRow_fields_map_to_ConjuntoRecursoRow()
    {
        var apu = new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            CodigoMaterial = "CAB-001",
            Descripcion = "Cable THHN 2.5mm",
            Unidad = "ML",
            Rendimiento = 3m,
            PrecioUf = 0.08m,
            MermaPct = 5m,
        };

        var conjRecurso = new ConjuntoRecursoRow
        {
            Tipo = apu.Tipo,
            Codigo = apu.CodigoMaterial,
            Descripcion = apu.Descripcion,
            Unidad = apu.Unidad,
            Rendimiento = apu.Rendimiento,
            Factor = 1m,
            PrecioUf = apu.PrecioUf,
            MermaPct = apu.MermaPct,
        };

        Assert.Equal(ApuTipoRecurso.MAT, conjRecurso.Tipo);
        Assert.Equal("CAB-001", conjRecurso.Codigo);
        Assert.Equal(3m, conjRecurso.Rendimiento);
        Assert.Equal(1m, conjRecurso.Factor);
    }

    // ── ConjuntoTipo: valores definidos ──────────────────────────────────────

    [Fact]
    public void ConjuntoTipo_All_contains_expected_values()
    {
        Assert.Contains(ConjuntoTipo.PuntoEnchufe, ConjuntoTipo.All);
        Assert.Contains(ConjuntoTipo.PuntoAlumbrado, ConjuntoTipo.All);
        Assert.Contains(ConjuntoTipo.Alimentador, ConjuntoTipo.All);
        Assert.Contains(ConjuntoTipo.General, ConjuntoTipo.All);
        Assert.True(ConjuntoTipo.All.Length >= 7);
    }
}
