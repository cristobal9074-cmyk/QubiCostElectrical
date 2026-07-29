using System;

namespace QubiCostElectrical;

/// <summary>
/// Política de moneda. El motor calcula siempre en CLP (moneda base); la conversión visual
/// a otras monedas se mantiene en CurrencyConversionService. UF queda para resúmenes de partidas.
/// </summary>
public sealed class CurrencyPolicy
{
    public string MonedaPresupuesto { get; set; } = "CLP";
    public decimal ValorUfClp { get; set; }
    public decimal TipoCambioUsdClp { get; set; }
}

/// <summary>
/// Configuración completa del presupuesto para BudgetEngine.
/// </summary>
public sealed class BudgetSettings
{
    public MarkupPolicy Markup { get; set; } = new();
    public CurrencyPolicy Currency { get; set; } = new();
    public decimal MetrosCuadrados { get; set; }
    public decimal HerramientasPct { get; set; } = 0m;
    public decimal MermasPct { get; set; } = 0m;

    /// <summary>GO del módulo Gastos Operacionales (CLP). 0 = no hay GO en el puente.</summary>
    public decimal GastosOperacionalesClp { get; set; }

    /// <summary>
    /// Total MO desde módulo Mano de obra (CLP). Si &gt; 0 reemplaza ManoObra del motor
    /// en el costo de ejecución bajo política Excel.
    /// </summary>
    public decimal ManoObraDotacionClp { get; set; }

    /// <summary>
    /// Define cómo se integran Cubicación y APU cuando ambos existen para una misma partida.
    /// - CubicacionManda: mantiene compatibilidad histórica; el APU solo entra cuando no hay cubicación valorizada.
    /// - ApuManda: el APU es la fuente técnica del precio unitario; la cubicación solo entra en partidas sin APU.
    /// - CubicacionMasApuComplementarios: la cubicación aporta materiales/MO y el APU suma equipos, subcontratos, HH, herramientas e imprevistos.
    /// - Manual: usa los valores manuales de esta configuración.
    /// </summary>
    public BudgetApuMode ApuMode { get; set; } = BudgetApuMode.CubicacionMasApuComplementarios;

    // Modo Manual (valores en CLP — moneda base del motor).
    public decimal ManualMaterialesUf { get; set; }
    public decimal ManualManoObraUf { get; set; }
    public decimal ManualEquiposUf { get; set; }
    public decimal ManualSubcontratosUf { get; set; }
    public decimal ManualHerramientasUf { get; set; }
    public decimal ManualMermasUf { get; set; }

    public static BudgetSettings FromProject(ProjectInfoRow? project)
    {
        project ??= new ProjectInfoRow();
        return new BudgetSettings
        {
            MetrosCuadrados = project.MetrosCuadrados,
            HerramientasPct = MarkupPolicy.ClampPct(project.HerramientasPct),
            MermasPct = MarkupPolicy.ClampPct(project.MermasPct),
            ApuMode = ParseApuMode(project.ApuCostingMode),
            Markup = new MarkupPolicy
            {
                MarkupMethod = Enum.TryParse<MarkupMethod>(project.MarkupMethod, true, out var mm) ? mm : MarkupMethod.SobreVenta,
                GastosGeneralesPct = project.GastosGeneralesPct,
                UtilidadPct = project.UtilidadPct,
                ValorAgregadoEquiposPct = project.ValorAgregadoEquiposPct,
                PoliticaCostosExcel = project.PoliticaCostosExcel,
                RiesgoPct = project.RiesgoPct,
                DescuentoPct = project.DescuentoPct,
                IvaPct = project.IvaPct,
                IncluirIvaEnTotal = project.IncluirIvaEnTotal
            },
            Currency = new CurrencyPolicy
            {
                MonedaPresupuesto = string.IsNullOrWhiteSpace(project.MonedaPresupuesto) ? "CLP" : project.MonedaPresupuesto.Trim().ToUpperInvariant(),
                ValorUfClp = project.ValorUfClp,
                TipoCambioUsdClp = project.TipoCambioUsdClp
            }
        };
    }

    public static BudgetSettings FromValues(decimal gastosGeneralesPct, decimal utilidadPct, decimal metrosCuadrados)
    {
        return new BudgetSettings
        {
            MetrosCuadrados = metrosCuadrados,
            Markup = new MarkupPolicy
            {
                GastosGeneralesPct = gastosGeneralesPct,
                UtilidadPct = utilidadPct
            }
        };
    }

    public static BudgetApuMode ParseApuMode(string? value)
    {
        if (Enum.TryParse<BudgetApuMode>(value, true, out var mode)) return mode;
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "CUBICACION" or "CUBICACIÓN" or "CUBICACION MANDA" or "CUBICACIÓN MANDA" => BudgetApuMode.CubicacionManda,
            "APU" or "APU MANDA" => BudgetApuMode.ApuManda,
            "COMPLEMENTARIO" or "CUBICACION+APU" or "CUBICACIÓN+APU" or "CUBICACION MAS APU" => BudgetApuMode.CubicacionMasApuComplementarios,
            "MANUAL" => BudgetApuMode.Manual,
            _ => BudgetApuMode.CubicacionMasApuComplementarios
        };
    }
}
