using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// RED DE REPRODUCIBILIDAD del motor de presupuesto. No fija la "respuesta correcta" de negocio;
/// fija que el motor sea DETERMINISTA y AUDITABLE:
///   1. mismo proyecto -> mismo total, en corridas repetidas.
///   2. el total NO depende del orden de las colecciones de entrada.
///   3. cálculo incremental == cálculo completo, para cada partida marcada sucia, en todos los ApuMode.
///   4. la oferta al cliente suma exactamente la venta neta y es determinista.
///   5. snapshot dorado del pipeline completo (BASELINE PRE-REFACTOR C1-C5+A1): cualquier
///      corrección de esos hallazgos debe mover estos números DE FORMA DELIBERADA y revisada.
///
/// Si un cambio rompe 1-4 sin intención, es una regresión de reproducibilidad y hay que revertirlo.
/// El test 5 se actualiza a mano con cada corrección, documentando el antes/después.
/// </summary>
public sealed class BudgetEngineReproducibilityTests
{
    // ----- Fixture: proyecto realista mediano -----------------------------------------------

    private sealed record Project(
        List<CubicacionRow> Cubicacion,
        List<ApuRow> Apus,
        List<ItemizadoRow> Itemizado,
        List<ChangeOrderRow> ChangeOrders,
        BudgetSettings Settings);

