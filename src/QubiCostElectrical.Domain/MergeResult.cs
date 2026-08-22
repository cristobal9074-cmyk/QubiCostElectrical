using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record MergeResult
{
	public ApuVersion MergedVersion { get; init; } = null!;

	public bool Success { get; init; }

	public bool HasConflicts => Conflicts.Count > 0;

	public bool AllConflictsResolved
	{
		get
		{
			if (Conflicts.Count > 0)
			{
				return Conflicts.All((MergeConflict c) => c.IsResolved);
			}
			return false;
		}
	}

	public IReadOnlyList<MergeConflict> Conflicts { get; init; } = Array.Empty<MergeConflict>();

	public IReadOnlyList<string> AppliedChanges { get; init; } = Array.Empty<string>();
}
