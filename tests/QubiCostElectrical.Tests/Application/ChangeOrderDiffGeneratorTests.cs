using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ChangeOrderDiffGeneratorTests
{
    [Fact]
    public void GenerateNextNumber_EmptyList_ReturnsOC001()
    {
        var result = ChangeOrderDiffGenerator.GenerateNextNumber(Array.Empty<ChangeOrderRow>());
        Assert.Equal("OC-001", result);
    }

    [Fact]
    public void GenerateNextNumber_ExistingOrders_IncrementsMax()
    {
        var existing = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-001" },
            new() { Numero = "OC-004" },
            new() { Numero = "OC-002" }
        };

        var result = ChangeOrderDiffGenerator.GenerateNextNumber(existing);
        Assert.Equal("OC-005", result);
    }

    [Fact]
    public void CreateFromDiff_WhenNoChanges_ReturnsNull()
    {
        var summary = new BudgetVersionComparisonSummary
        {
            BaselineLabel = "Rev 0",
            CurrentLabel = "Rev 1",
            Rows = new List<BudgetVersionComparisonRow>
            {
                new("P1", "1.1", "Canalización", 10m, 10m, 0m, 0m, "Sin cambio")
                {
                    DiffKind = RevisionDiffKind.SinCambio
                }
            }
        };

        var result = ChangeOrderDiffGenerator.CreateFromDiff(
            summary,
            Array.Empty<ChangeOrderRow>(),
            10m,
            8m,
            "Estimador");

        Assert.Null(result);
    }

    [Fact]
    public void CreateFromDiff_WhenHasChanges_PopulatesChangeOrderCorrectly()
    {
        var summary = new BudgetVersionComparisonSummary
        {
            BaselineLabel = "Rev 0",
            CurrentLabel = "Rev 1",
            BaselineUf = 100m,
            CurrentUf = 125m,
            Rows = new List<BudgetVersionComparisonRow>
            {
                new("P1", "1.1", "Tablero General", 50m, 60m, 10m, 20m, "Aumentó")
                {
                    DiffKind = RevisionDiffKind.Modificado,
                    CantidadBase = 1m,
                    CantidadActual = 1m,
                    DetalleCambio = "Aumento de protecciones"
                },
                new("P2", "1.2", "Circuito Emergencia", 0m, 20m, 20m, 100m, "Nuevo")
                {
                    DiffKind = RevisionDiffKind.Adicional,
                    CantidadBase = 0m,
                    CantidadActual = 1m,
                    DetalleCambio = "Nueva partida adicional"
                },
                new("P3", "1.3", "Lámpara Halógena", 10m, 0m, -10m, -100m, "Eliminado")
                {
                    DiffKind = RevisionDiffKind.Deductivo,
                    CantidadBase = 5m,
                    CantidadActual = 0m,
                    DetalleCambio = "Partida eliminada"
                },
                new("P4", "1.4", "Enchufes", 40m, 40m, 0m, 0m, "Sin cambio")
                {
                    DiffKind = RevisionDiffKind.SinCambio
                }
            }
        };

        var existing = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-001" }
        };

        var oc = ChangeOrderDiffGenerator.CreateFromDiff(
            summary,
            existing,
            gastosGeneralesPct: 10m,
            utilidadPct: 15m,
            usuarioNombre: "Carlos Estimador");

        Assert.NotNull(oc);
        Assert.Equal("OC-002", oc!.Numero);
        Assert.Equal("Ajuste contractual Rev 0 vs Rev 1", oc.Titulo);
        Assert.Equal("Borrador", oc.Estado);
        Assert.Equal("Carlos Estimador", oc.CreadoPor);
        Assert.Equal("Rev 0", oc.PresupuestoBaseEstado);
        Assert.Equal(100m, oc.CostoBaseUf);
        Assert.Equal(10m, oc.GastosGeneralesPct);
        Assert.Equal(15m, oc.UtilidadPct);

        // Delta Costo = 125 - 100 = 25 UF
        Assert.Equal(25m, oc.DeltaCostoUf);
        // Delta GG = 25 * 0.10 = 2.5 UF
        Assert.Equal(2.5m, oc.DeltaGastosGeneralesUf);
        // Delta Utilidad = 25 * 0.15 = 3.75 UF
        Assert.Equal(3.75m, oc.DeltaUtilidadUf);
        // Delta Venta = 25 * (1 + 0.10 + 0.15) = 31.25 UF
        Assert.Equal(31.25m, oc.DeltaVentaUf);

        // 3 líneas con cambio (P1, P2, P3); P4 sin cambio se excluye
        Assert.Equal(3, oc.Lineas.Count);

        var l1 = oc.Lineas[0];
        Assert.Equal("P1", l1.ItemId);
        Assert.Equal("1.1", l1.Item);
        Assert.Equal("Modificación", l1.TipoCambio);
        Assert.Equal(10m, l1.DeltaCostoUf);

        var l2 = oc.Lineas[1];
        Assert.Equal("P2", l2.ItemId);
        Assert.Equal("Adicional", l2.TipoCambio);
        Assert.Equal(20m, l2.DeltaCostoUf);

        var l3 = oc.Lineas[2];
        Assert.Equal("P3", l3.ItemId);
        Assert.Equal("Deductivo", l3.TipoCambio);
        Assert.Equal(-10m, l3.DeltaCostoUf);

        // Historial con entrada inicial
        Assert.Single(oc.Historial);
        Assert.Equal("Borrador", oc.Historial[0].EstadoNuevo);
        Assert.Equal("Carlos Estimador", oc.Historial[0].Usuario);
    }
}
