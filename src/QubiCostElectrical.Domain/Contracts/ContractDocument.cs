using System;

namespace QubiCostElectrical;

public sealed class ContractDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ContractId ContractId { get; init; } = null!;
    public string DocumentType { get; set; } = string.Empty; // "Bases", "Especificacion", "Plano", "Poliza", "Garantia", etc.
    public string Version { get; set; } = "1.0";
    public DateTime DocumentDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string FileReference { get; set; } = string.Empty; // External path / URL
    public string FileHash { get; set; } = string.Empty; // SHA-256 hash of the file if calculated
    public string Status { get; set; } = "Vigente"; // "Vigente", "Obsoleto", "Cancelado"
    public string Comments { get; set; } = string.Empty;
}