    private static readonly IReadOnlyDictionary<string, decimal> FamilyFactors =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Canalización"] = 1.57m,
            ["Conductores"] = 1.50m,
            ["Tableros"] = 1.30m,
            ["Iluminación"] = 1.17m,
        };

    private static Project BuildRealisticProject(BudgetApuMode mode, bool familyMarkups = false)
    {
        // 6 partidas: P1/P2 solo cubicación, P3 cubicación+APU complementario, P4 solo APU,
        // P5 cubicación con descuento negociado + override de merma, P6 título (sin costo).
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1", Descripcion = "Canalización EMT", Unidad = "ML", Cantidad = 120m, CantidadOriginal = 120m },
            new() { Id = "P2", Item = "2", Descripcion = "Conductores THHN", Unidad = "ML", Cantidad = 480m, CantidadOriginal = 480m },
            new() { Id = "P3", Item = "3", Descripcion = "Tableros de distribución", Unidad = "UN", Cantidad = 4m, CantidadOriginal = 4m },
            new() { Id = "P4", Item = "4", Descripcion = "Malla de tierra", Unidad = "GL", Cantidad = 1m, CantidadOriginal = 1m },
            new() { Id = "P5", Item = "5", Descripcion = "Luminarias LED", Unidad = "UN", Cantidad = 60m, CantidadOriginal = 60m },
            new() { Id = "P6", Item = "6", Descripcion = "OBRAS COMPLEMENTARIAS", Unidad = "GL", Cantidad = 1m, CantidadOriginal = 1m, EsTitulo = true },
        };

        var cubicacion = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Tipo = "MAT", Codigo = "EMT-20", Descripcion = "Tubería EMT 20mm", Unidad = "ML", Categoria = "Canalización",
                    Cantidad = 132m, MatClp = 1850m, MoClp = 700m, Area = "Subestación" },
            new() { ItemId = "P1", Tipo = "MAT", Codigo = "EMT-CURVA", Descripcion = "Curva EMT 20mm", Unidad = "UN", Categoria = "Canalización",
                    Cantidad = 40m, MatClp = 520m, MoClp = 180m, Area = "Subestación" },
            new() { ItemId = "P2", Tipo = "MAT", Codigo = "THHN-12", Descripcion = "Cable THHN 12 AWG", Unidad = "ML", Categoria = "Conductores",
                    Cantidad = 520m, MatClp = 640m, MoClp = 210m, Area = "Pisos" },
            new() { ItemId = "P3", Tipo = "MAT", Codigo = "TAB-CH", Descripcion = "Gabinete tablero", Unidad = "UN", Categoria = "Tableros",
                    Cantidad = 4m, MatClp = 285000m, MoClp = 60000m, Area = "Subestación" },
            new() { ItemId = "P5", Tipo = "MAT", Codigo = "LED-40W", Descripcion = "Luminaria LED 40W", Unidad = "UN", Categoria = "Iluminación",
                    Cantidad = 62m, MatClp = 18500m, MoClp = 4200m, Area = "Pisos", MermaPctOverride = 4m },
        };

        var apus = new List<ApuRow>
        {
            new()
            {
                ItemId = "P3", Descripcion = "Montaje y conexionado de tablero", Unidad = "UN",
                HerramientasPct = 3m, MermaPct = 2m, TransportePct = 1m, ImprevistosAlzaPct = 2m,
                Recursos =
                {
                    new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO-ELEC", Descripcion = "Eléctrico", Unidad = "HH", Rendimiento = 6m, PrecioUf = 9500m },
                    new() { Tipo = ApuTipoRecurso.EQ, CodigoMaterial = "EQ-TORQUE", Descripcion = "Torquímetro", Unidad = "DIA", Rendimiento = 0.5m, PrecioUf = 12000m },
                    new() { Tipo = ApuTipoRecurso.SC, CodigoMaterial = "SC-CERT", Descripcion = "Certificación SEC", Unidad = "UN", Rendimiento = 1m, PrecioUf = 45000m },
                },
            },
            new()
            {
                ItemId = "P4", Descripcion = "Malla de tierra perimetral", Unidad = "GL",
                HerramientasPct = 3m, MermaPct = 5m, TransportePct = 2m, ImprevistosAlzaPct = 2m,
                Recursos =
                {
                    new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "CU-DESNUDO", Descripcion = "Cable Cu desnudo 2/0", Unidad = "ML", Rendimiento = 80m, PrecioUf = 3200m },
                    new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "COPPERWELD", Descripcion = "Barra copperweld", Unidad = "UN", Rendimiento = 8m, PrecioUf = 15000m },
                    new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO-CUAD", Descripcion = "Cuadrilla tierra", Unidad = "HH", Rendimiento = 24m, PrecioUf = 8200m },
                },
            },
        };

        var changeOrders = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-001", Titulo = "Ampliación sala eléctrica", Estado = "Aprobada", DeltaCostoUf = 3_250_000m },
            new() { Numero = "OC-002", Titulo = "Cambio de recorrido bandeja", Estado = "Borrador", DeltaCostoUf = 900_000m },
        };

        var settings = new BudgetSettings
        {
            ApuMode = mode,
            MetrosCuadrados = 2400m,
            HerramientasPct = 2.5m,
            MermasPct = 3m,
            Currency = new CurrencyPolicy { MonedaPresupuesto = "CLP", ValorUfClp = 39250m },
            NegotiatedDiscounts = new List<NegotiatedDiscountRow>
            {
                new() { Codigo = "LED-40W", DescuentoPct = 8m },
            },
            Markup = new MarkupPolicy
            {
                MarkupMethod = MarkupMethod.SobreVenta,
                GastosGeneralesPct = 13m,
                UtilidadPct = 16m,
                ValorAgregadoEquiposPct = 3m,
                RiesgoPct = 2m,
                DescuentoPct = 1.5m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true,
            },
        };

        if (familyMarkups)
        {
            settings.Markup.FactoresPresentacionPorFamilia = true;
            // C-1: proyecto bien formado — dict de factores por familia + factor por defecto
            // del proyecto (APU por área desconocida, OC y cargos globales lo usan).
            settings.DefaultPresentationFactor = 1.30m;
            foreach (var kv in FamilyFactors)
                settings.PresentationFactorsByFamily[kv.Key] = kv.Value;
            foreach (var r in cubicacion)
                if (string.Equals(r.Tipo, "MAT", StringComparison.OrdinalIgnoreCase)
                    && FamilyFactors.TryGetValue(r.Categoria, out var f))
                    r.GgUtFactor = f;
        }

        return new Project(cubicacion, apus, itemizado, changeOrders, settings);
    }

    private static readonly string[] MoneyFields =
    {
        nameof(CostBreakdown.MaterialesUf), nameof(CostBreakdown.ManoObraUf), nameof(CostBreakdown.EquiposUf),
        nameof(CostBreakdown.SubcontratosUf), nameof(CostBreakdown.HerramientasUf), nameof(CostBreakdown.TransporteUf),
        nameof(CostBreakdown.MermasUf), nameof(CostBreakdown.OrdenesCambioUf), nameof(CostBreakdown.CostoDirectoUf),
        nameof(CostBreakdown.RiesgoUf), nameof(CostBreakdown.BaseCostoUf), nameof(CostBreakdown.GastosGeneralesUf),
        nameof(CostBreakdown.ValorAgregadoEquiposUf), nameof(CostBreakdown.BaseGgUtUf), nameof(CostBreakdown.UtilidadAditivaUf),
        nameof(CostBreakdown.CostoProyectoUf), nameof(CostBreakdown.SubtotalUf), nameof(CostBreakdown.UtilidadUf),
        nameof(CostBreakdown.DescuentoUf), nameof(CostBreakdown.TotalVentaNetoUf), nameof(CostBreakdown.IvaUf),
        nameof(CostBreakdown.TotalVentaConIvaUf), nameof(CostBreakdown.TotalVentaUf), nameof(CostBreakdown.UfM2),
        nameof(CostBreakdown.MargenRealPct),
    };

    private static void AssertBreakdownEqual(CostBreakdown expected, CostBreakdown actual, string because)
    {
        foreach (var name in MoneyFields)
        {
            var prop = typeof(CostBreakdown).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;
            var e = (decimal)prop.GetValue(expected)!;
            var a = (decimal)prop.GetValue(actual)!;
            Assert.True(e == a, $"{because}: {name} esperado {e} pero fue {a}");
        }
    }

    private static CostBreakdown Full(Project p)
        => new BudgetEngine().Calculate(p.Cubicacion, p.Settings, p.Apus, p.Itemizado, p.ChangeOrders);

    // Todas las combinaciones modo × (con/sin factores de presentación por familia).
    // Los factores por familia activan la ruta HasRowLevelMarkups (SumVentaClp), que es
    // la más frágil: es la que ejercita C3 y C4.
    public static IEnumerable<object[]> AllScenarios()
    {
        foreach (var mode in new[] { BudgetApuMode.CubicacionManda, BudgetApuMode.ApuManda, BudgetApuMode.CubicacionMasApuComplementarios })
            foreach (var family in new[] { false, true })
                yield return new object[] { mode, family };
    }

    // ----- 1. Determinismo ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Calculate_IsDeterministic_AcrossRepeatedRuns(BudgetApuMode mode, bool family)
    {
        var p = BuildRealisticProject(mode, family);
        var a = Full(p);
        var b = Full(p);
        var c = Full(BuildRealisticProject(mode, family)); // instancias nuevas de todo
        AssertBreakdownEqual(a, b, "corrida repetida");
        AssertBreakdownEqual(a, c, "fixture reconstruido");
    }

    // ----- 2. Independencia del orden de entrada -------------------------------------------

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Calculate_IsIndependentOfInputOrder(BudgetApuMode mode, bool family)
    {
        var p = BuildRealisticProject(mode, family);
        var baseline = Full(p);

        var shuffled = new Project(
            Shuffle(p.Cubicacion, 17),
            Shuffle(p.Apus, 29),
            Shuffle(p.Itemizado, 41),
            Shuffle(p.ChangeOrders, 7),
            p.Settings);
        var actual = Full(shuffled);

        AssertBreakdownEqual(baseline, actual, "orden de entrada distinto");
    }

    private static List<T> Shuffle<T>(IEnumerable<T> source, int seed)
    {
        var rnd = new Random(seed);
        return source.OrderBy(_ => rnd.Next()).ToList();
    }

    // ----- 3. Incremental == Completo -----------------------------------------------------

    public static IEnumerable<object[]> ModeItemFamily()
    {
        foreach (var mode in new[] { BudgetApuMode.CubicacionManda, BudgetApuMode.ApuManda, BudgetApuMode.CubicacionMasApuComplementarios })
            foreach (var family in new[] { false, true })
                foreach (var id in new[] { "P1", "P2", "P3", "P4", "P5" })
                    yield return new object[] { mode, family, id };
    }

    [Theory]
    [MemberData(nameof(ModeItemFamily))]
    public void Incremental_MatchesFull_WhenItemMarkedDirty(BudgetApuMode mode, bool family, string dirtyItemId)
    {
        var p = BuildRealisticProject(mode, family);
        var engine = new BudgetEngine();

        // Siembra: primera corrida completa (lastBudget).
        var seeded = engine.Calculate(p.Cubicacion, p.Settings, p.Apus, p.Itemizado, p.ChangeOrders);

        // "Edita" la partida: cambia una cantidad y la marca sucia.
        var touched = p.Cubicacion.FirstOrDefault(r => r.ItemId == dirtyItemId);
        if (touched != null) touched.Cantidad += 1m;
        else
        {
            var it = p.Itemizado.First(i => i.Id == dirtyItemId);
            it.Cantidad += 1m;
        }

        var incremental = engine.CalculateIncremental(
            p.Cubicacion, p.Settings, p.Apus, p.Itemizado, p.ChangeOrders, seeded,
            new[] { dirtyItemId });

        var full = new BudgetEngine().Calculate(p.Cubicacion, p.Settings, p.Apus, p.Itemizado, p.ChangeOrders);

        AssertBreakdownEqual(full, incremental, $"incremental[{mode}/family={family}/{dirtyItemId}] vs completo");
    }

    // ----- 4. Oferta al cliente: suma exacta y determinista ------------------------------

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void OfferCliente_SumsToVentaNeta_AndIsDeterministic(BudgetApuMode mode, bool family)
    {
        var p = BuildRealisticProject(mode, family);
        var cost = Full(p);
        decimal ventaNeta = cost.TotalVentaNetoUf;

        var rows1 = OfferClienteRowBuilder.Build(p.Itemizado, cost, ventaNeta);
        var rows2 = OfferClienteRowBuilder.Build(p.Itemizado, cost, ventaNeta);

        decimal sum1 = rows1.Where(r => !r.EsTitulo).Sum(r => r.TotalUf);
        decimal sum2 = rows2.Where(r => !r.EsTitulo).Sum(r => r.TotalUf);

        Assert.Equal(sum1, sum2);
        // El prorrateo debe reconstituir la venta neta (tolerancia 1 CLP por acumulación).
        Assert.True(Math.Abs(sum1 - ventaNeta) <= 1m, $"oferta suma {sum1} vs venta neta {ventaNeta}");
    }

    // ----- 5. Snapshot dorado del pipeline (BASELINE PRE-REFACTOR) -----------------------
    //
    // Estos números son el comportamiento ACTUAL del motor (antes de corregir C1-C5+A1),
    // redondeados al peso. NO son "la respuesta correcta": son el ancla para medir el
    // impacto de cada corrección.
    //
    // CHANGELOG de baseline (actualizar con cada corrección deliberada):
    //   2026-08-27  captura inicial pre-refactor (C1-C5+A1 sin corregir).
    //   2026-08-27  C3+C4: venta de línea en CLP directo; factores por familia = opt-in.
    //               Path aditivo (estos snapshots) SIN cambio; solo se movió Calculate_WithFamilyMarkups.
    //   2026-08-27  C1: APU complementario ya no infla herramientas/transporte/merma sobre bases
    //               que incluyen material/MO no cobrados. Complementarios: Herr/Transp/Mermas ↓.
    //   2026-08-27  C2: merma de nivel APU no se compone con la merma propia del recurso.
    //               Fixture: se quitó la merma de recurso de CU-DESNUDO (proyecto bien formado
    //               = una sola capa de merma). Materiales ↓ 7.680 en los 3 modos.
    //   2026-09-01  Motor v5: política de cierre efectiva (BudgetTotalsCalculator.FinalizeVentaYCierre).
    //               Con la política por defecto CostPlusMarkup el paso es pass-through
    //               (neto0 == venta base) ⇒ los montos NO cambian.
    //   2026-09-01  H-6: MargenRealPct pasa de base venta-con-IVA a base NETO (post-descuento,
    //               pre-IVA). Solo se movió ese diagnóstico: 38% → 26% en los 3 modos. Ningún
    //               monto ($) cambió.
    //   2026-08-30  C-1 (motor v4): se unifican las rutas de markup — venta = Σ costo × factor
    //               de presentación por familia para TODA línea (APU/OC/cargos globales incluidos).
    //               Los snapshots de ESTE test (familyMarkups: false) NO cambian: sin el flag el
    //               factor es 1 y la ruta aditiva es idéntica. Solo cambian los escenarios
    //               familyMarkups: true (sin golden fijo; cubiertos por FamilyMarkupPath_*).
    //
    // Al corregir un hallazgo: cambiar solo los valores afectados, dejar el resto, y
    // agregar una línea al changelog explicando qué se movió y por qué. Un delta
    // inesperado en un campo que "no debería" moverse = detener y revisar.

    [Theory]
    [InlineData(BudgetApuMode.CubicacionManda)]
    [InlineData(BudgetApuMode.ApuManda)]
    [InlineData(BudgetApuMode.CubicacionMasApuComplementarios)]
    public void GoldenSnapshot_FullPipeline_PreRefactor(BudgetApuMode mode)
    {
        var expected = Baseline[$"{mode}|plain"];
        var p = BuildRealisticProject(mode, familyMarkups: false);
        var cost = Full(p);

        foreach (var (name, value) in expected)
        {
            var prop = typeof(CostBreakdown).GetProperty(name)!;
            var actual = (decimal)prop.GetValue(cost)!;
            Assert.Equal(value, actual, 0); // al peso
        }
        Assert.Empty(cost.Inconsistencias);
    }

    // --- Ruta "factores de presentación por familia" (HasRowLevelMarkups) ---------------
    //
    // Antes de C3/C4: con CUALQUIER APU en el proyecto y un GgUtFactor ≠ 1 suelto, AddApuRow
    // hacía VentaClp = UfToClp(apu.PrecioUnitUf, ValorUfClp) tratando CLP como UF -> la venta
    // explotaba ~2400× (margen real 100%). Corregido:
    //   C3: la ruta por factores solo se activa con MarkupPolicy.FactoresPresentacionPorFamilia.
    //   C4: la venta de línea se calcula directo en CLP; el APU aporta su costo unitario en CLP.

    [Theory]
    [InlineData(BudgetApuMode.CubicacionManda)]
    [InlineData(BudgetApuMode.ApuManda)]
    [InlineData(BudgetApuMode.CubicacionMasApuComplementarios)]
    public void FamilyMarkupPath_ProducesSaneMargin(BudgetApuMode mode)
    {
        var p = BuildRealisticProject(mode, familyMarkups: true);
        var cost = Full(p);
        // Con factores 1.17–1.57 sobre materiales, GG 13% y OC, el margen real queda en un
        // rango de negocio (no 100%, no negativo).
        Assert.InRange(cost.MargenRealPct, 10m, 70m);
        // La venta es del orden del pipeline aditivo, no 2400×.
        var plain = Full(BuildRealisticProject(mode, familyMarkups: false));
        Assert.True(cost.TotalVentaUf < plain.TotalVentaUf * 3m,
            $"[{mode}] venta con factores {cost.TotalVentaUf} vs sin factores {plain.TotalVentaUf}");
    }

    // C-1 CERRADO (motor v4): APU/OC/cargos globales llevan el factor de presentación de su
    // familia/área (o el factor por defecto del proyecto). Un proyecto APU-dominante bien
    // formado (con factores ≥ 1.3) mantiene utilidad positiva.
    [Theory]
    [InlineData(BudgetApuMode.ApuManda)]
    [InlineData(BudgetApuMode.CubicacionMasApuComplementarios)]
    public void FamilyMarkupPath_ApuHeavy_KeepsPositiveMargin(BudgetApuMode mode)
    {
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1", Descripcion = "MAT", Unidad = "ML", Cantidad = 100m, CantidadOriginal = 100m },
            new() { Id = "P2", Item = "2", Descripcion = "APU grande", Unidad = "GL", Cantidad = 1m, CantidadOriginal = 1m },
        };
        var cub = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Tipo = "MAT", Codigo = "C", Categoria = "Conductores", Cantidad = 110m, MatClp = 900m, MoClp = 300m, GgUtFactor = 1.50m },
        };
        var apus = new List<ApuRow>
        {
            new()
            {
                ItemId = "P2", Descripcion = "Suministro y montaje mayor", HerramientasPct = 3m, ImprevistosAlzaPct = 2m,
                Recursos =
                {
                    new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "M", Rendimiento = 1m, PrecioUf = 8_000_000m },
                    new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO", Rendimiento = 400m, PrecioUf = 9000m },
                },
            },
        };
        var settings = new BudgetSettings
        {
            ApuMode = mode,
            Currency = new CurrencyPolicy { ValorUfClp = 39000m },
            DefaultPresentationFactor = 1.60m,
            PresentationFactorsByFamily = { ["CONDUCTORES"] = 1.60m },
            Markup = new MarkupPolicy
            {
                FactoresPresentacionPorFamilia = true,
                GastosGeneralesPct = 13m,
                UtilidadPct = 16m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true,
            },
        };
        var cost = new BudgetEngine().Calculate(cub, settings, apus, itemizado, null);
        Assert.DoesNotContain(cost.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
        Assert.True(cost.UtilidadUf > 0m,
            $"[{mode}] utilidad {cost.UtilidadUf} <= 0: la venta por factores no cubre GG+utilidad " +
            $"cuando el APU domina (venta {cost.TotalVentaNetoUf}, subtotal {cost.SubtotalUf}). " +
            "Es el hallazgo C-1: revisar el markup de la porción no-row-level.");
    }

    // C-1: el proyecto declara factores por familia pero NO configuró ninguno (dict vacío,
    // factor por defecto 1). La venta por factores no cubre el piso GG+utilidad ⇒ inconsistencia
    // BLOQUEANTE UtilidadNegativa (Revisión Final impide exportar).
    [Theory]
    [InlineData(BudgetApuMode.ApuManda)]
    [InlineData(BudgetApuMode.CubicacionMasApuComplementarios)]
    public void FamilyMarkupPath_WithoutFactors_EmitsBlockingInconsistency(BudgetApuMode mode)
    {
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1", Descripcion = "MAT", Unidad = "ML", Cantidad = 100m, CantidadOriginal = 100m },
            new() { Id = "P2", Item = "2", Descripcion = "APU grande", Unidad = "GL", Cantidad = 1m, CantidadOriginal = 1m },
        };
        var cub = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Tipo = "MAT", Codigo = "C", Categoria = "Conductores", Cantidad = 110m, MatClp = 900m, MoClp = 300m },
        };
        var apus = new List<ApuRow>
        {
            new()
            {
                ItemId = "P2", Descripcion = "Suministro y montaje mayor", HerramientasPct = 3m, ImprevistosAlzaPct = 2m,
                Recursos =
                {
                    new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "M", Rendimiento = 1m, PrecioUf = 8_000_000m },
                    new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO", Rendimiento = 400m, PrecioUf = 9000m },
                },
            },
        };
        var settings = new BudgetSettings
        {
            ApuMode = mode,
            Currency = new CurrencyPolicy { ValorUfClp = 39000m },
            // FactoresPresentacionPorFamilia = true pero dict vacío y factor por defecto = 1.
            DefaultPresentationFactor = 1m,
            Markup = new MarkupPolicy
            {
                FactoresPresentacionPorFamilia = true,
                GastosGeneralesPct = 13m,
                UtilidadPct = 16m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true,
            },
        };
        var cost = new BudgetEngine().Calculate(cub, settings, apus, itemizado, null);
        Assert.Contains(cost.Inconsistencias, i => i.Tipo == "UtilidadNegativa");

        var review = new FinalReviewService().Build(itemizado, System.Array.Empty<MaterialRow>(), cub, apus, settings: settings);
        Assert.Contains(review.Issues, x =>
            x.Category == FinalReviewCategory.EngineInconsistency
            && x.Severity == FinalReviewSeverity.Blocker
            && x.Problem.Contains("no cubre"));
        Assert.False(review.ReadyToExport);
    }

    [Fact]
    public void OfferCliente_PerPartidaAmounts_MatchGoldenDistribution()
    {
        // M-1: fija la DISTRIBUCIÓN por partida (no solo la suma) para el modo aditivo.
        var p = BuildRealisticProject(BudgetApuMode.CubicacionMasApuComplementarios, familyMarkups: false);
        var cost = Full(p);
        var offer = OfferClienteRowBuilder.Build(p.Itemizado, cost, cost.TotalVentaNetoUf);
        var montos = offer.Where(r => !r.EsTitulo).ToDictionary(r => r.Item, r => r.TotalUf);

        Assert.Equal(859323m, montos["1"]);    // Canalización
        Assert.Equal(1041747m, montos["2"]);   // Conductores
        Assert.Equal(3742934m, montos["3"]);   // Tableros (cub + APU complementario)
        Assert.Equal(1454496m, montos["4"]);   // Malla de tierra (solo APU, fullApu)
        Assert.Equal(3317090m, montos["5"]);   // Luminarias (con descuento negociado)
        Assert.Equal(cost.TotalVentaNetoUf, montos.Values.Sum(), 0); // suma exacta al peso
    }

    [Fact]
    public void C2_MermaDeApu_NoSeAplicaSobreRecursosConMermaPropia()
    {
        // Un material con merma propia (3%) + APU con merma (10%): la merma NO se compone.
        var apu = new ApuRow
        {
            ItemId = "P1",
            Descripcion = "APU tierra",
            MermaPct = 10m,
            HerramientasPct = 0m,
            ImprevistosAlzaPct = 0m,
            TransportePct = 0m,
            Recursos =
            {
                new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "A", Descripcion = "con merma propia", Rendimiento = 10m, PrecioUf = 100m, MermaPct = 3m },
                new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "B", Descripcion = "sin merma", Rendimiento = 5m, PrecioUf = 200m },
            },
        };
        // Merma APU solo sobre B: 5*200 = 1000 * 10% = 100. (A: 1000 base + 30 merma propia, intacto.)
        Assert.Equal(100m, apu.MermaUf);
        Assert.True(apu.TieneMermaEnDosNiveles);

        var items = new[] { new ItemizadoRow { Id = "P1", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;
        var cost = new BudgetEngine().Calculate(System.Array.Empty<CubicacionRow>(), settings, new[] { apu }, items);
        Assert.Contains(cost.Inconsistencias, i => i.Tipo == "MermaDobleNivel");
    }

    [Fact]
    public void GgUtFactorLoose_WithoutExplicitFlag_DoesNotChangeMarkupMethod()
    {
        // C3: un GgUtFactor ≠ 1 sin FactoresPresentacionPorFamilia no debe alterar el total.
        var baseline = Full(BuildRealisticProject(BudgetApuMode.CubicacionMasApuComplementarios));
        var p = BuildRealisticProject(BudgetApuMode.CubicacionMasApuComplementarios);
        foreach (var r in p.Cubicacion) r.GgUtFactor = 1.42m; // factor suelto, flag apagado
        var actual = Full(p);
        AssertBreakdownEqual(baseline, actual, "GgUtFactor suelto sin flag");
    }

    [Fact(Skip = "captura manual: quitar Skip, correr, pegar en Baseline, restaurar Skip")]
    public void DumpBaseline()
    {
        var sb = new System.Text.StringBuilder("\n");
        foreach (var m in new[] { BudgetApuMode.CubicacionManda, BudgetApuMode.ApuManda, BudgetApuMode.CubicacionMasApuComplementarios })
        {
            var cost = Full(BuildRealisticProject(m, familyMarkups: false));
            sb.Append($"[\"{m}|plain\"] = new() {{ ");
            foreach (var n in MoneyFields)
                sb.Append($"[\"{n}\"] = {Math.Round((decimal)typeof(CostBreakdown).GetProperty(n)!.GetValue(cost)!, 0, MidpointRounding.AwayFromZero)}m, ");
            sb.Append("},\n");
        }
        Assert.Fail(sb.ToString());
    }

    private static readonly IReadOnlyDictionary<string, Dictionary<string, decimal>> Baseline =
        new Dictionary<string, Dictionary<string, decimal>>
        {
            ["CubicacionManda|plain"] = new()
            {
                ["MaterialesUf"] = 3169040m,
                ["ManoObraUf"] = 906000m,
                ["EquiposUf"] = 0m,
                ["SubcontratosUf"] = 0m,
                ["HerramientasUf"] = 28554m,
                ["TransporteUf"] = 7520m,
                ["MermasUf"] = 136524m,
                ["OrdenesCambioUf"] = 3250000m,
                ["CostoDirectoUf"] = 7497638m,
                ["RiesgoUf"] = 149953m,
                ["BaseCostoUf"] = 7647591m,
                ["GastosGeneralesUf"] = 994187m,
                ["ValorAgregadoEquiposUf"] = 0m,
                ["BaseGgUtUf"] = 7647591m,
                ["UtilidadAditivaUf"] = 0m,
                ["CostoProyectoUf"] = 10287831m,
                ["SubtotalUf"] = 8641778m,
                ["UtilidadUf"] = 1646053m,
                ["DescuentoUf"] = 154317m,
                ["TotalVentaNetoUf"] = 10133513m,
                ["IvaUf"] = 1925367m,
                ["TotalVentaConIvaUf"] = 12058881m,
                ["TotalVentaUf"] = 12058881m,
                ["UfM2"] = 5025m,
                ["MargenRealPct"] = 26m,
            },
            ["ApuManda|plain"] = new()
            {
                ["MaterialesUf"] = 2029040m,
                ["ManoObraUf"] = 894000m,
                ["EquiposUf"] = 24000m,
                ["SubcontratosUf"] = 180000m,
                ["HerramientasUf"] = 35094m,
                ["TransporteUf"] = 7760m,
                ["MermasUf"] = 111106m,
                ["OrdenesCambioUf"] = 3250000m,
                ["CostoDirectoUf"] = 6531000m,
                ["RiesgoUf"] = 130620m,
                ["BaseCostoUf"] = 6661620m,
                ["GastosGeneralesUf"] = 866011m,
                ["ValorAgregadoEquiposUf"] = 720m,
                ["BaseGgUtUf"] = 6661620m,
                ["UtilidadAditivaUf"] = 0m,
                ["CostoProyectoUf"] = 8962322m,
                ["SubtotalUf"] = 7528350m,
                ["UtilidadUf"] = 1433971m,
                ["DescuentoUf"] = 134435m,
                ["TotalVentaNetoUf"] = 8827887m,
                ["IvaUf"] = 1677299m,
                ["TotalVentaConIvaUf"] = 10505185m,
                ["TotalVentaUf"] = 10505185m,
                ["UfM2"] = 4377m,
                ["MargenRealPct"] = 26m,
            },
            // C1 (2026-08-27): en complementario, el APU de P3 ya no suma herramientas/transporte/
            // merma calculadas sobre el material+MO del tablero (que aporta la cubicación); solo
            // suma sus recursos reales (EQ 24.000, SC 180.000) + imprevistos de alza sobre esa base.
            // Herr 35394→28554, Transp 7914→7674, Mermas 146084→141383, Costo directo -11.782.
            ["CubicacionMasApuComplementarios|plain"] = new()
            {
                ["MaterialesUf"] = 3169040m,
                ["ManoObraUf"] = 906000m,
                ["EquiposUf"] = 24000m,
                ["SubcontratosUf"] = 180000m,
                ["HerramientasUf"] = 28554m,
                ["TransporteUf"] = 7520m,
                ["MermasUf"] = 140604m,
                ["OrdenesCambioUf"] = 3250000m,
                ["CostoDirectoUf"] = 7705718m,
                ["RiesgoUf"] = 154114m,
                ["BaseCostoUf"] = 7859832m,
                ["GastosGeneralesUf"] = 1021778m,
                ["ValorAgregadoEquiposUf"] = 720m,
                ["BaseGgUtUf"] = 7859832m,
                ["UtilidadAditivaUf"] = 0m,
                ["CostoProyectoUf"] = 10574203m,
                ["SubtotalUf"] = 8882331m,
                ["UtilidadUf"] = 1691873m,
                ["DescuentoUf"] = 158613m,
                ["TotalVentaNetoUf"] = 10415590m,
                ["IvaUf"] = 1978962m,
                ["TotalVentaConIvaUf"] = 12394552m,
                ["TotalVentaUf"] = 12394552m,
                ["UfM2"] = 5164m,
                ["MargenRealPct"] = 26m,
            },
        };
}
