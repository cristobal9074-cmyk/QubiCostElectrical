using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public sealed record ApuVersionSnapshot
{
	public string SnapshotId { get; init; } = Guid.NewGuid().ToString("N");

	public string SourceVersionId { get; init; } = "";

	public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

	public int VersionNumber { get; init; }

	public string Author { get; init; } = "";

	public string CapturedBy { get; init; } = "";

	public string ChangeReason { get; init; } = "";

	public string Reason { get; init; } = "";

	public decimal TotalDirectCost { get; init; }

	public decimal TotalFinalPrice { get; init; }

	public string Currency { get; init; } = "CLP";

	public int ComponentCount { get; init; }

	public int ResourceRefCount { get; init; }

	public int EconomicConceptCount { get; init; }

	public int ScenarioCount { get; init; }

	public string IntegrityHash { get; init; } = "";

	public bool IsTampered
	{
		get
		{
			if (!string.IsNullOrEmpty(IntegrityHash))
			{
				return ComputeHash() != IntegrityHash;
			}
			return false;
		}
	}

	public string ComputeHash()
	{
		string s = BuildCanonicalString();
		using SHA256 sHA = SHA256.Create();
		return Convert.ToHexString(sHA.ComputeHash(Encoding.UTF8.GetBytes(s)));
	}

	private string BuildCanonicalString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(SourceVersionId ?? "").Append('\u0001').Append(VersionNumber)
			.Append('\u0001')
			.Append(Author ?? "")
			.Append('\u0001')
			.Append(CapturedBy ?? "")
			.Append('\u0001')
			.Append(Reason ?? "")
			.Append('\u0001')
			.Append(TotalDirectCost.ToString("F10", CultureInfo.InvariantCulture))
			.Append('\u0001')
			.Append(TotalFinalPrice.ToString("F10", CultureInfo.InvariantCulture))
			.Append('\u0001')
			.Append(Currency ?? "")
			.Append('\u0001')
			.Append(ComponentCount)
			.Append('\u0001')
			.Append(ResourceRefCount)
			.Append('\u0001')
			.Append(EconomicConceptCount)
			.Append('\u0001')
			.Append(ScenarioCount)
			.Append('\n');
		return stringBuilder.ToString();
	}
}
