using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public sealed class ContractService : IContractService
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

    public ContractService(IContractRepository contractRepo, IContractVersionRepository versionRepo, IContractPartyRepository partyRepo, IContractMilestoneRepository milestoneRepo, IContractScopeLinkRepository scopeRepo, IContractDocumentRepository documentRepo, IContractAuditRepository auditRepo, IPermissionService permissions, SoDPolicyConfig? sod = null)
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
        {
            throw new UnauthorizedAccessException("No tiene permisos para la acción contractual: " + action + ".");
        }
    }

    private async Task<Contract> LoadWritable(ContractId id, string expectedRowVersion, CancellationToken ct)
    {
        Contract contract = (await _contractRepo.GetByIdAsync(id, ct)) ?? throw new KeyNotFoundException("Contrato no encontrado.");
        if (!string.Equals(contract.RowVersion, expectedRowVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Concurrencia: El contrato ha sido modificado por otro usuario.");
        }
        List<ContractParty> collection = await _partyRepo.GetByContractAsync(id, ct);
        contract.Parties.Clear();
        contract.Parties.AddRange(collection);
        List<ContractMilestone> collection2 = await _milestoneRepo.GetByContractAsync(id, ct);
        contract.Milestones.Clear();
        contract.Milestones.AddRange(collection2);
        List<ContractScopeLink> collection3 = await _scopeRepo.GetByContractAsync(id, ct);
        contract.ScopeLinks.Clear();
        contract.ScopeLinks.AddRange(collection3);
        List<ContractDocument> collection4 = await _documentRepo.GetByContractAsync(id, ct);
        contract.Documents.Clear();
        contract.Documents.AddRange(collection4);
        return contract;
    }

    private async Task SaveContractStateAsync(Contract contract, string action, string actor, string details, CancellationToken ct)
    {
        contract.RowVersion = Guid.NewGuid().ToString("N");
        contract.ModifiedUtc = DateTime.UtcNow;
        contract.ModifiedBy = actor;
        await _contractRepo.SaveAsync(contract, ct);
        ContractAuditEvent evt = new ContractAuditEvent
        {
            ContractId = contract.Id,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                Action = action,
                Details = details,
                Date = DateTime.UtcNow
            })
        };
        await _auditRepo.RecordAsync(evt, ct);
    }

    public async Task<Contract> CreateAsync(Contract draft, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractCreate, "Create");
        ContractId id = draft.Id ?? ContractId.New();
        int value = await _contractRepo.GetNextNumberAsync(draft.ProjectPath, ct);
        string visibleNumber = (string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"CON-{value:D5}" : draft.VisibleNumber);
        Contract contract = new Contract
        {
            Id = id,
            VisibleNumber = visibleNumber,
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
        List<string> list = ContractValidation.Validate(contract);
        if (list.Any())
        {
            throw new InvalidOperationException("Errores de validación: " + string.Join(", ", list));
        }
        await _contractRepo.SaveAsync(contract, ct);
        string snapshotJson = JsonSerializer.Serialize(contract);
        ContractVersion version = new ContractVersion
        {
            Id = ContractVersionId.New(),
            ContractId = id,
            VersionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            SnapshotJson = snapshotJson,
            Hash = ContractVersion.ComputeHash(snapshotJson),
            IsApproved = false,
            Comments = "Versión inicial en borrador"
        };
        await _versionRepo.SaveAsync(version, ct);
        ContractAuditEvent evt = new ContractAuditEvent
        {
            ContractId = id,
            Action = "CREAR",
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                Details = "Creación de contrato",
                Version = 1
            })
        };
        await _auditRepo.RecordAsync(evt, ct);
        return contract;
    }

    public async Task<Contract> UpdateDraftAsync(Contract contract, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditDraft");
        Contract existing = await LoadWritable(contract.Id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = existing.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede editar un contrato aprobado, vigente o cerrado.");
        }
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
        List<string> list = ContractValidation.Validate(existing);
        if (list.Any())
        {
            throw new InvalidOperationException("Errores de validación: " + string.Join(", ", list));
        }
        await SaveContractStateAsync(existing, "EDITAR", actor, "Edición de borrador", ct);
        return existing;
    }

    public async Task<Contract> SubmitForReviewAsync(ContractId id, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractSubmit, "Submit");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.Borrador)
        {
            throw new InvalidOperationException("Solo los contratos en estado Borrador se pueden enviar a revisión.");
        }
        contract.Status = ContractStatus.PendienteRevision;
        await SaveContractStateAsync(contract, "ENVIAR_REVISION", actor, "Enviado a revisión", ct);
        return contract;
    }

    public async Task<Contract> ApproveAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractApprove, "Approve");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (_sod.ApprovalRequiresPriorReview && contract.Status != ContractStatus.PendienteRevision)
        {
            throw new InvalidOperationException("La aprobación requiere que el contrato esté en revisión.");
        }
        if (_sod.CreatorCannotApprove && string.Equals(contract.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: El creador del contrato no puede aprobarlo.");
        }
        if (_sod.LastEditorCannotApprove && string.Equals(contract.ModifiedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: El último editor no puede aprobar el contrato.");
        }
        contract.Status = ContractStatus.Vigente;
        await SaveContractStateAsync(contract, "APROBAR", actor, "Aprobación: " + comment, ct);
        ContractVersion? contractVersion = (await _versionRepo.GetByContractAsync(id, ct)).OrderByDescending((ContractVersion v) => v.VersionNumber).FirstOrDefault();
        if (contractVersion != null)
        {
            contractVersion.IsApproved = true;
            contractVersion.Comments = "Aprobado por " + actor + ": " + comment;
            contractVersion.SnapshotJson = JsonSerializer.Serialize(contract);
            contractVersion.Hash = ContractVersion.ComputeHash(contractVersion.SnapshotJson);
            await _versionRepo.SaveAsync(contractVersion, ct);
        }
        return contract;
    }

    public async Task<Contract> RejectAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractReject, "Reject");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.PendienteRevision)
        {
            throw new InvalidOperationException("Solo se pueden rechazar contratos pendientes de revisión.");
        }
        contract.Status = ContractStatus.Borrador;
        await SaveContractStateAsync(contract, "RECHAZAR", actor, "Rechazado: " + comment, ct);
        return contract;
    }

    public async Task<Contract> SuspendAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractSuspend, "Suspend");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.Vigente)
        {
            throw new InvalidOperationException("Solo contratos vigentes pueden ser suspendidos.");
        }
        contract.Status = ContractStatus.Suspendido;
        await SaveContractStateAsync(contract, "SUSPENDER", actor, "Suspendido: " + comment, ct);
        return contract;
    }

    public async Task<Contract> ReactivateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractSuspend, "Reactivate");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.Suspendido)
        {
            throw new InvalidOperationException("Solo contratos suspendidos pueden reactivarse.");
        }
        contract.Status = ContractStatus.Vigente;
        await SaveContractStateAsync(contract, "ACTIVAR", actor, "Reactivación: " + comment, ct);
        return contract;
    }

    public async Task<Contract> InitiateCloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractClose, "InitiateClose");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.Vigente)
        {
            throw new InvalidOperationException("Solo contratos vigentes pueden iniciar el proceso de cierre.");
        }
        contract.Status = ContractStatus.EnCierre;
        await SaveContractStateAsync(contract, "INICIAR_CIERRE", actor, "Inicio de cierre: " + comment, ct);
        return contract;
    }

    public async Task<Contract> CloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractClose, "Close");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        if (contract.Status != ContractStatus.EnCierre && contract.Status != ContractStatus.Vigente)
        {
            throw new InvalidOperationException("Solo contratos vigentes o en cierre se pueden cerrar.");
        }
        contract.Status = ContractStatus.Cerrado;
        await SaveContractStateAsync(contract, "CERRAR", actor, "Cerrado: " + comment, ct);
        return contract;
    }

    public async Task<Contract> TerminateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractTerminate, "Terminate");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        contract.Status = ContractStatus.Terminado;
        await SaveContractStateAsync(contract, "TERMINAR", actor, "Terminado: " + comment, ct);
        return contract;
    }

    public async Task<Contract> RescindAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractTerminate, "Rescind");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        contract.Status = ContractStatus.Rescindido;
        await SaveContractStateAsync(contract, "RESCINDIR", actor, "Rescindido: " + comment, ct);
        return contract;
    }

    public async Task<Contract> AddPartyAsync(ContractId id, ContractParty party, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditParty");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        ContractParty newParty = new ContractParty
        {
            ContractId = id,
            Role = party.Role,
            Name = party.Name,
            TaxId = party.TaxId,
            LegalRepresentative = party.LegalRepresentative,
            TechnicalContact = party.TechnicalContact,
            Email = party.Email,
            Phone = party.Phone
        };
        await _partyRepo.SaveAsync(newParty, ct);
        contract.Parties.Add(newParty);
        await SaveContractStateAsync(contract, "AGREGAR_PARTE", actor, $"Parte agregada: {party.Name} ({party.Role})", ct);
        return contract;
    }

    public async Task<Contract> RemovePartyAsync(ContractId id, string partyId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditParty");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        await _partyRepo.DeleteAsync(partyId, ct);
        contract.Parties.RemoveAll((ContractParty p) => p.Id == partyId);
        await SaveContractStateAsync(contract, "ELIMINAR_PARTE", actor, "Parte eliminada ID " + partyId, ct);
        return contract;
    }

    public async Task<Contract> AddMilestoneAsync(ContractId id, ContractMilestone milestone, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditMilestone");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        ContractMilestone newMilestone = new ContractMilestone
        {
            ContractId = id,
            Code = milestone.Code,
            Name = milestone.Name,
            ContractualDate = milestone.ContractualDate,
            MilestoneType = milestone.MilestoneType,
            RelatedAmount = milestone.RelatedAmount,
            ActivityId = milestone.ActivityId,
            RequiredEvidence = milestone.RequiredEvidence,
            Status = milestone.Status,
            ToleranceDays = milestone.ToleranceDays,
            ResponsibleName = milestone.ResponsibleName
        };
        await _milestoneRepo.SaveAsync(newMilestone, ct);
        contract.Milestones.Add(newMilestone);
        await SaveContractStateAsync(contract, "AGREGAR_HITO", actor, "Hito agregado: " + milestone.Code + " - " + milestone.Name, ct);
        return contract;
    }

    public async Task<Contract> RemoveMilestoneAsync(ContractId id, string milestoneId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditMilestone");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        await _milestoneRepo.DeleteAsync(milestoneId, ct);
        contract.Milestones.RemoveAll((ContractMilestone m) => m.Id == milestoneId);
        await SaveContractStateAsync(contract, "ELIMINAR_HITO", actor, "Hito eliminado ID " + milestoneId, ct);
        return contract;
    }

    public async Task<Contract> AddScopeLinkAsync(ContractId id, ContractScopeLink link, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditScope");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        ContractScopeLink newLink = new ContractScopeLink
        {
            ContractId = id,
            ScopeType = link.ScopeType,
            ReferenceId = link.ReferenceId,
            Quantity = link.Quantity,
            UnitPrice = link.UnitPrice
        };
        await _scopeRepo.SaveAsync(newLink, ct);
        contract.ScopeLinks.Add(newLink);
        await SaveContractStateAsync(contract, "AGREGAR_ALCANCE", actor, "Alcance agregado: " + link.ScopeType + " Ref " + link.ReferenceId, ct);
        return contract;
    }

    public async Task<Contract> RemoveScopeLinkAsync(ContractId id, string linkId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractEditDraft, "EditScope");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        bool flag = _sod.ApprovedOrObsoleteCannotEdit;
        if (flag)
        {
            ContractStatus status = contract.Status;
            bool flag2 = (((uint)(status - 2) <= 1u || status == ContractStatus.Cerrado) ? true : false);
            flag = flag2;
        }
        if (flag)
        {
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");
        }
        await _scopeRepo.DeleteAsync(linkId, ct);
        contract.ScopeLinks.RemoveAll((ContractScopeLink l) => l.Id == linkId);
        await SaveContractStateAsync(contract, "ELIMINAR_ALCANCE", actor, "Alcance eliminado ID " + linkId, ct);
        return contract;
    }

    public async Task<Contract> AddDocumentAsync(ContractId id, ContractDocument document, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractDocumentManage, "ManageDocument");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        ContractDocument newDoc = new ContractDocument
        {
            ContractId = id,
            DocumentType = document.DocumentType,
            Version = document.Version,
            DocumentDate = document.DocumentDate,
            Author = document.Author,
            FileReference = document.FileReference,
            FileHash = document.FileHash,
            Status = document.Status,
            Comments = document.Comments
        };
        await _documentRepo.SaveAsync(newDoc, ct);
        contract.Documents.Add(newDoc);
        await SaveContractStateAsync(contract, "AGREGAR_DOCUMENTO", actor, $"Documento agregado: {document.DocumentType} ({document.Version})", ct);
        return contract;
    }

    public async Task<Contract> RemoveDocumentAsync(ContractId id, string documentId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractDocumentManage, "ManageDocument");
        Contract contract = await LoadWritable(id, expectedRowVersion, ct);
        await _documentRepo.DeleteAsync(documentId, ct);
        contract.Documents.RemoveAll((ContractDocument d) => d.Id == documentId);
        await SaveContractStateAsync(contract, "ELIMINAR_DOCUMENTO", actor, "Documento eliminado ID " + documentId, ct);
        return contract;
    }

    public async Task<List<ContractVersion>> GetVersionsAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
    {
        return await _versionRepo.GetByContractAsync(contractId, ct);
    }

    public async Task<ContractVersion> VerifyIntegrityAsync(ContractVersionId versionId, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractIntegrityVerify, "VerifyIntegrity");
        ContractVersion contractVersion = (await _versionRepo.GetByIdAsync(versionId, ct)) ?? throw new KeyNotFoundException("Versión no encontrada.");
        if (!string.Equals(ContractVersion.ComputeHash(contractVersion.SnapshotJson), contractVersion.Hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"¡ALERTA DE INTEGRIDAD! La versión {contractVersion.VersionNumber} del contrato ha sido manipulada en la base de datos.");
        }
        return contractVersion;
    }

    public Task<string> CompareVersionsAsync(ContractVersionId v1Id, ContractVersionId v2Id, CancellationToken ct = default(CancellationToken))
    {
        return Task.FromResult($"Comparación básica entre Versión ID {v1Id.Value} y Versión ID {v2Id.Value}.");
    }

    public async Task<Contract?> GetAsync(ContractId id, CancellationToken ct = default(CancellationToken))
    {
        Contract? contract = await _contractRepo.GetByIdAsync(id, ct);
        if (contract == null)
        {
            return null;
        }
        List<ContractParty> collection = await _partyRepo.GetByContractAsync(id, ct);
        contract.Parties.Clear();
        contract.Parties.AddRange(collection);
        List<ContractMilestone> collection2 = await _milestoneRepo.GetByContractAsync(id, ct);
        contract.Milestones.Clear();
        contract.Milestones.AddRange(collection2);
        List<ContractScopeLink> collection3 = await _scopeRepo.GetByContractAsync(id, ct);
        contract.ScopeLinks.Clear();
        contract.ScopeLinks.AddRange(collection3);
        List<ContractDocument> collection4 = await _documentRepo.GetByContractAsync(id, ct);
        contract.Documents.Clear();
        contract.Documents.AddRange(collection4);
        return contract;
    }

    public async Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default(CancellationToken))
    {
        return await _contractRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }

    public async Task<List<ContractAuditEvent>> GetAuditLogAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
    {
        Ensure(_permissions.CanContractAuditView, "AuditView");
        return await _auditRepo.GetByContractAsync(contractId, 100, ct);
    }
}
