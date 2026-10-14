namespace QubiCostElectrical;

/// <summary>
/// Variables R1..Rn para fórmulas de cantidad oficial de partida (modo detalle).
/// </summary>
public static class PartidaCantidadVariablesBuilder
{
    public static Dictionary<string, decimal> Build(IReadOnlyList<decimal> resourceQuantities)
    {
        int count = resourceQuantities?.Count ?? 0;
        int max = Math.Max(count, 100);
        var dict = new Dictionary<string, decimal>(max * 3, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < max; i++)
        {
            var val = (resourceQuantities != null && i < count) ? resourceQuantities[i] : 0m;
            dict[$"R{i + 1}"] = val;
            dict[$"C{i + 1}"] = val;
            dict[$"F{i + 1}"] = val;
        }
        return dict;
    }
}
