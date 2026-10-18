using System;

namespace QubiCostElectrical;

public sealed class SolicitudItemRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Tipo { get; set; } = "Material"; // "Material" o "EPP"
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "UN";
    public decimal Cantidad { get; set; } = 1;
    public string Link { get; set; } = "";
    public string Observaciones { get; set; } = "";
    public string SolicitudCodigo { get; set; } = "SM 1";
    public string Fecha { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
    public string Estado { get; set; } = "Aprobado";
}
