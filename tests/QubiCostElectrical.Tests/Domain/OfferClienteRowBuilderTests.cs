using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class OfferClienteRowBuilderTests
{
    [Fact]
    public void Build_ComplementaryMode_UsesBudgetEngineOrigins_NotCubOrApuHeuristic()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 5m, MatClp = 2m, MatUf = 2m }
        };
        var apus = new[]
        {
            new ApuRow
            {
                ItemId = "P1",
                Descripcion = "APU P1",
                Recursos = new List<ApuRecursoRow>
                {
                    new() { CodigoMaterial = "EQ1", Descripcion = "Equipo", PrecioUf = 3m, Rendimiento = 1m, Tipo = ApuTipoRecurso.EQ }
                }
            }
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionMasApuComplementarios;

        var cost = engine.Calculate(rows, settings, apus, items, null);
        decimal ventaP1 = cost.Origenes.Where(o => o.ItemId == "P1").Sum(o => o.VentaClp);

        var offerRows = OfferClienteRowBuilder.Build(items, cost, cost.TotalVentaNetoUf);
        var partida = Assert.Single(offerRows, r => r.Item == "1.1");

        Assert.True(ventaP1 > 10m, "El modo complementario debe sumar cubicación y recursos APU.");
        // C5: una sola partida costeable => absorbe toda la venta neta (al peso).
        Assert.Equal(Money.Round(cost.TotalVentaNetoUf), partida.TotalUf);
    }

    [Fact]
    public void Build_ApuMandaMode_MatchesEngineDirectCost_NotCubicacionTotal()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Unidad = "ML", Cantidad = 10m, MatClp = 5m, MatUf = 5m }
        };
        var apus = new[]
        {
            new ApuRow
            {
                ItemId = "P1",
                Descripcion = "APU P1",
                Recursos = new List<ApuRecursoRow>
                {
                    new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20", PrecioUf = 2m, Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT }
                }
            }
        };
        var items = new[] { new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Cantidad = 1m } };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;

        var cost = engine.Calculate(rows, settings, apus, items, null);
        decimal directP1 = cost.Origenes.Where(o => o.ItemId == "P1").Sum(o => o.TotalUf);
        Assert.True(directP1 > 0m && directP1 < 50m, "ApuManda: usa el costo del APU, no el de la cubicación.");

        var offerRows = OfferClienteRowBuilder.Build(items, cost, cost.TotalVentaNetoUf);
        var partida = Assert.Single(offerRows, r => r.Item == "1.1");
        Assert.Equal(Money.Round(cost.TotalVentaNetoUf), partida.TotalUf);
    }
}
