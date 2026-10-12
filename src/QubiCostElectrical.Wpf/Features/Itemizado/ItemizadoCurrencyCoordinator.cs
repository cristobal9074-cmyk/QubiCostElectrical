using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed record CurrencyPresentationContext(
    string Currency,
    decimal ValorUfClp,
    decimal TipoCambioUsd,
    decimal TipoCambioEur)
{
    public string UnitPriceHeader => PriceConversion.GetCurrencyHeaderLabel("P.U.", Currency);
    public string TotalHeader => PriceConversion.GetCurrencyHeaderLabel("TOTAL", Currency);
}

/// <summary>
/// Coordina la conversión y presentación de precios del Itemizado Valorado en las 4 monedas
/// de entrega (UF, CLP, USD, EUR), manteniendo todo el cálculo interno en pesos chilenos (CLP).
/// </summary>
public static class ItemizadoCurrencyCoordinator
{
    public const string DefaultCurrency = Moneda.UF;

    public static CurrencyPresentationContext CreateContext(
        string? currency,
        decimal valorUfClp,
        decimal tipoCambioUsd,
        decimal tipoCambioEur)
    {
        string c = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (c != Moneda.UF && c != Moneda.CLP && c != Moneda.USD && c != Moneda.EUR)
            c = DefaultCurrency;

        return new CurrencyPresentationContext(
            Currency: c,
            ValorUfClp: valorUfClp > 0m ? valorUfClp : 39000m,
            TipoCambioUsd: tipoCambioUsd > 0m ? tipoCambioUsd : 950m,
            TipoCambioEur: tipoCambioEur > 0m ? tipoCambioEur : 1050m);
    }

    /// <summary>
    /// Convierte un monto base en CLP a la moneda de presentación activa.
    /// </summary>
    public static decimal ConvertClpToDelivery(decimal amountClp, CurrencyPresentationContext ctx)
    {
        return PriceConversion.ConvertToDeliveryCurrency(
            amountClp,
            ctx.Currency,
            ctx.ValorUfClp,
            ctx.TipoCambioUsd,
            ctx.TipoCambioEur);
    }

    /// <summary>
    /// Convierte un monto que viene históricamente en UF a CLP usando la tasa UF del proyecto.
    /// </summary>
    public static decimal ConvertUfToClp(decimal amountUf, CurrencyPresentationContext ctx)
    {
        return amountUf * ctx.ValorUfClp;
    }

    /// <summary>
    /// Obtiene el texto formateado de un precio unitario para la moneda activa.
    /// </summary>
    public static string FormatUnitPrice(decimal unitPriceInCurrency, CurrencyPresentationContext ctx)
    {
        return PriceConversion.FormatDeliveryPrice(unitPriceInCurrency, ctx.Currency, isUnitPrice: true);
    }

    /// <summary>
    /// Obtiene el texto formateado de un total para la moneda activa.
    /// </summary>
    public static string FormatTotal(decimal totalInCurrency, CurrencyPresentationContext ctx)
    {
        return PriceConversion.FormatDeliveryPrice(totalInCurrency, ctx.Currency, isUnitPrice: false);
    }

    /// <summary>
    /// Genera la leyenda de paridad activa para el sticky footer.
    /// </summary>
    public static string BuildRateCaption(CurrencyPresentationContext ctx)
    {
        return ctx.Currency switch
        {
            Moneda.USD => $"Paridad USD al día: ${ctx.TipoCambioUsd:N0}",
            Moneda.EUR => $"Paridad EUR al día: ${ctx.TipoCambioEur:N0}",
            Moneda.CLP => "Moneda base: Pesos Chilenos (CLP)",
            _ => $"Paridad UF al día: ${ctx.ValorUfClp:N0}"
        };
    }

    /// <summary>
    /// Precio unitario en la moneda de entrega, con el equivalente en CLP entre paréntesis.
    /// Regla de producto: en CLP se muestra solo pesos (sin UF); en UF/USD/EUR se agrega el CLP.
    /// </summary>
    public static string FormatUnitPricePair(decimal unitPriceUf, CurrencyPresentationContext ctx)
        => FormatPair(unitPriceUf, ctx, isUnitPrice: true, decimals: 3);

    /// <summary>
    /// Total en la moneda de entrega, con el equivalente en CLP entre paréntesis.
    /// Regla de producto: en CLP se muestra solo pesos (sin UF); en UF/USD/EUR se agrega el CLP.
    /// </summary>
    public static string FormatTotalPair(decimal totalUf, CurrencyPresentationContext ctx)
        => FormatPair(totalUf, ctx, isUnitPrice: false, decimals: 2);

    private static string FormatPair(decimal amountUf, CurrencyPresentationContext ctx, bool isUnitPrice, int decimals)
    {
        decimal clp = amountUf * ctx.ValorUfClp;
        if (ctx.Currency == Moneda.CLP)
            return $"$ {clp:N0}";

        decimal converted = ConvertClpToDelivery(clp, ctx);
        string primary = ctx.Currency switch
        {
            Moneda.USD => $"US$ {converted:N2}",
            Moneda.EUR => $"€ {converted:N2}",
            _ => $"{converted.ToString($"N{decimals}", System.Globalization.CultureInfo.CurrentCulture)} UF"
        };
        return $"{primary} ($ {clp:N0})";
    }
}
