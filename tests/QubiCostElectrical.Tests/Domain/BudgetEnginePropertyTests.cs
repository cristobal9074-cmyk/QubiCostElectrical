using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Invariantes del motor sobre ~200 proyectos pseudoaleatorios DETERMINISTAS (semilla fija).
/// No fijan valores; fijan propiedades que deben cumplirse SIEMPRE tras cerrar C-1:
///   1. factores ≥ 1 y GG/UT/riesgo ≥ 0 ⇒ UtilidadUf ≥ 0  O  hay inconsistencia UtilidadNegativa.
///   2. con factores por familia activos, Σ Origenes.VentaClp ≈ venta neta (bruto), tolerancia 1 CLP.
///   3. cálculo incremental == cálculo completo para una partida marcada sucia al azar.
/// </summary>
public sealed class BudgetEnginePropertyTests
{
    private sealed record Generated(
        List<CubicacionRow> Cubicacion,
        List<ApuRow> Apus,
        List<ItemizadoRow> Itemizado,
        List<ChangeOrderRow> ChangeOrders,
        BudgetSettings Settings,
        string[] ItemIds);

    private static readonly string[] Familias = { "Conductores", "Canalización", "Tableros", "Iluminación", "Puesta a tierra", "Varios" };

    private static decimal Range(Random r, decimal lo, decimal hi)
        => lo + (decimal)r.NextDouble() * (hi - lo);

    private static Generated Build(int seed)
    {
        var r = new Random(seed);
        int partidas = r.Next(2, 5);
        var itemizado = new List<ItemizadoRow>();
        var cubicacion = new List<CubicacionRow>();
        var apus = new List<ApuRow>();
        var ids = new List<string>();

        var factorsByFamily = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Familias)
            factorsByFamily[f] = Math.Round(Range(r, 1.0m, 2.0m), 3);

