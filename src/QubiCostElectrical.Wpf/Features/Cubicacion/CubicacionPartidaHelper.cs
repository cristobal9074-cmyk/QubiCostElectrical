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
    private static readonly Regex UnidadRefRegex = new(@"^[\+=]?[CcRrFf](?<idx>\d+)$", RegexOptions.Compiled);

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
                    string rawVal = exprs[c] ?? "";
                    if (decimal.TryParse(rawVal, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal val)
                        || decimal.TryParse(rawVal, NumberStyles.Number, CultureInfo.CurrentCulture, out val))
                    {
                        dict[$"F{r + 1}C{c + 1}"] = val;
                        dict[$"R{r + 1}C{c + 1}"] = val;
                        dict[$"C{r + 1}Z{c + 1}"] = val;
                    }
                }
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
}
