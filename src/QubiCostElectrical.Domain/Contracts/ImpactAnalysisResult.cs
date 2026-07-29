using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ImpactAnalysisResult
{
    public decimal CostImpact { get; set; }
    public int ScheduleImpactDays { get; set; }
    public List<string> AffectedActivities { get; set; } = new();
    public List<string> AffectedMilestones { get; set; } = new();
    public bool MayChangeCriticalPath { get; set; }
    public decimal? BacImpact { get; set; }
    public decimal? EacImpact { get; set; }
    public decimal? EtcImpact { get; set; }
    public decimal? VacImpact { get; set; }
    public decimal OriginalContractAmount { get; set; }
    public decimal CurrentContractAmount { get; set; }
    public decimal ProposedAmount { get; set; }
    public int OriginalDurationDays { get; set; }
    public int CurrentDurationDays { get; set; }
    public int ProposedDurationDays { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string DataQuality { get; set; } = "Unknown";
}
