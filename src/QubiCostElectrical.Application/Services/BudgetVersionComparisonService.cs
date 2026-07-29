using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record BudgetVersionComparisonRow(
    string ItemId,
    string Item,
    string Descripcion,
    decimal BaselineUf,
    decimal CurrentUf,
    decimal DeltaUf,
    decimal DeltaPct,
    string Estado)
{
    public bool EsTitulo { get; init; }
    public int NivelJerarquico { get; init; }
    public decimal BaselineQty { get; init; }
    public decimal CurrentQty { get; init; }
    public decimal BaselinePriceUf { get; init; }
    public decimal CurrentPriceUf { get; init; }
    public bool CambióCantidad { get; init; }
    public bool CambióPrecio { get; init; }
    public bool CambióMateriales { get; init; }
    public bool CambióManoObra { get; init; }
    public bool CambióEquipos { get; init; }
    public string DetalleCambios { get; init; } = "";

    public decimal BaselineTotalUf => BaselineUf;
    public decimal CurrentTotalUf => CurrentUf;
}

public sealed class BudgetVersionComparisonSummary
{
    public string BaselineLabel { get; init; } = "Versión base";
    public string CurrentLabel { get; init; } = "Estado actual";
    public decimal BaselineUf { get; init; }
    public decimal CurrentUf { get; init; }
    public decimal DeltaUf => CurrentUf - BaselineUf;
    public decimal DeltaPct => BaselineUf == 0m ? (CurrentUf == 0m ? 0m : 100m) : (DeltaUf / BaselineUf) * 100m;
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ChangedCount { get; init; }
    public IReadOnlyList<BudgetVersionComparisonRow> Rows { get; init; } = Array.Empty<BudgetVersionComparisonRow>();

    public decimal CostoAgregadoUf { get; init; }
    public decimal CostoEliminadoUf { get; init; }
}

