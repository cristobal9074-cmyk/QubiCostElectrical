using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Utilidades para resolver fórmulas y referencias de celdas/casillas en cabeceras de partida (UD y CANT).
/// </summary>
internal static class CubicacionPartidaHelper
{
    private static readonly Regex UnidadRefRegex = new(@"^[\+=]?[UuCcRrFf](?<idx>\d+)$", RegexOptions.Compiled);

    public static string ResolveUnidadReference(string input, IReadOnlyList<CubicacionRowVm>? rows)
    {
        if (string.IsNullOrWhiteSpace(input) || rows == null || rows.Count == 0)
            return (input ?? "").Trim();

        string trimmed = input.Trim();
        var m = UnidadRefRegex.Match(trimmed);
        if (m.Success && int.TryParse(m.Groups["idx"].Value, out int idx) && idx >= 1 && idx <= rows.Count)
        {
            var targetRow = rows[idx - 1];
            return (targetRow.Unidad ?? "").Trim();
        }

        return trimmed;
    }

    public static Dictionary<string, decimal> BuildVariables(IReadOnlyList<CubicacionRowVm>? rows, BudgetProjectSession session)
    {
        var rowList = rows ?? Array.Empty<CubicacionRowVm>();
        var dict = PartidaCantidadVariablesBuilder.Build(rowList.Select(r => r.Cantidad).ToList());

        for (int r = 0; r < rowList.Count; r++)
        {
            var row = rowList[r];
            var exprs = row.Source.QtyExpressions;
            if (exprs != null)
            {
                for (int c = 0; c < exprs.Length; c++)
                {
                    string rawVal = (exprs[c] ?? "").Trim();
                    if (string.IsNullOrEmpty(rawVal)) continue;

                    if (decimal.TryParse(rawVal, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal val)
                        || decimal.TryParse(rawVal, NumberStyles.Number, CultureInfo.CurrentCulture, out val))
                    {
                        dict[$"F{r + 1}C{c + 1}"] = val;
                        dict[$"R{r + 1}C{c + 1}"] = val;
                        dict[$"C{r + 1}Z{c + 1}"] = val;
                    }
                    else
                    {
                        var eval = QuantityFormulaService.Evaluate(rawVal, dict);
                        if (eval.Success)
                        {
                            dict[$"F{r + 1}C{c + 1}"] = eval.Value;
                            dict[$"R{r + 1}C{c + 1}"] = eval.Value;
                            dict[$"C{r + 1}Z{c + 1}"] = eval.Value;
                        }
                    }
                }
            }
        }

        // Casillas vacías por defecto en 0m para permitir fórmulas que referencien celdas vacías
        for (int r = 1; r <= Math.Max(rowList.Count, 30); r++)
        {
            for (int c = 1; c <= 20; c++)
            {
                dict.TryAdd($"F{r}C{c}", 0m);
                dict.TryAdd($"R{r}C{c}", 0m);
                dict.TryAdd($"C{r}Z{c}", 0m);
            }
        }

        foreach (var kv in session.ProjectVariables)
        {
            if (QuantityFormulaService.IsValidVariableName(kv.Key))
                dict[QuantityFormulaService.NormalizeVariableName(kv.Key)] = kv.Value;
        }

        dict["m2"] = session.MetrosCuadrados;
        dict["metros_cuadrados"] = session.MetrosCuadrados;
        dict["valor_uf"] = session.ValorUfClp;
        dict["uf_clp"] = session.ValorUfClp;
        dict["dolar_clp"] = session.TipoCambioUsdClp;

        return dict;
    }

    public static void SincronizarMaestroDesdePartida(
        IReadOnlyList<CubicacionRowVm>? rows,
        decimal parsed,
        BudgetProjectSession session,
        Action<CubicacionRowVm> validateRow)
    {
        if (rows == null || rows.Count == 0) return;

        bool tieneEsclavos = rows.Any(r => r.Source.Esclavo != null);
        if (!tieneEsclavos) return;

        var maestroVm = rows.FirstOrDefault(r => r.Source.Esclavo == null &&
            rows.Any(other => other.Source.Esclavo != null && CodeHelper.Match(other.Source.Esclavo.MaestroCodigo, r.Codigo)))
            ?? rows.FirstOrDefault(r => r.Source.Esclavo == null);

        if (maestroVm == null || maestroVm.HasFormula || maestroVm.Cantidad == parsed) return;

        maestroVm.Cantidad = parsed;
        maestroVm.RefreshDisplayProperties();
        validateRow(maestroVm);
        session.SyncCubicacionRowComposition(maestroVm.Source);
    }

    public static bool TryParsePartidaCantidad(
        string clean,
        Dictionary<string, decimal> variables,
        out decimal parsed,
        out string formula)
    {
        formula = "";
        if (string.IsNullOrEmpty(clean))
        {
            parsed = 0m;
            return true;
        }

        if (QuantityFormulaService.IsFormulaLike(clean))
        {
            string evalStr = clean.TrimEnd('+', '-', '*', '/', '^', '(', ',');
            var result = QuantityFormulaService.Evaluate(evalStr, variables);
            parsed = result.Value;
            if (result.Success) formula = clean;
            return result.Success;
        }

        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)
            || decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }

    public static string GetAlertaMensaje(PartidaListItemVm? partida)
    {
        if (partida == null || !partida.SinValorizacionGlobal)
            return "";

        if (partida.HasMissingGlobalUnidad && partida.HasMissingGlobalCantidad)
            return "Partida cubicada sin valorización global: Esta partida cuenta con materiales cubicados, pero no tiene definida su unidad ni cantidad global en el encabezado. Ingrese la Unidad y Cantidad arriba para que se valorice en el presupuesto y la oferta.";

        if (partida.HasMissingGlobalCantidad)
            return "Partida cubicada sin cantidad global: Esta partida cuenta con materiales cubicados, pero su cantidad global arriba es 0. Ingrese la Cantidad de la partida para que se valorice en el presupuesto y la oferta.";

        return "Partida cubicada sin unidad global: Esta partida cuenta con materiales cubicados, pero no tiene definida su unidad de medida arriba (ej. GL, UN, M, ML). Defina la Unidad de la partida para valorizarla en el presupuesto y la oferta.";
    }
}
