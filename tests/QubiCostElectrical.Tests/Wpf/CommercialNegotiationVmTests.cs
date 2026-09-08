using System;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class CommercialNegotiationVmTests
{
    [Fact]
    public void NegotiationSimulator_ZeroDiscount_MatchesBaseSale()
    {
        decimal appliedTarget = 0m;
        var vm = new CommercialNegotiationVm(t => appliedTarget = t);

        // Costo directo $80M, Venta $100M, UF $39.000
        vm.UpdateBase(80_000_000m, 100_000_000m, 39_000m);

        Assert.Equal(100_000_000m, vm.NegotiatedPriceClp);
        Assert.Equal(0m, vm.DiscountAmountClp);
        Assert.Equal(20_000_000m, vm.ResultingProfitClp);
        Assert.Equal(20.0m, vm.ResultingMarginPct);
        Assert.Equal(NegotiationRiskLevel.Healthy, vm.RiskLevel);
    }

    [Fact]
    public void NegotiationSimulator_5PercentDiscount_RecomputesCorrectly()
    {
        var vm = new CommercialNegotiationVm(_ => { });

        // Costo directo $80M, Venta $100M, UF $39.000
        vm.UpdateBase(80_000_000m, 100_000_000m, 39_000m);
        vm.DiscountPct = 5.0m;

        // Precio negociado $95M, Descuento $5M, Utilidad $15M
        Assert.Equal(95_000_000m, vm.NegotiatedPriceClp);
        Assert.Equal(5_000_000m, vm.DiscountAmountClp);
        Assert.Equal(15_000_000m, vm.ResultingProfitClp);

        // Margen = (15 / 95) * 100 = 15.78% -> Healthy (>15%)
        Assert.True(vm.ResultingMarginPct >= 15.0m);
        Assert.Equal(NegotiationRiskLevel.Healthy, vm.RiskLevel);
    }

    [Fact]
    public void NegotiationSimulator_ExcessiveDiscount_TriggersWarningOrCriticalRisk()
    {
        var vm = new CommercialNegotiationVm(_ => { });

        // Costo directo $85M, Venta $100M
        vm.UpdateBase(85_000_000m, 100_000_000m, 39_000m);

        // Descuento 8% -> Venta $92M, Margen (92-85)/92 = 7.6% -> Critical
        vm.DiscountPct = 8.0m;
        Assert.Equal(NegotiationRiskLevel.Critical, vm.RiskLevel);
        Assert.True(vm.ResultingMarginPct < 10m);
    }

    [Fact]
    public void NegotiationSimulator_ApplyCommand_InvokesCallbackWithPrice()
    {
        decimal capturedPrice = 0m;
        var vm = new CommercialNegotiationVm(p => capturedPrice = p);

        vm.UpdateBase(50_000_000m, 60_000_000m, 39_000m);
        vm.DiscountPct = 5.0m; // 60M * 0.95 = 57M

        Assert.True(vm.ApplyTargetPriceCommand.CanExecute(null));
        vm.ApplyTargetPriceCommand.Execute(null);

        Assert.Equal(57_000_000m, capturedPrice);
    }
}
