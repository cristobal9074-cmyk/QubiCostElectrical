#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

/// <summary>
/// Digest de credenciales intercambiado con el servidor de licencias. La contraseña en claro
/// nunca viaja por la red ni se almacena en el servidor: solo este SHA-256 de "correo:contraseña".
/// El worker de Cloudflare calcula exactamente la misma fórmula para comparar.
/// </summary>
public static class LicenseCredentialDigest
{
    public static string Compute(string? normalizedEmail, string? password)
    {
        var material = $"{(normalizedEmail ?? string.Empty).Trim().ToLowerInvariant()}:{password ?? string.Empty}";
        var bytes = Encoding.UTF8.GetBytes(material);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
