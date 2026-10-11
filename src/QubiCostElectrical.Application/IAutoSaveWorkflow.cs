#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IAutoSaveWorkflow
{
    Task<AutoSaveResult> SaveSnapshotAsync(BudgetProjectSession session, CancellationToken cancellationToken = default(CancellationToken));

    Task<RecoveryCandidate?> FindRecoveryCandidateAsync(string projectPath, CancellationToken cancellationToken = default(CancellationToken));

    Task DeleteSnapshotAsync(string projectPath, CancellationToken cancellationToken = default(CancellationToken));
}
