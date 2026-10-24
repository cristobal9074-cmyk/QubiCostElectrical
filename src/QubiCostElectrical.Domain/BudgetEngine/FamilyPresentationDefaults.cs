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
        or "MOVIMIENTOS" or "COMISIONAMIENTO";

    /// <summary>
    /// Mapeo canónico natural por defecto para familias y sinónimos de especialidad
    /// cuando el usuario no las ha asignado explícitamente a otra fila de sobreventa en Costos.
    /// Esto permite que el factor configurado por el usuario para una familia (ej: Canalización)
    /// aplique automáticamente a sus variantes (Canalizado, Bandejas, etc.) en cualquier proyecto.
    /// </summary>
    public static string NaturalCanonicalTarget(string? normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized ?? "ACCESORIOS";

        var clave = ElectricalFamilyCatalog.ClaveCanonica(normalized);
        return clave switch
        {
        // Canalización y distribución (incluye cajas, soportes y abrazaderas como en matriz Excel Ingelsam M02..M04, M09, M10)
        "CANALIZACION" or "CANALIZACIONES" or "CANALIZADO" or "CANALIZADOS"
            or "BANDEJAS" or "BANDEJA" or "ESCALERILLAS" or "ESCALERILLA"
            or "TUBERIAS" or "TUBERIA" or "CONDUIT" or "TUBERIAS CONDUIT" or "TUBERIA CONDUIT"
            or "DUCTOS" or "DUCTO" or "CANALETAS" or "CANALETA"
            or "CAJAS" or "CAJA" or "CAJAS DE DERIVACION" or "CAJA DE PASO"
            or "SOPORTES" or "SOPORTE" or "SOPORTACION" or "SOPORTACIONES"
            or "ABRAZADERAS" or "ABRAZADERA"
            => "CANALIZACION",

        // Conductores, alimentadores y puesta a tierra (en presupuestos electricos van en MATERIALES)
        "CONDUCTORES" or "CONDUCTOR" or "CABLES" or "CABLE" or "ALAMBRES" or "ALAMBRE"
            or "ALIMENTADORES" or "ALIMENTADOR" or "SUBALIMENTADORES" or "SUBALIMENTADOR"
            or "CORDONES" or "CORDON"
            or "OBRAS CIVILES"
            or "PUESTA A TIERRA" or "PUESTAS A TIERRA" or "TIERRA" or "TIERRAS"
            or "MALLA A TIERRA" or "MALLAS A TIERRA" or "MALLA TIERRA" or "MALLAS DE TIERRA"
            or "ELECTRODOS" or "BARRA COPPERWELD"
            => "CONDUCTORES",

        // Tableros y protecciones
        "TABLEROS" or "TABLERO" or "GABINETES" or "GABINETE" or "ARMARIOS" or "ARMARIO"
            or "PROTECCIONES" or "PROTECCION" or "DISYUNTORES" or "DISYUNTOR"
            or "AUTOMATICOS" or "AUTOMATICO" or "DIFERENCIALES" or "DIFERENCIAL"
            or "FUSIBLES" or "FUSIBLE"
            => "TABLEROS",

        // Equipos
        "EQUIPOS" or "EQUIPO" or "MAQUINARIA" or "MAQUINARIAS"
            or "TRANSFORMADORES" or "TRANSFORMADOR" or "TRAFO" or "TRAFOS"
            or "GRUPO ELECTROGENO" or "GRUPO ELECTROGENOS" or "GENERADORES" or "GENERADOR"
            or "UPS" or "INVERSORES" or "INVERSOR"
            or "CELDAS" or "DUCTO BARRA" or "PARARRAYOS" or "SISTEMA DE REMARCACION"
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
            or "TERMINALES" or "TERMINAL" or "PRENSACABLES" or "PRENSACABLE"
            or "FIJACIONES" or "PERNOS" or "TORNILLOS"
            or "DOCUMENTACION Y TRAMITES"
            => "ACCESORIOS",

        _ => string.IsNullOrEmpty(clave) ? "ACCESORIOS" : clave
        };
    }

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

    /// <summary>
    /// Markup por defecto de la familia. Con <paramref name="porDefecto"/> mayor que cero el
    /// proyecto manda (valor estandar 1,00 de los proyectos nuevos); sin ese valor se usa la
    /// tabla historica del producto, que conserva los proyectos ya presupuestados.
    /// </summary>
    public static decimal DefaultMarkup(string normalizedFamily) => DefaultMarkup(normalizedFamily, 0m);

    /// <inheritdoc cref="DefaultMarkup(string)"/>
    public static decimal DefaultMarkup(string normalizedFamily, decimal porDefecto)
    {
        if (porDefecto > 0m)
            return porDefecto;

        var target = NaturalCanonicalTarget(normalizedFamily);
        return target switch
        {
            "CONDUCTORES" => 1.72m,
            "CANALIZACION" => 1.72m,
            "TABLEROS" => 1.44m,
            "ILUMINACION" => 1.45m,
            "EQUIPOS" => 1.43m,
            "ACCESORIOS" => 1.36m,
            "ENCHUFES Y TOMACORRIENTES" or "ENCHUFES" => 1.45m,
            "MOVIMIENTOS" or "COMISIONAMIENTO" => 1.45m,
            _ => 1.45m
        };
    }

    public static decimal ResolveMarkup(IReadOnlyDictionary<string, decimal>? variables, string? family)
        => ResolveMarkup(variables, family, 0m);

    /// <inheritdoc cref="ResolveMarkup(IReadOnlyDictionary{string, decimal}, string)"/>
    public static decimal ResolveMarkup(IReadOnlyDictionary<string, decimal>? variables, string? family, decimal porDefecto)
    {
        var norm = Normalize(family);
        if (variables != null && variables.TryGetValue($"MarkupFactor_{norm}", out var v))
            return v;

        // Regla única en Domain (FamilyPresentationDefaults) para no duplicar la tabla.
        return DefaultMarkup(norm, porDefecto);
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
        => ResolveFactor(variables, family, 0m);

    /// <inheritdoc cref="ResolveFactor(IReadOnlyDictionary{string, decimal}, string)"/>
    public static decimal ResolveFactor(IReadOnlyDictionary<string, decimal>? variables, string? family, decimal porDefecto)
        => ExcelCostosBridge.PresentationFactor(
            ResolveMarkup(variables, family, porDefecto),
            ResolveDiscountPct(variables, family));
}
