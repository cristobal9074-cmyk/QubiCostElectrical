using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed class DashboardKpiCardVm
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Caption { get; init; } = "";
    public bool IsPrimary { get; init; }
    public DashboardExecutiveKpiAccent Accent { get; init; }

    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;

    public string ChipLabel { get; init; } = "";
    public string ChipKind { get; init; } = "mute";
    public string IconKind { get; init; } = "riesgo";
    public bool ShowUfUpdateButton { get; init; }
    public double GaugePercent { get; init; }
    public GridLength GaugeFillWidth { get; init; } = new(0.001, GridUnitType.Star);
    public GridLength GaugeRemainWidth { get; init; } = new(99.999, GridUnitType.Star);
}

public sealed class DashboardWorkflowStepVm
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool IsComplete { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsLast { get; init; }
    public ICommand? NavigateCommand { get; init; }
}

public sealed class DashboardAlertVm
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string SeverityLabel { get; init; } = "";
    public FinalReviewCategory Category { get; init; }
    public ICommand NavigateCommand { get; init; } = null!;
}

public sealed class DashboardPartidaVm
{
    public int Rank { get; init; }
    public string ItemId { get; init; } = "";
    public string Item { get; init; } = "";
    public string Partida { get; init; } = "";
    public string TotalLabel { get; init; } = "";
    public string TotalClpLabel { get; init; } = "";
    public string TotalUfLabel { get; init; } = "";
    public double SharePct { get; init; }
    public double AccumPct { get; init; }
    public bool IsPareto80 => AccumPct <= 80.5;
    public string ParetoClass => AccumPct <= 80.5 ? "A" : (AccumPct <= 95.0 ? "B" : "C");
    public string ParetoClassBadge => $"Tipo {ParetoClass}";
    public string PercentLabel => $"{SharePct:N1}%";
    public string AccumPercentLabel => $"{AccumPct:N1}%";
    public GridLength FillWidth => new(Math.Max(0.001, SharePct), GridUnitType.Star);
    public GridLength RemainderWidth => new(Math.Max(0.001, 100 - SharePct), GridUnitType.Star);
    public string Category { get; init; } = "Otros";
}

public sealed class DashboardCostShareVm
{
    public string Label { get; init; } = "";
    public string AmountLabel { get; init; } = "";
    public string PercentageLabel { get; init; } = "";
    public double PercentageValue { get; init; }
    public DashboardExecutiveKpiAccent Accent { get; init; }
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
}

public sealed class DashboardOfferReadinessItemVm
{
    public string Label { get; init; } = "";
    /// <summary>Avance medido de la compuerta ("108 de 124"), no un booleano suelto.</summary>
    public string Detail { get; init; } = "";
    public bool IsComplete { get; init; }
    public bool IsWarning { get; init; }
    public Brush StatusBrush { get; init; } = DashboardKpiBrushes.Green;
    public ICommand? NavigateCommand { get; init; }
}

public sealed class DashboardNavItemVm
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}

internal static class DashboardKpiBrushes
{
    public static Brush Blue => ThemeOrFallback("Brush.Blue", "#2563EB");
    public static Brush BlueDark => ThemeOrFallback("Brush.BlueDark", "#1D4ED8");
    public static Brush Amber => ThemeOrFallback("Brush.Amber", "#B45309");
    public static Brush DangerDark => ThemeOrFallback("Brush.DangerDark", "#B91C1C");
    public static Brush AmberRisk => ThemeOrFallback("Brush.AmberRisk", "#D97706");
    public static Brush Green => ThemeOrFallback("Brush.Green", "#15803D");
    public static Brush BlueSoft => ThemeOrFallback("Brush.BlueSoft", "#EAF1FF");
    public static Brush AmberSoft => ThemeOrFallback("Brush.AmberSoft", "#FFF5DD");
    public static Brush RedSoft => ThemeOrFallback("Brush.RedSoft", "#FFF0F0");
    public static Brush GreenSoft => ThemeOrFallback("Brush.GreenSoft", "#EAF8EF");
    public static Brush Muted => ThemeOrFallback("Brush.Muted", "#64748B");

    public static Brush ForAccent(DashboardExecutiveKpiAccent accent) => accent switch
    {
        DashboardExecutiveKpiAccent.Primary => Blue,
        DashboardExecutiveKpiAccent.Blue => BlueDark,
        DashboardExecutiveKpiAccent.Orange => Amber,
        DashboardExecutiveKpiAccent.RiskHigh => DangerDark,
        DashboardExecutiveKpiAccent.RiskMedium => AmberRisk,
        DashboardExecutiveKpiAccent.RiskLow => Green,
        _ => Blue
    };

    public static Brush ForAccentSoft(DashboardExecutiveKpiAccent accent) => accent switch
    {
        DashboardExecutiveKpiAccent.RiskHigh => RedSoft,
        DashboardExecutiveKpiAccent.RiskMedium or DashboardExecutiveKpiAccent.Orange => AmberSoft,
        DashboardExecutiveKpiAccent.RiskLow => GreenSoft,
        _ => BlueSoft
    };

    private static Brush ThemeOrFallback(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }
}
