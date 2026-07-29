namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica de cálculo movida a CubicacionTotalsCalculator (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que CubicacionView.xaml.cs y el resto de los partials ya conocen,
    // sin cambiar ninguna firma.
    public void ScheduleRefreshTotals() => totalsCalculator.ScheduleRefresh();

    public void RefreshTotals() => ApplyTotals(totalsCalculator.Calculate());

    // Separado de NotifyCubicacionSummaryChanged() (Persistence.cs) porque
    // CubicacionTotalsCalculator, al ser una clase distinta, no puede llamar directamente a
    // un método private de esta clase — necesita este punto de entrada internal. Preserva la
    // asimetría original: RefreshTotals() (síncrono) NO notifica, ScheduleRefreshTotals()
    // (async) sí, exactamente como estaba antes de extraer el cálculo.
    internal void NotifyCubicacionSummaryChangedInternal() => NotifyCubicacionSummaryChanged();

    internal void ApplyTotals(CubicacionPartidaTotals totals)
    {
        TotalMateriales = totals.Materiales;
        TotalManoObra = totals.ManoObra;
        TotalCostoDirecto = totals.CostoDirecto;
        PrecioUnitario = totals.PrecioUnitario;
        TotalPartida = totals.TotalPartida;
    }
}
