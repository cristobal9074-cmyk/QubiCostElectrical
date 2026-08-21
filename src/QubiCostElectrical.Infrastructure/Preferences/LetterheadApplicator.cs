namespace QubiCostElectrical;

/// <summary>
/// Aplica la identidad global de empresa (UserPreferences) sobre los campos de oferta (OfferPdfSettings).
/// Regla de precedencia para todos los campos:
///   1. Valor específico del proyecto/oferta (ya definido en OfferPdfSettings).
///   2. Preferencia global del usuario (UserPreferences.Letterhead* / Professional*).
///   3. Valor predeterminado vacío del sistema.
/// El global solo rellena campos nulos, vacíos o no configurados.
/// No modifica el objeto de preferencias; opera sobre una copia del offer.
/// </summary>
public static class LetterheadApplicator
{
    public static void Apply(UserPreferences prefs, OfferPdfSettings offer)
    {
        if (prefs == null || offer == null) return;

        if (string.IsNullOrWhiteSpace(offer.CompanyName))
            offer.CompanyName = (prefs.LetterheadCompanyName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.CompanyRut))
            offer.CompanyRut = (prefs.LetterheadRut ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.CompanyAddress))
            offer.CompanyAddress = (prefs.LetterheadAddress ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.CompanyPhone))
            offer.CompanyPhone = (prefs.LetterheadPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.CompanyEmail))
            offer.CompanyEmail = (prefs.LetterheadEmail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.CompanyWebsite))
            offer.CompanyWebsite = (prefs.LetterheadWebsite ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.LogoPath))
            offer.LogoPath = (prefs.LetterheadLogoPath ?? "").Trim();

        if (string.IsNullOrWhiteSpace(offer.SignatureName))
            offer.SignatureName = (prefs.ProfessionalName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.SignatureRut))
            offer.SignatureRut = (prefs.ProfessionalRut ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.SignatureRole))
            offer.SignatureRole = (prefs.ProfessionalRole ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.SignatureRegistroSec))
            offer.SignatureRegistroSec = (prefs.ProfessionalRegistroSec ?? "").Trim();
        if (string.IsNullOrWhiteSpace(offer.SignatureImagePath))
            offer.SignatureImagePath = (prefs.ProfessionalSignatureImagePath ?? "").Trim();
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
