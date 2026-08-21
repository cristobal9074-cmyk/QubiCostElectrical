namespace QubiCostElectrical;

/// <summary>Snapshot de coeficientes y política comercial guardado en el proyecto .qce.</summary>
public sealed class CostosCoeficientesPresetRow
{
    public string Etiqueta { get; set; } = "Preset del proyecto";
    public DateTime GuardadoEn { get; set; } = AppClock.Now;
    public Dictionary<string, decimal> Factores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> DescuentosFamiliaPct { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal GastosGeneralesPct { get; set; }
    public decimal UtilidadPct { get; set; }
    public decimal ValorAgregadoEquiposPct { get; set; }
    public bool PoliticaCostosExcel { get; set; }
}
