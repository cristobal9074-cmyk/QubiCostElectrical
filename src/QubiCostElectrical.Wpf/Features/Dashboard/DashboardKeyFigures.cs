using System;
using System.Collections.Generic;
using System.Windows;

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
    string ConcentracionCaption);

/// <summary>
/// Compone las cinco celdas del panel "Cifras clave". Siempre devuelve cinco celdas para que
/// la grilla mantenga la misma geometría con cualquier proyecto.
/// </summary>
public static class DashboardKeyFiguresComposer
{
    public static IReadOnlyList<DashboardKeyFigureVm> Build(DashboardKeyFiguresInput input)
    {
        input ??= new DashboardKeyFiguresInput("", "", "", "", "", "", false, "", 0, 0, "", "", "", 0, 0, "");

        var figures = new List<DashboardKeyFigureVm>(5)
        {
            new DashboardKeyFigureVm
            {
                Label = "Valor oficial de la oferta",
                Value = input.TotalLabel,
                DeltaText = input.TotalDeltaText,
                DeltaKind = input.TotalDeltaKind,
                DeltaIcon = input.TotalDeltaKind == "neg" ? "alert" : "up",
                Caption = input.TotalCaption
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
