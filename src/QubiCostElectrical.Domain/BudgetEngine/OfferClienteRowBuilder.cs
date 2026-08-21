using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class OfferClienteRow
{
    public string Item { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal TotalUf { get; set; }
    public bool EsTitulo { get; set; }
    public string Observaciones { get; set; } = "";
}

/// <summary>
/// Construye filas de itemizado cliente para PDF/Excel usando el mismo desglose
/// que <see cref="BudgetEngine"/> (<see cref="CostBreakdown.Origenes"/>), respetando ApuMode.
/// </summary>
public static class OfferClienteRowBuilder
{
    public static List<OfferClienteRow> Build(
        IEnumerable<ItemizadoRow>? itemizado,
        CostBreakdown cost,
        decimal ventaNetaUf,
        string fallbackProjectTitle = "Proyecto")
    {
        cost ??= new CostBreakdown();
        var items = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();

        decimal ventaNeta = ventaNetaUf > 0m
            ? ventaNetaUf
            : (cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf : cost.TotalVentaUf);

        var directByItem = (cost.Origenes ?? new List<CostOriginBreakdown>())
            .Where(o => !string.IsNullOrWhiteSpace(o.ItemId))
            .GroupBy(o => o.ItemId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalUf), StringComparer.OrdinalIgnoreCase);

        decimal baseTotal = items
            .Where(x => !x.EsTitulo)
            .Sum(x => directByItem.TryGetValue(x.Id, out decimal v) ? v : 0m);

        if (baseTotal <= 0m)
            baseTotal = Math.Max(1m, cost.CostoDirectoUf);

        decimal factor = ventaNeta > 0m && baseTotal > 0m ? ventaNeta / baseTotal : 1m;

        var rows = new List<OfferClienteRow>();
        foreach (var item in items)
        {
            if (item.EsTitulo)
            {
                rows.Add(new OfferClienteRow
                {
                    Item = item.Item,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    Cantidad = item.Cantidad,
                    EsTitulo = true
                });
                continue;
            }

            directByItem.TryGetValue(item.Id, out decimal baseUf);
            rows.Add(new OfferClienteRow
            {
                Item = item.Item,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                Cantidad = item.Cantidad,
                TotalUf = baseUf * factor,
                Observaciones = item.Observaciones ?? ""
            });
        }

        if (rows.Count == 0)
        {
            rows.Add(new OfferClienteRow
            {
                Item = "1",
                Descripcion = fallbackProjectTitle,
                Unidad = "GL",
                Cantidad = 1m,
                TotalUf = ventaNeta
            });
        }

        return rows;
    }
}
