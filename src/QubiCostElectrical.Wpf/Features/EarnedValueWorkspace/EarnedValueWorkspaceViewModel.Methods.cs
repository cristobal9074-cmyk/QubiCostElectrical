using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.EarnedValueWorkspace;

public sealed partial class EarnedValueWorkspaceViewModel
{
    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            if (_host.Services.EarnedValue is null || _host.Services.ScheduleRepository is null)
            {
                StatusMessage = "Servicios EVM no inicializados.";
                return;
            }

            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath ?? string.Empty);
            var schedule = schedules.FirstOrDefault();
            if (schedule is null)
            {
                StatusMessage = "No hay cronograma.";
                return;
            }

            _scheduleId = schedule.Id;
            var latest = await _host.Services.EarnedValue.GetLatestAsync(schedule.Id);
            if (latest is not null)
                BindResult(latest);

            _history.Clear();
            foreach (var h in await _host.Services.EarnedValue.GetHistoryAsync(schedule.Id))
                _history.Add(h);

            _snapshots.Clear();
            foreach (var s in await _host.Services.EarnedValue.ListSnapshotsAsync(schedule.Id))
                _snapshots.Add(s);

            StatusMessage = latest is null ? "Sin cálculo EVM. Pulse Calcular." : $"Último cálculo: {latest.StatusDate:dd/MM/yyyy}";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private async Task CalculateAsync()
    {
        if (_scheduleId is null || _isBusy) return;
        _isBusy = true;
        try
        {
            decimal? bac = null;
            if (decimal.TryParse(BacInput, out var parsed) && parsed > 0m) bac = parsed;
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var result = await _host.Services.EarnedValue.CalculateAsync(_scheduleId, actor, bac);
            BindResult(result);
            await LoadAsync();
            StatusMessage = result.IsValid ? "EVM calculado." : "EVM calculado con observaciones.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private async Task CreateSnapshotAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var snap = await _host.Services.EarnedValue.CreateSnapshotAsync(_scheduleId, "Snapshot EVM", actor);
            StatusMessage = $"Snapshot EVM v{snap.Version} · SHA {snap.SHA256[..12]}…";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CompareAsync()
    {
        if (SelectedSnapshotA is null || SelectedSnapshotB is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var cmp = await _host.Services.EarnedValue.CompareSnapshotsAsync(
                SelectedSnapshotA.SnapshotId, SelectedSnapshotB.SnapshotId, actor);
            ComparisonText =
                $"ΔPV={cmp.PvDelta:N0} ΔEV={cmp.EvDelta:N0} ΔAC={cmp.AcDelta:N0} ΔSPI={cmp.SpiDelta:0.####} ΔCPI={cmp.CpiDelta:0.####} · " +
                string.Join(" ", cmp.Findings);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ExportAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var latest = await _host.Services.EarnedValue.GetLatestAsync(_scheduleId);
            if (latest is null)
            {
                StatusMessage = "No hay resultado para exportar.";
                return;
            }
            var paths = EarnedValueExportHelper.ExportAllFormats(latest, _history.ToList());
            StatusMessage = $"Exportado CSV/Excel/PDF: {paths}";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void BindResult(EarnedValueResult result)
    {
        Explanation = result.Explanation;
        _indicators.Clear();
        var ind = result.Indicators;
        void Add(string name, string value)
        {
            var interp = result.Interpretations.FirstOrDefault(i => i.Indicator == name);
            _indicators.Add(new EvmIndicatorRow
            {
                Name = name,
                Value = value,
                Light = interp?.Light.ToString() ?? "Neutral",
                Message = interp?.Message ?? string.Empty
            });
        }

        Add("PV", ind.Pv.ToString("N2"));
        Add("EV", ind.Ev.ToString("N2"));
        Add("AC", ind.Ac.ToString("N2"));
        Add("BAC", ind.Bac.ToString("N2"));
        Add("EAC", ind.Eac.ToString("N2"));
        Add("ETC", ind.Etc.ToString("N2"));
        Add("VAC", ind.Vac.ToString("N2"));
        Add("SV", ind.Sv.ToString("N2"));
        Add("CV", ind.Cv.ToString("N2"));
        Add("SPI", ind.Spi?.ToString("0.####") ?? "N/D");
        Add("CPI", ind.Cpi?.ToString("0.####") ?? "N/D");
        Add("TCPI", ind.Tcpi?.ToString("0.####") ?? "N/D");
        _indicators.Add(new EvmIndicatorRow { Name = "% Plan", Value = ind.PercentPlanned.ToString("0.##") + "%" });
        _indicators.Add(new EvmIndicatorRow { Name = "% Ganado", Value = ind.PercentEarned.ToString("0.##") + "%" });
        _indicators.Add(new EvmIndicatorRow { Name = "% Actual", Value = ind.PercentActual.ToString("0.##") + "%" });
        _indicators.Add(new EvmIndicatorRow
        {
            Name = "Forecast costo",
            Value = result.Forecast.ForecastCost.ToString("N2")
        });
        _indicators.Add(new EvmIndicatorRow
        {
            Name = "Forecast var. fin (días)",
            Value = result.Forecast.ForecastFinishVarianceDays?.ToString("0.##") ?? "N/D"
        });
    }
}
