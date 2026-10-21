using System;

namespace QubiCostElectrical;

/// <summary>
/// Reglas de tolerancia/merma habitual en compras y cubicación eléctrica según familia/categoría de material.
/// En obra eléctrica, materiales lineales como cables y ductos sufren despuntes y mermas mucho mayores
/// que artefactos o tableros que son equipos unitarios exactos.
/// </summary>
public static class MaterialToleranceRules
{
    public const decimal DefaultConductores = 10.0m;
    public const decimal DefaultCanalizacionPvc = 8.0m;
    public const decimal DefaultBandejas = 5.0m;
    public const decimal DefaultCanalizacionMetalica = 5.0m;
    public const decimal DefaultAccesorios = 5.0m;
    public const decimal DefaultArtefactos = 3.0m;
    public const decimal DefaultTableros = 0.0m;
    public const decimal DefaultGeneral = 5.0m;

    /// <summary>
    /// Resuelve la tolerancia porcentual recomendada para un material basándose en su categoría,
    /// unidad de medida o descripción.
    /// </summary>
    public static decimal ResolveTolerancePct(string? categoria, string? unidad = null, string? descripcion = null)
    {
        string catNorm = (categoria ?? "").Trim().ToUpperInvariant();
        string descNorm = (descripcion ?? "").Trim().ToUpperInvariant();
        string uNorm = (unidad ?? "").Trim().ToUpperInvariant();

        // 1. Cables / Conductores (10%)
        if (catNorm.Contains("CONDUCTOR") || catNorm.Contains("CABLE") || catNorm.Contains("ALAMBRE")
            || descNorm.Contains("CONDUCTOR") || descNorm.Contains("CABLE") || descNorm.Contains("EVA ")
            || descNorm.Contains("THHN") || descNorm.Contains("XTU") || descNorm.Contains("CORDON")
            || descNorm.Contains("ALIMENTADOR") || descNorm.Contains("SUPERFLEX"))
        {
            return DefaultConductores;
        }

        // 2. Canalización PVC / Plástica (8%)
        if (catNorm.Contains("PVC") || descNorm.Contains("PVC") || descNorm.Contains("COPLOPLAS") || descNorm.Contains("CONDUIT NARANJA"))
        {
            return DefaultCanalizacionPvc;
        }

        // 3. Bandejas / Escalerillas / Canastillos (5%)
        if (catNorm.Contains("BANDEJA") || catNorm.Contains("ESCALERILLA")
            || descNorm.Contains("BANDEJA") || descNorm.Contains("ESCALERILLA") || descNorm.Contains("CANASTILLO"))
        {
            return DefaultBandejas;
        }

        // 4. Canalización metálica / EMT / Cañerías (5%)
        if (catNorm.Contains("CANALIZ") || catNorm.Contains("TUBER") || catNorm.Contains("EMT") || catNorm.Contains("CAG")
            || descNorm.Contains("EMT") || descNorm.Contains("TUBERIA") || descNorm.Contains("CANALIZACION"))
        {
            return DefaultCanalizacionMetalica;
        }

        // 5. Tableros / Protecciones / Equipos / Transformadores / Celdas (0%)
        if (catNorm.Contains("TABLERO") || catNorm.Contains("PROTECC") || catNorm.Contains("EQUIPO")
            || catNorm.Contains("TRANSFORMADOR") || catNorm.Contains("CELDA") || catNorm.Contains("UPS")
            || catNorm.Contains("GENERADOR") || catNorm.Contains("DISYUNTOR") || catNorm.Contains("DIFERENCIAL")
            || descNorm.Contains("TABLERO") || descNorm.Contains("GABINETE") || descNorm.Contains("DISYUNTOR")
            || descNorm.Contains("AUTOMATICO") || descNorm.Contains("DIFERENCIAL") || descNorm.Contains("INTERRUPTOR CAJA"))
        {
            return DefaultTableros;
        }

        // 6. Artefactos / Iluminación / Enchufes (3%)
        if (catNorm.Contains("ILUMIN") || catNorm.Contains("LUMINAR") || catNorm.Contains("ENCHUFE")
            || catNorm.Contains("ARTEFACTO") || descNorm.Contains("LUMINARIA") || descNorm.Contains("ENCHUFE")
            || descNorm.Contains("INTERRUPTOR 9/") || descNorm.Contains("PLACA 3"))
        {
            return DefaultArtefactos;
        }

        // 7. Accesorios / Cajas / Fijaciones (5%)
        if (catNorm.Contains("ACCESORIO") || catNorm.Contains("CAJA") || catNorm.Contains("FIJACION")
            || descNorm.Contains("CAJA CHUQUI") || descNorm.Contains("CAJA DERIV") || descNorm.Contains("CONECTOR"))
        {
            return DefaultAccesorios;
        }

        // Por unidad de medida como pista adicional: si es metros lineales (ML / M / MTS) suele ser canalización o cable
        if (uNorm is "ML" or "M" or "MTS" or "METRO" or "METROS")
        {
            return 7.0m;
        }

        return DefaultGeneral;
    }
}
