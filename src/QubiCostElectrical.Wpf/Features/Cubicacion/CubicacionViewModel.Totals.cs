namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    public void ScheduleRefreshTotals() => totalsCalculator.ScheduleRefresh();

    public void RefreshTotals() => ApplyTotals(totalsCalculator.Calculate());

    internal void NotifyCubicacionSummaryChangedInternal() => NotifyCubicacionSummaryChanged();

    internal void ApplyTotals(CubicacionPartidaTotals totals)
    {
        TotalMateriales = totals.Materiales;
        TotalManoObra = totals.ManoObra;
        TotalCostoDirecto = totals.CostoDirecto;
        PrecioUnitario = totals.PrecioUnitario;
        TotalPartida = totals.TotalPartida;
        PrecioVentaUnitario = totals.PrecioVentaUnitario;
        TotalVentaPartida = totals.TotalVenta;
        NotifyTotalsUfChanged();
    }
}
