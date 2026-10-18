using System;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed record ItemizadoHeroKpiSnapshot(
    string DirectCostText,
    string TotalOfferText,
    string MarginText,
    decimal MaterialesClp,
    decimal ManoObraClp,
    decimal OtrosClp,
    double MaterialesPct,
    double ManoObraPct,
    double OtrosPct,
    string MaterialesText,
    string ManoObraText,
    string OtrosText,
    string MaterialesPctText,
    string ManoObraPctText,
    string OtrosPctText,
    GridLength MaterialesGridLength,
    GridLength ManoObraGridLength,
    GridLength OtrosGridLength,
    int TotalPartidas,
    int Completas,
    int Incompletas,
    int SinPrecio,
    int Capitulos,
    int CompletionPct,
    string HealthBadgeText,
    string HealthBadgeKind,
    string HealthDescriptionText);

public static class ItemizadoHeroKpiCoordinator
{
    public static ItemizadoHeroKpiSnapshot Empty => new(
        DirectCostText: "$ 0",
        TotalOfferText: "$ 0",
        MarginText: "0.0%",
        MaterialesClp: 0,
        ManoObraClp: 0,
        OtrosClp: 0,
        MaterialesPct: 50,
        ManoObraPct: 50,
        OtrosPct: 0,
        MaterialesText: "$ 0",
        ManoObraText: "$ 0",
        OtrosText: "$ 0",
        MaterialesPctText: "0.0%",
        ManoObraPctText: "0.0%",
        OtrosPctText: "0.0%",
        MaterialesGridLength: new GridLength(50, GridUnitType.Star),
        ManoObraGridLength: new GridLength(50, GridUnitType.Star),
        OtrosGridLength: new GridLength(0),
        TotalPartidas: 0,
        Completas: 0,
        Incompletas: 0,
        SinPrecio: 0,
        Capitulos: 0,
        CompletionPct: 0,
        HealthBadgeText: "Sin partidas",
        HealthBadgeKind: "info",
        HealthDescriptionText: "Abra o cree un presupuesto.");

    public static ItemizadoHeroKpiSnapshot Compute(
        ItemizadoPresenter presenter,
        BudgetProjectSession session,
        ItemizadoSummaryMetrics summary,
        int capitulosCount,
        CurrencyPresentationContext currencyCtx)
    {
        CostBreakdown cost;
        try
        {
            cost = presenter.BuildCostBreakdown();
        }
        catch
        {
            cost = new CostBreakdown();
        }

        decimal totalDirectoClp = cost.CostoDirectoClp;
        decimal matClp = cost.MaterialesClp;
        decimal moClp = cost.ManoObraClp;
        decimal otrosClp = Math.Max(0, totalDirectoClp - matClp - moClp);

        double matPct = totalDirectoClp > 0 ? (double)(matClp / totalDirectoClp * 100m) : 50.0;
        double moPct = totalDirectoClp > 0 ? (double)(moClp / totalDirectoClp * 100m) : 50.0;
        double otrosPct = totalDirectoClp > 0 ? Math.Max(0, 100.0 - matPct - moPct) : 0.0;

        var matLength = new GridLength(Math.Max(matPct, 1.0), GridUnitType.Star);
        var moLength = new GridLength(Math.Max(moPct, 1.0), GridUnitType.Star);
        var otrosLength = otrosPct > 0 ? new GridLength(otrosPct, GridUnitType.Star) : new GridLength(0);

        string matText = FormatAmount(matClp, currencyCtx);
        string moText = FormatAmount(moClp, currencyCtx);
        string otrosText = FormatAmount(otrosClp, currencyCtx);
        string directText = FormatAmount(totalDirectoClp, currencyCtx);

        decimal marginPct = session.ProjectInfo?.UtilidadPct ?? 0m;
        string marginText = $"{marginPct:N1}%";

        decimal totalVentaClp = cost.TotalVentaClp;
        string totalOfferText = FormatAmount(totalVentaClp, currencyCtx);

        int totalPartidas = summary.TotalPartidas;
        int completas = summary.Completas;
        int incompletas = summary.Incompletas;
        int sinPrecio = summary.SinPrecio;
        int completionPct = totalPartidas > 0 ? (int)Math.Round(summary.CompletedPct) : 0;

        string healthKind;
        string healthBadge;
        string healthDesc;

        if (totalPartidas == 0)
        {
            healthKind = "info";
            healthBadge = "Sin partidas";
            healthDesc = "Agregue o importe partidas para comenzar el presupuesto.";
        }
        else if (incompletas == 0)
        {
            healthKind = "ok";
            healthBadge = "100% Cubicado";
            healthDesc = "Todas las partidas cuentan con cubicación y APU valorizado.";
        }
        else
        {
            healthKind = "warn";
            healthBadge = $"{incompletas} pendientes";
            healthDesc = $"{incompletas} partida(s) requieren APU o cubicación para valorizar.";
        }

        return new ItemizadoHeroKpiSnapshot(
            DirectCostText: directText,
            TotalOfferText: totalOfferText,
            MarginText: marginText,
            MaterialesClp: matClp,
            ManoObraClp: moClp,
            OtrosClp: otrosClp,
            MaterialesPct: matPct,
            ManoObraPct: moPct,
            OtrosPct: otrosPct,
            MaterialesText: matText,
            ManoObraText: moText,
            OtrosText: otrosText,
            MaterialesPctText: $"{matPct:F1}%",
            ManoObraPctText: $"{moPct:F1}%",
            OtrosPctText: $"{otrosPct:F1}%",
            MaterialesGridLength: matLength,
            ManoObraGridLength: moLength,
            OtrosGridLength: otrosLength,
            TotalPartidas: totalPartidas,
            Completas: completas,
            Incompletas: incompletas,
            SinPrecio: sinPrecio,
            Capitulos: capitulosCount,
            CompletionPct: completionPct,
            HealthBadgeText: healthBadge,
            HealthBadgeKind: healthKind,
            HealthDescriptionText: healthDesc);
    }

    private static string FormatAmount(decimal clpAmount, CurrencyPresentationContext ctx)
    {
        if (ctx.Currency == Moneda.CLP)
            return $"$ {clpAmount:N0}";

        decimal delivery = ItemizadoCurrencyCoordinator.ConvertClpToDelivery(clpAmount, ctx);
        return ctx.Currency switch
        {
            Moneda.USD => $"US$ {delivery:N0}",
            Moneda.EUR => $"€ {delivery:N0}",
            _ => $"{delivery:N3} UF"
        };
    }
}
