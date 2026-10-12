using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

/// <summary>
/// Código automático para materiales creados sin escribir uno a mano (alta rápida de la lista de
/// precios). Formato PREFIJO-NNN: el prefijo sale de la familia —o de la descripción si no hay
/// familia— y el correlativo es el siguiente libre entre los códigos que ya existen, para que dos
/// "Tableros" seguidos den TAB-001 y TAB-002 sin colisionar con el banco.
///
/// El prefijo usa las iniciales cuando la familia tiene varias palabras ("Canalización de
/// Bandejas" → CB) y las tres primeras letras cuando es una sola ("Tableros" → TAB), que es lo
/// que un estimador espera reconocer de un vistazo.
/// </summary>
public static class MaterialCodeGenerator
{
    private const int LargoMaximoPrefijo = 3;
    private const int AnchoCorrelativo = 3;
    private const string PrefijoDeEmergencia = "MAT";

    /// <summary>Palabras que no aportan al prefijo ("Caja de Paso" → CP, no CDP).</summary>
    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "del", "la", "el", "los", "las", "y", "e", "o", "u", "al", "un", "una",
        "para", "por", "con", "en", "a", "tipo", "tpo"
    };

    /// <summary>
    /// Siguiente código libre para la familia/descripción dadas. Los códigos existentes nunca se
    /// repiten: si ya hay TAB-007, devuelve TAB-008.
    /// </summary>
    public static string Siguiente(string? familia, string? descripcion, IEnumerable<string>? codigosExistentes)
    {
        string prefijo = Prefijo(familia, descripcion);
        int mayor = 0;

        foreach (string codigo in codigosExistentes ?? Enumerable.Empty<string>())
        {
            string normalizado = (codigo ?? "").Trim().ToUpperInvariant();
            if (normalizado.Length == 0 || !normalizado.StartsWith(prefijo, StringComparison.Ordinal))
                continue;

            string sufijo = normalizado[prefijo.Length..].TrimStart('-', '_', ' ', '.');
            if (int.TryParse(sufijo, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numero) && numero > mayor)
                mayor = numero;
        }

        return $"{prefijo}-{(mayor + 1).ToString("D" + AnchoCorrelativo, CultureInfo.InvariantCulture)}";
    }

    /// <summary>Prefijo de hasta 3 letras: iniciales si hay varias palabras, si no las 3 primeras letras.</summary>
    public static string Prefijo(string? familia, string? descripcion)
    {
        string prefijo = ConstruirPrefijo(familia);
        if (prefijo.Length == 0)
            prefijo = ConstruirPrefijo(descripcion);
        if (prefijo.Length == 0)
            prefijo = PrefijoDeEmergencia;

        return prefijo.Length > LargoMaximoPrefijo ? prefijo[..LargoMaximoPrefijo] : prefijo;
    }

    private static string ConstruirPrefijo(string? texto)
    {
        var palabras = PalabrasSignificativas(texto);
        if (palabras.Count == 0)
            return "";

        if (palabras.Count == 1)
            return palabras[0][..Math.Min(LargoMaximoPrefijo, palabras[0].Length)].ToUpperInvariant();

        var sb = new StringBuilder();
        foreach (string palabra in palabras)
        {
            if (sb.Length >= LargoMaximoPrefijo)
                break;
            sb.Append(char.ToUpperInvariant(palabra[0]));
        }

        return sb.ToString();
    }

    /// <summary>Palabras con letras iniciales, sin acentos y sin las vacías; descarta las que parten con número.</summary>
    private static List<string> PalabrasSignificativas(string? texto)
    {
        var resultado = new List<string>();
        if (string.IsNullOrWhiteSpace(texto))
            return resultado;

        foreach (string palabra in SepararPalabras(texto))
        {
            if (PalabrasVacias.Contains(palabra))
                continue;
            if (!char.IsLetter(palabra[0]))
                continue;
            resultado.Add(palabra);
        }

        return resultado;
    }

    /// <summary>Parte por todo lo que no sea letra o dígito, descartando acentos ("Eléctricos" → ELECTRICOS).</summary>
    private static IEnumerable<string> SepararPalabras(string texto)
    {
        var sb = new StringBuilder();
        foreach (char crudo in texto)
        {
            char c = QuitarAcento(crudo);
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    private static char QuitarAcento(char c)
    {
        string descompuesto = c.ToString().Normalize(NormalizationForm.FormD);
        if (descompuesto.Length <= 1)
            return c;

        foreach (char marca in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(marca) != UnicodeCategory.NonSpacingMark)
                return marca;
        }

        return c;
    }
}
