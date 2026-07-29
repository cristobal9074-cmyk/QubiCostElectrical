using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ExtraWork
{
    public ExtraWorkId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public ChangeRequestId? ChangeRequestId { get; set; }
    public ChangeOrderId? ChangeOrderId { get; set; }
    public string VisibleNumber { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExtraWorkOrigin Origin { get; set; }
    public DateTime InstructionDate { get; set; }
    public string WbsCode { get; set; } = string.Empty;
    public string ActivityCode { get; set; } = string.Empty;
    public string Resources { get; set; } = string.Empty;
    public string Materials { get; set; } = string.Empty;
    public string Labor { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public decimal Quantities { get; set; }
    public decimal Hours { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal FinalCost { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public string Evidence { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovalDate { get; set; }
    public decimal ValuationAmount { get; set; }
    public ExtraWorkStatus Status { get; set; } = ExtraWorkStatus.Draft;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}
