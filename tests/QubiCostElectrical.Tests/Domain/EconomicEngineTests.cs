using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class EconomicEngineTests
{
    [Fact]
    public void Calculate_ChainsPercentageConceptsInOrder()
    {
        // 1000 + 10% (sobre costo directo) = 1100; 1100 + 20% (sobre subtotal anterior) = 1320.
        var config = new EconomicConfig
        {
            Concepts = new List<EconomicConcept>
            {
                new() { Code = "OH", Name = "Gastos generales", Category = EconomicCategory.Overhead, CalculationType = CalculationType.Percentage, CalculationBase = CalculationBase.DirectCost, Value = 10m, Order = 1 },
                new() { Code = "UT", Name = "Utilidad", Category = EconomicCategory.Profit, CalculationType = CalculationType.Percentage, CalculationBase = CalculationBase.PreviousSubtotal, Value = 20m, Order = 2 }
            }.AsReadOnly()
        };
        var input = new EconomicInput { UnitDirectCost = 1000m, Config = config };

        var result = EconomicEngine.Calculate(input);

        Assert.Equal(1320m, result.FinalUnitPrice);
        Assert.Equal(100m, result.Overhead);
        Assert.Equal(220m, result.Profit);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.ConceptsApplied.Count);
    }

    [Fact]
    public void Calculate_ClampsDiscountExceedingPreviousSubtotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new List<EconomicConcept>
            {
                new() { Code = "DESC", Name = "Descuento", Category = EconomicCategory.Discount, CalculationType = CalculationType.Percentage, CalculationBase = CalculationBase.PreviousSubtotal, Value = 150m, Order = 1 }
            }.AsReadOnly()
        };
        var input = new EconomicInput { UnitDirectCost = 100m, Config = config };

        var result = EconomicEngine.Calculate(input);

        Assert.Equal(0m, result.FinalUnitPrice);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Validate_FlagsDuplicateOrder()
    {
        var config = new EconomicConfig
        {
            Concepts = new List<EconomicConcept>
            {
                new() { Code = "A", CalculationBase = CalculationBase.DirectCost, Order = 1 },
                new() { Code = "B", CalculationBase = CalculationBase.DirectCost, Order = 1 }
            }.AsReadOnly()
        };

        var result = new EconomicValidation().ValidateForCalculation(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_ORDER");
    }

    [Fact]
    public void EconomicCalculationService_ReturnsErrorResultInsteadOfThrowing_WhenConfigInvalid()
    {
        var service = new EconomicCalculationService();
        var config = new EconomicConfig { Concepts = new List<EconomicConcept>().AsReadOnly() };
        var input = new EconomicInput { UnitDirectCost = 500m, Config = config };

        var result = service.Calculate(input);

        Assert.NotEmpty(result.Errors);
    }
}
