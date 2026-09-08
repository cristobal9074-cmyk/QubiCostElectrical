namespace QubiCostElectrical;

/// <summary>
/// Compacta expresiones de sector quitando trailing vacíos (misma regla que la matriz Cub).
/// </summary>
public static class CubicacionSectorExpressionCompactor
{
    public static string[] TrimTrailingEmpty(string[] expressions)
    {
        if (expressions == null || expressions.Length == 0)
            return [];

        var last = expressions.Length - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(expressions[last]))
            last--;

        if (last < 0)
            return [];

        var result = new string[last + 1];
        Array.Copy(expressions, result, result.Length);
        return result;
    }
}
