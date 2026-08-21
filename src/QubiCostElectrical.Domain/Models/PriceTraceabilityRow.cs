namespace QubiCostElectrical;

public enum PriceTraceStatus
{
    OK,
    FaltaPrecio,
    PrecioVencido,
    RevisarProveedor,
    ManualSinRespaldo,
    ImportadoSinValidar
}

public sealed class PriceTraceabilityRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal PrecioUf { get; set; }
    public string Moneda { get; set; } = QubiCostElectrical.Moneda.UF;
    public decimal UfUsadaClp { get; set; }
    public string Proveedor { get; set; } = "";
    public string Origen { get; set; } = "Lista de precios";
    public string Usuario { get; set; } = "";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    public DateTime UltimaActualizacion { get; set; } = AppClock.Now;
    public PriceTraceStatus Estado { get; set; } = PriceTraceStatus.OK;
    public string UsadoEn { get; set; } = "";
    public string Observacion { get; set; } = "";

    public string EstadoTexto => Estado switch
    {
        PriceTraceStatus.OK => "OK",
        PriceTraceStatus.FaltaPrecio => "Falta precio",
        PriceTraceStatus.PrecioVencido => "Precio vencido",
        PriceTraceStatus.RevisarProveedor => "Revisar proveedor",
        PriceTraceStatus.ManualSinRespaldo => "Manual sin respaldo",
        PriceTraceStatus.ImportadoSinValidar => "Importado sin validar",
        _ => "Revisar"
    };
}
