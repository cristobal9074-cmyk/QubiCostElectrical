using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class NegotiatedDiscountVmTests
{
    [Fact]
    public void ToRow_RoundTripsAllFields()
    {
        var row = new NegotiatedDiscountRow { Codigo = "TRAFO", Descripcion = "Transformador", DescuentoPct = 5m };
        var vm = new NegotiatedDiscountVm(row, onChanged: null);

        var result = vm.ToRow();

        Assert.Equal(row.Id, result.Id);
        Assert.Equal("TRAFO", result.Codigo);
        Assert.Equal("Transformador", result.Descripcion);
        Assert.Equal(5m, result.DescuentoPct);
    }

    [Fact]
    public void DescuentoPct_ClampsToZeroToOneHundred()
    {
        var vm = new NegotiatedDiscountVm(new NegotiatedDiscountRow(), onChanged: null);

        vm.DescuentoPct = 150m;
        Assert.Equal(100m, vm.DescuentoPct);

        vm.DescuentoPct = -20m;
        Assert.Equal(0m, vm.DescuentoPct);
    }

    [Fact]
    public void OnChanged_FiresOnEveryFieldEdit()
    {
        int calls = 0;
        var vm = new NegotiatedDiscountVm(new NegotiatedDiscountRow(), onChanged: () => calls++);

        vm.Codigo = "C1";
        vm.Descripcion = "x";
        vm.DescuentoPct = 10m;

        Assert.Equal(3, calls);
    }

    [Fact]
    public void ValidationHint_FlagsBlankCodigo_ClearsWhenFixed()
    {
        var vm = new NegotiatedDiscountVm(new NegotiatedDiscountRow { Codigo = "" }, onChanged: null);

        Assert.True(vm.HasValidationError);
        Assert.NotEmpty(vm.ValidationHint);

        vm.Codigo = "C1";

        Assert.False(vm.HasValidationError);
        Assert.Empty(vm.ValidationHint);
    }
}
