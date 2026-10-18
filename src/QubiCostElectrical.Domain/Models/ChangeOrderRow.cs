namespace QubiCostElectrical;

public sealed class ChangeOrderHistoryRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EstadoAnterior { get; set; } = "";
    public string EstadoNuevo { get; set; } = EstadoPartida.Borrador;
    public DateTime Fecha { get; set; } = AppClock.Now;
    public string Usuario { get; set; } = "";
    public string Nota { get; set; } = "";
}

public sealed class ChangeOrderLineRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string Item { get; set; } = "";
    public string DescripcionPartida { get; set; } = "";
    public string TipoCambio { get; set; } = "Modificación";
    public string Unidad { get; set; } = "UN";
    public bool EsContractual { get; set; } = true;
    public decimal CantidadBase { get; set; }
    public decimal CantidadAdicional { get; set; }
    public decimal CantidadNueva { get; set; }
    public decimal PrecioUnitarioContratoClp { get; set; }
    public decimal PrecioUnitarioContratoUf { get; set; }
    public decimal SubtotalClp { get; set; }
    public decimal SubtotalUf { get; set; }
    public decimal CostoBaseUf { get; set; }
    public decimal CostoNuevoUf { get; set; }
    public decimal DeltaCostoUf { get; set; }
    public string Nota { get; set; } = "";
    public List<CubicacionRow> Materiales { get; set; } = new();
}

public sealed class ChangeOrderRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Numero { get; set; } = "OC-001";
    public string Titulo { get; set; } = "Orden de cambio";
    public string Estado { get; set; } = "Borrador";
    public DateTime FechaCreacion { get; set; } = AppClock.Now;
    public DateTime FechaModificacion { get; set; } = AppClock.Now;
    public string CreadoPor { get; set; } = "";
    public string PresupuestoBaseEstado { get; set; } = "Aprobado";
    public DateTime PresupuestoBaseFecha { get; set; } = AppClock.Now;
    public decimal CostoBaseUf { get; set; }
    public decimal VentaBaseUf { get; set; }
    public decimal GastosGeneralesPct { get; set; }
    public decimal UtilidadPct { get; set; }
    public decimal DeltaCostoUf { get; set; }
    public decimal DeltaGastosGeneralesUf { get; set; }
    public decimal DeltaUtilidadUf { get; set; }
    public decimal DeltaVentaUf { get; set; }
    public string Nota { get; set; } = "";
    public List<ChangeOrderLineRow> Lineas { get; set; } = new();
    public List<ChangeOrderHistoryRow> Historial { get; set; } = new();
}
