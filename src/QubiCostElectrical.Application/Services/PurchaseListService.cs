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
        decimal safeTolerance = Math.Clamp(toleranciaPct, 0m, 100m);
        decimal factor = 1m + safeTolerance / 100m;

        var materialLookup = (materiales ?? Enumerable.Empty<MaterialRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => NormalizeCode(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(IsPurchasableMaterial)
            .GroupBy(x => ResolvePurchaseGroupKey(x, materialLookup), StringComparer.OrdinalIgnoreCase)
            .Select(g => BuildRow(g.Key, g.ToList(), materialLookup, safeTolerance, factor))
            .Where(x => x.CantidadNeta != 0m)
            .OrderByDescending(x => x.TotalCompraUf)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CompraMaterialViewModel BuildRow(
        string codigoKey,
        IReadOnlyList<CubicacionRow> rows,
        IReadOnlyDictionary<string, MaterialRow> materialLookup,
        decimal toleranciaPct,
        decimal factor)
    {
        materialLookup.TryGetValue(codigoKey, out var material);
        material ??= rows
            .Select(x => materialLookup.TryGetValue(NormalizeCode(x.Codigo), out var rowMaterial) ? rowMaterial : null)
            .FirstOrDefault(x => x != null);

        decimal cantidadNeta = rows.Sum(x => x.Cantidad);
        decimal totalNetoUf = rows.Sum(x => x.Cantidad * x.MatUf);
        decimal cantidadCompra = cantidadNeta * factor;
        decimal matUfUnitario = cantidadNeta != 0m
            ? totalNetoUf / cantidadNeta
            : material?.MatUf ?? 0m;
        decimal totalCompraUf = cantidadCompra * matUfUnitario;

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

        return new CompraMaterialViewModel(
            codigoKey,
            descripcion,
            unidad,
            categoria,
            proveedor,
            cantidadNeta,
            toleranciaPct,
            cantidadCompra,
            matUfUnitario,
            totalNetoUf,
            totalCompraUf,
            rows.Count);
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

    private static string NormalizeCode(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }
}
