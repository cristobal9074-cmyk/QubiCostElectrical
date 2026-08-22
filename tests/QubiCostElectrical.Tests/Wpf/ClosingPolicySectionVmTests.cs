using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ClosingPolicySectionVmTests
{
    private static ClosingPolicySectionVm Make(out List<(string Param, decimal Value)> commits)
    {
        var captured = new List<(string, decimal)>();
        commits = captured;
        return new ClosingPolicySectionVm(_ => { }, (p, v) => captured.Add((p, v)));
    }

    [Fact]
    public void PercentFields_ClampToZeroToOneHundred()
    {
        var vm = Make(out var commits);

        vm.FeePctText = "500";
        vm.LumpSumMinRiskPctText = "-10";
        vm.GainsharePctText = "150";

        Assert.Equal(100m, commits.Single(c => c.Param == nameof(ClosingPolicySelection.FeePct)).Value);
        Assert.Equal(0m, commits.Single(c => c.Param == nameof(ClosingPolicySelection.LumpSumMinRiskPct)).Value);
        Assert.Equal(100m, commits.Single(c => c.Param == nameof(ClosingPolicySelection.GainsharePct)).Value);
    }

    [Fact]
    public void ClpFields_FloorAtZero()
    {
        var vm = Make(out var commits);

        vm.TargetPriceClpText = "-1000000";
        vm.FeeClpText = "-500";
        vm.TargetCostClpText = "-1";
        vm.GainshareCapClpText = "-1";

        Assert.All(commits, c => Assert.True(c.Value >= 0m));
        Assert.Equal(0m, commits.Single(c => c.Param == nameof(ClosingPolicySelection.TargetPriceClp)).Value);
    }

    [Fact]
    public void ClpFields_AllowPositiveValuesUnclamped()
    {
        var vm = Make(out var commits);

        vm.TargetPriceClpText = "150000000";

        Assert.Equal(150_000_000m, commits.Single(c => c.Param == nameof(ClosingPolicySelection.TargetPriceClp)).Value);
    }
}
