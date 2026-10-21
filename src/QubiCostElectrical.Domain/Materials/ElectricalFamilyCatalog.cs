using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

/// <summary>
/// Catálogo maestro de familias eléctricas de la aplicación.
/// 
/// Provee las familias estándar (tanto tradicionales como de especialidad: Transformadores,
/// Grupo electrógeno, Ducto barra, Media tensión, etc.) y un algoritmo de combinación inteligente
/// que fusiona las familias de la lista de precios externa, las del proyecto y las estándar internas
/// garantizando que NUNCA colisionen ni se dupliquen por diferencias de mayúsculas, tildes o plurales.
/// </summary>
public static class ElectricalFamilyCatalog
{
    /// <summary>
    /// Familias eléctricas estándar internas de la aplicación.
    /// Siempre disponibles de fábrica en la interfaz para selección y alta rápida.
    /// </summary>
    public static readonly IReadOnlyList<string> FamiliasEstandar =
    [
        "Conductores",
        "Canalización",
        "Tableros",
        "Protecciones",
        "Iluminación",
        "Enchufes y tomacorrientes",
        "Equipos",
        "Transformadores",
        "Grupo electrógeno",
        "Celdas",
        "UPS",
        "Ducto de barra",
        "Pararrayos",
        "Obras civiles",
        "Sistema de remarcación",
        "Documentación y trámites",
        "Media tensión",
        "Puesta a tierra",
        "Accesorios"
    ];

    /// <summary>
    /// Clave canónica de comparación para detectar si dos nombres corresponden a la misma familia
    /// (ignora mayúsculas/minúsculas, tildes, plurales simples y unifica sinónimos frecuentes).
    /// </summary>
    public static string ClaveCanonica(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return "";

        string compacto = Compactar(FamilyPresentationDefaults.Normalize(nombre));
        if (compacto.Length == 0)
            return "";

        if (EsUps(compacto)) return "UPS";
        if (EsCeldas(compacto)) return "CELDAS";
        if (EsPararrayos(compacto)) return "PARARRAYOS";
        if (EsObrasCiviles(compacto)) return "OBRAS CIVILES";
        if (EsRemarcacion(compacto)) return "SISTEMA DE REMARCACION";
        if (EsDocumentacion(compacto)) return "DOCUMENTACION Y TRAMITES";

        // Unificación de sinónimos y variantes frecuentes en presupuestos eléctricos
        return compacto switch
        {
            "GENERADOR" or "GENERADORES" or "GRUPO ELECTROGENO" or "GRUPOS ELECTROGENOS"
                or "GRUPO GENERADOR" or "GRUPOS GENERADORES" => "GRUPO ELECTROGENO",
            "TRANSFORMADOR" or "TRANSFORMADORES" or "TRAFO" or "TRAFOS" => "TRANSFORMADORES",
            "DUCTO BARRA" or "DUCTO BARRAS" or "DUCTO DE BARRA" or "DUCTOS DE BARRA"
                or "DUCTOBARRA" or "DUCTOBARRAS" => "DUCTO BARRA",
            "MEDIA TENSION" or "MEDIA TENSION ELECTRICA" or "MT" => "MEDIA TENSION",
            "CONDUCTOR" or "CONDUCTORES" => "CONDUCTORES",
            "CANALIZACION" or "CANALIZACIONES" => "CANALIZACION",
            "TABLERO" or "TABLEROS" => "TABLEROS",
            "PROTECCION" or "PROTECCIONES" => "PROTECCIONES",
            "LUMINARIA" or "LUMINARIAS" or "ILUMINACION" => "ILUMINACION",
            "ARTEFACTO" or "ARTEFACTOS" or "ENCHUFE" or "ENCHUFES" or "ENCHUFES Y TOMACORRIENTES" => "ENCHUFES Y TOMACORRIENTES",
            "EQUIPO" or "EQUIPOS" => "EQUIPOS",
            "ACCESORIO" or "ACCESORIOS" => "ACCESORIOS",
            "PUESTA A TIERRA" or "PAT" or "TIERRA" => "PUESTA A TIERRA",
            _ => compacto
        };
    }

