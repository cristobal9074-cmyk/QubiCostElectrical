using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ISupplierOfferService
{
    Task<SupplierOffer> RegisterAsync(SupplierOffer draft, string actor, CancellationToken ct = default);
    Task<SupplierOffer> SubmitAsync(SupplierOfferId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<TechnicalEvaluation> SaveTechnicalAsync(TechnicalEvaluation evaluation, string actor, CancellationToken ct = default);
    Task<CommercialEvaluation> SaveCommercialAsync(CommercialEvaluation evaluation, string actor, CancellationToken ct = default);
    Task<SupplierOffer?> GetAsync(SupplierOfferId id, CancellationToken ct = default);
    Task<List<SupplierOffer>> ListByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public sealed class SupplierOfferService : ISupplierOfferService
{
    private readonly ISupplierOfferRepository _offers;
    private readonly ITechnicalEvaluationRepository _tech;
    private readonly ICommercialEvaluationRepository _commercial;
    private readonly IRequestForQuotationRepository _rfqs;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public SupplierOfferService(
        ISupplierOfferRepository offers,
        ITechnicalEvaluationRepository tech,
        ICommercialEvaluationRepository commercial,
        IRequestForQuotationRepository rfqs,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _offers = offers;
        _tech = tech;
        _commercial = commercial;
        _rfqs = rfqs;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<SupplierOffer> RegisterAsync(SupplierOffer draft, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationRegister, actor);
        _ = await _rfqs.GetByIdAsync(draft.RfqId, ct) ?? throw new InvalidOperationException("RfqNotFound");
        var offer = new SupplierOffer
        {
            Id = draft.Id ?? SupplierOfferId.New(),
            RfqId = draft.RfqId,
            SupplierId = draft.SupplierId,
            SourceResponseId = draft.SourceResponseId,
            Version = draft.Version <= 0 ? 1 : draft.Version,
            OfferDateUtc = draft.OfferDateUtc == default ? DateTime.UtcNow : draft.OfferDateUtc,
            ValidityUtc = draft.ValidityUtc,
            CurrencyCode = string.IsNullOrWhiteSpace(draft.CurrencyCode) ? "CLP" : draft.CurrencyCode,
            TaxPercent = draft.TaxPercent,
            DiscountPercent = draft.DiscountPercent,
            FreightAmount = draft.FreightAmount,
            InsuranceAmount = draft.InsuranceAmount,
            DeliveryLeadDays = draft.DeliveryLeadDays,
            PaymentTerms = draft.PaymentTerms ?? string.Empty,
            Warranty = draft.Warranty ?? string.Empty,
            Availability = draft.Availability ?? string.Empty,
            Conditions = draft.Conditions ?? string.Empty,
            Remarks = draft.Remarks ?? string.Empty,
            Status = SupplierOfferStatus.Draft,
            CreatedBy = actor,
            CreatedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            Lines = draft.Lines ?? new List<OfferLine>(),
            CommercialConditions = draft.CommercialConditions ?? new List<CommercialCondition>(),
            Documents = draft.Documents ?? new List<ProcurementDocumentRef>(),
            RowVersion = Guid.NewGuid().ToString("N")
        };
        ThrowIf(OfferAwardValidation.ValidateOffer(offer));
        await _offers.SaveAsync(offer, ct);
        await Audit("Offer", offer.Id.Value, "Register", actor, null, offer.Status.ToString(), ct);
        return offer;
    }

    public async Task<SupplierOffer> SubmitAsync(SupplierOfferId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationRegister, actor);
        var offer = await LoadWritable(id, expectedRowVersion, ct);
        ThrowIf(OfferAwardValidation.ValidateOffer(offer));
        offer.Status = SupplierOfferStatus.Submitted;
        offer.ModifiedBy = actor;
        offer.ModifiedUtc = DateTime.UtcNow;
        offer.RowVersion = Guid.NewGuid().ToString("N");
        await _offers.SaveAsync(offer, ct);
        await Audit("Offer", offer.Id.Value, "Submit", actor, "Draft", offer.Status.ToString(), ct);
        return offer;
    }

    public async Task<TechnicalEvaluation> SaveTechnicalAsync(TechnicalEvaluation evaluation, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.TechnicalEvaluationManage, actor);
        _ = await _offers.GetByIdAsync(evaluation.OfferId, ct) ?? throw new InvalidOperationException("InvalidQuotationResponse");
        evaluation = new TechnicalEvaluation
        {
            EvaluationId = string.IsNullOrWhiteSpace(evaluation.EvaluationId) ? Guid.NewGuid().ToString("N") : evaluation.EvaluationId,
            OfferId = evaluation.OfferId,
            OfferLineId = evaluation.OfferLineId,
            Compliance = evaluation.Compliance,
            DocumentComplete = evaluation.DocumentComplete,
            Score = PrecisionPolicy.RoundPorcentaje(evaluation.Score),
            Comments = evaluation.Comments ?? string.Empty,
            EvaluatedBy = actor,
            EvaluatedUtc = DateTime.UtcNow
        };
        await _tech.SaveAsync(evaluation, ct);
        var offer = await _offers.GetByIdAsync(evaluation.OfferId, ct);
        if (offer is not null)
        {
            offer.Status = SupplierOfferStatus.UnderEvaluation;
            offer.RowVersion = Guid.NewGuid().ToString("N");
            await _offers.SaveAsync(offer, ct);
        }
        await Audit("TechnicalEvaluation", evaluation.EvaluationId, "Save", actor, null, evaluation.Compliance.ToString(), ct);
        return evaluation;
    }

    public async Task<CommercialEvaluation> SaveCommercialAsync(CommercialEvaluation evaluation, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationEvaluate, actor);
        _ = await _offers.GetByIdAsync(evaluation.OfferId, ct) ?? throw new InvalidOperationException("InvalidQuotationResponse");
        evaluation = new CommercialEvaluation
        {
            EvaluationId = string.IsNullOrWhiteSpace(evaluation.EvaluationId) ? Guid.NewGuid().ToString("N") : evaluation.EvaluationId,
            OfferId = evaluation.OfferId,
            Risk = evaluation.Risk,
            Score = PrecisionPolicy.RoundPorcentaje(evaluation.Score),
            PaymentAssessment = evaluation.PaymentAssessment ?? string.Empty,
            WarrantyAssessment = evaluation.WarrantyAssessment ?? string.Empty,
            Comments = evaluation.Comments ?? string.Empty,
            EvaluatedBy = actor,
            EvaluatedUtc = DateTime.UtcNow
        };
        await _commercial.SaveAsync(evaluation, ct);
        await Audit("CommercialEvaluation", evaluation.EvaluationId, "Save", actor, null, evaluation.Risk.ToString(), ct);
        return evaluation;
    }

    public Task<SupplierOffer?> GetAsync(SupplierOfferId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationView, "system");
        return _offers.GetByIdAsync(id, ct);
    }

    public Task<List<SupplierOffer>> ListByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationView, "system");
        return _offers.GetByRfqAsync(rfqId, ct);
    }

    private async Task<SupplierOffer> LoadWritable(SupplierOfferId id, string expectedRowVersion, CancellationToken ct)
    {
        var offer = await _offers.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("InvalidQuotationResponse");
        if (!string.Equals(offer.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return offer;
    }

    private void Ensure(string permission, string actor)
    {
        if (_permissions.Has(permission)) return;
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private Task Audit(string entity, string id, string action, string actor, string? prev, string? next, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = entity, EntityId = id, Action = action,
            Actor = actor, UtcTimestamp = DateTime.UtcNow, PreviousState = prev, NewState = next, Result = "Ok"
        }, ct);

    private static void ThrowIf(IEnumerable<ProcurementValidationIssue> issues)
    {
        var err = issues.FirstOrDefault(i => i.Severity == ProcurementValidationSeverity.Error);
        if (err is not null) throw new InvalidOperationException($"{err.Code}:{err.Message}");
    }
}
