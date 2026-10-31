using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>
/// Una celda del panel "Cifras clave". La celda es plana y tipográfica: no lleva icono, ni
/// borde propio, ni tarjeta; el separador lo dibuja el panel. Así la lectura del encabezado
/// es la del total y no la del color.
/// </summary>
public sealed class DashboardKeyFigureVm
{
    /// <summary>Etiqueta corta de la cifra (una línea).</summary>
    public string Label { get; init; } = "";

    /// <summary>Valor principal, ya formateado en la moneda del presupuesto.</summary>
    public string Value { get; init; } = "";

    /// <summary>El valor es corto: se reduce el cuerpo para que no desborde la celda.</summary>
    public bool IsCompactValue { get; init; }

    /// <summary>Línea de variación: "+3,2% vs v. anterior". Vacía si la cifra no tiene comparación.</summary>
    public string DeltaText { get; init; } = "";

    /// <summary>Tono de la variación: up | warn | neg | empty.</summary>
    public string DeltaKind { get; init; } = "";

    /// <summary>Icono de la variación: up | check | alert.</summary>
    public string DeltaIcon { get; init; } = "";

    /// <summary>Nota al pie de la cifra.</summary>
    public string Caption { get; init; } = "";

    /// <summary>Avance 0..100 para la micro-barra de la cifra (0 = sin barra).</summary>
    public double BarPercent { get; init; }

    /// <summary>Tono de la micro-barra: brand | warn | pos | empty.</summary>
    public string BarKind { get; init; } = "";

    /// <summary>La cifra tiene micro-barra de avance.</summary>
    public bool HasBar => BarKind.Length > 0;

    /// <summary>Columna llena de la micro-barra (proporción del total).</summary>
    public GridLength BarFill => new(Math.Max(0.001, BarPercent), GridUnitType.Star);

    /// <summary>Columna restante de la micro-barra.</summary>
    public GridLength BarRemain => new(Math.Max(0.001, 100 - BarPercent), GridUnitType.Star);

    /// <summary>Indica si la cifra despliega un mini-gráfico sparkline de tendencia.</summary>
    public bool HasSparkline => !string.IsNullOrEmpty(SparklineLineData);

    /// <summary>Ruta vectorial (StreamGeometry) de la línea de tendencia del costo.</summary>
    public string SparklineLineData { get; init; } = "";

    /// <summary>Ruta vectorial (StreamGeometry) del área bajo la curva del costo.</summary>
    public string SparklineAreaData { get; init; } = "";

    /// <summary>Margen (X,Y) del punto indicador sobre el valor más reciente de la curva.</summary>
    public Thickness SparklineDotMargin { get; init; } = new(0);

    /// <summary>Pincel de la línea de tendencia.</summary>
    public Brush SparklineStroke { get; init; } = DashboardKpiBrushes.Blue;

    /// <summary>Pincel del área bajo la curva.</summary>
    public Brush SparklineAreaFill { get; init; } = DashboardKpiBrushes.Blue;
}

/// <summary>Datos de entrada del panel de cifras, ya calculados por el motor.</summary>
public sealed record DashboardKeyFiguresInput(
    string TotalLabel,
    string TotalDeltaText,
    string TotalDeltaKind,
    string TotalCaption,
    string MargenLabel,
    string MargenCaption,
    bool MargenHealthy,
    string PartidasLabel,
    int PartidasPendientes,
    double PartidasConfianza,
    string RiesgoLabel,
    string RiesgoDeltaText,
    string RiesgoCaption,
    double ConcentracionPct,
    int ConcentracionCount,
    string ConcentracionCaption,
    IReadOnlyList<double>? CostTrendPoints = null,
    string? TotalTitle = null);

