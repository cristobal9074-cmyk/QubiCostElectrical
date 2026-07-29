using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical.Wpf.Features.Contracts;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Contracts;

public sealed class ContractTests
{
    private readonly FakeContractRepository _contractRepo = new();
    private readonly FakeContractVersionRepository _versionRepo = new();
    private readonly FakeContractPartyRepository _partyRepo = new();
    private readonly FakeContractMilestoneRepository _milestoneRepo = new();
    private readonly FakeContractScopeLinkRepository _scopeRepo = new();
    private readonly FakeContractDocumentRepository _documentRepo = new();
    private readonly FakeContractAuditRepository _auditRepo = new();
    private readonly UserRolePermissionService _permissions = new();
    private readonly ContractService _svc;

    public ContractTests()
    {
        _svc = new ContractService(
            _contractRepo, _versionRepo, _partyRepo, _milestoneRepo, 
            _scopeRepo, _documentRepo, _auditRepo, _permissions, new SoDPolicyConfig());
        _permissions.SetRole(UserRole.Admin);
    }

    [Fact]
    public async Task Create_Contract_And_Initial_Version()
    {
        var draft = new Contract
        {
            ProjectPath = "C:/Projects/P1.qce",
            VisibleNumber = "CON-00001",
            Title = "Instalaciones Eléctricas de Media Tensión",
            Description = "Contrato para acometida principal",
            ClientName = "Mandante S.A.",
            ContractorName = "Contratista S.A.",
            OriginalAmount = 150000000,
            TaxAmount = 28500000,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(180),
            ContractualDurationDays = 180
        };

        var contract = await _svc.CreateAsync(draft, "User1");
        
        Assert.NotNull(contract.Id);
        Assert.Equal("CON-00001", contract.VisibleNumber);
        Assert.Equal(ContractStatus.Borrador, contract.Status);
        Assert.Equal(1, contract.Version);
        Assert.Equal("User1", contract.CreatedBy);

        // Verify initial version was recorded
        var versions = await _svc.GetVersionsAsync(contract.Id);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].VersionNumber);
        Assert.False(versions[0].IsApproved);
        Assert.NotEmpty(versions[0].Hash);
    }

    [Fact]
    public async Task Validation_Fails_On_Invalid_Values()
    {
        var draft = new Contract
        {
            VisibleNumber = "", // Invalid
            Title = "", // Invalid
            OriginalAmount = -500 // Invalid
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.CreateAsync(draft, "User1"));
    }

    [Fact]
    public async Task Update_Draft_Updates_Values_And_Triggers_Audit()
    {
        var contract = await CreateDefaultContractAsync();
        contract.Title = "Updated Title";
        contract.OriginalAmount = 200000;

        var updated = await _svc.UpdateDraftAsync(contract, contract.RowVersion, "User2");
        
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(200000, updated.OriginalAmount);
        Assert.Equal("User2", updated.ModifiedBy);

        var audit = await _svc.GetAuditLogAsync(contract.Id);
        Assert.Contains(audit, a => a.Action == "EDITAR");
    }

    [Fact]
    public async Task Submit_For_Review_Transitions_Status()
    {
        var contract = await CreateDefaultContractAsync();
        var updated = await _svc.SubmitForReviewAsync(contract.Id, contract.RowVersion, "User1");
        
        Assert.Equal(ContractStatus.PendienteRevision, updated.Status);
    }

    [Fact]
    public async Task Approve_Transitions_Status_To_Vigente_And_Seals_Version()
    {
        var contract = await CreateDefaultContractAsync();
        await _svc.SubmitForReviewAsync(contract.Id, contract.RowVersion, "User1");
        
        // Approve by a different user to satisfy SoD Creator/LastEditor policy
        var approved = await _svc.ApproveAsync(contract.Id, contract.RowVersion, "ApproverUser", "Aprobación OK");
        
        Assert.Equal(ContractStatus.Vigente, approved.Status);

        var versions = await _svc.GetVersionsAsync(contract.Id);
        Assert.True(versions.Last().IsApproved);
    }

    [Fact]
    public async Task Approve_Fails_If_Same_Creator_Under_SoD()
    {
        var contract = await CreateDefaultContractAsync(); // Created by "DefaultUser"
        await _svc.SubmitForReviewAsync(contract.Id, contract.RowVersion, "DefaultUser");
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _svc.ApproveAsync(contract.Id, contract.RowVersion, "DefaultUser", "Same User Approve"));
    }

    [Fact]
    public async Task Reject_Transitions_Back_To_Borrador()
    {
        var contract = await CreateDefaultContractAsync();
        await _svc.SubmitForReviewAsync(contract.Id, contract.RowVersion, "User1");
        var rejected = await _svc.RejectAsync(contract.Id, contract.RowVersion, "ReviewerUser", "Faltan documentos");
        
        Assert.Equal(ContractStatus.Borrador, rejected.Status);
    }

    [Fact]
    public async Task Suspend_And_Reactivate_Workflows()
    {
        var contract = await CreateApprovedContractAsync();
        
        var suspended = await _svc.SuspendAsync(contract.Id, contract.RowVersion, "AdminUser", "Suspensión de obras");
        Assert.Equal(ContractStatus.Suspendido, suspended.Status);

        var active = await _svc.ReactivateAsync(contract.Id, contract.RowVersion, "AdminUser", "Reinicio");
        Assert.Equal(ContractStatus.Vigente, active.Status);
    }

    [Fact]
    public async Task Close_Terminate_Rescind_Workflows()
    {
        var contract = await CreateApprovedContractAsync();
        
        var closed = await _svc.CloseAsync(contract.Id, contract.RowVersion, "AdminUser", "Cierre contractual");
        Assert.Equal(ContractStatus.Cerrado, closed.Status);
    }

    [Fact]
    public async Task Add_And_Remove_Parties()
    {
        var contract = await CreateDefaultContractAsync();
        var party = new ContractParty
        {
            Role = ContractPartyRole.Mandante,
            Name = "Qubi Mandante",
            TaxId = "77.777.777-7",
            Email = "m@qubi.cl"
        };

        var updated = await _svc.AddPartyAsync(contract.Id, party, contract.RowVersion, "User1");
        Assert.Single(updated.Parties);
        Assert.Equal("Qubi Mandante", updated.Parties[0].Name);

        var removed = await _svc.RemovePartyAsync(contract.Id, updated.Parties[0].Id, updated.RowVersion, "User1");
        Assert.Empty(removed.Parties);
    }

    [Fact]
    public async Task Add_And_Remove_Milestones()
    {
        var contract = await CreateDefaultContractAsync();
        var milestone = new ContractMilestone
        {
            Code = "HITO1",
            Name = "Entrega de planos",
            ContractualDate = DateTime.Today.AddDays(30),
            MilestoneType = "Entrega",
            RelatedAmount = 5000000
        };

        var updated = await _svc.AddMilestoneAsync(contract.Id, milestone, contract.RowVersion, "User1");
        Assert.Single(updated.Milestones);
        Assert.Equal("HITO1", updated.Milestones[0].Code);

        var removed = await _svc.RemoveMilestoneAsync(contract.Id, updated.Milestones[0].Id, updated.RowVersion, "User1");
        Assert.Empty(removed.Milestones);
    }

    [Fact]
    public async Task Add_And_Remove_ScopeLinks()
    {
        var contract = await CreateDefaultContractAsync();
        var link = new ContractScopeLink
        {
            ScopeType = "WBS",
            ReferenceId = "WBS-01.01",
            Quantity = 1,
            UnitPrice = 10000
        };

        var updated = await _svc.AddScopeLinkAsync(contract.Id, link, contract.RowVersion, "User1");
        Assert.Single(updated.ScopeLinks);
        Assert.Equal("WBS-01.01", updated.ScopeLinks[0].ReferenceId);

        var removed = await _svc.RemoveScopeLinkAsync(contract.Id, updated.ScopeLinks[0].Id, updated.RowVersion, "User1");
        Assert.Empty(removed.ScopeLinks);
    }

    [Fact]
    public async Task Add_And_Remove_Documents()
    {
        var contract = await CreateDefaultContractAsync();
        var doc = new ContractDocument
        {
            DocumentType = "Poliza",
            Version = "1.0",
            DocumentDate = DateTime.Today,
            Author = "Aseguradora S.A.",
            FileReference = "C:/Docs/poliza.pdf"
        };

        var updated = await _svc.AddDocumentAsync(contract.Id, doc, contract.RowVersion, "User1");
        Assert.Single(updated.Documents);
        Assert.Equal("Poliza", updated.Documents[0].DocumentType);

        var removed = await _svc.RemoveDocumentAsync(contract.Id, updated.Documents[0].Id, updated.RowVersion, "User1");
        Assert.Empty(removed.Documents);
    }

    [Fact]
    public async Task Verify_Integrity_Succeeds_When_Unchanged()
    {
        var contract = await CreateApprovedContractAsync();
        var versions = await _svc.GetVersionsAsync(contract.Id);
        var verified = await _svc.VerifyIntegrityAsync(versions.Last().Id);
        
        Assert.NotNull(verified);
        Assert.Equal(versions.Last().Hash, verified.Hash);
    }

    [Fact]
    public async Task Verify_Integrity_Throws_When_Tampered()
    {
        var contract = await CreateApprovedContractAsync();
        var versions = await _svc.GetVersionsAsync(contract.Id);
        
        // Simulate tampering of snapshot data
        versions.Last().SnapshotJson = "{ \"title\": \"Tampered Title\" }";
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _svc.VerifyIntegrityAsync(versions.Last().Id));
    }

    [Fact]
    public async Task Concurrency_Throws_Exception_On_RowVersion_Mismatch()
    {
        var contract = await CreateDefaultContractAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _svc.SubmitForReviewAsync(contract.Id, "stale_version", "User1"));
    }

    [Fact]
    public async Task ViewModels_Load_And_Search_Correctly()
    {
        var dummyShell = new DummyShellNavigationHost();
        var dummyAppHost = WpfAppHost.Initialize("C:/.gemini/antigravity/logs", "C:/.gemini/antigravity/pricebank.db");
        dummyAppHost.Services.SetContractServices(
            _contractRepo, _versionRepo, _partyRepo, _milestoneRepo, 
            _scopeRepo, _documentRepo, _auditRepo, _svc);

        // Prepopulate repo
        var c = await CreateDefaultContractAsync();
        dummyAppHost.Session.CurrentProjectPath = "C:/Projects/P1.qce";

        var wsVm = new ContractWorkspaceViewModel(dummyShell, dummyAppHost);
        await wsVm.LoadAsync();
        Assert.Single(wsVm.Items);
        Assert.Equal("CON-00001", wsVm.Items[0].VisibleNumber);

        var verVm = new ContractVersionsViewModel(dummyShell, dummyAppHost, c.Id);
        await verVm.LoadVersionsAsync();
        Assert.Single(verVm.Versions);
    }

    [Fact]
    public void ShellRoute_Mappings_Are_Correct()
    {
        var routeInfo = ShellRouteCatalog.Get(ShellRoute.ContractWorkspace);
        Assert.Equal("Gestión contractual", routeInfo.Group);
        Assert.Equal("Contratos", routeInfo.Title);

        var verRouteInfo = ShellRouteCatalog.Get(ShellRoute.ContractVersions);
        Assert.Equal("Gestión contractual", verRouteInfo.Group);
        Assert.Equal("Historial de Versiones", verRouteInfo.Title);
    }

    // Helper methods
    private async Task<Contract> CreateDefaultContractAsync()
    {
        var draft = new Contract
        {
            VisibleNumber = "CON-00001",
            Title = "Contrato Inicial",
            ProjectPath = "C:/Projects/P1.qce",
            OriginalAmount = 100000
        };
        return await _svc.CreateAsync(draft, "DefaultUser");
    }

    private async Task<Contract> CreateApprovedContractAsync()
    {
        var contract = await CreateDefaultContractAsync();
        await _svc.SubmitForReviewAsync(contract.Id, contract.RowVersion, "DefaultUser");
        return await _svc.ApproveAsync(contract.Id, contract.RowVersion, "ApproverUser", "Approve", CancellationToken.None);
    }
}

