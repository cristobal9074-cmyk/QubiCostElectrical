using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public interface ICubicacionTakeoffQueryService
{
    Dictionary<string, decimal> BuildFormulaVariables(BudgetProjectSession session, string itemId);
    decimal EvaluateStoredExpressions(BudgetProjectSession session, CubicacionRow row, int qtyColumnCount = 300);

    // Igual que EvaluateStoredExpressions, pero además invoca onZoneComputed(indice, valor) por
    // cada sector con expresión propia — usado para mostrar en la celda el RESULTADO de una
    // fórmula de sector (ej. "2+3" → se ve "5"), no el texto de la fórmula, igual que Excel.
    // No repite trabajo: se calcula en la misma pasada que ya hacía EvaluateStoredExpressions.
    decimal EvaluateStoredExpressions(BudgetProjectSession session, CubicacionRow row, int qtyColumnCount, Action<int, decimal> onZoneComputed);

    void RecalculateAll(BudgetProjectSession session, int qtyColumnCount = 300);
    void RecalculateForItem(BudgetProjectSession session, string itemId, int qtyColumnCount = 300);
    decimal ApplyRounding(CubicacionRow row, decimal value);
    string[] NormalizeExpressions(string[]? expressions, int count);
}

public sealed partial class CubicacionTakeoffQueryService : ICubicacionTakeoffQueryService
{
    private static readonly string[] DefaultHeaders300 = Enumerable.Range(1, 300).Select(i => $"Zona {i}").ToArray();
    private static readonly string[] QVarNames = Enumerable.Range(1, 301).Select(i => $"q{i}").ToArray();
    private static readonly string[] ZonaVarNames = Enumerable.Range(1, 301).Select(i => $"zona{i}").ToArray();
    private static readonly string[] ZonaUnderlineVarNames = Enumerable.Range(1, 301).Select(i => $"zona_{i}").ToArray();
}
