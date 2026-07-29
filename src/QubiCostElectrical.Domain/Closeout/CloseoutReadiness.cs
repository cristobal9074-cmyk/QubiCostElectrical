using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class CloseoutReadinessResult
{
    public double GlobalPct { get; set; }
    public List<CloseoutReadinessScore> Dimensions { get; set; } = new();
    public List<CloseoutBlockingIssue> Blockers { get; set; } = new();
    public bool IsReady { get; set; }
    public string GeneratedUtc { get; set; } = string.Empty;
    public DataQualityStatus DataQuality { get; set; }
}

public sealed class CloseoutReadinessScore
{
    public CloseoutDimension Dimension { get; set; }
    public double Pct { get; set; }
    public DataQualityStatus Status { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string Evidence { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public sealed class CloseoutBlockingIssue
{
    public string Code { get; set; } = string.Empty;
    public CloseoutDimension Dimension { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCritical { get; set; } = true;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
}

public sealed class CloseoutReadinessPolicy
{
    public bool NoCriticalBlockers { get; set; } = true;
    public bool MandatoryChecklistComplete { get; set; } = true;
    public bool BlockerDocumentsApproved { get; set; } = true;
    public bool CriticalPunchClosed { get; set; } = true;
    public bool CriticalNcClosed { get; set; } = true;
    public bool EconomicConsistency { get; set; } = true;
    public bool NoContractualBlockers { get; set; } = true;
    public bool NoUnauthorizedCriticalActivity { get; set; } = true;
}
