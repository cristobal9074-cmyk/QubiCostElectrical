using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Coordina la generación de filas de costo (CostRows), grupos jerárquicos, ferretería por familia y KPIs de resumen.
/// </summary>
internal sealed class CostoBreakdownReportCoordinator
{
    private readonly CostoViewModel _vm;

    public CostoBreakdownReportCoordinator(CostoViewModel vm)
    {
        _vm = vm;
    }

    public void UpdateSummaryKpis(CostBreakdown cost, ProjectInfoRow project, decimal valorUf)
    {
        decimal directoClp = cost.CostoDirectoClp;
        decimal ventaClp = cost.TotalVentaClp;
        decimal m2Clp = project.MetrosCuadrados > 0 ? ventaClp / project.MetrosCuadrados : 0m;

        bool enClp = !_vm.ShowsUfFigures;
        var costoPair = _vm.FormatMoneyPair(directoClp);
        var ventaPair = _vm.FormatMoneyPair(ventaClp);
        _vm.CostoDirectoSubLabel = costoPair.Primary;
        _vm.CostoDirectoLabel = costoPair.Secondary;
        _vm.TotalVentaSubLabel = ventaPair.Primary;
        _vm.TotalVentaLabel = ventaPair.Secondary;
        _vm.MargenLabel = $"{cost.MargenRealPct:N1} %";
        decimal margenClp = Math.Max(0m, ventaClp - directoClp);
        var margenPair = _vm.FormatMoneyPair(margenClp);
        _vm.MargenSubLabel = margenPair.Secondary.Length > 0
            ? $"{margenPair.Primary} ({margenPair.Secondary})"
            : margenPair.Primary;
        _vm.ValorUfDiaLabel = valorUf.ToString("N0", CultureInfo.CurrentCulture);
        _vm.UfM2Label = enClp
            ? $"$ {m2Clp:N0}/m²"
            : $"{PriceConversion.ClpToUf(m2Clp, valorUf):N3} UF/m²";
        _vm.UfM2SubLabel = project.MetrosCuadrados > 0
            ? (enClp ? "Total venta / m²" : $"$ {m2Clp:N0}/m²")
            : "Sin m²";
        _vm.ResumenFooterText = enClp
            ? $"Costo ejecución $ {directoClp:N0} · Total venta $ {ventaClp:N0} · Margen {cost.MargenRealPct:N1}%"
            : $"Costo ejecución {_vm.MoneyTextInternal(directoClp)} · Total venta {_vm.MoneyTextInternal(ventaClp)} · Margen {cost.MargenRealPct:N1}%";
        _vm.NegotiationSimulator.UpdateBase(directoClp, ventaClp, valorUf);

        // Segmentación Financiera (Ratio Apilado WCAG AAA)
        decimal indirectosClp = Math.Max(0m, cost.GastosGeneralesUf + cost.RiesgoUf + cost.ValorAgregadoEquiposUf);
        if (cost.CostoProyectoUf > directoClp + indirectosClp && cost.UtilidadAditivaUf == 0m)
        {
            indirectosClp = cost.CostoProyectoUf - directoClp;
        }
        decimal margenFinancieroClp = Math.Max(0m, ventaClp - directoClp - indirectosClp);
        decimal totalReparto = directoClp + indirectosClp + margenFinancieroClp;

        if (totalReparto > 0m && ventaClp > 0m)
        {
            double pctDir = (double)(directoClp / totalReparto * 100m);
            double pctInd = (double)(indirectosClp / totalReparto * 100m);
            double pctMar = (double)(margenFinancieroClp / totalReparto * 100m);

            _vm.FinancialRatioDirectosStar = new System.Windows.GridLength(Math.Max(0.001, pctDir), System.Windows.GridUnitType.Star);
            _vm.FinancialRatioIndirectosStar = new System.Windows.GridLength(Math.Max(0.001, pctInd), System.Windows.GridUnitType.Star);
            _vm.FinancialRatioMargenStar = new System.Windows.GridLength(Math.Max(0.001, pctMar), System.Windows.GridUnitType.Star);

            _vm.FinancialRatioDirectosPct = $"{pctDir:N1}%";
            _vm.FinancialRatioIndirectosPct = $"{pctInd:N1}%";
            _vm.FinancialRatioMargenPct = $"{pctMar:N1}%";

            var pairDir = _vm.FormatMoneyPair(directoClp);
            var pairInd = _vm.FormatMoneyPair(indirectosClp);
            var pairMar = _vm.FormatMoneyPair(margenFinancieroClp);

            _vm.FinancialRatioDirectosMonto = pairDir.Primary;
            _vm.FinancialRatioDirectosSub = pairDir.Secondary;
            _vm.FinancialRatioIndirectosMonto = pairInd.Primary;
            _vm.FinancialRatioIndirectosSub = pairInd.Secondary;
            _vm.FinancialRatioMargenMonto = pairMar.Primary;
            _vm.FinancialRatioMargenSub = pairMar.Secondary;

            _vm.HasFinancialRatioData = true;
        }
        else
        {
            _vm.FinancialRatioDirectosStar = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            _vm.FinancialRatioIndirectosStar = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            _vm.FinancialRatioMargenStar = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            _vm.FinancialRatioDirectosPct = "—";
            _vm.FinancialRatioIndirectosPct = "—";
            _vm.FinancialRatioMargenPct = "—";
            _vm.FinancialRatioDirectosMonto = "—";
            _vm.FinancialRatioIndirectosMonto = "—";
            _vm.FinancialRatioMargenMonto = "—";
            _vm.FinancialRatioDirectosSub = "";
            _vm.FinancialRatioIndirectosSub = "";
            _vm.FinancialRatioMargenSub = "";
            _vm.HasFinancialRatioData = false;
        }

        _vm.Tabs.Update(cost, project, _vm.Familias, _vm.FormatMoneyPrimary, clp => _vm.FormatMoneyPair(clp).Secondary);
    }

