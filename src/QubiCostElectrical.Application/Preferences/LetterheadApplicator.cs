namespace QubiCostElectrical;

/// <summary>
/// Aplica la identidad global de empresa (UserPreferences) sobre los campos de oferta (OfferPdfSettings).
/// Regla de precedencia para todos los campos de identidad:
///   1. Preferencia global del usuario (UserPreferences.Letterhead* / Professional*) — manda siempre que no esté vacía.
///   2. Valor específico del proyecto/oferta (OfferPdfSettings) — solo se usa si el global está vacío.
///   3. Valor predeterminado vacío del sistema.
/// La identidad vive en Preferencias y es la misma en todos los proyectos; el proyecto
/// solo aporta datos cuando el global no está configurado.
/// No modifica el objeto de preferencias; opera sobre el offer recibido.
/// </summary>
public static class LetterheadApplicator
{
    public static void Apply(UserPreferences prefs, OfferPdfSettings offer)
    {
        if (prefs == null || offer == null) return;

        ApplyGlobal(prefs.LetterheadCompanyName, v => offer.CompanyName = v);
        ApplyGlobal(prefs.LetterheadRut, v => offer.CompanyRut = v);
        ApplyGlobal(prefs.LetterheadAddress, v => offer.CompanyAddress = v);
        ApplyGlobal(prefs.LetterheadPhone, v => offer.CompanyPhone = v);
        ApplyGlobal(prefs.LetterheadEmail, v => offer.CompanyEmail = v);
        ApplyGlobal(prefs.LetterheadWebsite, v => offer.CompanyWebsite = v);
        ApplyGlobal(prefs.LetterheadLogoPath, v => offer.LogoPath = v);
        ApplyGlobal(prefs.LetterheadClientLogoPath, v => offer.ClientLogoPath = v);

        ApplyGlobal(prefs.ProfessionalName, v => offer.SignatureName = v);
        ApplyGlobal(prefs.ProfessionalRut, v => offer.SignatureRut = v);
        ApplyGlobal(prefs.ProfessionalRole, v => offer.SignatureRole = v);
        ApplyGlobal(prefs.ProfessionalRegistroSec, v => offer.SignatureRegistroSec = v);
        ApplyGlobal(prefs.ProfessionalSignatureImagePath, v => offer.SignatureImagePath = v);
        ApplyGlobal(prefs.ProfessionalPhone, v => offer.SignaturePhone = v);
        ApplyGlobal(prefs.ProfessionalEmail, v => offer.SignatureEmail = v);
        ApplyGlobal(prefs.ProfessionalSecClass, v => offer.SignatureSecClass = v);

        if (prefs.ProfessionalSignatureWidthMm > 0)
            offer.SignatureWidthMm = prefs.ProfessionalSignatureWidthMm;
        if (prefs.ProfessionalSignatureHeightMm > 0)
            offer.SignatureHeightMm = prefs.ProfessionalSignatureHeightMm;
    }

    /// <summary>Escribe el valor global solo si viene con contenido; si está vacío conserva el del proyecto.</summary>
    private static void ApplyGlobal(string? globalValue, System.Action<string> setter)
    {
        string value = (globalValue ?? "").Trim();
        if (value.Length > 0)
            setter(value);
    }

    public static string[] SupportedImageExtensions { get; } = { ".png", ".jpg", ".jpeg" };
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static string? ValidateImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (!System.Array.Exists(SupportedImageExtensions, e => e == ext))
            return $"Extensión no soportada: {ext}. Use PNG o JPG.";
        if (!System.IO.File.Exists(path))
            return "El archivo no existe.";
        try
        {
            var info = new System.IO.FileInfo(path);
            if (info.Length == 0) return "El archivo está vacío.";
            if (info.Length > MaxFileSizeBytes) return $"El archivo supera los 5 MB ({info.Length / 1024 / 1024:N0} MB).";
            using var _ = System.IO.File.OpenRead(path);
        }
        catch
        {
            return "El archivo no se puede leer.";
        }
        return null;
    }
}
