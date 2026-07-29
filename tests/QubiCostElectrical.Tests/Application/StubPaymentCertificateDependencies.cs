using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Stub implementations for all PaymentCertificateService dependencies.
/// Provides in-memory storage for repositories and configurable permission stubs.
/// </summary>
public sealed class StubPaymentCertificateDependencies
{
    public StubCertRepo CertRepo { get; } = new();
    public StubLineRepo LineRepo { get; } = new();
    public StubRevisionRepo RevisionRepo { get; } = new();
    public StubAuditRepo AuditRepo { get; } = new();
    public StubAdvanceRepo AdvanceRepo { get; } = new();
    public StubAmortRepo AmortRepo { get; } = new();
    public StubRetentionRepo RetentionRepo { get; } = new();
    public StubRetAppRepo RetAppRepo { get; } = new();
    public StubRetReleaseRepo RetReleaseRepo { get; } = new();
    public StubObservationRepo ObservationRepo { get; } = new();
    public StubContractRepo ContractRepo { get; } = new();
    public StubPermissionService PermissionService { get; } = new();
    public SoDPolicyConfig SodConfig { get; } = new();

    public PaymentCertificateService CreateService()
    {
        return new PaymentCertificateService(
            CertRepo, LineRepo, RevisionRepo, AuditRepo,
            AdvanceRepo, AmortRepo, RetentionRepo, RetAppRepo, RetReleaseRepo,
            ObservationRepo, ContractRepo, PermissionService, SodConfig);
    }
}

public sealed class StubCertRepo : IPaymentCertificateRepository
{
    public Dictionary<PaymentCertificateId, PaymentCertificate> Certificates { get; } = new();
    private int _nextNumber;

    public Task SaveAsync(PaymentCertificate cert, CancellationToken ct = default)
    {
        Certificates[cert.Id] = cert;
        return Task.CompletedTask;
    }

    public Task<PaymentCertificate?> GetByIdAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        Certificates.TryGetValue(id, out var cert);
        return Task.FromResult(cert);
    }

    public Task<PaymentCertificate?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default)
    {
        return Task.FromResult(Certificates.Values.FirstOrDefault(c => c.VisibleNumber == visibleNumber));
    }

    public Task<List<PaymentCertificate>> SearchAsync(string projectPath, string? text, PaymentCertificateStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var query = Certificates.Values.AsEnumerable();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(text))
            query = query.Where(c => c.VisibleNumber.Contains(text) || c.Presenter.Contains(text));
        return Task.FromResult(query.Skip(skip).Take(take).ToList());
    }

    public Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        return Task.FromResult(++_nextNumber);
    }

    public Task<List<PaymentCertificate>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(Certificates.Values.Where(c => c.ContractId == contractId).ToList());
    }
}

public sealed class StubLineRepo : IPaymentCertificateLineRepository
{
    public Dictionary<PaymentCertificateLineId, PaymentCertificateLine> Lines { get; } = new();

    public Task SaveAsync(PaymentCertificateLine line, CancellationToken ct = default)
    {
        Lines[line.Id] = line;
        return Task.CompletedTask;
    }

    public Task SaveRangeAsync(IEnumerable<PaymentCertificateLine> lines, CancellationToken ct = default)
    {
        foreach (var line in lines) Lines[line.Id] = line;
        return Task.CompletedTask;
    }

    public Task<List<PaymentCertificateLine>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        return Task.FromResult(Lines.Values.Where(l => l.PaymentCertificateId == id).ToList());
    }

    public Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var toRemove = Lines.Values.Where(l => l.PaymentCertificateId == id).ToList();
        foreach (var l in toRemove) Lines.Remove(l.Id);
        return Task.CompletedTask;
    }
}

public sealed class StubRevisionRepo : IPaymentCertificateRevisionRepository
{
    public Dictionary<PaymentCertificateRevisionId, PaymentCertificateRevision> Revisions { get; } = new();

    public Task SaveAsync(PaymentCertificateRevision revision, CancellationToken ct = default)
    {
        Revisions[revision.Id] = revision;
        return Task.CompletedTask;
    }

    public Task<PaymentCertificateRevision?> GetByIdAsync(PaymentCertificateRevisionId id, CancellationToken ct = default)
    {
        Revisions.TryGetValue(id, out var rev);
        return Task.FromResult(rev);
    }

    public Task<List<PaymentCertificateRevision>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        return Task.FromResult(Revisions.Values.Where(r => r.PaymentCertificateId == id).ToList());
    }
}

public sealed class StubAuditRepo : IPaymentCertificateAuditRepository
{
    public List<PaymentCertificateAuditEvent> Events { get; } = new();