    public void RefreshFerreteriaFamilias(CostBreakdown cost, ProjectFileData data, ProjectInfoRow project, decimal valorUf)
    {
        _ = valorUf;
        decimal pct = project.FerreteriaPct;
        decimal baseEngine = cost.MaterialesUf - cost.FerreteriaUf;
        var codeToFamily = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in data.Materiales ?? Enumerable.Empty<MaterialRow>())
        {
            if (string.IsNullOrWhiteSpace(material.Codigo) || codeToFamily.ContainsKey(material.Codigo))
                continue;
            codeToFamily[material.Codigo] = string.IsNullOrWhiteSpace(material.Categoria)
                ? "Sin familia"
                : material.Categoria.Trim();
        }

        var byFamily = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal summed = 0m;
        foreach (var row in data.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            decimal mat = row.Cantidad * row.MatClpConFallback;
            if (mat == 0m)
                continue;
            var familia = !string.IsNullOrWhiteSpace(row.Codigo) && codeToFamily.TryGetValue(row.Codigo, out var name)
                ? name
                : "Sin familia";
            byFamily[familia] = byFamily.GetValueOrDefault(familia) + mat;
            summed += mat;
        }

        decimal gap = baseEngine - summed;
        if (Math.Abs(gap) >= 1m)
            byFamily["Otros materiales"] = byFamily.GetValueOrDefault("Otros materiales") + gap;

        _vm.FerreteriaFamilias.Clear();
        decimal totalMat = 0m;
        decimal totalFerr = 0m;
        foreach (var pair in byFamily.Where(p => p.Value != 0m).OrderByDescending(p => p.Value))
        {
            decimal ferr = pair.Value * pct / 100m;
            totalMat += pair.Value;
            totalFerr += ferr;
            _vm.FerreteriaFamilias.Add(LineaFerreteria(pair.Key, pair.Value, ferr, total: false));
        }

        if (_vm.FerreteriaFamilias.Count > 0)
            _vm.FerreteriaFamilias.Add(LineaFerreteria("Total", totalMat, totalFerr, total: true));

