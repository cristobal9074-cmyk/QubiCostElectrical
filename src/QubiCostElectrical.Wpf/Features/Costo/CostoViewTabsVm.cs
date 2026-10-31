using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>Pestañas del desglose de costos (rediseño 2.0).</summary>
public enum CostoTabKind
{
    Composicion,
    Cierre,
    Familias,
    MoViaticos,
    GastosOperacionales
}

/// <summary>
/// Estado de una pestaña de la barra segmentada. El clic escribe <see cref="IsSelected"/>
/// (TwoWay) y avisa al contenedor para que apague las demás.
/// </summary>
public sealed class CostoTabVm : ViewModelBase
{
    private readonly Action<CostoTabKind> onSelect;
    private bool isSelected;

    public CostoTabVm(CostoTabKind kind, string label, string glyph, Action<CostoTabKind> onSelect)
    {
        Kind = kind;
        Label = label;
        Glyph = glyph;
        this.onSelect = onSelect;
    }

    public CostoTabKind Kind { get; }
    public string Label { get; }

    /// <summary>Glifo decorativo de la pestaña (jerarquía visual del rediseño).</summary>
    public string Glyph { get; }

    private string countLabel = "0";
    private int count;
    public string CountLabel
    {
        get => countLabel;
        private set => SetProperty(ref countLabel, value);
    }

    /// <summary>Sin badge cuando no hay nada que contar (M.O. y gastos no aportan un conteo útil).</summary>
    public bool HasCount => count > 0;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (!SetProperty(ref isSelected, value))
                return;
            if (value)
                onSelect(Kind);
        }
    }

    internal void SetSelectedSilent(bool value)
    {
        if (isSelected == value)
            return;
        isSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }

    internal void SetCount(int value)
    {
        count = value;
        CountLabel = value.ToString("N0", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(HasCount));
    }
}

/// <summary>Línea de detalle dentro de una sub-familia del costo directo.</summary>
public sealed class CostoSubFamilyLineVm
{
    public string Concepto { get; init; } = "";
    public string Explicacion { get; init; } = "";
    public string OrigenCode { get; init; } = "";
    public string OrigenHint { get; init; } = "";
    public string ImporteLabel { get; init; } = "";
    public string ImporteSecundario { get; init; } = "";
    public string PctLabel { get; init; } = "";
    public bool IsZero { get; init; }
    public bool HasSecundario => ImporteSecundario.Length > 0;
    public System.Windows.Media.Brush OrigenForeground => CostoViewTabsVm.OrigenFill(OrigenCode, 1);
    public System.Windows.Media.Brush OrigenBackground => CostoViewTabsVm.OrigenFill(OrigenCode, 0.14);
}

/// <summary>
/// Sub-familia del costo directo (Materiales, Mano de obra, Equipos, Complementarios).
/// Agrupa líneas homogéneas del motor sin alterar montos: solo presentación.
/// </summary>
public sealed class CostoSubFamilyVm : ViewModelBase
{
    private bool isExpanded = true;

    public string Titulo { get; init; } = "";
    public string OrigenCode { get; init; } = "";
    public string OrigenHint { get; init; } = "";
    public string TotalLabel { get; init; } = "";
    public string TotalSecundario { get; init; } = "";
    public string PctLabel { get; init; } = "";
    public string DetalleLabel { get; init; } = "";
    public bool HasTotalSecundario => TotalSecundario.Length > 0;
    public System.Windows.Media.Brush OrigenForeground => CostoViewTabsVm.OrigenFill(OrigenCode, 1);
    public System.Windows.Media.Brush OrigenBackground => CostoViewTabsVm.OrigenFill(OrigenCode, 0.14);
    public ObservableCollection<CostoSubFamilyLineVm> Lineas { get; } = new();

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
                OnPropertyChanged(nameof(Chevron));
        }
    }

    public string Chevron => isExpanded ? "\u25BE" : "\u25B8";
}

