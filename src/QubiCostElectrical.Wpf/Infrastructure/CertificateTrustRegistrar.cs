using System;
using System.Security.Cryptography.X509Certificates;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Registra automáticamente el certificado público de Qubi Cost Electrical
/// en el almacén de Editores de Confianza (TrustedPublisher) del usuario de Windows.
/// Esto permite que Windows reconozca las firmas digitales de la aplicación y sus
/// actualizaciones automáticas sin requerir permisos de Administrador ni diálogos UAC.
/// </summary>
internal static class CertificateTrustRegistrar
{
    private const string PublicCertBase64 =
        "MIIDUDCCAjigAwIBAgIQI0NfDIQvObhNglp6X08S5DANBgkqhkiG9w0BAQsFADBAMQswCQYDVQQGEwJDTDESMBAGA1UECgwJUXViaSBDb3N0MR0wGwYDVQQDDBRRdWJpIENvc3QgRWxlY3RyaWNhbDAeFw0yNjEwMjcxODI2NDhaFw0zMTEwMjcxODM2NDhaMEAxCzAJBgNVBAYTAkNMMRIwEAYDVQQKDAlRdWJpIENvc3QxHTAbBgNVBAMMFFF1YmkgQ29zdCBFbGVjdHJpY2FsMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzj4o7nEct45MBu0bE8lTy9LY9S3YSjlIv8hl9vrdK4P60zbQuS5tRKB98LLCtc5rrKg+5W2ZK1qUWMlH+SU04BQckEZzsYx/jw8hPiZwemWUN2BqI2C3JKczxaAKUrl6D6PY/YwU8rfYIEnQ3xVkV+hr5FVt2w4G6yQ1D38IdXdhTc7+BRYd8tIkXiKQHG7fF+WCvTRBGUfAKVHzwecSIoJLoxNeVHNoPcJ9+hP945zb+mLZ4YyqOi8EioYdR5HDMLgGtXVP6HaX7w4mwfRzA/4WGHEHrSrjpSXql5XWO0mkaALa4Yll0Mp/gTG8tddMqmcX8Q13VNoi4mQZ7vcCDQIDAQABo0YwRDAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwMwHQYDVR0OBBYEFB2AY6uuMJSdcNCtTuEEhoDV7zaMMA0GCSqGSIb3DQEBCwUAA4IBAQARYWIJCB0p+RxMXmOYUsgzogQVPle3BwMNwQt3pUhUB+BNpHK+ut1GvaNWTAPkVvcBdUPNGWZ8iDlteus69/XcdJKZBKaME3HAHc0pBdu9ixB/tENhV82DhrO1vcAPwvK4op8ahmWsjruGuwcsCNATKnlPtd4G2aWYvre5wRTwrkFQJGa29norhgrABUjBAT/hRdiCrs9QKFkXa0wfoukW1580JiKz3gsNwxmJGyMxe8iYd5sm72BGcH9ipe58aINNpo6iJgmgeqnLWQGlAwIDl52UiXPg04COS/xzwi8YmPxb2ys2zZy19Xk2eRqL5r6auJebtZsW14LQRW+rvn4z";

    public static void EnsureTrusted()
    {
        try
        {
            var rawBytes = Convert.FromBase64String(PublicCertBase64);
            using var cert = X509CertificateLoader.LoadCertificate(rawBytes);

            using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            // Verificar si el certificado ya se encuentra instalado
            var existing = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                cert.Thumbprint,
                validOnly: false);

            if (existing.Count == 0)
            {
                store.Add(cert);
            }

            store.Close();
        }
        catch
        {
            // Falla no crítica: si una directiva corporativa bloquea el almacén de usuario, no interrumpir la app.
        }
    }
}
