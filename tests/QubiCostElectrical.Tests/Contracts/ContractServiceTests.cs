using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Contracts;

public sealed class ContractServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IContractService _service;
    private readonly UserRolePermissionService _permissions;

    public ContractServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_contract_test_{Guid.NewGuid():N}.db");
        SQLiteProjectStore.SaveSnapshot(_dbPath, new ProjectFileData());

        _permissions = new UserRolePermissionService();
        _service = new ContractService(
            new SqliteContractRepository(_dbPath),
            new SqliteContractVersionRepository(_dbPath),
            new SqliteContractPartyRepository(_dbPath),
            new SqliteContractMilestoneRepository(_dbPath),
            new SqliteContractScopeLinkRepository(_dbPath),
            new SqliteContractDocumentRepository(_dbPath),
            new SqliteContractAuditRepository(_dbPath),
            _permissions);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    private static Contract NewDraft() => new Contract
    {
        Id = ContractId.New(),
        ProjectPath = "proj-1",
        Title = "Suministro de tableros eléctricos",
        ClientName = "Cliente SPA",
        ContractorName = "Contratista Ltda",
        OriginalAmount = 1_000_000m,
        Status = ContractStatus.Borrador
    };

    [Fact]
    public async Task CreateAsync_PersistsContractAndInitialVersion()
    {
        _permissions.SetRole(UserRole.Estimator);
        var draft = NewDraft();

        var created = await _service.CreateAsync(draft, "estimator1");

        Assert.False(string.IsNullOrEmpty(created.VisibleNumber));
        Assert.Equal(ContractStatus.Borrador, created.Status);

        var versions = await _service.GetVersionsAsync(created.Id);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].VersionNumber);
    }

    [Fact]
    public async Task FullLifecycle_SubmitThenApprove_ChangesStatusAndApprovesLatestVersion()
    {
        _permissions.SetRole(UserRole.Estimator);
        var created = await _service.CreateAsync(NewDraft(), "estimator1");

        var submitted = await _service.SubmitForReviewAsync(created.Id, created.RowVersion, "estimator1");
        Assert.Equal(ContractStatus.PendienteRevision, submitted.Status);

        _permissions.SetRole(UserRole.Admin);
        var approved = await _service.ApproveAsync(submitted.Id, submitted.RowVersion, "admin1", "OK");
        Assert.Equal(ContractStatus.Vigente, approved.Status);

        var versions = await _service.GetVersionsAsync(approved.Id);
        Assert.True(versions.OrderByDescending(v => v.VersionNumber).First().IsApproved);
    }

    [Fact]
    public async Task SegregationOfDuties_CreatorCannotApproveOwnContract()
    {
        _permissions.SetRole(UserRole.Admin);
        var created = await _service.CreateAsync(NewDraft(), "admin1");
        var submitted = await _service.SubmitForReviewAsync(created.Id, created.RowVersion, "admin1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(submitted.Id, submitted.RowVersion, "admin1", "OK"));
    }

    [Fact]
    public async Task EstimatorCannotApprove_ThrowsUnauthorized()
    {
        _permissions.SetRole(UserRole.Estimator);
        var created = await _service.CreateAsync(NewDraft(), "estimator1");
        var submitted = await _service.SubmitForReviewAsync(created.Id, created.RowVersion, "estimator1");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ApproveAsync(submitted.Id, submitted.RowVersion, "estimator1", "OK"));
    }

    [Fact]
    public async Task VerifyIntegrityAsync_DetectsTamperedSnapshot()
    {
        _permissions.SetRole(UserRole.Estimator);
        var created = await _service.CreateAsync(NewDraft(), "estimator1");
        var versionRepo = new SqliteContractVersionRepository(_dbPath);
        var version = (await versionRepo.GetByContractAsync(created.Id)).Single();

        // Simula manipulación directa en la base de datos: el snapshot cambia
        // pero el hash almacenado queda desactualizado.
        version.SnapshotJson += "tampered";
        await versionRepo.SaveAsync(version);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.VerifyIntegrityAsync(version.Id));
    }

    [Fact]
    public async Task UpdateDraftAsync_WithStaleRowVersion_ThrowsConcurrencyException()
    {
        _permissions.SetRole(UserRole.Estimator);
        var created = await _service.CreateAsync(NewDraft(), "estimator1");
        created.Title = "Título actualizado";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateDraftAsync(created, "stale-row-version", "estimator1"));
    }

    [Fact]
    public async Task SearchAsync_FiltersByProjectPath()
    {
        _permissions.SetRole(UserRole.Estimator);
        var a = NewDraft();
        a.ProjectPath = "proj-A";
        await _service.CreateAsync(a, "estimator1");

        var b = NewDraft();
        b.ProjectPath = "proj-B";
        await _service.CreateAsync(b, "estimator1");

        var results = await _service.SearchAsync("proj-A", null, null, 0, 50);

        Assert.Single(results);
        Assert.Equal("proj-A", results[0].ProjectPath);
    }
}
