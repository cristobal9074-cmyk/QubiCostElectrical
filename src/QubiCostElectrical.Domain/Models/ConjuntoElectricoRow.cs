namespace QubiCostElectrical;

public static class ConjuntoTipo
{
    public const string PuntoEnchufe = "Punto de enchufe";
    public const string PuntoAlumbrado = "Punto de alumbrado";
    public const string Alimentador = "Alimentador";
    public const string Tablero = "Tablero";
    public const string Bandeja = "Bandeja";
    public const string Canalizacion = "Canalización";
    public const string PuestaTierra = "Puesta a tierra";
    public const string General = "General";

    public static readonly string[] All =
    {
        PuntoEnchufe, PuntoAlumbrado, Alimentador, Tablero, Bandeja, Canalizacion, PuestaTierra, General
    };
}

/// <summary>
/// Define la forma en que se calcula la cantidad de un recurso en una plantilla parametrica.
/// Factor: Cantidad = Base x Rendimiento x Factor.
/// Espaciamiento: Cantidad = Ceil(Base / Espaciamiento) (cada N metros, p. ej. soportes cada 1.5 m).
/// </summary>
public enum TipoCalculoParametrico
{
    Factor,
    Espaciamiento
}
public sealed class ConjuntoRecursoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ApuTipoRecurso Tipo { get; set; } = ApuTipoRecurso.MAT;
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Rendimiento { get; set; } = 1m;
    public decimal Factor { get; set; } = 1m;
    public decimal Minimo { get; set; }
    /// <summary>Tipo de calculo parametrico: Factor directo o Espaciamiento (cada N metros).</summary>
    public TipoCalculoParametrico TipoCalculo { get; set; } = TipoCalculoParametrico.Factor;
    /// <summary>Metros entre repeticiones cuando TipoCalculo = Espaciamiento (ej. 1.5 -> soporte cada 1.5 m).</summary>
    public decimal Espaciamiento { get; set; } = 1m;
    /// <summary>
    /// Id de la fila maestro de la que deriva esta (vacio = la fila es maestro / independiente).
    /// Un esclavo usa la cantidad calculada de su maestro como base en lugar de la cantidad de la partida.
    /// Solo puede apuntar a una fila maestro de la misma plantilla (sin cadenas ni ciclos).
    /// </summary>
    public string MaestroId { get; set; } = "";
    /// <summary>
    /// Fórmula libre de la fila (rol Operación): combina cantidades de otras filas de la
    /// misma plantilla por su código, ej. "PAZ * 2 + TEEG500". Vacío = fila normal
    /// (Factor / Espaciamiento / Esclavo). Múltiples operadores: +, -, *, /, ^ y paréntesis
    /// (QuantityFormulaService). Se copia a CubicacionRow.Operacion al expandir y se
    /// recalcula en vivo en la hoja de cubicación.
    /// </summary>
    public string Operacion { get; set; } = "";
    public decimal PrecioUf { get; set; }
    public decimal ValorHHUf { get; set; }
    public decimal MermaPct { get; set; }
}

/// <summary>
/// Assembly eléctrico reutilizable: recursos, MO, merma, herramientas y parámetros por unidad de partida.
/// </summary>
public sealed class ConjuntoElectricoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string TipoConjunto { get; set; } = ConjuntoTipo.General;
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = Unidades.UN;
    public string Keywords { get; set; } = "";
    public decimal HerramientasPct { get; set; } = 3m;
    public decimal MermaPct { get; set; }
    public decimal TransportePct { get; set; }
    public decimal ImprevistosAlzaPct { get; set; } = 2m;
    public Dictionary<string, decimal> Parametros { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ConjuntoRecursoRow> Recursos { get; set; } = new();
    public int Version { get; set; } = 1;
    public DateTime ActualizadoEn { get; set; } = AppClock.Now;
}
