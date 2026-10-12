using System;

namespace QubiCostElectrical;

/// <summary>
/// Política de presentación de montos en documentos exportados (PDF y Excel). Es el único
/// lugar donde los exportadores deciden encabezados y columnas de moneda, para que todos
/// respeten la misma regla de producto:
/// <list type="bullet">
/// <item>Proyecto en <c>CLP</c>: se emite una sola columna en pesos; jamás aparece la UF
/// (ni valores, ni encabezados, ni el valor de la UF de referencia).</item>
/// <item>Proyecto en <c>UF</c>/<c>USD</c>/<c>EUR</c>: se emite la moneda del proyecto y, junto
/// a ella, el equivalente en pesos chilenos para dar visibilidad del precio.</item>
/// </list>
/// El motor guarda los montos en CLP; los campos legacy con sufijo <c>Uf</c> siguen siendo
/// CLP salvo los explícitamente reales en UF (cubicación), que usan los métodos <c>DesdeUf</c>.
/// </summary>
public sealed class ExportCurrencyPresenter
{
    private readonly CurrencyConversionService conversion = new();

    public ExportCurrencyPresenter(string? moneda, decimal valorUfClp, decimal tipoCambioUsdClp)
    {
        Moneda = conversion.NormalizeCurrency(moneda);
        ValorUfClp = valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;
        TipoCambioUsdClp = tipoCambioUsdClp;
    }

    /// <summary>Moneda de presentación normalizada: CLP, UF, USD o EUR.</summary>
    public string Moneda { get; }

    public decimal ValorUfClp { get; }

    public decimal TipoCambioUsdClp { get; }

    /// <summary>true cuando la moneda del proyecto no es CLP: corresponde mostrar UF y su equivalente.</summary>
    public bool MuestraUf => !string.Equals(Moneda, CurrencyConversionService.CurrencyClp, StringComparison.Ordinal);

    /// <summary>Encabezado de la columna principal: la moneda del proyecto o "$" si es CLP.</summary>
    public string EncabezadoPrincipal => MuestraUf ? Moneda : "$";

    /// <summary>Encabezado de la columna de equivalencia en pesos; vacío cuando no corresponde emitirla.</summary>
    public string EncabezadoEquivalenteClp => MuestraUf ? "$" : "";

    /// <summary>Encabezado compuesto: "TOTAL $" en CLP, "TOTAL UF" en el resto.</summary>
    public string Encabezado(string concepto) => $"{concepto} {EncabezadoPrincipal}";

    /// <summary>Columnas de moneda que debe emitir cada fila: 1 en CLP, 2 en el resto.</summary>
    public int ColumnasMoneda => MuestraUf ? 2 : 1;

    /// <summary>Importe principal de un monto CLP (moneda base del motor).</summary>
    public string DesdeClp(decimal clp) =>
        conversion.FormatMoneyFromClp(clp, Moneda, ValorUfClp, TipoCambioUsdClp);

    /// <summary>Equivalente en pesos de un monto CLP; cadena vacía cuando el proyecto ya está en CLP.</summary>
    public string EquivalenteClp(decimal clp) => MuestraUf ? Pesos(clp) : "";

    /// <summary>Importe principal de un monto expresado en UF real (cubicación).</summary>
    public string DesdeUf(decimal uf) =>
        MuestraUf ? conversion.FormatMoneyFromUf(uf, Moneda, ValorUfClp, TipoCambioUsdClp) : Pesos(uf * ValorUfClp);

    /// <summary>Equivalente en pesos de un monto en UF real; cadena vacía cuando el proyecto está en CLP.</summary>
    public string EquivalenteClpDesdeUf(decimal uf) => MuestraUf ? Pesos(uf * ValorUfClp) : "";

    private static string Pesos(decimal valor) => $"$ {Money.Round(valor):N0}";
}
