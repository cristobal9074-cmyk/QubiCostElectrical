using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ContractDashboard;

public sealed partial class ContractDashboardViewModel
{
    private static readonly CancellationToken s_ct = CancellationToken.None;

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var svc = _host.Services.ContractDashboardService;
            var contractRepo = _host.Services.ContractRepository;
            if (svc is null)
            {
                StatusMessage = "Servicio de dashboard contractual no disponible.";
                return;
            }

            var contracts = await contractRepo.SearchAsync(
                _host.Session.CurrentProjectPath ?? string.Empty,
                null, null, 0, int.MaxValue, s_ct);
            var contract = contracts.FirstOrDefault();
            if (contract is null)
            {
                StatusMessage = "No hay contratos en el proyecto.";
                return;
            }

            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var dash = await svc.GetExecutiveAsync(contract.Id, null, actor, s_ct);

            Header = $"{contract.Title} · {contract.CurrencyCode} · {dash.FinancialSummary.Quality}";
            Summary = $"Monto original: {dash.FinancialSummary.OriginalAmount:N2} · Actual: {dash.FinancialSummary.CurrentAmount:N2} · Pagado: {dash.FinancialSummary.PaidAmount:N2}";
            Quality = dash.FinancialSummary.Quality.ToString();

            _kpis.Clear();
            foreach (var k in dash.Kpis)
            {
                _kpis.Add(new ContractDashboardKpiRow
                {
                    Code = k.Code,
                    Title = k.Title,
                    DisplayValue = k.DisplayValue + (string.IsNullOrEmpty(k.Unit) ? "" : " " + k.Unit),
                    Status = k.Status.ToString(),
                    Interpretation = k.Interpretation,
                    Trend = k.Trend.ToString(),
                    Category = k.Category,
                    Icon = k.Status switch
                    {
                        ContractDashboardKpiStatus.Good => "✓",
                        ContractDashboardKpiStatus.Warning => "!",
                        ContractDashboardKpiStatus.Critical => "✕",
                        ContractDashboardKpiStatus.NoData => "?",
                        _ => "●"
                    }
                });
            }

            _alerts.Clear();
            foreach (var a in dash.Alerts)
            {
                _alerts.Add(new ContractDashboardAlertRow
                {
                    AlertId = a.AlertId,
                    Code = a.Code,
                    Severity = a.Severity.ToString(),
                    Title = a.Title,
                    Description = a.Description,
                    Status = a.Status.ToString(),
                    Responsible = a.Responsible,
                    CreatedUtc = a.CreatedUtc.ToString("g"),
                    Impact = a.Impact
                });
            }

            _snapshots.Clear();
            foreach (var s in await svc.ListSnapshotsAsync(contract.Id, s_ct))
            {
                _snapshots.Add(new ContractSnapshotRow
                {
                    SnapshotId = s.SnapshotId,
                    Version = s.Version,
                    StatusDate = s.StatusDate.ToString("g"),
                    Author = s.Author,
                    Comment = s.Comment,
                    CreatedUtc = s.CreatedUtc.ToString("g")
                });
            }

            StatusMessage = $"Dashboard generado · {dash.Kpis.Count} KPIs · {dash.Alerts.Count} alertas";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private async Task CreateSnapshotAsync()
    {
        try
        {
            var svc = _host.Services.ContractDashboardService;
            var contractRepo = _host.Services.ContractRepository;
            if (svc is null) return;

            var contracts = await contractRepo.SearchAsync(
                _host.Session.CurrentProjectPath ?? string.Empty,
                null, null, 0, int.MaxValue, s_ct);
            var contract = contracts.FirstOrDefault();
            if (contract is null) return;

            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await svc.CreateSnapshotAsync(contract.Id, "Snapshot manual", actor, s_ct);
            await LoadAsync();
            StatusMessage = "Snapshot creado exitosamente.";
        }
        catch (Exception ex) { StatusMessage = $"Error al crear snapshot: {ex.Message}"; }
    }

    private async Task CompareAsync()
    {
        try
        {
            if (SnapshotA is null || SnapshotB is null) return;

            var svc = _host.Services.ContractDashboardService;
            if (svc is null) return;

            var result = await svc.CompareSnapshotsAsync(
                SnapshotA.SnapshotId, SnapshotB.SnapshotId, "System", s_ct);

            _comparisonResults.Clear();
            _comparisonResults.Add(new ContractComparisonResultRow
            {
                Scope = result.Scope.ToString(),
                LabelA = result.LabelA,
                LabelB = result.LabelB,
                AmountChange = result.AmountChange.ToString("N2"),
                DaysChange = result.DaysChange.ToString(),
                Findings = string.Join("; ", result.Findings)
            });

            ComparisonText = $"Comparación: {result.LabelA} vs {result.LabelB} · {result.NewAlerts.Count} alertas nuevas";
        }
        catch (Exception ex) { ComparisonText = $"Error en comparación: {ex.Message}"; }
    }

    private async Task ExportAsync()
    {
        try
        {
            var svc = _host.Services.ContractDashboardService;
            var contractRepo = _host.Services.ContractRepository;
            if (svc is null) return;

            var contracts = await contractRepo.SearchAsync(
                _host.Session.CurrentProjectPath ?? string.Empty,
                null, null, 0, int.MaxValue, s_ct);
            var contract = contracts.FirstOrDefault();
            if (contract is null) return;

            var csv = await svc.ExportDashboardCsvAsync(contract.Id, s_ct);
            StatusMessage = $"Exportación generada: {csv.Length} caracteres";
        }
        catch (Exception ex) { StatusMessage = $"Error al exportar: {ex.Message}"; }
    }

    private async Task AckAlertAsync()
    {
        try
        {
            if (SelectedAlert is null) return;
            var svc = _host.Services.ContractDashboardService;
            if (svc is null) return;

            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await svc.AcknowledgeAlertAsync(SelectedAlert.AlertId, actor, "Acusada desde dashboard", s_ct);
            await LoadAsync();
            StatusMessage = $"Alerta '{SelectedAlert.Title}' acusada.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    private async Task ResolveAlertAsync()
    {
        try
        {
            if (SelectedAlert is null) return;
            var svc = _host.Services.ContractDashboardService;
            if (svc is null) return;

            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await svc.ResolveAlertAsync(SelectedAlert.AlertId, actor, "Resuelta desde dashboard", s_ct);
            await LoadAsync();
            StatusMessage = $"Alerta '{SelectedAlert.Title}' resuelta.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }
}
