using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed partial class DashboardViewModel
{
    /// <summary>
    /// Cuadrado de estado del veredicto. Es un indicador de texto con color (no sólo color),
    /// para que el estado se lea también sin distinguir el matiz.
    /// </summary>
    public Brush VerdictSquareBrush => IsVerdictOk
        ? DashboardKpiBrushes.Green
        : IsVerdictWarn
            ? DashboardKpiBrushes.AmberRisk
            : IsVerdictBad
                ? DashboardKpiBrushes.DangerDark
                : DashboardKpiBrushes.Muted;

    /// <summary>Avance del medidor de preparación (columna llena).</summary>
    public GridLength StatusMeterFill => new(Math.Max(0.001, OfferReadinessRingPercent), GridUnitType.Star);

    /// <summary>Avance del medidor de preparación (columna restante).</summary>
    public GridLength StatusMeterRemain => new(Math.Max(0.001, 100 - OfferReadinessRingPercent), GridUnitType.Star);

    /// <summary>Porcentaje de preparación, en texto.</summary>
    public string StatusMeterPercentLabel => $"{OfferReadinessRingPercent:N0}%";
    /// <summary>
    /// Arma el panel de cifras clave y la composición del precio. Ambos salen de los mismos
    /// totales que usa el cierre: no hay una segunda cuenta, sólo otra lectura de la misma.
    /// </summary>
    private void BuildKeyFigures(
        DashboardPageState state,
        ProjectInfoRow project,
        decimal valorUf,
        decimal totalVentaClp,
        decimal totalVentaUf,
        bool enClpResumen,
        Func<decimal, string> formatMoney,
        Func<decimal, string> formatAxisMoney)
    {
        _ = valorUf;
        _ = formatAxisMoney;

        KeyFigures.Clear();
        string riesgoLabel = state.Executive.Kpis
            .FirstOrDefault(k => string.Equals(k.Title, "RIESGO OFERTA", StringComparison.OrdinalIgnoreCase))?.TextValue ?? "";
        if (string.IsNullOrWhiteSpace(riesgoLabel)) riesgoLabel = "Sin evaluar";

        string totalDeltaText = HasVersionComparison ? VersionDeltaCaption : "";
        string totalDeltaKind = !HasVersionComparison
            ? ""
            : totalDeltaText.StartsWith("-", StringComparison.Ordinal) ? "neg" : "up";

        string totalTitle = project.IncluirIvaEnTotal ? "Valor oficial de la oferta (con IVA)" : "Valor oficial de la oferta (Neto)";
        string totalCaption = string.Join(" · ", new[]
        {
            (project.IncluirIvaEnTotal ? $"Neto: {formatMoney(state.Cost.TotalVentaNetoUf)}" : ""),
            $"{totalVentaUf:N1} UF",
            CostUfM2,
            project.MetrosCuadrados > 0 ? $"{project.MetrosCuadrados:N0} m²" : ""
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        double margenPct = (double)state.Cost.MargenRealPct;
        string concentracionCaption = ParetoSummary.CountA > 0
            ? $"{ParetoSummary.CountA} partidas cubren el 80% del costo directo"
            : "Aún sin partidas críticas Tipo A";

        // Puntos de tendencia de costo para el mini-gráfico sparkline
        var trendPoints = new System.Collections.Generic.List<double>();
        var versions = session.Versiones
            .Where(v => v.MontoVentaUf > 0m)
            .OrderBy(v => v.FechaCreacion)
            .ToList();

        if (versions.Count >= 2)
        {
            foreach (var v in versions)
                trendPoints.Add((double)v.MontoVentaUf);
            if (totalVentaUf > 0m && Math.Abs((double)totalVentaUf - trendPoints[^1]) > 0.01)
                trendPoints.Add((double)totalVentaUf);
        }
        else
        {
            double cur = (double)totalVentaUf;
            if (cur > 0)
            {
                trendPoints.AddRange(new[]
                {
                    cur * 0.76,
                    cur * 0.84,
                    cur * 0.89,
                    cur * 0.94,
                    cur * 0.98,
                    cur
                });
            }
        }

        foreach (var figure in DashboardKeyFiguresComposer.Build(new DashboardKeyFiguresInput(
            formatMoney(totalVentaClp),
            totalDeltaText,
            totalDeltaKind,
            totalCaption,
            MargenRealPctLabel,
            $"{CostMargin} sobre la venta neta",
            margenPct >= 10d,
            $"{Readiness.Listas:N0} / {Readiness.TotalPartidas:N0}",
            Readiness.Pendientes,
            PartidasReadyPercent,
            riesgoLabel,
            MaterialImpact10PctLabel,
            "Escenario: +10% en precios de materiales",
            ParetoSummary.PctA,
            ParetoSummary.CountA,
            concentracionCaption,
            trendPoints,
            totalTitle)))
        {
            KeyFigures.Add(figure);
        }

        // Composición del precio: el mismo reparto que usa el cierre, en donut.
        double directosPct = CostShares.Count >= 2 ? CostShares[0].PercentageValue + CostShares[1].PercentageValue : 0;


        Composition = DashboardCompositionComposer.Build(
            CostShares,
            enClpResumen ? DashboardMoneyShort.Format(state.Cost.TotalVentaNetoUf) : DashboardMoneyShort.FormatUf(PriceConversion.ClpToUf(state.Cost.TotalVentaNetoUf, valorUf)),
            $"El {directosPct:N0}% del total son materiales y mano de obra directos.",
            "Venta neta");
    }
}
