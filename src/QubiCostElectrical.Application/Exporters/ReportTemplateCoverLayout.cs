using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

public enum ReportCoverSectionKind
{
    Project,
    Client,
    Summary,
    Itemized,
    Conditions,
    Signature
}

public sealed record ReportTemplateCoverLayout(IReadOnlyList<ReportCoverSectionKind> Sections, bool ShowLogo)
{
    public static ReportTemplateCoverLayout Resolve(IEnumerable<string>? orderedFields)
    {
        var sections = new List<ReportCoverSectionKind>();
        bool showLogo = false;
        bool anyValue = false;

        foreach (string? raw in orderedFields ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            anyValue = true;
            string token = Normalize(raw);

            if (token == "logo")
            {
                showLogo = true;
                continue;
            }

            if (TryMap(token, out var section) && !sections.Contains(section))
                sections.Add(section);
        }

        if (sections.Count == 0)
        {
            sections.AddRange(DefaultSections);
            showLogo = true;
        }

        if (!anyValue)
            showLogo = true;

        return new ReportTemplateCoverLayout(sections, showLogo);
    }

    public static IReadOnlyList<string> DefaultOrderedFields { get; } = new[]
    {
        "Logo", "Proyecto", "Cliente", "Resumen UF", "Itemizado", "Condiciones", "Firma"
    };

    public static IReadOnlyList<ReportCoverSectionKind> DefaultSections { get; } = new[]
    {
        ReportCoverSectionKind.Project,
        ReportCoverSectionKind.Client,
        ReportCoverSectionKind.Summary,
        ReportCoverSectionKind.Itemized,
        ReportCoverSectionKind.Conditions,
        ReportCoverSectionKind.Signature
    };

    private static bool TryMap(string token, out ReportCoverSectionKind section)
    {
        switch (token)
        {
            case "proyecto":
                section = ReportCoverSectionKind.Project;
                return true;
            case "cliente":
                section = ReportCoverSectionKind.Client;
                return true;
            case "resumen uf":
            case "resumen":
            case "valor uf":
                section = ReportCoverSectionKind.Summary;
                return true;
            case "itemizado":
            case "partidas":
                section = ReportCoverSectionKind.Itemized;
                return true;
            case "condiciones":
            case "condiciones comerciales":
                section = ReportCoverSectionKind.Conditions;
                return true;
            case "firma":
                section = ReportCoverSectionKind.Signature;
                return true;
            default:
                section = default;
                return false;
        }
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString();
    }
}
