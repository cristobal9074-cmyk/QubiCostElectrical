using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Coordina la lógica de Gastos Operacionales (GO): carga, sincronización EPP/traslados,
/// cálculo de totales, barra de composición y diálogo de viáticos.
/// </summary>
internal sealed class CostoGastosOperacionalesCoordinator
{
    private static readonly string[] GoPalette =
    [
        "Brush.BrandStroke",
        "Brush.SuccessBright",
        "Brush.AmberRisk",
        "Brush.Violet",
        "Brush.Pink",
        "Brush.Slate"
    ];

    private readonly CostoViewModel _vm;

    public CostoGastosOperacionalesCoordinator(CostoViewModel vm)
    {
        _vm = vm;
    }

    public void RefreshGoSection(ProjectFileData data, decimal valorUf)
    {
        var open = _vm.GastosOperacionales
            .Where(c => c.IsExpanded)
            .Select(c => c.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _vm.GastosOperacionales.Clear();

        // Repara proyectos donde el sync EPP dejó solo 3.5 (u otras categorías faltantes).
        var repaired = GastoOperacionalPlantilla.EnsureComplete(data);

        var source = data.GastosOperacionales;
        var seededFresh = source == null || source.Count == 0;
        var rows = seededFresh ? GastoOperacionalPlantilla.CreateClean() : source!;

        foreach (var r in rows)
        {
            var expanded = open.Contains(r.Codigo ?? "");
            _vm.GastosOperacionales.Add(new GastoOpCategoriaVm(r, valorUf, OnGoChanged, expanded: expanded, canEdit: () => _vm.CanEdit));
        }

        var ensuredBus = EnsureTrasladoBusItem();
        var syncedEpp = SyncGoEppFromBudget(data, valorUf);
        UpdateGoTotals();
        _vm.ShowGoSection = true;

        if ((seededFresh || repaired || ensuredBus || syncedEpp) && _vm.CanEdit)
            PersistGo(markDirty: true);
    }

    /// <summary>Empuja el total EPP del módulo a la categoría GO 3.5.</summary>
    public bool SyncGoEppFromBudget(ProjectFileData data, decimal valorUf)
    {
        var epp = data.Epp;
        if (epp == null || !epp.HasContent)
            return false;

        // Asegura que las cantidades EPP reflejen la MO actual antes de pintar GO.
        var mo = EppBudgetData.ReadMoDrivers(data);
        epp.ApplyMoDotacion(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
        data.Epp = epp;
        EppBudgetData.PushToGoCategory(data, epp);

        var cat = _vm.GastosOperacionales.FirstOrDefault(c =>
            string.Equals(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase)
            || c.Concepto.Contains("EPP", StringComparison.OrdinalIgnoreCase));

        if (cat == null)
        {
            var row = data.GastosOperacionales?.FirstOrDefault(c =>
                string.Equals(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase));
            if (row == null)
                return false;
            cat = new GastoOpCategoriaVm(row, valorUf, OnGoChanged, canEdit: () => _vm.CanEdit);
            var idx = _vm.GastosOperacionales.ToList().FindIndex(c =>
                string.Compare(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase) > 0);
            if (idx < 0) _vm.GastosOperacionales.Add(cat);
            else _vm.GastosOperacionales.Insert(idx, cat);
            return true;
        }

        var before = cat.MontoClp;
        var goItems = epp.ToGoCategoryItems();
        cat.ReplaceItems(goItems);
        return Math.Abs(cat.MontoClp - before) > 0.5m || cat.Items.Count != goItems.Count;
    }

    public bool EnsureTrasladoBusItem()
    {
        var cat = _vm.GastosOperacionales.FirstOrDefault(c =>
            GastoOperacionalCategoryMaintenance.IsMovilizacionCategory(c.Codigo, c.Concepto));
        if (cat == null)
            return false;

        if (GastoOperacionalCategoryMaintenance.HasTrasladoBusItem(cat.Items.Select(i => i.Concepto)))
            return false;

        cat.AddItemSilent(
            GastoOperacionalCategoryMaintenance.TrasladoBusConcepto,
            GastoOperacionalCategoryMaintenance.TrasladoBusUnidad);
        return true;
    }

    public void OnGoChanged()
    {
        UpdateGoTotals();
        PersistGo(markDirty: true);
    }

    public void UpdateGoTotals()
    {
        var totalGoClp = _vm.GastosOperacionales.Sum(r => r.MontoClp);
        foreach (var cat in _vm.GastosOperacionales)
            cat.Pct = totalGoClp > 0 ? cat.MontoClp / totalGoClp * 100m : 0m;

        _vm.TotalGoLabel = _vm.FormatMoneyPrimary(totalGoClp);
        _vm.TotalGoUfLabel = _vm.FormatMoneyPair(totalGoClp).Secondary;
        _vm.TotalGoUfSubLabel = _vm.FormatMoneyPair(totalGoClp).Secondary;
        _vm.GoCategoriaCountLabel = $"{_vm.GastosOperacionales.Count} categorías";
        _vm.GoItemCountLabel = _vm.GastosOperacionales.Sum(c => c.Items.Count).ToString();
        _vm.GoComposeHint = totalGoClp > 0 ? "por categoría" : "sin montos aún";
        _vm.GoFooterSubLabel = totalGoClp > 0
            ? "Incluye todas las categorías"
            : "Listo para cargar números reales";

        RebuildComposeBar(totalGoClp);
    }

    public void RebuildComposeBar(decimal totalGoClp)
    {
        _vm.GoComposeSegments.Clear();
        _vm.GoComposeLegend.Clear();

        var shares = _vm.GastosOperacionales
            .Select(c => new CostoGoCategoryShare(c.Codigo, c.MontoClp, c.Pct))
            .ToList();
        var projection = CostoGoComposeProjectionBuilder.Build(shares, totalGoClp);

        foreach (var seg in projection.Segments)
        {
            _vm.GoComposeSegments.Add(new GoComposeSegmentVm
            {
                Fill = CostoUiBrushes.Parse(GoPalette[seg.PaletteIndex % GoPalette.Length], seg.FillOpacity),
                WidthUnits = seg.WidthUnits,
            });
        }

        foreach (var item in projection.Legend)
        {
            _vm.GoComposeLegend.Add(new GoComposeLegendVm
            {
                Fill = CostoUiBrushes.Parse(GoPalette[item.PaletteIndex % GoPalette.Length]),
                Codigo = item.Codigo,
                PctLabel = item.PctLabel,
            });
        }
    }

    public void PersistGo(bool markDirty)
    {
        if (!_vm.CanEdit || _vm.ShowEmptyState)
            return;

        _vm.Session.Data.GastosOperacionales = _vm.GastosOperacionales.Select(c => c.ToRow()).ToList();
        if (!markDirty)
            return;

        _vm.SuppressGoRefresh = true;
        try
        {
            _vm.Session.MarkDirty(ProjectDirtyScope.GastosOperacionales);
        }
        finally
        {
            _vm.SuppressGoRefresh = false;
        }
    }

    public void ToggleGoCategoria(GastoOpCategoriaVm? cat) => cat?.ToggleExpanded();

    public void OpenViaticos(GastoOpCategoriaVm? cat)
    {
        if (!_vm.CanEdit || cat is not { IsViaticosCategory: true })
            return;

        var data = _vm.Session.Data;
        var picoDir = data.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? _vm.TotalMoDirectaDotacion;
        var rolesInd = data.MoIndirecta?.Count(r => r.Dotacion > 0) ?? _vm.MoIndirectaRows.Count(r => r.Dotacion > 0);
        if (rolesInd <= 0)
            rolesInd = data.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? _vm.MoIndirectaRows.Sum(r => r.Dotacion);
        var mesesObra = data.MoDirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        if (mesesObra <= 0)
            mesesObra = data.MoIndirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        if (mesesObra <= 0)
            mesesObra = 12;

        var vm = new ViaticosProfesionalViewModel(_vm.CurrentValorUf, picoDir, rolesInd, mesesObra, ocultarEquivalenciaUf: !_vm.ShowsUfFigures);
        vm.SyncFromMoRequested = () =>
        {
            var d = _vm.Session.Data;
            var p = d.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
            var ind = d.MoIndirecta?.Count(r => r.Dotacion > 0) ?? 0;
            if (ind <= 0)
                ind = d.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
            var m = d.MoDirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 12;
            if (m <= 0) m = 12;
            vm.ApplyMoSnapshot(p, ind, m, _vm.CurrentValorUf);
        };

        var owner = Application.Current?.MainWindow;
        var dialog = new ViaticosProfesionalDialog(vm);
        if (owner != null)
            dialog.Owner = owner;

        if (dialog.ShowDialog() != true)
            return;

        var bus = cat.Items.FirstOrDefault(i =>
            i.Concepto.Contains("bus", StringComparison.OrdinalIgnoreCase)
            || i.Concepto.Contains("Arriendo / traslado", StringComparison.OrdinalIgnoreCase));
        var preserveBus = bus?.ToRow();
        cat.ReplaceItems(vm.ResultItems, preserveBus);
    }

    public void AddGoItem(GastoOpCategoriaVm? cat)
    {
        if (!_vm.CanEdit || cat == null)
            return;
        cat.AddBlankItem();
    }

    public void RemoveGoItem(GastoOpItemVm? item)
    {
        if (!_vm.CanEdit || item == null)
            return;
        var cat = _vm.GastosOperacionales.FirstOrDefault(c => c.Items.Contains(item));
        cat?.RemoveItem(item);
    }

    public void AddGoCategoria()
    {
        if (!_vm.CanEdit)
            return;
        var n = _vm.GastosOperacionales.Count + 1;
        var row = new GastoOperacionalRow
        {
            Codigo = $"3.{n}",
            Concepto = "Nueva categoría",
            Items =
            [
                new GastoOperacionalItemRow { Concepto = "Nuevo ítem", Unidad = "un", Cantidad = 0, PrecioUnitarioClp = 0 }
            ],
        };
        var vm = new GastoOpCategoriaVm(row, _vm.CurrentValorUf, OnGoChanged, canEdit: () => _vm.CanEdit);
        _vm.GastosOperacionales.Add(vm);
        OnGoChanged();
    }

    public void RemoveGoCategoria(GastoOpCategoriaVm? cat)
    {
        if (!_vm.CanEdit || cat == null)
            return;
        if (!_vm.GastosOperacionales.Remove(cat))
            return;
        OnGoChanged();
    }

    public void ResetGoPlantilla()
    {
        if (!_vm.CanEdit)
            return;
        _vm.GastosOperacionales.Clear();
        foreach (var r in GastoOperacionalPlantilla.CreateClean())
            _vm.GastosOperacionales.Add(new GastoOpCategoriaVm(r, _vm.CurrentValorUf, OnGoChanged, canEdit: () => _vm.CanEdit));
        OnGoChanged();
    }
}
