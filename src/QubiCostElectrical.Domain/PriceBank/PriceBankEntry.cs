using System;

namespace QubiCostElectrical;

/// <summary>
/// Entrada del banco de precios central, reutilizable entre proyectos (equivalente al
/// "cuadro de precios" / banco de Presto y Arqu�medes). Es un material deduplicado por
/// c�digo con su mejor precio conocido y trazabilidad de origen.
/// </summary>
public sealed class PriceBankEntry
{
    /// <summary>C�digo normalizado (clave �nica en el banco).</summary>
    public string Codigo { get; set; } = "";

    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Categoria { get; set; } = "";

    // Fuente del precio: SIEMPRE en pesos chilenos (ver MaterialRow.MatClp para el mismo patrón
    // en la lista de precios del proyecto). MatUf/MoUf quedan como valor derivado, recalculado
    // con RecalcularUf cuando cambia el valor de la UF.
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    public decimal MatClpConFallback => MatClp > 0m ? MatClp : (MatUf > 1000m ? MatUf : 0m);
    public decimal MoClpConFallback => MoClp > 0m ? MoClp : (MoUf > 15m ? MoUf : 0m);
    public decimal MatUf { get; set; }
    public decimal MoUf { get; set; }

    public string MonedaPrecio { get; set; } = "CLP";
    public string Proveedor { get; set; } = "";
    public string Marca { get; set; } = "";

    /// <summary>Fecha del precio (no del registro). Define qu� precio es "m�s nuevo".</summary>
    public DateTime FechaPrecio { get; set; } = DateTime.Today;

    public string OrigenPrecio { get; set; } = "Banco de precios";

    /// <summary>Proyecto que aport� por �ltima vez este precio (para auditar la fuente).</summary>
    public string ProyectoOrigen { get; set; } = "";

    /// <summary>Marca de tiempo del �ltimo upsert en el banco (metadato de persistencia).</summary>
    public DateTime ActualizadoEn { get; set; } = AppClock.Now;

    public PriceBankEntry Clone() => (PriceBankEntry)MemberwiseClone();

    /// <summary>Recalcula MatUf/MoUf desde MatClp/MoClp con el valor de UF vigente.</summary>
    public void RecalcularUf(decimal valorUfClp)
    {
        MatUf = PriceConversion.ClpToUf(MatClp, valorUfClp);
        MoUf = PriceConversion.ClpToUf(MoClp, valorUfClp);
    }

    /// <summary>Vigente si el precio tiene menos de 6 meses; si no, hay que revisarlo.</summary>
    public bool RequiereRevisionPrecio =>
        FechaPrecio != default && (DateTime.Today - FechaPrecio.Date).Days >= PriceConversion.VigenteDiasUmbral;
}

/// <summary>
/// Describe un cambio de precio que el banco propone aplicar a un material de proyecto.
/// </summary>
public sealed record PriceBankPriceUpdate(
    string Codigo,
    string Descripcion,
    decimal MatUfAnterior,
    decimal MoUfAnterior,
    decimal MatUfNuevo,
    decimal MoUfNuevo,
    DateTime FechaPrecioAnterior,
    DateTime FechaPrecioNuevo,
    string ProveedorAnterior,
    string ProveedorNuevo,
    string MonedaAnterior,
    string MonedaNueva,
    decimal UfUsadaClpAnterior,
    decimal UfUsadaClpNuevo,
    // Precio fuente (CLP) del banco — lo que realmente se aplica al material; MatUfNuevo/MoUfNuevo
    // quedan solo para mostrar la comparación en la UI. Sin esto, aplicar un precio del banco
    // dejaba el MatUf del material correcto pero su MatClp desactualizado, y el próximo
    // RecalcularUf (p. ej. al cambiar el valor UF) revertía el precio aplicado en silencio.
    decimal MatClpNuevo,
    decimal MoClpNuevo,
    decimal MatClpAnterior,
    decimal MoClpAnterior);
