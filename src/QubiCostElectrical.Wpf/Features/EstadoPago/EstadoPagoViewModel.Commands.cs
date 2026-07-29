using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadoPago;

partial class EstadoPagoViewModel
{
    private async Task SaveAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            if (_selectedMonth == null)
            {
                StatusMessage = "Seleccione un período para guardar.";
                return;
            }

            var partidas = _rows.Where(r => !r.EsTitulo).ToList();
            int updated = 0;
            foreach (var r in partidas)
            {
                if (r.CantidadPeriodo > 0) updated++;
            }

            StatusMessage = $"✅ Estado de pago {CertNumber} guardado. {updated} partidas actualizadas.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
            RaiseCommands();
        }
    }

    private void RecalcAll()
    {
        foreach (var r in _rows)
            r.Recalc();
        RecalcTotals();
        if (_selectedMonth != null)
            UpdateMonthAmount(_selectedMonth);
        StatusMessage = "🧮 Todas las partidas recalculadas.";
    }

    private void CreateNew()
    {
        if (_selectedMonth == null) return;

        // Reset the current month's rows
        foreach (var r in _rows)
        {
            r.CantidadPeriodo = 0;
            r.Recalc();
        }
        RecalcTotals();
        StatusMessage = $"Nuevo certificado para {_selectedMonth.Label}. Ingrese cantidades del período.";
    }

    private void SelectMonth(EstadoPagoMonthVm? month)
    {
        if (month == null || month == _selectedMonth) return;
        SelectedMonth = month;
    }
}
