using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadoPago;

partial class EstadoPagoViewModel
{
    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            BuildMonthTimeline();
            await LoadCertificateForMonth(_months.FirstOrDefault());
            RecalcTotals();
        }
        finally
        {
            _isBusy = false;
            RaiseCommands();
        }
    }

    private void BuildMonthTimeline()
    {
        _months.Clear();

        var project = _host.Session.ProjectInfo;
        if (project == null) return;

        DateTime start = new DateTime(DateTime.Today.Year, 1, 1);
        DateTime end = DateTime.Today.AddMonths(1);

        int startYear = start.Year;
        int startMonth = start.Month;
        int totalMonths = ((end.Year - startYear) * 12) + (end.Month - startMonth);
        if (totalMonths < 1) totalMonths = 1;

        for (int i = 0; i < totalMonths; i++)
        {
            int y = startYear + (startMonth + i - 1) / 12;
            int m = ((startMonth + i - 1) % 12) + 1;
            var vm = new EstadoPagoMonthVm(y, m);
            vm.IsActive = i == totalMonths - 1;
            _months.Add(vm);
        }

        if (_months.Count > 0)
            _months[^1].IsActive = true;
    }

    private async Task LoadCertificateForMonth(EstadoPagoMonthVm? month)
    {
        _rows.Clear();

        if (month == null)
        {
            StatusMessage = "Seleccione un período para ver su detalle.";
            return;
        }

        foreach (var m in _months) m.IsActive = m == month;

        var itemizado = _host.Session.Itemizado;
        if (itemizado == null || itemizado.Count == 0)
        {
            StatusMessage = "No hay partidas en el Itemizado. Agregue partidas para comenzar.";
            return;
        }

        var prevMonth = await LoadPreviousMonthTotals(month);
        int certNumber = 1;

        foreach (var item in itemizado)
        {
            var row = new EstadoPagoRowVm();
            decimal prev = 0m;
            if (prevMonth.TryGetValue(item.Item ?? "", out var p))
                prev = p;

            row.LoadFromItemizadoRow(item, prev);
            if (!string.IsNullOrEmpty(_searchText) && !MatchesSearch(row))
                continue;
            _rows.Add(row);
        }

        certNumber = _months.IndexOf(month) + 1;
        CertNumber = $"N°{certNumber:D2}";
        PeriodLabel = $"{new DateTime(month.Year, month.Month, 1):dd MMM yyyy} — {new DateTime(month.Year, month.Month, 1).AddMonths(1).AddDays(-1):dd MMM yyyy}";
        StatusMessage = $"{_rows.Count(r => !r.EsTitulo)} partidas cargadas desde el Itemizado.";
        RecalcTotals();
        UpdateMonthAmount(month);
    }

    private Task<Dictionary<string, decimal>> LoadPreviousMonthTotals(EstadoPagoMonthVm month)
    {
        var result = new Dictionary<string, decimal>();
        int idx = _months.IndexOf(month);
        if (idx <= 0) return Task.FromResult(result);

        // Load from previous month's accumulated data
        // In a full implementation, this would load from the persisted certificate
        return Task.FromResult(result);
    }

    private void UpdateMonthAmount(EstadoPagoMonthVm month)
    {
        decimal total = 0;
        foreach (var r in _rows)
            total += r.MontoPeriodo;
        month.Amount = total;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            // Reload all rows from Itemizado (not just filtered)
            _ = LoadCertificateForMonth(_selectedMonth);
            return;
        }

        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (!MatchesSearch(_rows[i]))
                _rows.RemoveAt(i);
        }
        RecalcTotals();
    }

    private bool MatchesSearch(EstadoPagoRowVm row)
    {
        if (string.IsNullOrEmpty(_searchText)) return true;
        var q = _searchText.AsSpan();
        return row.Item.AsSpan().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
            || row.Descripcion.AsSpan().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RecalcTotals()
    {
        var partidas = _rows.Where(r => !r.EsTitulo).ToList();
        TotalRowCount = partidas.Count;
        ActiveRowCount = partidas.Count(r => r.CantidadPeriodo > 0);

        decimal contractTotal = 0, accumulatedTotal = 0, periodTotal = 0, balanceTotal = 0;
        foreach (var r in partidas)
        {
            contractTotal += r.TotalContractual;
            accumulatedTotal += r.MontoAcumulado;
            periodTotal += r.MontoPeriodo;
            balanceTotal += r.Saldo;
        }

        ContractAmountLabel = $"$ {contractTotal:N2}";
        AccumulatedLabel = $"$ {accumulatedTotal:N2}";
        PeriodAmountLabel = $"$ {periodTotal:N2}";
        BalanceLabel = $"$ {balanceTotal:N2}";
        ProgressPctLabel = contractTotal > 0 ? $"{accumulatedTotal / contractTotal * 100:F1}%" : "0%";
    }

    private void RaiseCommands()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RecalcAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateNewCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
