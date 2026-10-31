using System;
using System.IO;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

public sealed record RevisionSuggestion(
    string CurrentRevisionCode,
    string NextRevisionCode,
    string NextSuggestedFileName,
    string NextSuggestedFilePath,
    string NextSuggestedProjectCode);

public static class RevisionNamingHelper
{
    private static readonly Regex PelYearRegex = new(
        @"^(?<prefix>.*?\b[A-Za-z]+\d+)(?<letter>[A-Za-z])(?<suffix>\d{2,4}\b.*)$",
        RegexOptions.Compiled);

    private static readonly Regex RevLetterRegex = new(
        @"(?i)(?<prefix>.*?(?:^|[\s._\-\[(])(?:rev|revisi[oó]n)[._ -]*)(?<letter>[A-Za-z])(?<suffix>[\s._\-\]\)].*|$)",
        RegexOptions.Compiled);

    private static readonly Regex VersionNumRegex = new(
        @"(?i)(?<prefix>.*?(?:^|[\s._\-\[(])(?:v|versi[oó]n)[._ -]*)(?<num>\d+)(?<suffix>[\s._\-\]\)].*|$)",
        RegexOptions.Compiled);

    public static RevisionSuggestion SuggestNextRevision(string? currentPathOrName)
    {
        if (string.IsNullOrWhiteSpace(currentPathOrName))
        {
            return new RevisionSuggestion("Rev A", "Rev B", "Proyecto_Rev_B.qce", "Proyecto_Rev_B.qce", "Rev B");
        }

        string directory = Path.GetDirectoryName(currentPathOrName) ?? string.Empty;
        string fileName = Path.GetFileName(currentPathOrName);
        string ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".qce";

        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // 1. Patrón código de proyecto estándar (ej. PRJ001A2026: letras + num + letra_rev + año)
        var pelMatch = PelYearRegex.Match(nameWithoutExt);
        if (pelMatch.Success)
        {
            char currentLetter = pelMatch.Groups["letter"].Value[0];
            char nextLetter = IncrementLetter(currentLetter);
            string prefix = pelMatch.Groups["prefix"].Value;
            string suffix = pelMatch.Groups["suffix"].Value;

            string nextName = $"{prefix}{nextLetter}{suffix}";
            string nextFileName = $"{nextName}{ext}";
            string nextPath = string.IsNullOrWhiteSpace(directory) ? nextFileName : Path.Combine(directory, nextFileName);

            string currentRevCode = $"Rev {char.ToUpperInvariant(currentLetter)}";
            string nextRevCode = $"Rev {char.ToUpperInvariant(nextLetter)}";
            string nextProjCode = ExtractProjectCode(nextName);

            return new RevisionSuggestion(currentRevCode, nextRevCode, nextFileName, nextPath, nextProjCode);
        }

        // 2. Patrón explícito "Rev A" / "Revision A"
        var revMatch = RevLetterRegex.Match(nameWithoutExt);
        if (revMatch.Success)
        {
            char currentLetter = revMatch.Groups["letter"].Value[0];
            char nextLetter = IncrementLetter(currentLetter);
            string prefix = revMatch.Groups["prefix"].Value;
            string suffix = revMatch.Groups["suffix"].Value;

            string nextName = $"{prefix}{nextLetter}{suffix}";
            string nextFileName = $"{nextName}{ext}";
            string nextPath = string.IsNullOrWhiteSpace(directory) ? nextFileName : Path.Combine(directory, nextFileName);

            string currentRevCode = $"Rev {char.ToUpperInvariant(currentLetter)}";
            string nextRevCode = $"Rev {char.ToUpperInvariant(nextLetter)}";
            string nextProjCode = ExtractProjectCode(nextName);

            return new RevisionSuggestion(currentRevCode, nextRevCode, nextFileName, nextPath, nextProjCode);
        }

        // 3. Patrón numérico "V1" / "Version 1"
        var verMatch = VersionNumRegex.Match(nameWithoutExt);
        if (verMatch.Success && int.TryParse(verMatch.Groups["num"].Value, out int currentNum))
        {
            int nextNum = currentNum + 1;
            string prefix = verMatch.Groups["prefix"].Value;
            string suffix = verMatch.Groups["suffix"].Value;
            string numFormat = verMatch.Groups["num"].Value.Length > 1
                ? nextNum.ToString($"D{verMatch.Groups["num"].Value.Length}")
                : nextNum.ToString();

            string nextName = $"{prefix}{numFormat}{suffix}";
            string nextFileName = $"{nextName}{ext}";
            string nextPath = string.IsNullOrWhiteSpace(directory) ? nextFileName : Path.Combine(directory, nextFileName);

            string currentRevCode = $"V{currentNum}";
            string nextRevCode = $"V{nextNum}";
            string nextProjCode = ExtractProjectCode(nextName);

            return new RevisionSuggestion(currentRevCode, nextRevCode, nextFileName, nextPath, nextProjCode);
        }

        // 4. Fallback: agregar _Rev_B
        {
            string nextName = $"{nameWithoutExt}_Rev_B";
            string nextFileName = $"{nextName}{ext}";
            string nextPath = string.IsNullOrWhiteSpace(directory) ? nextFileName : Path.Combine(directory, nextFileName);
            return new RevisionSuggestion("Rev A", "Rev B", nextFileName, nextPath, "Rev B");
        }
    }

    private static char IncrementLetter(char c)
    {
        if (c >= 'A' && c < 'Z') return (char)(c + 1);
        if (c >= 'a' && c < 'z') return (char)(c + 1);
        if (c == 'Z') return 'A';
        if (c == 'z') return 'a';
        return 'B';
    }

    private static string ExtractProjectCode(string text)
    {
        var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : text;
    }
}
