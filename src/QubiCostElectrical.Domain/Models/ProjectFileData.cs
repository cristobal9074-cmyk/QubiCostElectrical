namespace QubiCostElectrical;

public sealed class BudgetVersionData
{
    public ProjectInfoRow Proyecto { get; set; } = new();
    public List<ItemizadoRow> Itemizado { get; set; } = new();
    public List<MaterialRow> Materiales { get; set; } = new();
    public List<CubicacionRow> Cubicacion { get; set; } = new();
    public List<TemplateRow> Plantillas { get; set; } = new();
    public List<ConjuntoElectricoRow> ConjuntosElectricos { get; set; } = new();
    public List<CubicacionRecursoRow> CubicacionRecursos { get; set; } = new();
    public List<CubicacionZonaCantidadRow> CubicacionZonaCantidades { get; set; } = new();
    public List<CubicacionValorizacionRow> CubicacionValorizaciones { get; set; } = new();
    public List<PartidaApuVinculoRow> PartidaApuVinculos { get; set; } = new();
    public Dictionary<string, string[]> QtyHeadersByItem { get; set; } = new();
    public List<ZonaProyecto> Zonas { get; set; } = new();
    public List<MeasurementLineRow> Mediciones { get; set; } = new();
    public List<ApuRow> Apus { get; set; } = new();
    public List<ChangeOrderRow> OrdenesCambio { get; set; } = new();
    public List<CuadroCargaSCRow> CubicacionSC { get; set; } = new();
    public CubicacionScConfig CubicacionScConfig { get; set; } = CubicacionScConfig.Default();
    public List<SolicitudItemRow> Solicitudes { get; set; } = new();
    public List<MoDirectaRow> MoDirecta { get; set; } = new();
    public List<MoIndirectaRow> MoIndirecta { get; set; } = new();
    public List<GastoOperacionalRow> GastosOperacionales { get; set; } = new();
    public List<MoProjectionTaskRow> MoProjectionTasks { get; set; } = new();
    public List<NegotiatedDiscountRow> NegotiatedDiscounts { get; set; } = new();
    public List<StaffingRoleRow> StaffingPlan { get; set; } = new();
    public EppBudgetData Epp { get; set; } = EppBudgetData.Default();
    public ClosingPolicySelection ClosingPolicy { get; set; } = ClosingPolicySelection.Default();

    public static BudgetVersionData FromProjectFileData(ProjectFileData source)
    {
        source ??= new ProjectFileData();
        ProjectSnapshotIntegrity.Ensure(source);
        return new BudgetVersionData
        {
            Proyecto = source.Proyecto,
            Itemizado = source.Itemizado,
            Materiales = source.Materiales,
            Cubicacion = source.Cubicacion,
            Plantillas = source.Plantillas,
            ConjuntosElectricos = source.ConjuntosElectricos,
            CubicacionRecursos = source.CubicacionRecursos,
            CubicacionZonaCantidades = source.CubicacionZonaCantidades,
            CubicacionValorizaciones = source.CubicacionValorizaciones,
            PartidaApuVinculos = source.PartidaApuVinculos,
            QtyHeadersByItem = source.QtyHeadersByItem,
            Zonas = source.Zonas,
            Mediciones = source.Mediciones,
            Apus = source.Apus,
            OrdenesCambio = source.OrdenesCambio,
            CubicacionSC = source.CubicacionSC,
            CubicacionScConfig = source.CubicacionScConfig,
            Solicitudes = source.Solicitudes,
            MoDirecta = source.MoDirecta,
            MoIndirecta = source.MoIndirecta,
            GastosOperacionales = source.GastosOperacionales,
            MoProjectionTasks = source.MoProjectionTasks,
            NegotiatedDiscounts = source.NegotiatedDiscounts,
            StaffingPlan = source.StaffingPlan,
            Epp = source.Epp,
            ClosingPolicy = source.ClosingPolicy
        };
    }
}

