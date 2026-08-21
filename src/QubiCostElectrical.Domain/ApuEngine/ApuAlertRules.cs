namespace QubiCostElectrical;

/// <summary>
/// Alertas operativas de un APU individual, mostradas en el panel de trabajo
/// (Features/Apu/ApuWorkspaceVm.cs) — antes vivían hardcodeadas en el ViewModel, sin poder
/// testearse sin instanciar la pantalla completa.
/// </summary>
public static class ApuAlertRules
{
    public const int PrecioAntiguoWarningDays = 90;

    public static IReadOnlyList<string> Build(ApuRow apu, DateTime today)
    {
        var alerts = new List<string>();
        if (apu.Recursos.Count == 0) alerts.Add("APU sin recursos.");
        if (apu.Recursos.Any(r => r.PrecioBaseUf <= 0m)) alerts.Add("Hay recursos sin precio.");
        if (apu.Recursos.Any(r => r.Rendimiento <= 0m)) alerts.Add("Hay recursos sin rendimiento.");

        var diasDesde = (today.Date - apu.FechaPrecio.Date).TotalDays;
        if (diasDesde > PrecioAntiguoWarningDays)
            alerts.Add($"Precios con {(int)diasDesde} días de antigüedad.");

        return alerts;
    }
}
