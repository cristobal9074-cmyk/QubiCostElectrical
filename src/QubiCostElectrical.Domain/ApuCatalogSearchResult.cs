using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ApuCatalogSearchResult
{
    public IReadOnlyList<ApuCatalogEntry> Entries { get; init; } = Array.Empty<ApuCatalogEntry>();

    public int TotalCount { get; init; }

    public int FilteredCount { get; init; }

    public bool HasMore => Offset + MaxResults < FilteredCount;

    public int Offset { get; init; }

    public int MaxResults { get; init; }
}
