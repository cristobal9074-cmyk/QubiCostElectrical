using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CloseoutReconciliationService : ICloseoutReconciliationService
{
    public Task<CloseoutReadinessResult> ReconcileAsync(string projectId, string closeoutId, string statusDate, string currency, CancellationToken ct)
    {
        var result = new CloseoutReadinessResult
        {
            GlobalPct = 0, IsReady = false,
            GeneratedUtc = System.DateTime.UtcNow.ToString("O"),
            DataQuality = DataQualityStatus.Partial
        };
        result.Dimensions.Add(new CloseoutReadinessScore { Dimension = CloseoutDimension.Economic, Pct = 0, Status = DataQualityStatus.MissingSource, Explanation = "Requiere BudgetEngine" });
        result.Dimensions.Add(new CloseoutReadinessScore { Dimension = CloseoutDimension.Contractual, Pct = 0, Status = DataQualityStatus.MissingSource, Explanation = "Requiere módulo de contratos" });
        result.Dimensions.Add(new CloseoutReadinessScore { Dimension = CloseoutDimension.Purchasing, Pct = 0, Status = DataQualityStatus.MissingSource, Explanation = "Requiere módulo de compras" });
        result.Dimensions.Add(new CloseoutReadinessScore { Dimension = CloseoutDimension.Planning, Pct = 0, Status = DataQualityStatus.MissingSource, Explanation = "Requiere planificación" });
        return Task.FromResult(result);
    }
}
