namespace QubiCostElectrical;

/// <summary>
/// Variables R1..Rn para fórmulas de cantidad oficial de partida (modo detalle).
/// </summary>
public static class PartidaCantidadVariablesBuilder
{
    public static Dictionary<string, decimal> Build(IReadOnlyList<decimal> resourceQuantities)
    {
        var dict = new Dictionary<string, decimal>(
            resourceQuantities?.Count ?? 0,
            StringComparer.OrdinalIgnoreCase);
        if (resourceQuantities == null)
            return dict;

        for (var i = 0; i < resourceQuantities.Count; i++)
            dict[$"R{i + 1}"] = resourceQuantities[i];
        return dict;
    }
}