    /// <summary>Deja letras, números y un solo espacio. Así "as-built" y "as built" comparan igual.</summary>
    private static string Compactar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        bool espacio = false;
        foreach (char c in texto)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                espacio = false;
            }
            else if (!espacio && sb.Length > 0)
            {
                sb.Append(' ');
                espacio = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static bool EsUps(string compacto) => compacto is "UPS" or "U P S";

    private static bool EsCeldas(string compacto)
        => compacto is "CELDA" or "CELDAS" || compacto.StartsWith("CELDAS ", StringComparison.Ordinal) || compacto.StartsWith("CELDA ", StringComparison.Ordinal);

    private static bool EsPararrayos(string compacto)
        => compacto.StartsWith("PARARRAYO", StringComparison.Ordinal);

    private static bool EsObrasCiviles(string compacto)
        => compacto is "OBRA CIVIL" or "OBRAS CIVILES"
           || compacto.StartsWith("OBRA CIVIL ", StringComparison.Ordinal)
           || compacto.StartsWith("OBRAS CIVILES ", StringComparison.Ordinal);

    private static bool EsRemarcacion(string compacto)
        => compacto.Contains("REMARCACION", StringComparison.Ordinal);

    private static bool EsDocumentacion(string compacto)
        => compacto.Contains("DOCUMENTACION", StringComparison.Ordinal)
           || compacto.Contains("TRAMITE", StringComparison.Ordinal)
           || compacto.Contains("AS BUILT", StringComparison.Ordinal);

    /// <summary>
    /// Combina las familias provistas desde fuentes externas (lista de precio, materiales del proyecto)
    /// junto con las familias estándar internas de la aplicación.
    ///
    /// Reglas:
    /// 1. Si la lista de precios ya incluye una familia (ej: "Transformadores" o "Generadores"),
    ///    se respeta esa forma exacta y no se agrega duplicada.
    /// 2. Si la lista de precios no la incluye, se agregan las familias estándar internas
    ///    (Transformadores, Grupo electrógeno, Ducto barra, Media tensión, etc.) para que estén
    ///    disponibles de inmediato en autocompletados y desplegables.
    /// 3. Las familias nuevas que el usuario cree en la sesión se incorporan automáticamente.
    /// 4. El resultado final se devuelve ordenado alfabéticamente sin duplicados.
    /// </summary>
    public static IReadOnlyList<string> CombinarFamilias(params IEnumerable<string?>[] colecciones)
        => CombinarFamiliasConOcultas(null, colecciones);

    /// <summary>
    /// Combina las familias provistas desde fuentes externas y estándar, excluyendo las familias
    /// que el usuario haya ocultado explícitamente en el proyecto.
    /// </summary>
    public static IReadOnlyList<string> CombinarFamiliasConOcultas(IEnumerable<string>? familiasOcultas, params IEnumerable<string?>[] colecciones)
    {
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (familiasOcultas != null)
        {
            foreach (var o in familiasOcultas)
            {
                var c = ClaveCanonica(o);
                if (!string.IsNullOrEmpty(c))
                    ocultas.Add(c);
            }
        }

        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resultado = new List<string>();

        void Agregar(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return;

            string limpio = nombre.Trim();
            string clave = ClaveCanonica(limpio);
            if (string.IsNullOrEmpty(clave) || ocultas.Contains(clave))
                return;

            if (vistas.Add(clave))
                resultado.Add(limpio);
        }

        // Primero se procesan las fuentes del proyecto y lista de precios (tienen prioridad de texto)
        if (colecciones != null)
        {
            foreach (var col in colecciones)
            {
                if (col == null)
                    continue;
                foreach (var item in col)
                    Agregar(item);
            }
        }

        // Luego se complementa con las familias estándar internas de la app
        foreach (var estandar in FamiliasEstandar)
            Agregar(estandar);

        resultado.Sort(StringComparer.CurrentCultureIgnoreCase);
        return resultado;
    }
}
