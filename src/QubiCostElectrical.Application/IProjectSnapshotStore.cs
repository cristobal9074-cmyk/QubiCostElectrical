#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IProjectSnapshotStore
{
	Task<SnapshotWriteResult> WriteAsync(string path, ProjectFileData data, CancellationToken cancellationToken = default(CancellationToken));

	Task<ProjectFileData> ReadAsync(string path, CancellationToken cancellationToken = default(CancellationToken));

	DateTime? GetLastWriteTimeUtc(string path);

	bool Exists(string path);

	void Delete(string path);
}
