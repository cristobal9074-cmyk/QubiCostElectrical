using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class BudgetEngineTests
{
    [Fact]
    public void Calculate_AppliesMarkupAndUfM2Deterministically()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 10m, MatClp = 1m, MoClp = 0.5m, MatUf = 1m, MoUf = 0.5m }
        };
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 10m, utilidadPct: 20m, metrosCuadrados: 30m);
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;

        var result = engine.Calculate(rows, settings);

        // subtotal = 16.5; sobre-venta 20%: bruto = 16.5/0.8 = 20.625
        Assert.Equal(15m, result.CostoDirectoUf);
        Assert.Equal(1.5m, result.GastosGeneralesUf);
        Assert.Equal(16.5m, result.SubtotalUf);
        Assert.Equal(4.125m, result.UtilidadUf);
        Assert.Equal(20.625m, result.TotalVentaUf);
        Assert.Equal(0.6875m, result.UfM2);
    }

    [Fact]
    public void CalculateIncremental_ReplacesOnlyDirtyItem_WhenNoCrossDependenciesExist()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var initialRows = new List<CubicacionRow>
        {
            new() { ItemId = "A", Codigo = "A", Descripcion = "A", Unidad = "UN", Cantidad = 1m, MatClp = 10m, MatUf = 10m },
            new() { ItemId = "B", Codigo = "B", Descripcion = "B", Unidad = "UN", Cantidad = 1m, MatClp = 20m, MatUf = 20m }
        };
        var initial = engine.Calculate(initialRows, settings);

        initialRows[0].Cantidad = 3m;
        engine.MarkDirtyItem("A");
        var incremental = engine.CalculateIncremental(initialRows, settings, null, null, null, initial);
        var full = engine.Calculate(initialRows, settings);

        Assert.Equal(full.CostoDirectoUf, incremental.CostoDirectoUf);
        Assert.Equal(full.TotalVentaUf, incremental.TotalVentaUf);
        Assert.Equal(full.MaterialesCriticos.Count, incremental.MaterialesCriticos.Count);
        Assert.Equal(full.MaterialesCriticos[0].Unidad, incremental.MaterialesCriticos[0].Unidad);
        Assert.Equal(full.MaterialesCriticos[0].Cantidad, incremental.MaterialesCriticos[0].Cantidad);
        Assert.Empty(engine.DirtyItems);
    }

    [Fact]
    public void CalculateIncremental_PreservesMaterialQuantitiesUnitsAndNegativeAdjustments()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "A", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 10m, MatClp = 1m, MatUf = 1m },
            new() { ItemId = "A", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = -2m, MatClp = 1m, MatUf = 1m },
            new() { ItemId = "B", Codigo = "CAB25", Descripcion = "Cable 2.5", Unidad = "ML", Cantidad = 5m, MatClp = 2m, MatUf = 2m }
        };
        var previous = engine.Calculate(rows, settings);

        rows[2].Cantidad = 8m;
        engine.MarkDirtyItem("B");
        var incremental = engine.CalculateIncremental(rows, settings, null, null, null, previous);
        var full = engine.Calculate(rows, settings);

        Assert.Equal(full.CostoDirectoUf, incremental.CostoDirectoUf);
        Assert.Equal(full.TotalVentaUf, incremental.TotalVentaUf);
        Assert.Equal(full.MaterialesCriticos.Count, incremental.MaterialesCriticos.Count);

        var pvc = Assert.Single(incremental.MaterialesCriticos, x => x.Codigo == "PVC20");
        Assert.Equal("ML", pvc.Unidad);
        Assert.Equal(8m, pvc.Cantidad);
        Assert.Equal(8m, pvc.TotalUf);

        var cable = Assert.Single(incremental.MaterialesCriticos, x => x.Codigo == "CAB25");
        Assert.Equal("ML", cable.Unidad);
        Assert.Equal(8m, cable.Cantidad);
        Assert.Equal(16m, cable.TotalUf);
    }

    [Fact]
    public void CalculateIncremental_RecomputesDirtyItemWithApusAndComplementaries()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionMasApuComplementarios;

        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "MAT1", Descripcion = "Material 1", Unidad = "UN", Cantidad = 1m, MatClp = 4m, MoClp = 1m, MatUf = 4m, MoUf = 1m },
            new() { ItemId = "P2", Codigo = "MAT2", Descripcion = "Material 2", Unidad = "UN", Cantidad = 1m, MatClp = 7m, MoClp = 0m, MatUf = 7m, MoUf = 0m }
        };
        var apus = new[]
        {
            new ApuRow
            {
                ItemId = "P1",
                Descripcion = "APU P1",
                Recursos = new List<ApuRecursoRow>
                {
                    new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "MAT-IGN", Descripcion = "Material ignorado", PrecioUf = 10m, Rendimiento = 1m },
                    new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO-IGN", Descripcion = "MO ignorada", ValorHHUf = 8m, Rendimiento = 1m },
                    new() { Tipo = ApuTipoRecurso.EQ, CodigoMaterial = "EQ-1", Descripcion = "Equipo", PrecioUf = 3m, Rendimiento = 1m },
                    new() { Tipo = ApuTipoRecurso.SC, CodigoMaterial = "SC-1", Descripcion = "Subcontrato", PrecioUf = 4m, Rendimiento = 1m },
                    new() { Tipo = ApuTipoRecurso.HERR, CodigoMaterial = "HERR-1", Descripcion = "Herramienta", PrecioUf = 0.5m, Rendimiento = 1m },
                    new() { Tipo = ApuTipoRecurso.TR, CodigoMaterial = "TR-1", Descripcion = "Transporte", PrecioUf = 0.25m, Rendimiento = 1m }
                }
            }
        };
        var items = new[]
        {
            new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Cantidad = 1m },
            new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Partida 2", Cantidad = 1m }
        };

        var previous = engine.Calculate(rows, settings, apus, items, null);
        rows[0].Cantidad = 2m;
        engine.MarkDirtyItem("P1");

        var incremental = engine.CalculateIncremental(rows, settings, apus, items, null, previous);
        var full = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(full.CostoDirectoUf, incremental.CostoDirectoUf);
        Assert.Equal(full.TotalVentaUf, incremental.TotalVentaUf);
        Assert.Contains(incremental.Origenes, x => x.Origen == BudgetCostOrigin.Apu && x.ItemId == "P1");
        Assert.Empty(engine.DirtyItems);
    }

    /// <summary>
    /// Guarda contra la regresión del hallazgo de la revisión CEO: el cálculo incremental
    /// detectaba los orígenes sintéticos "Herramientas globales %"/"Mermas globales %" buscando
    /// ese texto en Descripcion. Ahora usa CostOriginBreakdown.IsGlobalAutoCharge, que no
    /// depende del idioma ni del formato del porcentaje mostrado.
    /// </summary>
    [Fact]
    public void CalculateIncremental_RecomputesGlobalAutoCharges_WithoutDuplicating()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.HerramientasPct = 10m;
        settings.MermasPct = 5m;

        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "A", Codigo = "A", Descripcion = "A", Unidad = "UN", Cantidad = 1m, MatClp = 10m, MoClp = 4m, MatUf = 10m, MoUf = 4m },
            new() { ItemId = "B", Codigo = "B", Descripcion = "B", Unidad = "UN", Cantidad = 1m, MatClp = 20m, MoClp = 6m, MatUf = 20m, MoUf = 6m }
        };
        var previous = engine.Calculate(rows, settings);

        Assert.Single(previous.Origenes, x => x.IsGlobalAutoCharge && x.Descripcion.StartsWith("Herramientas globales"));
        Assert.Single(previous.Origenes, x => x.IsGlobalAutoCharge && x.Descripcion.StartsWith("Mermas globales"));

        rows[0].Cantidad = 3m;
        engine.MarkDirtyItem("A");
        var incremental = engine.CalculateIncremental(rows, settings, null, null, null, previous);
        var full = engine.Calculate(rows, settings);

        // Sin duplicados: exactamente un origen de cada recargo global tras el recálculo incremental.
        Assert.Single(incremental.Origenes, x => x.IsGlobalAutoCharge && x.Descripcion.StartsWith("Herramientas globales"));
        Assert.Single(incremental.Origenes, x => x.IsGlobalAutoCharge && x.Descripcion.StartsWith("Mermas globales"));
        Assert.Equal(full.HerramientasUf, incremental.HerramientasUf);
        Assert.Equal(full.MermasUf, incremental.MermasUf);
        Assert.Equal(full.CostoDirectoUf, incremental.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_CantidadCeroNoAfectaTotalVenta()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 0m, MatClp = 1m, MoClp = 0.5m, MatUf = 1m, MoUf = 0.5m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20", PrecioUf = 1m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        var items = new[]
        {
            new ItemizadoRow { Id = "1", Item = "1.1", Descripcion = "Canalización", Cantidad = 0m }
        };
        var settings = BudgetSettings.FromValues(10m, 20m, 0m);
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;
        settings.ApuMode = BudgetApuMode.ApuManda;

        var result = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(0m, result.CostoDirectoUf);
        Assert.Equal(0m, result.TotalVentaUf);
        Assert.Equal(2, result.Inconsistencias.Count);
        Assert.Contains("CantidadCero", result.Inconsistencias[0].Tipo);
    }

    [Fact]
    public void Calculate_MarkupPolicyClampsInvalidPercentages()
    {
        var policy = new MarkupPolicy
        {
            GastosGeneralesPct = -80m,
            UtilidadPct = 200m,
            RiesgoPct = -5m,
            DescuentoPct = 150m
        };

        Assert.Equal(0m, policy.SafeGastosGeneralesPct);
        Assert.Equal(100m, policy.SafeUtilidadPct);
        Assert.Equal(0m, policy.SafeRiesgoPct);
        Assert.Equal(100m, policy.SafeDescuentoPct);
        Assert.NotEmpty(policy.Validate());
    }

    [Fact]
    public void Calculate_CeroCubicacionNoActivaApu()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 0m, MatClp = 1m, MoClp = 0.5m, MatUf = 1m, MoUf = 0.5m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20", PrecioUf = 1m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        var items = new[]
        {
            new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Cantidad = 0m }
        };
        var settings = BudgetSettings.FromValues(10m, 20m, 0m);
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;
        settings.ApuMode = BudgetApuMode.CubicacionMasApuComplementarios;

        var result = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(0m, result.CostoDirectoUf);
        Assert.Equal(0m, result.TotalVentaUf);
    }

    [Fact]
    public void Calculate_CubicacionManda_NegativeOnlyCubicacionDoesNotActivateFullApu()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "DEV-PVC", Descripcion = "Devolución PVC", Unidad = "ML", Cantidad = -3m, MatClp = 2m, MatUf = 2m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20", PrecioUf = 10m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Ajuste", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionManda;

        var result = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(-6m, result.CostoDirectoUf);
        Assert.DoesNotContain(result.Origenes, x => x.Origen == BudgetCostOrigin.Apu);
        var material = Assert.Single(result.MaterialesCriticos);
        Assert.Equal("DEV-PVC", material.Codigo);
        Assert.Equal(-3m, material.Cantidad);
        Assert.Equal(-6m, material.TotalUf);
    }

    [Fact]
    public void Calculate_ApuManda_IgnoresValuedCubicacion_UsesApuOnly()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 10m, MatClp = 5m, MatUf = 5m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20", PrecioUf = 2m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;

        var result = engine.Calculate(rows, settings, apus, items, null);

        Assert.DoesNotContain(result.Origenes, x => x.Origen == BudgetCostOrigin.Cubicacion && x.ItemId == "P1");
        Assert.Contains(result.Origenes, x => x.Origen == BudgetCostOrigin.Apu && x.ItemId == "P1");
        Assert.True(result.CostoDirectoUf > 0m && result.CostoDirectoUf < 50m);
    }

    [Fact]
    public void Calculate_ComplementaryApu_NetZeroCubicacionStillPreventsApuMaterialsDoubleCount()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 5m, MatClp = 2m, MatUf = 2m },
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = -5m, MatClp = 2m, MatUf = 2m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20 APU", PrecioUf = 10m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT },
                new() { CodigoMaterial = "MO", Descripcion = "Mano de obra", ValorHHUf = 4m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MO },
                new() { CodigoMaterial = "EQ", Descripcion = "Equipo", PrecioUf = 3m, Rendimiento = 1m, Tipo = ApuTipoRecurso.EQ }
            }}
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionMasApuComplementarios;

        var result = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(3m, result.EquiposUf);
        Assert.True(result.CostoDirectoUf >= result.EquiposUf);
        Assert.Equal(0m, result.MaterialesUf);
        Assert.Equal(0m, result.ManoObraUf);
        // C1: en complementario, herramientas/transporte del APU solo entran si hay recursos
        // HERR/TR explícitos (aquí no hay) — antes se cargaba 0.12 (3% de la MO no cobrada).
        Assert.Equal(0m, result.HerramientasUf);
        // Imprevistos de alza (2%) solo sobre la base complementaria real (equipos 3) = 0.06;
        // antes 0.3424 porque se calculaba sobre el costo directo completo del APU (mat+MO incl.).
        Assert.Equal(0.06m, result.MermasUf);
        Assert.DoesNotContain(result.MaterialesCriticos, x => x.Descripcion == "PVC 20 APU");
    }

    [Fact]
    public void ApuRow_TotalPartidaUf_ZeroWhenCantidadCero()
    {
        var apu = new ApuRow
        {
            CantidadPartida = 0m,
            Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "MAT1", Descripcion = "Material", PrecioUf = 10m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }
        };
        Assert.Equal(0m, apu.TotalPartidaUf);
    }

    // Regresión para la Fase 2 del plan de auditoría (~/.claude/plans/glittery-stargazing-hippo.md):
    // GetItemQuantity/InferArea pasaron de escanear itemizado/rows completos por cada APU (O(N×M))
    // a un índice precomputado (BuildItemizadoIndex/BuildAreaIndex). Estos tests fijan el mismo
    // comportamiento que tenía el FirstOrDefault original, incluyendo sus casos límite.
    [Fact]
    public void InferArea_UsesFirstRowWithNonEmptyArea_WhenMultipleRowsShareItemId()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            // La primera fila de "P1" tiene Area explícitamente vacía (el default del modelo es
            // "General", no ""); InferArea debe saltarla y usar la siguiente fila con Area no
            // vacía, no la primera fila del ItemId a secas.
            new CubicacionRow { ItemId = "P1", Codigo = "A", Descripcion = "A", Unidad = "UN", Cantidad = 0m, Area = "" },
            new CubicacionRow { ItemId = "P1", Codigo = "B", Descripcion = "B", Unidad = "UN", Cantidad = 0m, Area = "Subestación" },
            new CubicacionRow { ItemId = "P1", Codigo = "C", Descripcion = "C", Unidad = "UN", Cantidad = 0m, Area = "Sala eléctrica" }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "MAT1", Descripcion = "Material", PrecioUf = 1m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;

        var result = engine.Calculate(rows, settings, apus, items, null);

        var apuOrigin = Assert.Single(result.Origenes, o => o.Origen == BudgetCostOrigin.Apu);
        Assert.Equal("Subestación", apuOrigin.Area);
    }

    [Fact]
    public void GetItemQuantity_UsesFirstItemizadoRow_WhenIdIsDuplicated()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "A", Descripcion = "A", Unidad = "UN", Cantidad = 0m }
        };
        var apus = new[]
        {
            new ApuRow { ItemId = "P1", Descripcion = "APU P1", Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "MAT1", Descripcion = "Material", PrecioUf = 1m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
            }}
        };
        // Id duplicado (no debería pasar en datos reales, pero el índice debe imitar el
        // comportamiento previo del FirstOrDefault: se queda con la PRIMERA coincidencia).
        var items = new[]
        {
            new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Primera", Cantidad = 3m },
            new ItemizadoRow { Id = "P1", Item = "1.1-dup", Descripcion = "Segunda", Cantidad = 99m }
        };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;

        var result = engine.Calculate(rows, settings, apus, items, null);

        var apuOrigin = Assert.Single(result.Origenes, o => o.Origen == BudgetCostOrigin.Apu);
        // PrecioUf=1 * Rendimiento=1 * CantidadPartida(3, de la primera fila) = 3
        Assert.Equal(3m, apuOrigin.MaterialesUf);
    }

    [Fact]
    public void Calculate_ManyPartidasWithApuAndCubicacion_MatchesFullAndIncrementalTotals()
    {
        // Stress de escala moderada (50 partidas × 20 filas de cubicación) para la Fase 2:
        // confirma que el índice precomputado da el mismo resultado que el cálculo completo,
        // y que la ruta incremental (que reusa el mismo índice) coincide con Calculate().
        const int partidaCount = 50;
        const int rowsPerPartida = 20;
        var engine = new BudgetEngine();
        var rows = new List<CubicacionRow>();
        var items = new List<ItemizadoRow>();
        var apus = new List<ApuRow>();

        for (int p = 0; p < partidaCount; p++)
        {
            string itemId = $"P{p}";
            items.Add(new ItemizadoRow { Id = itemId, Item = $"1.{p}", Descripcion = $"Partida {p}", Cantidad = 2m });
            for (int r = 0; r < rowsPerPartida; r++)
            {
                rows.Add(new CubicacionRow
                {
                    ItemId = itemId,
                    Codigo = $"MAT{p}-{r}",
                    Descripcion = $"Material {p}-{r}",
                    Unidad = "UN",
                    Cantidad = 1m,
                    MatClp = 0.5m,
                    MatUf = 0.5m,
                    MoClp = 0.1m,
                    MoUf = 0.1m,
                    Area = r == 0 ? $"Área {p}" : ""
                });
            }
            // Cada 3ra partida también tiene APU, para ejercitar GetItemQuantity/InferArea.
            if (p % 3 == 0)
            {
                apus.Add(new ApuRow
                {
                    ItemId = itemId,
                    Descripcion = $"APU {p}",
                    Recursos = new List<ApuRecursoRow>
                {
                    new() { CodigoMaterial = $"APUMAT{p}", Descripcion = "Material APU", PrecioUf = 2m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT },
                    // Recurso complementario real (equipo) para que el APU aporte algo en modo
                    // complementario y su origen exista (C1: un APU solo-MAT ya no genera origen).
                    new() { CodigoMaterial = $"APUEQ{p}", Descripcion = "Equipo APU", PrecioUf = 1m, Rendimiento = 1m, Tipo = ApuTipoRecurso.EQ }
                }
                });
            }
        }

        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionMasApuComplementarios;

        var full = engine.Calculate(rows, settings, apus, items, null);

        // Editar una sola partida y recalcular incremental debe dar el mismo total que un
        // Calculate() completo desde cero.
        rows[0].Cantidad = 5m;
        engine.MarkDirtyItem("P0");
        var incremental = engine.CalculateIncremental(rows, settings, apus, items, null, full);
        var fullAfterEdit = engine.Calculate(rows, settings, apus, items, null);

        Assert.Equal(fullAfterEdit.CostoDirectoUf, incremental.CostoDirectoUf);
        Assert.Equal(fullAfterEdit.TotalVentaUf, incremental.TotalVentaUf);
        Assert.Equal(fullAfterEdit.Origenes.Count, incremental.Origenes.Count);
        // Cada partida con APU debe conservar el Area de su primera fila con Area no vacía.
        var apuOriginForP0 = incremental.Origenes.Single(o => o.Origen == BudgetCostOrigin.Apu && o.ItemId == "P0");
        Assert.Equal("Área 0", apuOriginForP0.Area);
    }

    [Fact]
    public void Calculate_IncludesApprovedChangeOrders_WithBothAprobadoAndAprobada()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 10m, MatClp = 1m, MatUf = 10m }
        };
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 0m, utilidadPct: 0m, metrosCuadrados: 1m);
        var changeOrders = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-001", Titulo = "Adicional Tablero", Estado = "Aprobado", DeltaCostoUf = 5m },
            new() { Numero = "OC-002", Titulo = "Canalización Extra", Estado = "Aprobada", DeltaCostoUf = 3m },
            new() { Numero = "OC-003", Titulo = "Alimentador pendiente", Estado = "En revisión", DeltaCostoUf = 10m },
            new() { Numero = "OC-004", Titulo = "Idea descartada", Estado = "Rechazado", DeltaCostoUf = 20m },
            new() { Numero = "OC-005", Titulo = "Borrador inicial", Estado = "Borrador", DeltaCostoUf = 15m }
        };

        var result = engine.Calculate(rows, settings, null, null, changeOrders);

        // Base cubicacion: 10 UF
        // OC-001 (Aprobado): 5 UF
        // OC-002 (Aprobada): 3 UF
        // Total direct cost = 10 + 5 + 3 = 18 UF
        Assert.Equal(18m, result.CostoDirectoUf);
        Assert.Equal(8m, result.OrdenesCambioUf);

        var ocOrigins = result.Origenes.Where(x => x.Origen == BudgetCostOrigin.OrdenCambio).ToList();
        Assert.Equal(2, ocOrigins.Count);
        Assert.Contains(ocOrigins, x => x.Codigo == "OC-001" && x.OrdenesCambioUf == 5m);
        Assert.Contains(ocOrigins, x => x.Codigo == "OC-002" && x.OrdenesCambioUf == 3m);
    }
}

