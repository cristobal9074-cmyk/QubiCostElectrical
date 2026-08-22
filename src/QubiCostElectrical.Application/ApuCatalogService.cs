using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public sealed class ApuCatalogService : IApuCatalogService
{
	private readonly IApuVersionRepository _versionRepo;

	private readonly IApuSnapshotRepository _snapshotRepo;

	private readonly IApuCatalogRepository _catalogRepo;

	private readonly ApuVersionComparator _comparator;

	private readonly ApuVersionMerger _merger;

	public ApuCatalogService(IApuVersionRepository versionRepo, IApuSnapshotRepository snapshotRepo, IApuCatalogRepository catalogRepo)
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
		int nextNumber = await _versionRepo.GetNextVersionNumberAsync(apuId);
		ApuCatalogEntry? entry = await _catalogRepo.GetByApuIdAsync(apuId);
		ApuVersion apuVersion = ((baseVersionId != null) ? ((await _versionRepo.GetByIdAsync(baseVersionId)) ?? throw new InvalidOperationException("Base version '" + baseVersionId + "' not found.")) : ((entry == null || string.IsNullOrEmpty(entry.ActiveVersionId)) ? new ApuVersion
		{
			ApuId = apuId
		} : ((await _versionRepo.GetByIdAsync(entry.ActiveVersionId)) ?? new ApuVersion
		{
			ApuId = apuId,
			Code = entry.Code,
			Name = entry.Name,
			Unit = entry.Unit,
			Category = entry.Category
		})));
		ApuVersion draft = apuVersion with
		{
			VersionId = Guid.NewGuid().ToString("N"),
			VersionNumber = nextNumber,
			Status = ApuVersionStatus.Draft,
			Author = author,
			ChangeReason = changeReason,
			CreatedAt = DateTime.UtcNow,
			PreviousVersionId = apuVersion.VersionId,
			IntegrityHash = ""
		};
		await _versionRepo.SaveAsync(draft);
		entry = ((entry != null) ? entry with
		{
			VersionCount = entry.VersionCount + 1,
			ActiveVersionNumber = draft.VersionNumber,
			ActiveVersionId = draft.VersionId,
			ActiveVersionStatus = ApuVersionStatus.Draft,
			LastModifiedAt = DateTime.UtcNow
		} : new ApuCatalogEntry
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
		});
		await _catalogRepo.SaveAsync(entry);
		return draft;
	}

	public async Task<ApuVersionSnapshot> CreateSnapshotAsync(string versionId, string capturedBy, string reason)
	{
		ApuVersion apuVersion = (await _versionRepo.GetByIdAsync(versionId)) ?? throw new InvalidOperationException("Version '" + versionId + "' not found.");
		ApuVersionSnapshot snapshot = new ApuVersionSnapshot
		{
			SnapshotId = Guid.NewGuid().ToString("N"),
			SourceVersionId = versionId,
			VersionNumber = apuVersion.VersionNumber,
			Author = apuVersion.Author,
			CapturedBy = capturedBy,
			ChangeReason = apuVersion.ChangeReason,
			Reason = reason,
			TotalDirectCost = apuVersion.CalculatedDirectCost,
			TotalFinalPrice = apuVersion.CalculatedFinalPrice,
			Currency = apuVersion.CalculatedCurrency,
			ComponentCount = apuVersion.Components.Count,
			ResourceRefCount = apuVersion.ResourceRefs.Count,
			EconomicConceptCount = apuVersion.EconomicConcepts.Count,
			ScenarioCount = apuVersion.Scenarios.Count,
			CapturedAt = DateTime.UtcNow
		};
		snapshot = snapshot with
		{
			IntegrityHash = snapshot.ComputeHash()
		};
		await _snapshotRepo.SaveAsync(snapshot);
		return snapshot;
	}

	public async Task<ApuVersion?> RestoreFromSnapshotAsync(string snapshotId, string author, string changeReason)
	{
		ApuVersionSnapshot snapshot = (await _snapshotRepo.GetByIdAsync(snapshotId)) ?? throw new InvalidOperationException("Snapshot '" + snapshotId + "' not found.");
		if (snapshot.IsTampered)
		{
			throw new InvalidOperationException("Snapshot '" + snapshotId + "' integrity check failed. Data may be tampered.");
		}
		ApuVersion? originalVersion = await _versionRepo.GetByIdAsync(snapshot.SourceVersionId);
		if (originalVersion == null)
		{
			throw new InvalidOperationException("Original version '" + snapshot.SourceVersionId + "' not found.");
		}
		int versionNumber = await _versionRepo.GetNextVersionNumberAsync(originalVersion.ApuId);
		ApuVersion restored = originalVersion with
		{
			VersionId = Guid.NewGuid().ToString("N"),
			VersionNumber = versionNumber,
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
		ApuVersion version = (await _versionRepo.GetByIdAsync(versionId)) ?? throw new InvalidOperationException("Version '" + versionId + "' not found.");
		if (version.Status != ApuVersionStatus.Draft && version.Status != ApuVersionStatus.PendingReview && version.Status != ApuVersionStatus.InReview)
		{
			throw new InvalidOperationException($"Cannot approve version in status '{version.Status}'.");
		}
		ApuVersion approved = version with
		{
			Status = ApuVersionStatus.Approved,
			ChangeReason = version.ChangeReason + (string.IsNullOrEmpty(comment) ? "" : (" | " + comment)),
			Notes = comment,
			IntegrityHash = new ApuVersionSnapshot
			{
				SourceVersionId = versionId,
				VersionNumber = version.VersionNumber,
				Author = version.Author,
				CapturedBy = approver,
				Reason = "Approved",
				TotalDirectCost = version.CalculatedDirectCost,
				TotalFinalPrice = version.CalculatedFinalPrice,
				Currency = version.CalculatedCurrency,
				ComponentCount = version.Components.Count,
				ResourceRefCount = version.ResourceRefs.Count,
				EconomicConceptCount = version.EconomicConcepts.Count,
				ScenarioCount = version.Scenarios.Count
			}.ComputeHash()
		};
		await _versionRepo.SaveAsync(approved);
		ApuCatalogEntry? apuCatalogEntry = await _catalogRepo.GetByApuIdAsync(version.ApuId);
		if (apuCatalogEntry != null && (string.IsNullOrEmpty(apuCatalogEntry.ApprovedVersionId) || version.VersionNumber > apuCatalogEntry.ApprovedVersionNumber))
		{
			ApuCatalogEntry entry = apuCatalogEntry with
			{
				ApprovedVersionId = versionId,
				ApprovedVersionNumber = version.VersionNumber,
				ActiveVersionStatus = ApuVersionStatus.Approved,
				LastPrice = version.CalculatedFinalPrice,
				LastModifiedAt = DateTime.UtcNow
			};
			await _catalogRepo.SaveAsync(entry);
		}
		await CreateSnapshotAsync(versionId, approver, "Approved: " + comment);
		return approved;
	}

	public async Task<bool> VerifyIntegrityAsync(string versionId)
	{
		ApuVersion? apuVersion = await _versionRepo.GetByIdAsync(versionId);
		if (apuVersion == null)
		{
			return false;
		}
		if (apuVersion.Status != ApuVersionStatus.Approved)
		{
			return true;
		}
		if (string.IsNullOrEmpty(apuVersion.IntegrityHash))
		{
			return false;
		}
		ApuVersionSnapshot apuVersionSnapshot = new ApuVersionSnapshot
		{
			SourceVersionId = versionId,
			VersionNumber = apuVersion.VersionNumber,
			Author = apuVersion.Author,
			Reason = "Approved",
			TotalDirectCost = apuVersion.CalculatedDirectCost,
			TotalFinalPrice = apuVersion.CalculatedFinalPrice,
			Currency = apuVersion.CalculatedCurrency,
			ComponentCount = apuVersion.Components.Count,
			ResourceRefCount = apuVersion.ResourceRefs.Count,
			EconomicConceptCount = apuVersion.EconomicConcepts.Count,
			ScenarioCount = apuVersion.Scenarios.Count
		};
		return string.Equals(apuVersion.IntegrityHash, apuVersionSnapshot.ComputeHash(), StringComparison.OrdinalIgnoreCase);
	}

	public async Task<IReadOnlyList<ApuVersion>> GetVersionHistoryAsync(string apuId)
	{
		return await _versionRepo.GetByApuIdAsync(apuId);
	}

	public async Task PromoteToActiveAsync(string apuId, string versionId)
	{
		ApuVersion version = (await _versionRepo.GetByIdAsync(versionId)) ?? throw new InvalidOperationException("Version '" + versionId + "' not found.");
		ApuCatalogEntry? apuCatalogEntry = await _catalogRepo.GetByApuIdAsync(apuId);
		if (apuCatalogEntry != null)
		{
			ApuCatalogEntry entry = apuCatalogEntry with
			{
				ActiveVersionId = versionId,
				ActiveVersionNumber = version.VersionNumber,
				ActiveVersionStatus = version.Status,
				LastModifiedAt = DateTime.UtcNow
			};
			await _catalogRepo.SaveAsync(entry);
		}
	}

	public async Task MarkVersionStatusAsync(string versionId, ApuVersionStatus newStatus, string user, string comment)
	{
		ApuVersion obj = (await _versionRepo.GetByIdAsync(versionId)) ?? throw new InvalidOperationException("Version '" + versionId + "' not found.");
		ApuVersion version = obj with
		{
			Status = newStatus,
			ChangeReason = comment,
			Notes = comment
		};
		await _versionRepo.SaveAsync(version);
	}

	public async Task<ApuComparisonSummary> CompareVersionsAsync(string leftVersionId, string rightVersionId)
	{
		ApuVersion left = (await _versionRepo.GetByIdAsync(leftVersionId)) ?? throw new InvalidOperationException("Left version '" + leftVersionId + "' not found.");
		ApuVersion right = (await _versionRepo.GetByIdAsync(rightVersionId)) ?? throw new InvalidOperationException("Right version '" + rightVersionId + "' not found.");
		return _comparator.Compare(left, right);
	}

	public async Task<MergeResult> MergeVersionsAsync(string leftVersionId, string rightVersionId, string baseVersionId, string author, string changeReason, Dictionary<string, MergeConflictResolution>? autoResolutions = null)
	{
		ApuVersion left = (await _versionRepo.GetByIdAsync(leftVersionId)) ?? throw new InvalidOperationException("Left version '" + leftVersionId + "' not found.");
		ApuVersion right = (await _versionRepo.GetByIdAsync(rightVersionId)) ?? throw new InvalidOperationException("Right version '" + rightVersionId + "' not found.");
		ApuVersion baseVersion = (await _versionRepo.GetByIdAsync(baseVersionId)) ?? throw new InvalidOperationException("Base version '" + baseVersionId + "' not found.");
		MergeResult result = _merger.Merge(left, right, baseVersion, author, changeReason, autoResolutions);
		if (result.Success && result.AllConflictsResolved)
		{
			await _versionRepo.SaveAsync(result.MergedVersion);
		}
		return result;
	}
}
