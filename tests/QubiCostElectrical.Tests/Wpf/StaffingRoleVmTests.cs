using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class StaffingRoleVmTests
{
    [Fact]
    public void TotalClp_MatchesDomainFormula()
    {
        var row = new StaffingRoleRow { Cargo = "Administrador", TarifaMensualClp = 1_000_000m, DedicacionPct = 30m, Meses = 10 };
        var vm = new StaffingRoleVm(row, onChanged: null);

        Assert.Equal(row.TotalClp, vm.TotalClp);
        Assert.Equal(3_000_000m, vm.TotalClp);
    }

    [Fact]
    public void TarifaMensualClp_And_Meses_ClampToNonNegative()
    {
        var vm = new StaffingRoleVm(new StaffingRoleRow(), onChanged: null);

        vm.TarifaMensualClp = -500m;
        Assert.Equal(0m, vm.TarifaMensualClp);

        vm.Meses = -3;
        Assert.Equal(0, vm.Meses);
    }

    [Fact]
    public void DedicacionPct_ClampsToZeroToOneHundred()
    {
        var vm = new StaffingRoleVm(new StaffingRoleRow(), onChanged: null);

        vm.DedicacionPct = 150m;
        Assert.Equal(100m, vm.DedicacionPct);
    }

    [Fact]
    public void ToRow_RoundTripsAllFields()
    {
        var row = new StaffingRoleRow { Cargo = "Supervisor", TarifaMensualClp = 2_000_000m, DedicacionPct = 100m, Meses = 12 };
        var vm = new StaffingRoleVm(row, onChanged: null);

        var result = vm.ToRow();

        Assert.Equal(row.Id, result.Id);
        Assert.Equal("Supervisor", result.Cargo);
        Assert.Equal(2_000_000m, result.TarifaMensualClp);
        Assert.Equal(100m, result.DedicacionPct);
        Assert.Equal(12, result.Meses);
    }

    [Fact]
    public void OnChanged_FiresOnEveryFieldEdit()
    {
        int calls = 0;
        var vm = new StaffingRoleVm(new StaffingRoleRow(), onChanged: () => calls++);

        vm.Cargo = "Capataz";
        vm.TarifaMensualClp = 1_000_000m;
        vm.DedicacionPct = 50m;
        vm.Meses = 6;

        Assert.Equal(4, calls);
    }

    [Fact]
    public void ValidationHint_FlagsBlankCargo_ClearsWhenFixed()
    {
        var vm = new StaffingRoleVm(new StaffingRoleRow { Cargo = "" }, onChanged: null);

        Assert.True(vm.HasValidationError);

        vm.Cargo = "Prevencionista";

        Assert.False(vm.HasValidationError);
    }
}
