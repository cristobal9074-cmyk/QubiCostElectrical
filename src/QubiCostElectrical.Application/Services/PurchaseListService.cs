using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class PurchaseListService : IPurchaseListService
{
    public IReadOnlyList<CompraMaterialViewModel> Build(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        decimal toleranciaPct)
    {
        return Build(cubicacion, materiales, customTolerances: null, useCategoryDefaults: false, fallbackToleranciaPct: toleranciaPct);
    }

    public IReadOnlyList<CompraMaterialViewModel> Build(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        IReadOnlyDictionary<string, decimal>? customTolerances,
        bool useCategoryDefaults,
        CategoryToleranceConfig? categoryConfig = null)
    {
        return Build(cubicacion, materiales, customTolerances, useCategoryDefaults, fallbackToleranciaPct: 5m, categoryConfig);
    }

    private IReadOnlyList<CompraMaterialViewModel> Build(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        IReadOnlyDictionary<string, decimal>? customTolerances,
        bool useCategoryDefaults,
        decimal fallbackToleranciaPct,
        CategoryToleranceConfig? categoryConfig = null)
    {
        var materialLookup = (materiales ?? Enumerable.Empty<MaterialRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => NormalizeCode(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(IsPurchasableMaterial)
            .GroupBy(x => ResolvePurchaseGroupKey(x, materialLookup), StringComparer.OrdinalIgnoreCase)
            .Select(g => BuildRow(g.Key, g.ToList(), materialLookup, customTolerances, useCategoryDefaults, fallbackToleranciaPct, categoryConfig))
            .Where(x => x.CantidadNeta != 0m)
            .OrderByDescending(x => x.TotalCompraUf)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CompraMaterialViewModel BuildRow(
        string codigoKey,
        IReadOnlyList<CubicacionRow> rows,
        IReadOnlyDictionary<string, MaterialRow> materialLookup,
        IReadOnlyDictionary<string, decimal>? customTolerances,
        bool useCategoryDefaults,
        decimal fallbackToleranciaPct,
        CategoryToleranceConfig? categoryConfig = null)
    {
        materialLookup.TryGetValue(codigoKey, out var material);
        material ??= rows
            .Select(x => materialLookup.TryGetValue(NormalizeCode(x.Codigo), out var rowMaterial) ? rowMaterial : null)
            .FirstOrDefault(x => x != null);

        string descripcion = material?.Descripcion
            ?? rows.Select(x => x.Descripcion).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "Sin descripción";
        string unidad = material?.Unidad
            ?? rows.Select(x => x.Unidad).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "";
        string categoria = material?.Categoria
            ?? rows.Select(x => x.Area).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "General";
        string proveedor = material?.Proveedor?.Trim() ?? "";

        decimal resolvedTol;
        if (customTolerances != null && customTolerances.TryGetValue(codigoKey, out var customVal))
        {
            resolvedTol = customVal;
        }
        else if (useCategoryDefaults)
        {
            resolvedTol = categoryConfig != null ? categoryConfig.Resolve(categoria, unidad, descripcion) : MaterialToleranceRules.ResolveTolerancePct(categoria, unidad, descripcion);
        }
        else
        {
            resolvedTol = fallbackToleranciaPct;
        }

        decimal safeTolerance = Math.Clamp(resolvedTol, 0m, 100m);
        decimal factor = 1m + safeTolerance / 100m;

        decimal cantidadNeta = rows.Sum(x => x.Cantidad);
        decimal totalNetoUf = rows.Sum(x => x.Cantidad * x.MatUf);
        decimal totalNetoClp = rows.Sum(x => x.Cantidad * x.MatClpConFallback);
        decimal cantidadCompra = cantidadNeta * factor;
        decimal matUfUnitario = cantidadNeta != 0m
            ? totalNetoUf / cantidadNeta
            : material?.MatUf ?? 0m;
        decimal matClpUnitario = cantidadNeta != 0m
            ? totalNetoClp / cantidadNeta
            : (material?.MatClp > 0m ? material.MatClp : 0m);

        if (matClpUnitario <= 0m && matUfUnitario > 0m)
        {
            matClpUnitario = PriceConversion.UfToClp(matUfUnitario, PriceConversion.DefaultValorUfClp);
            totalNetoClp = cantidadNeta * matClpUnitario;
        }

        decimal totalCompraUf = cantidadCompra * matUfUnitario;
        decimal totalCompraClp = cantidadCompra * matClpUnitario;

        return new CompraMaterialViewModel(
            codigoKey,
            descripcion,
            unidad,
            categoria,
            proveedor,
            cantidadNeta,
            safeTolerance,
            cantidadCompra,
            matUfUnitario,
            totalNetoUf,
            totalCompraUf,
            rows.Count,
            matClpUnitario,
            totalNetoClp,
            totalCompraClp);
    }

    private static string ResolvePurchaseGroupKey(CubicacionRow row, IReadOnlyDictionary<string, MaterialRow> materialLookup)
    {
        string code = NormalizeCode(row.Codigo);
        if (materialLookup.TryGetValue(code, out var material) && !string.IsNullOrWhiteSpace(material.EquivalenciaKey))
            return NormalizeCode(material.EquivalenciaKey);
        return code;
    }

    private static bool IsPurchasableMaterial(CubicacionRow row)
    {
        if (row == null) return false;
        if (string.IsNullOrWhiteSpace(row.Codigo)) return false;
        if (row.Cantidad == 0m) return false;

        string tipo = (row.Tipo ?? "MAT").Trim().ToUpperInvariant();
        if (tipo is "MO" or "MANO_OBRA" or "MANO DE OBRA" or "EQUIPO" or "HERRAMIENTA")
            return false;

        return true;
    }

    public IReadOnlyList<MaterialUsageBreakdown> GetMaterialUsage(
        string codigo,
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<ItemizadoRow> itemizado)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Array.Empty<MaterialUsageBreakdown>();

        string target = NormalizeCode(codigo);
        var matchingRows = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(IsPurchasableMaterial)
            .Where(x => string.Equals(NormalizeCode(x.Codigo), target, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingRows.Count == 0)
            return Array.Empty<MaterialUsageBreakdown>();

        decimal totalCantidad = matchingRows.Sum(x => x.Cantidad);

        var itemById = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return matchingRows
            .GroupBy(x => (ItemId: (x.ItemId ?? "").Trim(), Area: string.IsNullOrWhiteSpace(x.Area) ? "General" : x.Area.Trim()))
            .Select(g =>
            {
                itemById.TryGetValue(g.Key.ItemId, out var item);
                decimal sum = g.Sum(x => x.Cantidad);
                decimal pct = totalCantidad > 0m ? Math.Round((sum / totalCantidad) * 100m, 1) : 0m;
                var first = g.First();
                return new MaterialUsageBreakdown(
                    g.Key.ItemId,
                    item?.Item ?? "—",
                    item?.Descripcion ?? "Partida sin descripción",
                    g.Key.Area,
                    sum,
                    pct,
                    first.Codigo,
                    first.Descripcion);
            })
            .OrderByDescending(x => x.Cantidad)
            .ThenBy(x => x.ItemNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeCode(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }
}
