#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record ChecklistItemId(string Value)
{
	public static ChecklistItemId New()
	{
		return new ChecklistItemId(Guid.NewGuid().ToString("N"));
	}

	public static ChecklistItemId From(string value)
	{
		return new ChecklistItemId(value);
	}
}