public sealed class BudgetVersionRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "V1";
    public string Nombre { get; set; } = "Versión preliminar";
    public string Estado { get; set; } = "Preliminar";
    public string Nota { get; set; } = "";
    public string CreadoPor { get; set; } = "";
    public DateTime FechaCreacion { get; set; } = AppClock.Now;
    public DateTime FechaModificacion { get; set; } = AppClock.Now;
    public decimal MontoVentaUf { get; set; }
    public BudgetVersionData Snapshot { get; set; } = new();
}

public sealed class ZonaProyecto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre { get; set; } = "";
    public int Orden { get; set; }
    public bool Activa { get; set; } = true;
}

/// <summary>Fila de datos dentro de un ítem de la Hoja de Cubicación (ver HojaCubicacionItem):
/// una descripción y una serie de valores por columna. IsLabelRow marca la fila fija de nombres
/// de columna (siempre la primera), texto libre que no participa de fórmulas ni de la suma.</summary>
public sealed class HojaCubicacionFila
{
    public string Descripcion { get; set; } = "";
    public List<string> Valores { get; set; } = new();
    public bool IsLabelRow { get; set; }
}

/// <summary>Un ítem (partida) a cubicar dentro de la Hoja de Cubicación — herramienta de apoyo
/// manual con formato Excel (fórmulas, 300 columnas) para sumar mediciones sueltas antes de
/// trasladarlas a la cubicación real. Persiste con el proyecto para dar trazabilidad a futuro.</summary>
public sealed class HojaCubicacionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Unidad { get; set; } = "ml";
    public int Orden { get; set; }
    public List<HojaCubicacionFila> Filas { get; set; } = new();
}

public sealed class ProjectFileData
{
    public ProjectInfoRow Proyecto { get; set; } = new();
    public List<ItemizadoRow> Itemizado { get; set; } = new();
    public List<MaterialRow> Materiales { get; set; } = new();
    public List<CubicacionRow> Cubicacion { get; set; } = new();
    public List<TemplateRow> Plantillas { get; set; } = new();
    public List<ConjuntoElectricoRow> ConjuntosElectricos { get; set; } = new();
    public List<CubicacionRecursoRow> CubicacionRecursos { get; set; } = new();
    public List<CubicacionZonaCantidadRow> CubicacionZonaCantidades { get; set; } = new();
    public List<CubicacionValorizacionRow> CubicacionValorizaciones { get; set; } = new();
    public List<PartidaApuVinculoRow> PartidaApuVinculos { get; set; } = new();
    public Dictionary<string, string[]> QtyHeadersByItem { get; set; } = new();
    public List<ZonaProyecto> Zonas { get; set; } = new();
    public List<MeasurementLineRow> Mediciones { get; set; } = new();
    public List<BudgetVersionRow> Versiones { get; set; } = new();
    public List<ApuRow> Apus { get; set; } = new();
    public List<ChangeOrderRow> OrdenesCambio { get; set; } = new();
    public List<CuadroCargaSCRow> CubicacionSC { get; set; } = new();
    public CubicacionScConfig CubicacionScConfig { get; set; } = CubicacionScConfig.Default();
    public List<HojaCubicacionItem> HojaCubicacion { get; set; } = new();
    public List<SolicitudItemRow> Solicitudes { get; set; } = new();
    public List<MoDirectaRow> MoDirecta { get; set; } = new();
    public List<MoIndirectaRow> MoIndirecta { get; set; } = new();
    public List<GastoOperacionalRow> GastosOperacionales { get; set; } = new();
    public List<MoProjectionTaskRow> MoProjectionTasks { get; set; } = new();
    public List<NegotiatedDiscountRow> NegotiatedDiscounts { get; set; } = new();
    public List<StaffingRoleRow> StaffingPlan { get; set; } = new();
    public EppBudgetData Epp { get; set; } = EppBudgetData.Default();
    public ClosingPolicySelection ClosingPolicy { get; set; } = ClosingPolicySelection.Default();
    public List<EstadoPagoMesRow> EstadoPagoMeses { get; set; } = new();
    public EstadoPagoConfig EstadoPagoConfig { get; set; } = EstadoPagoConfig.Default();
    public string ActiveVersionId { get; set; } = "";
}