/// <summary>Fila del resumen de concentración de materiales (Pareto Top 3).</summary>
public sealed class CostoConcentracionRowVm
{
    public int Puesto { get; init; }
    public string Familia { get; init; } = "";
    public string ImporteLabel { get; init; } = "";
    public string ShareLabel { get; init; } = "";
    public double Share { get; init; }
}

/// <summary>Chip de la leyenda de orígenes del costo directo (CUB, APU, OC, GLO, COEF…).</summary>
public sealed class CostoOriginLegendVm
{
    public string Code { get; init; } = "";
    public string Hint { get; init; } = "";
    public System.Windows.Media.Brush Foreground => CostoViewTabsVm.OrigenFill(Code, 1);
    public System.Windows.Media.Brush Background => CostoViewTabsVm.OrigenFill(Code, 0.14);
}

/// <summary>
/// Estado de la pantalla de desglose de costos: pestaña activa, KPIs del hero y
/// agrupación del costo directo en cuatro sub-familias con sus chips de origen.
/// Es una proyección derivada del <see cref="CostBreakdown"/>; no recalcula nada.
/// </summary>
public sealed class CostoViewTabsVm : ViewModelBase
{
    /// <summary>Agrupación aprobada del costo directo: 4 sub-familias + origen declarado.</summary>
    private static readonly (string Titulo, string Origen, string Hint, string[] Prefixes)[] Composicion =
    [
        ("Materiales", "CUB", "Cubicación y banco de precios", ["Materiales", "Ferretería", "Mermas"]),
        ("Mano de obra", "CUB", "APU y módulo de mano de obra", ["Mano de obra"]),
        ("Equipos", "APU", "Equipos y herramientas provenientes de APU", ["Equipos", "Herramientas"]),
        ("Complementarios", "GLO", "Globales, subcontratos y órdenes de cambio",
            ["Gastos operacionales", "Subcontratos", "Transporte", "Órdenes de cambio"]),
    ];

