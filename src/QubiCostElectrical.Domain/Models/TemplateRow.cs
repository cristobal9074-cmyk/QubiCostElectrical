namespace QubiCostElectrical;

public sealed class TemplateItemRow
{
    public string Clase { get; set; } = "MATERIAL";
    public string Codigo { get; set; } = "";
    public string SubPlantilla { get; set; } = "";
    public decimal Factor { get; set; } = 1;
    public decimal Minimo { get; set; }
    public string Tipo { get; set; } = TipoRecurso.MAT;
}

public sealed class TemplateRow
{
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string UnidadBase { get; set; } = Unidades.ML;
    public string Keywords { get; set; } = "";
    public string Area { get; set; } = "General";
    public List<TemplateItemRow> Items { get; set; } = new();
}
