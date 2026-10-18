using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical.Infrastructure;

public sealed class ProjectExchangePackage
{
    public string SchemaVersion { get; set; } = ProductVersionInfo.ProjectFormatLabel;
    public string ProductVersion { get; set; } = ProductVersionInfo.ProductVersion;
    public int ProjectFormatVersion { get; set; } = ProductVersionInfo.ProjectFormatVersion;
    public int DatabaseSchemaVersion { get; set; } = ProductVersionInfo.DatabaseSchemaVersion;
    public int CalculationEngineVersion { get; set; } = ProductVersionInfo.CalculationEngineVersion;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string ExportedBy { get; set; } = Environment.UserName;
    public string HashAlgorithm { get; set; } = "SHA-256";
    public string PayloadHash { get; set; } = "";
    public ProjectFileData Payload { get; set; } = new();
}

/// <summary>
/// Hist.: formato abierto de intercambio .peprox.
/// Es JSON estándar con versión de esquema y hash de integridad.
/// </summary>
public sealed class ProjectExchangePackageService
{
    public async Task ExportAsync(string path, ProjectFileData data, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        data ??= new ProjectFileData();
        string payloadJson = JsonSerializer.Serialize(data, options);
        var package = new ProjectExchangePackage
        {
            Payload = data,
            PayloadHash = ComputeHash(payloadJson)
        };

        string json = JsonSerializer.Serialize(package, options);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectFileData> ImportAsync(string path, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var package = JsonSerializer.Deserialize<ProjectExchangePackage>(json, options)
            ?? throw new InvalidDataException("El archivo .peprox no contiene un paquete válido.");

        string payloadJson = JsonSerializer.Serialize(package.Payload, options);
        string hash = ComputeHash(payloadJson);
        if (!string.IsNullOrWhiteSpace(package.PayloadHash)
            && !string.Equals(hash, package.PayloadHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El hash de integridad del paquete .peprox no coincide.");

        return package.Payload ?? new ProjectFileData();
    }

    private static string ComputeHash(string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
