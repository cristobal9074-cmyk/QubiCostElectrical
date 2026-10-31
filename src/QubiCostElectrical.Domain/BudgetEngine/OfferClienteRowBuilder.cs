using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class OfferClienteRow
{
    public string Id { get; set; } = "";
    public string Item { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal TotalUf { get; set; }
    public bool EsTitulo { get; set; }
    public string Observaciones { get; set; } = "";
    public decimal PrecioUnitarioUf => Cantidad > 0m ? Money.Round(TotalUf / Cantidad) : 0m;
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
        ventaNeta = Math.Max(0m, ventaNeta);

        // C5: prorratear por la VENTA real por partida (origin.VentaClp), no por el costo directo.
        // La venta por línea ya incluye el factor de presentación de esa familia, así que una
        // partida intensiva en equipos con su propio valor agregado queda bien valorada.
        var ventaByItem = (cost.Origenes ?? new List<CostOriginBreakdown>())
            .Where(o => !string.IsNullOrWhiteSpace(o.ItemId))
            .GroupBy(o => o.ItemId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.VentaClp), StringComparer.OrdinalIgnoreCase);

        // Fallback: si ningún origen aporta venta (proyectos legacy), repartir por costo directo.
        if (ventaByItem.Count == 0 || ventaByItem.Values.All(v => v <= 0m))
        {
            ventaByItem = (cost.Origenes ?? new List<CostOriginBreakdown>())
                .Where(o => !string.IsNullOrWhiteSpace(o.ItemId))
                .GroupBy(o => o.ItemId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalUf), StringComparer.OrdinalIgnoreCase);
        }

        var costeables = items.Where(x => !x.EsTitulo).ToList();
        decimal baseTotal = costeables.Sum(x => ventaByItem.TryGetValue(x.Id, out decimal v) ? v : 0m);

        // Peso de cada partida en la venta. Si no hay base, repartir en partes iguales.
        decimal PesoDe(ItemizadoRow it)
        {
            if (baseTotal > 0m)
                return ventaByItem.TryGetValue(it.Id, out decimal v) ? v / baseTotal : 0m;
            return costeables.Count > 0 ? 1m / costeables.Count : 0m;
        }

        var rows = new List<OfferClienteRow>();
        OfferClienteRow? ultimaCosteable = null;
        decimal acumulado = 0m;
        foreach (var item in items)
        {
            if (item.EsTitulo)
            {
                rows.Add(new OfferClienteRow
                {
                    Id = item.Id ?? "",
                    Item = item.Item,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    Cantidad = item.Cantidad,
                    EsTitulo = true
                });
                continue;
            }

            decimal total = Money.Round(ventaNeta * PesoDe(item));
            acumulado += total;
            var row = new OfferClienteRow
            {
                Id = item.Id ?? "",
                Item = item.Item,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                Cantidad = item.Cantidad,
                TotalUf = total,
                Observaciones = !string.IsNullOrWhiteSpace(item.Observaciones)
                    ? item.Observaciones
                    : (item.MotivoNoCubicar ?? "")
            };
            rows.Add(row);
            if (total > 0m) ultimaCosteable = row;
        }

        // La última partida con monto absorbe el resto del redondeo: la suma cuadra EXACTO con
        // la venta neta. Se limita a no volverla negativa (residuo típico < $1, pero se protege).
        if (ultimaCosteable != null)
        {
            decimal ajuste = Money.Round(ventaNeta) - acumulado;
            ultimaCosteable.TotalUf = Math.Max(0m, ultimaCosteable.TotalUf + ajuste);
        }

        // Sin partidas costeables (solo títulos o itemizado vacío): una línea global con la venta.
        bool sinMonto = rows.Count == 0 || rows.All(r => r.EsTitulo);
        if (sinMonto && ventaNeta > 0m)
        {
            rows.Add(new OfferClienteRow
            {
                Id = "",
                Item = "1",
                Descripcion = fallbackProjectTitle,
                Unidad = "GL",
                Cantidad = 1m,
                TotalUf = Money.Round(ventaNeta)
            });
        }

        return rows;
    }
}
