using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Error o anomalía detectada durante el cálculo. No interrumpe el cálculo;
/// se acumula en CostBreakdown.Inconsistencias para que la UI decida si bloquear o advertir.
/// </summary>
public sealed class BudgetInconsistencia
{
    public string? ItemId { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>CantidadCero | PorcentajeInvalido | ValorNegativo</summary>
    public string Tipo { get; set; } = "";
    public string Detalle { get; set; } = "";
}

/// <summary>
/// Desglose auditable por origen. Permite responder de dónde salió cada número.
/// Los campos históricos <c>*Uf</c> almacenan montos en <b>CLP</b> (migración de moneda del motor);
/// usar los alias <c>*Clp</c> en código nuevo.
/// </summary>
public sealed class CostOriginBreakdown
{
    public string Origen { get; set; } = BudgetCostOrigin.Cubicacion;
    public string ItemId { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Area { get; set; } = "General";
    public decimal MaterialesUf { get; set; }
    public decimal ManoObraUf { get; set; }
    public decimal EquiposUf { get; set; }
    public decimal SubcontratosUf { get; set; }
    public decimal HerramientasUf { get; set; }
    public decimal TransporteUf { get; set; }
    public decimal MermasUf { get; set; }
    public decimal OrdenesCambioUf { get; set; }

    public decimal MaterialesClp { get => MaterialesUf; set => MaterialesUf = value; }
    public decimal ManoObraClp { get => ManoObraUf; set => ManoObraUf = value; }
    public decimal EquiposClp { get => EquiposUf; set => EquiposUf = value; }
    public decimal SubcontratosClp { get => SubcontratosUf; set => SubcontratosUf = value; }
    public decimal HerramientasClp { get => HerramientasUf; set => HerramientasUf = value; }
    public decimal TransporteClp { get => TransporteUf; set => TransporteUf = value; }
    public decimal MermasClp { get => MermasUf; set => MermasUf = value; }
    public decimal OrdenesCambioClp { get => OrdenesCambioUf; set => OrdenesCambioUf = value; }
    public decimal VentaClp { get; set; }

    // Hist.: permite auditar si esta línea usa un recargo por partida
    // y no la regla global del proyecto. -1 = usar global.
    public decimal MermaPctOverride { get; set; } = -1m;
    public decimal HerramientasPctOverride { get; set; } = -1m;
    /// <summary>
    /// Marca los orígenes sintéticos "Herramientas globales %" / "Mermas globales %" que
    /// BudgetEngine.AddGlobalPercentages recalcula siempre desde cero. El cálculo incremental
    /// los usa para excluirlos del arrastre de la corrida anterior sin depender del texto de
    /// Descripcion (que puede traducirse o renombrarse).
    /// </summary>
    public bool IsGlobalAutoCharge { get; set; }
    public bool HasRowLevelMarkup { get; set; }
    public decimal TotalUf => MaterialesUf + ManoObraUf + EquiposUf + SubcontratosUf + HerramientasUf + TransporteUf + MermasUf + OrdenesCambioUf;
    public decimal TotalClp => TotalUf;
}

public sealed class CostOriginSummary
{
    public string Origen { get; set; } = "";
    public decimal MaterialesUf { get; set; }
    public decimal ManoObraUf { get; set; }
    public decimal EquiposUf { get; set; }
    public decimal SubcontratosUf { get; set; }
    public decimal HerramientasUf { get; set; }
    public decimal TransporteUf { get; set; }
    public decimal MermasUf { get; set; }
    public decimal OrdenesCambioUf { get; set; }
    public decimal TotalUf { get; set; }
}

/// <summary>
/// Resultado único de costos. Este objeto es la fuente oficial para Dashboard, Cubicación,
/// APU, Sensibilidad, PDF y Excel. Los campos históricos <c>*Uf</c> almacenan montos en
/// <b>CLP</b>; preferir alias <c>*Clp</c> / <see cref="Directo"/> en código nuevo.
/// </summary>
public sealed class CostBreakdown
{
    public decimal MaterialesUf { get; set; }
    public decimal ManoObraUf { get; set; }
    public decimal EquiposUf { get; set; }
    public decimal SubcontratosUf { get; set; }
    public decimal HerramientasUf { get; set; }
    public decimal TransporteUf { get; set; }
    public decimal MermasUf { get; set; }
    public decimal OrdenesCambioUf { get; set; }
    public decimal CostoDirectoUf { get; set; }
    public decimal RiesgoUf { get; set; }
    public decimal BaseCostoUf { get; set; }
    public decimal GastosGeneralesUf { get; set; }
    /// <summary>VA solo sobre equipos (CLP pese al sufijo Uf histórico).</summary>
    public decimal ValorAgregadoEquiposUf { get; set; }
    /// <summary>GO incluido en costo de ejecución (política Excel).</summary>
    public decimal GastosOperacionalesUf { get; set; }
    /// <summary>Base Mat+MO+GO usada para GG/UT en política Excel.</summary>
    public decimal BaseGgUtUf { get; set; }
    /// <summary>Utilidad aditiva (Excel UT %). En legado puede coincidir con UtilidadUf.</summary>
    public decimal UtilidadAditivaUf { get; set; }
    /// <summary>Venta − costo proyecto (Excel «Diferencia por factores»).</summary>
    public decimal DiferenciaFactoresUf { get; set; }
    /// <summary>Costo ejecución + GG + UT + VA.</summary>
    public decimal CostoProyectoUf { get; set; }
    public decimal SubtotalUf { get; set; }
    public decimal UtilidadUf { get; set; }
    public decimal DescuentoUf { get; set; }
    public decimal TotalVentaNetoUf { get; set; }
    public decimal IvaUf { get; set; }
    public decimal TotalVentaConIvaUf { get; set; }
    public decimal TotalVentaUf { get; set; }
    public decimal UfM2 { get; set; }
    public decimal MargenRealPct { get; set; }
    public BudgetApuMode ApuMode { get; set; } = BudgetApuMode.CubicacionMasApuComplementarios;
    public List<CostOriginBreakdown> Origenes { get; set; } = new();
    public List<CostOriginSummary> OrigenesResumen { get; set; } = new();

