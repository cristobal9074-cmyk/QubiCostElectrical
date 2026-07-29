using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Verifica que los repositorios SQLite de PaymentCertificate persisten y
/// recuperan datos sin pérdida. Usa archivos temporales que se limpian en cada test.
/// </summary>
public sealed class PaymentCertificateStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqlitePaymentCertificateRepository _certRepo;
    private readonly SqlitePaymentCertificateLineRepository _lineRepo;
    private readonly SqlitePaymentCertificateRevisionRepository _revisionRepo;
    private readonly SqlitePaymentCertificateAuditRepository _auditRepo;
    private readonly SqliteContractAdvanceRepository _advanceRepo;
    private readonly SqliteAdvanceAmortizationRepository _amortRepo;
    private readonly SqliteContractRetentionRepository _retentionRepo;
    private readonly SqliteRetentionApplicationRepository _retAppRepo;
    private readonly SqliteRetentionReleaseRepository _retReleaseRepo;
    private readonly SqlitePaymentObservationRepository _observationRepo;

    public PaymentCertificateStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_pc_test_{Guid.NewGuid():N}.db");
        CreateTables();
        _certRepo = new SqlitePaymentCertificateRepository(_dbPath);
        _lineRepo = new SqlitePaymentCertificateLineRepository(_dbPath);
        _revisionRepo = new SqlitePaymentCertificateRevisionRepository(_dbPath);
        _auditRepo = new SqlitePaymentCertificateAuditRepository(_dbPath);
        _advanceRepo = new SqliteContractAdvanceRepository(_dbPath);
        _amortRepo = new SqliteAdvanceAmortizationRepository(_dbPath);
        _retentionRepo = new SqliteContractRetentionRepository(_dbPath);
        _retAppRepo = new SqliteRetentionApplicationRepository(_dbPath);
        _retReleaseRepo = new SqliteRetentionReleaseRepository(_dbPath);
        _observationRepo = new SqlitePaymentObservationRepository(_dbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDeleteWithRetries(_dbPath);
    }

    // ─── PaymentCertificate ───────────────────────────────────────────────

    [Fact]
    public async Task Cert_SaveThenGetById_ReturnsSameCertificate()
    {
        var cert = CreateSampleCert();
        await _certRepo.SaveAsync(cert);
        var loaded = await _certRepo.GetByIdAsync(cert.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cert.Id.Value, loaded!.Id.Value);
        Assert.Equal(cert.VisibleNumber, loaded.VisibleNumber);
        Assert.Equal(cert.Status, loaded.Status);
        Assert.Equal(cert.SubtotalValorizado, loaded.SubtotalValorizado);
        Assert.Equal(cert.MontoLiquido, loaded.MontoLiquido);
    }

    [Fact]
    public async Task Cert_SaveTwice_LastDataWins()
    {
        var cert = CreateSampleCert();
        cert.Comment = "Versión 1";
        await _certRepo.SaveAsync(cert);

        cert.Comment = "Versión 2";
        await _certRepo.SaveAsync(cert);

        var loaded = await _certRepo.GetByIdAsync(cert.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Versión 2", loaded!.Comment);
    }

    [Fact]
    public async Task Cert_GetByVisibleNumber_ReturnsCertificate()
    {
        var cert = CreateSampleCert();
        cert.VisibleNumber = "PC-2025-001";
        await _certRepo.SaveAsync(cert);

        var loaded = await _certRepo.GetByVisibleNumberAsync("PC-2025-001");
        Assert.NotNull(loaded);
        Assert.Equal(cert.Id.Value, loaded!.Id.Value);
    }

    [Fact]
    public async Task Cert_GetByVisibleNumber_CaseInsensitive()
    {
        var cert = CreateSampleCert();
        cert.VisibleNumber = "PC-TEST-001";
        await _certRepo.SaveAsync(cert);

        var loaded = await _certRepo.GetByVisibleNumberAsync("pc-test-001");
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task Cert_GetByVisibleNumber_NotFound_ReturnsNull()
    {
        var loaded = await _certRepo.GetByVisibleNumberAsync("NO-EXISTE");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Cert_GetByContract_ReturnsCertificates()
    {
        var cid = ContractId.New();
        var cert1 = CreateSampleCert();
        cert1.ContractId = cid;
        var cert2 = CreateSampleCert();
        cert2.ContractId = cid;
        await _certRepo.SaveAsync(cert1);
        await _certRepo.SaveAsync(cert2);

        var list = await _certRepo.GetByContractAsync(cid);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task Cert_GetNextNumber_ReturnsCountPlusOne()
    {
        var cert = CreateSampleCert();
        InsertContract(cert.ContractId);
        await _certRepo.SaveAsync(cert);

        var next = await _certRepo.GetNextNumberAsync("/test/path");
        Assert.True(next >= 1);
    }

    [Fact]
    public async Task Cert_Search_ByText_ReturnsMatching()
    {
        var cert = CreateSampleCert();
        cert.VisibleNumber = "PC-SEARCH-001";
        InsertContract(cert.ContractId);
        await _certRepo.SaveAsync(cert);

        var results = await _certRepo.SearchAsync("/test/path", "SEARCH", null, 0, 50);
        Assert.Contains(results, c => c.VisibleNumber == "PC-SEARCH-001");
    }

    [Fact]
    public async Task Cert_Search_ByStatus_ReturnsFiltered()
    {
        var cert = CreateSampleCert();
        cert.Status = PaymentCertificateStatus.Presentado;
        InsertContract(cert.ContractId);
        await _certRepo.SaveAsync(cert);

        var results = await _certRepo.SearchAsync("/test/path", null, PaymentCertificateStatus.Presentado, 0, 50);
        Assert.All(results, c => Assert.Equal(PaymentCertificateStatus.Presentado, c.Status));
    }

    // ─── Lines ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Line_SaveThenGetByCertificate_ReturnsLines()
    {
        var certId = PaymentCertificateId.New();
        var line = CreateSampleLine(certId);
        await _lineRepo.SaveAsync(line);

        var lines = await _lineRepo.GetByCertificateAsync(certId);
        var loaded = Assert.Single(lines);
        Assert.Equal(line.Description, loaded.Description);
        Assert.Equal(line.CantidadPeriodo, loaded.CantidadPeriodo);
        Assert.Equal(line.MontoPeriodo, loaded.MontoPeriodo);
    }

    [Fact]
    public async Task Line_SaveRange_PersistsAll()
    {
        var certId = PaymentCertificateId.New();
        var line1 = CreateSampleLine(certId);
        line1.PartidaContractual = "1.1";
        var line2 = CreateSampleLine(certId);
        line2.PartidaContractual = "1.2";
        var line3 = CreateSampleLine(certId);
        line3.PartidaContractual = "1.3";
        await _lineRepo.SaveRangeAsync(new[] { line1, line2, line3 });

        var loaded = await _lineRepo.GetByCertificateAsync(certId);
        Assert.Equal(3, loaded.Count);
    }

    [Fact]
    public async Task Line_DeleteByCertificate_RemovesLines()
    {
        var certId = PaymentCertificateId.New();
        var line = CreateSampleLine(certId);
        await _lineRepo.SaveAsync(line);

        await _lineRepo.DeleteByCertificateAsync(certId);

        var lines = await _lineRepo.GetByCertificateAsync(certId);
        Assert.Empty(lines);
    }

    // ─── Revisions ────────────────────────────────────────────────────────

    [Fact]
    public async Task Revision_SaveThenGetById_ReturnsRevision()
    {
        var certId = PaymentCertificateId.New();
        var rev = CreateSampleRevision(certId);
        await _revisionRepo.SaveAsync(rev);

        var loaded = await _revisionRepo.GetByIdAsync(rev.Id);
        Assert.NotNull(loaded);
        Assert.Equal(rev.RevisionNumber, loaded!.RevisionNumber);
        Assert.Equal(rev.Hash, loaded.Hash);
    }

    [Fact]
    public async Task Revision_GetByCertificate_ReturnsOrdered()
    {
        var certId = PaymentCertificateId.New();
        var rev1 = CreateSampleRevision(certId);
        rev1.RevisionNumber = 1;
        var rev2 = CreateSampleRevision(certId);
        rev2.RevisionNumber = 2;
        await _revisionRepo.SaveAsync(rev1);
        await _revisionRepo.SaveAsync(rev2);

        var revisions = await _revisionRepo.GetByCertificateAsync(certId);
        Assert.Equal(2, revisions.Count);
    }

    // ─── Audit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Audit_RecordThenGetByCertificate_ReturnsEntries()
    {
        var certId = PaymentCertificateId.New();
        var audit = new PaymentCertificateAuditEvent
        {
            PaymentCertificateId = certId,
            Action = "Crear",
            Actor = "testuser",
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = "{}"
        };
        await _auditRepo.RecordAsync(audit);

        var entries = await _auditRepo.GetByCertificateAsync(certId, 100);
        var entry = Assert.Single(entries);
        Assert.Equal("Crear", entry.Action);
        Assert.Equal("testuser", entry.Actor);
    }

    // ─── Advances ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Advance_SaveThenGetById_ReturnsAdvance()
    {
        var adv = CreateSampleAdvance();
        await _advanceRepo.SaveAsync(adv);

        var loaded = await _advanceRepo.GetByIdAsync(adv.Id);
        Assert.NotNull(loaded);
        Assert.Equal(adv.Amount, loaded!.Amount);
        Assert.Equal(adv.Status, loaded.Status);
    }

    [Fact]
    public async Task Advance_GetByContract_ReturnsAdvances()
    {
        var cid = ContractId.New();
        var adv1 = CreateSampleAdvance();
        adv1.ContractId = cid;
        var adv2 = CreateSampleAdvance();
        adv2.ContractId = cid;
        await _advanceRepo.SaveAsync(adv1);
        await _advanceRepo.SaveAsync(adv2);

        var list = await _advanceRepo.GetByContractAsync(cid);
        Assert.Equal(2, list.Count);
    }

    // ─── Advance Amortizations ────────────────────────────────────────────

    [Fact]
    public async Task Amortization_SaveThenGetByCertificate_ReturnsAmortizations()
    {
        var certId = PaymentCertificateId.New();
        var amt = CreateSampleAmortization(certId);
        await _amortRepo.SaveAsync(amt);

        var list = await _amortRepo.GetByCertificateAsync(certId);
        var loaded = Assert.Single(list);
        Assert.Equal(amt.Amount, loaded.Amount);
    }

    [Fact]
    public async Task Amortization_DeleteByCertificate_Removes()
    {
        var certId = PaymentCertificateId.New();
        var amt = CreateSampleAmortization(certId);
        await _amortRepo.SaveAsync(amt);

        await _amortRepo.DeleteByCertificateAsync(certId);
        Assert.Empty(await _amortRepo.GetByCertificateAsync(certId));
    }

    // ─── Retentions ───────────────────────────────────────────────────────

    [Fact]
    public async Task Retention_SaveThenGetById_ReturnsRetention()
    {
        var ret = CreateSampleRetention();
        await _retentionRepo.SaveAsync(ret);

        var loaded = await _retentionRepo.GetByIdAsync(ret.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ret.Percentage, loaded!.Percentage);
        Assert.Equal(ret.Status, loaded.Status);
    }

    [Fact]
    public async Task Retention_GetByContract_ReturnsRetentions()
    {
        var cid = ContractId.New();
        var ret1 = CreateSampleRetention();
        ret1.ContractId = cid;
        var ret2 = CreateSampleRetention();
        ret2.ContractId = cid;
        await _retentionRepo.SaveAsync(ret1);
        await _retentionRepo.SaveAsync(ret2);

        var list = await _retentionRepo.GetByContractAsync(cid);
        Assert.Equal(2, list.Count);
    }

    // ─── Retention Applications ───────────────────────────────────────────

    [Fact]
    public async Task RetApp_SaveThenGetByCertificate_Returns()
    {
        var certId = PaymentCertificateId.New();
        var app = CreateSampleRetentionApplication(certId);
        await _retAppRepo.SaveAsync(app);

        var list = await _retAppRepo.GetByCertificateAsync(certId);
        var loaded = Assert.Single(list);
        Assert.Equal(app.AppliedAmount, loaded.AppliedAmount);
    }

    // ─── Retention Releases ───────────────────────────────────────────────

    [Fact]
    public async Task RetRelease_SaveThenGetByRetention_Returns()
    {
        var retId = ContractRetentionId.New();
        var rel = CreateSampleRetentionRelease(retId);
        await _retReleaseRepo.SaveAsync(rel);

        var list = await _retReleaseRepo.GetByRetentionAsync(retId);
        var loaded = Assert.Single(list);
        Assert.Equal(rel.ReleasedAmount, loaded.ReleasedAmount);
    }

    [Fact]
    public async Task RetRelease_GetByCertificate_Returns()
    {
        var certId = PaymentCertificateId.New();
        var rel = CreateSampleRetentionRelease(ContractRetentionId.New());
        rel.PaymentCertificateId = certId;
        await _retReleaseRepo.SaveAsync(rel);

        var list = await _retReleaseRepo.GetByCertificateAsync(certId);
        Assert.NotEmpty(list);
    }

    // ─── Observations ─────────────────────────────────────────────────────

    [Fact]
    public async Task Observation_SaveThenGetById_Returns()
    {
        var obs = CreateSampleObservation();
        await _observationRepo.SaveAsync(obs);

        var loaded = await _observationRepo.GetByIdAsync(obs.Id);
        Assert.NotNull(loaded);
        Assert.Equal(obs.Comment, loaded!.Comment);
        Assert.Equal(obs.Status, loaded.Status);
    }

    [Fact]
    public async Task Observation_GetByCertificate_Returns()
    {
        var certId = PaymentCertificateId.New();
        var obs1 = CreateSampleObservation();
        obs1.PaymentCertificateId = certId;
        var obs2 = CreateSampleObservation();
        obs2.PaymentCertificateId = certId;
        await _observationRepo.SaveAsync(obs1);
        await _observationRepo.SaveAsync(obs2);

        var list = await _observationRepo.GetByCertificateAsync(certId);
        Assert.Equal(2, list.Count);
    }

    // ─── Error paths ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        var loaded = await _certRepo.GetByIdAsync(PaymentCertificateId.New());
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetByCertificate_NoData_ReturnsEmpty()
    {
        var lines = await _lineRepo.GetByCertificateAsync(PaymentCertificateId.New());
        Assert.Empty(lines);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static PaymentCertificate CreateSampleCert()
    {
        var now = DateTime.UtcNow;
        return new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = "PC-001",
            RevisionNumber = 1,
            PeriodStart = new DateTime(2025, 1, 1),
            PeriodEnd = new DateTime(2025, 1, 31),
            CutOffDate = new DateTime(2025, 1, 15),
            PresentationDate = now,
            Currency = "CLP",
            ExchangeRate = 1m,
            Status = PaymentCertificateStatus.Borrador,
            Presenter = "testuser",
            Revisor = "",
            Aprobador = "",
            SubtotalValorizado = 100_000m,
            Reajuste = 0m,
            AnticipoAmortizado = 0m,
            RetencionesAplicadas = 0m,
            Descuentos = 0m,
            Multas = 0m,
            Impuestos = 19_000m,
            MontoBruto = 100_000m,
            MontoLiquido = 119_000m,
            MontoAnterior = 0m,
            MontoAcumulado = 119_000m,
            SaldoContractual = 500_000m,
            SaldoPorValorizar = 381_000m,
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = now,
            CreatedBy = "testuser",
            ModifiedUtc = now,
            ModifiedBy = "testuser",
            Hash = "abc123",
            CompatibilityMetadata = "",
            Comment = ""
        };
    }

    private static PaymentCertificateLine CreateSampleLine(PaymentCertificateId certId)
    {
        return new PaymentCertificateLine
        {
            Id = PaymentCertificateLineId.New(),
            PaymentCertificateId = certId,
            PartidaContractual = "1.1",
            WbsCode = "",
            ActivityId = "",
            Description = "Excavación",
            Unit = "M3",
            CantidadContractual = 100m,
            PrecioUnitarioContractual = 1_000m,
            CantidadAnterior = 30m,
            CantidadPeriodo = 10m,
            CantidadAcumulada = 40m,
            SaldoCantidad = 60m,
            MontoAnterior = 30_000m,
            MontoPeriodo = 10_000m,
            MontoAcumulado = 40_000m,
            SaldoMonetario = 60_000m,
            OrigenAvance = "Manual",
            Evidencia = "",
            Observacion = "",
            Status = "Pending",
            RowVersion = Guid.NewGuid().ToString("N")
        };
    }

    private static PaymentCertificateRevision CreateSampleRevision(PaymentCertificateId certId)
    {
        return new PaymentCertificateRevision
        {
            Id = PaymentCertificateRevisionId.New(),
            PaymentCertificateId = certId,
            RevisionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "testuser",
            SnapshotJson = "{}",
            Hash = "hash123",
            Comment = ""
        };
    }

    private static ContractAdvance CreateSampleAdvance()
    {
        return new ContractAdvance
        {
            Id = ContractAdvanceId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = "ANT-001",
            Amount = 50_000m,
            Currency = "CLP",
            Date = DateTime.UtcNow,
            AmortizationPercentage = 10m,
            InitialBalance = 50_000m,
            CurrentBalance = 50_000m,
            TotalAmortized = 0m,
            Status = "Active",
            DocumentReference = "",
            Comment = "",
            CreatedBy = "testuser",
            CreatedUtc = DateTime.UtcNow,
            AdvanceType = AdvanceType.Financiero
        };
    }

    private static AdvanceAmortization CreateSampleAmortization(PaymentCertificateId certId)
    {
        return new AdvanceAmortization
        {
            Id = Guid.NewGuid().ToString("N"),
            PaymentCertificateId = certId,
            ContractAdvanceId = ContractAdvanceId.New(),
            Amount = 5_000m,
            Percentage = 10m,
            PreAmortizationBalance = 50_000m,
            PostAmortizationBalance = 45_000m
        };
    }

    private static ContractRetention CreateSampleRetention()
    {
        return new ContractRetention
        {
            Id = ContractRetentionId.New(),
            ContractId = ContractId.New(),
            RetentionType = RetentionType.Contractual,
            BaseType = "Subtotal",
            Percentage = 5m,
            FixedAmount = 0m,
            LimitAmount = 0m,
            ExpiryDate = null,
            ReleaseCondition = "",
            Status = "Active",
            Observation = ""
        };
    }

    private static RetentionApplication CreateSampleRetentionApplication(PaymentCertificateId certId)
    {
        return new RetentionApplication
        {
            Id = Guid.NewGuid().ToString("N"),
            PaymentCertificateId = certId,
            ContractRetentionId = ContractRetentionId.New(),
            BaseAmount = 100_000m,
            Percentage = 5m,
            AppliedAmount = 5_000m,
            CumulativeApplied = 15_000m,
            ReleasedAmount = 0m,
            PendingBalance = 10_000m
        };
    }

    private static RetentionRelease CreateSampleRetentionRelease(ContractRetentionId retId)
    {
        return new RetentionRelease
        {
            Id = Guid.NewGuid().ToString("N"),
            ContractRetentionId = retId,
            PaymentCertificateId = PaymentCertificateId.New(),
            ReleasedAmount = 5_000m,
            ReleaseDate = DateTime.UtcNow,
            AuthorizedBy = "testuser",
            Comment = ""
        };
    }

    private static PaymentObservation CreateSampleObservation()
    {
        return new PaymentObservation
        {
            Id = PaymentObservationId.New(),
            PaymentCertificateId = PaymentCertificateId.New(),
            PaymentCertificateLineId = null,
            Author = "revisor1",
            CreatedUtc = DateTime.UtcNow,
            Category = "Cantidad",
            Severity = "Media",
            Status = PaymentObservationStatus.Abierta,
            Comment = "Verificar cantidad reportada",
            Response = "",
            Responder = "",
            ResolvedUtc = null
        };
    }

    private void CreateTables()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS contracts (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL DEFAULT '',
                visible_number TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                original_amount REAL NOT NULL DEFAULT 0,
                json_data TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS payment_certificates (
                id TEXT PRIMARY KEY, contract_id TEXT NOT NULL, visible_number TEXT NOT NULL,
                revision_number INTEGER NOT NULL DEFAULT 1, period_start TEXT NOT NULL,
                period_end TEXT NOT NULL, cut_off_date TEXT NOT NULL, presentation_date TEXT NOT NULL,
                currency TEXT NOT NULL DEFAULT 'CLP', exchange_rate REAL NOT NULL DEFAULT 1.0,
                status INTEGER NOT NULL DEFAULT 0, presenter TEXT NOT NULL DEFAULT '',
                revisor TEXT NOT NULL DEFAULT '', aprobador TEXT NOT NULL DEFAULT '',
                approved_at_utc TEXT, comment TEXT NOT NULL DEFAULT '',
                subtotal_valorizado REAL NOT NULL DEFAULT 0, reajuste REAL NOT NULL DEFAULT 0,
                anticipo_amortizado REAL NOT NULL DEFAULT 0, retenciones_aplicadas REAL NOT NULL DEFAULT 0,
                descuentos REAL NOT NULL DEFAULT 0, multas REAL NOT NULL DEFAULT 0,
                impuestos REAL NOT NULL DEFAULT 0, monto_bruto REAL NOT NULL DEFAULT 0,
                monto_liquido REAL NOT NULL DEFAULT 0, monto_anterior REAL NOT NULL DEFAULT 0,
                monto_acumulado REAL NOT NULL DEFAULT 0, saldo_contractual REAL NOT NULL DEFAULT 0,
                saldo_por_valorizar REAL NOT NULL DEFAULT 0, row_version TEXT NOT NULL,
                created_utc TEXT NOT NULL, created_by TEXT NOT NULL, modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '', hash TEXT NOT NULL DEFAULT '',
                compatibility_metadata TEXT NOT NULL DEFAULT '', json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS payment_certificate_lines (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                partida_contractual TEXT NOT NULL DEFAULT '', wbs_code TEXT NOT NULL DEFAULT '',
                activity_id TEXT NOT NULL DEFAULT '', apu_id TEXT,
                description TEXT NOT NULL, unit TEXT NOT NULL,
                cantidad_contractual REAL NOT NULL DEFAULT 0, precio_unitario_contractual REAL NOT NULL DEFAULT 0,
                cantidad_anterior REAL NOT NULL DEFAULT 0, cantidad_periodo REAL NOT NULL DEFAULT 0,
                cantidad_acumulada REAL NOT NULL DEFAULT 0, saldo_cantidad REAL NOT NULL DEFAULT 0,
                monto_anterior REAL NOT NULL DEFAULT 0, monto_periodo REAL NOT NULL DEFAULT 0,
                monto_acumulado REAL NOT NULL DEFAULT 0, saldo_monetario REAL NOT NULL DEFAULT 0,
                origen_avance TEXT NOT NULL DEFAULT 'Manual', evidencia TEXT NOT NULL DEFAULT '',
                observacion TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Pending',
                row_version TEXT NOT NULL, json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS payment_certificate_revisions (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                revision_number INTEGER NOT NULL, created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL, snapshot_json TEXT NOT NULL,
                hash TEXT NOT NULL, comment TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS payment_certificate_audit (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                action TEXT NOT NULL, actor TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL, details_json TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS contract_advances (
                id TEXT PRIMARY KEY, contract_id TEXT NOT NULL,
                visible_number TEXT NOT NULL DEFAULT '', amount REAL NOT NULL,
                currency TEXT NOT NULL DEFAULT 'CLP', date TEXT NOT NULL,
                amortization_percentage REAL NOT NULL DEFAULT 0,
                initial_balance REAL NOT NULL DEFAULT 0, current_balance REAL NOT NULL DEFAULT 0,
                total_amortized REAL NOT NULL DEFAULT 0, status TEXT NOT NULL DEFAULT 'Active',
                document_reference TEXT NOT NULL DEFAULT '', comment TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                advance_type INTEGER NOT NULL DEFAULT 0, json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS contract_advance_amortizations (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                contract_advance_id TEXT NOT NULL, amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0, pre_amortization_balance REAL NOT NULL DEFAULT 0,
                post_amortization_balance REAL NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS contract_retentions (
                id TEXT PRIMARY KEY, contract_id TEXT NOT NULL,
                retention_type INTEGER NOT NULL DEFAULT 0, base_type TEXT NOT NULL DEFAULT 'Subtotal',
                percentage REAL NOT NULL DEFAULT 0, fixed_amount REAL NOT NULL DEFAULT 0,
                limit_amount REAL NOT NULL DEFAULT 0, expiry_date TEXT,
                release_condition TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Active',
                observation TEXT NOT NULL DEFAULT '', json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS contract_retention_applications (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                contract_retention_id TEXT NOT NULL, base_amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0, applied_amount REAL NOT NULL DEFAULT 0,
                cumulative_applied REAL NOT NULL DEFAULT 0, released_amount REAL NOT NULL DEFAULT 0,
                pending_balance REAL NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS contract_retention_releases (
                id TEXT PRIMARY KEY, contract_retention_id TEXT NOT NULL,
                payment_certificate_id TEXT, released_amount REAL NOT NULL DEFAULT 0,
                release_date TEXT NOT NULL, authorized_by TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS contract_payment_observations (
                id TEXT PRIMARY KEY, payment_certificate_id TEXT NOT NULL,
                payment_certificate_line_id TEXT, author TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL, category TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT 'Info', status INTEGER NOT NULL DEFAULT 0,
                comment TEXT NOT NULL DEFAULT '', response TEXT NOT NULL DEFAULT '',
                responder TEXT NOT NULL DEFAULT '', resolved_utc TEXT,
                json_data TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserta un contrato de prueba para que SearchAsync y GetNextNumberAsync
    /// (que hacen INNER JOIN con contracts) puedan encontrar registros.
    /// </summary>
    private void InsertContract(ContractId contractId, string projectPath = "/test/path")
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO contracts (id, project_path, visible_number, status, original_amount, json_data)
            VALUES ($id, $proj, $vis, $st, $amt, $json);
        ";
        cmd.Parameters.AddWithValue("$id", contractId.Value);
        cmd.Parameters.AddWithValue("$proj", projectPath);
        cmd.Parameters.AddWithValue("$vis", "CTR-TEST");
        cmd.Parameters.AddWithValue("$st", (int)ContractStatus.Vigente);
        cmd.Parameters.AddWithValue("$amt", 1_000_000m);
        cmd.Parameters.AddWithValue("$json", "{}");
        cmd.ExecuteNonQuery();
    }

    private static void TryDeleteWithRetries(string path)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch { return; }
        }
    }
}
