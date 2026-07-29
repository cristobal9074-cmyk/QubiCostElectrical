using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class SupplierOffer
{
    public SupplierOfferId Id { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public string? SourceResponseId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime OfferDateUtc { get; set; }
    public DateTime? ValidityUtc { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public decimal TaxPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public int DeliveryLeadDays { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Warranty { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public SupplierOfferStatus Status { get; set; } = SupplierOfferStatus.Draft;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<OfferLine> Lines { get; set; } = new();
    public List<CommercialCondition> CommercialConditions { get; set; } = new();
    public List<ProcurementDocumentRef> Documents { get; set; } = new();
}

public sealed class OfferLine
{
    public string LineId { get; init; } = null!;
    public SupplierOfferId OfferId { get; init; } = null!;
    public string? RfqItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public decimal Quantity { get; set; }
    public decimal UnitPriceOriginal { get; set; }
    public decimal UnitPriceBase { get; set; }
    public decimal LineTotalOriginal { get; set; }
    public decimal LineTotalBase { get; set; }
    public string TechnicalNotes { get; set; } = string.Empty;
    public bool MeetsSpecification { get; set; } = true;
    public List<OfferAlternative> Alternatives { get; set; } = new();
}

public sealed class OfferAlternative
{
    public string AlternativeId { get; init; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPriceOriginal { get; set; }
    public decimal UnitPriceBase { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class CommercialCondition
{
    public string ConditionId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class TechnicalEvaluation
{
    public string EvaluationId { get; init; } = null!;
    public SupplierOfferId OfferId { get; init; } = null!;
    public string? OfferLineId { get; set; }
    public TechnicalComplianceLevel Compliance { get; set; } = TechnicalComplianceLevel.NotEvaluated;
    public bool DocumentComplete { get; set; }
    public decimal Score { get; set; }
    public string Comments { get; set; } = string.Empty;
    public string EvaluatedBy { get; set; } = string.Empty;
    public DateTime EvaluatedUtc { get; set; }
}

public sealed class CommercialEvaluation
{
    public string EvaluationId { get; init; } = null!;
    public SupplierOfferId OfferId { get; init; } = null!;
    public CommercialRiskLevel Risk { get; set; } = CommercialRiskLevel.Medium;
    public decimal Score { get; set; }
    public string PaymentAssessment { get; set; } = string.Empty;
    public string WarrantyAssessment { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string EvaluatedBy { get; set; } = string.Empty;
    public DateTime EvaluatedUtc { get; set; }
}
