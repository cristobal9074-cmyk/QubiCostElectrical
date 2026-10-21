namespace QubiCostElectrical;

/// <summary>Recurso cubicado: identidad del material/MO/EQ sin cantidad ni precio.</summary>
public sealed class CubicacionRecursoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string Tipo { get; set; } = TipoRecurso.MAT;
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Area { get; set; } = "General";
    public int Orden { get; set; }
}

/// <summary>Desglose secundario por zona: cantidad o expresión, sin precio.</summary>
public sealed class CubicacionZonaCantidadRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RecursoId { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string ZonaId { get; set; } = "";
    public int ZonaOffset { get; set; } = -1;
    public decimal Cantidad { get; set; }
    public string Expresion { get; set; } = "";
}

/// <summary>Valorización con snapshot de precio usado en el presupuesto.
/// Fuente: <see cref="MatClp"/>/<see cref="MoClp"/>. <see cref="MatUf"/>/<see cref="MoUf"/> son UF derivados.</summary>
public sealed class CubicacionValorizacionRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RecursoId { get; set; } = "";
    public string ItemId { get; set; } = "";
    /// <summary>Precio material en CLP (fuente).</summary>
    public decimal MatClp { get; set; }
    /// <summary>Precio MO en CLP (fuente).</summary>
    public decimal MoClp { get; set; }
    /// <summary>UF derivado desde MatClp (o UF legacy si aún no hay CLP).</summary>
    public decimal MatUf { get; set; }
    /// <summary>UF derivado desde MoClp (o UF legacy si aún no hay CLP).</summary>
    public decimal MoUf { get; set; }
    public string FuentePrecio { get; set; } = "Lista de precios";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    public decimal MermaPctOverride { get; set; } = -1m;
    public decimal HerramientasPctOverride { get; set; } = -1m;
    public decimal RedondeoMultiplo { get; set; }
    public string RedondeoModo { get; set; } = ModoRedondeo.Ninguno;
    public string TotalExpression { get; set; } = "";
    public PrecioCongeladoRow? PrecioSnapshot { get; set; }
}

/// <summary>Relación explícita partida ? APU principal.</summary>
public sealed class PartidaApuVinculoRow
{
    public string ItemId { get; set; } = "";
    public string ApuId { get; set; } = "";
    public string Modo { get; set; } = "Principal";
    public DateTime ActualizadoEn { get; set; } = AppClock.Now;
}
