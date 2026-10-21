using System;
using System.Collections.Generic;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Decide si el texto de una cantidad puede escribirse al modelo.
/// Una fórmula que no calcula no se convierte en cero.
/// </summary>
internal static class CubicacionOpenCellRules
{
    public static bool TryAcceptQuantityText(
        string? text,
        IReadOnlyDictionary<string, decimal>? variables,
        out string error)
    {
        string clean = (text ?? "").Trim();
        var vars = variables as Dictionary<string, decimal>
            ?? new Dictionary<string, decimal>(variables ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase);

        if (CubicacionPartidaHelper.TryParsePartidaCantidad(clean, vars, out _, out _))
        {
            error = "";
            return true;
        }

        if (QuantityFormulaService.IsFormulaLike(clean))
        {
            string evalStr = clean.TrimEnd('+', '-', '*', '/', '^', '(', ',');
            var result = QuantityFormulaService.Evaluate(evalStr, vars);
            error = string.IsNullOrWhiteSpace(result.Error)
                ? "La fórmula no se puede calcular."
                : result.Error;
            return false;
        }

        error = "Cantidad no válida.";
        return false;
    }
}
