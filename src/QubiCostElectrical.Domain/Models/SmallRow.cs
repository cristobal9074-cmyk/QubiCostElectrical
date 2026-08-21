namespace QubiCostElectrical;

public sealed class CubicacionCellCommentRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public int VisualRow { get; set; }
    // Referencia estable a CubicacionRow.Id. VisualRow se mantiene solo por compatibilidad
    // con datos anteriores a la migración 022; el lookup real usa CubicacionRowId.
    public string CubicacionRowId { get; set; } = "";
    public string ColumnKey { get; set; } = "";
    public string Comment { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = AppClock.Now;
    public DateTime UpdatedAt { get; set; } = AppClock.Now;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string ResolvedBy { get; set; } = "";
}

public sealed class TechnicalValidationRuleRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre { get; set; } = "Regla t�cnica";
    public string Campo { get; set; } = "Descripcion";
    public string Contiene { get; set; } = "";
    public string UnidadEsperada { get; set; } = "";
    public string CodigoMaterialContiene { get; set; } = "";
    public string Severidad { get; set; } = "Advertencia";
    public bool Activa { get; set; } = true;
}

public sealed class RealCostRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string Concepto { get; set; } = "Costo real";
    public decimal CostoRealUf { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Origen { get; set; } = "Manual";
    public string Nota { get; set; } = "";
}

public sealed class PartidaWorkflowRow
{
    public string ItemId { get; set; } = "";
    public string Estado { get; set; } = EstadoPartida.Borrador;
    public string Responsable { get; set; } = "";
    public DateTime FechaCambio { get; set; } = AppClock.Now;
    public string Nota { get; set; } = "";
}

public sealed class PartidaChangeTimelineRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public DateTime Fecha { get; set; } = AppClock.Now;
    public string Usuario { get; set; } = "";
    public string Campo { get; set; } = "Costo";
    public string ValorAnterior { get; set; } = "";
    public string ValorNuevo { get; set; } = "";
    public decimal DeltaCostoUf { get; set; }
    public string Origen { get; set; } = "Presupuesto";
}

public sealed class ProjectAuditEntryRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime OccurredAt { get; set; } = AppClock.Now;
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class ProactiveAlertRuleRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre { get; set; } = "Alerta";
    public string Tipo { get; set; } = "CostoTotalUf";
    public decimal Umbral { get; set; } = 0m;
    public bool Activa { get; set; } = true;
    public string Severidad { get; set; } = "Advertencia";
    public string Mensaje { get; set; } = "";
}

public sealed class LaborDaySettingsRow
{
    public decimal HorasPorJornada { get; set; } = 8m;
    public decimal DiasPorSemana { get; set; } = 5m;
    public decimal FactorProductividad { get; set; } = 1m;
    public decimal ValorJornadaUf { get; set; }
    public string CuadrillaDefault { get; set; } = "1 maestro + 1 ayudante";
}
