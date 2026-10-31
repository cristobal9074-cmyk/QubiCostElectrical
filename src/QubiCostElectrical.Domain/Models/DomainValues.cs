namespace QubiCostElectrical;

public static class Moneda
{
    public const string UF = "UF";
    public const string CLP = "CLP";
    public const string USD = "USD";
    public const string EUR = "EUR";
}

public static class EstadoRecurso
{
    public const string OK = "OK";
}

public static class EstadoPartida
{
    public const string Borrador = "Borrador";
    public const string EnRevision = "En revisión";
    public const string Aprobado = "Aprobado";
    public const string Rechazado = "Rechazado";
    public const string Ofertado = "Ofertado";
    public const string Adjudicado = "Adjudicado";
}

public static class ModoRedondeo
{
    public const string Ninguno = "Ninguno";
    public const string Arriba = "Arriba";
    public const string Abajo = "Abajo";
    public const string Cercano = "Cercano";
}

public static class Unidades
{
    public const string GL = "GL";
    public const string UN = "UN";
    public const string ML = "ML";
    public const string M2 = "M2";
    public const string M3 = "M3";
    public const string HH = "HH";
    public const string KG = "KG";
}

public static class TipoRecurso
{
    public const string MAT = nameof(ApuTipoRecurso.MAT);
    public const string MO = nameof(ApuTipoRecurso.MO);
    public const string EQ = nameof(ApuTipoRecurso.EQ);
    public const string SC = nameof(ApuTipoRecurso.SC);
    public const string HH = nameof(ApuTipoRecurso.HH);
    public const string HERR = nameof(ApuTipoRecurso.HERR);
    public const string TR = nameof(ApuTipoRecurso.TR);
}
