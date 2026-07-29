using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionPresenterTests
{
    private static (BudgetProjectSession session, CubicacionPresenter presenter) CreatePresenter()
    {
        var session = new BudgetProjectSession();
        var services = new CubicacionPresenterServices(
            new BudgetCalculationService(),
            new CubicacionCompositionService());
        return (session, new CubicacionPresenter(services, session));
    }

    [Fact]
    public void CalculatePartidaTotals_WithRows_ReturnsDirectCost()
    {
        var (session, presenter) = CreatePresenter();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "01", Descripcion = "Partida", Cantidad = 2m });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT1", Descripcion = "Material", Unidad = "UN", MatClp = 1000m, MoClp = 500m, MatUf = 1m, MoUf = 0.5m });

        var rows = new List<CubicacionRow>
        {
            new()
            {
                Id = "R1",
                ItemId = "P1",
                Codigo = "MAT1",
                Descripcion = "Material",
                Unidad = "UN",
                Cantidad = 10m,
                MatClp = 1000m,
                MoClp = 500m,
                MatUf = 1m,
                MoUf = 0.5m
            }
        };

        var totals = presenter.CalculatePartidaTotals("P1", rows);

        // Totales en CLP: Σ (MatClp × Cant) / Σ (MoClp × Cant)
        Assert.Equal(10_000m, totals.Materiales);
        Assert.Equal(5_000m, totals.ManoObra);
        Assert.Equal(15_000m, totals.CostoDirecto);
    }

    [Fact]
    public void ValidateRow_UnknownCode_ReturnsError()
    {
        var (_, presenter) = CreatePresenter();
        var row = new CubicacionRow { Codigo = "UNKNOWN", Cantidad = 1m, MatClp = 1000m, MatUf = 1m };

        var validation = presenter.ValidateRow(row);

        Assert.True(validation.HasError);
        Assert.Contains("UNKNOWN", validation.ErrorMessage);
    }

    [Fact]
    public void ValidateRow_TotalExpressionOnly_DoesNotRequirePositiveQuantity()
    {
        var (session, presenter) = CreatePresenter();
        session.NewProject("test", "Tester");
        session.Materiales.Add(new MaterialRow { Codigo = "MAT1", Descripcion = "Material", Unidad = "UN", MatClp = 1000m, MatUf = 1m, MoUf = 0m });

        var row = new CubicacionRow
        {
            Codigo = "MAT1",
            Cantidad = 0m,
            MatClp = 1000m,
            MatUf = 1m,
            TotalExpression = "10+2"
        };

        var validation = presenter.ValidateRow(row);

        Assert.Null(validation.CantidadError);
        Assert.False(validation.HasError);
    }

    [Fact]
    public void AfterMedicionesChanged_UpdatesLegacyQuantityFromNormalizedModel()
    {
        var (session, presenter) = CreatePresenter();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "01", Descripcion = "Partida", Cantidad = 1m });
        session.ZonasProyecto.Add(new ZonaProyecto { Id = "Z1", Nombre = "Zona 1", Orden = 0, Activa = true });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "MAT1",
            Descripcion = "Material",
            Unidad = "UN",
            Cantidad = 0m,
            MatUf = 1m,
            MoUf = 0m
        });
        session.Mediciones.Add(new MeasurementLineRow
        {
            ItemId = "P1",
            RecursoId = "R1",
            ZonaOffset = 0,
            Longitud = 5m,
            Unidades = 2m
        });

        presenter.AfterMedicionesChanged("P1");

        var row = session.Cubicacion[0];
        Assert.Equal(10m, row.Cantidad);
        Assert.Single(session.CubicacionRecursos);
    }
}
