#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record CloseoutSnapshotId(string Value)
{
	public static CloseoutSnapshotId New()
	{
		return new CloseoutSnapshotId(Guid.NewGuid().ToString("N"));
	}

	public static CloseoutSnapshotId From(string value)
	{
		return new CloseoutSnapshotId(value);
	}
}
