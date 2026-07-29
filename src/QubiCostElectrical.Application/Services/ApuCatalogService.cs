using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IApuCatalogService
{
    Task<ApuCatalogEntry?> GetCatalogEntryAsync(string apuId);
    Task<ApuCatalogSearchResult> SearchCatalogAsync(ApuCatalogSearchRequest request);
    Task<ApuCatalogStats> GetCatalogStatsAsync();
    Task<ApuVersion?> CreateDraftAsync(string apuId, string author, string changeReason, string? baseVersionId = null);
    Task<ApuVersionSnapshot> CreateSnapshotAsync(string versionId, string capturedBy, string reason);
    Task<ApuVersion?> RestoreFromSnapshotAsync(string snapshotId, string author, string changeReason);
    Task<ApuVersion?> ApproveVersionAsync(string versionId, string approver, string comment);
    Task<bool> VerifyIntegrityAsync(string versionId);
    Task<IReadOnlyList<ApuVersion>> GetVersionHistoryAsync(string apuId);
    Task PromoteToActiveAsync(string apuId, string versionId);
    Task MarkVersionStatusAsync(string versionId, ApuVersionStatus newStatus, string user, string comment);
    Task<ApuComparisonSummary> CompareVersionsAsync(string leftVersionId, string rightVersionId);
    Task<MergeResult> MergeVersionsAsync(
        string leftVersionId, string rightVersionId, string baseVersionId,
        string author, string changeReason,
        Dictionary<string, MergeConflictResolution>? autoResolutions = null);
}

public sealed class ApuCatalogService : IApuCatalogService
{
    private readonly IApuVersionRepository _versionRepo;
    private readonly IApuSnapshotRepository _snapshotRepo;
    private readonly IApuCatalogRepository _catalogRepo;
    private readonly ApuVersionComparator _comparator;
    private readonly ApuVersionMerger _merger;

    public ApuCatalogService(
        IApuVersionRepository versionRepo,
        IApuSnapshotRepository snapshotRepo,
        IApuCatalogRepository catalogRepo)
    {
        _versionRepo = versionRepo;
        _snapshotRepo = snapshotRepo;
        _catalogRepo = catalogRepo;
        _comparator = new ApuVersionComparator();
        _merger = new ApuVersionMerger();
    }

    public async Task<ApuCatalogEntry?> GetCatalogEntryAsync(string apuId)
    {
        return await _catalogRepo.GetByApuIdAsync(apuId);
    }

    public async Task<ApuCatalogSearchResult> SearchCatalogAsync(ApuCatalogSearchRequest request)
    {
        return await _catalogRepo.SearchAsync(request);
    }

    public async Task<ApuCatalogStats> GetCatalogStatsAsync()
    {
        return await _catalogRepo.GetStatsAsync();
    }

    public async Task<ApuVersion?> CreateDraftAsync(string apuId, string author, string changeReason, string? baseVersionId = null)
    {
        var nextNumber = await _versionRepo.GetNextVersionNumberAsync(apuId);
        var entry = await _catalogRepo.GetByApuIdAsync(apuId);

        ApuVersion baseVersion;
        if (baseVersionId != null)
        {
            baseVersion = await _versionRepo.GetByIdAsync(baseVersionId)
                ?? throw new InvalidOperationException($"Base version '{baseVersionId}' not found.");
        }
        else if (entry != null && !string.IsNullOrEmpty(entry.ActiveVersionId))
        {
            baseVersion = await _versionRepo.GetByIdAsync(entry.ActiveVersionId)
                ?? new ApuVersion { ApuId = apuId, Code = entry.Code, Name = entry.Name, Unit = entry.Unit, Category = entry.Category };
        }
        else
        {
            baseVersion = new ApuVersion { ApuId = apuId };
        }

        var draft = baseVersion with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = nextNumber,
            Status = ApuVersionStatus.Draft,
            Author = author,
            ChangeReason = changeReason,
            CreatedAt = DateTime.UtcNow,
            PreviousVersionId = baseVersion.VersionId,
            IntegrityHash = ""
        };

        await _versionRepo.SaveAsync(draft);

