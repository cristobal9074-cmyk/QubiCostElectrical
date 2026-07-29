namespace QubiCostElectrical;

public static class ConjuntoTipo
{
    public const string PuntoEnchufe = "Punto de enchufe";
    public const string PuntoAlumbrado = "Punto de alumbrado";
    public const string Alimentador = "Alimentador";
    public const string Tablero = "Tablero";
    public const string Bandeja = "Bandeja";
    public const string Canalizacion = "Canalizaci�n";
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
/// Formula: Cantidad evaluada desde una expresion matematica que referencia al MAESTRO.
/// </summary>
public enum TipoCalculoParametrico
{
    Factor,
    Espaciamiento,
    Formula
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
    /// <summary>Tipo de calculo parametrico: Factor directo, Espaciamiento (cada N metros) o Formula.</summary>
    public TipoCalculoParametrico TipoCalculo { get; set; } = TipoCalculoParametrico.Factor;
    /// <summary>Metros entre repeticiones cuando TipoCalculo = Espaciamiento (ej. 1.5 -> soporte cada 1.5 m).</summary>
    public decimal Espaciamiento { get; set; } = 1m;
    /// <summary>Marca este recurso como el MAESTRO. Su cantidad es la base que siguen los esclavos.</summary>
    public bool EsMaestro { get; set; }
    /// <summary>Id del recurso MAESTRO al que este esclavo sigue.</summary>
    public string MaestroRecursoId { get; set; } = "";
    /// <summary>Operador de formula: "=", "+", "-", "*", "/". Genera "MAESTRO [op] [factor]".</summary>
    public string FormulaOperator { get; set; } = "=";
    /// <summary>Expresion matematica cuando TipoCalculo = Formula. Usa MAESTRO como variable (ej. "MAESTRO / 1.5").</summary>
    public string Formula { get; set; } = "";
    public decimal PrecioUf { get; set; }
    public decimal ValorHHUf { get; set; }
    public decimal MermaPct { get; set; }
}

/// <summary>
/// Assembly el�ctrico reutilizable: recursos, MO, merma, herramientas y par�metros por unidad de partida.
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
