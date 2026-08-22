using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class NegotiatedDiscountAndStaffingValidationTests
{
    [Fact]
    public void NegotiatedDiscountRow_Validate_FlagsBlankCodigo()
    {
        var row = new NegotiatedDiscountRow { Codigo = "", DescuentoPct = 5m };
        Assert.NotEmpty(row.Validate());
    }

    [Fact]
    public void NegotiatedDiscountRow_Validate_FlagsOutOfRangeDescuento()
    {
        var row = new NegotiatedDiscountRow { Codigo = "C1", DescuentoPct = 150m };
        Assert.NotEmpty(row.Validate());
    }

    [Fact]
    public void NegotiatedDiscountRow_Validate_PassesForValidRow()
    {
        var row = new NegotiatedDiscountRow { Codigo = "C1", DescuentoPct = 10m };
        Assert.Empty(row.Validate());
    }

    [Fact]
    public void StaffingRoleRow_Validate_FlagsBlankCargo()
    {
        var row = new StaffingRoleRow { Cargo = "", TarifaMensualClp = 100m };
        Assert.NotEmpty(row.Validate());
    }

    [Fact]
    public void StaffingRoleRow_Validate_FlagsNegativeMeses()
    {
        var row = new StaffingRoleRow { Cargo = "Administrador", Meses = -1 };
        Assert.NotEmpty(row.Validate());
    }

    [Fact]
    public void StaffingRoleRow_Validate_PassesForValidRow()
    {
        var row = new StaffingRoleRow { Cargo = "Administrador", TarifaMensualClp = 1_000_000m, DedicacionPct = 100m, Meses = 12 };
        Assert.Empty(row.Validate());
    }
}
