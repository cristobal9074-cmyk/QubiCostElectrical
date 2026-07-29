using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ContractService : IContractService
{
    // Sub-collections Management
    public async Task<Contract> AddPartyAsync(ContractId id, ContractParty party, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditParty");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        var newParty = new ContractParty
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

    public async Task<Contract> RemovePartyAsync(ContractId id, string partyId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditParty");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        await _partyRepo.DeleteAsync(partyId, ct);
        contract.Parties.RemoveAll(p => p.Id == partyId);
        await SaveContractStateAsync(contract, "ELIMINAR_PARTE", actor, $"Parte eliminada ID {partyId}", ct);
        return contract;
    }

    public async Task<Contract> AddMilestoneAsync(ContractId id, ContractMilestone milestone, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditMilestone");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        var newMilestone = new ContractMilestone
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
        await SaveContractStateAsync(contract, "AGREGAR_HITO", actor, $"Hito agregado: {milestone.Code} - {milestone.Name}", ct);
        return contract;
    }

    public async Task<Contract> RemoveMilestoneAsync(ContractId id, string milestoneId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditMilestone");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        await _milestoneRepo.DeleteAsync(milestoneId, ct);
        contract.Milestones.RemoveAll(m => m.Id == milestoneId);
        await SaveContractStateAsync(contract, "ELIMINAR_HITO", actor, $"Hito eliminado ID {milestoneId}", ct);
        return contract;
    }

    public async Task<Contract> AddScopeLinkAsync(ContractId id, ContractScopeLink link, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditScope");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        var newLink = new ContractScopeLink
        {
            ContractId = id,
            ScopeType = link.ScopeType,
            ReferenceId = link.ReferenceId,
            Quantity = link.Quantity,
            UnitPrice = link.UnitPrice
        };

        await _scopeRepo.SaveAsync(newLink, ct);
        contract.ScopeLinks.Add(newLink);
        await SaveContractStateAsync(contract, "AGREGAR_ALCANCE", actor, $"Alcance agregado: {link.ScopeType} Ref {link.ReferenceId}", ct);
        return contract;
    }

    public async Task<Contract> RemoveScopeLinkAsync(ContractId id, string linkId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractEditDraft, "EditScope");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && contract.Status is ContractStatus.Aprobado or ContractStatus.Vigente or ContractStatus.Cerrado)
            throw new InvalidOperationException("No se puede modificar un contrato aprobado o cerrado.");

        await _scopeRepo.DeleteAsync(linkId, ct);
        contract.ScopeLinks.RemoveAll(l => l.Id == linkId);
        await SaveContractStateAsync(contract, "ELIMINAR_ALCANCE", actor, $"Alcance eliminado ID {linkId}", ct);
        return contract;
    }

    public async Task<Contract> AddDocumentAsync(ContractId id, ContractDocument document, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractDocumentManage, "ManageDocument");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        var newDoc = new ContractDocument
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

    public async Task<Contract> RemoveDocumentAsync(ContractId id, string documentId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractDocumentManage, "ManageDocument");
        var contract = await LoadWritable(id, expectedRowVersion, ct);

        await _documentRepo.DeleteAsync(documentId, ct);
        contract.Documents.RemoveAll(d => d.Id == documentId);
        await SaveContractStateAsync(contract, "ELIMINAR_DOCUMENTO", actor, $"Documento eliminado ID {documentId}", ct);
        return contract;
    }

    public async Task<List<ContractVersion>> GetVersionsAsync(ContractId contractId, CancellationToken ct = default)
    {
        return await _versionRepo.GetByContractAsync(contractId, ct);
    }

    public async Task<ContractVersion> VerifyIntegrityAsync(ContractVersionId versionId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractIntegrityVerify, "VerifyIntegrity");
        var version = await _versionRepo.GetByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("Versión no encontrada.");

        var currentHash = ContractVersion.ComputeHash(version.SnapshotJson);
        if (!string.Equals(currentHash, version.Hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"¡ALERTA DE INTEGRIDAD! La versión {version.VersionNumber} del contrato ha sido manipulada en la base de datos.");

        return version;
    }

    public Task<string> CompareVersionsAsync(ContractVersionId v1Id, ContractVersionId v2Id, CancellationToken ct = default)
    {
        return Task.FromResult($"Comparación básica entre Versión ID {v1Id.Value} y Versión ID {v2Id.Value}.");
    }

    public async Task<Contract?> GetAsync(ContractId id, CancellationToken ct = default)
    {
        var contract = await _contractRepo.GetByIdAsync(id, ct);
        if (contract == null) return null;

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

    public async Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return await _contractRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }

    public async Task<List<ContractAuditEvent>> GetAuditLogAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanContractAuditView, "AuditView");
        return await _auditRepo.GetByContractAsync(contractId, 100, ct);
    }
}
