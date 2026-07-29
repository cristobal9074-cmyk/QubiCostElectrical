using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public sealed record ApuVersionSnapshot
{
    public string SnapshotId { get; init; } = Guid.NewGuid().ToString("N");
    public string SourceVersionId { get; init; } = "";
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    public int VersionNumber { get; init; }
    public string Author { get; init; } = "";
    public string CapturedBy { get; init; } = "";
    public string ChangeReason { get; init; } = "";
    public string Reason { get; init; } = "";

    public decimal TotalDirectCost { get; init; }
    public decimal TotalFinalPrice { get; init; }
    public string Currency { get; init; } = "CLP";
    public int ComponentCount { get; init; }
    public int ResourceRefCount { get; init; }
    public int EconomicConceptCount { get; init; }
    public int ScenarioCount { get; init; }
    public string IntegrityHash { get; init; } = "";
    public bool IsTampered => !string.IsNullOrEmpty(IntegrityHash) && ComputeHash() != IntegrityHash;

    public string ComputeHash()
    {
        var canonical = BuildCanonicalString();
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    private string BuildCanonicalString()
    {
        var sb = new StringBuilder();
        sb.Append(SourceVersionId ?? "").Append('\x01')
          .Append(VersionNumber).Append('\x01')
          .Append(Author ?? "").Append('\x01')
          .Append(CapturedBy ?? "").Append('\x01')
          .Append(Reason ?? "").Append('\x01')
          .Append(TotalDirectCost.ToString("F10", CultureInfo.InvariantCulture)).Append('\x01')
          .Append(TotalFinalPrice.ToString("F10", CultureInfo.InvariantCulture)).Append('\x01')
          .Append(Currency ?? "").Append('\x01')
          .Append(ComponentCount).Append('\x01')
          .Append(ResourceRefCount).Append('\x01')
          .Append(EconomicConceptCount).Append('\x01')
          .Append(ScenarioCount).Append('\n');
        return sb.ToString();
    }
}

public sealed record ApuCatalogEntry
{
    public string ApuId { get; init; } = Guid.NewGuid().ToString("N");
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string Unit { get; init; } = "UN";
    public int VersionCount { get; init; }
    public int ActiveVersionNumber { get; init; }
    public string ActiveVersionId { get; init; } = "";
    public ApuVersionStatus ActiveVersionStatus { get; init; } = ApuVersionStatus.Draft;
    public DateTime LastModifiedAt { get; init; }
    public decimal LastPrice { get; init; }
    public string ApprovedVersionId { get; init; } = "";
    public int ApprovedVersionNumber { get; init; }
    public string Owner { get; init; } = "";
    public string Tags { get; init; } = "";
    public bool IsActive { get; init; } = true;
}

public enum ApuCatalogFilter
{
    All,
    ActiveOnly,
    OnlyWithApprovedVersion,
    OnlyWithoutApprovedVersion,
    ByCategory,
    ByOwner,
    RecentModified
}

public sealed record ApuCatalogSearchRequest
{
    public string Query { get; init; } = "";
    public ApuCatalogFilter Filter { get; init; } = ApuCatalogFilter.ActiveOnly;
    public string Category { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Tags { get; init; } = "";
    public int MaxResults { get; init; } = 100;
    public int Offset { get; init; }
}

public sealed record ApuCatalogSearchResult
{
    public IReadOnlyList<ApuCatalogEntry> Entries { get; init; } = Array.Empty<ApuCatalogEntry>();
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public bool HasMore => (Offset + MaxResults) < FilteredCount;
    public int Offset { get; init; }
    public int MaxResults { get; init; }
}

public sealed record ApuCatalogStats
{
    public int TotalEntries { get; init; }
    public int ActiveEntries { get; init; }
    public int WithApprovedVersion { get; init; }
    public int WithDraftVersion { get; init; }
    public int TotalVersions { get; init; }
    public int TotalSnapshots { get; init; }
}

public sealed record CreateVersionFromSnapshotRequest
{
    public string SourceSnapshotId { get; init; } = "";
    public string Author { get; init; } = "";
    public string ChangeReason { get; init; } = "";
    public bool RestoreResources { get; init; } = true;
    public bool RestoreEconomicConfig { get; init; } = true;
    public bool RestoreScenarios { get; init; } = true;
}
