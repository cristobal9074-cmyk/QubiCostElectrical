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
