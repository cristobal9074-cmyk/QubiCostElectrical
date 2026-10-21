using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>Un segmento de la dona de composición del precio.</summary>
public sealed class DashboardCompositionSegmentVm
{
    public string Label { get; init; } = "";

    /// <summary>Importe principal del reparto (sin el equivalente en pesos).</summary>
    public string AmountLabel { get; init; } = "";

    /// <summary>Importe con su equivalente en la otra moneda, cuando la moneda lo permite.</summary>
    public string AmountFullLabel { get; init; } = "";

    public string PercentageLabel { get; init; } = "";
    public double PercentageValue { get; init; }

    /// <summary>Pincel del segmento (dinámico: responde al cambio de tema).</summary>
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;

    /// <summary>Trazo del arco del segmento, en coordenadas del lienzo de la dona.</summary>
    public Geometry ArcGeometry { get; init; } = Geometry.Empty;

    /// <summary>Participación del segmento dentro del reparto (0..100).</summary>
    public string ShareLabel { get; init; } = "";
}

/// <summary>Composición del precio: dona + total + lectura.</summary>
public sealed class DashboardCompositionVm
{
    public IReadOnlyList<DashboardCompositionSegmentVm> Segments { get; init; } = Array.Empty<DashboardCompositionSegmentVm>();

    /// <summary>Total en el centro de la dona.</summary>
    public string TotalLabel { get; init; } = "";

    /// <summary>Rótulo bajo el total del centro.</summary>
    public string TotalCaption { get; init; } = "Total venta";

    /// <summary>Lectura de una línea bajo la leyenda.</summary>
    public string Insight { get; init; } = "";

    /// <summary>Circunferencia de la dona (2 * PI * radio). El radio del lienzo es 76.</summary>
    public double Circumference { get; init; } = 477.52;

    public bool HasData => Segments.Count > 0;

    public static DashboardCompositionVm Empty { get; } = new();
}

/// <summary>
/// Compone la dona de composición del precio. Los cuatro sectores son siempre los mismos
/// (materiales, mano de obra, gastos operacionales y margen) y el largo de cada arco se
/// normaliza contra el total, de modo que la suma de los arcos cierra el círculo aunque el
/// motor entregue porcentajes que no sumen exactamente 100.
/// </summary>
public static class DashboardCompositionComposer
{
    /// <summary>Radio del trazo de la dona (lienzo de 200 x 200).</summary>
    public const double Radius = 76;

    /// <summary>Centro del lienzo de la dona.</summary>
    public const double Center = 100;

    /// <summary>Separación angular entre arcos, en grados (equivale a ~4 unidades de circunferencia).</summary>
    public const double GapDegrees = 3.2;

    public static DashboardCompositionVm Build(
        IReadOnlyList<DashboardCostShareVm> shares,
        string totalLabel,
        string insight)
    {
        shares ??= Array.Empty<DashboardCostShareVm>();
        if (shares.Count == 0)
            return new DashboardCompositionVm { TotalLabel = totalLabel };

        double circumference = 2 * Math.PI * Radius;
        double total = 0;
        foreach (var share in shares)
            total += Math.Max(0, share.PercentageValue);
        if (total <= 0) total = 1;

        // Cada sector se dibuja como un arco explícito (no como guiones sobre un trazo): así el
        // reparto es exacto, el hueco entre sectores es siempre el mismo y ningún arco se sale
        // del círculo cuando alguna participación es muy pequeña.
        var segments = new List<DashboardCompositionSegmentVm>(shares.Count);
        double accumulated = -90; // arranca a las 12 en punto
        for (int i = 0; i < shares.Count; i++)
        {
            var share = shares[i];
            double shareOfCircle = Math.Max(0, share.PercentageValue) / total;
            double sweep = 360 * shareOfCircle;
            double start = accumulated;
            double end = accumulated + sweep - GapDegrees;

            Geometry geometry = Geometry.Empty;
            if (end > start)
                geometry = BuildArc(start, end);

            string full = share.AmountLabel ?? "";
            int pair = full.IndexOf(" (", StringComparison.Ordinal);
            segments.Add(new DashboardCompositionSegmentVm
            {
                Label = share.Label,
                AmountLabel = pair > 0 ? full[..pair] : full,
                AmountFullLabel = full,
                PercentageLabel = share.PercentageLabel,
                PercentageValue = share.PercentageValue,
                ShareLabel = $"{shareOfCircle * 100:N1}%",
                AccentBrush = BrushFor(i),
                ArcGeometry = geometry
            });
            accumulated += sweep;
        }

        return new DashboardCompositionVm
        {
            Segments = segments,
            TotalLabel = totalLabel,
            Insight = insight,
            Circumference = circumference
        };
    }

    /// <summary>
    /// Trazo de un arco en sentido horario dentro del lienzo de la dona. La geometría se
    /// construye en memoria (no como texto) porque el analizador de trazos de WPF usa la
    /// cultura del sistema y en es-CL el separador decimal choca con el de coordenadas.
    /// </summary>
    private static Geometry BuildArc(double startDegrees, double endDegrees)
    {
        double startRad = startDegrees * Math.PI / 180d;
        double endRad = endDegrees * Math.PI / 180d;
        var start = new Point(Center + Radius * Math.Cos(startRad), Center + Radius * Math.Sin(startRad));
        var end = new Point(Center + Radius * Math.Cos(endRad), Center + Radius * Math.Sin(endRad));
        bool isLargeArc = endDegrees - startDegrees > 180d;

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(end, new Size(Radius, Radius), 0d, isLargeArc, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Tonos de la dona. Se eligen por posición y no por el acento original del reparto para
    /// que los cuatro sectores se distinguan entre sí también en modo oscuro.
    /// </summary>
    private static Brush BrushFor(int index) => index switch
    {
        0 => DashboardKpiBrushes.BlueDark,
        1 => DashboardKpiBrushes.Cyan,
        2 => DashboardKpiBrushes.Slate,
        _ => DashboardKpiBrushes.Green
    };
}
