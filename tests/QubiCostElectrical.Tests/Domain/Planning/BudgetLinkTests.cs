using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class BudgetLinkTests
{
    [Fact]
    public void Create_ValidItemLink_SetsProperties()
    {
        var link = new BudgetLink
        {
            BudgetItemId = "ITEM-001",
            AllocationMethod = AllocationMethod.FullItem,
            AllocationValue = 100m,
            IsPrimary = true
        };
        Assert.Equal("ITEM-001", link.BudgetItemId);
        Assert.Equal(AllocationMethod.FullItem, link.AllocationMethod);
        Assert.True(link.IsPrimary);
    }

    [Fact]
    public void NonExistentItem_LinkStillCreated()
    {
        var link = new BudgetLink
        {
            BudgetItemId = "NONEXISTENT",
            AllocationMethod = AllocationMethod.ExplicitQuantity,
            AllocationValue = 50m
        };
        Assert.Equal("NONEXISTENT", link.BudgetItemId);
    }

    [Fact]
    public void PercentageAllocation_StoresValue()
    {
        var link = new BudgetLink
        {
            BudgetItemId = "ITEM-002",
            AllocationMethod = AllocationMethod.Percentage,
            AllocationValue = 25m
        };
        Assert.Equal(25m, link.AllocationValue);
        Assert.Equal(AllocationMethod.Percentage, link.AllocationMethod);
    }

    [Fact]
    public void ExcessAllocation_ValueAbove100_Stored()
    {
        var link = new BudgetLink
        {
            BudgetItemId = "ITEM-003",
            AllocationMethod = AllocationMethod.Percentage,
            AllocationValue = 150m
        };
        Assert.Equal(150m, link.AllocationValue);
    }

    [Fact]
    public void MultipleActivities_SameBudgetItem_Allowed()
    {
        var link1 = new BudgetLink { BudgetItemId = "ITEM-004", AllocationMethod = AllocationMethod.Percentage, AllocationValue = 50m };
        var link2 = new BudgetLink { BudgetItemId = "ITEM-004", AllocationMethod = AllocationMethod.Percentage, AllocationValue = 50m };
        Assert.Equal(link1.BudgetItemId, link2.BudgetItemId);
    }

    [Fact]
    public void Traceability_ApuLinked_HasApuId()
    {
        var link = new BudgetLink
        {
            BudgetItemId = "ITEM-005",
            ApuId = "APU-001",
            AllocationMethod = AllocationMethod.FullItem,
            IsPrimary = true
        };
        Assert.Equal("APU-001", link.ApuId);
    }
}
