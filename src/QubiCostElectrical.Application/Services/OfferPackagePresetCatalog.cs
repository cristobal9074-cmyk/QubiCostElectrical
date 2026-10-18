namespace QubiCostElectrical;

/// <summary>Presets de documentos del paquete de oferta.</summary>
public static class OfferPackagePresetCatalog
{
    public const string Dossier = "Dossier";
    public const string Mandante = "Mandante";
    public const string Interno = "Interno";
    public const string Banco = "Banco";

    /// <summary>
    /// Ids de documento a marcar. Null = seleccionar todos los disponibles.
    /// </summary>
    public static IReadOnlyList<string>? ResolveDocumentIds(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return Array.Empty<string>();

        return preset.Trim() switch
        {
            Dossier => new[] { "OfferPdf", "OfferLetter" },
            Mandante => new[] { "OfferLetter", "ResumenExcel" },
            Interno => null,
            Banco => new[] { "OfferLetter", "ResumenExcel", "ControlExcel" },
            _ => Array.Empty<string>()
        };
    }
}
