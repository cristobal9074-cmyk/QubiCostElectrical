using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ApuResumenRow(
    string ItemId,
    string Descripcion,
    string Unidad,
    decimal CostoMat,
    decimal CostoMo,
    decimal CostoEquipo,
    decimal CostoSc,
    decimal Herramientas,
    decimal Imprevistos,
    decimal PrecioUnit,
    decimal CantidadItem,
    decimal TotalUf);

public sealed class ApuService : IApuService
{
    private readonly ApuEngine engine = new();

    public ApuRow CrearDesdeItem(ItemizadoRow item) => engine.CreateFromItem(item);

    public ApuRow MigrarDesde(CubicacionRow cub, string descripcionItem, string unidadItem)
        => engine.CreateFromCubicacion(cub, descripcionItem, unidadItem);

    public (decimal MatUf, decimal MoUf) GetPreciosParaCubicacion(ApuRow apu)
    {
        var prices = engine.GetPricesForCubicacion(apu);
        return (prices.MaterialUf, prices.ManoObraUf);
    }


    public List<ApuResumenRow> GetResumenProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado)
    {
        var cantidadPorItem = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(i => !i.EsTitulo && !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Cantidad, StringComparer.OrdinalIgnoreCase);

        return (apus ?? Enumerable.Empty<ApuRow>())
            .Where(a => !string.IsNullOrWhiteSpace(a.ItemId))
            .Select(apu =>
            {
                cantidadPorItem.TryGetValue(apu.ItemId, out decimal cant);
                return new ApuResumenRow(
                    ItemId: apu.ItemId,
                    Descripcion: apu.Descripcion,
                    Unidad: apu.Unidad,
                    CostoMat: apu.CostoMaterialUf,
                    CostoMo: apu.CostoMoUf,
                    CostoEquipo: apu.CostoEquipoUf,
                    CostoSc: apu.CostoScUf,
                    Herramientas: apu.CostoHhUf + apu.HerramientasCalculadoUf,
                    Imprevistos: apu.ImprevistosUf,
                    PrecioUnit: apu.PrecioUnitUf,
                    CantidadItem: cant,
                    TotalUf: apu.PrecioUnitUf * cant);
            })
            .OrderByDescending(r => r.TotalUf)
            .ToList();
    }

    public (decimal Mat, decimal Mo, decimal Equipo, decimal Sc, decimal Herramientas, decimal Imprevistos, decimal Total) GetTotalesProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado)
    {
        var cantidadPorItem = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(i => !i.EsTitulo && !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Cantidad, StringComparer.OrdinalIgnoreCase);

        decimal mat = 0m;
        decimal mo = 0m;
        decimal eq = 0m;
        decimal sc = 0m;
        decimal hh = 0m;
        decimal imp = 0m;

        foreach (var apu in apus ?? Enumerable.Empty<ApuRow>())
        {
            if (string.IsNullOrWhiteSpace(apu.ItemId)) continue;
            cantidadPorItem.TryGetValue(apu.ItemId, out decimal cant);
            mat += apu.CostoMaterialUf * cant;
            mo += apu.CostoMoUf * cant;
            eq += apu.CostoEquipoUf * cant;
            sc += apu.CostoScUf * cant;
            hh += (apu.CostoHhUf + apu.HerramientasCalculadoUf) * cant;
            imp += apu.ImprevistosUf * cant;
        }

        return (mat, mo, eq, sc, hh, imp, mat + mo + eq + sc + hh + imp);
    }

    public List<string> Validar(ApuRow apu) => engine.Validate(apu).ToList();
}
