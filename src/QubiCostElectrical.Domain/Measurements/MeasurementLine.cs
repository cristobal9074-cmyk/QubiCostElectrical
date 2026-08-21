namespace QubiCostElectrical;

/// <summary>
/// Línea de medición detallada al estilo Presto / Arquímedes (FIEBDC).
/// Descompone una cantidad en factores legibles y trazables:
/// Plano | Revisión | Piso | Sector | Fórmula | Cantidad | Nota.
/// El parcial resultante alimenta la cantidad de una partida o fila de cubicación.
/// </summary>
public sealed class MeasurementLineRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Partida / ItemId al que pertenece esta línea de medición.</summary>
    public string ItemId { get; set; } = "";

    /// <summary>Orden de presentación dentro de la partida.</summary>
    public int Orden { get; set; }

    /// <summary>Texto descriptivo o nota técnica de la medición.</summary>
    public string Comentario { get; set; } = "";

    /// <summary>+1 para mediciones que suman, -1 para deducciones (huecos, descuentos).</summary>
    public int Signo { get; set; } = 1;

    /// <summary>Número de unidades iguales (Nº uds). 0 = factor no usado.</summary>
    public decimal Unidades { get; set; }

    public decimal Longitud { get; set; }
    public decimal Anchura { get; set; }
    public decimal Altura { get; set; }

    /// <summary>
    /// Fórmula opcional que, si está presente, reemplaza el cálculo Nº×L×A×H.
    /// Permite expresiones como "largo_pasillo * 1.1" reutilizando QuantityFormulaService.
    /// </summary>
    public string FormulaParcial { get; set; } = "";

    /// <summary>Etiqueta de zona/área para agrupar y filtrar mediciones.</summary>
    public string Area { get; set; } = "General";

    /// <summary>Id de la CubicacionRow legacy (compatibilidad).</summary>
    public string CubicacionRowId { get; set; } = "";

    /// <summary>Id del recurso normalizado al que pertenece este desglose.</summary>
    public string RecursoId { get; set; } = "";

    /// <summary>
    /// Índice de zona (0-based) dentro de QtyExpressions de la CubicacionRow.
    /// -1 cuando no está asociado a una celda de zona específica.
    /// </summary>
    public int ZonaOffset { get; set; } = -1;

    /// <summary>Referencia al plano del que se tomó la medición (ej. "E-03 Rev.2").</summary>
    public string Plano { get; set; } = "";

    /// <summary>Revisión documental del plano o antecedente usado.</summary>
    public string RevisionPlano { get; set; } = "";

    /// <summary>Piso o nivel donde se tomó la medición.</summary>
    public string Piso { get; set; } = "";

    /// <summary>Sector físico de la obra asociado a la medición.</summary>
    public string Sector { get; set; } = "";

    /// <summary>Nota visible para auditoría/revisión de la cantidad.</summary>
    public string Nota { get; set; } = "";

    /// <summary>Estado de revisión: Pendiente | Revisado | Excluido.</summary>
    public string Estado { get; set; } = "Pendiente";

    /// <summary>Origen de la medición (ej. "Plano entregado", "Medición in situ", "Estimación").</summary>
    public string Origen { get; set; } = "";

    public string UsuarioModificacion { get; set; } = "";
    public DateTime? FechaModificacion { get; set; }
}
