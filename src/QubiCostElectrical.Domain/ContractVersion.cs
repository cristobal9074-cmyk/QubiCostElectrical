#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public sealed class ContractVersion
{
	public required ContractVersionId Id { get; init; }

	public required ContractId ContractId { get; init; }

	public int VersionNumber { get; set; }

	public DateTime CreatedUtc { get; init; }

	public string CreatedBy { get; init; } = string.Empty;

	public string SnapshotJson { get; set; } = string.Empty;

	public string Hash { get; set; } = string.Empty;

	public string Comments { get; set; } = string.Empty;

	public bool IsApproved { get; set; }

	public static string ComputeHash(string snapshotJson)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();
	}
}
