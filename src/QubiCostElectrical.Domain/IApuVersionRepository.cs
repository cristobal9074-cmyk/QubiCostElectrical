using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IApuVersionRepository
{
	Task<ApuVersion?> GetByIdAsync(string versionId);

	Task<IReadOnlyList<ApuVersion>> GetByApuIdAsync(string apuId);

	Task<IReadOnlyList<ApuVersion>> GetAllAsync();

	Task SaveAsync(ApuVersion version);

	Task DeleteAsync(string versionId);

	Task<bool> ExistsAsync(string versionId);

	Task<int> GetNextVersionNumberAsync(string apuId);
}