    public Task RecordAsync(PaymentCertificateAuditEvent evt, CancellationToken ct = default)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }

    public Task<List<PaymentCertificateAuditEvent>> GetByCertificateAsync(PaymentCertificateId id, int limit = 100, CancellationToken ct = default)
    {
        return Task.FromResult(Events.Where(e => e.PaymentCertificateId == id).Take(limit).ToList());
    }
}

public sealed class StubAdvanceRepo : IContractAdvanceRepository
{
    public Dictionary<ContractAdvanceId, ContractAdvance> Advances { get; } = new();

    public Task SaveAsync(ContractAdvance advance, CancellationToken ct = default)
    {
        Advances[advance.Id] = advance;
        return Task.CompletedTask;
    }

    public Task<ContractAdvance?> GetByIdAsync(ContractAdvanceId id, CancellationToken ct = default)
    {
        Advances.TryGetValue(id, out var adv);
        return Task.FromResult(adv);
    }

    public Task<List<ContractAdvance>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(Advances.Values.Where(a => a.ContractId == contractId).ToList());
    }
}

public sealed class StubAmortRepo : IAdvanceAmortizationRepository
{
    public Dictionary<string, AdvanceAmortization> Amortizations { get; } = new();

    public Task SaveAsync(AdvanceAmortization amort, CancellationToken ct = default)
    {
        Amortizations[amort.Id] = amort;
        return Task.CompletedTask;
    }

    public Task SaveRangeAsync(IEnumerable<AdvanceAmortization> amorts, CancellationToken ct = default)
    {
        foreach (var a in amorts) Amortizations[a.Id] = a;
        return Task.CompletedTask;
    }

    public Task<List<AdvanceAmortization>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        return Task.FromResult(Amortizations.Values.Where(a => a.PaymentCertificateId == id).ToList());
    }

    public Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var toRemove = Amortizations.Values.Where(a => a.PaymentCertificateId == id).ToList();
        foreach (var a in toRemove) Amortizations.Remove(a.Id);
        return Task.CompletedTask;
    }
}

public sealed class StubRetentionRepo : IContractRetentionRepository
{
    public Dictionary<ContractRetentionId, ContractRetention> Retentions { get; } = new();

    public Task SaveAsync(ContractRetention retention, CancellationToken ct = default)
    {
        Retentions[retention.Id] = retention;
        return Task.CompletedTask;
    }

    public Task<ContractRetention?> GetByIdAsync(ContractRetentionId id, CancellationToken ct = default)
    {
        Retentions.TryGetValue(id, out var ret);
        return Task.FromResult(ret);
    }

    public Task<List<ContractRetention>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(Retentions.Values.Where(r => r.ContractId == contractId).ToList());
    }
}

public sealed class StubRetAppRepo : IRetentionApplicationRepository
{
    public Dictionary<string, RetentionApplication> Apps { get; } = new();

    public Task SaveAsync(RetentionApplication app, CancellationToken ct = default)
    {
        Apps[app.Id] = app;
        return Task.CompletedTask;
    }

    public Task SaveRangeAsync(IEnumerable<RetentionApplication> apps, CancellationToken ct = default)
    {
        foreach (var a in apps) Apps[a.Id] = a;
        return Task.CompletedTask;
    }

    public Task<List<RetentionApplication>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        return Task.FromResult(Apps.Values.Where(a => a.PaymentCertificateId == id).ToList());
    }

    public Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        var toRemove = Apps.Values.Where(a => a.PaymentCertificateId == id).ToList();
        foreach (var a in toRemove) Apps.Remove(a.Id);
        return Task.CompletedTask;
    }
}

public sealed class StubRetReleaseRepo : IRetentionReleaseRepository
{
    public List<RetentionRelease> Releases { get; } = new();

    public Task SaveAsync(RetentionRelease release, CancellationToken ct = default)
    {
        Releases.Add(release);
        return Task.CompletedTask;
    }

    public Task<List<RetentionRelease>> GetByRetentionAsync(ContractRetentionId retentionId, CancellationToken ct = default)
    {
        return Task.FromResult(Releases.Where(r => r.ContractRetentionId == retentionId).ToList());
    }

    public Task<List<RetentionRelease>> GetByCertificateAsync(PaymentCertificateId certificateId, CancellationToken ct = default)
    {
        return Task.FromResult(Releases.Where(r => r.PaymentCertificateId == certificateId).ToList());
    }
}

public sealed class StubObservationRepo : IPaymentObservationRepository
{
    public Dictionary<PaymentObservationId, PaymentObservation> Observations { get; } = new();

