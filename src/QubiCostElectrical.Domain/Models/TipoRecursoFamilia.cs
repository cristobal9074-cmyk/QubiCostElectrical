namespace QubiCostElectrical;

/// <summary>
/// Clasificación automática de una línea de cubicación como equipo o material según su familia.
/// Refleja el bloque 1.1 EQUIPOS de la hoja de costos: tableros y equipos (grupo generador,
/// transformador, celdas de media tensión, UPS, ducto de barra, pararrayos) son equipo; el
/// resto de familias son material. Es el mismo criterio con el que la hoja de costos separa
/// el bloque EQUIPOS del bloque de materiales, que es el que define el valor agregado sobre
/// equipos y la base de ferretería.
/// </summary>
public static class TipoRecursoFamilia
{
    public const string Equipo = "EQ";
    public const string Material = "MAT";

    /// <summary>True cuando la familia (o su sinonimia canónica) es de equipos.</summary>
    public static bool EsFamiliaEquipo(string? familia)
    {
        var objetivo = FamilyPresentationDefaults.NaturalCanonicalTarget(FamilyPresentationDefaults.Normalize(familia));
        return objetivo is "TABLEROS" or "EQUIPOS";
    }

    /// <summary>Tipo de recurso que corresponde a la familia: EQ o MAT.</summary>
    public static string Resolver(string? familia) => EsFamiliaEquipo(familia) ? Equipo : Material;
}
