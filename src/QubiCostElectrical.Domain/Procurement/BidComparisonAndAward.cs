using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class BidComparison
{
    public BidComparisonId Id { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public string ProjectPath { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "CLP";
    public decimal ExchangeRate { get; set; } = 1m;
    public DateTime ExchangeRateDate { get; set; }
    public BidComparisonStatus Status { get; set; } = BidComparisonStatus.Draft;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<BidComparisonRow> Rows { get; set; } = new();
    public AwardRecommendation? Recommendation { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class BidComparisonRow
{
    public string RowId { get; init; } = null!;
    public string? RfqItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public decimal RequestedQuantity { get; set; }
    public decimal BudgetUnitPriceBase { get; set; }
    public List<BidOfferCell> Cells { get; set; } = new();
}

public sealed class BidOfferCell
{
    public SupplierOfferId OfferId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public string? OfferLineId { get; set; }
    public string CurrencyOriginal { get; set; } = "CLP";
    public decimal UnitPriceOriginal { get; set; }
    public decimal UnitPriceBase { get; set; }
    public decimal LineTotalBase { get; set; }
    public decimal TaxAmountBase { get; set; }
    public decimal DiscountAmountBase { get; set; }
    public decimal FreightShareBase { get; set; }
    public decimal InsuranceShareBase { get; set; }
    public decimal TotalAcquisitionCostBase { get; set; }
    public int DeliveryLeadDays { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public TechnicalComplianceLevel TechnicalCompliance { get; set; }
    public bool DocumentComplete { get; set; }
    public DateTime? ValidityUtc { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Warranty { get; set; } = string.Empty;
    public CommercialRiskLevel Risk { get; set; }
    public decimal BudgetDeviationPercent { get; set; }
    public decimal RankScore { get; set; }
    public bool IsRecommended { get; set; }
}

public sealed class AwardRecommendation
{
    public string RecommendationId { get; init; } = null!;
    public AwardKind SuggestedKind { get; set; } = AwardKind.Total;
    public string Summary { get; set; } = string.Empty;
    public List<RecommendedAwardLine> Lines { get; set; } = new();
    public decimal EstimatedTotalBase { get; set; }
    public DateTime IssuedUtc { get; set; }
    public string IssuedBy { get; set; } = "system";
    public bool RequiresConfirmation { get; set; } = true;
}

public sealed class RecommendedAwardLine
{
    public string? RfqItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public SupplierOfferId OfferId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPriceBase { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class AwardDecision
{
    public AwardId Id { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public BidComparisonId? ComparisonId { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public AwardKind Kind { get; set; } = AwardKind.Total;
    public AwardStatus Status { get; set; } = AwardStatus.Draft;
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string? Approver { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public string? RejectionReason { get; set; }
    public decimal BudgetImpactBase { get; set; }
    public string ScheduleImpactNotes { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<AwardedItem> Items { get; set; } = new();
    public List<DiscardedAlternative> DiscardedAlternatives { get; set; } = new();
    public List<AwardConflict> Conflicts { get; set; } = new();
    public List<AwardException> Exceptions { get; set; } = new();
}

public sealed class AwardedItem
{
    public string AwardedItemId { get; init; } = null!;
    public AwardId AwardId { get; init; } = null!;
    public string? RfqItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SupplierOfferId OfferId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPriceBase { get; set; }
    public decimal TotalBase { get; set; }
    public string CurrencyOriginal { get; set; } = "CLP";
    public decimal UnitPriceOriginal { get; set; }
}

public sealed class DiscardedAlternative
{
    public string Id { get; init; } = null!;
    public SupplierOfferId OfferId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public string? RfqItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class AwardConflict
{
    public string ConflictId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> ItemIds { get; init; } = Array.Empty<string>();
}

public sealed class AwardException
{
    public string ExceptionId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
    public DateTime AuthorizedUtc { get; set; }
}