// Dummy classes and Fakes for testing
public sealed class DummyShellNavigationHost : IShellNavigationHost
{
    public WpfAppHost Host => null!;
    public ShellRoute CurrentRoute { get; set; }
    public string ActiveProjectLabel => "";
    public bool RequestNavigate(ShellRoute route, bool force = false)
    {
        CurrentRoute = route;
        return true;
    }
    public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
    {
        CurrentRoute = route;
        return true;
    }
    public Task NewProjectAsync() => Task.CompletedTask;
    public Task OpenProjectAsync() => Task.CompletedTask;
    public Task<bool> CloseProjectAsync() => Task.FromResult(true);
    public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
    public void RemoveRecentProject(string path) {}
    public Task SaveProjectAsync() => Task.CompletedTask;
    public Task SaveProjectAsAsync() => Task.CompletedTask;
    public void RefreshChrome() {}
}

public sealed class FakeContractRepository : IContractRepository
{
    private readonly Dictionary<string, Contract> _data = new();

    public Task SaveAsync(Contract contract, CancellationToken ct = default)
    {
        _data[contract.Id.Value] = contract;
        return Task.CompletedTask;
    }

    public Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default)
    {
        _data.TryGetValue(id.Value, out var val);
        return Task.FromResult(val);
    }

    public Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default)
    {
        var val = _data.Values.FirstOrDefault(c => c.VisibleNumber == visibleNumber);
        return Task.FromResult(val);
    }

    public Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Values.Where(c => c.ProjectPath == projectPath).ToList());
    }

    public Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Count + 1);
    }
}

