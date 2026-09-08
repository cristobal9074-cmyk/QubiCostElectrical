namespace QubiCostElectrical;

public sealed class CubicacionRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string Tipo { get; set; } = TipoRecurso.MAT;
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Categoria { get; set; } = "";
    public decimal Cantidad { get; set; }
    // Fuente del precio: SIEMPRE en pesos chilenos (mismo patrón que MaterialRow.MatClp/MoClp).
    // MatUf/MoUf quedan como valor derivado (RecalcularUf), consumido sin cambios por el resto
    // del motor (UfUnit/UfTotal, APU, presupuesto, resumen).
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    /// <summary>
    /// Precio material en CLP para cálculos. Prefiere MatClp. Solo si MatClp=0 y MatUf parece
    /// pesos contaminados en la columna UF (umbral alto) se usa MatUf como CLP. Nunca trata
    /// un UF real (~fracciones) como pesos.
    /// </summary>
    public decimal MatClpConFallback => MatClp > 0m ? MatClp : (MatUf > 1000m ? MatUf : 0m);
    /// <summary>
    /// Precio MO en CLP para cálculos. Prefiere MoClp. Solo si MoClp=0 y MoUf parece pesos
    /// contaminados en la columna UF (MoUf &gt; 15) se usa MoUf como CLP. Nunca trata un UF
    /// real (p. ej. 0,38) como pesos — eso era la causa del "otro precio" vs la lista.
    /// </summary>
    public decimal MoClpConFallback => MoClp > 0m ? MoClp : (MoUf > 15m ? MoUf : 0m);
    public decimal MatUf { get; set; }
    public decimal MoUf { get; set; }
    // Factor GG+UT (Gastos Generales + Utilidad) de esta línea. Fijo en 1 por ahora — pedido
    // explícito: todavía no se conecta a los % reales del proyecto (ProjectInfoRow.GastosGeneralesPct/
    // UtilidadPct), queda para una iteración futura.
    public decimal GgUtFactor { get; set; } = 1m;
    public string Area { get; set; } = "General";
    public string[] QtyExpressions { get; set; } = Array.Empty<string>();
    public string TotalExpression { get; set; } = "";
    public decimal MermaPctOverride { get; set; } = -1m;
    public decimal HerramientasPctOverride { get; set; } = -1m;
    public decimal RedondeoMultiplo { get; set; } = 0m;
    public string RedondeoModo { get; set; } = ModoRedondeo.Ninguno;
    // Nota interna del recurso — solo para revisión del equipo, nunca se exporta a la oferta
    // (a diferencia de ItemizadoRow.Observaciones, que sí viaja al Resumen/oferta).
    public string NotaInterna { get; set; } = "";
    public decimal UfUnit => MatUf + MoUf;
    public decimal UfTotal => Cantidad * UfUnit;

    // $ UNIT: precio unitario en pesos (MAT + MO). SUBTOTAL: $ UNIT × cantidad.
    public decimal PrecioUnitClp => MatClp + MoClp;
    /// <summary>
    /// Precio unitario CLP usando la misma fuente-con-fallback que el costo del motor
    /// (<see cref="MatClpConFallback"/> / <see cref="MoClpConFallback"/>). Evita que en proyectos
    /// legacy sin MatClp la venta quede en 0 mientras el costo sí tiene valor.
    /// </summary>
    public decimal PrecioUnitClpConFallback => MatClpConFallback + MoClpConFallback;
    public decimal SubtotalClp => Cantidad * PrecioUnitClp;
    /// <summary>Subtotal solo de mano de obra: MoClp unitario × cantidad.</summary>
    public decimal MoSubtotalClp => Cantidad * MoClp;

    /// <summary>Recalcula MatUf/MoUf desde MatClp/MoClp con el valor de UF vigente.</summary>
    public void RecalcularUf(decimal valorUfClp)
    {
        MatUf = PriceConversion.ClpToUf(MatClp, valorUfClp);
        MoUf = PriceConversion.ClpToUf(MoClp, valorUfClp);
    }

    /// <summary>UF UNIT = REDONDEAR($ UNIT × GG+UT / valor UF ; 3).</summary>
    public decimal CalcularUfUnit(decimal valorUfClp) =>
        valorUfClp > 0m ? Math.Round(PrecioUnitClp * GgUtFactor / valorUfClp, 3, MidpointRounding.AwayFromZero) : 0m;
}