        if (entry == null)
        {
            entry = new ApuCatalogEntry
            {
                ApuId = apuId,
                Code = draft.Code,
                Name = draft.Name,
                Unit = draft.Unit,
                Category = draft.Category,
                VersionCount = 1,
                ActiveVersionNumber = draft.VersionNumber,
                ActiveVersionId = draft.VersionId,
                ActiveVersionStatus = ApuVersionStatus.Draft,
                LastModifiedAt = DateTime.UtcNow
            };
        }
        else
        {
            entry = entry with
            {
                VersionCount = entry.VersionCount + 1,
                ActiveVersionNumber = draft.VersionNumber,
                ActiveVersionId = draft.VersionId,
                ActiveVersionStatus = ApuVersionStatus.Draft,
                LastModifiedAt = DateTime.UtcNow
            };
        }

        await _catalogRepo.SaveAsync(entry);
        return draft;
    }

    public async Task<ApuVersionSnapshot> CreateSnapshotAsync(string versionId, string capturedBy, string reason)
    {
        var version = await _versionRepo.GetByIdAsync(versionId)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found.");

        var snapshot = new ApuVersionSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            SourceVersionId = versionId,
            VersionNumber = version.VersionNumber,
            Author = version.Author,
            CapturedBy = capturedBy,
            ChangeReason = version.ChangeReason,
            Reason = reason,
            TotalDirectCost = version.CalculatedDirectCost,
            TotalFinalPrice = version.CalculatedFinalPrice,
            Currency = version.CalculatedCurrency,
            ComponentCount = version.Components.Count,
            ResourceRefCount = version.ResourceRefs.Count,
            EconomicConceptCount = version.EconomicConcepts.Count,
            ScenarioCount = version.Scenarios.Count,
            CapturedAt = DateTime.UtcNow
        };

        snapshot = snapshot with { IntegrityHash = snapshot.ComputeHash() };
        await _snapshotRepo.SaveAsync(snapshot);
        return snapshot;
    }

    public async Task<ApuVersion?> RestoreFromSnapshotAsync(string snapshotId, string author, string changeReason)
    {
        var snapshot = await _snapshotRepo.GetByIdAsync(snapshotId)
            ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' not found.");

        if (snapshot.IsTampered)
        {
            throw new InvalidOperationException($"Snapshot '{snapshotId}' integrity check failed. Data may be tampered.");
        }

        var originalVersion = await _versionRepo.GetByIdAsync(snapshot.SourceVersionId);
        if (originalVersion == null)
        {
            throw new InvalidOperationException($"Original version '{snapshot.SourceVersionId}' not found.");
        }

        var nextNumber = await _versionRepo.GetNextVersionNumberAsync(originalVersion.ApuId);

        var restored = originalVersion with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = nextNumber,
            Status = ApuVersionStatus.Draft,
            Author = author,
            ChangeReason = changeReason,
            CreatedAt = DateTime.UtcNow,
            PreviousVersionId = snapshot.SourceVersionId,
            IntegrityHash = ""
        };

        await _versionRepo.SaveAsync(restored);
        return restored;
    }

    public async Task<ApuVersion?> ApproveVersionAsync(string versionId, string approver, string comment)
    {
        var version = await _versionRepo.GetByIdAsync(versionId)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found.");

        if (version.Status != ApuVersionStatus.Draft && version.Status != ApuVersionStatus.PendingReview && version.Status != ApuVersionStatus.InReview)
        {
            throw new InvalidOperationException($"Cannot approve version in status '{version.Status}'.");
        }

        var approved = version with
        {
            Status = ApuVersionStatus.Approved,
            ChangeReason = version.ChangeReason + (string.IsNullOrEmpty(comment) ? "" : $" | {comment}"),
            Notes = comment,
            IntegrityHash = new ApuVersionSnapshot { SourceVersionId = versionId, VersionNumber = version.VersionNumber, Author = version.Author, CapturedBy = approver, Reason = "Approved", TotalDirectCost = version.CalculatedDirectCost, TotalFinalPrice = version.CalculatedFinalPrice, Currency = version.CalculatedCurrency, ComponentCount = version.Components.Count, ResourceRefCount = version.ResourceRefs.Count, EconomicConceptCount = version.EconomicConcepts.Count, ScenarioCount = version.Scenarios.Count }.ComputeHash()
        };

        await _versionRepo.SaveAsync(approved);

        var entry = await _catalogRepo.GetByApuIdAsync(version.ApuId);
        if (entry != null)
        {
            if (string.IsNullOrEmpty(entry.ApprovedVersionId) || version.VersionNumber > entry.ApprovedVersionNumber)
            {
                var updatedEntry = entry with
                {
                    ApprovedVersionId = versionId,
                    ApprovedVersionNumber = version.VersionNumber,
                    ActiveVersionStatus = ApuVersionStatus.Approved,
                    LastPrice = version.CalculatedFinalPrice,
                    LastModifiedAt = DateTime.UtcNow
                };
                await _catalogRepo.SaveAsync(updatedEntry);
            }
        }

        var snapshot = await CreateSnapshotAsync(versionId, approver, $"Approved: {comment}");
        return approved;
    }

    public async Task<bool> VerifyIntegrityAsync(string versionId)
    {
        var version = await _versionRepo.GetByIdAsync(versionId);
        if (version == null) return false;
        if (version.Status != ApuVersionStatus.Approved) return true;
        if (string.IsNullOrEmpty(version.IntegrityHash)) return false;

        var snapshot = new ApuVersionSnapshot
        {
            SourceVersionId = versionId,
            VersionNumber = version.VersionNumber,
            Author = version.Author,
            Reason = "Approved",
            TotalDirectCost = version.CalculatedDirectCost,
            TotalFinalPrice = version.CalculatedFinalPrice,
            Currency = version.CalculatedCurrency,
            ComponentCount = version.Components.Count,
            ResourceRefCount = version.ResourceRefs.Count,
            EconomicConceptCount = version.EconomicConcepts.Count,
            ScenarioCount = version.Scenarios.Count
        };

        return string.Equals(version.IntegrityHash, snapshot.ComputeHash(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ApuVersion>> GetVersionHistoryAsync(string apuId)
    {
        return await _versionRepo.GetByApuIdAsync(apuId);
    }

    public async Task PromoteToActiveAsync(string apuId, string versionId)
    {
        var version = await _versionRepo.GetByIdAsync(versionId)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found.");
        var entry = await _catalogRepo.GetByApuIdAsync(apuId);

        if (entry != null)
        {
            var updated = entry with
            {
                ActiveVersionId = versionId,
                ActiveVersionNumber = version.VersionNumber,
                ActiveVersionStatus = version.Status,
                LastModifiedAt = DateTime.UtcNow
            };
            await _catalogRepo.SaveAsync(updated);
        }
    }

    public async Task MarkVersionStatusAsync(string versionId, ApuVersionStatus newStatus, string user, string comment)
    {
        var version = await _versionRepo.GetByIdAsync(versionId)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found.");

        var updated = version with
        {
            Status = newStatus,
            ChangeReason = comment,
            Notes = comment
        };

        await _versionRepo.SaveAsync(updated);
    }

    public async Task<ApuComparisonSummary> CompareVersionsAsync(string leftVersionId, string rightVersionId)
    {
        var left = await _versionRepo.GetByIdAsync(leftVersionId)
            ?? throw new InvalidOperationException($"Left version '{leftVersionId}' not found.");
        var right = await _versionRepo.GetByIdAsync(rightVersionId)
            ?? throw new InvalidOperationException($"Right version '{rightVersionId}' not found.");

        return _comparator.Compare(left, right);
    }

    public async Task<MergeResult> MergeVersionsAsync(
        string leftVersionId, string rightVersionId, string baseVersionId,
        string author, string changeReason,
        Dictionary<string, MergeConflictResolution>? autoResolutions = null)
    {
        var left = await _versionRepo.GetByIdAsync(leftVersionId)
            ?? throw new InvalidOperationException($"Left version '{leftVersionId}' not found.");
        var right = await _versionRepo.GetByIdAsync(rightVersionId)
            ?? throw new InvalidOperationException($"Right version '{rightVersionId}' not found.");
        var baseVersion = await _versionRepo.GetByIdAsync(baseVersionId)
            ?? throw new InvalidOperationException($"Base version '{baseVersionId}' not found.");

        var result = _merger.Merge(left, right, baseVersion, author, changeReason, autoResolutions);
        if (result.Success && result.AllConflictsResolved)
        {
            await _versionRepo.SaveAsync(result.MergedVersion);
        }
        return result;
    }
}
