namespace QubiCostElectrical;

/// <summary>
/// Navegación Ctrl+flecha entre sectores (misma regla Excel: borde de bloque contiguo).
/// </summary>
public static class CubicacionSectorNavigation
{
    public static bool HasContent(string[]? expressions, int index)
        => expressions != null
           && index >= 0
           && index < expressions.Length
           && !string.IsNullOrWhiteSpace(expressions[index]);

    public static int FindDataEdge(string[]? expressions, int current, int direction, int max)
    {
        int next = current + direction;
        if (next < 0) return 0;
        if (next >= max) return max - 1;

        if (HasContent(expressions, current) && HasContent(expressions, next))
        {
            int i = next;
            while (i + direction >= 0 && i + direction < max && HasContent(expressions, i + direction))
                i += direction;
            return i;
        }

        int j = next;
        while (j >= 0 && j < max && !HasContent(expressions, j))
            j += direction;
        return j < 0 ? 0 : j >= max ? max - 1 : j;
    }
}
