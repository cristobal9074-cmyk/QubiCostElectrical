#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public sealed class AutoSaveWorkflow : IAutoSaveWorkflow
{
    private readonly IProjectSnapshotStore store;

    public AutoSaveWorkflow(IProjectSnapshotStore store)
    {
        this.store = store ?? throw new ArgumentNullException("store");
    }

    public async Task<AutoSaveResult> SaveSnapshotAsync(BudgetProjectSession session, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (session == null)
        {
            throw new ArgumentNullException("session");
        }
        if (!session.IsDirty)
        {
            return new AutoSaveResult(Success: true, null, null, null);
        }
        string path = ProjectStoragePaths.GetAutoSavePathForProject(session.CurrentProjectPath, session.ProjectInfo.Nombre);
        ProjectFileData data = session.CreateSnapshot();
        SnapshotWriteResult snapshotWriteResult = await store.WriteAsync(path, data, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return snapshotWriteResult.Success ? new AutoSaveResult(Success: true, path, DateTime.UtcNow, null) : new AutoSaveResult(Success: false, path, null, snapshotWriteResult.ErrorMessage);
    }

    public async Task<RecoveryCandidate?> FindRecoveryCandidateAsync(string projectPath, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }
        string snapshotPath = ProjectStoragePaths.GetAutoSavePathForProject(projectPath, "Proyecto");
        if (!store.Exists(snapshotPath))
        {
            return null;
        }
        DateTime? snapshotTime = store.GetLastWriteTimeUtc(snapshotPath);
        DateTime? lastWriteTimeUtc = store.GetLastWriteTimeUtc(projectPath);
        if (!snapshotTime.HasValue || (lastWriteTimeUtc.HasValue && snapshotTime <= lastWriteTimeUtc))
        {
            return null;
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.ReadAsync(snapshotPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return new RecoveryCandidate(projectPath, snapshotPath, snapshotTime.Value, 0L, "", IsValid: true, null);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            return new RecoveryCandidate(projectPath, snapshotPath, snapshotTime.Value, 0L, "", IsValid: false, "El snapshot de recuperación no es válido.");
        }
    }

    public Task DeleteSnapshotAsync(string projectPath, CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        string autoSavePathForProject = ProjectStoragePaths.GetAutoSavePathForProject(projectPath, "Proyecto");
        if (store.Exists(autoSavePathForProject))
        {
            store.Delete(autoSavePathForProject);
        }
        return Task.CompletedTask;
    }
}
