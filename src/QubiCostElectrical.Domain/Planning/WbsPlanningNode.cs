using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class WbsPlanningNode
{
    public WbsPlanningNodeId Id { get; private set; } = null!;
    public ScheduleId ScheduleId { get; private set; } = null!;
    public string WbsCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WbsNodeType NodeType { get; set; }
    public WbsPlanningNodeId? ParentId { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }
    public ScheduleStatus Status { get; set; }
    public string? Responsible { get; set; }
    public string? BudgetItemId { get; set; }
    public string? ApuId { get; set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    public WbsPlanningNode(WbsPlanningNodeId id, ScheduleId scheduleId, string wbsCode, string name, WbsNodeType nodeType, string modifiedBy)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ScheduleId = scheduleId ?? throw new ArgumentNullException(nameof(scheduleId));
        WbsCode = wbsCode ?? throw new ArgumentNullException(nameof(wbsCode));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NodeType = nodeType;
        ModifiedBy = modifiedBy ?? throw new ArgumentNullException(nameof(modifiedBy));
        Description = string.Empty;
        Status = ScheduleStatus.Draft;
        Level = 1;
        Tags = string.Empty;
        Remarks = string.Empty;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = CreatedAt;
    }

    public WbsPlanningNode() { }
}
