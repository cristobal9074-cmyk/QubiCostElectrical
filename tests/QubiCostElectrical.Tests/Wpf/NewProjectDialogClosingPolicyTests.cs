using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Project;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class NewProjectDialogClosingPolicyTests
{
    [Fact]
    public void Build_CostPlusMarkup_IsDefaultWithNoParam()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.CostPlusMarkup, 0m);
        Assert.Equal(ClosingPolicyType.CostPlusMarkup, s.Policy);
        Assert.Equal(0m, s.TargetPriceClp);
    }

    [Fact]
    public void Build_TargetPrice_MapsParamToTargetPriceClp()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.TargetPrice, 150_000_000m);
        Assert.Equal(ClosingPolicyType.TargetPrice, s.Policy);
        Assert.Equal(150_000_000m, s.TargetPriceClp);
    }

    [Fact]
    public void Build_LumpSumEpc_MapsParamToMinRiskPctClamped()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.LumpSumEpc, 12m);
        Assert.Equal(12m, s.LumpSumMinRiskPct);
        Assert.Equal(100m, NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.LumpSumEpc, 500m).LumpSumMinRiskPct);
    }

    [Fact]
    public void Build_CostPlusFee_SmallParamIsPercent_LargeParamIsAmount()
    {
        Assert.Equal(8m, NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.CostPlusFee, 8m).FeePct);
        Assert.Equal(5_000_000m, NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.CostPlusFee, 5_000_000m).FeeClp);
    }

    [Fact]
    public void Build_Gainshare_MapsParamToTargetCostClp()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.GainsharePainshare, 90_000_000m);
        Assert.Equal(ClosingPolicyType.GainsharePainshare, s.Policy);
        Assert.Equal(90_000_000m, s.TargetCostClp);
    }

    [Fact]
    public void Build_UnitPricePure_HasNoParam()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.UnitPricePure, 0m);
        Assert.Equal(ClosingPolicyType.UnitPricePure, s.Policy);
    }

    [Fact]
    public void Build_NegativeParam_IsFlooredAtZero()
    {
        var s = NewProjectDialog.BuildClosingPolicy(ClosingPolicyType.TargetPrice, -1m);
        Assert.Equal(0m, s.TargetPriceClp);
    }
}
