using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractService
{
    Task<Contract> CreateAsync(Contract draft, string actor, CancellationToken ct = default);
    Task<Contract> UpdateDraftAsync(Contract contract, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> SubmitForReviewAsync(ContractId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> ApproveAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> RejectAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> SuspendAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> ReactivateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> InitiateCloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> CloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> TerminateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<Contract> RescindAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    
    // Sub-collections
    Task<Contract> AddPartyAsync(ContractId id, ContractParty party, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> RemovePartyAsync(ContractId id, string partyId, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> AddMilestoneAsync(ContractId id, ContractMilestone milestone, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> RemoveMilestoneAsync(ContractId id, string milestoneId, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> AddScopeLinkAsync(ContractId id, ContractScopeLink link, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> RemoveScopeLinkAsync(ContractId id, string linkId, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> AddDocumentAsync(ContractId id, ContractDocument document, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<Contract> RemoveDocumentAsync(ContractId id, string documentId, string expectedRowVersion, string actor, CancellationToken ct = default);

    // Versioning & Integrity
    Task<List<ContractVersion>> GetVersionsAsync(ContractId contractId, CancellationToken ct = default);
    Task<ContractVersion> VerifyIntegrityAsync(ContractVersionId versionId, CancellationToken ct = default);
    Task<string> CompareVersionsAsync(ContractVersionId v1Id, ContractVersionId v2Id, CancellationToken ct = default);
    
    // Query
    Task<Contract?> GetAsync(ContractId id, CancellationToken ct = default);
    Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default);
    Task<List<ContractAuditEvent>> GetAuditLogAsync(ContractId contractId, CancellationToken ct = default);
}

public sealed partial class ContractService : IContractService
{
    private readonly IContractRepository _contractRepo;
    private readonly IContractVersionRepository _versionRepo;
    private readonly IContractPartyRepository _partyRepo;
    private readonly IContractMilestoneRepository _milestoneRepo;
    private readonly IContractScopeLinkRepository _scopeRepo;
    private readonly IContractDocumentRepository _documentRepo;
    private readonly IContractAuditRepository _auditRepo;
    private readonly IPermissionService _permissions;
    private readonly SoDPolicyConfig _sod;

    public ContractService(
        IContractRepository contractRepo,
        IContractVersionRepository versionRepo,
        IContractPartyRepository partyRepo,
        IContractMilestoneRepository milestoneRepo,
        IContractScopeLinkRepository scopeRepo,
        IContractDocumentRepository documentRepo,
        IContractAuditRepository auditRepo,
        IPermissionService permissions,
        SoDPolicyConfig? sod = null)
    {
        _contractRepo = contractRepo;
        _versionRepo = versionRepo;
        _partyRepo = partyRepo;
        _milestoneRepo = milestoneRepo;
        _scopeRepo = scopeRepo;
        _documentRepo = documentRepo;
        _auditRepo = auditRepo;
        _permissions = permissions;
        _sod = sod ?? new SoDPolicyConfig();
    }

    private void Ensure(bool canDo, string action)
    {
        if (!canDo)
            throw new UnauthorizedAccessException($"No tiene permisos para la acción contractual: {action}.");
    }

    private async Task<Contract> LoadWritable(ContractId id, string expectedRowVersion, CancellationToken ct)
    {
        var contract = await _contractRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Contrato no encontrado.");

        if (!string.Equals(contract.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El contrato ha sido modificado por otro usuario.");

        var parties = await _partyRepo.GetByContractAsync(id, ct);
        contract.Parties.Clear();
        contract.Parties.AddRange(parties);

        var milestones = await _milestoneRepo.GetByContractAsync(id, ct);
        contract.Milestones.Clear();
        contract.Milestones.AddRange(milestones);

        var scope = await _scopeRepo.GetByContractAsync(id, ct);
        contract.ScopeLinks.Clear();
        contract.ScopeLinks.AddRange(scope);

        var docs = await _documentRepo.GetByContractAsync(id, ct);
        contract.Documents.Clear();
        contract.Documents.AddRange(docs);

        return contract;
    }

    private async Task SaveContractStateAsync(Contract contract, string action, string actor, string details, CancellationToken ct)
    {
        contract.RowVersion = Guid.NewGuid().ToString("N");
        contract.ModifiedUtc = DateTime.UtcNow;
        contract.ModifiedBy = actor;

        await _contractRepo.SaveAsync(contract, ct);

        var audit = new ContractAuditEvent
        {
            ContractId = contract.Id,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Action = action, Details = details, Date = DateTime.UtcNow })
        };
        await _auditRepo.RecordAsync(audit, ct);
    }

    public async Task<Contract> CreateAsync(Contract draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractCreate, "Create");
        
        var id = draft.Id ?? ContractId.New();
        var nextNum = await _contractRepo.GetNextNumberAsync(draft.ProjectPath, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"CON-{nextNum:D5}" : draft.VisibleNumber;

        var contract = new Contract
        {
            Id = id,
            VisibleNumber = visibleNum,
            ProjectPath = draft.ProjectPath,
            Type = draft.Type,
            Title = draft.Title,
            Description = draft.Description,
            ClientName = draft.ClientName,
            ContractorName = draft.ContractorName,
            SubcontractorOrSupplierName = draft.SubcontractorOrSupplierName,
            ContractManager = draft.ContractManager,
            CurrencyCode = draft.CurrencyCode,
            OriginalAmount = draft.OriginalAmount,
            TaxAmount = draft.TaxAmount,
            SignDate = draft.SignDate,
            StartDate = draft.StartDate,
            EndDate = draft.EndDate,
            ContractualDurationDays = draft.ContractualDurationDays,
            ScopeDescription = draft.ScopeDescription,
            PaymentConditions = draft.PaymentConditions,
            AdvancePercentage = draft.AdvancePercentage,
            RetentionPercentage = draft.RetentionPercentage,
            GuaranteesRequired = draft.GuaranteesRequired,
            Status = ContractStatus.Borrador,
            Version = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            Observations = draft.Observations
        };

        var errors = ContractValidation.Validate(contract);
        if (errors.Any())
            throw new InvalidOperationException($"Errores de validación: {string.Join(", ", errors)}");

        await _contractRepo.SaveAsync(contract, ct);

        var snapshot = JsonSerializer.Serialize(contract);
        var version = new ContractVersion
        {
            Id = ContractVersionId.New(),
            ContractId = id,
            VersionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            SnapshotJson = snapshot,
            Hash = ContractVersion.ComputeHash(snapshot),
            IsApproved = false,
            Comments = "Versión inicial en borrador"
        };
        await _versionRepo.SaveAsync(version, ct);

        var audit = new ContractAuditEvent
        {
            ContractId = id,
            Action = "CREAR",
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Details = "Creación de contrato", Version = 1 })
        };
        await _auditRepo.RecordAsync(audit, ct);

        return contract;
    }

    public async Task<Contract> UpdateDraftAsync(Contract contract, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditDraft");
        var existing = await LoadWritable(contract.Id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede editar un contrato aprobado, vigente o cerrado.");

        existing.Title = contract.Title;
        existing.Description = contract.Description;
        existing.ClientName = contract.ClientName;
        existing.ContractorName = contract.ContractorName;
        existing.SubcontractorOrSupplierName = contract.SubcontractorOrSupplierName;
        existing.ContractManager = contract.ContractManager;
        existing.CurrencyCode = contract.CurrencyCode;
        existing.OriginalAmount = contract.OriginalAmount;
        existing.TaxAmount = contract.TaxAmount;
        existing.SignDate = contract.SignDate;
        existing.StartDate = contract.StartDate;
        existing.EndDate = contract.EndDate;
        existing.ContractualDurationDays = contract.ContractualDurationDays;
        existing.ScopeDescription = contract.ScopeDescription;
        existing.PaymentConditions = contract.PaymentConditions;
        existing.AdvancePercentage = contract.AdvancePercentage;
        existing.RetentionPercentage = contract.RetentionPercentage;
        existing.GuaranteesRequired = contract.GuaranteesRequired;
        existing.Observations = contract.Observations;

        var errors = ContractValidation.Validate(existing);
        if (errors.Any())
            throw new InvalidOperationException($"Errores de validación: {string.Join(", ", errors)}");

        await SaveContractStateAsync(existing, "EDITAR", actor, "Edición de borrador", ct);
        return existing;
    }

    public async Task<Contract> SubmitForReviewAsync(ContractId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractSubmit, "Submit");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.Borrador)
            throw new InvalidOperationException("Solo los contratos en estado Borrador se pueden enviar a revisión.");

        contract.Status = ContractStatus.PendienteRevision;
        await SaveContractStateAsync(contract, "ENVIAR_REVISION", actor, "Enviado a revisión", ct);
        return contract;
    }

    public async Task<Contract> ApproveAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractApprove, "Approve");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovalRequiresPriorReview && contract.Status != ContractStatus.PendienteRevision)
            throw new InvalidOperationException("La aprobación requiere que el contrato esté en revisión.");

        if (_sod.CreatorCannotApprove && string.Equals(contract.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El creador del contrato no puede aprobarlo.");

        if (_sod.LastEditorCannotApprove && string.Equals(contract.ModifiedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El último editor no puede aprobar el contrato.");

        contract.Status = ContractStatus.Vigente;
        
        await SaveContractStateAsync(contract, "APROBAR", actor, $"Aprobación: {comment}", ct);

        var versions = await _versionRepo.GetByContractAsync(id, ct);
        var latestVersion = versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (latestVersion != null)
        {
            latestVersion.IsApproved = true;
            latestVersion.Comments = $"Aprobado por {actor}: {comment}";
            latestVersion.SnapshotJson = JsonSerializer.Serialize(contract);
            latestVersion.Hash = ContractVersion.ComputeHash(latestVersion.SnapshotJson);
            await _versionRepo.SaveAsync(latestVersion, ct);
        }

        return contract;
    }

    public async Task<Contract> RejectAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractReject, "Reject");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.PendienteRevision)
            throw new InvalidOperationException("Solo se pueden rechazar contratos pendientes de revisión.");

        contract.Status = ContractStatus.Borrador;
        await SaveContractStateAsync(contract, "RECHAZAR", actor, $"Rechazado: {comment}", ct);
        return contract;
    }

    public async Task<Contract> SuspendAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractSuspend, "Suspend");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.Vigente)
            throw new InvalidOperationException("Solo contratos vigentes pueden ser suspendidos.");

        contract.Status = ContractStatus.Suspendido;
        await SaveContractStateAsync(contract, "SUSPENDER", actor, $"Suspendido: {comment}", ct);
        return contract;
    }

    public async Task<Contract> ReactivateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractSuspend, "Reactivate");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.Suspendido)
            throw new InvalidOperationException("Solo contratos suspendidos pueden reactivarse.");

        contract.Status = ContractStatus.Vigente;
        await SaveContractStateAsync(contract, "ACTIVAR", actor, $"Reactivación: {comment}", ct);
        return contract;
    }

    public async Task<Contract> InitiateCloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractClose, "InitiateClose");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.Vigente)
            throw new InvalidOperationException("Solo contratos vigentes pueden iniciar el proceso de cierre.");

        contract.Status = ContractStatus.EnCierre;
        await SaveContractStateAsync(contract, "INICIAR_CIERRE", actor, $"Inicio de cierre: {comment}", ct);
        return contract;
    }

    public async Task<Contract> CloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractClose, "Close");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (contract.Status != ContractStatus.EnCierre && contract.Status != ContractStatus.Vigente)
            throw new InvalidOperationException("Solo contratos vigentes o en cierre se pueden cerrar.");

        contract.Status = ContractStatus.Cerrado;
        await SaveContractStateAsync(contract, "CERRAR", actor, $"Cerrado: {comment}", ct);
        return contract;
    }

    public async Task<Contract> TerminateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractTerminate, "Terminate");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        contract.Status = ContractStatus.Terminado;
        await SaveContractStateAsync(contract, "TERMINAR", actor, $"Terminado: {comment}", ct);
        return contract;
    }

    public async Task<Contract> RescindAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractTerminate, "Rescind");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        contract.Status = ContractStatus.Rescindido;
        await SaveContractStateAsync(contract, "RESCINDIR", actor, $"Rescindido: {comment}", ct);
        return contract;
    }
}