        _vm.FerreteriaPopupTitulo = $"Ferretería {pct:N1} %";
        _vm.FerreteriaPopupNota = project.FerreteriaActiva
            ? "Porcentaje sobre los materiales de cada familia. Este monto ya está en el costo."
            : "Porcentaje sobre los materiales de cada familia. Está apagada: todavía no suma al costo.";
    }

    public FerreteriaFamiliaVm LineaFerreteria(string familia, decimal materiales, decimal ferreteria, bool total)
        => new()
        {
            Familia = familia,
            MaterialesLabel = _vm.FormatMoneyPrimary(materiales),
            MaterialesUfLabel = _vm.FormatMoneyPair(materiales).Secondary,
            FerreteriaLabel = _vm.FormatMoneyPrimary(ferreteria),
            FerreteriaUfLabel = _vm.FormatMoneyPair(ferreteria).Secondary,
            IsTotal = total
        };

    public void ToggleCostGroup(CostoGrupoVm? grupo)
    {
        if (grupo == null)
            return;
        grupo.IsExpanded = !grupo.IsExpanded;
    }

    public void ToggleFerreteriaDetalle()
    {
        _vm.FerreteriaDetalleAbierto = !_vm.FerreteriaDetalleAbierto;
    }

    public CostoRowVm CreateCostRow(string label, decimal clp, string explanation, decimal valorUf, string kind, string participacion = "", bool enInforme = true, bool tieneOrigen = false, string origenTexto = "", double share = 0d, string seccion = "directo")
    {
        decimal uf = PriceConversion.ClpToUf(clp, valorUf);
        return new CostoRowVm
        {
            Concepto = label,
            ValorUf = uf,
            CurrencyFigure = _vm.FormatMoneyPrimary(clp),
            ShowCurrencyColumn = _vm.ShowsUfFigures,
            ValorMoneda = clp,
            Explicacion = explanation,
            Kind = kind,
            ParticipacionLabel = participacion,
            Share = share,
            Seccion = seccion,
            IsEnInforme = enInforme,
            TieneOrigen = tieneOrigen,
            OrigenTexto = origenTexto,
            ValorMonedaLabel = clp == 0m
                ? "$ 0"
                : $"$ {decimal.Round(clp, 0, MidpointRounding.AwayFromZero):N0}"
        };
    }

    public void AddRow(ObservableCollection<CostoRowVm> rows, string label, decimal clp, string explanation,
        string moneda, decimal valorUf, decimal tipoCambio, string kind = "regular")
    {
        _ = moneda;
        _ = tipoCambio;
        rows.Add(CreateCostRow(label, clp, explanation, valorUf, kind));
    }

    public void PushCostGroup(string titulo, CostoRowVm header, IReadOnlyList<CostoRowVm> children, bool firstLoad, HashSet<string> openTitles, bool expandOnFirst)
    {
        var grupo = new CostoGrupoVm
        {
            Titulo = titulo,
            ClpLabel = header.ValorMonedaLabel,
            UfLabel = header.CurrencyFigure,
            Explicacion = header.Explicacion,
            IsExpanded = firstLoad ? expandOnFirst : openTitles.Contains(titulo)
        };
        foreach (var child in children)
            grupo.Lineas.Add(child);
        _vm.CostGroups.Add(grupo);
    }

    public void FillCostRows(
        ObservableCollection<CostoRowVm> rows,
        CostBreakdown cost,
        ProjectInfoRow project,
        string moneda,
        decimal valorUf,
        decimal tipoCambio)
    {
        _ = moneda;
        _ = tipoCambio;
        rows.Clear();
        _vm.OrigenesDirectos.Clear();
        var openTitles = _vm.CostGroups.Where(g => g.IsExpanded).Select(g => g.Titulo).ToHashSet(StringComparer.Ordinal);
        var firstLoad = _vm.CostGroups.Count == 0;
        _vm.CostGroups.Clear();
        _vm.CierreAjustes.Clear();

        var pending = new List<CostoRowVm>();
        CostoRowVm? venta = null;
        CostoRowVm? metro = null;
        var sawCost = false;
        var sawRecargos = false;
        var openedComp = false;
        var openedCierre = false;
        var ventaBase = cost.TotalVentaUf;

        foreach (var line in CostoBreakdownLineBuilder.BuildLines(cost, project))
        {
            var enInforme = !CostoBreakdownLineBuilder.IsMetroLine(line.Label);
            if (!sawCost && !openedComp && line.Kind == "regular" && line.AmountUf != 0m)
            {
                rows.Add(InformeSection("Composición del costo de ejecución"));
                openedComp = true;
            }

            if (sawCost && !openedCierre && enInforme && line.Kind != "grand")
            {
                rows.Add(InformeSection("Recargos, margen y cierre"));
                openedCierre = true;
            }

            var share = enInforme && ventaBase != 0m ? $"{line.AmountUf / ventaBase * 100m:N1} %" : "";
            var esFerreteria = line.Label.StartsWith("Ferretería", StringComparison.Ordinal);
            var row = CreateCostRow(line.Label, line.AmountUf, line.Explanation, valorUf, line.Kind, share, enInforme, esFerreteria, "", enInforme && ventaBase != 0m ? (double)(line.AmountUf / ventaBase) : 0d, sawCost ? "cierre" : "directo");
            rows.Add(row);
            if (!sawCost && line.Kind == "regular" && !row.IsQuiet)
                _vm.OrigenesDirectos.Add(row);

            if (line.Kind == "grand")
            {
                venta = row;
                continue;
            }

            if (CostoBreakdownLineBuilder.IsMetroLine(line.Label))
            {
                metro = row;
                continue;
            }

            if (line.Kind == "total" && !sawCost)
            {
                PushCostGroup(line.Label, row, pending, firstLoad, openTitles, expandOnFirst: true);
                pending.Clear();
                sawCost = true;
                continue;
            }

            if (line.Kind == "subtotal" && sawCost && !sawRecargos)
            {
                PushCostGroup(line.Label, row, pending, firstLoad, openTitles, expandOnFirst: false);
                pending.Clear();
                sawRecargos = true;
                continue;
            }

            pending.Add(row);
        }

        if (pending.Count > 0)
        {
            var header = pending.LastOrDefault(r => r.Kind == "subtotal");
            if (header != null)
            {
                var index = pending.IndexOf(header);
                PushCostGroup(header.Concepto, header, pending.Take(index).ToList(), firstLoad, openTitles, expandOnFirst: false);
                foreach (var extra in pending.Skip(index + 1))
                    _vm.CierreAjustes.Add(extra);
            }
            else
            {
                foreach (var extra in pending)
                    _vm.CierreAjustes.Add(extra);
            }
        }

        _vm.VentaCierre = venta;
        _vm.UfMetroCierre = metro;
        _vm.ChainIvaLabel = rows.FirstOrDefault(r => r.Concepto == "IVA") is { } iva ? _vm.MoneyTextInternal(iva.ValorMoneda) : "—";
        _vm.ChainTotalLabel = venta?.ValorMonedaLabel is { Length: > 0 } clp ? clp : "—";
    }

    public static CostoRowVm InformeSection(string title) => new()
    {
        Concepto = title,
        Kind = "section",
        Explicacion = "",
        ValorMonedaLabel = ""
    };
}