    private static readonly Dictionary<string, string> OrigenDeLinea = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Materiales"] = "CUB",
        ["Ferretería"] = "COEF",
        ["Mermas"] = "COEF",
        ["Mano de obra"] = "CUB",
        ["Equipos"] = "APU",
        ["Herramientas"] = "APU",
        ["Gastos operacionales"] = "GLO",
        ["Subcontratos"] = "CUB",
        ["Transporte"] = "GLO",
        ["Órdenes de cambio"] = "OC",
    };

    private CostoTabKind selected = CostoTabKind.Composicion;

    public CostoViewTabsVm()
    {
        Items =
        [
            new CostoTabVm(CostoTabKind.Composicion, "Composición", "\u25A6", Select),
            new CostoTabVm(CostoTabKind.Cierre, "Márgenes y cierre", "\u25D4", Select),
            new CostoTabVm(CostoTabKind.Familias, "Familias", "\u25A4", Select),
            new CostoTabVm(CostoTabKind.MoViaticos, "M.O. y viáticos", "\u2637", Select),
            new CostoTabVm(CostoTabKind.GastosOperacionales, "Gastos operacionales", "\u2261", Select),
        ];
        Items[0].SetSelectedSilent(true);
        BuildLegend();
    }

    public ObservableCollection<CostoTabVm> Items { get; }
    public ObservableCollection<CostoSubFamilyVm> SubFamilies { get; } = new();
    public ObservableCollection<CostoConcentracionRowVm> Concentracion { get; } = new();

    /// <summary>Leyenda fija de códigos de origen (mismo lenguaje visual en toda la pantalla).</summary>
    public ObservableCollection<CostoOriginLegendVm> OrigenesLegend { get; } = new();

    private void BuildLegend()
    {
        foreach (var code in new[] { "CUB", "APU", "OC", "GLO", "COEF", "FAM", "NEG", "TRIB" })
            OrigenesLegend.Add(new CostoOriginLegendVm { Code = code, Hint = DescripcionOrigen(code) });
    }

    public CostoTabKind Selected
    {
        get => selected;
        private set
        {
            if (selected == value)
                return;
            selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsComposicion));
            OnPropertyChanged(nameof(IsCierre));
            OnPropertyChanged(nameof(IsFamilias));
            OnPropertyChanged(nameof(IsMoViaticos));
            OnPropertyChanged(nameof(IsGastosOperacionales));
        }
    }

    public bool IsComposicion => selected == CostoTabKind.Composicion;
    public bool IsCierre => selected == CostoTabKind.Cierre;
    public bool IsFamilias => selected == CostoTabKind.Familias;
    public bool IsMoViaticos => selected == CostoTabKind.MoViaticos;
    public bool IsGastosOperacionales => selected == CostoTabKind.GastosOperacionales;

    // ─── Hero: KPIs del rediseño ──────────────────────────────────────────────

    private string costoProyectoLabel = "—";
    public string CostoProyectoLabel
    {
        get => costoProyectoLabel;
        private set => SetProperty(ref costoProyectoLabel, value);
    }

    private string costoProyectoSub = "";
    public string CostoProyectoSub
    {
        get => costoProyectoSub;
        private set => SetProperty(ref costoProyectoSub, value);
    }

    private string ventaNetaLabel = "—";
    public string VentaNetaLabel
    {
        get => ventaNetaLabel;
        private set => SetProperty(ref ventaNetaLabel, value);
    }

    private string ventaNetaSub = "";
    private string totalOfertaConIvaLabel = "";
    public string TotalOfertaConIvaLabel { get => totalOfertaConIvaLabel; private set => SetProperty(ref totalOfertaConIvaLabel, value); }
    private bool hasTotalOfertaConIva;
    public bool HasTotalOfertaConIva { get => hasTotalOfertaConIva; private set => SetProperty(ref hasTotalOfertaConIva, value); }
    public string VentaNetaSub
    {
        get => ventaNetaSub;
        private set => SetProperty(ref ventaNetaSub, value);
    }

    private string diferenciaFactoresLabel = "—";
    public string DiferenciaFactoresLabel
    {
        get => diferenciaFactoresLabel;
        private set => SetProperty(ref diferenciaFactoresLabel, value);
    }

    private string diferenciaFactoresSub = "";
    public string DiferenciaFactoresSub
    {
        get => diferenciaFactoresSub;
        private set => SetProperty(ref diferenciaFactoresSub, value);
    }

    private string concentracionLabel = "—";
    public string ConcentracionLabel
    {
        get => concentracionLabel;
        private set => SetProperty(ref concentracionLabel, value);
    }

    private string concentracionSub = "Sin familias de materiales";
    public string ConcentracionSub
    {
        get => concentracionSub;
        private set => SetProperty(ref concentracionSub, value);
    }

    private string subFamiliaTotalLabel = "—";
    /// <summary>= Costo de ejecución: cierre de la suma de las cuatro sub-familias.</summary>
    public string SubFamiliaTotalLabel
    {
        get => subFamiliaTotalLabel;
        private set => SetProperty(ref subFamiliaTotalLabel, value);
    }

    private string subFamiliaTotalSub = "";
    public string SubFamiliaTotalSub
    {
        get => subFamiliaTotalSub;
        private set => SetProperty(ref subFamiliaTotalSub, value);
    }

    private string subFamiliaTotalPct = "";
    public string SubFamiliaTotalPct
    {
        get => subFamiliaTotalPct;
        private set => SetProperty(ref subFamiliaTotalPct, value);
    }

    private bool hasSubFamilies;
    public bool HasSubFamilies
    {
        get => hasSubFamilies;
        private set => SetProperty(ref hasSubFamilies, value);
    }

    // ─── Selección ────────────────────────────────────────────────────────────

    private void Select(CostoTabKind kind)
    {
        foreach (var item in Items)
            item.SetSelectedSilent(item.Kind == kind);
        Selected = kind;
    }

    // ─── Proyección ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstruye la vista derivada del desglose. Los montos de las líneas vienen en CLP
    /// (el sufijo histórico <c>Uf</c> del motor guarda pesos); el formateo lo aporta el host.
    /// </summary>
    public void Update(
        CostBreakdown cost,
        ProjectInfoRow project,
        IReadOnlyList<CostoFamiliaVm> familias,
        Func<decimal, string> money,
        Func<decimal, string> moneySecondary)
    {
        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);
        var ventaBase = cost.TotalVentaUf;

        BuildSubFamilies(lines, ventaBase, money, moneySecondary);

        SubFamiliaTotalLabel = money(cost.CostoDirectoUf);
        SubFamiliaTotalSub = moneySecondary(cost.CostoDirectoUf);
        SubFamiliaTotalPct = Pct(cost.CostoDirectoUf, ventaBase);

        CostoProyectoLabel = money(cost.CostoProyectoUf);
        CostoProyectoSub = moneySecondary(cost.CostoProyectoUf);
        VentaNetaLabel = money(cost.TotalVentaNetoUf);
        VentaNetaSub = moneySecondary(cost.TotalVentaNetoUf);
        if (project.IncluirIvaEnTotal && cost.TotalVentaConIvaUf > 0m && cost.TotalVentaConIvaUf != cost.TotalVentaNetoUf)
        {
            HasTotalOfertaConIva = true;
            TotalOfertaConIvaLabel = $"Total c/IVA: {money(cost.TotalVentaConIvaUf)}";
        }
        else
        {
            HasTotalOfertaConIva = false;
            TotalOfertaConIvaLabel = "";
        }
        DiferenciaFactoresLabel = money(cost.DiferenciaFactoresUf);
        DiferenciaFactoresSub = moneySecondary(cost.DiferenciaFactoresUf);

        BuildConcentracion(familias, money);

        Items[0].SetCount(SubFamilies.Sum(f => f.Lineas.Count));
        Items[1].SetCount(CierreCount(lines));
        Items[2].SetCount(familias.Count);
        Items[3].SetCount(0);
        Items[4].SetCount(0);
    }

    /// <summary>Líneas del tramo de cierre (recargos y margen que siguen al costo de ejecución).</summary>
    private static int CierreCount(IReadOnlyList<CostoBreakdownLine> lines)
    {
        var vioCosto = false;
        var n = 0;
        foreach (var line in lines)
        {
            if (line.Kind == "total")
            {
                vioCosto = true;
                continue;
            }
            if (vioCosto && line.Kind == "regular" && line.AmountUf != 0m)
                n++;
        }
        return n;
    }

    private void BuildSubFamilies(
        IReadOnlyList<CostoBreakdownLine> lines,
        decimal ventaBase,
        Func<decimal, string> money,
        Func<decimal, string> moneySecondary)
    {
        SubFamilies.Clear();

        var acumulado = new decimal[Composicion.Length];
        var detalle = new List<CostoSubFamilyLineVm>[Composicion.Length];
        for (var i = 0; i < detalle.Length; i++)
            detalle[i] = new List<CostoSubFamilyLineVm>();

        // El costo directo termina en la primera línea "total" (Costo de ejecución / Costo directo).
        foreach (var line in lines)
        {
            if (line.Kind == "total")
                break;
            if (line.Kind != "regular")
                continue;

            var idx = IndexOfGroup(line.Label);
            if (idx < 0)
                continue;

            acumulado[idx] += line.AmountUf;
            var code = OrigenDeLinea.FirstOrDefault(kv =>
                line.Label.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)).Value ?? "";
            detalle[idx].Add(new CostoSubFamilyLineVm
            {
                Concepto = line.Label,
                Explicacion = line.Explanation,
                OrigenCode = code,
                OrigenHint = DescripcionOrigen(code),
                ImporteLabel = money(line.AmountUf),
                ImporteSecundario = moneySecondary(line.AmountUf),
                PctLabel = Pct(line.AmountUf, ventaBase),
                IsZero = line.AmountUf == 0m,
            });
        }

        for (var i = 0; i < Composicion.Length; i++)
        {
            var grupo = new CostoSubFamilyVm
            {
                Titulo = Composicion[i].Titulo,
                OrigenCode = Composicion[i].Origen,
                OrigenHint = Composicion[i].Hint,
                TotalLabel = money(acumulado[i]),
                TotalSecundario = moneySecondary(acumulado[i]),
                PctLabel = Pct(acumulado[i], ventaBase),
                DetalleLabel = detalle[i].Count == 1 ? "1 línea" : $"{detalle[i].Count} líneas",
            };
            foreach (var l in detalle[i])
                grupo.Lineas.Add(l);
            SubFamilies.Add(grupo);
        }

        HasSubFamilies = SubFamilies.Any(g => g.Lineas.Count > 0);
    }

    private static int IndexOfGroup(string label)
    {
        for (var g = 0; g < Composicion.Length; g++)
            if (Composicion[g].Prefixes.Any(p => label.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return g;
        return -1;
    }

    private void BuildConcentracion(IReadOnlyList<CostoFamiliaVm> familias, Func<decimal, string> money)
    {
        Concentracion.Clear();
        var ordered = familias.Where(f => f.TotalClp > 0m).OrderByDescending(f => f.TotalClp).ToList();
        var total = ordered.Sum(f => f.TotalClp);
        if (total <= 0m)
        {
            ConcentracionLabel = "—";
            ConcentracionSub = "Sin familias de materiales";
            return;
        }

        var top = ordered.Take(3).ToList();
        for (var i = 0; i < top.Count; i++)
        {
            var share = (double)(top[i].TotalClp / total);
            Concentracion.Add(new CostoConcentracionRowVm
            {
                Puesto = i + 1,
                Familia = top[i].Familia,
                ImporteLabel = money(top[i].TotalClp),
                ShareLabel = (share * 100d).ToString("N1", CultureInfo.CurrentCulture) + " %",
                Share = share,
            });
        }

        var top3 = top.Sum(f => f.TotalClp) / total;
        ConcentracionLabel = (top3 * 100m).ToString("N1", CultureInfo.CurrentCulture) + " %";
        ConcentracionSub = $"Top {top.Count} · {(top3 * 100m):N0} % del material";
    }

    private static string Pct(decimal monto, decimal baseUf)
        => baseUf == 0m ? "—" : (monto / baseUf * 100m).ToString("N1", CultureInfo.CurrentCulture) + " %";

    private static readonly Dictionary<string, string> OrigenPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CUB"] = "Brush.Blue",
        ["APU"] = "Brush.Violet",
        ["OC"] = "Brush.Amber",
        ["GLO"] = "Brush.Slate",
        ["COEF"] = "Brush.Cyan",
        ["FAM"] = "Brush.Purple",
        ["NEG"] = "Brush.Pink",
        ["TRIB"] = "Brush.Red",
    };

    /// <summary>Color temático del chip de origen (mismo código ⇒ mismo color).</summary>
    public static System.Windows.Media.Brush OrigenFill(string code, double opacity)
        => CostoUiBrushes.Parse(OrigenPalette.TryGetValue(code ?? "", out var key) ? key : "Brush.Slate3", opacity);

    private static string DescripcionOrigen(string code) => code switch
    {
        "CUB" => "Partidas valorizadas desde la cubicación",
        "APU" => "Análisis de precios unitarios",
        "OC" => "Órdenes de cambio aprobadas",
        "GLO" => "Recargos globales del motor",
        "COEF" => "Coeficiente sobre materiales",
        "FAM" => "Factores de presentación por familia",
        "NEG" => "Descuentos negociados",
        "TRIB" => "Tributos e IVA",
        _ => "Origen no clasificado",
    };
}