/// <summary>
/// Compone las cinco celdas del panel "Cifras clave". Siempre devuelve cinco celdas para que
/// la grilla mantenga la misma geometría con cualquier proyecto.
/// </summary>
public static class DashboardKeyFiguresComposer
{
    public static (string lineData, string areaData, Thickness dotMargin) GenerateSparkline(
        IReadOnlyList<double>? points,
        double width = 110,
        double height = 22)
    {
        if (points == null || points.Count < 2)
            return ("", "", new Thickness(0));

        double min = double.MaxValue;
        double max = double.MinValue;
        foreach (var p in points)
        {
            if (p < min) min = p;
            if (p > max) max = p;
        }

        double padTop = 3;
        double padBottom = 3;
        double usableHeight = Math.Max(1, height - padTop - padBottom);
        double range = max - min;

        var coords = new List<(double X, double Y)>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            double x = i * (width / (points.Count - 1));
            double y = range < 0.0001
                ? height / 2.0
                : height - padBottom - ((points[i] - min) / range * usableHeight);
            coords.Add((Math.Round(x, 1), Math.Round(y, 1)));
        }

        var sbLine = new StringBuilder();
        sbLine.Append(System.Globalization.CultureInfo.InvariantCulture, $"M {coords[0].X:0.#},{coords[0].Y:0.#}");
        for (int i = 1; i < coords.Count; i++)
        {
            sbLine.Append(System.Globalization.CultureInfo.InvariantCulture, $" L {coords[i].X:0.#},{coords[i].Y:0.#}");
        }

        var sbArea = new StringBuilder(sbLine.ToString());
        sbArea.Append(System.Globalization.CultureInfo.InvariantCulture, $" L {coords[^1].X:0.#},{height:0.#} L {coords[0].X:0.#},{height:0.#} Z");

        var last = coords[^1];
        var dotMargin = new Thickness(Math.Max(0, last.X - 2.5), Math.Max(0, last.Y - 2.5), 0, 0);

        return (sbLine.ToString(), sbArea.ToString(), dotMargin);
    }

    public static IReadOnlyList<DashboardKeyFigureVm> Build(DashboardKeyFiguresInput input)
    {
        input ??= new DashboardKeyFiguresInput("", "", "", "", "", "", false, "", 0, 0, "", "", "", 0, 0, "");

        var (sparkLine, sparkArea, sparkDot) = GenerateSparkline(input.CostTrendPoints, 120, 22);

        var figures = new List<DashboardKeyFigureVm>(5)
        {
            new DashboardKeyFigureVm
            {
                Label = string.IsNullOrWhiteSpace(input.TotalTitle) ? "Valor oficial de la oferta" : input.TotalTitle,
                Value = input.TotalLabel,
                DeltaText = input.TotalDeltaText,
                DeltaKind = input.TotalDeltaKind,
                DeltaIcon = input.TotalDeltaKind == "neg" ? "alert" : "up",
                Caption = input.TotalCaption,
                SparklineLineData = sparkLine,
                SparklineAreaData = sparkArea,
                SparklineDotMargin = sparkDot,
                SparklineStroke = DashboardKpiBrushes.Blue,
                SparklineAreaFill = DashboardKpiBrushes.Blue
            },
            new DashboardKeyFigureVm
            {
                Label = "Margen real",
                Value = input.MargenLabel,
                DeltaText = input.MargenHealthy ? "Saludable" : "Ajustado",
                DeltaKind = input.MargenHealthy ? "up" : "warn",
                DeltaIcon = input.MargenHealthy ? "check" : "alert",
                Caption = input.MargenCaption
            },
            new DashboardKeyFigureVm
            {
                Label = "Partidas listas",
                Value = input.PartidasLabel,
                IsCompactValue = true,
                BarPercent = Math.Clamp(input.PartidasConfianza, 0, 100),
                BarKind = "brand",
                Caption = input.PartidasPendientes > 0
                    ? $"{input.PartidasPendientes:N0} pendientes · confianza {input.PartidasConfianza:N0}%"
                    : $"Sin pendientes · confianza {input.PartidasConfianza:N0}%"
            },
            new DashboardKeyFigureVm
            {
                Label = "Riesgo de oferta",
                Value = input.RiesgoLabel,
                DeltaText = input.RiesgoDeltaText,
                DeltaKind = "warn",
                DeltaIcon = "alert",
                Caption = input.RiesgoCaption
            },
            new DashboardKeyFigureVm
            {
                Label = "Concentración clase A",
                Value = $"{input.ConcentracionPct:N1}%",
                BarPercent = Math.Clamp(input.ConcentracionPct, 0, 100),
                BarKind = "warn",
                Caption = input.ConcentracionCaption
            }
        };

        return figures;
    }
}
