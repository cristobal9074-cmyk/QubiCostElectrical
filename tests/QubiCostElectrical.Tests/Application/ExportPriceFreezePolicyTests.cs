using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ExportPriceFreezePolicyTests
{
    [Fact]
    public void IsFrozen_WhenProjectFrozen_ReturnsTrue()
    {
        var policy = new ExportPriceFreezePolicy();
        var proyecto = new ProjectInfoRow { PreciosCongelados = true };

        Assert.True(policy.IsFrozen(proyecto));
        Assert.False(policy.RequiresConfirmation(proyecto));
    }

    [Fact]
    public void RequiresConfirmation_WhenProjectNotFrozen_ReturnsTrue()
    {
        var policy = new ExportPriceFreezePolicy();
        var proyecto = new ProjectInfoRow { PreciosCongelados = false };

        Assert.False(policy.IsFrozen(proyecto));
        Assert.True(policy.RequiresConfirmation(proyecto));
    }
}
