using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Configuración de tolerancias porcentuales por categoría/familia física de materiales.
/// Permite al usuario personalizar los porcentajes desde la interfaz superior y recalcular la lista.
/// </summary>
public sealed class CategoryToleranceConfig
{
    public decimal Conductores { get; set; } = MaterialToleranceRules.DefaultConductores; // 10%
    public decimal CanalizacionPvc { get; set; } = MaterialToleranceRules.DefaultCanalizacionPvc; // 8%
    public decimal Bandejas { get; set; } = MaterialToleranceRules.DefaultBandejas; // 5%
    public decimal CanalizacionMetalica { get; set; } = MaterialToleranceRules.DefaultCanalizacionMetalica; // 5%
    public decimal Accesorios { get; set; } = MaterialToleranceRules.DefaultAccesorios; // 5%
    public decimal Artefactos { get; set; } = MaterialToleranceRules.DefaultArtefactos; // 3%
    public decimal Tableros { get; set; } = MaterialToleranceRules.DefaultTableros; // 0%
    public decimal General { get; set; } = MaterialToleranceRules.DefaultGeneral; // 5%

    public CategoryToleranceConfig Clone() => new()
    {
        Conductores = this.Conductores,
        CanalizacionPvc = this.CanalizacionPvc,
        Bandejas = this.Bandejas,
        CanalizacionMetalica = this.CanalizacionMetalica,
        Accesorios = this.Accesorios,
        Artefactos = this.Artefactos,
        Tableros = this.Tableros,
        General = this.General
    };

    public void ResetToDefaults()
    {
        Conductores = MaterialToleranceRules.DefaultConductores;
        CanalizacionPvc = MaterialToleranceRules.DefaultCanalizacionPvc;
        Bandejas = MaterialToleranceRules.DefaultBandejas;
        CanalizacionMetalica = MaterialToleranceRules.DefaultCanalizacionMetalica;
        Accesorios = MaterialToleranceRules.DefaultAccesorios;
        Artefactos = MaterialToleranceRules.DefaultArtefactos;
        Tableros = MaterialToleranceRules.DefaultTableros;
        General = MaterialToleranceRules.DefaultGeneral;
    }

    public decimal Resolve(string? categoria, string? unidad = null, string? descripcion = null)
    {
        string catNorm = (categoria ?? "").Trim().ToUpperInvariant();
        string descNorm = (descripcion ?? "").Trim().ToUpperInvariant();
        string uNorm = (unidad ?? "").Trim().ToUpperInvariant();

        // 1. Cables / Conductores
        if (catNorm.Contains("CONDUCTOR") || catNorm.Contains("CABLE") || catNorm.Contains("ALAMBRE")
            || descNorm.Contains("CONDUCTOR") || descNorm.Contains("CABLE") || descNorm.Contains("EVA ")
            || descNorm.Contains("THHN") || descNorm.Contains("XTU") || descNorm.Contains("CORDON")
            || descNorm.Contains("ALIMENTADOR") || descNorm.Contains("SUPERFLEX"))
        {
            return Conductores;
        }

        // 2. Canalización PVC / Plástica
        if (catNorm.Contains("PVC") || descNorm.Contains("PVC") || descNorm.Contains("COPLOPLAS") || descNorm.Contains("CONDUIT NARANJA"))
        {
            return CanalizacionPvc;
        }

        // 3. Bandejas / Escalerillas / Canastillos
        if (catNorm.Contains("BANDEJA") || catNorm.Contains("ESCALERILLA")
            || descNorm.Contains("BANDEJA") || descNorm.Contains("ESCALERILLA") || descNorm.Contains("CANASTILLO"))
        {
            return Bandejas;
        }

        // 4. Canalización metálica / EMT / Cañerías
        if (catNorm.Contains("CANALIZ") || catNorm.Contains("TUBER") || catNorm.Contains("EMT") || catNorm.Contains("CAG")
            || descNorm.Contains("EMT") || descNorm.Contains("TUBERIA") || descNorm.Contains("CANALIZACION"))
        {
            return CanalizacionMetalica;
        }

        // 5. Tableros / Protecciones / Equipos / Transformadores / Celdas
        if (catNorm.Contains("TABLERO") || catNorm.Contains("PROTECC") || catNorm.Contains("EQUIPO")
            || catNorm.Contains("TRANSFORMADOR") || catNorm.Contains("CELDA") || catNorm.Contains("UPS")
            || catNorm.Contains("GENERADOR") || catNorm.Contains("DISYUNTOR") || catNorm.Contains("DIFERENCIAL")
            || descNorm.Contains("TABLERO") || descNorm.Contains("GABINETE") || descNorm.Contains("DISYUNTOR")
            || descNorm.Contains("AUTOMATICO") || descNorm.Contains("DIFERENCIAL") || descNorm.Contains("INTERRUPTOR CAJA"))
        {
            return Tableros;
        }

        // 6. Artefactos / Iluminación / Enchufes
        if (catNorm.Contains("ILUMIN") || catNorm.Contains("LUMINAR") || catNorm.Contains("ENCHUFE")
            || catNorm.Contains("ARTEFACTO") || descNorm.Contains("LUMINARIA") || descNorm.Contains("ENCHUFE")
            || descNorm.Contains("INTERRUPTOR 9/") || descNorm.Contains("PLACA 3"))
        {
            return Artefactos;
        }

        // 7. Accesorios / Cajas / Fijaciones
        if (catNorm.Contains("ACCESORIO") || catNorm.Contains("CAJA") || catNorm.Contains("FIJACION")
            || descNorm.Contains("CAJA CHUQUI") || descNorm.Contains("CAJA DERIV") || descNorm.Contains("CONECTOR"))
        {
            return Accesorios;
        }

        // Por unidad de medida
        if (uNorm is "ML" or "M" or "MTS" or "METRO" or "METROS")
        {
            return CanalizacionMetalica;
        }

        return General;
    }
}