    /// <summary>
    /// Totales de materiales calculados durante la misma pasada del BudgetEngine.
    /// Permite que el Dashboard muestre materiales críticos sin volver a recorrer
    /// Cubicación ni APUs en el hilo UI.
    /// </summary>
    public List<MaterialCostTotal> MaterialesCriticos { get; set; } = new();

    /// <summary>
    /// Anomalías detectadas durante el cálculo. Si hay entradas aquí, la oferta debería
    /// pedir confirmación antes de exportar o emitir.
    /// </summary>
    public List<BudgetInconsistencia> Inconsistencias { get; set; } = new();

    // Alias CLP / pantallas: el motor calcula en pesos pese al sufijo histórico Uf.
    public decimal MaterialesClp { get => MaterialesUf; set => MaterialesUf = value; }
    public decimal ManoObraClp { get => ManoObraUf; set => ManoObraUf = value; }
    public decimal CostoDirectoClp { get => CostoDirectoUf; set => CostoDirectoUf = value; }
    public decimal Directo => CostoDirectoUf;
    public decimal GastosGenerales => GastosGeneralesUf;
    public decimal Subtotal => SubtotalUf;
    public decimal Utilidad => UtilidadUf;
    public decimal Venta => TotalVentaUf;
}

public sealed class AreaCostBreakdown
{
    public string Area { get; set; } = "General";
    public decimal MaterialesUf { get; set; }
    public decimal ManoObraUf { get; set; }
    public decimal EquiposUf { get; set; }
    public decimal SubcontratosUf { get; set; }
    public decimal HerramientasUf { get; set; }
    public decimal TransporteUf { get; set; }
    public decimal MermasUf { get; set; }
    public decimal OrdenesCambioUf { get; set; }
    public decimal TotalUf { get; set; }
}

public sealed class MaterialCostTotal
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    /// <summary>Total en CLP (nombre histórico TotalUf).</summary>
    public decimal TotalUf { get; set; }
    public decimal TotalClp { get => TotalUf; set => TotalUf = value; }
}

internal sealed class DirectCostResult
{
    public decimal MaterialesUf { get; set; }
    public decimal ManoObraUf { get; set; }
    public decimal EquiposUf { get; set; }
    public decimal SubcontratosUf { get; set; }
    public decimal HerramientasUf { get; set; }
    public decimal TransporteUf { get; set; }
    public decimal MermasUf { get; set; }
    public decimal OrdenesCambioUf { get; set; }
    public decimal SumVentaClp { get; set; }
    public bool HasRowLevelMarkups { get; set; }
    public List<CostOriginBreakdown> Origenes { get; } = new();
    public List<MaterialCostTotal> Materiales { get; } = new();
    public List<BudgetInconsistencia> Inconsistencias { get; } = new();
    public decimal TotalUf => MaterialesUf + ManoObraUf + EquiposUf + SubcontratosUf + HerramientasUf + TransporteUf + MermasUf + OrdenesCambioUf;

    public AreaCostBreakdown ToArea(string area = "General") => new()
    {
        Area = area,
        MaterialesUf = MaterialesUf,
        ManoObraUf = ManoObraUf,
        EquiposUf = EquiposUf,
        SubcontratosUf = SubcontratosUf,
        HerramientasUf = HerramientasUf,
        TransporteUf = TransporteUf,
        MermasUf = MermasUf,
        OrdenesCambioUf = OrdenesCambioUf,
        TotalUf = TotalUf
    };
}