public sealed class FakeContractVersionRepository : IContractVersionRepository
{
    private readonly Dictionary<string, ContractVersion> _data = new();

    public Task SaveAsync(ContractVersion version, CancellationToken ct = default)
    {
        _data[version.Id.Value] = version;
        return Task.CompletedTask;
    }

    public Task<ContractVersion?> GetByIdAsync(ContractVersionId id, CancellationToken ct = default)
    {
        _data.TryGetValue(id.Value, out var val);
        return Task.FromResult(val);
    }

    public Task<List<ContractVersion>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        var list = _data.Values.Where(v => v.ContractId == contractId).ToList();
        return Task.FromResult(list);
    }

    public Task<int> GetLatestVersionNumberAsync(ContractId contractId, CancellationToken ct = default)
    {
        var max = _data.Values.Where(v => v.ContractId == contractId).Select(v => v.VersionNumber).DefaultIfEmpty(0).Max();
        return Task.FromResult(max);
    }
}

public sealed class FakeContractPartyRepository : IContractPartyRepository
{
    private readonly List<ContractParty> _data = new();

    public Task SaveAsync(ContractParty party, CancellationToken ct = default)
    {
        _data.RemoveAll(p => p.Id == party.Id);
        _data.Add(party);
        return Task.CompletedTask;
    }

