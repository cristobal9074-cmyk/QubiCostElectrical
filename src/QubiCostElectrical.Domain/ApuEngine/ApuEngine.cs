using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Motor puro de APU. No conoce WinForms, SQLite, Excel ni PDF.
/// La UI y los servicios deben llamar a este motor para que el APU sea testeable.
/// </summary>
public sealed class ApuEngine
{
    public ApuRow CreateFromItem(ItemizadoRow item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        return new ApuRow
        {
            ItemId = item.Id,
            Descripcion = item.Descripcion,
            Unidad = item.Unidad,
            CantidadPartida = item.Cantidad < 0m ? 0m : item.Cantidad,
            FechaPrecio = DateTime.Today,
            FuentePrecio = "APU creado desde itemizado",
            Estado = "Falta precio",
            CondicionesLaborales = new ApuCondicionesLaboralesRow()
        };
    }

    public ApuRow CreateFromCubicacion(CubicacionRow cub, string descripcionItem, string unidadItem)
    {
        if (cub == null) throw new ArgumentNullException(nameof(cub));
        return new ApuRow
        {
            ItemId = cub.ItemId,
            Descripcion = descripcionItem ?? string.Empty,
            Unidad = unidadItem ?? string.Empty,
            CantidadPartida = cub.Cantidad < 0m ? 0m : cub.Cantidad,
            FuentePrecio = "Migrado desde cubicación",
            FechaPrecio = DateTime.Today,
            Estado = "Revisar rendimiento",
            Recursos = new List<ApuRecursoRow>
            {
                new()
                {
                    Tipo = ApuTipoRecurso.MAT,
                    CodigoMaterial = CodeHelper.Normalize(cub.Codigo),
                    Descripcion = cub.Descripcion,
                    Unidad = cub.Unidad,
                    Rendimiento = 1m,
                    PrecioUf = cub.MatUf,
                    FuentePrecio = "Cubicación",
                    FechaPrecio = DateTime.Today,
                    Estado = cub.MatUf <= 0m ? "Falta precio" : "OK"
                },
                new()
                {
                    Tipo = ApuTipoRecurso.MO,
                    Descripcion = "Mano de obra (migrado)",
                    Unidad = "HH",
                    Rendimiento = 1m,
                    PrecioUf = cub.MoUf,
                    ValorHHUf = cub.MoUf,
                    FuentePrecio = "Cubicación",
                    FechaPrecio = DateTime.Today,
                    Estado = cub.MoUf <= 0m ? "Falta precio" : "OK"
                }
            }
        };
    }

    public (decimal MaterialUf, decimal ManoObraUf) GetPricesForCubicacion(ApuRow? apu)
    {
        if (apu == null) return (0m, 0m);
        // TransporteUf ya resuelve "usar el recurso TR explícito si existe, si no calcular por
        // TransportePct" — usar CostoTransporteRecursoUf a secas (como antes) omitía el transporte
        // calculado por porcentaje cuando no hay recurso TR, desalineando este total con PrecioUnitUf.
        decimal material = apu.CostoMaterialUf + apu.CostoEquipoUf + apu.TransporteUf + apu.MermaUf;
        decimal manoObra = apu.CostoMoUf + apu.CostoScUf + apu.CostoHhUf + apu.HerramientasCalculadoUf + apu.ImprevistosUf;
        return (material, manoObra);
    }

    public IReadOnlyList<string> Validate(ApuRow? apu)
    {
        var errors = new List<string>();
        if (apu == null)
        {
            errors.Add("El APU no puede ser nulo.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(apu.Descripcion)) errors.Add("El APU debe tener descripción.");
        if (string.IsNullOrWhiteSpace(apu.Unidad)) errors.Add("El APU debe tener unidad.");
        if (apu.Recursos == null || apu.Recursos.Count == 0) errors.Add("El APU debe tener al menos un recurso.");

        foreach (var resource in apu.Recursos ?? Enumerable.Empty<ApuRecursoRow>())
        {
            if (string.IsNullOrWhiteSpace(resource.Descripcion)) errors.Add($"Recurso sin descripción (tipo {resource.Tipo}).");
            if (resource.Rendimiento <= 0m) errors.Add($"Recurso '{resource.Descripcion}': el rendimiento debe ser mayor a 0.");
            if (resource.PrecioUf < 0m) errors.Add($"Recurso '{resource.Descripcion}': el precio no puede ser negativo.");
        }

        if (apu.CantidadPartida < 0m) errors.Add("La cantidad de partida no puede ser negativa.");
        if (apu.HerramientasPct < 0m || apu.HerramientasPct > 30m) errors.Add("El % de herramientas debe estar entre 0 y 30.");
        if (apu.MermaPct < 0m || apu.MermaPct > 50m) errors.Add("El % de merma debe estar entre 0 y 50.");
        if (apu.TransportePct < 0m || apu.TransportePct > 50m) errors.Add("El % de transporte debe estar entre 0 y 50.");
        if (apu.ImprevistosAlzaPct < 0m || apu.ImprevistosAlzaPct > 20m) errors.Add("El % de imprevistos debe estar entre 0 y 20.");
        if (apu.PrecioUnitUf <= 0m) errors.Add("APU sin precio unitario de venta/costo técnico.");
        if (apu.Recursos != null && apu.Recursos.Any(r => string.Equals(r.Estado, "Falta precio", StringComparison.OrdinalIgnoreCase))) errors.Add("APU con recursos en estado Falta precio.");
        if (apu.Recursos != null && apu.Recursos.Any(r => string.Equals(r.Estado, "Revisar rendimiento", StringComparison.OrdinalIgnoreCase))) errors.Add("APU con recursos marcados para revisar rendimiento.");
        return errors;
    }
}
