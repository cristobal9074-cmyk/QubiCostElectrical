using System;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public sealed class ContractVersion
{
    public ContractVersionId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public int VersionNumber { get; set; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public bool IsApproved { get; set; }

    public static string ComputeHash(string snapshotJson)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
