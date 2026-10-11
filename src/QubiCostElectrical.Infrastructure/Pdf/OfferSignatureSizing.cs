using System;

namespace QubiCostElectrical;

/// <summary>
/// Tamaño de la imagen de la firma en los PDF, convertido de milímetros (configurables
/// en OfferPdfSettings.SignatureWidthMm / SignatureHeightMm) a puntos de QuestPDF.
/// Valores fuera de rango o en 0 usan los predeterminados 46 x 16 mm.
/// </summary>
public static class OfferSignatureSizing
{
    public static float WidthPt(OfferPdfSettings? offer) => MmToPt(offer?.SignatureWidthMm ?? 0, 46, 15, 130);

    public static float HeightPt(OfferPdfSettings? offer) => MmToPt(offer?.SignatureHeightMm ?? 0, 16, 8, 60);

    private static float MmToPt(int value, int fallback, int min, int max)
    {
        int mm = value <= 0 ? fallback : Math.Clamp(value, min, max);
        return mm * 72f / 25.4f;
    }
}