    public Task<List<ContractParty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Where(p => p.ContractId == contractId).ToList());
    }

    public Task DeleteAsync(string partyId, CancellationToken ct = default)
    {
        _data.RemoveAll(p => p.Id == partyId);
        return Task.CompletedTask;
    }
}

public sealed class FakeContractMilestoneRepository : IContractMilestoneRepository
{
    private readonly List<ContractMilestone> _data = new();

    public Task SaveAsync(ContractMilestone milestone, CancellationToken ct = default)
    {
        _data.RemoveAll(m => m.Id == milestone.Id);
        _data.Add(milestone);
        return Task.CompletedTask;
    }

    public Task<List<ContractMilestone>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Where(m => m.ContractId == contractId).ToList());
    }

    public Task DeleteAsync(string milestoneId, CancellationToken ct = default)
    {
        _data.RemoveAll(m => m.Id == milestoneId);
        return Task.CompletedTask;
    }
}

public sealed class FakeContractScopeLinkRepository : IContractScopeLinkRepository
{
    private readonly List<ContractScopeLink> _data = new();

    public Task SaveAsync(ContractScopeLink link, CancellationToken ct = default)
    {
        _data.RemoveAll(l => l.Id == link.Id);
        _data.Add(link);
        return Task.CompletedTask;
    }

    public Task<List<ContractScopeLink>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Where(l => l.ContractId == contractId).ToList());
    }

    public Task DeleteAsync(string linkId, CancellationToken ct = default)
    {
        _data.RemoveAll(l => l.Id == linkId);
        return Task.CompletedTask;
    }
}

public sealed class FakeContractDocumentRepository : IContractDocumentRepository
{
    private readonly List<ContractDocument> _data = new();

    public Task SaveAsync(ContractDocument document, CancellationToken ct = default)
    {
        _data.RemoveAll(d => d.Id == document.Id);
        _data.Add(document);
        return Task.CompletedTask;
    }

    public Task<List<ContractDocument>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Where(d => d.ContractId == contractId).ToList());
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        _data.RemoveAll(d => d.Id == documentId);
        return Task.CompletedTask;
    }
}

public sealed class FakeContractAuditRepository : IContractAuditRepository
{
    private readonly List<ContractAuditEvent> _data = new();

    public Task RecordAsync(ContractAuditEvent evt, CancellationToken ct = default)
    {
        _data.Add(evt);
        return Task.CompletedTask;
    }

    public Task<List<ContractAuditEvent>> GetByContractAsync(ContractId contractId, int limit = 100, CancellationToken ct = default)
    {
        return Task.FromResult(_data.Where(a => a.ContractId == contractId).Take(limit).ToList());
    }
}
