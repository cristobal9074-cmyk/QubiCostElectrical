// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625
using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record MergeResult
{
	public ApuVersion MergedVersion { get; init; }

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
