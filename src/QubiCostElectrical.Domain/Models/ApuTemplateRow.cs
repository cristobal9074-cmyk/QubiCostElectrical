using System;

namespace QubiCostElectrical;

public sealed class ApuTemplateRow
{
    public string Id           { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre       { get; set; } = "";
    public string Categoria    { get; set; } = "";
    public string Descripcion  { get; set; } = "";
    public string Unidad       { get; set; } = "";
    public string JsonRecursos { get; set; } = "[]";
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}
