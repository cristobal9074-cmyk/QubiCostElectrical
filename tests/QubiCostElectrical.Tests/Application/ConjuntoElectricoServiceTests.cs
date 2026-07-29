using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ConjuntoElectricoServiceTests
{
    private readonly ConjuntoElectricoService _service = new();

    private static MaterialRow Mat(string codigo, string desc, string unidad = "UN", decimal matUf = 1m, decimal moUf = 0m)
        => new() { Codigo = codigo, Descripcion = desc, Unidad = unidad, MatUf = matUf, MoUf = moUf };

    private static ItemizadoRow Partida(string id, decimal cantidad = 1m, string descripcion = "")
        => new() { Id = id, Item = id, Descripcion = string.IsNullOrWhiteSpace(descripcion) ? id : descripcion, Cantidad = cantidad };

    private static ConjuntoElectricoRow Conjunto(string codigo, params ConjuntoRecursoRow[] recursos)
        => new()
        {
            Codigo = codigo,
            Nombre = codigo,
            TipoConjunto = ConjuntoTipo.PuntoEnchufe,
            Unidad = Unidades.UN,
            HerramientasPct = 3m,
            MermaPct = 5m,
            Recursos = recursos.ToList()
        };

    private static ConjuntoRecursoRow MatRecurso(string codigo, decimal rendimiento = 1m, decimal factor = 1m, decimal minimo = 0m)
        => new() { Tipo = ApuTipoRecurso.MAT, Codigo = codigo, Rendimiento = rendimiento, Factor = factor, Minimo = minimo };

    private static ConjuntoRecursoRow MoRecurso(decimal rendimiento = 0.5m, decimal valorHh = 12m)
        => new() { Tipo = ApuTipoRecurso.MO, Descripcion = "Electricista", Unidad = "HH", Rendimiento = rendimiento, ValorHHUf = valorHh };

    [Fact]
    public void ExpandToCubicacion_SimpleConjunto_ProducesMaterialRows()
    {
        var conjunto = Conjunto("PE-01", MatRecurso("CABLE2.5", 12m));
        var materiales = new[] { Mat("CABLE2.5", "Cable 2.5mm", "ML", 0.08m) };
        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Single(result.Rows);
        Assert.Equal("CABLE2.5", result.Rows[0].Codigo);
        Assert.Equal(120m, result.Rows[0].Cantidad);
        Assert.Equal(ConjuntoTipo.PuntoEnchufe, result.Rows[0].Area);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ExpandToCubicacion_SkipsLaborResources()
    {
        var conjunto = Conjunto("PE-01", MatRecurso("CABLE2.5", 1m), MoRecurso());
        var materiales = new[] { Mat("CABLE2.5", "Cable 2.5mm", "ML", 0.08m) };
        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 4m), materiales);

        Assert.Single(result.Rows);
        Assert.Equal("MAT", result.Rows[0].Tipo);
    }

    [Fact]
    public void ExpandToCubicacion_MaterialNotFound_AddsWarning()
    {
        var conjunto = Conjunto("PE-01", MatRecurso("INEXISTENTE", 1m));
        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 1m), Enumerable.Empty<MaterialRow>());

        Assert.Empty(result.Rows);
        Assert.Contains("INEXISTENTE", result.Warnings[0]);
    }

    [Fact]
    public void BuildApu_IncludesMaterialsAndLaborWithHeaderFactors()
    {
        var conjunto = Conjunto("PE-01", MatRecurso("CABLE2.5", 12m), MoRecurso(0.4m, 15m));
        var materiales = new[] { Mat("CABLE2.5", "Cable 2.5mm", "ML", 0.08m) };
        var apu = _service.BuildApu(conjunto, Partida("P1", 5m), materiales);

        Assert.Equal(5m, apu.CantidadPartida);
        Assert.Equal(3m, apu.HerramientasPct);
        Assert.Equal(5m, apu.MermaPct);
        Assert.Equal(2, apu.Recursos.Count);
        Assert.Contains(apu.Recursos, r => r.Tipo == ApuTipoRecurso.MAT && r.Rendimiento == 12m);
        Assert.Contains(apu.Recursos, r => r.Tipo == ApuTipoRecurso.MO && r.ValorHHUf == 15m && r.RendHH == 0.4m);
    }

    [Fact]
    public void SuggestConjunto_MatchesKeywords()
    {
        var conjuntos = new List<ConjuntoElectricoRow>
        {
            new() { Codigo = "PE-01", Nombre = "Punto enchufe estándar", Keywords = "enchufe;toma", TipoConjunto = ConjuntoTipo.PuntoEnchufe },
            new() { Codigo = "PA-01", Nombre = "Punto alumbrado LED", Keywords = "alumbrado;luminaria", TipoConjunto = ConjuntoTipo.PuntoAlumbrado }
        };

        var match = _service.SuggestConjunto("Instalación punto de enchufe cocina", conjuntos);

        Assert.NotNull(match);
        Assert.Equal("PE-01", match!.Codigo);
    }

    [Fact]
    public void ApplyBulk_ReplacesCubicacionAndUpdatesApu()
    {
        var conjunto = Conjunto("PE-01", MatRecurso("CABLE2.5", 2m), MoRecurso(0.2m, 10m));
        var partidas = new List<ItemizadoRow>
        {
            Partida("P1", 3m),
            new ItemizadoRow { Id = "P2", Item = "P2", Descripcion = "Titulo", Cantidad = 1m, EsTitulo = true }
        };
        var cubicacion = new List<CubicacionRow>
        {
            new() { Id = "old", ItemId = "P1", Codigo = "VIEJO", Cantidad = 99m }
        };
        var apus = new List<ApuRow>();
        var materiales = new[] { Mat("CABLE2.5", "Cable 2.5mm", "ML", 0.08m) };

        var result = _service.ApplyBulk(
            conjunto,
            partidas,
            cubicacion,
            apus,
            materiales,
            ConjuntoApplyMode.CubicacionYApu,
            replaceCubicacion: true);

        Assert.Equal(1, result.PartidasProcesadas);
        Assert.Equal(1, result.FilasCubicacion);
        Assert.Equal(1, result.ApusActualizados);
        Assert.DoesNotContain(cubicacion, c => c.Codigo == "VIEJO");
        Assert.Single(apus);
        Assert.Equal("P1", apus[0].ItemId);
    }

    [Fact]
    public void TouchVersion_IncrementsVersionAndTimestamp()
    {
        var conjunto = new ConjuntoElectricoRow { Version = 2, ActualizadoEn = new DateTime(2020, 1, 1) };
        _service.TouchVersion(conjunto);

        Assert.Equal(3, conjunto.Version);
        Assert.True(conjunto.ActualizadoEn > new DateTime(2020, 1, 1));
    }
}
