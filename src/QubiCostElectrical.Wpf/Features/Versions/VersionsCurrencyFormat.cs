using System;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Versions;

/// <summary>
/// Traduce importes de Versiones a la moneda de presentación del proyecto. Los campos con
/// sufijo <c>Uf</c> del motor vienen en dos sabores: unos son UF reales (comparador de
/// revisiones, derivados de <c>CubicacionRow.UfTotal</c>) y otros son CLP con nombre legado
/// (<c>BudgetVersionRow.MontoVentaUf</c>). Regla de producto: con CLP nunca se muestra UF;
/// con UF se muestra la UF y, donde el contexto lo pide, su equivalente en pesos.
/// </summary>
public sealed class VersionsCurrencyFormat
{
    public VersionsCurrencyFormat(bool muestraUf, decimal ufRate)
    {
        MuestraUf = muestraUf;
        UfRate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp;
    }

    public bool MuestraUf { get; }
    public decimal UfRate { get; }

    /// <summary>Importe que el motor entrega en UF.</summary>
    public string FromUf(decimal uf) => MuestraUf
        ? $"{uf:N3} UF"
        : $"$ {Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero):N0}";

    /// <summary>Delta de un importe que el motor entrega en UF.</summary>
    public string FromUfSigned(decimal uf) => uf == 0m
        ? "—"
        : MuestraUf
            ? $"{uf:+0.000;-0.000;—} UF"
            : $"{(uf > 0m ? "+" : "-")}$ {Math.Round(Math.Abs(uf) * UfRate, 0, MidpointRounding.AwayFromZero):N0}";

    /// <summary>Importe en pesos chilenos guardado en un campo con sufijo Uf (nombre legado).</summary>
    public string FromClp(decimal clp) => MuestraUf
        ? $"{PriceConversion.ClpToUf(clp, UfRate):N3} UF"
        : $"$ {Math.Round(clp, 0, MidpointRounding.AwayFromZero):N0}";

    /// <summary>Delta de un importe en pesos chilenos guardado en un campo con sufijo Uf.</summary>
    public string FromClpSigned(decimal clp) => clp == 0m
        ? "—"
        : MuestraUf
            ? $"{PriceConversion.ClpToUf(clp, UfRate):+0.000;-0.000;—} UF"
            : $"{(clp > 0m ? "+" : "-")}$ {Math.Round(Math.Abs(clp), 0, MidpointRounding.AwayFromZero):N0}";
}
