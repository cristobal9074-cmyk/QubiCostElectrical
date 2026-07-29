using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class Contract
{
    public ContractId Id { get; init; } = null!;
    public string VisibleNumber { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public ContractType Type { get; set; } = ContractType.Principal;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Parties metadata inside contract
    public string ClientName { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string SubcontractorOrSupplierName { get; set; } = string.Empty;
    public string ContractManager { get; set; } = string.Empty;
    
    // Economics
    public string CurrencyCode { get; set; } = "CLP";
    public decimal OriginalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount => OriginalAmount + TaxAmount;
    
    // Dates & Terms
    public DateTime SignDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int ContractualDurationDays { get; set; }
    public string ScopeDescription { get; set; } = string.Empty;
    public string PaymentConditions { get; set; } = string.Empty;
    public decimal AdvancePercentage { get; set; }
    public decimal RetentionPercentage { get; set; }
    public string GuaranteesRequired { get; set; } = string.Empty;
    
    // Metadata & Workflow
    public ContractStatus Status { get; set; } = ContractStatus.Borrador;
    public int Version { get; set; } = 1;
    public ContractId? ParentContractId { get; set; }
    
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public string Observations { get; set; } = string.Empty;
    public string CompatibilityMetadata { get; set; } = string.Empty;

    // Navigation/Collections (not in DB columns, saved in JSON or separate tables as required)
    public List<ContractParty> Parties { get; } = new();
    public List<ContractMilestone> Milestones { get; } = new();
    public List<ContractScopeLink> ScopeLinks { get; } = new();
    public List<ContractDocument> Documents { get; } = new();
}
