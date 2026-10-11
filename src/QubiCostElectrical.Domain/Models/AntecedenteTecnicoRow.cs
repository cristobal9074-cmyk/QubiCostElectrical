namespace QubiCostElectrical;

public static class AntecedenteTecnicoTipo
{
    public const string Plano = "Plano";
    public const string Especificacion = "Especificación";
    public const string Addenda = "Addenda";
    public const string Consulta = "Consulta";
    public const string Aclaracion = "Aclaración";
    public const string Otro = "Otro";

    public static readonly string[] All =
    {
        Plano, Especificacion, Addenda, Consulta, Aclaracion, Otro
    };
}

/// <summary>
/// Especialidad de planos/antecedentes y prefijo de código (ELE-, CD-, TEL-, ET-, ARQ-).
/// </summary>
public static class AntecedenteDisciplina
{
    public const string Electricidad = "Electricidad";
    public const string CorrientesDebiles = "Corrientes débiles";
    public const string Telecomunicaciones = "Telecomunicaciones";
    public const string EspecificacionesTecnicas = "Especificaciones técnicas";
    public const string Arquitectura = "Arquitectura";

    public static readonly string[] All =
    [
        Electricidad,
        CorrientesDebiles,
        Telecomunicaciones,
        EspecificacionesTecnicas,
        Arquitectura
    ];

    public static string PrefixOf(string? disciplina) => disciplina switch
    {
        Electricidad => "ELE-",
        CorrientesDebiles => "CD-",
        Telecomunicaciones => "TEL-",
        EspecificacionesTecnicas => "ET-",
        Arquitectura => "ARQ-",
        _ => ""
    };

    /// <summary>
    /// Antepone el prefijo de especialidad al código si aún no lo tiene.
    /// </summary>
    public static string ApplyCodigoPrefix(string? codigo, string? disciplina)
    {
        var trimmed = (codigo ?? "").Trim();
        var prefix = PrefixOf(disciplina);
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(trimmed))
            return trimmed;
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return trimmed;
        foreach (var known in All)
        {
            var other = PrefixOf(known);
            if (!string.IsNullOrEmpty(other) &&
                trimmed.StartsWith(other, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[other.Length..].TrimStart('-', ' ', '_');
                break;
            }
        }
        return prefix + trimmed;
    }
}

/// <summary>
/// Registro central de antecedentes técnicos del presupuesto (planos, especificaciones, addendas, etc.).
/// </summary>
public sealed class AntecedenteTecnicoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Tipo { get; set; } = AntecedenteTecnicoTipo.Plano;
    public string Codigo { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Disciplina { get; set; } = AntecedenteDisciplina.Electricidad;
    public DateTime FechaDocumento { get; set; } = DateTime.Today;
    public bool EsVigente { get; set; } = true;
    public bool IncluidoEnAlcance { get; set; }
    public bool EsExclusion { get; set; }
    public bool EsSupuesto { get; set; }
    public bool EsPendienteMandante { get; set; }
    public string ResponsableRevision { get; set; } = "";
    public string Notas { get; set; } = "";
    public string RutaArchivo { get; set; } = "";
    public DateTime RegistradoEn { get; set; } = AppClock.Now;
}
