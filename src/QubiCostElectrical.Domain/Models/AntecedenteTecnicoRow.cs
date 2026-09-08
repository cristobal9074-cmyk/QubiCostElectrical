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
/// Registro central de antecedentes técnicos del presupuesto (planos, especificaciones, addendas, etc.).
/// </summary>
public sealed class AntecedenteTecnicoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Tipo { get; set; } = AntecedenteTecnicoTipo.Plano;
    public string Codigo { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Disciplina { get; set; } = "Electricidad";
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
