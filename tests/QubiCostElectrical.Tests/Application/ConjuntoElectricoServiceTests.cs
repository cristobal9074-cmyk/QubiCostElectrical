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

    // ── Maestro / Esclavo ──────────────────────────────────────────────────────

    private static ConjuntoRecursoRow EspaciamientoRecurso(string codigo, decimal paso)
        => new()
        {
            Tipo = ApuTipoRecurso.MAT,
            Codigo = codigo,
            TipoCalculo = TipoCalculoParametrico.Espaciamiento,
            Espaciamiento = paso
        };

    [Fact]
    public void ExpandToCubicacion_EsclavoEspaciamiento_UsaCantidadDelMaestro()
    {
        var maestro = MatRecurso("TUBO20", 1m);
        var esclavo = EspaciamientoRecurso("ABR-CADDY", 1.5m);
        esclavo.MaestroId = maestro.Id;
        var conjunto = Conjunto("CS-01", maestro, esclavo);
        var materiales = new[]
        {
            Mat("TUBO20", "Tuberia EMT 20", "ML"),
            Mat("ABR-CADDY", "Abrazadera CADDY", "UN")
        };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 1000m), materiales);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1000m, result.Rows.First(r => r.Codigo == "TUBO20").Cantidad);
        // Ceil(1000 / 1.5) = 667 — la base es el maestro, no la partida
        Assert.Equal(667m, result.Rows.First(r => r.Codigo == "ABR-CADDY").Cantidad);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ExpandToCubicacion_EsclavoFactor_UsaCantidadDelMaestroComoBase()
    {
        var maestro = MatRecurso("PERNO", 2m);      // partida 10 -> 20
        var esclavo = MatRecurso("ARANDELA", 3m);    // 20 x 3 = 60 (sin maestro seria 10 x 3 = 30)
        esclavo.MaestroId = maestro.Id;
        var conjunto = Conjunto("CS-02", maestro, esclavo);
        var materiales = new[] { Mat("PERNO", "Perno", "UN"), Mat("ARANDELA", "Arandela", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Equal(60m, result.Rows.First(r => r.Codigo == "ARANDELA").Cantidad);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ExpandToCubicacion_EsclavoSinMaestro_SobreBaseDePartida_ConWarning()
    {
        var esclavo = MatRecurso("ABR", 1m);
        esclavo.MaestroId = "NO-EXISTE";
        var conjunto = Conjunto("CS-03", esclavo);

        var result = _service.ExpandToCubicacion(
            conjunto, Partida("P1", 10m), new[] { Mat("ABR", "Abrazadera", "UN") });

        Assert.Equal(10m, result.Rows.Single().Cantidad);
        Assert.Contains(result.Warnings, w => w.Contains("es esclavo pero su maestro no existe"));
    }

    [Fact]
    public void ExpandToCubicacion_EsclavoDeEsclavo_NoSeEncadena()
    {
        var a = MatRecurso("MAT-A", 1m);
        var b = MatRecurso("MAT-B", 1m);
        b.MaestroId = a.Id;
        var c = MatRecurso("MAT-C", 1m);
        c.MaestroId = b.Id; // b es esclavo: no puede ser base de c
        var conjunto = Conjunto("CS-04", a, b, c);
        var materiales = new[] { Mat("MAT-A", "A", "UN"), Mat("MAT-B", "B", "UN"), Mat("MAT-C", "C", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Warnings, w => w.Contains("'MAT-C'") && w.Contains("es esclavo pero su maestro no existe"));
    }

    [Fact]
    public void ExpandToCubicacion_VariosMaestrosYEsclavos_CalculaCadaParIndependiente()
    {
        var tubo = MatRecurso("TUBO20", 1m);                 // 100 -> 100
        var clip = EspaciamientoRecurso("CLIP", 1.5m);        // base 100 -> Ceil(100/1.5)=67
        clip.MaestroId = tubo.Id;
        var canal = EspaciamientoRecurso("CANAL", 2m);        // 100 -> Ceil(100/2)=50
        var abra = EspaciamientoRecurso("ABRA", 5m);          // base 50 -> Ceil(50/5)=10
        abra.MaestroId = canal.Id;
        var conjunto = Conjunto("CS-05", tubo, clip, canal, abra);
        var materiales = new[]
        {
            Mat("TUBO20", "Tubo", "ML"), Mat("CLIP", "Clip", "UN"),
            Mat("CANAL", "Canal", "ML"), Mat("ABRA", "Abrazadera", "UN")
        };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 100m), materiales);

        Assert.Equal(100m, result.Rows.First(r => r.Codigo == "TUBO20").Cantidad);
        Assert.Equal(67m, result.Rows.First(r => r.Codigo == "CLIP").Cantidad);
        Assert.Equal(50m, result.Rows.First(r => r.Codigo == "CANAL").Cantidad);
        Assert.Equal(10m, result.Rows.First(r => r.Codigo == "ABRA").Cantidad);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void BuildApu_Esclavo_RendimientoEsCantidadResueltaSobrePartida()
    {
        var maestro = MatRecurso("TUBO20", 12m);             // partida 5 -> 60
        var esclavo = EspaciamientoRecurso("ABR", 1.5m);      // Ceil(60/1.5)=40 -> rend 40/5=8
        esclavo.MaestroId = maestro.Id;
        var conjunto = Conjunto("CS-06", maestro, esclavo);
        var materiales = new[] { Mat("TUBO20", "Tubo", "ML"), Mat("ABR", "Abrazadera", "UN") };

        var apu = _service.BuildApu(conjunto, Partida("P1", 5m), materiales);

        Assert.Equal(12m, apu.Recursos.First(r => r.CodigoMaterial == "TUBO20").Rendimiento);
        Assert.Equal(8m, apu.Recursos.First(r => r.CodigoMaterial == "ABR").Rendimiento);
    }

    [Fact]
    public void BuildApu_EspaciamientoSinMaestro_RendimientoEsCantidadEntrePartida()
    {
        var recurso = EspaciamientoRecurso("CLIP", 1.5m);     // Ceil(1000/1.5)=667 -> rend 0.667
        var conjunto = Conjunto("CS-07", recurso);
        var materiales = new[] { Mat("CLIP", "Clip", "UN") };

        var apu = _service.BuildApu(conjunto, Partida("P1", 1000m), materiales);

        Assert.Equal(0.667m, apu.Recursos.Single().Rendimiento);
    }

    [Fact]
    public void CloneConjunto_RemapeaIdsDeRecursosYMaestros()
    {
        var maestro = MatRecurso("TUBO20", 1m);
        maestro.Id = "M1";
        var esclavo = EspaciamientoRecurso("ABR", 1.5m);
        esclavo.Id = "E1";
        esclavo.MaestroId = "M1";
        esclavo.Minimo = 3m;
        var original = Conjunto("CE-CLONE", maestro, esclavo);
        original.Id = "CONJ-1";
        original.HerramientasPct = 7m;
        original.ImprevistosAlzaPct = 4m;

        var clone = ConjuntoElectricoService.CloneConjunto(original);

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(2, clone.Recursos.Count);
        Assert.DoesNotContain(clone.Recursos, r => r.Id == "M1" || r.Id == "E1");
        var cloneEsclavo = clone.Recursos.First(r => r.Codigo == "ABR");
        var cloneMaestro = clone.Recursos.First(r => r.Codigo == "TUBO20");
        Assert.Equal(cloneMaestro.Id, cloneEsclavo.MaestroId);
        Assert.Equal(3m, cloneEsclavo.Minimo);
        Assert.Equal(7m, clone.HerramientasPct);
        Assert.Equal(4m, clone.ImprevistosAlzaPct);
        // El original no se toca
        Assert.Equal("M1", original.Recursos.First(r => r.Codigo == "ABR").MaestroId);
    }

    [Fact]
    public void CloneConjunto_EsclavoHuerfano_QuedaLiberadoComoMaestro()
    {
        var esclavo = MatRecurso("ABR", 1m);
        esclavo.MaestroId = "GHOST";
        var original = Conjunto("CE-ORPH", esclavo);

        var clone = ConjuntoElectricoService.CloneConjunto(original);

        Assert.Equal("", clone.Recursos.Single().MaestroId);
    }

    [Fact]
    public void LiberarEsclavos_ConvierteEsclavosDelMaestroEliminadoEnMaestros()
    {
        var e1 = MatRecurso("ABR", 1m); e1.MaestroId = "M1";
        var e2 = MatRecurso("CLIP", 1m); e2.MaestroId = "m1"; // distinto casing
        var otro = MatRecurso("CINTA", 1m); otro.MaestroId = "OTRO";
        var conjunto = Conjunto("CE-LIB", MatRecurso("TUBO", 1m), e1, e2, otro);

        int liberados = ConjuntoElectricoService.LiberarEsclavos(conjunto, "M1");

        Assert.Equal(2, liberados);
        Assert.Equal("", e1.MaestroId);
        Assert.Equal("", e2.MaestroId);
        Assert.Equal("OTRO", otro.MaestroId);
    }

    // ── Carga en cubicación (cantidades en cero, orden, vínculo) ──────────────

    [Fact]
    public void ExpandToCubicacion_CantidadesEnCero_DejaTodasLasFilasEnCero()
    {
        var maestro = MatRecurso("TUBO20", 1m);
        var esclavo = EspaciamientoRecurso("ABR-CADDY", 1.5m);
        esclavo.MaestroId = maestro.Id;
        var conjunto = Conjunto("CS-ZERO", maestro, esclavo);
        var materiales = new[] { Mat("TUBO20", "Tuberia EMT 20", "ML"), Mat("ABR-CADDY", "Abrazadera", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 1000m), materiales, cantidadesEnCero: true);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal(0m, r.Cantidad));
        // El vínculo maestro→esclavo se conserva aunque las cantidades estén en cero
        Assert.NotNull(result.Rows.First(r => r.Codigo == "ABR-CADDY").Esclavo);
    }

    [Fact]
    public void ExpandToCubicacion_CantidadesEnCero_PreservaOrdenDeLaPlantilla()
    {
        // Orden intencionalmente no alfabético ni por tipo: debe salir igual que en la pestaña.
        var conjunto = Conjunto("CS-ORD", MatRecurso("ZETA", 1m), MatRecurso("ALFA", 2m), MatRecurso("MILO", 3m));
        var materiales = new[]
        {
            Mat("ZETA", "Zeta", "ML"), Mat("ALFA", "Alfa", "UN"), Mat("MILO", "Milo", "UN")
        };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales, cantidadesEnCero: true);

        Assert.Equal(new[] { "ZETA", "ALFA", "MILO" }, result.Rows.Select(r => r.Codigo).ToArray());
    }

    [Fact]
    public void ExpandToCubicacion_Esclavo_GuardaVinculoConParametrosResueltos()
    {
        var maestro = MatRecurso("TUBO20", 1m);
        var esclavo = EspaciamientoRecurso("ABR-CADDY", 1.5m);
        esclavo.MaestroId = maestro.Id;
        esclavo.Minimo = 3m;
        var conjunto = Conjunto("CS-VINC", maestro, esclavo);
        conjunto.Parametros["factor"] = 2m;
        var materiales = new[] { Mat("TUBO20", "Tuberia", "ML"), Mat("ABR-CADDY", "Abrazadera", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        var filaMaestro = result.Rows.First(r => r.Codigo == "TUBO20");
        var filaEsclavo = result.Rows.First(r => r.Codigo == "ABR-CADDY");
        Assert.Null(filaMaestro.Esclavo);
        Assert.NotNull(filaEsclavo.Esclavo);
        Assert.Equal("TUBO20", filaEsclavo.Esclavo!.MaestroCodigo);
        Assert.Equal(TipoCalculoParametrico.Espaciamiento, filaEsclavo.Esclavo.TipoCalculo);
        Assert.Equal(1.5m, filaEsclavo.Esclavo.Espaciamiento);
        Assert.Equal(3m, filaEsclavo.Esclavo.Minimo);
        Assert.Equal(2m, filaEsclavo.Esclavo.Factor); // escalado con Parametros["factor"]
        Assert.Equal(1m, filaEsclavo.Esclavo.Rendimiento);
    }

    [Fact]
    public void ExpandToCubicacion_EsclavoOrfano_NoGuardaVinculo()
    {
        var esclavo = MatRecurso("ABR", 1m);
        esclavo.MaestroId = "NO-EXISTE";
        var conjunto = Conjunto("CS-ORPH", esclavo);

        var result = _service.ExpandToCubicacion(
            conjunto, Partida("P1", 10m), new[] { Mat("ABR", "Abrazadera", "UN") });

        Assert.Null(result.Rows.Single().Esclavo);
        Assert.Contains(result.Warnings, w => w.Contains("es esclavo pero su maestro no existe"));
    }

    [Fact]
    public void ExpandToCubicacion_EsclavoDuplicado_MantieneVinculoTrasMerge()
    {
        var maestro = MatRecurso("TUBO20", 1m);
        var esclavo1 = MatRecurso("ABR", 1m);
        esclavo1.MaestroId = maestro.Id;
        var esclavo2 = MatRecurso("ABR", 1m); // mismo código y precios: se fusiona con esclavo1
        esclavo2.MaestroId = maestro.Id;
        var conjunto = Conjunto("CS-MERGE", maestro, esclavo1, esclavo2);
        var materiales = new[] { Mat("TUBO20", "Tubo", "ML"), Mat("ABR", "Abrazadera", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales, cantidadesEnCero: true);

        Assert.Equal(2, result.Rows.Count);
        var filaEsclavo = result.Rows.First(r => r.Codigo == "ABR");
        Assert.NotNull(filaEsclavo.Esclavo);
        Assert.Equal("TUBO20", filaEsclavo.Esclavo!.MaestroCodigo);
    }

    // ── ComputeEsclavoValue (propagación en la hoja de cubicación) ────────────

    [Fact]
    public void ComputeEsclavoValue_Espaciamiento_AplicaCeilYPaso()
    {
        var vinculo = new CubicacionEsclavoVinculo
        {
            TipoCalculo = TipoCalculoParametrico.Espaciamiento,
            Espaciamiento = 1.5m,
            Factor = 2m // no aplica en modo espaciamiento
        };

        Assert.Equal(667m, ConjuntoElectricoService.ComputeEsclavoValue(1000m, vinculo));
        Assert.Equal(7m, ConjuntoElectricoService.ComputeEsclavoValue(10m, vinculo)); // Ceil(10/1.5) = 7
    }

    [Fact]
    public void ComputeEsclavoValue_Factor_MultiplicaRendimientoYFactor()
    {
        var vinculo = new CubicacionEsclavoVinculo { Rendimiento = 3m, Factor = 2m };

        Assert.Equal(120m, ConjuntoElectricoService.ComputeEsclavoValue(20m, vinculo));
        Assert.Equal(60m, ConjuntoElectricoService.ComputeEsclavoValue(20m, new CubicacionEsclavoVinculo { Rendimiento = 3m, Factor = 0m }));
    }

    [Fact]
    public void ComputeEsclavoValue_ConMinimo_NoBajaDelMinimo()
    {
        var vinculo = new CubicacionEsclavoVinculo { Rendimiento = 1m, Factor = 1m, Minimo = 5m };

        Assert.Equal(5m, ConjuntoElectricoService.ComputeEsclavoValue(2m, vinculo));
        Assert.Equal(20m, ConjuntoElectricoService.ComputeEsclavoValue(20m, vinculo));
    }

    // ── Operaciones (fórmula libre por fila) ─────────────────────────────────

    private static ConjuntoRecursoRow OpRecurso(string codigo, string operacion)
        => new() { Tipo = ApuTipoRecurso.MAT, Codigo = codigo, Operacion = operacion };

    [Fact]
    public void ExpandToCubicacion_OperacionCombinandoDosMaestros_SumaSusCantidades()
    {
        var paz = MatRecurso("PAZ", 5m);       // partida 10 -> 50
        var teeg = MatRecurso("TEEG500", 3m);  // partida 10 -> 30
        var op = OpRecurso("OP1", "PAZ * 2 + TEEG500");
        var conjunto = Conjunto("OP-01", paz, teeg, op);
        var materiales = new[]
        {
            Mat("PAZ", "Conductor PAZ", "ML"),
            Mat("TEEG500", "Termica TEEG500", "UN"),
            Mat("OP1", "Combinada", "UN")
        };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Empty(result.Warnings);
        var filaOp = result.Rows.Single(r => r.Codigo == "OP1");
        Assert.Equal(130m, filaOp.Cantidad); // 50 * 2 + 30
        Assert.Equal("PAZ * 2 + TEEG500", filaOp.Operacion);
        // El orden de la plantilla se preserva (la operación donde el usuario la puso)
        Assert.Equal(new[] { "PAZ", "TEEG500", "OP1" }, result.Rows.Select(r => r.Codigo).ToArray());
    }

    [Fact]
    public void ExpandToCubicacion_OperacionReferenciadaEnOtraOperacion_SumaEnOrdenTopologico()
    {
        var base1 = MatRecurso("PAZ", 1m);                    // partida 10 -> 10
        var op1 = OpRecurso("OP1", "PAZ * 3");                // 30
        var op2 = OpRecurso("OP2", "OP1 + PAZ");              // 30 + 10 = 40
        var conjunto = Conjunto("OP-02", base1, op1, op2);
        var materiales = new[]
        {
            Mat("PAZ", "Conductor PAZ", "ML"),
            Mat("OP1", "Intermedia", "UN"),
            Mat("OP2", "Final", "UN")
        };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Empty(result.Warnings);
        Assert.Equal(30m, result.Rows.Single(r => r.Codigo == "OP1").Cantidad);
        Assert.Equal(40m, result.Rows.Single(r => r.Codigo == "OP2").Cantidad);
    }

    [Fact]
    public void ExpandToCubicacion_OperacionesEnCiclo_ExcluyeConAdvertencia()
    {
        var a = OpRecurso("OPA", "OPB * 2");
        var b = OpRecurso("OPB", "OPA + 1");
        var conjunto = Conjunto("OP-CYC", a, b);
        var materiales = new[] { Mat("OPA", "Ciclo A", "UN"), Mat("OPB", "Ciclo B", "UN") };

        var result = _service.ExpandToCubicacion(conjunto, Partida("P1", 10m), materiales);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal(0m, r.Cantidad));
        Assert.Contains(result.Warnings, w => w.Contains("ciclo"));
    }

    [Fact]
    public void ExpandToCubicacion_OperacionConVariableDesconocida_AdvierteYQuedaEnCero()
    {
        var op = OpRecurso("OP1", "INEXISTENTE * 2");
        var conjunto = Conjunto("OP-UNK", op);

        var result = _service.ExpandToCubicacion(
            conjunto, Partida("P1", 10m), new[] { Mat("OP1", "Combinada", "UN") });

        Assert.Equal(0m, result.Rows.Single().Cantidad);
        Assert.Contains(result.Warnings, w => w.Contains("INEXISTENTE"));
    }

    [Fact]
    public void BuildApu_Operacion_RendimientoEsLaCantidadResueltaSobrePartida()
    {
        var paz = MatRecurso("PAZ", 5m);       // 10 x 5 = 50
        var teeg = MatRecurso("TEEG500", 3m);  // 10 x 3 = 30
        var op = OpRecurso("OP1", "PAZ + TEEG500");
        var conjunto = Conjunto("OP-APU", paz, teeg, op);
        var materiales = new[]
        {
            Mat("PAZ", "Conductor PAZ", "ML"),
            Mat("TEEG500", "Termica TEEG500", "UN"),
            Mat("OP1", "Combinada", "UN")
        };

        var apu = _service.BuildApu(conjunto, Partida("P1", 10m), materiales);

        // Cantidad resuelta 80 / partida 10 = rendimiento 8 por unidad
        Assert.Equal(8m, apu.Recursos.First(r => r.CodigoMaterial == "OP1").Rendimiento);
    }
}
