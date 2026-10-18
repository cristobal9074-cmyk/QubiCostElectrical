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
    /// Mapeo canónico natural por defecto para familias y sinónimos de especialidad
    /// cuando el usuario no las ha asignado explícitamente a otra fila de sobreventa en Costos.
    /// Esto permite que el factor configurado por el usuario para una familia (ej: Canalización)
    /// aplique automáticamente a sus variantes (Canalizado, Bandejas, etc.) en cualquier proyecto.
    /// </summary>
    public static string NaturalCanonicalTarget(string? normalized) => (normalized ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        // Canalización y distribución
        "CANALIZACION" or "CANALIZACIONES" or "CANALIZADO" or "CANALIZADOS"
            or "BANDEJAS" or "BANDEJA" or "ESCALERILLAS" or "ESCALERILLA"
            or "TUBERIAS" or "TUBERIA" or "CONDUIT" or "TUBERIAS CONDUIT" or "TUBERIA CONDUIT"
            or "DUCTOS" or "DUCTO" or "CANALETAS" or "CANALETA"
            or "DUCTO BARRA" or "DUCTO BARRAS" or "DUCTOBARRA" or "DUCTOBARRAS"
            => "CANALIZACION",

        // Conductores y alimentadores
        "CONDUCTORES" or "CONDUCTOR" or "CABLES" or "CABLE" or "ALAMBRES" or "ALAMBRE"
            or "ALIMENTADORES" or "ALIMENTADOR" or "SUBALIMENTADORES" or "SUBALIMENTADOR"
            or "CORDONES" or "CORDON"
            => "CONDUCTORES",

        // Puesta a tierra
        "PUESTA A TIERRA" or "PUESTAS A TIERRA" or "TIERRA" or "TIERRAS"
            or "MALLA A TIERRA" or "MALLAS A TIERRA" or "MALLA TIERRA" or "MALLAS DE TIERRA"
            or "ELECTRODOS" or "BARRA COPPERWELD"
            => "PUESTA A TIERRA",

        // Tableros
        "TABLEROS" or "TABLERO" or "GABINETES" or "GABINETE" or "ARMARIOS" or "ARMARIO"
            or "CELDAS" or "CELDA"
            => "TABLEROS",

        // Protecciones
        "PROTECCIONES" or "PROTECCION" or "DISYUNTORES" or "DISYUNTOR"
            or "AUTOMATICOS" or "AUTOMATICO" or "DIFERENCIALES" or "DIFERENCIAL"
            or "FUSIBLES" or "FUSIBLE"
            => "PROTECCIONES",

        // Equipos
        "EQUIPOS" or "EQUIPO" or "MAQUINARIA" or "MAQUINARIAS"
            or "TRANSFORMADORES" or "TRANSFORMADOR" or "TRAFO" or "TRAFOS"
            or "GRUPO ELECTROGENO" or "GRUPO ELECTROGENOS" or "GENERADORES" or "GENERADOR"
            or "UPS" or "INVERSORES" or "INVERSOR"
            or "MEDIA TENSION" or "MEDIA TENSION ELECTRICA" or "MT"
            or "SUBESTACION" or "SUBESTACIONES"
            => "EQUIPOS",

        // Iluminación
        "ILUMINACION" or "LUMINARIAS" or "LUMINARIA" or "LAMPARAS" or "LAMPARA"
            or "FOCOS" or "FOCO" or "PROYECTORES" or "PROYECTOR"
            or "EQUIPOS DE EMERGENCIA" or "LUCES DE EMERGENCIA"
            => "ILUMINACION",

        // Enchufes y artefactos
        "ENCHUFES Y TOMACORRIENTES" or "ENCHUFES" or "ENCHUFE" or "TOMACORRIENTES" or "TOMACORRIENTE"
            or "INTERRUPTORES" or "INTERRUPTOR" or "PLACAS" or "PLACA"
            or "ARTEFACTOS" or "ARTEFACTO"
            => "ENCHUFES Y TOMACORRIENTES",

        // Accesorios y montaje
        "ACCESORIOS" or "ACCESORIO" or "FERRETERIA"
            or "CAJAS" or "CAJA" or "CAJAS DE DERIVACION" or "CAJA DE PASO"
            or "SOPORTES" or "SOPORTE" or "SOPORTACION" or "SOPORTACIONES"
            or "ABRAZADERAS" or "ABRAZADERA"
            or "TERMINALES" or "TERMINAL" or "PRENSACABLES" or "PRENSACABLE"
            or "FIJACIONES" or "PERNOS" or "TORNILLOS"
            => "ACCESORIOS",

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
