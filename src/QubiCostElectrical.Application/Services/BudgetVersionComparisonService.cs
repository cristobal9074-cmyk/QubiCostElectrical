using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum RevisionDiffKind
{
    SinCambio = 0,
    Adicional = 1,
    Deductivo = 2,
    Modificado = 3
}

public sealed record BudgetVersionResourceDiffRow(
    string Codigo,
    string Descripcion,
    string Unidad,
    decimal CantidadBase,
    decimal CantidadActual,
    decimal DeltaCantidad,
    decimal UfUnitario,
    decimal DeltaTotalUf,
    RevisionDiffKind DiffKind);

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
    public string Unidad { get; init; } = "GL";
    public decimal CantidadBase { get; init; }
    public decimal CantidadActual { get; init; }
    public decimal DeltaCantidad => CantidadActual - CantidadBase;
    public decimal PuUfBase { get; init; }
    public decimal PuUfActual { get; init; }
    public decimal DeltaPuUf => PuUfActual - PuUfBase;
    public decimal BaselineClp { get; init; }
    public decimal CurrentClp { get; init; }
    public decimal DeltaClp => CurrentClp - BaselineClp;
    public RevisionDiffKind DiffKind { get; init; } = RevisionDiffKind.SinCambio;
    public string DisplayBadge { get; init; } = "SIN CAMBIO";
    public string DetalleCambio { get; init; } = "";
    public IReadOnlyList<BudgetVersionResourceDiffRow> ResourceDiffs { get; init; } = Array.Empty<BudgetVersionResourceDiffRow>();
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
    public int UnchangedCount { get; init; }
    public decimal ValorUf { get; init; } = PriceConversion.DefaultValorUfClp;
    public decimal BaselineClp => Math.Round(BaselineUf * ValorUf, 0, MidpointRounding.AwayFromZero);
    public decimal CurrentClp => Math.Round(CurrentUf * ValorUf, 0, MidpointRounding.AwayFromZero);
    public decimal DeltaClp => CurrentClp - BaselineClp;
    public decimal TotalAdicionalesUf { get; init; }
    public decimal TotalDeductivosUf { get; init; }
    public decimal TotalAdicionalesClp => Math.Round(TotalAdicionalesUf * ValorUf, 0, MidpointRounding.AwayFromZero);
    public decimal TotalDeductivosClp => Math.Round(TotalDeductivosUf * ValorUf, 0, MidpointRounding.AwayFromZero);
    public decimal NetVariationUf => DeltaUf;
    public decimal NetVariationClp => DeltaClp;
    public string ProjectTitle { get; init; } = "Proyecto";
    public string ClientName { get; init; } = "";
    public string ContractorName { get; init; } = "";
    public DateTime FechaComparacion { get; init; } = AppClock.Now;
    public IReadOnlyList<BudgetVersionComparisonRow> Rows { get; init; } = Array.Empty<BudgetVersionComparisonRow>();
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

        decimal valorUf = current.Proyecto?.ValorUfClp > 0m
            ? current.Proyecto.ValorUfClp
            : (baseline.Proyecto?.ValorUfClp > 0m ? baseline.Proyecto.ValorUfClp : PriceConversion.DefaultValorUfClp);

        string projectTitle = !string.IsNullOrWhiteSpace(current.Proyecto?.Nombre)
            ? current.Proyecto.Nombre
            : (!string.IsNullOrWhiteSpace(baseline.Proyecto?.Nombre) ? baseline.Proyecto.Nombre : "Proyecto Eléctrico");

        string clientName = !string.IsNullOrWhiteSpace(current.Proyecto?.Cliente)
            ? current.Proyecto.Cliente
            : (!string.IsNullOrWhiteSpace(baseline.Proyecto?.Cliente) ? baseline.Proyecto.Cliente : "Constructora / Mandante");

        string contractorName = !string.IsNullOrWhiteSpace(current.Proyecto?.UsuarioNombre)
            ? current.Proyecto.UsuarioNombre
            : (!string.IsNullOrWhiteSpace(baseline.Proyecto?.UsuarioNombre) ? baseline.Proyecto.UsuarioNombre : "Contratista Eléctrico");

        var baselineRows = BuildDetailedRows(baseline.Itemizado, baseline.Cubicacion, valorUf);
        var currentRows = BuildDetailedRows(current.Itemizado, current.Cubicacion, valorUf);

        var keys = new HashSet<string>(baselineRows.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in currentRows.Keys)
            keys.Add(key);

        var rows = new List<BudgetVersionComparisonRow>();
        int added = 0;
        int removed = 0;
        int changed = 0;
        int unchanged = 0;

        foreach (string key in keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            baselineRows.TryGetValue(key, out var baseItem);
            currentRows.TryGetValue(key, out var curItem);

            decimal baselineUf = baseItem?.TotalUf ?? 0m;
            decimal currentUf = curItem?.TotalUf ?? 0m;
            decimal deltaUf = currentUf - baselineUf;
            decimal deltaPct = baselineUf == 0m
                ? (currentUf == 0m ? 0m : 100m)
                : deltaUf / baselineUf * 100m;

            string itemCode = curItem?.Item ?? baseItem?.Item ?? key;
            string descripcion = curItem?.Descripcion ?? baseItem?.Descripcion ?? "Sin descripción";
            string unidad = curItem?.Unidad ?? baseItem?.Unidad ?? "GL";

            decimal qBase = baseItem?.Cantidad ?? 0m;
            decimal qCur = curItem?.Cantidad ?? 0m;
            decimal deltaQ = qCur - qBase;

            decimal puBase = baseItem?.PuUf ?? 0m;
            decimal puCur = curItem?.PuUf ?? 0m;
            decimal deltaPu = puCur - puBase;

            decimal clpBase = baseItem?.TotalClp ?? Math.Round(baselineUf * valorUf, 0, MidpointRounding.AwayFromZero);
            decimal clpCur = curItem?.TotalClp ?? Math.Round(currentUf * valorUf, 0, MidpointRounding.AwayFromZero);

            // Diff de recursos/materiales de la partida
            var resourceDiffs = BuildResourceDiffs(baseItem?.Resources, curItem?.Resources);

            string estado = "Sin cambio";
            RevisionDiffKind diffKind = RevisionDiffKind.SinCambio;
            string displayBadge = "SIN CAMBIO";
            string detalleCambio = "Sin variaciones cuantitativas ni económicas";

            if (baselineUf == 0m && currentUf > 0m)
            {
                estado = "Nuevo";
                diffKind = RevisionDiffKind.Adicional;
                displayBadge = "+ ADICIONAL";
                detalleCambio = "Partida nueva incorporada en la revisión";
                added++;
            }
            else if (baselineUf > 0m && currentUf == 0m)
            {
                estado = "Eliminado";
                diffKind = RevisionDiffKind.Deductivo;
                displayBadge = "- DEDUCTIVO";
                detalleCambio = "Partida eliminada en la revisión";
                removed++;
            }
            else if (deltaUf != 0m || deltaQ != 0m || Math.Abs(deltaPu) >= 0.001m)
            {
                estado = deltaUf > 0m ? "Aumentó" : (deltaUf < 0m ? "Disminuyó" : "Sin cambio");
                diffKind = RevisionDiffKind.Modificado;

                if (deltaQ > 0m && Math.Abs(deltaPu) < 0.001m)
                {
                    displayBadge = "MAYOR METRAJE";
                    detalleCambio = $"+{deltaQ:G} {unidad} adicionales";
                }
                else if (deltaQ < 0m && Math.Abs(deltaPu) < 0.001m)
                {
                    displayBadge = "MENOR METRAJE";
                    detalleCambio = $"{deltaQ:G} {unidad} reducidos";
                }
                else if (deltaQ == 0m && Math.Abs(deltaPu) >= 0.001m)
                {
                    displayBadge = "CAMBIO P.U.";
                    detalleCambio = $"P.U. varió de {puBase:N3} a {puCur:N3} UF";
                }
                else
                {
                    displayBadge = deltaUf > 0m ? "AUMENTÓ" : "DISMINUYÓ";
                    detalleCambio = deltaQ != 0m
                        ? $"{deltaQ:+0.##;-0.##} {unidad} · Δ P.U. {deltaPu:+0.###;-0.###} UF"
                        : $"Δ P.U. {deltaPu:+0.###;-0.###} UF";
                }
                changed++;
            }
            else
            {
                unchanged++;
            }

            rows.Add(new BudgetVersionComparisonRow(
                key,
                itemCode,
                descripcion,
                baselineUf,
                currentUf,
                deltaUf,
                deltaPct,
                estado)
            {
                Unidad = unidad,
                CantidadBase = qBase,
                CantidadActual = qCur,
                PuUfBase = puBase,
                PuUfActual = puCur,
                BaselineClp = clpBase,
                CurrentClp = clpCur,
                DiffKind = diffKind,
                DisplayBadge = displayBadge,
                DetalleCambio = detalleCambio,
                ResourceDiffs = resourceDiffs
            });
        }

        decimal totalAdicionalesUf = rows.Where(r => r.DeltaUf > 0m).Sum(r => r.DeltaUf);
        decimal totalDeductivosUf = rows.Where(r => r.DeltaUf < 0m).Sum(r => r.DeltaUf);

        return new BudgetVersionComparisonSummary
        {
            BaselineLabel = baselineLabel,
            CurrentLabel = currentLabel,
            BaselineUf = rows.Sum(x => x.BaselineUf),
            CurrentUf = rows.Sum(x => x.CurrentUf),
            AddedCount = added,
            RemovedCount = removed,
            ChangedCount = changed,
            UnchangedCount = unchanged,
            ValorUf = valorUf,
            TotalAdicionalesUf = totalAdicionalesUf,
            TotalDeductivosUf = totalDeductivosUf,
            ProjectTitle = projectTitle,
            ClientName = clientName,
            ContractorName = contractorName,
            FechaComparacion = AppClock.Now,
            Rows = rows
                .OrderByDescending(x => Math.Abs(x.DeltaUf))
                .ThenBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
                .ToList()
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

    public BudgetVersionComparisonSummary CompareWithIncomingItemizado(
        IReadOnlyList<ItemizadoRow> incomingItems,
        ProjectFileData currentProject,
        string incomingLabel = "Aclaración / Addenda",
        string currentLabel = "Presupuesto Actual")
    {
        currentProject ??= new ProjectFileData();
        incomingItems ??= Array.Empty<ItemizadoRow>();

        decimal valorUf = currentProject.Proyecto?.ValorUfClp > 0m
            ? currentProject.Proyecto.ValorUfClp
            : PriceConversion.DefaultValorUfClp;

        string projectTitle = !string.IsNullOrWhiteSpace(currentProject.Proyecto?.Nombre)
            ? currentProject.Proyecto.Nombre
            : "Proyecto Eléctrico";

        string clientName = !string.IsNullOrWhiteSpace(currentProject.Proyecto?.Cliente)
            ? currentProject.Proyecto.Cliente
            : "Constructora / Mandante";

        string contractorName = !string.IsNullOrWhiteSpace(currentProject.Proyecto?.UsuarioNombre)
            ? currentProject.Proyecto.UsuarioNombre
            : "Contratista Eléctrico";

        // 1. Filas detalladas del proyecto actual (Presupuesto Base)
        var baseDetailed = BuildDetailedRows(currentProject.Itemizado, currentProject.Cubicacion, valorUf);

        // Mapeo auxiliar por Item (código de partida) y Descripción para matching flexible
        var baseByItem = new Dictionary<string, DetailedItemRow>(StringComparer.OrdinalIgnoreCase);
        var baseByDesc = new Dictionary<string, DetailedItemRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in baseDetailed.Values)
        {
            if (!string.IsNullOrWhiteSpace(row.Item))
                baseByItem[row.Item.Trim()] = row;

            string normDesc = NormalizeText(row.Descripcion);
            if (!string.IsNullOrWhiteSpace(normDesc) && !baseByDesc.ContainsKey(normDesc))
                baseByDesc[normDesc] = row;
        }

        // 2. Filas de la Addenda entrante
        var incomingDetailed = new Dictionary<string, DetailedItemRow>(StringComparer.OrdinalIgnoreCase);
        int extraItemIndex = 1;

        foreach (var inc in incomingItems)
        {
            if (inc.EsTitulo) continue;

            string itemCode = inc.Item?.Trim() ?? string.Empty;
            string desc = inc.Descripcion?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(desc))
                continue;

            decimal qty = inc.Cantidad > 0m ? inc.Cantidad : (inc.CantidadOriginal > 0m ? inc.CantidadOriginal : 1m);
            string unidad = !string.IsNullOrWhiteSpace(inc.Unidad) ? inc.Unidad : "GL";

            // Matching inteligente: primero por código de partida (ej. "1.1"), luego por descripción normalizada
            DetailedItemRow? matchedBase = null;
            if (!string.IsNullOrWhiteSpace(itemCode) && baseByItem.TryGetValue(itemCode, out var byCode))
            {
                matchedBase = byCode;
            }
            else
            {
                string norm = NormalizeText(desc);
                if (!string.IsNullOrWhiteSpace(norm) && baseByDesc.TryGetValue(norm, out var byDesc))
                {
                    matchedBase = byDesc;
                }
            }

            string key;
            decimal puUf = 0m;
            IReadOnlyList<CubicacionRow> resources = Array.Empty<CubicacionRow>();

            if (matchedBase != null)
            {
                key = matchedBase.ItemId;
                puUf = matchedBase.PuUf;
                resources = matchedBase.Resources;
                if (string.IsNullOrWhiteSpace(unidad)) unidad = matchedBase.Unidad;
            }
            else
            {
                key = !string.IsNullOrWhiteSpace(itemCode) ? $"ADD_{itemCode}" : $"ADD_ROW_{extraItemIndex++}";
            }

            decimal totalUf = qty * puUf;
            decimal totalClp = Math.Round(totalUf * valorUf, 0, MidpointRounding.AwayFromZero);

            incomingDetailed[key] = new DetailedItemRow(
                key,
                itemCode,
                desc,
                unidad,
                qty,
                totalUf,
                totalClp,
                resources);
        }

        // 3. Comparar baseDetailed (Baseline) vs incomingDetailed (Current/Addenda)
        var allKeys = new HashSet<string>(baseDetailed.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in incomingDetailed.Keys)
            allKeys.Add(k);

        var rows = new List<BudgetVersionComparisonRow>();
        int added = 0;
        int removed = 0;
        int changed = 0;
        int unchanged = 0;

        foreach (string key in allKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            baseDetailed.TryGetValue(key, out var bItem);
            incomingDetailed.TryGetValue(key, out var incItem);

            decimal baselineUf = bItem?.TotalUf ?? 0m;
            decimal currentUf = incItem?.TotalUf ?? 0m;
            decimal deltaUf = currentUf - baselineUf;
            decimal deltaPct = baselineUf == 0m
                ? (currentUf == 0m ? 0m : 100m)
                : deltaUf / baselineUf * 100m;

            string itemCode = incItem?.Item ?? bItem?.Item ?? key;
            string descripcion = incItem?.Descripcion ?? bItem?.Descripcion ?? "Sin descripción";
            string unidad = incItem?.Unidad ?? bItem?.Unidad ?? "GL";

            decimal qBase = bItem?.Cantidad ?? 0m;
            decimal qCur = incItem?.Cantidad ?? 0m;
            decimal deltaQ = qCur - qBase;

            decimal puBase = bItem?.PuUf ?? 0m;
            decimal puCur = incItem?.PuUf ?? puBase;
            decimal deltaPu = puCur - puBase;

            decimal clpBase = bItem?.TotalClp ?? Math.Round(baselineUf * valorUf, 0, MidpointRounding.AwayFromZero);
            decimal clpCur = incItem?.TotalClp ?? Math.Round(currentUf * valorUf, 0, MidpointRounding.AwayFromZero);

            string estado = "Sin cambio";
            RevisionDiffKind diffKind = RevisionDiffKind.SinCambio;
            string displayBadge = "SIN CAMBIO";
            string detalleCambio = "Sin variaciones cuantitativas";

            if (bItem == null && incItem != null)
            {
                estado = "Nuevo";
                diffKind = RevisionDiffKind.Adicional;
                displayBadge = "+ ADICIONAL";
                detalleCambio = $"Partida nueva incorporada en Addenda ({qCur:G} {unidad})";
                added++;
            }
            else if (bItem != null && incItem == null)
            {
                estado = "Eliminado";
                diffKind = RevisionDiffKind.Deductivo;
                displayBadge = "- DEDUCTIVO";
                detalleCambio = $"Partida eliminada en Addenda ({qBase:G} {unidad} omitidos)";
                removed++;
            }
            else if (deltaQ != 0m || deltaUf != 0m || Math.Abs(deltaPu) >= 0.001m)
            {
                estado = deltaQ > 0m ? "Mayor metraje" : (deltaQ < 0m ? "Menor metraje" : "Modificado");
                diffKind = RevisionDiffKind.Modificado;

                if (deltaQ > 0m)
                {
                    displayBadge = "MAYOR METRAJE";
                    detalleCambio = $"+{deltaQ:G} {unidad} adicionales ({qBase:G} → {qCur:G})";
                }
                else if (deltaQ < 0m)
                {
                    displayBadge = "MENOR METRAJE";
                    detalleCambio = $"{deltaQ:G} {unidad} reducidos ({qBase:G} → {qCur:G})";
                }
                else
                {
                    displayBadge = "MODIFICADO";
                    detalleCambio = "Variación de precio / alcance";
                }
                changed++;
            }
            else
            {
                unchanged++;
            }

            rows.Add(new BudgetVersionComparisonRow(
                key,
                itemCode,
                descripcion,
                baselineUf,
                currentUf,
                deltaUf,
                deltaPct,
                estado)
            {
                Unidad = unidad,
                CantidadBase = qBase,
                CantidadActual = qCur,
                PuUfBase = puBase,
                PuUfActual = puCur,
                BaselineClp = clpBase,
                CurrentClp = clpCur,
                DiffKind = diffKind,
                DisplayBadge = displayBadge,
                DetalleCambio = detalleCambio,
                ResourceDiffs = Array.Empty<BudgetVersionResourceDiffRow>()
            });
        }

        decimal totalAdicionalesUf = rows.Where(r => r.DeltaUf > 0m).Sum(r => r.DeltaUf);
        decimal totalDeductivosUf = rows.Where(r => r.DeltaUf < 0m).Sum(r => r.DeltaUf);

        return new BudgetVersionComparisonSummary
        {
            BaselineLabel = currentLabel,
            CurrentLabel = incomingLabel,
            BaselineUf = rows.Sum(x => x.BaselineUf),
            CurrentUf = rows.Sum(x => x.CurrentUf),
            AddedCount = added,
            RemovedCount = removed,
            ChangedCount = changed,
            UnchangedCount = unchanged,
            ValorUf = valorUf,
            TotalAdicionalesUf = totalAdicionalesUf,
            TotalDeductivosUf = totalDeductivosUf,
            ProjectTitle = projectTitle,
            ClientName = clientName,
            ContractorName = contractorName,
            FechaComparacion = AppClock.Now,
            Rows = rows
                .OrderByDescending(x => Math.Abs(x.DeltaUf))
                .ThenBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var chars = text.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
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

    private static Dictionary<string, DetailedItemRow> BuildDetailedRows(
        IReadOnlyList<ItemizadoRow> itemizado,
        IReadOnlyList<CubicacionRow> cubicacion,
        decimal valorUf)
    {
        var rows = new Dictionary<string, DetailedItemRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemizado ?? Array.Empty<ItemizadoRow>())
        {
            if (string.IsNullOrWhiteSpace(item.Id) || item.EsTitulo)
                continue;

            decimal qty = item.Cantidad > 0m ? item.Cantidad : (item.CantidadOriginal > 0m ? item.CantidadOriginal : 1m);
            rows[item.Id] = new DetailedItemRow(
                item.Id,
                item.Item ?? "",
                item.Descripcion ?? "",
                item.Unidad ?? "GL",
                qty,
                0m,
                0m,
                new List<CubicacionRow>());
        }

        foreach (var group in (cubicacion ?? Array.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            decimal totalUf = group.Sum(x => x.UfTotal);
            decimal totalClp = group.Sum(x => x.SubtotalClp > 0m ? x.SubtotalClp : Math.Round(x.UfTotal * valorUf, 0, MidpointRounding.AwayFromZero));

            rows.TryGetValue(group.Key, out var existing);
            decimal qty = existing?.Cantidad ?? (first.Cantidad > 0m ? first.Cantidad : 1m);
            if (qty <= 0m) qty = 1m;

            rows[group.Key] = new DetailedItemRow(
                group.Key,
                existing?.Item ?? string.Empty,
                existing?.Descripcion ?? first.Descripcion ?? "Sin descripción",
                existing?.Unidad ?? first.Unidad ?? "GL",
                qty,
                totalUf,
                totalClp,
                group.ToList());
        }

        return rows;
    }

    private static List<BudgetVersionResourceDiffRow> BuildResourceDiffs(
        IReadOnlyList<CubicacionRow>? baseRes,
        IReadOnlyList<CubicacionRow>? curRes)
    {
        baseRes ??= Array.Empty<CubicacionRow>();
        curRes ??= Array.Empty<CubicacionRow>();

        var baseMap = baseRes.GroupBy(r => !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo : r.Descripcion, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var curMap = curRes.GroupBy(r => !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo : r.Descripcion, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var keys = new HashSet<string>(baseMap.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in curMap.Keys) keys.Add(k);

        var diffs = new List<BudgetVersionResourceDiffRow>();
        foreach (var key in keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            baseMap.TryGetValue(key, out var bList);
            curMap.TryGetValue(key, out var cList);

            var first = cList?.FirstOrDefault() ?? bList?.FirstOrDefault();
            if (first == null) continue;

            decimal qB = bList?.Sum(x => x.Cantidad) ?? 0m;
            decimal qC = cList?.Sum(x => x.Cantidad) ?? 0m;
            decimal deltaQ = qC - qB;

            decimal ufB = bList?.Sum(x => x.UfTotal) ?? 0m;
            decimal ufC = cList?.Sum(x => x.UfTotal) ?? 0m;
            decimal deltaUf = ufC - ufB;

            decimal pu = cList?.FirstOrDefault()?.UfUnit > 0m
                ? cList.First().UfUnit
                : (bList?.FirstOrDefault()?.UfUnit ?? 0m);

            RevisionDiffKind kind = RevisionDiffKind.SinCambio;
            if (qB == 0m && qC > 0m) kind = RevisionDiffKind.Adicional;
            else if (qB > 0m && qC == 0m) kind = RevisionDiffKind.Deductivo;
            else if (deltaQ != 0m || deltaUf != 0m) kind = RevisionDiffKind.Modificado;

            diffs.Add(new BudgetVersionResourceDiffRow(
                first.Codigo,
                first.Descripcion,
                first.Unidad,
                qB,
                qC,
                deltaQ,
                pu,
                deltaUf,
                kind));
        }

        return diffs;
    }

    private sealed record DetailedItemRow(
        string ItemId,
        string Item,
        string Descripcion,
        string Unidad,
        decimal Cantidad,
        decimal TotalUf,
        decimal TotalClp,
        IReadOnlyList<CubicacionRow> Resources)
    {
        public decimal PuUf => Cantidad > 0m ? TotalUf / Cantidad : TotalUf;
    }
}
