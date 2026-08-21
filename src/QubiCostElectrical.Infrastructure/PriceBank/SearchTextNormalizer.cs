using System.Globalization;
using System.Text;

namespace QubiCostElectrical;

/// <summary>
/// Normaliza texto para búsquedas insensibles a mayúsculas/acentos. SQLite's UPPER() nativo es
/// ASCII-only sin la extensión ICU (no incluida en Microsoft.Data.Sqlite por defecto): buscar
/// "tuberias" no encontraba "Tuberías" porque UPPER('Tuberías') deja la 'í' intacta en vez de
/// convertirla a 'Í'. Se filtra en memoria con esta normalización en vez de con SQL UPPER()/LIKE.
/// </summary>
internal static class SearchTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var upper = value.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(upper.Length);
        foreach (var ch in upper)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
