using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public interface IPriceTraceabilityService
{
    IReadOnlyList<PriceTraceabilityRow> Build(
        IEnumerable<MaterialRow> materiales,
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<ApuRow> apus,
        ProjectInfoRow? proyecto = null,
        string? usuario = null);
}

/// <summary>
/// Hist.: Auditoría profesional de precios.
/// Cada precio usado en el presupuesto queda trazable por origen, fecha, proveedor, moneda, UF usada, usuario y estado.
/// </summary>
public sealed class PriceTraceabilityService : IPriceTraceabilityService
{
    public IReadOnlyList<PriceTraceabilityRow> Build(
        IEnumerable<MaterialRow> materiales,
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<ApuRow> apus,
        ProjectInfoRow? proyecto = null,
        string? usuario = null)
    {
        proyecto ??= new ProjectInfoRow();
        usuario = string.IsNullOrWhiteSpace(usuario) ? proyecto.UsuarioNombre : usuario;

        var materialList = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var cubicacionList = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).ToList();
        var rows = new List<PriceTraceabilityRow>();

        var materialByCode = materialList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in cubicacionList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            materialByCode.TryGetValue(group.Key, out var material);
            decimal precioUf = material != null ? material.MatUf + material.MoUf : first.MatUf + first.MoUf;
            rows.Add(new PriceTraceabilityRow
            {
                Codigo = group.Key,
                Descripcion = material?.Descripcion ?? first.Descripcion ?? "",
                Unidad = material?.Unidad ?? first.Unidad ?? "",
                PrecioUf = precioUf,
                Moneda = material?.MonedaPrecio ?? Moneda.CLP,
                UfUsadaClp = proyecto.ValorUfClp,
                Proveedor = material?.Proveedor ?? "",
                Origen = material?.OrigenPrecio ?? (material != null ? "Lista de precios" : "Cubicación manual"),
                Usuario = string.IsNullOrWhiteSpace(material?.UsuarioPrecio) ? (usuario ?? "") : material!.UsuarioPrecio,
                FechaPrecio = material?.FechaPrecio == default ? DateTime.Today : (material?.FechaPrecio ?? DateTime.Today),
                UltimaActualizacion = material?.UltimaActualizacionPrecio == default ? (proyecto.FechaModificacion == default ? AppClock.Now : proyecto.FechaModificacion) : (material?.UltimaActualizacionPrecio ?? AppClock.Now),
                Estado = ResolveMaterialStatus(material, precioUf, proyecto),
                UsadoEn = string.Join(", ", group.Select(x => x.ItemId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(8)),
                Observacion = material == null ? "Código usado en cubicación, pero no existe en la lista de precios." : "Precio tomado desde lista maestra."
            });
        }

        foreach (var apu in apuList)
        {
            foreach (var recurso in apu.Recursos ?? new List<ApuRecursoRow>())
            {
                string codigo = string.IsNullOrWhiteSpace(recurso.CodigoMaterial) ? recurso.Id : recurso.CodigoMaterial.Trim();
                rows.Add(new PriceTraceabilityRow
                {
                    Codigo = codigo,
                    Descripcion = recurso.Descripcion ?? apu.Descripcion ?? "",
                    Unidad = recurso.Unidad ?? apu.Unidad ?? "",
                    PrecioUf = recurso.PrecioBaseUf,
                    Moneda = Moneda.CLP,  // APU: PrecioUf contiene CLP desde la migración
                    UfUsadaClp = proyecto.ValorUfClp,
                    Proveedor = recurso.Proveedor ?? apu.Proveedor ?? "",
                    Origen = string.IsNullOrWhiteSpace(recurso.FuentePrecio) ? "APU" : recurso.FuentePrecio,
                    Usuario = usuario ?? "",
                    FechaPrecio = recurso.FechaPrecio == default ? apu.FechaPrecio : recurso.FechaPrecio,
                    UltimaActualizacion = proyecto.FechaModificacion == default ? AppClock.Now : proyecto.FechaModificacion,
                    Estado = ResolveApuResourceStatus(recurso, proyecto),
                    UsadoEn = apu.ItemId,
                    Observacion = recurso.Nota ?? "Recurso de APU profesional."
                });
            }
        }

        return rows
            .GroupBy(x => x.Origen + "|" + x.Codigo + "|" + x.UsadoEn, StringComparer.OrdinalIgnoreCase)
            .Select(g => Merge(g.ToList()))
            .OrderBy(x => x.Estado == PriceTraceStatus.OK ? 1 : 0)
            .ThenBy(x => x.Codigo)
            .ToList();
    }


    private static PriceTraceStatus ResolveMaterialStatus(MaterialRow? material, decimal precioUf, ProjectInfoRow proyecto)
    {
        if (precioUf <= 0m) return PriceTraceStatus.FaltaPrecio;
        if (material == null) return PriceTraceStatus.ImportadoSinValidar;
        if (material.FechaPrecio != default && proyecto.IsPrecioVencido(material.FechaPrecio)) return PriceTraceStatus.PrecioVencido;
        if (string.Equals(material.EstadoPrecio, "ManualSinRespaldo", StringComparison.OrdinalIgnoreCase)) return PriceTraceStatus.ManualSinRespaldo;
        if (string.Equals(material.EstadoPrecio, "ImportadoSinValidar", StringComparison.OrdinalIgnoreCase)) return PriceTraceStatus.ImportadoSinValidar;
        return PriceTraceStatus.OK;
    }

    private static PriceTraceStatus ResolveApuResourceStatus(ApuRecursoRow recurso, ProjectInfoRow proyecto)
    {
        if (recurso.PrecioBaseUf <= 0m) return PriceTraceStatus.FaltaPrecio;
        if ((recurso.Tipo == ApuTipoRecurso.MO || recurso.Tipo == ApuTipoRecurso.HH) && recurso.HHUnitarias <= 0m)
            return PriceTraceStatus.ImportadoSinValidar;
        if (recurso.Rendimiento <= 0m) return PriceTraceStatus.ImportadoSinValidar;
        if (string.IsNullOrWhiteSpace(recurso.Proveedor) && string.Equals(recurso.FuentePrecio, "Manual", StringComparison.OrdinalIgnoreCase))
            return PriceTraceStatus.ManualSinRespaldo;
        if (proyecto.IsPrecioVencido(recurso.FechaPrecio)) return PriceTraceStatus.PrecioVencido;
        return PriceTraceStatus.OK;
    }

    private static PriceTraceabilityRow Merge(IReadOnlyList<PriceTraceabilityRow> rows)
    {
        var first = rows[0];
        first.UsadoEn = string.Join(", ", rows.Select(x => x.UsadoEn).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(12));
        if (rows.Any(x => x.Estado != PriceTraceStatus.OK))
            first.Estado = rows.First(x => x.Estado != PriceTraceStatus.OK).Estado;
        return first;
    }
}
