namespace QubiCostElectrical;

public sealed class TorreZonaConfig
{
    public string Nombre { get; init; } = "";
    public int Pisos { get; init; }
    public int Cubiertas { get; init; }
}

/// <summary>
/// Reglas de validación y algoritmo de generación de códigos de zona (subterráneos, pisos
/// comunes, pisos y cubiertas por torre) para el asistente de armado de edificio. Antes vivía
/// como código-behind de AsistenteEdificioDialog.xaml.cs — una regla de negocio (rangos válidos,
/// consistencia pisos-totales vs pisos-comunes, el algoritmo de nombrado de zonas) atada a una
/// ventana WPF y sin poder testearse sin instanciar un Window.
/// </summary>
public static class BuildingZoneGenerator
{
    public const int MaxSubterraneos = 50;
    public const int MaxPisosComunes = 50;
    public const int MaxPisosTorre = 100;
    public const int MaxCubiertas = 10;

    /// <summary>Primer error de validación encontrado, o null si los parámetros son válidos.</summary>
    public static string? Validate(int subterraneos, int torresCount, int pisosComunes, IReadOnlyList<TorreZonaConfig> torres)
    {
        if (subterraneos < 0 || subterraneos > MaxSubterraneos)
            return $"Ingrese una cantidad válida de subterráneos (0 - {MaxSubterraneos}).";

        if (torresCount > 1 && (pisosComunes < 0 || pisosComunes > MaxPisosComunes))
            return $"Ingrese una cantidad válida de pisos comunes (0 - {MaxPisosComunes}).";

        foreach (var t in torres)
        {
            if (t.Pisos < 0 || t.Pisos > MaxPisosTorre)
                return $"Ingrese una cantidad válida de pisos para la {t.Nombre} (0 - {MaxPisosTorre}).";
            if (t.Pisos < pisosComunes)
                return $"Los pisos totales ({t.Pisos}) de la {t.Nombre} no pueden ser menores que los pisos comunes ({pisosComunes}).";
            if (t.Cubiertas < 0 || t.Cubiertas > MaxCubiertas)
                return $"Ingrese una cantidad válida de cubiertas para la {t.Nombre} (0 - {MaxCubiertas}).";
        }

        return null;
    }

    /// <summary>Genera los códigos de zona para un edificio de N torres, en el mismo orden que espera la UI (subterráneos, pisos comunes, pisos y cubiertas por torre).</summary>
    public static List<string> GenerateZones(
        int subterraneos,
        int torresCount,
        int pisosComunes,
        bool subterraneoComun,
        string prefijoTipo,
        IReadOnlyList<TorreZonaConfig> torres)
    {
        var zonas = new List<string>();

        // 1. Subterráneos comunes (compartidos entre torres)
        if (torresCount > 1 && subterraneoComun)
        {
            for (int s = subterraneos; s >= 1; s--)
                zonas.Add($"S{s}");
        }

        // 2. Subterráneos independientes (si no son comunes)
        if (torresCount == 1 || !subterraneoComun)
        {
            for (int t = 0; t < torresCount; t++)
            {
                string prefijo = TorrePrefijo(torresCount, t, prefijoTipo);
                for (int s = subterraneos; s >= 1; s--)
                    zonas.Add($"{prefijo}S{s}");
            }
        }

        // 3. Pisos comunes (compartidos entre torres)
        if (torresCount > 1 && pisosComunes > 0)
        {
            for (int p = 1; p <= pisosComunes; p++)
                zonas.Add($"P{p}");
        }

        // 4. Pisos independientes por cada torre (a partir del piso común + 1) y sus cubiertas
        for (int t = 0; t < torresCount; t++)
        {
            var config = torres[t];
            string prefijo = TorrePrefijo(torresCount, t, prefijoTipo);

            int primerPisoIndep = torresCount > 1 ? pisosComunes + 1 : 1;
            for (int p = primerPisoIndep; p <= config.Pisos; p++)
                zonas.Add($"{prefijo}P{p}");

            if (config.Cubiertas == 1)
            {
                zonas.Add($"{prefijo}CUB");
            }
            else if (config.Cubiertas > 1)
            {
                for (int c = 1; c <= config.Cubiertas; c++)
                    zonas.Add($"{prefijo}CUB{c}");
            }
        }

        return zonas;
    }

    private static string TorrePrefijo(int torresCount, int index, string prefijoTipo) =>
        torresCount > 1
            ? (prefijoTipo == "LETRAS" ? $"T{(char)('A' + index)}-" : $"T{index + 1}-")
            : "";
}
