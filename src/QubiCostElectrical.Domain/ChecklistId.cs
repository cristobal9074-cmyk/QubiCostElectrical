#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record ChecklistId(string Value)
{
	public static ChecklistId New()
	{
		return new ChecklistId(Guid.NewGuid().ToString("N"));
	}

	public static ChecklistId From(string value)
	{
		return new ChecklistId(value);
	}
}
