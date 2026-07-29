using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class RequestForQuotation
{
    public RequestForQuotationId Id { get; init; } = null!;
    public string Number { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? IssuedUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public string Responsible { get; set; } = string.Empty;
    public string ComparisonCurrency { get; set; } = "CLP";
    public string DeliveryPlace { get; set; } = string.Empty;
    public string Terms { get; set; } = string.Empty;
    public RfqStatus Status { get; set; } = RfqStatus.Draft;
    public string Remarks { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public List<string> LinkedRequisitionIds { get; set; } = new();
    public List<RfqItem> Items { get; set; } = new();
    public List<RfqSupplierInvitation> Invitations { get; set; } = new();
    public List<ProcurementDocumentRef> Documents { get; set; } = new();
}

public sealed class RfqItem
{
    public string ItemId { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public string? ResourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechnicalSpec { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public decimal Quantity { get; set; }
    public DateTime RequiredDate { get; set; }
    public string DeliveryPlace { get; set; } = string.Empty;
    public string? WbsNodeId { get; set; }
    public string? ActivityId { get; set; }
    public string? SourceRequisitionId { get; set; }
    public string? SourceRequisitionItemId { get; set; }
    public string ReferenceCurrency { get; set; } = "CLP";
    public string TechnicalNotes { get; set; } = string.Empty;
}

public sealed class RfqSupplierInvitation
{
    public string InvitationId { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public string? ContactId { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? ConfirmedUtc { get; set; }
    public RfqInvitationStatus Status { get; set; } = RfqInvitationStatus.Pending;
    public string Comment { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email";
    public string? DocumentRef { get; set; }
    public int RfqVersion { get; set; } = 1;
    public string Responsible { get; set; } = string.Empty;
}

public sealed class QuotationResponse
{
    public string ResponseId { get; init; } = null!;
    public RequestForQuotationId RfqId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public DateTime ReceivedUtc { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public DateTime? ValidityUtc { get; set; }
    public int DeliveryLeadDays { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? OfferDocumentRef { get; set; }
    public QuotationResponseStatus Status { get; set; } = QuotationResponseStatus.Received;
    public string IntegrityHash { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class ProcurementDocumentRef
{
    public string DocumentId { get; init; } = null!;
    public ProcurementDocumentKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PathOrUri { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime AttachedUtc { get; set; }
    public string AttachedBy { get; set; } = string.Empty;
}

public sealed class ProcurementAuditEvent
{
    public string Id { get; init; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime UtcTimestamp { get; set; }
    public string? PreviousState { get; set; }
    public string? NewState { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Result { get; set; } = "Ok";
    public string ProjectPath { get; set; } = string.Empty;
}
