#nullable enable
using System;

namespace QubiCostElectrical;

public sealed class ContractDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required ContractId ContractId { get; init; }

    public string DocumentType { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";

    public DateTime DocumentDate { get; set; }

    public string Author { get; set; } = string.Empty;

    public string FileReference { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public string Status { get; set; } = "Vigente";

    public string Comments { get; set; } = string.Empty;
}
