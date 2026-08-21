namespace QubiCostElectrical;

public static class ApuNivelDificultad
{
    public const string Baja = "Baja";
    public const string Normal = "Normal";
    public const string Alta = "Alta";
    public const string Extrema = "Extrema";

    public static readonly string[] All = { Baja, Normal, Alta, Extrema };
}

public static class ApuUbicacionLabor
{
    public const string Interior = "Interior";
    public const string Altura = "Altura";
    public const string Subterraneo = "Subterráneo";
    public const string Facade = "Fachada";
    public const string EspacioConfinado = "Espacio confinado";

    public static readonly string[] All = { Interior, Altura, Subterraneo, Facade, EspacioConfinado };
}

/// <summary>
/// Factores profesionales de mano de obra embebidos en el APU (no pantalla aparte).
/// </summary>
public sealed class ApuCondicionesLaboralesRow
{
    public decimal FactorAlturaPct { get; set; }
    public bool TrabajoNocturno { get; set; }
    public decimal RecargoNocturnoPct { get; set; } = 25m;
    public bool EdificioEnOperacion { get; set; }
    public decimal RecargoOperacionPct { get; set; } = 20m;
    public string NivelDificultad { get; set; } = ApuNivelDificultad.Normal;
    public decimal FactorRepetitividadPct { get; set; }
    public decimal HorasExtraordinariasPct { get; set; }
    public decimal RecargoHoraExtraPct { get; set; } = 50m;
    public string Ubicacion { get; set; } = ApuUbicacionLabor.Interior;
    public decimal FactorUbicacionPct { get; set; }
    public string CondicionesEspeciales { get; set; } = "";
    public decimal FactorCondicionesEspecialesPct { get; set; }
    public decimal HorasPorJornada { get; set; } = 8m;
    public decimal DiasPorSemana { get; set; } = 5m;
    public decimal DiasEstimados { get; set; }
    public string CuadrillaReferencia { get; set; } = "";
    public decimal RendimientoEstimadoHH { get; set; }
    public decimal RendimientoRealHH { get; set; }
}
