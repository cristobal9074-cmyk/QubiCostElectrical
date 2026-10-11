#nullable enable
using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class Contract
{
    public required ContractId Id { get; init; }

    public string VisibleNumber { get; set; } = string.Empty;

    public string ProjectPath { get; set; } = string.Empty;

    public ContractType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string ContractorName { get; set; } = string.Empty;

    public string SubcontractorOrSupplierName { get; set; } = string.Empty;

    public string ContractManager { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "CLP";

    public decimal OriginalAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount => OriginalAmount + TaxAmount;

    public DateTime SignDate { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int ContractualDurationDays { get; set; }

    public string ScopeDescription { get; set; } = string.Empty;

    public string PaymentConditions { get; set; } = string.Empty;

    public decimal AdvancePercentage { get; set; }

    public decimal RetentionPercentage { get; set; }

    public string GuaranteesRequired { get; set; } = string.Empty;

    public ContractStatus Status { get; set; }

    public int Version { get; set; } = 1;

    public ContractId? ParentContractId { get; set; }

    public DateTime CreatedUtc { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTime ModifiedUtc { get; set; }

    public string ModifiedBy { get; set; } = string.Empty;

    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public string Observations { get; set; } = string.Empty;

    public string CompatibilityMetadata { get; set; } = string.Empty;

    public List<ContractParty> Parties { get; } = new List<ContractParty>();

    public List<ContractMilestone> Milestones { get; } = new List<ContractMilestone>();

    public List<ContractScopeLink> ScopeLinks { get; } = new List<ContractScopeLink>();

    public List<ContractDocument> Documents { get; } = new List<ContractDocument>();
}
