using System.Text;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

public sealed class ProjectTextValidationResult
{
    public string FieldName { get; init; } = "";
    public string CleanedText { get; set; } = "";
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public static class ProjectTextValidator
{
    private static readonly char[] WindowsInvalidFileNameChars =
    {
        '<', '>', ':', '"', '/', '\\', '|', '?', '*'
    };

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string NormalizeText(string? value, string fallback, int maxLength)
    {
        string text = RemoveControlCharacters(value ?? "");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(text))
            text = fallback;

        if (maxLength > 0 && text.Length > maxLength)
            text = text.Substring(0, maxLength).Trim();

        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    public static ProjectTextValidationResult ValidateText(
        string? value,
        string fieldName,
        string fallback,
        int maxLength,
        bool required,
        bool mustBeFileNameSafe)
    {
        var result = new ProjectTextValidationResult { FieldName = fieldName };
        string raw = value ?? "";
        string cleaned = RemoveControlCharacters(raw);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        if (required && string.IsNullOrWhiteSpace(cleaned))
            result.Errors.Add($"{fieldName}: es obligatorio.");

        if (raw.Any(char.IsControl))
            result.Errors.Add($"{fieldName}: no debe contener saltos de línea ni caracteres invisibles.");

        if (maxLength > 0 && cleaned.Length > maxLength)
            result.Errors.Add($"{fieldName}: máximo {maxLength} caracteres. Actualmente tiene {cleaned.Length}.");

        if (mustBeFileNameSafe && !string.IsNullOrWhiteSpace(cleaned))
        {
            var invalidChars = cleaned
                .Where(IsInvalidFileNameCharacter)
                .Distinct()
                .Select(ch => ch.ToString())
                .ToList();

            if (invalidChars.Count > 0)
                result.Errors.Add($"{fieldName}: no puede contener estos caracteres: {string.Join(" ", invalidChars)}");

            if (cleaned.EndsWith(".", StringComparison.Ordinal) || cleaned.EndsWith(" ", StringComparison.Ordinal))
                result.Errors.Add($"{fieldName}: no puede terminar con punto ni espacio.");

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(cleaned);
            if (WindowsReservedNames.Contains(nameWithoutExtension))
                result.Errors.Add($"{fieldName}: '{cleaned}' es un nombre reservado de Windows.");
        }

        result.CleanedText = string.IsNullOrWhiteSpace(cleaned)
            ? fallback
            : (maxLength > 0 && cleaned.Length > maxLength ? cleaned.Substring(0, maxLength).Trim() : cleaned);

        return result;
    }

    public static string MakeSafeFileName(string? value, string fallback = "Proyecto", int maxLength = 80)
    {
        string name = NormalizeText(value, fallback, maxLength <= 0 ? 80 : maxLength);
        var sb = new StringBuilder(name.Length);

        foreach (char ch in name)
        {
            if (IsInvalidFileNameCharacter(ch) || char.IsControl(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }

        name = Regex.Replace(sb.ToString(), @"_+", "_").Trim(' ', '.', '_');

        if (string.IsNullOrWhiteSpace(name))
            name = fallback;

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
        if (WindowsReservedNames.Contains(nameWithoutExtension))
            name = name + "_Proyecto";

        if (maxLength > 0 && name.Length > maxLength)
            name = name.Substring(0, maxLength).Trim(' ', '.', '_');

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static string RemoveControlCharacters(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
            sb.Append(char.IsControl(ch) ? ' ' : ch);
        return sb.ToString();
    }

    private static bool IsInvalidFileNameCharacter(char ch)
    {
        if (WindowsInvalidFileNameChars.Contains(ch)) return true;
        return Path.GetInvalidFileNameChars().Contains(ch);
    }
}
