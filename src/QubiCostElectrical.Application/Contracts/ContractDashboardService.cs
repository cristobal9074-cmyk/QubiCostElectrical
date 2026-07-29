using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ContractDashboardService : IContractDashboardService
{
    private readonly IContractRepository _contractRepo;
    private readonly IContractVersionRepository _versionRepo;
    private readonly IPaymentCertificateRepository _certRepo;
    private readonly IContractAdvanceRepository _advanceRepo;
    private readonly IContractRetentionRepository _retentionRepo;
    private readonly IChangeRequestRepository _changeRequestRepo;
    private readonly IChangeOrderRepository _changeOrderRepo;
    private readonly IContractClaimRepository _claimRepo;
    private readonly IExtraWorkRepository _extraWorkRepo;
    private readonly IContractGuaranteeRepository _guaranteeRepo;
    private readonly IContractPenaltyRepository _penaltyRepo;
    private readonly IContractInvoiceRepository _invoiceRepo;
    private readonly IContractAdjustmentRepository _adjustmentRepo;
    private readonly IContractDashboardRepository _dashRepo;
    private readonly IContractDashboardPermissionChecker _perms;
    private readonly IPermissionService _basePerms;

    public ContractDashboardService(
        IContractRepository contractRepo,
        IContractVersionRepository versionRepo,
        IPaymentCertificateRepository certRepo,
        IContractAdvanceRepository advanceRepo,
        IContractRetentionRepository retentionRepo,
        IChangeRequestRepository changeRequestRepo,
        IChangeOrderRepository changeOrderRepo,
        IContractClaimRepository claimRepo,
        IExtraWorkRepository extraWorkRepo,
        IContractGuaranteeRepository guaranteeRepo,
        IContractPenaltyRepository penaltyRepo,
        IContractInvoiceRepository invoiceRepo,
        IContractAdjustmentRepository adjustmentRepo,
        IContractDashboardRepository dashRepo,
        IContractDashboardPermissionChecker perms,
        IPermissionService basePerms)
    {
        _contractRepo = contractRepo;
        _versionRepo = versionRepo;
        _certRepo = certRepo;
        _advanceRepo = advanceRepo;
        _retentionRepo = retentionRepo;
        _changeRequestRepo = changeRequestRepo;
        _changeOrderRepo = changeOrderRepo;
        _claimRepo = claimRepo;
        _extraWorkRepo = extraWorkRepo;
        _guaranteeRepo = guaranteeRepo;
        _penaltyRepo = penaltyRepo;
        _invoiceRepo = invoiceRepo;
        _adjustmentRepo = adjustmentRepo;
        _dashRepo = dashRepo;
        _perms = perms;
        _basePerms = basePerms;
    }

    public async Task<ContractExecutiveDashboard> GetExecutiveAsync(
        ContractId contractId, ContractDashboardFilter? filter, string actor, CancellationToken ct)
    {
        var contract = await _contractRepo.GetByIdAsync(contractId, ct)
            ?? throw new KeyNotFoundException("Contrato no encontrado.");

        var certs = await _certRepo.GetByContractAsync(contractId, ct);
        var advances = await _advanceRepo.GetByContractAsync(contractId, ct);
        var retentions = await _retentionRepo.GetByContractAsync(contractId, ct);
        var changeOrders = await _changeOrderRepo.GetByContractAsync(contractId, ct);
        var claims = await _claimRepo.GetByContractAsync(contractId, ct);
        var extras = await _extraWorkRepo.GetByContractAsync(contractId, ct);
        var adjustments = await _adjustmentRepo.GetByContractAsync(contractId, ct);
        var guarantees = await _guaranteeRepo.GetByContractAsync(contractId, ct);
        var penalties = await _penaltyRepo.GetByContractAsync(contractId, ct);
        var invoices = await _invoiceRepo.GetByContractAsync(contractId, ct);
        var versions = await _versionRepo.GetByContractAsync(contractId, ct);

        var finSummary = BuildFinancialSummary(contract, certs, advances, retentions, changeOrders, adjustments, claims, penalties);
        var timeSummary = BuildTimeSummary(contract, versions, changeOrders, claims);
        var changeSummary = BuildChangeSummary(changeOrders, extras);
        var paymentSummary = BuildPaymentSummary(certs);
        var riskSummary = BuildRiskSummary(claims, guarantees, penalties, invoices);
        var adminSummary = BuildAdminSummary(adjustments, penalties, guarantees, invoices);
        var evmSummary = BuildEvmSummary(contract, certs);

        var kpis = BuildKpis(contract, finSummary, timeSummary, changeSummary, paymentSummary, riskSummary, adminSummary, evmSummary);
        var alerts = BuildAlerts(contract, certs, guarantees, penalties, invoices, adjustments, changeOrders, claims, versions);
        var series = BuildSeries(certs, changeOrders, claims);

        var quality = ComputeQuality(contract, certs, versions, advances, retentions);

        return new ContractExecutiveDashboard
        {
            ContractId = contractId,
            ContractCode = contract.VisibleNumber,
            ContractName = contract.Title ?? contract.VisibleNumber,
            CurrencyCode = contract.CurrencyCode,
            StatusDate = DateTime.UtcNow,
            DataQuality = quality,
            ExecutiveSummary = BuildSummary(contract, finSummary, timeSummary),
            FinancialSummary = finSummary,
            TimeSummary = timeSummary,
            ChangeSummary = changeSummary,
            PaymentSummary = paymentSummary,
            RiskSummary = riskSummary,
            AdministrationSummary = adminSummary,
            EvmSummary = evmSummary,
            Kpis = kpis,
            Alerts = alerts,
            Series = series,
            GeneratedUtc = DateTime.UtcNow
        };
    }

    // -- Builders, KPIs, Alerts, Series in partial files --

    public async Task<List<ContractDashboardSnapshot>> ListSnapshotsAsync(ContractId contractId, CancellationToken ct)
        => await _dashRepo.ListSnapshotsAsync(contractId, ct);

    public async Task<ContractDashboardSnapshot> CreateSnapshotAsync(
        ContractId contractId, string comment, string actor, CancellationToken ct)
    {
        var dash = await GetExecutiveAsync(contractId, null, actor, ct);
        var json = System.Text.Json.JsonSerializer.Serialize(dash);
        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        var snapshots = await _dashRepo.ListSnapshotsAsync(contractId, ct);
        var version = snapshots.Count > 0 ? snapshots.Max(s => s.Version) + 1 : 1;

        var snapshot = new ContractDashboardSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            ContractId = contractId,
            Version = version,
            StatusDate = DateTime.UtcNow,
            StatusLabel = $"v{version}",
            Sha256 = sha256,
            Author = actor,
            Comment = comment,
            CreatedUtc = DateTime.UtcNow
        };

        await _dashRepo.SaveSnapshotAsync(snapshot, json, ct);
        return snapshot;
    }

    public async Task<ContractComparisonResult> CompareSnapshotsAsync(
        string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct)
    {
        var snapA = await _dashRepo.GetSnapshotAsync(snapshotIdA, ct)
            ?? throw new KeyNotFoundException("Snapshot A no encontrado.");
        var snapB = await _dashRepo.GetSnapshotAsync(snapshotIdB, ct)
            ?? throw new KeyNotFoundException("Snapshot B no encontrado.");

        var findings = new List<string>();
        if (snapA.FinancialSummary.CurrentAmount != snapB.FinancialSummary.CurrentAmount)
        {
            var diff = snapB.FinancialSummary.CurrentAmount - snapA.FinancialSummary.CurrentAmount;
            findings.Add($"Monto vigente cambió de {snapA.FinancialSummary.CurrentAmount:N0} a {snapB.FinancialSummary.CurrentAmount:N0} ({diff:+0;-0} {snapA.FinancialSummary.CurrencyCode})");
        }

        return new ContractComparisonResult
        {
            Scope = ContractComparisonScope.StatusDate,
            LabelA = snapA.StatusDate.ToString("yyyy-MM-dd HH:mm"),
            LabelB = snapB.StatusDate.ToString("yyyy-MM-dd HH:mm"),
            AmountChange = snapB.FinancialSummary.CurrentAmount - snapA.FinancialSummary.CurrentAmount,
            Findings = findings,
            NewAlerts = snapB.Alerts.Select(a => a.Title).Except(snapA.Alerts.Select(a => a.Title)).ToList()
        };
    }

    public async Task AcknowledgeAlertAsync(string alertId, string actor, string? resolution, CancellationToken ct)
    {
        var alert = await _dashRepo.GetAlertAsync(alertId, ct)
            ?? throw new KeyNotFoundException("Alerta no encontrada.");
        if (alert.Status != ContractAlertStatus.New)
            throw new InvalidOperationException("Solo se pueden reconocer alertas en estado New.");

        await _dashRepo.UpdateAlertStatusAsync(alertId, ContractAlertStatus.Acknowledged, actor, ct);
    }

    public async Task ResolveAlertAsync(string alertId, string actor, string resolution, CancellationToken ct)
    {
        var alert = await _dashRepo.GetAlertAsync(alertId, ct)
            ?? throw new KeyNotFoundException("Alerta no encontrada.");
        if (alert.Status != ContractAlertStatus.Acknowledged)
            throw new InvalidOperationException("Solo se pueden resolver alertas reconocidas.");

        await _dashRepo.UpdateAlertStatusAsync(alertId, ContractAlertStatus.Resolved, actor, ct);
    }

    public async Task<string> ExportDashboardCsvAsync(ContractId contractId, CancellationToken ct)
    {
        var dash = await GetExecutiveAsync(contractId, null, "System", ct);
        var sb = new StringBuilder();
        sb.AppendLine("Código,KPI,Valor,Unidad,Estado,Categoría");
        foreach (var k in dash.Kpis)
            sb.AppendLine($"{k.Code},{k.Title},{k.DisplayValue},{k.Unit},{k.Status},{k.Category}");
        return sb.ToString();
    }

    public async Task<string> ExportDashboardJsonAsync(ContractId contractId, CancellationToken ct)
    {
        var dash = await GetExecutiveAsync(contractId, null, "System", ct);
        return System.Text.Json.JsonSerializer.Serialize(dash, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
