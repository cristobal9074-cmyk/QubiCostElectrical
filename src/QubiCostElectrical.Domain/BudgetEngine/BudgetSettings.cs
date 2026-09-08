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

    /// <summary>
    /// Política comercial de cierre por proyecto. Vive en <c>ProjectFileData</c>, no en
    /// <c>ProjectInfoRow</c>, así que <see cref="FromProject"/> no la puebla; los call-sites con
    /// acceso al <c>ProjectFileData</c> completo la asignan (ver <c>ExcelCostosBridge.EnrichFromProjectData</c>).
    /// <c>null</c> ⇒ el motor usa <see cref="ClosingPolicyType.CostPlusMarkup"/> (pass-through).
    /// </summary>
    public ClosingPolicySelection? ClosingPolicy { get; set; }

    /// <summary>
    /// Factor de presentación por familia (venta = Σ costo_origen × factor). Se puebla en
    /// <see cref="FromProject"/> desde <c>ProjectInfo.Variables["MarkupFactor_*"]</c> ×
    /// (1 − <c>MarkupDiscount_*</c>/100). Vacío ⇒ todo cae en <see cref="DefaultPresentationFactor"/>.
    /// </summary>
    public Dictionary<string, decimal> PresentationFactorsByFamily { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Factor para líneas sin familia clasificada (APU por área desconocida, OC, cargos globales).
    /// 1 ⇒ el proyecto no usa factores por familia (no cambia nada respecto de antes).
    /// </summary>
    public decimal DefaultPresentationFactor { get; set; } = 1m;

    /// <summary>Normaliza la familia y devuelve su factor; si no está, <see cref="DefaultPresentationFactor"/>.</summary>
    public decimal ResolvePresentationFactor(string? family)
    {
        var norm = FamilyPresentationDefaults.Normalize(family);
        return PresentationFactorsByFamily.TryGetValue(norm, out var f) ? f : DefaultPresentationFactor;
    }

    /// <summary>Como <see cref="ResolvePresentationFactor"/> pero informa si la familia estaba
    /// mapeada explícitamente (para distinguir un factor 1.0 pactado del fallback).</summary>
    public bool TryResolvePresentationFactor(string? family, out decimal factor)
    {
        var norm = FamilyPresentationDefaults.Normalize(family);
        return PresentationFactorsByFamily.TryGetValue(norm, out factor);
    }
    public decimal MetrosCuadrados { get; set; }
    public decimal HerramientasPct { get; set; } = 0m;
    public decimal MermasPct { get; set; } = 0m;
    public CommercialCostingModel CommercialModel { get; set; } = CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public bool AllowPartidaCommercialOverrides { get; set; } = true;
    public List<CommercialPartidaRule> CommercialPartidaRules { get; set; } = new();

    /// <summary>
    /// Descuentos negociados por código de material/equipo específico (independiente del factor
    /// de presentación por categoría). Se aplican sobre el costo de material antes de GG/Utilidad.
    /// </summary>
    public List<NegotiatedDiscountRow> NegotiatedDiscounts { get; set; } = new();

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
        var settings = new BudgetSettings
        {
            MetrosCuadrados = project.MetrosCuadrados,
            HerramientasPct = MarkupPolicy.ClampPct(project.HerramientasPct),
            MermasPct = MarkupPolicy.ClampPct(project.MermasPct),
            CommercialModel = project.CommercialModel,
            AllowPartidaCommercialOverrides = project.AllowPartidaCommercialOverrides,
            CommercialPartidaRules = (project.CommercialPartidaRules ?? new List<CommercialPartidaRule>()).Select(x => x.Clone()).ToList(),
            ApuMode = ParseApuMode(project.ApuCostingMode),
            Markup = new MarkupPolicy
            {
                MarkupMethod = Enum.TryParse<MarkupMethod>(project.MarkupMethod, true, out var mm) ? mm : MarkupMethod.SobreVenta,
                GastosGeneralesPct = project.GastosGeneralesPct,
                UtilidadPct = project.UtilidadPct,
                ValorAgregadoEquiposPct = project.ValorAgregadoEquiposPct,
                PoliticaCostosExcel = project.PoliticaCostosExcel,
                FactoresPresentacionPorFamilia = project.UsarFactoresPresentacionFamilia || project.PoliticaCostosExcel,
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

        // C-1: unificación de rutas de markup. La venta se arma línea a línea con el factor de
        // presentación por familia. Solo cuando el proyecto lo eligió (UsarFactores || Excel);
        // si no, dict vacío y factor 1 ⇒ no cambia nada para proyectos sin factores.
        if (settings.Markup.FactoresPresentacionPorFamilia)
        {
            var vars = project.Variables;
            if (vars != null)
            {
                foreach (var kvp in vars)
                {
                    if (!kvp.Key.StartsWith("MarkupFactor_", StringComparison.OrdinalIgnoreCase)) continue;
                    var fam = kvp.Key.Substring("MarkupFactor_".Length);
                    if (string.IsNullOrWhiteSpace(fam)) continue;
                    settings.PresentationFactorsByFamily[fam] = FamilyPresentationDefaults.ResolveFactor(vars, fam);
                }
            }
            // Familia general ("" → default 1.14 × (1 − disc)): factor por defecto del proyecto.
            settings.DefaultPresentationFactor = FamilyPresentationDefaults.ResolveFactor(vars, "");
        }

        return settings;
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
