using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetCalculationServiceTests
{
    [Fact]
    public void CalculateBudget_ReusesIncrementalPathAfterDirtyItem()
    {
        var service = new BudgetCalculationService();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "MAT1", Descripcion = "Material 1", Unidad = "UN", Cantidad = 2m, MatClp = 5m }
        };

        var first = service.CalculateBudget(rows, settings);
        rows[0].Cantidad = 4m;
        service.MarkDirtyItem("P1");
        var second = service.CalculateBudget(rows, settings);

        Assert.Equal(10m, first.CostoDirectoUf);
        Assert.Equal(20m, second.CostoDirectoUf);
    }

    [Fact]
    public void ResetIncrementalCache_PreventsStaleDataFromPreviousProject()
    {
        var service = new BudgetCalculationService();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);

        var projectA = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "MAT1", Descripcion = "Material 1", Unidad = "UN", Cantidad = 10m, MatClp = 5m }
        };
        var firstResult = service.CalculateBudget(projectA, settings);
        Assert.Equal(50m, firstResult.CostoDirectoUf);

        service.ResetIncrementalCache();

        var projectB = new List<CubicacionRow>();
        var secondResult = service.CalculateBudget(projectB, settings);
        Assert.Equal(0m, secondResult.CostoDirectoUf);
    }
}
