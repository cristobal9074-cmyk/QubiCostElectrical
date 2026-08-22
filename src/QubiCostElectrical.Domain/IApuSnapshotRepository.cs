using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IApuSnapshotRepository
{
	Task<ApuVersionSnapshot?> GetByIdAsync(string snapshotId);

	Task<IReadOnlyList<ApuVersionSnapshot>> GetByVersionIdAsync(string versionId);

	Task SaveAsync(ApuVersionSnapshot snapshot);

	Task<bool> ExistsAsync(string snapshotId);

	Task<ApuVersionSnapshot?> GetLatestByApuIdAsync(string apuId);
}
