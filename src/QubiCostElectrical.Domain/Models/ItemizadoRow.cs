namespace QubiCostElectrical;

public sealed class ItemizadoRow
{
    /// <summary>Identificador persistente — se genera una vez al crear la fila y nunca cambia.</summary>
    public Guid PartidaId { get; set; } = Guid.NewGuid();
    public string Id { get; set; } = "";
    public string Item { get; set; } = "";
    public string WBSCode { get; set; } = "";
    public string ParentId { get; set; } = "";
    public int NivelJerarquico { get; set; }
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = Unidades.GL;
    [System.Text.Json.Serialization.JsonPropertyName("Cantidad")]
    public decimal CantidadOriginal { get; set; } = 1;

    [System.Text.Json.Serialization.JsonPropertyName("CantidadCubicada")]
    public decimal Cantidad { get; set; } = 0;
    public bool EsTitulo { get; set; }
    public bool ExcluidoCubicacion { get; set; }
    public string Observaciones { get; set; } = "";
}
