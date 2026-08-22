// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625
using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ApuVersionMerger
{
	public MergeResult Merge(ApuVersion left, ApuVersion right, ApuVersion baseVersion, string author, string changeReason, Dictionary<string, MergeConflictResolution>? autoResolutions = null)
	{
		if (autoResolutions == null)
		{
			autoResolutions = new Dictionary<string, MergeConflictResolution>();
		}
		List<MergeConflict> list = new List<MergeConflict>();
		List<string> list2 = new List<string>();
		List<ApuVersionComponent> list3 = MergeComponentLists(left.Components, right.Components, baseVersion.Components, list, list2, autoResolutions);
		List<ApuVersionResourceRef> list4 = MergeResourceRefs(left.ResourceRefs, right.ResourceRefs, baseVersion.ResourceRefs, list, list2, autoResolutions);
		List<ApuVersionEconomicConcept> list5 = MergeEconomicConcepts(left.EconomicConcepts, right.EconomicConcepts, baseVersion.EconomicConcepts, list, list2, autoResolutions);
		List<ApuVersionScenario> list6 = MergeScenarioLists(left.Scenarios, right.Scenarios, baseVersion.Scenarios, list, list2, autoResolutions);
		decimal calculatedDirectCost = ResolveNumericConflict("CalculatedDirectCost", left.CalculatedDirectCost, right.CalculatedDirectCost, baseVersion.CalculatedDirectCost, list, list2, autoResolutions);
		decimal calculatedFinalPrice = ResolveNumericConflict("CalculatedFinalPrice", left.CalculatedFinalPrice, right.CalculatedFinalPrice, baseVersion.CalculatedFinalPrice, list, list2, autoResolutions);
		ApuVersion mergedVersion = left with
		{
			VersionId = Guid.NewGuid().ToString("N"),
			VersionNumber = Math.Max(left.VersionNumber, right.VersionNumber) + 1,
			PreviousVersionId = left.VersionId + "," + right.VersionId,
			Status = ApuVersionStatus.Draft,
			Author = author,
			ChangeReason = changeReason,
			CreatedAt = DateTime.UtcNow,
			Components = list3.AsReadOnly(),
			ResourceRefs = list4.AsReadOnly(),
			EconomicConcepts = list5.AsReadOnly(),
			Scenarios = list6.AsReadOnly(),
			CalculatedDirectCost = calculatedDirectCost,
			CalculatedFinalPrice = calculatedFinalPrice,
			IntegrityHash = ""
		};
		return new MergeResult
		{
			MergedVersion = mergedVersion,
			Success = (list.Count == 0 || list.All((MergeConflict c) => c.IsResolved)),
			Conflicts = list.AsReadOnly(),
			AppliedChanges = list2.AsReadOnly()
		};
	}

	private static List<ApuVersionComponent> MergeComponentLists(IReadOnlyList<ApuVersionComponent> left, IReadOnlyList<ApuVersionComponent> right, IReadOnlyList<ApuVersionComponent> baseVersion, List<MergeConflict> conflicts, List<string> appliedChanges, Dictionary<string, MergeConflictResolution> resolutions)
	{
		Dictionary<string, ApuVersionComponent> dictionary = baseVersion.ToDictionary((ApuVersionComponent c) => c.ReferenceId, (ApuVersionComponent c) => c);
		Dictionary<string, ApuVersionComponent> dictionary2 = left.ToDictionary((ApuVersionComponent c) => c.ReferenceId, (ApuVersionComponent c) => c);
		Dictionary<string, ApuVersionComponent> dictionary3 = right.ToDictionary((ApuVersionComponent c) => c.ReferenceId, (ApuVersionComponent c) => c);
		HashSet<string> source = new HashSet<string>(dictionary2.Keys.Concat(dictionary3.Keys));
		List<ApuVersionComponent> list = new List<ApuVersionComponent>();
		foreach (string item in source.OrderBy((string k) => k))
		{
			bool flag = dictionary2.TryGetValue(item, out var value);
			bool flag2 = dictionary3.TryGetValue(item, out var value2);
			bool flag3 = dictionary.TryGetValue(item, out var value3);
			if (flag & flag2)
			{
				if (value == value2)
				{
					list.Add(value);
					continue;
				}
				if (flag3 && value3 == value)
				{
					list.Add(value2);
					appliedChanges.Add("Component '" + item + "': using right version (base == left)");
					continue;
				}
				if (flag3 && value3 == value2)
				{
					list.Add(value);
					appliedChanges.Add("Component '" + item + "': using left version (base == right)");
					continue;
				}
				string key = "Component:" + item;
				if (resolutions.TryGetValue(key, out var value4))
				{
					switch (value4)
					{
					case MergeConflictResolution.UseLeft:
						list.Add(value);
						appliedChanges.Add("Component '" + item + "': auto-resolved using left");
						continue;
					case MergeConflictResolution.UseRight:
						list.Add(value2);
						appliedChanges.Add("Component '" + item + "': auto-resolved using right");
						continue;
					}
					list.Add(value);
					conflicts.Add(CreateConflict(item, "Yield", $"{value.Yield}", $"{value2.Yield}", $"{value3?.Yield}"));
				}
				else
				{
					list.Add(value3 ?? value);
					conflicts.Add(CreateConflict(item, "Yield", $"{value.Yield}", $"{value2.Yield}", $"{value3?.Yield}"));
				}
			}
			else if (flag && !flag2)
			{
				if (flag3 && value3 != null)
				{
					conflicts.Add(CreateConflict(item, "Existence", "Present", "Removed", "Present"));
				}
				list.Add(value);
			}
			else if (!flag & flag2)
			{
				if (flag3)
				{
					conflicts.Add(CreateConflict(item, "Existence", "Removed", "Present", "Present"));
					list.Add(value2);
				}
				else
				{
					list.Add(value2);
					appliedChanges.Add("Component '" + item + "': added from right branch");
				}
			}
		}
		return list;
	}

	private static List<ApuVersionResourceRef> MergeResourceRefs(IReadOnlyList<ApuVersionResourceRef> left, IReadOnlyList<ApuVersionResourceRef> right, IReadOnlyList<ApuVersionResourceRef> baseVersion, List<MergeConflict> conflicts, List<string> appliedChanges, Dictionary<string, MergeConflictResolution> resolutions)
	{
		Dictionary<string, ApuVersionResourceRef> dictionary = baseVersion.ToDictionary((ApuVersionResourceRef r) => r.ResourceId, (ApuVersionResourceRef r) => r);
		Dictionary<string, ApuVersionResourceRef> dictionary2 = left.ToDictionary((ApuVersionResourceRef r) => r.ResourceId, (ApuVersionResourceRef r) => r);
		Dictionary<string, ApuVersionResourceRef> dictionary3 = right.ToDictionary((ApuVersionResourceRef r) => r.ResourceId, (ApuVersionResourceRef r) => r);
		HashSet<string> source = new HashSet<string>(dictionary2.Keys.Concat(dictionary3.Keys));
		List<ApuVersionResourceRef> list = new List<ApuVersionResourceRef>();
		foreach (string item2 in source.OrderBy((string k) => k))
		{
			ApuVersionResourceRef value5;
			if (dictionary2.TryGetValue(item2, out var value) && dictionary3.TryGetValue(item2, out var value2))
			{
				ApuVersionResourceRef value3;
				if (value == value2)
				{
					list.Add(value);
				}
				else if (dictionary.TryGetValue(item2, out value3))
				{
					if (value3 == value)
					{
						list.Add(value2);
						appliedChanges.Add("Resource '" + item2 + "': using right version");
						continue;
					}
					if (value3 == value2)
					{
						list.Add(value);
						appliedChanges.Add("Resource '" + item2 + "': using left version");
						continue;
					}
					string key = "Resource:" + item2;
					if (resolutions.TryGetValue(key, out var value4))
					{
						if (value4 == MergeConflictResolution.UseLeft)
						{
							list.Add(value);
							appliedChanges.Add("Resource '" + item2 + "': auto-resolved using left");
						}
						else
						{
							list.Add(value2);
							appliedChanges.Add("Resource '" + item2 + "': auto-resolved using right");
						}
					}
					else
					{
						list.Add(value);
						conflicts.Add(CreateConflict(item2, "Price", $"{value.Price}", $"{value2.Price}", $"{value3.Price}"));
					}
				}
				else
				{
					list.Add(value2);
					appliedChanges.Add("Resource '" + item2 + "': new, using right branch value");
				}
			}
			else if (dictionary2.TryGetValue(item2, out value5))
			{
				list.Add(value5);
			}
			else
			{
				ApuVersionResourceRef item = dictionary3[item2];
				list.Add(item);
				appliedChanges.Add("Resource '" + item2 + "': added from right branch");
			}
		}
		return list;
	}

	private static List<ApuVersionEconomicConcept> MergeEconomicConcepts(IReadOnlyList<ApuVersionEconomicConcept> left, IReadOnlyList<ApuVersionEconomicConcept> right, IReadOnlyList<ApuVersionEconomicConcept> baseVersion, List<MergeConflict> conflicts, List<string> appliedChanges, Dictionary<string, MergeConflictResolution> resolutions)
	{
		Dictionary<string, ApuVersionEconomicConcept> dictionary = baseVersion.ToDictionary((ApuVersionEconomicConcept c) => c.Code, (ApuVersionEconomicConcept c) => c);
		Dictionary<string, ApuVersionEconomicConcept> dictionary2 = left.ToDictionary((ApuVersionEconomicConcept c) => c.Code, (ApuVersionEconomicConcept c) => c);
		Dictionary<string, ApuVersionEconomicConcept> dictionary3 = right.ToDictionary((ApuVersionEconomicConcept c) => c.Code, (ApuVersionEconomicConcept c) => c);
		HashSet<string> source = new HashSet<string>(dictionary2.Keys.Concat(dictionary3.Keys));
		List<ApuVersionEconomicConcept> list = new List<ApuVersionEconomicConcept>();
		foreach (string item in source.OrderBy((string k) => k))
		{
			if (dictionary2.TryGetValue(item, out var value) && dictionary3.TryGetValue(item, out var value2))
			{
				ApuVersionEconomicConcept value3;
				if (value == value2)
				{
					list.Add(value);
				}
				else if (dictionary.TryGetValue(item, out value3))
				{
					if (value3 == value)
					{
						list.Add(value2);
						appliedChanges.Add("Economic concept '" + item + "': using right version");
						continue;
					}
					if (value3 == value2)
					{
						list.Add(value);
						appliedChanges.Add("Economic concept '" + item + "': using left version");
						continue;
					}
					string key = "Economic:" + item;
					if (resolutions.TryGetValue(key, out var value4))
					{
						if (value4 == MergeConflictResolution.UseLeft)
						{
							list.Add(value);
							appliedChanges.Add("Economic concept '" + item + "': auto-resolved using left");
						}
						else
						{
							list.Add(value2);
							appliedChanges.Add("Economic concept '" + item + "': auto-resolved using right");
						}
					}
					else
					{
						list.Add(value);
						conflicts.Add(CreateConflict(item, "Value", $"{value.Value}", $"{value2.Value}", $"{value3.Value}"));
					}
				}
				else
				{
					list.Add(value2);
					appliedChanges.Add("Economic concept '" + item + "': new, using right branch value");
				}
			}
			else if (dictionary2.ContainsKey(item))
			{
				list.Add(dictionary2[item]);
			}
			else
			{
				list.Add(dictionary3[item]);
				appliedChanges.Add("Economic concept '" + item + "': added from right branch");
			}
		}
		return list;
	}

	private static List<ApuVersionScenario> MergeScenarioLists(IReadOnlyList<ApuVersionScenario> left, IReadOnlyList<ApuVersionScenario> right, IReadOnlyList<ApuVersionScenario> baseVersion, List<MergeConflict> conflicts, List<string> appliedChanges, Dictionary<string, MergeConflictResolution> resolutions)
	{
		baseVersion.ToDictionary((ApuVersionScenario s) => s.ScenarioId, (ApuVersionScenario s) => s);
		Dictionary<string, ApuVersionScenario> dictionary = left.ToDictionary((ApuVersionScenario s) => s.ScenarioId, (ApuVersionScenario s) => s);
		Dictionary<string, ApuVersionScenario> dictionary2 = right.ToDictionary((ApuVersionScenario s) => s.ScenarioId, (ApuVersionScenario s) => s);
		HashSet<string> source = new HashSet<string>(dictionary.Keys.Concat(dictionary2.Keys));
		List<ApuVersionScenario> list = new List<ApuVersionScenario>();
		foreach (string item in source.OrderBy((string k) => k))
		{
			if (dictionary.ContainsKey(item) && !dictionary2.ContainsKey(item))
			{
				list.Add(dictionary[item]);
			}
			else if (!dictionary.ContainsKey(item) && dictionary2.ContainsKey(item))
			{
				list.Add(dictionary2[item]);
				appliedChanges.Add("Scenario '" + item + "': added from right branch");
			}
			else
			{
				list.Add(dictionary[item]);
			}
		}
		return list;
	}

	private static decimal ResolveNumericConflict(string property, decimal left, decimal right, decimal baseVal, List<MergeConflict> conflicts, List<string> appliedChanges, Dictionary<string, MergeConflictResolution> resolutions)
	{
		if (left == right)
		{
			return left;
		}
		if (left == baseVal)
		{
			appliedChanges.Add($"'{property}': using right ({right}) — left unchanged");
			return right;
		}
		if (right == baseVal)
		{
			appliedChanges.Add($"'{property}': using left ({left}) — right unchanged");
			return left;
		}
		string key = "Numeric:" + property;
		if (resolutions.TryGetValue(key, out var value))
		{
			switch (value)
			{
			case MergeConflictResolution.UseLeft:
				appliedChanges.Add($"'{property}': auto-resolved using left ({left})");
				return left;
			case MergeConflictResolution.UseRight:
				appliedChanges.Add($"'{property}': auto-resolved using right ({right})");
				return right;
			case MergeConflictResolution.UseNewer:
			{
				decimal num2 = Math.Max(left, right);
				appliedChanges.Add($"'{property}': auto-resolved using newer ({num2})");
				return num2;
			}
			case MergeConflictResolution.UseOlder:
			{
				decimal num = Math.Min(left, right);
				appliedChanges.Add($"'{property}': auto-resolved using older ({num})");
				return num;
			}
			}
		}
		conflicts.Add(CreateConflict("_numeric_", property, $"{left:F6}", $"{right:F6}", $"{baseVal:F6}"));
		return baseVal;
	}

	private static MergeConflict CreateConflict(string id, string prop, string leftVal, string rightVal, string baseVal)
	{
		return new MergeConflict
		{
			Identifier = id,
			Property = prop,
			LeftValue = leftVal,
			RightValue = rightVal,
			BaseValue = baseVal,
			Resolution = MergeConflictResolution.Manual
		};
	}
}
