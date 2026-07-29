using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ConsolidationConflict
{
    public string Key { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> ItemIds { get; init; } = Array.Empty<string>();
}

public sealed class ConsolidatedNeedLine
{
    public string ConsolidationKey { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = "CLP";
    public decimal TotalQuantity { get; init; }
    public DateTime EarliestRequiredDate { get; init; }
    public DateTime LatestRequiredDate { get; init; }
    public IReadOnlyList<string> SourceItemIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActivityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WbsNodeIds { get; init; } = Array.Empty<string>();
    public string TechnicalSpec { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class ConsolidationResult
{
    public IReadOnlyList<ConsolidatedNeedLine> Lines { get; init; } = Array.Empty<ConsolidatedNeedLine>();
    public IReadOnlyList<ConsolidationConflict> Conflicts { get; init; } = Array.Empty<ConsolidationConflict>();
}

public static class ProcurementConsolidationEngine
{
    public static ConsolidationResult Consolidate(IEnumerable<PurchaseRequisitionItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items.Where(i => i.Status != RequisitionItemStatus.Cancelled).ToList();
        var conflicts = new List<ConsolidationConflict>();
        var lines = new List<ConsolidatedNeedLine>();

        foreach (var group in list.GroupBy(Key).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var specs = members.Select(m => Normalize(m.TechnicalSpec)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (specs.Count > 1)
            {
                conflicts.Add(new ConsolidationConflict
                {
                    Key = group.Key,
                    Message = "Especificaciones técnicas incompatibles; no se consolidan.",
                    ItemIds = members.Select(m => m.ItemId).ToList()
                });
                continue;
            }

            var warnings = new List<string>();
            var dates = members.Select(m => m.RequiredDate.Date).Distinct().OrderBy(d => d).ToList();
            if (dates.Count > 1)
                warnings.Add($"Fechas requeridas distintas ({dates.First():yyyy-MM-dd} … {dates.Last():yyyy-MM-dd}).");

            lines.Add(new ConsolidatedNeedLine
            {
                ConsolidationKey = group.Key,
                Code = members[0].Code,
                Description = members[0].Description,
                Unit = members[0].Unit,
                CurrencyCode = members[0].CurrencyCode,
                TotalQuantity = members.Sum(m => m.PendingQuantity),
                EarliestRequiredDate = dates.First(),
                LatestRequiredDate = dates.Last(),
                SourceItemIds = members.Select(m => m.ItemId).ToList(),
                ActivityIds = members.Select(m => m.ActivityId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Cast<string>().ToList(),
                WbsNodeIds = members.Select(m => m.WbsNodeId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Cast<string>().ToList(),
                TechnicalSpec = members[0].TechnicalSpec,
                Warnings = warnings
            });
        }

        return new ConsolidationResult { Lines = lines, Conflicts = conflicts };
    }

    private static string Key(PurchaseRequisitionItem i) =>
        string.Join("|",
            (i.ResourceId ?? i.Code).Trim().ToUpperInvariant(),
            i.Unit.Trim().ToUpperInvariant(),
            i.CurrencyCode.Trim().ToUpperInvariant());

    private static string Normalize(string? s) => (s ?? string.Empty).Trim();
}