public sealed class BudgetVersionComparisonService : IVersionComparisonService
{
    public BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        ProjectFileData current,
        string baselineLabel = "Versión base",
        string currentLabel = "Estado actual")
    {
        baseline ??= new BudgetVersionData();
        current ??= new ProjectFileData();

        var baseItems = (baseline.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var curItems = (current.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var baseItemsByWbs = new Dictionary<string, ItemizadoRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in baseline.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
        {
            if (!string.IsNullOrWhiteSpace(item.Item))
                baseItemsByWbs[item.Item] = item;
        }

        var curItemsByWbs = new Dictionary<string, ItemizadoRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in current.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
        {
            if (!string.IsNullOrWhiteSpace(item.Item))
                curItemsByWbs[item.Item] = item;
        }

        var baseApus = (baseline.Apus ?? Enumerable.Empty<ApuRow>())
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var curApus = (current.Apus ?? Enumerable.Empty<ApuRow>())
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var baseCubsByItem = (baseline.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var curCubsByItem = (current.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in baseline.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
            allKeys.Add(item.Id);
        foreach (var item in current.Itemizado ?? Enumerable.Empty<ItemizadoRow>())
            allKeys.Add(item.Id);

        var rowsList = new List<BudgetVersionComparisonRow>();
        int added = 0;
        int removed = 0;
        int changed = 0;
        decimal costoAgregado = 0m;
        decimal costoEliminado = 0m;

        foreach (var id in allKeys)
        {
            curItems.TryGetValue(id, out var curItem);
            baseItems.TryGetValue(id, out var baseItem);

            if (curItem == null && baseItem != null && !string.IsNullOrWhiteSpace(baseItem.Item))
            {
                if (curItemsByWbs.TryGetValue(baseItem.Item, out var matchedCur))
                    curItem = matchedCur;
            }
            if (baseItem == null && curItem != null && !string.IsNullOrWhiteSpace(curItem.Item))
            {
                if (baseItemsByWbs.TryGetValue(curItem.Item, out var matchedBase))
                    baseItem = matchedBase;
            }

            if (curItem == null && baseItem == null)
                continue;

            string itemCode = curItem?.Item ?? baseItem?.Item ?? "";
            string descripcion = curItem?.Descripcion ?? baseItem?.Descripcion ?? "Sin descripción";
            bool esTitulo = curItem?.EsTitulo ?? baseItem?.EsTitulo ?? false;
            int nivel = curItem?.NivelJerarquico ?? baseItem?.NivelJerarquico ?? 0;

            decimal baseQty = esTitulo ? 0m : (baseItem?.Cantidad ?? 0m);
            decimal curQty = esTitulo ? 0m : (curItem?.Cantidad ?? 0m);

            baseApus.TryGetValue(baseItem?.Id ?? "", out var baseApu);
            curApus.TryGetValue(curItem?.Id ?? "", out var curApu);

            decimal basePrice = esTitulo ? 0m : (baseApu?.PrecioUnitUf ?? 0m);
            decimal curPrice = esTitulo ? 0m : (curApu?.PrecioUnitUf ?? 0m);

            decimal baseTotal = 0m;
            if (!esTitulo)
            {
                if (baseApu != null && baseQty > 0m)
                    baseTotal = baseQty * basePrice;
                else if (baseItem != null && baseCubsByItem.TryGetValue(baseItem.Id, out var baseCubs))
                    baseTotal = baseCubs.Sum(x => x.UfTotal);
            }

            decimal curTotal = 0m;
            if (!esTitulo)
            {
                if (curApu != null && curQty > 0m)
                    curTotal = curQty * curPrice;
                else if (curItem != null && curCubsByItem.TryGetValue(curItem.Id, out var curCubs))
                    curTotal = curCubs.Sum(x => x.UfTotal);
            }

            decimal deltaTotal = curTotal - baseTotal;
            decimal deltaPct = baseTotal == 0m ? (curTotal == 0m ? 0m : 100m) : (deltaTotal / baseTotal) * 100m;

            string estado = "Sin cambio";
            bool cambioCantidad = false;
            bool cambioPrecio = false;
            bool cambioMateriales = false;
            bool cambioManoObra = false;
            bool cambioEquipos = false;

            if (baseItem == null && curItem != null)
            {
                estado = "Nuevo";
                added++;
                costoAgregado += curTotal;
            }
            else if (baseItem != null && curItem == null)
            {
                estado = "Eliminado";
                removed++;
                costoEliminado += baseTotal;
            }
            else
            {
                cambioCantidad = baseQty != curQty;
                cambioPrecio = basePrice != curPrice;

                if (baseApu != null && curApu != null)
                {
                    cambioMateriales = baseApu.CostoMaterialUf != curApu.CostoMaterialUf ||
                                       !AreResourcesEqual(baseApu.Recursos, curApu.Recursos, ApuTipoRecurso.MAT);
                    cambioManoObra = baseApu.CostoMoUf != curApu.CostoMoUf ||
                                     baseApu.CostoHhUf != curApu.CostoHhUf ||
                                     !AreResourcesEqual(baseApu.Recursos, curApu.Recursos, ApuTipoRecurso.MO, ApuTipoRecurso.HH, ApuTipoRecurso.SC);
                    cambioEquipos = baseApu.CostoEquipoUf != curApu.CostoEquipoUf ||
                                    !AreResourcesEqual(baseApu.Recursos, curApu.Recursos, ApuTipoRecurso.EQ, ApuTipoRecurso.HERR);
                }

                bool cambioTotal = baseTotal != curTotal;

                if (cambioCantidad || cambioPrecio || cambioMateriales || cambioManoObra || cambioEquipos || cambioTotal || (baseItem?.Descripcion != curItem?.Descripcion))
                {
                    estado = deltaTotal > 0m ? "Aumentó" : (deltaTotal < 0m ? "Disminuyó" : "Modificado");
                    changed++;
                    if (deltaTotal > 0m)
                        costoAgregado += deltaTotal;
                    else
                        costoEliminado += Math.Abs(deltaTotal);
                }
            }

            var changesList = new List<string>();
            if (cambioCantidad) changesList.Add("Cantidad");
            if (cambioPrecio) changesList.Add("Precio");
            if (cambioMateriales) changesList.Add("Materiales");
            if (cambioManoObra) changesList.Add("Mano de Obra");
            if (cambioEquipos) changesList.Add("Equipos");
            string detalle = string.Join(", ", changesList);

            rowsList.Add(new BudgetVersionComparisonRow(
                id,
                itemCode,
                descripcion,
                baseTotal,
                curTotal,
                deltaTotal,
                deltaPct,
                estado)
            {
                EsTitulo = esTitulo,
                NivelJerarquico = nivel,
                BaselineQty = baseQty,
                CurrentQty = curQty,
                BaselinePriceUf = basePrice,
                CurrentPriceUf = curPrice,
                CambióCantidad = cambioCantidad,
                CambióPrecio = cambioPrecio,
                CambióMateriales = cambioMateriales,
                CambióManoObra = cambioManoObra,
                CambióEquipos = cambioEquipos,
                DetalleCambios = detalle
            });
        }

        rowsList = rowsList.OrderBy(x => x.Item, new WbsComparer()).ToList();

        for (int i = 0; i < rowsList.Count; i++)
        {
            if (rowsList[i].EsTitulo)
            {
                decimal tBase = 0m;
                decimal tCur = 0m;

                string prefix = rowsList[i].Item + ".";
                for (int j = i + 1; j < rowsList.Count; j++)
                {
                    var child = rowsList[j];
                    if (!child.EsTitulo)
                    {
                        if (child.Item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            tBase += child.BaselineTotalUf;
                            tCur += child.CurrentTotalUf;
                        }
                    }
                }

                decimal dTotal = tCur - tBase;
                decimal dPct = tBase == 0m ? (tCur == 0m ? 0m : 100m) : (dTotal / tBase) * 100m;

                string state = "Sin cambio";
                if (tBase == 0m && tCur > 0m) state = "Nuevo";
                else if (tBase > 0m && tCur == 0m) state = "Eliminado";
                else if (dTotal != 0m) state = "Modificado";

                rowsList[i] = rowsList[i] with
                {
                    BaselineUf = tBase,
                    CurrentUf = tCur,
                    DeltaUf = dTotal,
                    DeltaPct = dPct,
                    Estado = state
                };
            }
        }

        return new BudgetVersionComparisonSummary
        {
            BaselineLabel = baselineLabel,
            CurrentLabel = currentLabel,
            BaselineUf = rowsList.Where(x => !x.EsTitulo).Sum(x => x.BaselineTotalUf),
            CurrentUf = rowsList.Where(x => !x.EsTitulo).Sum(x => x.CurrentTotalUf),
            AddedCount = added,
            RemovedCount = removed,
            ChangedCount = changed,
            CostoAgregadoUf = costoAgregado,
            CostoEliminadoUf = costoEliminado,
            Rows = rowsList
        };
    }

    public BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        BudgetVersionData current,
        string baselineLabel,
        string currentLabel)
    {
        return Compare(baseline, ToProjectFileData(current), baselineLabel, currentLabel);
    }

    private static ProjectFileData ToProjectFileData(BudgetVersionData version)
    {
        version ??= new BudgetVersionData();
        return new ProjectFileData
        {
            Proyecto = version.Proyecto,
            Itemizado = version.Itemizado,
            Materiales = version.Materiales,
            Cubicacion = version.Cubicacion,
            Plantillas = version.Plantillas,
            ConjuntosElectricos = version.ConjuntosElectricos,
            CubicacionRecursos = version.CubicacionRecursos,
            CubicacionZonaCantidades = version.CubicacionZonaCantidades,
            CubicacionValorizaciones = version.CubicacionValorizaciones,
            PartidaApuVinculos = version.PartidaApuVinculos,
            QtyHeadersByItem = version.QtyHeadersByItem,
            Zonas = version.Zonas,
            Mediciones = version.Mediciones,
            Apus = version.Apus,
            OrdenesCambio = version.OrdenesCambio,
            CubicacionSC = version.CubicacionSC,
            CubicacionScConfig = version.CubicacionScConfig
        };
    }

    private static bool AreResourcesEqual(List<ApuRecursoRow> baseRecs, List<ApuRecursoRow> curRecs, params ApuTipoRecurso[] tipos)
    {
        var bList = (baseRecs ?? new List<ApuRecursoRow>()).Where(r => tipos.Contains(r.Tipo)).OrderBy(r => r.Codigo).ToList();
        var cList = (curRecs ?? new List<ApuRecursoRow>()).Where(r => tipos.Contains(r.Tipo)).OrderBy(r => r.Codigo).ToList();

        if (bList.Count != cList.Count) return false;
        for (int i = 0; i < bList.Count; i++)
        {
            if (bList[i].Codigo != cList[i].Codigo ||
                bList[i].Rendimiento != cList[i].Rendimiento ||
                bList[i].PrecioBaseUf != cList[i].PrecioBaseUf)
                return false;
        }
        return true;
    }

    private sealed class WbsComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (string.IsNullOrEmpty(x)) return -1;
            if (string.IsNullOrEmpty(y)) return 1;

            var partsX = x.Split('.');
            var partsY = y.Split('.');

            for (int i = 0; i < Math.Min(partsX.Length, partsY.Length); i++)
            {
                bool isNumX = int.TryParse(partsX[i], out int numX);
                bool isNumY = int.TryParse(partsY[i], out int numY);

                if (isNumX && isNumY)
                {
                    if (numX != numY) return numX.CompareTo(numY);
                }
                else
                {
                    int comp = string.Compare(partsX[i], partsY[i], StringComparison.OrdinalIgnoreCase);
                    if (comp != 0) return comp;
                }
            }
            return partsX.Length.CompareTo(partsY.Length);
        }
    }
}