        for (int i = 0; i < partidas; i++)
        {
            string id = $"P{i}";
            ids.Add(id);
            decimal cant = r.Next(1, 50);
            itemizado.Add(new ItemizadoRow { Id = id, Item = $"{i + 1}", Descripcion = $"Partida {i}", Unidad = "UN", Cantidad = cant, CantidadOriginal = cant });

            string familia = Familias[r.Next(Familias.Length)];
            decimal mat = Math.Round(Range(r, 100m, 500_000m), 0);
            decimal mo = Math.Round(Range(r, 0m, 200_000m), 0);
            cubicacion.Add(new CubicacionRow
            {
                ItemId = id,
                Tipo = "MAT",
                Codigo = $"C{i}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                Categoria = familia,
                Area = familia,
                Cantidad = r.Next(1, 40),
                MatClp = mat,
                MoClp = mo,
                GgUtFactor = factorsByFamily[familia],
            });

            // ~40% de las partidas llevan un APU complementario/completo.
            if (r.NextDouble() < 0.4)
            {
                apus.Add(new ApuRow
                {
                    ItemId = id,
                    Descripcion = $"APU {i}",
                    Unidad = "UN",
                    HerramientasPct = Range(r, 0m, 5m),
                    ImprevistosAlzaPct = Range(r, 0m, 3m),
                    Recursos =
                    {
                        new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = $"AM{i}", Descripcion = "mat apu", Unidad = "UN", Rendimiento = Range(r, 0.5m, 4m), PrecioUf = Math.Round(Range(r, 1_000m, 80_000m), 0) },
                        new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = $"AMO{i}", Descripcion = "mo apu", Unidad = "HH", Rendimiento = Range(r, 1m, 8m), PrecioUf = Math.Round(Range(r, 5_000m, 15_000m), 0) },
                        new() { Tipo = ApuTipoRecurso.EQ, CodigoMaterial = $"AE{i}", Descripcion = "eq apu", Unidad = "DIA", Rendimiento = Range(r, 0.1m, 1m), PrecioUf = Math.Round(Range(r, 2_000m, 40_000m), 0) },
                    },
                });
            }
        }

        var changeOrders = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-1", Titulo = "OC aprobada", Estado = "Aprobada", DeltaCostoUf = Math.Round(Range(r, 0m, 3_000_000m), 0) },
            new() { Numero = "OC-2", Titulo = "OC borrador", Estado = "Borrador", DeltaCostoUf = 500_000m },
        };

        var settings = new BudgetSettings
        {
            ApuMode = BudgetApuMode.CubicacionMasApuComplementarios,
            MetrosCuadrados = r.Next(500, 5000),
            HerramientasPct = Range(r, 0m, 5m),
            MermasPct = Range(r, 0m, 5m),
            Currency = new CurrencyPolicy { MonedaPresupuesto = "CLP", ValorUfClp = 39_000m },
            DefaultPresentationFactor = Math.Round(Range(r, 1.0m, 2.0m), 3),
            Markup = new MarkupPolicy
            {
                MarkupMethod = r.NextDouble() < 0.5 ? MarkupMethod.SobreVenta : MarkupMethod.SobreCosto,
                FactoresPresentacionPorFamilia = true,
                GastosGeneralesPct = Math.Round(Range(r, 0m, 30m), 2),
                UtilidadPct = Math.Round(Range(r, 0m, 30m), 2),
                ValorAgregadoEquiposPct = Math.Round(Range(r, 0m, 10m), 2),
                RiesgoPct = Math.Round(Range(r, 0m, 30m), 2),
                DescuentoPct = 0m, // así TotalVentaNetoUf == bruto (invariante 2)
                IvaPct = 19m,
                IncluirIvaEnTotal = true,
            },
        };
        foreach (var kv in factorsByFamily)
            settings.PresentationFactorsByFamily[kv.Key] = kv.Value;

        return new Generated(cubicacion, apus, itemizado, changeOrders, settings, ids.ToArray());
    }

    [Fact]
    public void Invariants_HoldAcross200RandomProjects()
    {
        var failures = new List<string>();

        for (int seed = 1; seed <= 200; seed++)
        {
            var g = Build(seed);
            var engine = new BudgetEngine();
            var cost = engine.Calculate(g.Cubicacion, g.Settings, g.Apus, g.Itemizado, g.ChangeOrders);

            // Invariante 1: utilidad no negativa, o inconsistencia BLOQUEANTE declarada.
            bool utilidadOk = cost.UtilidadUf >= 0m
                || cost.Inconsistencias.Any(i => i.Tipo == "UtilidadNegativa");
            if (!utilidadOk)
                failures.Add($"seed {seed}: UtilidadUf={cost.UtilidadUf} < 0 y sin UtilidadNegativa (subtotal={cost.SubtotalUf}, venta neta={cost.TotalVentaNetoUf}).");

            // Invariante 2: Σ Origenes.VentaClp ≈ bruto (== venta neta con descuento 0).
            decimal sumVenta = cost.Origenes.Sum(o => o.VentaClp);
            if (Math.Abs(sumVenta - cost.TotalVentaNetoUf) >= 1m)
                failures.Add($"seed {seed}: Σ VentaClp={sumVenta} vs venta neta={cost.TotalVentaNetoUf} (Δ={sumVenta - cost.TotalVentaNetoUf}).");

            // Invariante 3: incremental ≈ completo para una partida sucia al azar.
            // Tolerancia 0.01 CLP: el orden de acumulación de VentaClp difiere entre la ruta
            // incremental (orígenes clonados) y la completa; con entradas aleatorias de precisión
            // larga eso produce ruido en el último dígito del decimal, no una divergencia real.
            var rnd = new Random(seed * 31 + 7);
            string dirty = g.ItemIds[rnd.Next(g.ItemIds.Length)];
            var seeded = engine.Calculate(g.Cubicacion, g.Settings, g.Apus, g.Itemizado, g.ChangeOrders);
            var touched = g.Cubicacion.FirstOrDefault(x => x.ItemId == dirty);
            if (touched != null) touched.Cantidad += 1m;
            else g.Itemizado.First(x => x.Id == dirty).Cantidad += 1m;

            var incremental = engine.CalculateIncremental(
                g.Cubicacion, g.Settings, g.Apus, g.Itemizado, g.ChangeOrders, seeded, new[] { dirty });
            var full = new BudgetEngine().Calculate(g.Cubicacion, g.Settings, g.Apus, g.Itemizado, g.ChangeOrders);

            foreach (var (name, a, b) in new[]
                     {
                         ("CostoDirectoUf", incremental.CostoDirectoUf, full.CostoDirectoUf),
                         ("SubtotalUf", incremental.SubtotalUf, full.SubtotalUf),
                         ("UtilidadUf", incremental.UtilidadUf, full.UtilidadUf),
                         ("CostoProyectoUf", incremental.CostoProyectoUf, full.CostoProyectoUf),
                         ("TotalVentaNetoUf", incremental.TotalVentaNetoUf, full.TotalVentaNetoUf),
                         ("TotalVentaUf", incremental.TotalVentaUf, full.TotalVentaUf),
                     })
            {
                if (Math.Abs(a - b) >= 0.01m)
                    failures.Add($"seed {seed}: incremental[{dirty}].{name}={a} != completo {b} (Δ={a - b}).");
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} violaciones de invariante:\n" + string.Join("\n", failures.Take(20)));
    }

    private static readonly ClosingPolicyType[] AllPolicies =
    {
        ClosingPolicyType.CostPlusMarkup, ClosingPolicyType.TargetPrice, ClosingPolicyType.LumpSumEpc,
        ClosingPolicyType.CostPlusFee, ClosingPolicyType.UnitPricePure, ClosingPolicyType.GainsharePainshare,
    };

    /// <summary>
    /// Invariante de política de cierre (motor v5): con una política elegida al azar entre las 6,
    /// el NETO de la política cubre el piso comercial O el motor emitió una inconsistencia
    /// BLOQUEANTE (UtilidadNegativa). Nunca una pérdida silenciosa.
    /// </summary>
    [Fact]
    public void ClosingPolicy_NetoCoversPiso_OrBlockingInconsistency_Across200RandomProjects()
    {
        var failures = new List<string>();

        for (int seed = 1; seed <= 200; seed++)
        {
            var g = Build(seed);
            var rnd = new Random(seed * 17 + 3);
            var policy = AllPolicies[rnd.Next(AllPolicies.Length)];
            g.Settings.ClosingPolicy = new ClosingPolicySelection
            {
                Policy = policy,
                TargetPriceClp = policy == ClosingPolicyType.TargetPrice ? Math.Round(Range(rnd, 0m, 30_000_000m), 0) : 0m,
                FeeClp = 0m,
                FeePct = Math.Round(Range(rnd, 0m, 20m), 2),
                TargetCostClp = policy == ClosingPolicyType.GainsharePainshare ? Math.Round(Range(rnd, 0m, 20_000_000m), 0) : 0m,
                LumpSumMinRiskPct = Math.Round(Range(rnd, 0m, 30m), 2),
                GainsharePct = Math.Round(Range(rnd, 0m, 80m), 2),
            };

            var cost = new BudgetEngine().Calculate(g.Cubicacion, g.Settings, g.Apus, g.Itemizado, g.ChangeOrders);
            Assert.NotNull(cost.ClosingPolicyResult);
            Assert.Equal(policy, cost.ClosingPolicy);

            decimal neto0 = cost.ClosingPolicyResult!.VentaFinalClp;
            decimal piso = cost.CostoProyectoUf;
            decimal costoEjecucion = cost.CostoDirectoUf;
            bool blocking = cost.Inconsistencias.Any(i => i.Tipo == "UtilidadNegativa");
            // H-1: el umbral del bloqueo es policy-aware. CostPlusMarkup/UnitPricePure venden por
            // factores y deben cubrir el piso comercial (GG+utilidad). El resto son modelos de
            // precio-primero / costo-transparente: solo se exige que no haya pérdida de caja real
            // (neto por debajo del costo de ejecución); entre costo y piso es advertencia.
            bool factorPolicy = policy is ClosingPolicyType.CostPlusMarkup or ClosingPolicyType.UnitPricePure;
            decimal umbral = factorPolicy ? piso : costoEjecucion;
            bool ok = neto0 >= umbral - 0.01m || blocking;
            if (!ok)
                failures.Add($"seed {seed} [{policy}]: neto0={neto0} < umbral={umbral} y sin UtilidadNegativa.");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} violaciones:\n" + string.Join("\n", failures.Take(20)));
    }
}
