using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Regla única (Domain) para normalizar el nombre de familia y resolver su factor de
/// presentación por defecto. Antes esta tabla vivía duplicada en
/// <c>BudgetProjectSession</c> (Application); ahora Application delega aquí.
/// </summary>
public static class FamilyPresentationDefaults
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "ACCESORIOS";
        return name.Trim().ToUpperInvariant()
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U");
    }

    /// <summary>Markup por defecto por familia (idéntico a la tabla histórica de GetFamilyMarkup).</summary>
    public static bool IsCanonical(string? normalized) => normalized is
        "CONDUCTORES" or "CANALIZACION" or "TABLEROS" or "ILUMINACION" or "EQUIPOS"
        or "ACCESORIOS" or "ENCHUFES Y TOMACORRIENTES" or "ENCHUFES"
        or "PROTECCIONES" or "PUESTA A TIERRA";

    /// <summary>
    /// Mapeo canónico natural por defecto para familias de especialidad
    /// cuando el usuario no las ha asignado explícitamente a otra fila de sobreventa en Costos.
    /// </summary>
    public static string NaturalCanonicalTarget(string? normalized) => normalized switch
    {
        "TRANSFORMADORES" or "TRANSFORMADOR" => "EQUIPOS",
        "GRUPO ELECTROGENO" or "GRUPO ELECTROGENOS" or "GENERADORES" or "GENERADOR" => "EQUIPOS",
        "DUCTO BARRA" or "DUCTO BARRAS" or "DUCTOBARRA" or "DUCTOBARRAS" => "CANALIZACION",
        "MEDIA TENSION" or "MEDIA TENSION ELECTRICA" or "MT" => "EQUIPOS",
        _ => normalized ?? "ACCESORIOS"
    };

    /// <summary>
    /// Si la familia viene de la lista de precios y el usuario la movió a una fila canónica,
    /// el factor que aplica es el de esa fila. Las filas canónicas no se redirigen.
    /// Si no tiene asignación explícita en Costos, sigue su objetivo canónico natural.
    /// </summary>
    public static string FollowAssignment(IReadOnlyDictionary<string, string>? assigned, string? family)
    {
        var norm = Normalize(family);
        if (IsCanonical(norm))
            return norm;

        if (assigned != null && assigned.TryGetValue(norm, out var target) && !string.IsNullOrWhiteSpace(target))
        {
            var next = Normalize(target);
            return IsCanonical(next) ? next : norm;
        }

        var natural = NaturalCanonicalTarget(norm);
        return IsCanonical(natural) ? natural : norm;
    }

    public static decimal DefaultMarkup(string normalizedFamily)
    {
        var target = NaturalCanonicalTarget(normalizedFamily);
        return target switch
        {
            "CONDUCTORES" => 1.50m,
            "CANALIZACION" => 1.44m,
            "TABLEROS" => 1.10m,
            "PROTECCIONES" => 1.12m,
            "ILUMINACION" => 1.17m,
            "EQUIPOS" => 1.12m,
            "ENCHUFES Y TOMACORRIENTES" or "ENCHUFES" => 1.15m,
            "PUESTA A TIERRA" => 1.20m,
            _ => 1.14m
        };
    }

    public static decimal ResolveMarkup(IReadOnlyDictionary<string, decimal>? variables, string? family)
    {
        var norm = Normalize(family);
        if (variables != null && variables.TryGetValue($"MarkupFactor_{norm}", out var v))
            return v;
        return DefaultMarkup(norm);
    }

    public static decimal ResolveDiscountPct(IReadOnlyDictionary<string, decimal>? variables, string? family)
    {
        var norm = Normalize(family);
        return variables != null && variables.TryGetValue($"MarkupDiscount_{norm}", out var v)
            ? MarkupPolicy.ClampPct(v)
            : 0m;
    }

    /// <summary>Factor de presentación efectivo: markup × (1 − descuento%).</summary>
    public static decimal ResolveFactor(IReadOnlyDictionary<string, decimal>? variables, string? family)
        => ExcelCostosBridge.PresentationFactor(
            ResolveMarkup(variables, family),
            ResolveDiscountPct(variables, family));
}
