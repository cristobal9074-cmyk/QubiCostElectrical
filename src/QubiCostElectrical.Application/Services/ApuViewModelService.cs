using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ApuViewModelService : IApuViewModelService
{
    private readonly ICubicacionViewModelService cubicacionViewModelService;

    public ApuViewModelService(ICubicacionViewModelService cubicacionViewModelService)
    {
        this.cubicacionViewModelService = cubicacionViewModelService;
    }

    public ApuViewModel BuildFromItem(
        ItemizadoRow? item,
        IEnumerable<CubicacionRow> cubicacion,
        decimal gastosGeneralesPct,
        decimal utilidadPct)
    {
        string itemId = item?.Id ?? "";
        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => string.Equals(x.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .ToList();

        decimal itemQty = item?.Cantidad > 0m ? item.Cantidad : 1m;

        var recursos = rows
            .Select(x => new ApuResourceViewModel(
                x.Tipo,
                x.Codigo,
                x.Descripcion,
                x.Unidad,
                itemQty > 0m ? x.Cantidad / itemQty : x.Cantidad,
                x.MatUf,
                x.MoUf,
                x.Cantidad * x.MatUf,
                x.Cantidad * x.MoUf,
                x.UfTotal,
                string.IsNullOrWhiteSpace(x.Area) ? "General" : x.Area.Trim()))
            .ToList();

        var summary = cubicacionViewModelService.BuildSummary(
            rows.Select(cubicacionViewModelService.ToLine),
            gastosGeneralesPct,
            utilidadPct,
            0m);

        decimal precioUnitario = itemQty > 0m ? summary.VentaUf / itemQty : summary.VentaUf;
        var alertas = BuildAlerts(item, recursos, summary).ToList();

        return new ApuViewModel(
            itemId,
            item?.Item ?? "",
            item?.Descripcion ?? "Partida sin descripción",
            item?.Unidad ?? "GL",
            itemQty,
            recursos,
            summary,
            precioUnitario,
            recursos.Count > 0,
            alertas);
    }

    private static IEnumerable<string> BuildAlerts(ItemizadoRow? item, IReadOnlyList<ApuResourceViewModel> recursos, CostSummaryViewModel summary)
    {
        if (item == null) yield return "Partida no encontrada.";
        if (recursos.Count == 0) yield return "APU sin recursos asociados.";
        if (summary.MaterialUf <= 0m) yield return "APU sin materiales valorizados.";
        if (summary.ManoObraUf <= 0m) yield return "APU sin mano de obra valorizada.";
        if (summary.VentaUf < summary.CostoDirectoUf) yield return "Precio venta menor que costo directo.";
    }
}
