using System.Collections.Generic;
using System.Threading.Tasks;
using QubiCostElectrical;

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

	Task<MergeResult> MergeVersionsAsync(string leftVersionId, string rightVersionId, string baseVersionId, string author, string changeReason, Dictionary<string, MergeConflictResolution>? autoResolutions = null);
}