    public Task SaveAsync(PaymentObservation observation, CancellationToken ct = default)
    {
        Observations[observation.Id] = observation;
        return Task.CompletedTask;
    }

    public Task<PaymentObservation?> GetByIdAsync(PaymentObservationId id, CancellationToken ct = default)
    {
        Observations.TryGetValue(id, out var obs);
        return Task.FromResult(obs);
    }

    public Task<List<PaymentObservation>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        return Task.FromResult(Observations.Values.Where(o => o.PaymentCertificateId == id).ToList());
    }
}

public sealed class StubContractRepo : IContractRepository
{
    public ContractStatus Status { get; set; } = ContractStatus.Vigente;

    public Task SaveAsync(Contract contract, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default)
    {
        var contract = new Contract
        {
            Id = id,
            VisibleNumber = "CTR-001",
            Status = Status,
            ProjectPath = "/test/path",
            OriginalAmount = 500_000m
        };
        return Task.FromResult<Contract?>(contract);
    }

    public Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default)
    {
        return Task.FromResult<Contract?>(new Contract
        {
            Id = ContractId.From("CTR-001"),
            VisibleNumber = visibleNumber,
            Status = ContractStatus.Vigente
        });
    }

    public Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return Task.FromResult(new List<Contract>
        {
            new() { Id = ContractId.From("CTR-001"), VisibleNumber = "CTR-001" }
        });
    }

    public Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        return Task.FromResult(1);
    }
}

public sealed class StubPermissionService : IPermissionService
{
    public UserRole CurrentRole { get; set; } = UserRole.Admin;
    public void SetRole(UserRole role) => CurrentRole = role;

    public bool CanPaymentCertificateView { get; set; } = true;
    public bool CanPaymentCertificateCreate { get; set; } = true;
    public bool CanPaymentCertificateEditDraft { get; set; } = true;
    public bool CanPaymentCertificateSubmit { get; set; } = true;
    public bool CanPaymentCertificateReview { get; set; } = true;
    public bool CanPaymentCertificateObserve { get; set; } = true;
    public bool CanPaymentCertificateCorrect { get; set; } = true;
    public bool CanPaymentCertificateApprove { get; set; } = true;
    public bool CanPaymentCertificateReject { get; set; } = true;
    public bool CanPaymentCertificateInvoice { get; set; } = true;
    public bool CanPaymentCertificateMarkPaid { get; set; } = true;
    public bool CanPaymentCertificateClose { get; set; } = true;
    public bool CanPaymentCertificateVoid { get; set; } = true;
    public bool CanValuationImport { get; set; } = true;
    public bool CanAdvanceManage { get; set; } = true;
    public bool CanRetentionManage { get; set; } = true;
    public bool CanPaymentCertificateAuditView { get; set; } = true;
    public bool CanPaymentCertificateExport { get; set; } = true;

    // Required IPermissionService members (unused in payment cert flow - default true)
    public bool CanExportDocuments => true;
    public bool CanEditGlobalPriceBank => true;
    public bool CanManageAppSettings => true;
    public bool CanApproveVersions => true;
    public bool CanSendVersionToReview => true;
    public bool CanViewItemizado => true;
    public bool CanEditItemizado => true;
    public bool CanViewApus => true;
    public bool CanEditApus => true;
    public bool CanViewConjuntos => true;
    public bool CanEditConjuntos => true;
    public bool CanViewCubicacion => true;
    public bool CanEditCubicacion => true;
    public bool CanViewCubicacionSC => true;
    public bool CanEditCubicacionSC => true;
    public bool CanViewBudget => true;
    public bool CanEditBudget => true;
    public bool CanViewReports => true;
    public bool CanExport => true;
    public bool CanViewVersions => true;
    public bool CanCreateVersion => true;
    public bool CanEditVersion => true;
    public bool CanStartVersionReview => true;
    public bool CanRejectVersion => true;
    public bool CanViewAudit => true;
    public bool CanManageUsers => true;
    public bool CanManageRoles => true;
    public bool CanManagePolicies => true;
    public bool CanCompareVersions => true;
    public string RoleDisplayName => "Test";
    public string RoleReadOnlyMessage => "";
    public bool CanContractView => true;
    public bool CanContractCreate => true;
    public bool CanContractEditDraft => true;
    public bool CanContractCreateVersion => true;
    public bool CanContractSubmit => true;
    public bool CanContractApprove => true;
    public bool CanContractReject => true;
    public bool CanContractSuspend => true;
    public bool CanContractClose => true;
    public bool CanContractTerminate => true;
    public bool CanContractDocumentManage => true;
    public bool CanContractAuditView => true;
    public bool CanContractIntegrityVerify => true;
}
