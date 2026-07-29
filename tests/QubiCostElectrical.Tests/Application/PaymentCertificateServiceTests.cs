using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class PaymentCertificateServiceTests
{
    private static PaymentCertificate CreateDraft()
    {
        return new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = ContractId.From("CTR-001"),
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31),
            CutOffDate = new DateTime(2026, 1, 25),
            Currency = "CLP",
            ExchangeRate = 1.0m,
            Presenter = "Juan Pérez",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "test@empresa.cl"
        };
    }

    private static PaymentCertificateLine CreateLine(string? id = null)
    {
        return new PaymentCertificateLine
        {
            Id = id != null ? PaymentCertificateLineId.From(id) : PaymentCertificateLineId.New(),
            Description = "Instalación de cable NYY 2.5mm²",
            Unit = "ML",
            CantidadContractual = 100m,
            PrecioUnitarioContractual = 1000m,
            CantidadAnterior = 40m,
            CantidadPeriodo = 20m,
            CantidadAcumulada = 60m
        };
    }

    [Fact]
    public async Task CreateAsync_ValidDraft_ReturnsCertificateWithNextNumber()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        Assert.NotNull(cert);
        Assert.NotEmpty(cert.Id.Value);
        Assert.Equal("EP-00001", cert.VisibleNumber);
        Assert.Equal(PaymentCertificateStatus.Borrador, cert.Status);
        Assert.Equal("test@empresa.cl", cert.CreatedBy);
        Assert.Equal(1, cert.RevisionNumber);
    }

    [Fact]
    public async Task CreateAsync_WithoutPermission_ThrowsUnauthorized()
    {
        var deps = new StubPaymentCertificateDependencies();
        deps.PermissionService.CanPaymentCertificateCreate = false;
        var service = deps.CreateService();

        var draft = CreateDraft();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(draft, "test@empresa.cl"));
    }

    [Fact]
    public async Task CreateAsync_WithInactiveContract_Throws()
    {
        var deps = new StubPaymentCertificateDependencies();
        deps.ContractRepo.Status = ContractStatus.Archivado;
        var service = deps.CreateService();

        var draft = CreateDraft();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(draft, "test@empresa.cl"));
    }

    [Fact]
    public async Task UpdateDraftAsync_EditableDraft_UpdatesFields()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        cert.Comment = "Comentario actualizado";
        cert.ExchangeRate = 850m;
        var updated = await service.UpdateDraftAsync(cert, cert.RowVersion, "test@empresa.cl");

        Assert.Equal("Comentario actualizado", updated.Comment);
        Assert.Equal(850m, updated.ExchangeRate);
    }

    [Fact]
    public async Task SubmitAsync_FromBorrador_TransitionsToPresentado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        // Add a line so submission is valid
        var line = CreateLine();
        cert = await service.AddLineAsync(cert.Id, line, cert.RowVersion, "test@empresa.cl");

        var submitted = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");

        Assert.Equal(PaymentCertificateStatus.Presentado, submitted.Status);
        Assert.Equal(2, submitted.RevisionNumber);
    }

    [Fact]
    public async Task SubmitAsync_WithoutLines_Throws()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl"));
    }

    [Fact]
    public async Task FullWorkflow_SubmitToApprove_Succeeds()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "creator@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "creator@empresa.cl");

        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "submitter@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Presentado, cert.Status);

        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.EnRevision, cert.Status);

        // EnRevision → PendienteAprobacion (nuevo paso intermedio)
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.PendienteAprobacion, cert.Status);

        // PendienteAprobacion → Aprobado
        cert = await service.ApproveAsync(cert.Id, cert.RowVersion, "approver@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Aprobado, cert.Status);
        Assert.Equal("approver@empresa.cl", cert.Aprobador);
        Assert.NotNull(cert.ApprovedAtUtc);
    }

    [Fact]
    public async Task ApproveAsync_CreatorCannotApprove_Throws()
    {
        var deps = new StubPaymentCertificateDependencies();
        deps.SodConfig.CreatorCannotApprove = true;
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "creator@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "creator@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "submitter@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");

        // EnRevision → PendienteAprobacion (nuevo paso intermedio)
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ApproveAsync(cert.Id, cert.RowVersion, "creator@empresa.cl"));
    }

    [Fact]
    public async Task ApproveAsync_PresenterCannotApprove_Throws()
    {
        var deps = new StubPaymentCertificateDependencies();
        deps.SodConfig.PresenterCannotApprove = true;
        var service = deps.CreateService();

        var draft = CreateDraft();
        draft.Presenter = "presenter@empresa.cl";
        var cert = await service.CreateAsync(draft, "creator@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "creator@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "presenter@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");

        // EnRevision → PendienteAprobacion (nuevo paso intermedio)
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ApproveAsync(cert.Id, cert.RowVersion, "presenter@empresa.cl"));
    }

    [Fact]
    public async Task ObserveAsync_FromPresented_TransitionsToObservado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");

        cert = await service.ObserveAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl", "Falta documentación de respaldo");

        Assert.Equal(PaymentCertificateStatus.Observado, cert.Status);
        Assert.Contains("Falta documentación", cert.Comment);
    }

    [Fact]
    public async Task CorrectAsync_FromObservado_TransitionsToCorregido()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.ObserveAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl", "Falta respaldo");

        cert = await service.CorrectAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Corregido, cert.Status);
    }

    [Fact]
    public async Task RejectAsync_FromEnRevision_TransitionsToRechazado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");

        cert = await service.RejectAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl", "Valores incorrectos");

        Assert.Equal(PaymentCertificateStatus.Rechazado, cert.Status);
    }

    [Fact]
    public async Task InvoiceAsync_FromAprobado_TransitionsToFacturado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.ApproveAsync(cert.Id, cert.RowVersion, "approver@empresa.cl");

        cert = await service.InvoiceAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Facturado, cert.Status);
    }

    [Fact]
    public async Task MarkPaidAsync_FromFacturado_TransitionsToPagado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.ApproveAsync(cert.Id, cert.RowVersion, "approver@empresa.cl");
        cert = await service.InvoiceAsync(cert.Id, cert.RowVersion, "test@empresa.cl");

        cert = await service.MarkPaidAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Pagado, cert.Status);
    }

    [Fact]
    public async Task CloseAsync_FromPagado_TransitionsToCerrado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");
        cert = await service.AddLineAsync(cert.Id, CreateLine(), cert.RowVersion, "test@empresa.cl");
        cert = await service.SubmitAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.ReviewAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.SendToApprovalAsync(cert.Id, cert.RowVersion, "reviewer@empresa.cl");
        cert = await service.ApproveAsync(cert.Id, cert.RowVersion, "approver@empresa.cl");
        cert = await service.InvoiceAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        cert = await service.MarkPaidAsync(cert.Id, cert.RowVersion, "test@empresa.cl");

        cert = await service.CloseAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Cerrado, cert.Status);
    }

    [Fact]
    public async Task VoidAsync_FromBorrador_TransitionsToAnulado()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        cert = await service.VoidAsync(cert.Id, cert.RowVersion, "test@empresa.cl");
        Assert.Equal(PaymentCertificateStatus.Anulado, cert.Status);
    }

    [Fact]
    public async Task AddLineAsync_ValidLine_AddsToCertificate()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var line = CreateLine();
        cert = await service.AddLineAsync(cert.Id, line, cert.RowVersion, "test@empresa.cl");

        Assert.Single(cert.Lines);
        Assert.Equal("Instalación de cable NYY 2.5mm²", cert.Lines[0].Description);
    }

    [Fact]
    public async Task RemoveLineAsync_ExistingLine_RemovesFromCertificate()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var line = CreateLine("L1");
        cert = await service.AddLineAsync(cert.Id, line, cert.RowVersion, "test@empresa.cl");
        cert = await service.RemoveLineAsync(cert.Id, PaymentCertificateLineId.From("L1"), cert.RowVersion, "test@empresa.cl");

        Assert.Empty(cert.Lines);
    }

    [Fact]
    public async Task RecalculateAsync_WithLines_ReturnsCalculationResult()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var line = new PaymentCertificateLine
        {
            Id = PaymentCertificateLineId.New(),
            PaymentCertificateId = cert.Id,
            Description = "Test",
            Unit = "UN",
            CantidadContractual = 100m,
            PrecioUnitarioContractual = 1000m,
            CantidadAnterior = 30m,
            CantidadPeriodo = 20m,
            CantidadAcumulada = 50m
        };
        cert = await service.AddLineAsync(cert.Id, line, cert.RowVersion, "test@empresa.cl");

        var result = await service.RecalculateAsync(cert.Id);

        Assert.NotNull(result);
        Assert.Single(result.Lines);
        Assert.Equal(20_000m, result.SubtotalValorizado);  // 20 * 1000
        Assert.Equal(20_000m, result.MontoBruto);
    }

    [Fact]
    public async Task ApplyCalculationAsync_UpdatesCertificateTotals()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var line = CreateLine();
        cert = await service.AddLineAsync(cert.Id, line, cert.RowVersion, "test@empresa.cl");

        var result = await service.RecalculateAsync(cert.Id);
        cert = await service.ApplyCalculationAsync(cert.Id, result, cert.RowVersion, "test@empresa.cl");

        Assert.Equal(20_000m, cert.SubtotalValorizado);
        Assert.Equal(20_000m, cert.MontoBruto);
        Assert.Equal(23_800m, cert.MontoLiquido);
        Assert.NotEmpty(cert.Hash);
    }

    [Fact]
    public async Task CreateAdvanceAsync_ValidAdvance_CreatesSuccessfully()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var advance = new ContractAdvance
        {
            ContractId = ContractId.From("CTR-001"),
            Amount = 50_000m,
            AmortizationPercentage = 15m,
            Currency = "CLP"
        };

        var result = await service.CreateAdvanceAsync(advance, "test@empresa.cl");

        Assert.NotNull(result.Id);
        Assert.Equal(50_000m, result.InitialBalance);
        Assert.Equal(50_000m, result.CurrentBalance);
        Assert.Equal("Active", result.Status);
        Assert.Equal(0m, result.TotalAmortized);
    }

    [Fact]
    public async Task CreateRetentionAsync_ValidRetention_CreatesSuccessfully()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var retention = new ContractRetention
        {
            ContractId = ContractId.From("CTR-001"),
            RetentionType = RetentionType.Garantia,
            Percentage = 10m
        };

        var result = await service.CreateRetentionAsync(retention, "test@empresa.cl");

        Assert.NotNull(result.Id);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task ReleaseRetentionAsync_ValidRetention_ReleasesSuccessfully()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var retention = new ContractRetention
        {
            Id = ContractRetentionId.From("R1"),
            ContractId = ContractId.From("CTR-001"),
            RetentionType = RetentionType.Garantia,
            Percentage = 10m,
            Status = "Active"
        };
        deps.RetentionRepo.Retentions[retention.Id] = retention;

        var release = await service.ReleaseRetentionAsync(retention.Id, 5_000m, "authorizer@empresa.cl", "Liberación por término de obra");

        Assert.Equal(5_000m, release.ReleasedAmount);
        Assert.Equal("authorizer@empresa.cl", release.AuthorizedBy);
        Assert.Equal("Released", deps.RetentionRepo.Retentions[retention.Id].Status);
    }

    [Fact]
    public async Task AddObservationAsync_ValidObservation_CreatesSuccessfully()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var observation = new PaymentObservation
        {
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            Author = "Revisor",
            Comment = "Verificar cantidades en partida EL-01"
        };

        var result = await service.AddObservationAsync(observation, "rowver", "revisor@empresa.cl");

        Assert.NotNull(result.Id);
        Assert.Equal(PaymentObservationStatus.Abierta, result.Status);
        Assert.Equal("revisor@empresa.cl", result.Author);
    }

    [Fact]
    public async Task RespondObservationAsync_OpenObservation_RespondsSuccessfully()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var obsId = PaymentObservationId.From("OBS-001");
        var observation = new PaymentObservation
        {
            Id = obsId,
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            Author = "Revisor",
            Comment = "Verificar cantidades",
            Status = PaymentObservationStatus.Abierta
        };
        deps.ObservationRepo.Observations[obsId] = observation;

        var result = await service.RespondObservationAsync(obsId, "Cantidades corregidas en nueva medición", "creator@empresa.cl");

        Assert.Equal(PaymentObservationStatus.Respondida, result.Status);
        Assert.Equal("Cantidades corregidas en nueva medición", result.Response);
        Assert.Equal("creator@empresa.cl", result.Responder);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WithValidHash_ReturnsTrue()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var result = await service.VerifyIntegrityAsync(cert.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task GetAsync_ExistingCertificate_ReturnsWithCollections()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var loaded = await service.GetAsync(cert.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cert.Id.Value, loaded!.Id.Value);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingCertificates()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        await service.CreateAsync(draft, "test@empresa.cl");

        var results = await service.SearchAsync("/test/path", null, null, 0, 10);

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetByContractAsync_ReturnsContractCertificates()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        await service.CreateAsync(draft, "test@empresa.cl");

        var results = await service.GetByContractAsync(ContractId.From("CTR-001"));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetAuditLogAsync_ReturnsAuditEntries()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        var auditLog = await service.GetAuditLogAsync(cert.Id);

        Assert.NotEmpty(auditLog);
        Assert.Contains(auditLog, e => e.Action == "CREAR");
    }

    [Fact]
    public async Task GetAdvancesByContractAsync_ReturnsAdvances()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var advance = new ContractAdvance
        {
            Id = ContractAdvanceId.From("A1"),
            ContractId = ContractId.From("CTR-001"),
            Amount = 50_000m
        };
        deps.AdvanceRepo.Advances[advance.Id] = advance;

        var results = await service.GetAdvancesByContractAsync(ContractId.From("CTR-001"));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetRetentionsByContractAsync_ReturnsRetentions()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var retention = new ContractRetention
        {
            Id = ContractRetentionId.From("R1"),
            ContractId = ContractId.From("CTR-001"),
            Percentage = 10m
        };
        deps.RetentionRepo.Retentions[retention.Id] = retention;

        var results = await service.GetRetentionsByContractAsync(ContractId.From("CTR-001"));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task CompareRevisionsAsync_IdenticalHashes_ReturnsIsIdentical()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var v1Id = PaymentCertificateRevisionId.From("V1");
        var v2Id = PaymentCertificateRevisionId.From("V2");

        deps.RevisionRepo.Revisions[v1Id] = new PaymentCertificateRevision
        {
            Id = v1Id,
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            RevisionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "user1",
            SnapshotJson = "{}",
            Hash = "abc123"
        };

        deps.RevisionRepo.Revisions[v2Id] = new PaymentCertificateRevision
        {
            Id = v2Id,
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            RevisionNumber = 2,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "user2",
            SnapshotJson = "{}",
            Hash = "abc123"
        };

        var comparison = await service.CompareRevisionsAsync(v1Id, v2Id);

        Assert.True(comparison.IsIdentical);
        Assert.Empty(comparison.Differences);
    }

    [Fact]
    public async Task CompareRevisionsAsync_DifferentHashes_ReturnsDifferences()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var v1Id = PaymentCertificateRevisionId.From("V1");
        var v2Id = PaymentCertificateRevisionId.From("V2");

        deps.RevisionRepo.Revisions[v1Id] = new PaymentCertificateRevision
        {
            Id = v1Id,
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            RevisionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "user1",
            SnapshotJson = """{"SubtotalValorizado":10000}""",
            Hash = "abc"
        };

        deps.RevisionRepo.Revisions[v2Id] = new PaymentCertificateRevision
        {
            Id = v2Id,
            PaymentCertificateId = PaymentCertificateId.From("PC-001"),
            RevisionNumber = 2,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "user2",
            SnapshotJson = """{"SubtotalValorizado":20000}""",
            Hash = "def"
        };

        var comparison = await service.CompareRevisionsAsync(v1Id, v2Id);

        Assert.False(comparison.IsIdentical);
        Assert.NotEmpty(comparison.Differences);
    }

    [Fact]
    public async Task ConcurrencyCheck_WrongRowVersion_Throws()
    {
        var deps = new StubPaymentCertificateDependencies();
        var service = deps.CreateService();

        var draft = CreateDraft();
        var cert = await service.CreateAsync(draft, "test@empresa.cl");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitAsync(cert.Id, "wrong-row-version", "test@empresa.cl"));
    }
}
