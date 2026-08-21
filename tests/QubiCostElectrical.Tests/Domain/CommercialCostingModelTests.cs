using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class CommercialCostingModelTests
{
    private static ItemizadoRow Item(string id, decimal quantity) => new() { Id = id, PartidaId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Item = "1.1", Descripcion = "Partida", Unidad = "UN", Cantidad = quantity };

    [Fact]
    public void ExistingProjectWithoutRules_KeepsLegacyCost()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var row = new CubicacionRow { ItemId = "P1", Codigo = "M", Cantidad = 2m, MatClp = 100m };
        Assert.Equal(200m, engine.Calculate(new[] { row }, settings).CostoDirectoUf);
    }

    [Fact]
    public void UnitPriceRule_ReplacesTechnicalCostAndUsesContractQuantity()
    {
        var item = Item("P1", 3m);
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.CommercialPartidaRules.Add(new CommercialPartidaRule { PartidaId = item.PartidaId, UseProjectModel = false, Model = CommercialCostingModel.UnitPrice, UnitPriceClp = 250m });
        var technical = new CubicacionRow { ItemId = "P1", Codigo = "M", Cantidad = 3m, MatClp = 999m };
        Assert.Equal(750m, new BudgetEngine().Calculate(new[] { technical }, settings, itemizado: new[] { item }).CostoDirectoUf);
    }

    [Fact]
    public void TimeAndMaterialsRule_UsesHoursMaterialsAndExpenses()
    {
        var item = Item("P1", 1m);
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.CommercialPartidaRules.Add(new CommercialPartidaRule { PartidaId = item.PartidaId, UseProjectModel = false, Model = CommercialCostingModel.TimeAndMaterials, LaborHours = 4m, LaborHourlyRateClp = 100m, MaterialsClp = 50m, ReimbursableExpensesClp = 25m });
        var result = new BudgetEngine().Calculate(Array.Empty<CubicacionRow>(), settings, itemizado: new[] { item });
        Assert.Equal(475m, result.CostoDirectoUf);
        Assert.Equal(400m, result.ManoObraUf);
    }
}
