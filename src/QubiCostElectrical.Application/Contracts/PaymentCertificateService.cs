using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IPaymentCertificateService
{
    // Lifecycle
    Task<PaymentCertificate> CreateAsync(PaymentCertificate draft, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> UpdateDraftAsync(PaymentCertificate certificate, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> SubmitAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> ReviewAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> ObserveAsync(PaymentCertificateId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<PaymentCertificate> CorrectAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    /// <summary>
    /// Envía un certificado de EnRevision a PendienteAprobacion.
    /// Luego de revisar, el revisor lo envía para que otro usuario (aprobador)
    /// pueda aprobarlo. Transición: EnRevision → PendienteAprobacion.
    /// </summary>
    Task<PaymentCertificate> SendToApprovalAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> ApproveAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> RejectAsync(PaymentCertificateId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<PaymentCertificate> InvoiceAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> MarkPaidAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> CloseAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> VoidAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default);

    // Lines
    Task<PaymentCertificate> AddLineAsync(PaymentCertificateId id, PaymentCertificateLine line, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> AddLinesAsync(PaymentCertificateId id, List<PaymentCertificateLine> lines, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentCertificate> RemoveLineAsync(PaymentCertificateId id, PaymentCertificateLineId lineId, string expectedRowVersion, string actor, CancellationToken ct = default);

    // Calculations
    Task<ValuationCalculationResult> RecalculateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<PaymentCertificate> ApplyCalculationAsync(PaymentCertificateId id, ValuationCalculationResult result, string expectedRowVersion, string actor, CancellationToken ct = default);

    // Advances & Retentions
    Task<ContractAdvance> CreateAdvanceAsync(ContractAdvance advance, string actor, CancellationToken ct = default);
    Task<ContractRetention> CreateRetentionAsync(ContractRetention retention, string actor, CancellationToken ct = default);
    Task<RetentionRelease> ReleaseRetentionAsync(ContractRetentionId retentionId, decimal amount, string authorizedBy, string comment, CancellationToken ct = default);

    // Observations
    Task<PaymentObservation> AddObservationAsync(PaymentObservation observation, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PaymentObservation> RespondObservationAsync(PaymentObservationId id, string response, string responder, CancellationToken ct = default);

    // Revision comparison
    Task<PaymentCertificateRevisionComparison> CompareRevisionsAsync(PaymentCertificateRevisionId v1Id, PaymentCertificateRevisionId v2Id, CancellationToken ct = default);

    // Integrity
    Task<bool> VerifyIntegrityAsync(PaymentCertificateId id, CancellationToken ct = default);

    // Query
    Task<PaymentCertificate?> GetAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<List<PaymentCertificate>> SearchAsync(string projectPath, string? text, PaymentCertificateStatus? status, int skip, int take, CancellationToken ct = default);
    Task<List<PaymentCertificate>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<PaymentCertificateAuditEvent>> GetAuditLogAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<List<PaymentCertificateRevision>> GetRevisionsAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<List<ContractAdvance>> GetAdvancesByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ContractRetention>> GetRetentionsByContractAsync(ContractId contractId, CancellationToken ct = default);
}

public sealed record PaymentCertificateRevisionComparison(
    PaymentCertificateRevision Revision1,
    PaymentCertificateRevision Revision2,
    List<string> Differences,
    bool IsIdentical
);

public sealed partial class PaymentCertificateService : IPaymentCertificateService
{
    private readonly IPaymentCertificateRepository _certRepo;
    private readonly IPaymentCertificateLineRepository _lineRepo;
    private readonly IPaymentCertificateRevisionRepository _revisionRepo;
    private readonly IPaymentCertificateAuditRepository _auditRepo;
    private readonly IContractAdvanceRepository _advanceRepo;
    private readonly IAdvanceAmortizationRepository _amortRepo;
    private readonly IContractRetentionRepository _retentionRepo;
    private readonly IRetentionApplicationRepository _retAppRepo;
    private readonly IRetentionReleaseRepository _retReleaseRepo;
    private readonly IPaymentObservationRepository _observationRepo;
    private readonly IContractRepository _contractRepo;
    private readonly IPermissionService _permissions;
    private readonly SoDPolicyConfig _sod;

    public PaymentCertificateService(
        IPaymentCertificateRepository certRepo,
        IPaymentCertificateLineRepository lineRepo,
        IPaymentCertificateRevisionRepository revisionRepo,
        IPaymentCertificateAuditRepository auditRepo,
        IContractAdvanceRepository advanceRepo,
        IAdvanceAmortizationRepository amortRepo,
        IContractRetentionRepository retentionRepo,
        IRetentionApplicationRepository retAppRepo,
        IRetentionReleaseRepository retReleaseRepo,
        IPaymentObservationRepository observationRepo,
        IContractRepository contractRepo,
        IPermissionService permissions,
        SoDPolicyConfig? sod = null)
    {
        _certRepo = certRepo;
        _lineRepo = lineRepo;
        _revisionRepo = revisionRepo;
        _auditRepo = auditRepo;
        _advanceRepo = advanceRepo;
        _amortRepo = amortRepo;
        _retentionRepo = retentionRepo;
        _retAppRepo = retAppRepo;
        _retReleaseRepo = retReleaseRepo;
        _observationRepo = observationRepo;
        _contractRepo = contractRepo;
        _permissions = permissions;
        _sod = sod ?? new SoDPolicyConfig();
    }

    private void Ensure(bool canDo, string action)
    {
        if (!canDo)
            throw new UnauthorizedAccessException($"No tiene permisos para la acción: {action}.");
    }

    private async Task<PaymentCertificate> LoadWritable(PaymentCertificateId id, string expectedRowVersion, CancellationToken ct)
    {
        var cert = await _certRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Estado de pago no encontrado.");

        if (!string.Equals(cert.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El estado de pago ha sido modificado por otro usuario.");

        var lines = await _lineRepo.GetByCertificateAsync(id, ct);
        cert.Lines.Clear();
        cert.Lines.AddRange(lines);

        var observations = await _observationRepo.GetByCertificateAsync(id, ct);
        cert.Observations.Clear();
        cert.Observations.AddRange(observations);

        var amortizations = await _amortRepo.GetByCertificateAsync(id, ct);
        cert.Amortizations.Clear();
        cert.Amortizations.AddRange(amortizations);

        var retApps = await _retAppRepo.GetByCertificateAsync(id, ct);
        cert.RetentionApplications.Clear();
        cert.RetentionApplications.AddRange(retApps);

        return cert;
    }

    private async Task SaveStateAsync(PaymentCertificate cert, string action, string actor, string details, CancellationToken ct)
    {
        cert.RowVersion = Guid.NewGuid().ToString("N");
        cert.ModifiedUtc = DateTime.UtcNow;
        cert.ModifiedBy = actor;

        // Recompute hash after modification
        cert.Hash = PaymentCertificate.ComputeHash(JsonSerializer.Serialize(cert));

        await _certRepo.SaveAsync(cert, ct);

        var audit = new PaymentCertificateAuditEvent
        {
            PaymentCertificateId = cert.Id,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Action = action, Details = details })
        };
        await _auditRepo.RecordAsync(audit, ct);
    }

    private async Task CreateRevisionSnapshot(PaymentCertificate cert, string actor, string comment, CancellationToken ct)
    {
        var snapshotJson = JsonSerializer.Serialize(cert);
        var revision = new PaymentCertificateRevision
        {
            Id = PaymentCertificateRevisionId.New(),
            PaymentCertificateId = cert.Id,
            RevisionNumber = cert.RevisionNumber,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            SnapshotJson = snapshotJson,
            Hash = PaymentCertificate.ComputeHash(snapshotJson),
            Comment = comment
        };
        await _revisionRepo.SaveAsync(revision, ct);
    }

    public async Task<PaymentCertificate> CreateAsync(PaymentCertificate draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateCreate, "Create");

        if (_sod.DisabledUserCannotAct)
            Ensure(_permissions.CanPaymentCertificateCreate, "Create:DisabledUser");

        var contract = await _contractRepo.GetByIdAsync(draft.ContractId, ct);
        if (contract == null)
            throw new InvalidOperationException("El contrato especificado no existe.");
        if (_sod.InactiveContractBlocksCreation && contract.Status != ContractStatus.Vigente && contract.Status != ContractStatus.Aprobado)
            throw new InvalidOperationException("El contrato no está vigente o aprobado. No se puede crear un estado de pago.");

        var nextNum = await _certRepo.GetNextNumberAsync(contract.ProjectPath, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"EP-{nextNum:D5}" : draft.VisibleNumber;

        var cert = new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = draft.ContractId,
            VisibleNumber = visibleNum,
            RevisionNumber = 1,
            PeriodStart = draft.PeriodStart,
            PeriodEnd = draft.PeriodEnd,
            CutOffDate = draft.CutOffDate,
            PresentationDate = DateTime.UtcNow,
            Currency = draft.Currency,
            ExchangeRate = draft.ExchangeRate > 0 ? draft.ExchangeRate : 1.0m,
            Status = PaymentCertificateStatus.Borrador,
            Presenter = draft.Presenter,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        var errors = PaymentCertificateValidation.Validate(cert);
        if (errors.Any())
            throw new InvalidOperationException($"Errores de validación: {string.Join("; ", errors)}");

        // Compute integrity hash before saving
        cert.Hash = PaymentCertificate.ComputeHash(JsonSerializer.Serialize(cert));

        await _certRepo.SaveAsync(cert, ct);
        await CreateRevisionSnapshot(cert, actor, "Creación inicial", ct);

        var audit = new PaymentCertificateAuditEvent
        {
            PaymentCertificateId = cert.Id,
            Action = "CREAR",
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Details = "Creación de estado de pago", Numero = visibleNum })
        };
        await _auditRepo.RecordAsync(audit, ct);

        return cert;
    }

    public async Task<PaymentCertificate> UpdateDraftAsync(PaymentCertificate certificate, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateEditDraft, "EditDraft");
        var existing = await LoadWritable(certificate.Id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.IsEditable(existing.Status))
            throw new InvalidOperationException($"No se puede editar un estado de pago en estado {existing.Status}.");

        existing.PeriodStart = certificate.PeriodStart;
        existing.PeriodEnd = certificate.PeriodEnd;
        existing.CutOffDate = certificate.CutOffDate;
        existing.Currency = certificate.Currency;
        existing.ExchangeRate = certificate.ExchangeRate;
        existing.Comment = certificate.Comment;

        var errors = PaymentCertificateValidation.Validate(existing);
        if (errors.Any())
            throw new InvalidOperationException($"Errores de validación: {string.Join("; ", errors)}");

        await SaveStateAsync(existing, "EDITAR", actor, "Edición de borrador", ct);
        return existing;
    }

    public async Task<PaymentCertificate> SubmitAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateSubmit, "Submit");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Presentado);

        if (!cert.Lines.Any())
            throw new InvalidOperationException("No se puede presentar un estado de pago sin líneas de valorización.");

        cert.Status = PaymentCertificateStatus.Presentado;
        cert.PresentationDate = DateTime.UtcNow;
        cert.RevisionNumber++;

        await SaveStateAsync(cert, "PRESENTAR", actor, "Estado de pago presentado", ct);
        await CreateRevisionSnapshot(cert, actor, "Presentación del estado de pago", ct);
        return cert;
    }

    public async Task<PaymentCertificate> ReviewAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateReview, "Review");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.EnRevision);
        cert.Status = PaymentCertificateStatus.EnRevision;
        cert.Revisor = actor;

        await SaveStateAsync(cert, "EN_REVISION", actor, "Inicio de revisión", ct);
        return cert;
    }

    public async Task<PaymentCertificate> SendToApprovalAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateApprove, "SendToApproval");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.PendienteAprobacion);
        cert.Status = PaymentCertificateStatus.PendienteAprobacion;

        await SaveStateAsync(cert, "ENVIAR_APROBACION", actor, "Enviado para aprobación", ct);
        return cert;
    }

    public async Task<PaymentCertificate> ObserveAsync(PaymentCertificateId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateObserve, "Observe");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Observado);
        cert.Status = PaymentCertificateStatus.Observado;
        cert.Comment = comment;

        await SaveStateAsync(cert, "OBSERVAR", actor, $"Observación: {comment}", ct);
        return cert;
    }

    public async Task<PaymentCertificate> CorrectAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateCorrect, "Correct");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.CanBeCorrected(cert.Status))
            throw new InvalidOperationException($"No se puede corregir un estado de pago en estado {cert.Status}.");

        // Create a new revision before modifying
        await CreateRevisionSnapshot(cert, actor, "Snapshot pre-corrección", ct);

        cert.Status = PaymentCertificateStatus.Corregido;
        cert.RevisionNumber++;

        await SaveStateAsync(cert, "CORREGIR", actor, "Corrección del estado de pago", ct);
        return cert;
    }

    public async Task<PaymentCertificate> ApproveAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateApprove, "Approve");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Aprobado);

        if (_sod.CreatorCannotApprove && string.Equals(cert.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("SoD: El creador del estado de pago no puede aprobarlo.");

        if (_sod.LastEditorCannotApprove && string.Equals(cert.ModifiedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("SoD: El último editor no puede aprobar el estado de pago.");

        if (_sod.PresenterCannotApprove && string.Equals(cert.Presenter, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("SoD: El presentador no puede aprobar el estado de pago.");

        // Verify integrity before approving
        var integrityOk = await VerifyIntegrityAsync(id, ct);
        if (!integrityOk)
            throw new InvalidOperationException("La integridad del estado de pago es inválida. No se puede aprobar.");

        cert.Status = PaymentCertificateStatus.Aprobado;
        cert.Aprobador = actor;
        cert.ApprovedAtUtc = DateTime.UtcNow;

        await SaveStateAsync(cert, "APROBAR", actor, "Estado de pago aprobado", ct);
        await CreateRevisionSnapshot(cert, actor, "Aprobación del estado de pago", ct);
        return cert;
    }

    public async Task<PaymentCertificate> RejectAsync(PaymentCertificateId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateReject, "Reject");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Rechazado);
        cert.Status = PaymentCertificateStatus.Rechazado;
        cert.Comment = comment;

        await SaveStateAsync(cert, "RECHAZAR", actor, $"Rechazado: {comment}", ct);
        return cert;
    }

    public async Task<PaymentCertificate> InvoiceAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateInvoice, "Invoice");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Facturado);
        cert.Status = PaymentCertificateStatus.Facturado;

        await SaveStateAsync(cert, "FACTURAR", actor, "Facturación informativa", ct);
        return cert;
    }

    public async Task<PaymentCertificate> MarkPaidAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateMarkPaid, "MarkPaid");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Pagado);
        cert.Status = PaymentCertificateStatus.Pagado;

        await SaveStateAsync(cert, "PAGAR", actor, "Pago informativo registrado", ct);
        return cert;
    }

    public async Task<PaymentCertificate> CloseAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateClose, "Close");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Cerrado);
        cert.Status = PaymentCertificateStatus.Cerrado;

        await SaveStateAsync(cert, "CERRAR", actor, "Estado de pago cerrado", ct);
        return cert;
    }

    public async Task<PaymentCertificate> VoidAsync(PaymentCertificateId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateVoid, "Void");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        PaymentCertificateStatusTransitions.EnsureCanTransition(cert.Status, PaymentCertificateStatus.Anulado);
        cert.Status = PaymentCertificateStatus.Anulado;

        await SaveStateAsync(cert, "ANULAR", actor, "Estado de pago anulado", ct);
        return cert;
    }

    public async Task<PaymentCertificate> AddLineAsync(PaymentCertificateId id, PaymentCertificateLine line, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateEditDraft, "AddLine");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.IsEditable(cert.Status))
            throw new InvalidOperationException("No se pueden añadir líneas a un estado de pago no editable.");

        var lineErrors = PaymentCertificateValidation.ValidateLine(line);
        if (lineErrors.Any())
            throw new InvalidOperationException($"Errores en línea: {string.Join("; ", lineErrors)}");

        line.Id = line.Id ?? PaymentCertificateLineId.New();
        line.PaymentCertificateId = id;
        cert.Lines.Add(line);
        await _lineRepo.SaveAsync(line, ct);

        await SaveStateAsync(cert, "AÑADIR_LINEA", actor, $"Línea añadida: {line.Description}", ct);
        return cert;
    }

    public async Task<PaymentCertificate> AddLinesAsync(PaymentCertificateId id, List<PaymentCertificateLine> lines, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateEditDraft, "AddLines");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.IsEditable(cert.Status))
            throw new InvalidOperationException("No se pueden añadir líneas a un estado de pago no editable.");

        foreach (var line in lines)
        {
            var lineErrors = PaymentCertificateValidation.ValidateLine(line);
            if (lineErrors.Any())
                throw new InvalidOperationException($"Errores en línea '{line.Description}': {string.Join("; ", lineErrors)}");

            line.Id = line.Id ?? PaymentCertificateLineId.New();
            line.PaymentCertificateId = id;
        }

        cert.Lines.AddRange(lines);
        await _lineRepo.SaveRangeAsync(lines, ct);

        await SaveStateAsync(cert, "AÑADIR_LINEAS", actor, $"{lines.Count} líneas añadidas", ct);
        return cert;
    }

    public async Task<PaymentCertificate> RemoveLineAsync(PaymentCertificateId id, PaymentCertificateLineId lineId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateEditDraft, "RemoveLine");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.IsEditable(cert.Status))
            throw new InvalidOperationException("No se pueden eliminar líneas de un estado de pago no editable.");

        cert.Lines.RemoveAll(l => l.Id == lineId);
        await _lineRepo.DeleteByCertificateAsync(id, ct);

        // Re-save remaining lines
        if (cert.Lines.Any())
            await _lineRepo.SaveRangeAsync(cert.Lines, ct);

        await SaveStateAsync(cert, "ELIMINAR_LINEA", actor, "Línea eliminada", ct);
        return cert;
    }

    public async Task<ValuationCalculationResult> RecalculateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var cert = await _certRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Estado de pago no encontrado.");

        var lines = await _lineRepo.GetByCertificateAsync(id, ct);
        var amortizations = await _amortRepo.GetByCertificateAsync(id, ct);
        var retApps = await _retAppRepo.GetByCertificateAsync(id, ct);

        var contract = await _contractRepo.GetByIdAsync(cert.ContractId, ct);
        if (contract == null)
            throw new InvalidOperationException("Contrato no encontrado para el estado de pago.");

        var lineInputs = lines.Select(l => new ValuationLineInput(
            l.Id.Value,
            l.CantidadContractual,
            l.PrecioUnitarioContractual,
            l.CantidadAnterior,
            l.CantidadPeriodo
        )).ToList();

        var advances = new List<ValuationAdvanceInput>();
        var contractAdvances = await _advanceRepo.GetByContractAsync(cert.ContractId, ct);
        foreach (var adv in contractAdvances)
        {
            if (adv.Status != "Active") continue;
            advances.Add(new ValuationAdvanceInput(
                adv.Id.Value,
                adv.CurrentBalance,
                adv.AmortizationPercentage,
                0m, // fixed amount, not used with percentage rule
                "PercentageOfPeriodSubtotal"
            ));
        }

        var retentions = new List<ValuationRetentionInput>();
        var contractRetentions = await _retentionRepo.GetByContractAsync(cert.ContractId, ct);
        foreach (var ret in contractRetentions)
        {
            if (ret.Status != "Active") continue;
            decimal cumulativeBefore = 0m;
            var existing = retApps.FirstOrDefault(r => r.ContractRetentionId == ret.Id);
            if (existing != null)
                cumulativeBefore = existing.CumulativeApplied;

            retentions.Add(new ValuationRetentionInput(
                ret.Id.Value,
                ret.Percentage,
                ret.FixedAmount,
                ret.LimitAmount,
                ret.BaseType,
                cumulativeBefore
            ));
        }

        var input = new ValuationCalculationInput(
            contract.OriginalAmount,
            cert.Reajuste,
            cert.Multas,
            cert.Descuentos,
            0.19m, // default tax rate
            lineInputs,
            advances,
            retentions
        );

        return ValuationEngine.Calculate(input);
    }

    public async Task<PaymentCertificate> ApplyCalculationAsync(PaymentCertificateId id, ValuationCalculationResult result, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateEditDraft, "ApplyCalculation");
        var cert = await LoadWritable(id, expectedRowVersion, ct);

        if (!PaymentCertificateStatusTransitions.IsEditable(cert.Status))
            throw new InvalidOperationException("No se puede aplicar cálculo a un estado de pago no editable.");

        // Update lines with results
        foreach (var lineResult in result.Lines)
        {
            var line = cert.Lines.FirstOrDefault(l => l.Id.Value == lineResult.LineId);
            if (line == null) continue;

            line.CantidadAcumulada = lineResult.CantidadAcumulada;
            line.SaldoCantidad = lineResult.SaldoCantidad;
            line.MontoAnterior = lineResult.MontoAnterior;
            line.MontoPeriodo = lineResult.MontoPeriodo;
            line.MontoAcumulado = lineResult.MontoAcumulado;
            line.SaldoMonetario = lineResult.SaldoMonetario;
        }
        await _lineRepo.SaveRangeAsync(cert.Lines, ct);

        // Update amortizations
        foreach (var amortResult in result.Amortizations)
        {
            var advId = ContractAdvanceId.From(amortResult.AdvanceId);
            var advance = await _advanceRepo.GetByIdAsync(advId, ct);
            if (advance == null) continue;

            var amortization = new AdvanceAmortization
            {
                PaymentCertificateId = id,
                ContractAdvanceId = advId,
                Amount = amortResult.Amount,
                Percentage = amortResult.Percentage,
                PreAmortizationBalance = amortResult.PreAmortizationBalance,
                PostAmortizationBalance = amortResult.PostAmortizationBalance
            };
            await _amortRepo.SaveAsync(amortization, ct);

            advance.CurrentBalance = amortResult.PostAmortizationBalance;
            advance.TotalAmortized += amortResult.Amount;
            if (advance.CurrentBalance <= 0m)
                advance.Status = "FullyAmortized";
            await _advanceRepo.SaveAsync(advance, ct);
        }

        // Update retentions
        foreach (var retResult in result.Retentions)
        {
            var retId = ContractRetentionId.From(retResult.RetentionId);
            var app = new RetentionApplication
            {
                PaymentCertificateId = id,
                ContractRetentionId = retId,
                BaseAmount = retResult.BaseAmount,
                Percentage = retResult.Percentage,
                AppliedAmount = retResult.AppliedAmount,
                CumulativeApplied = retResult.CumulativeApplied,
                PendingBalance = retResult.PendingBalance
            };
            await _retAppRepo.SaveAsync(app, ct);
        }

        // Update certificate totals
        cert.SubtotalValorizado = result.SubtotalValorizado;
        cert.Reajuste = result.Reajuste;
        cert.AnticipoAmortizado = result.AnticipoAmortizado;
        cert.RetencionesAplicadas = result.RetencionesAplicadas;
        cert.Descuentos = result.Descuentos;
        cert.Multas = result.Multas;
        cert.Impuestos = result.Impuestos;
        cert.MontoBruto = result.MontoBruto;
        cert.MontoLiquido = result.MontoLiquido;
        cert.MontoAnterior = result.MontoAnterior;
        cert.MontoAcumulado = result.MontoAcumulado;
        cert.SaldoContractual = result.SaldoContractual;
        cert.SaldoPorValorizar = result.SaldoPorValorizar;
        cert.Hash = PaymentCertificate.ComputeHash(JsonSerializer.Serialize(cert));

        await SaveStateAsync(cert, "CALCULAR", actor, "Cálculo de valorización aplicado", ct);
        return cert;
    }

    public async Task<ContractAdvance> CreateAdvanceAsync(ContractAdvance advance, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanAdvanceManage, "CreateAdvance");

        var errors = PaymentCertificateValidation.ValidateAdvance(advance);
        if (errors.Any())
            throw new InvalidOperationException($"Errores en anticipo: {string.Join("; ", errors)}");

        advance.Id = advance.Id ?? ContractAdvanceId.New();
        advance.CreatedUtc = DateTime.UtcNow;
        advance.CreatedBy = actor;
        advance.InitialBalance = advance.Amount;
        advance.CurrentBalance = advance.Amount;
        advance.TotalAmortized = 0m;
        advance.Status = "Active";

        await _advanceRepo.SaveAsync(advance, ct);

        var audit = new PaymentCertificateAuditEvent
        {
            PaymentCertificateId = null!,
            Action = "CREAR_ANTICIPO",
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Details = "Anticipo creado", Monto = advance.Amount })
        };
        try { await _auditRepo.RecordAsync(audit, ct); } catch { }

        return advance;
    }

    public async Task<ContractRetention> CreateRetentionAsync(ContractRetention retention, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanRetentionManage, "CreateRetention");

        var errors = PaymentCertificateValidation.ValidateRetention(retention);
        if (errors.Any())
            throw new InvalidOperationException($"Errores en retención: {string.Join("; ", errors)}");

        retention.Id = retention.Id ?? ContractRetentionId.New();
        retention.Status = "Active";

        await _retentionRepo.SaveAsync(retention, ct);

        var audit = new PaymentCertificateAuditEvent
        {
            PaymentCertificateId = null!,
            Action = "CREAR_RETENCION",
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Details = "Retención creada", Tipo = retention.RetentionType.ToString() })
        };
        try { await _auditRepo.RecordAsync(audit, ct); } catch { }

        return retention;
    }

    public async Task<RetentionRelease> ReleaseRetentionAsync(ContractRetentionId retentionId, decimal amount, string authorizedBy, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanRetentionManage, "ReleaseRetention");

        var retention = await _retentionRepo.GetByIdAsync(retentionId, ct)
            ?? throw new KeyNotFoundException("Retención no encontrada.");

        if (retention.Status != "Active")
            throw new InvalidOperationException("La retención no está activa.");

        if (amount <= 0)
            throw new InvalidOperationException("El monto a liberar debe ser positivo.");

        var release = new RetentionRelease
        {
            ContractRetentionId = retentionId,
            ReleasedAmount = amount,
            ReleaseDate = DateTime.UtcNow,
            AuthorizedBy = authorizedBy,
            Comment = comment
        };
        await _retReleaseRepo.SaveAsync(release, ct);

        retention.Status = "Released";
        await _retentionRepo.SaveAsync(retention, ct);

        return release;
    }

    public async Task<PaymentObservation> AddObservationAsync(PaymentObservation observation, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanPaymentCertificateObserve, "AddObservation");

        var errors = PaymentCertificateValidation.ValidateObservation(observation);
        if (errors.Any())
            throw new InvalidOperationException($"Errores en observación: {string.Join("; ", errors)}");

        observation.Id = observation.Id ?? PaymentObservationId.New();
        observation.CreatedUtc = DateTime.UtcNow;
        observation.Author = actor;
        observation.Status = PaymentObservationStatus.Abierta;

        await _observationRepo.SaveAsync(observation, ct);

        return observation;
    }

    public async Task<PaymentObservation> RespondObservationAsync(PaymentObservationId id, string response, string responder, CancellationToken ct = default)
    {
        var observation = await _observationRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Observación no encontrada.");

        if (observation.Status != PaymentObservationStatus.Abierta)
            throw new InvalidOperationException("Solo se puede responder a observaciones abiertas.");

        observation.Response = response;
        observation.Responder = responder;
        observation.Status = PaymentObservationStatus.Respondida;
        observation.ResolvedUtc = DateTime.UtcNow;

        await _observationRepo.SaveAsync(observation, ct);
        return observation;
    }

    public async Task<PaymentCertificateRevisionComparison> CompareRevisionsAsync(PaymentCertificateRevisionId v1Id, PaymentCertificateRevisionId v2Id, CancellationToken ct = default)
    {
        var v1 = await _revisionRepo.GetByIdAsync(v1Id, ct)
            ?? throw new KeyNotFoundException("Revisión v1 no encontrada.");
        var v2 = await _revisionRepo.GetByIdAsync(v2Id, ct)
            ?? throw new KeyNotFoundException("Revisión v2 no encontrada.");

        var differences = new List<string>();

        if (string.Equals(v1.Hash, v2.Hash, StringComparison.OrdinalIgnoreCase))
            return new PaymentCertificateRevisionComparison(v1, v2, differences, true);

        differences.Add("Las revisiones tienen diferente hash.");
        differences.Add($"Revisión {v1.RevisionNumber}: {v1.CreatedBy} el {v1.CreatedUtc:O}");
        differences.Add($"Revisión {v2.RevisionNumber}: {v2.CreatedBy} el {v2.CreatedUtc:O}");

        try
        {
            var cert1 = JsonSerializer.Deserialize<PaymentCertificate>(v1.SnapshotJson);
            var cert2 = JsonSerializer.Deserialize<PaymentCertificate>(v2.SnapshotJson);

            if (cert1 != null && cert2 != null)
            {
                if (cert1.SubtotalValorizado != cert2.SubtotalValorizado)
                    differences.Add($"Subtotal: {cert1.SubtotalValorizado} → {cert2.SubtotalValorizado}");
                if (cert1.MontoBruto != cert2.MontoBruto)
                    differences.Add($"Monto Bruto: {cert1.MontoBruto} → {cert2.MontoBruto}");
                if (cert1.MontoLiquido != cert2.MontoLiquido)
                    differences.Add($"Monto Líquido: {cert1.MontoLiquido} → {cert2.MontoLiquido}");
                if (cert1.AnticipoAmortizado != cert2.AnticipoAmortizado)
                    differences.Add($"Anticipo Amortizado: {cert1.AnticipoAmortizado} → {cert2.AnticipoAmortizado}");
                if (cert1.RetencionesAplicadas != cert2.RetencionesAplicadas)
                    differences.Add($"Retenciones: {cert1.RetencionesAplicadas} → {cert2.RetencionesAplicadas}");
            }

            if (cert1?.Lines.Count != cert2?.Lines.Count)
                differences.Add($"Cantidad de líneas: {cert1?.Lines.Count ?? 0} → {cert2?.Lines.Count ?? 0}");
        }
        catch
        {
            differences.Add("No se pudieron deserializar los snapshots para comparación detallada.");
        }

        return new PaymentCertificateRevisionComparison(v1, v2, differences, false);
    }

    public async Task<bool> VerifyIntegrityAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var cert = await _certRepo.GetByIdAsync(id, ct);
        if (cert == null) return false;

        var lines = await _lineRepo.GetByCertificateAsync(id, ct);
        var observations = await _observationRepo.GetByCertificateAsync(id, ct);
        var amortizations = await _amortRepo.GetByCertificateAsync(id, ct);
        var retApps = await _retAppRepo.GetByCertificateAsync(id, ct);

        // Recompute expected hash from current state
        var expectedHash = PaymentCertificate.ComputeHash(JsonSerializer.Serialize(cert));

        // Check if stored hash matches
        return string.Equals(cert.Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PaymentCertificate?> GetAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var cert = await _certRepo.GetByIdAsync(id, ct);
        if (cert == null) return null;

        var lines = await _lineRepo.GetByCertificateAsync(id, ct);
        cert.Lines.Clear();
        cert.Lines.AddRange(lines);

        var observations = await _observationRepo.GetByCertificateAsync(id, ct);
        cert.Observations.Clear();
        cert.Observations.AddRange(observations);

        var amortizations = await _amortRepo.GetByCertificateAsync(id, ct);
        cert.Amortizations.Clear();
        cert.Amortizations.AddRange(amortizations);

        var retApps = await _retAppRepo.GetByCertificateAsync(id, ct);
        cert.RetentionApplications.Clear();
        cert.RetentionApplications.AddRange(retApps);

        return cert;
    }

    public Task<List<PaymentCertificate>> SearchAsync(string projectPath, string? text, PaymentCertificateStatus? status, int skip, int take, CancellationToken ct = default)
        => _certRepo.SearchAsync(projectPath, text, status, skip, take, ct);

    public Task<List<PaymentCertificate>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _certRepo.GetByContractAsync(contractId, ct);

    public Task<List<PaymentCertificateAuditEvent>> GetAuditLogAsync(PaymentCertificateId id, CancellationToken ct = default)
        => _auditRepo.GetByCertificateAsync(id, 100, ct);

    public Task<List<PaymentCertificateRevision>> GetRevisionsAsync(PaymentCertificateId id, CancellationToken ct = default)
        => _revisionRepo.GetByCertificateAsync(id, ct);

    public Task<List<ContractAdvance>> GetAdvancesByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _advanceRepo.GetByContractAsync(contractId, ct);

    public Task<List<ContractRetention>> GetRetentionsByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _retentionRepo.GetByContractAsync(contractId, ct);
}
