using System;
using System.Collections.Generic;
using System.Linq;

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
        "Ducto barra",
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

        string normalizado = FamilyPresentationDefaults.Normalize(nombre);

        // Unificación de sinónimos y variantes frecuentes en presupuestos eléctricos
        return normalizado switch
        {
            "GENERADOR" or "GENERADORES" or "GRUPO ELECTROGENO" or "GRUPOS ELECTROGENOS" => "GRUPO ELECTROGENO",
            "TRANSFORMADOR" or "TRANSFORMADORES" => "TRANSFORMADORES",
            "DUCTO BARRA" or "DUCTO BARRAS" or "DUCTOBARRA" or "DUCTOBARRAS" => "DUCTO BARRA",
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
            _ => normalizado
        };
    }

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
