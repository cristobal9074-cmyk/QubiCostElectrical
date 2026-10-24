using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical.Infrastructure;

public sealed class RemoteLicenseService : IRemoteLicenseService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _endpointUrl;

    public RemoteLicenseService(string? endpointUrl = null)
    {
        _endpointUrl = endpointUrl
            ?? Environment.GetEnvironmentVariable("QUBI_LICENSE_URL")
            ?? "https://qubicost-updates.cristobal9074.workers.dev/api/verify-license";
    }

    public async Task<RemoteLicenseVerificationResult> VerifyLicenseAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            // Nunca viaja la contraseña en claro: se envía el digest SHA-256 de "correo:contraseña".
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var passwordHash = LicenseCredentialDigest.Compute(normalizedEmail, password);

            var payload = new
            {
                email = normalizedEmail,
                password,
                passwordHash
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(_endpointUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new RemoteLicenseVerificationResult(
                    false,
                    null,
                    null,
                    $"Error del servidor de licencias (HTTP {(int)response.StatusCode}). Reintenta en unos instantes.",
                    "http_error");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            bool isValid = root.TryGetProperty("valid", out var validProp) && validProp.GetBoolean();
            string? displayName = root.TryGetProperty("displayName", out var dnProp) ? dnProp.GetString() : null;
            string? expiresAt = root.TryGetProperty("expiresAt", out var expProp) ? expProp.GetString() : null;
            string? message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
            string? reason = root.TryGetProperty("reason", out var rsnProp) ? rsnProp.GetString() : null;

            return new RemoteLicenseVerificationResult(isValid, displayName, expiresAt, message, reason);
        }
        catch (HttpRequestException)
        {
            return new RemoteLicenseVerificationResult(
                false,
                null,
                null,
                "No se pudo conectar con el servidor de licencias. Verifica tu conexión a internet para validar el acceso.",
                "network_error");
        }
        catch (TaskCanceledException)
        {
            return new RemoteLicenseVerificationResult(
                false,
                null,
                null,
                "Tiempo de espera agotado al conectar con el servidor de licencias.",
                "timeout");
        }
        catch (Exception ex)
        {
            return new RemoteLicenseVerificationResult(
                false,
                null,
                null,
                $"Error de validación: {ex.Message}",
                "unknown");
        }
    }
}
