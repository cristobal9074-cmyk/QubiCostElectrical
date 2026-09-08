using System;

namespace QubiCostElectrical;

public sealed class CurrencyConversionService : ICurrencyConversionService
{
    public const string CurrencyUf = "UF";
    public const string CurrencyClp = "CLP";
    public const string CurrencyUsd = "USD";

    public string NormalizeCurrency(string? currency)
    {
        string value = (currency ?? CurrencyUf).Trim().ToUpperInvariant();
        return value switch
        {
            "CLP" or "PESO" or "PESOS" => CurrencyClp,
            "USD" or "DOLAR" or "DÓLAR" or "US$" => CurrencyUsd,
            _ => CurrencyUf
        };
    }

    public string GetNumberFormat(string? currency)
    {
        return NormalizeCurrency(currency) switch
        {
            CurrencyClp => "N0",
            CurrencyUsd => "N2",
            _ => "N3"
        };
    }

    public decimal ConvertUfToClp(decimal uf, decimal valorUfClp)
    {
        if (valorUfClp <= 0m) return uf;
        return uf * valorUfClp;
    }

    public decimal ConvertClpToUf(decimal clp, decimal valorUfClp)
    {
        decimal rate = valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;
        return Math.Round(clp / rate, 6, MidpointRounding.AwayFromZero);
    }

    public decimal ConvertUfToUsd(decimal uf, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        if (valorUfClp <= 0m || tipoCambioUsdClp <= 0m) return uf;
        return uf * valorUfClp / tipoCambioUsdClp;
    }

    public decimal ConvertUsdToUf(decimal usd, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        if (valorUfClp <= 0m || tipoCambioUsdClp <= 0m) return usd;
        return Math.Round(usd * tipoCambioUsdClp / valorUfClp, 6, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Convierte un valor en CLP (moneda base del motor) a la moneda de visualización.
    /// Para CLP devuelve el mismo valor. Para USD divide por tipoCambio. Para UF divide por valorUfClp.
    /// </summary>
    public decimal ConvertClpToCurrency(decimal clp, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        return NormalizeCurrency(currency) switch
        {
            CurrencyClp => clp,
            CurrencyUsd => tipoCambioUsdClp > 0m ? Math.Round(clp / tipoCambioUsdClp, 2, MidpointRounding.AwayFromZero) : clp,
            CurrencyUf  => PriceConversion.ClpToUf(clp, valorUfClp),
            _ => clp
        };
    }

    // ---- Compatibilidad: los métodos *FromUf siguen existiendo pero ahora reciben CLP ----
    // (el motor ya devuelve CLP; el nombre es legado)
    public decimal ConvertUfToCurrency(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        return NormalizeCurrency(currency) switch
        {
            CurrencyClp => ConvertUfToClp(uf, valorUfClp),
            CurrencyUsd => ConvertUfToUsd(uf, valorUfClp, tipoCambioUsdClp),
            _ => uf
        };
    }

    public decimal ConvertCurrencyToUf(decimal value, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        return NormalizeCurrency(currency) switch
        {
            CurrencyClp => ConvertClpToUf(value, valorUfClp),
            CurrencyUsd => ConvertUsdToUf(value, valorUfClp, tipoCambioUsdClp),
            _ => value
        };
    }

    /// <summary>
    /// Formatea un valor CLP (moneda base del motor) a la moneda de visualización del proyecto.
    /// </summary>
    public string FormatMoneyFromClp(decimal clp, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        string normalized = NormalizeCurrency(currency);
        decimal value = ConvertClpToCurrency(clp, normalized, valorUfClp, tipoCambioUsdClp);

        return normalized switch
        {
            CurrencyClp => $"$ {value:N0}",
            CurrencyUsd => $"US$ {value:N2}",
            CurrencyUf  => $"{value:N3} UF",
            _ => $"$ {value:N0}"
        };
    }

    public string FormatMoneyFromUf(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        string normalized = NormalizeCurrency(currency);
        decimal value = ConvertUfToCurrency(uf, normalized, valorUfClp, tipoCambioUsdClp);

        return normalized switch
        {
            CurrencyClp => $"$ {value:N0} CLP",
            CurrencyUsd => $"US$ {value:N2}",
            _ => $"{value:N3} UF"
        };
    }

    public string FormatMoneyFromUfCompact(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        string normalized = NormalizeCurrency(currency);
        decimal value = ConvertUfToCurrency(uf, normalized, valorUfClp, tipoCambioUsdClp);

        return normalized switch
        {
            CurrencyClp => $"$ {value:N0}",
            CurrencyUsd => $"US$ {value:N2}",
            _ => $"{value:N3} UF"
        };
    }
}
