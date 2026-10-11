using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class ApuFromCubicacionService
{
    public static bool IsAutoManagedApu(ApuRow apu) =>
        apu.FuentePrecio.StartsWith("Cubicacion", StringComparison.OrdinalIgnoreCase) ||
        apu.FuentePrecio.StartsWith("Cubicación", StringComparison.OrdinalIgnoreCase);

    public static string GetProfessionalStatus(ApuRow? apu)
    {
        if (apu == null || apu.Recursos == null || apu.Recursos.Count == 0) return "Falta precio";
        if (apu.Recursos.Any(r => r.PrecioBaseUf <= 0m ||
            string.Equals(r.Estado, "Falta precio", StringComparison.OrdinalIgnoreCase)))
            return "Falta precio";
        if (ApuLaborEngine.RequiereRevisionRendimiento(apu.CondicionesLaborales))
            return "Revisar rendimiento";
        if (apu.Recursos.Any(r => r.Rendimiento <= 0m ||
            string.Equals(r.Estado, "Revisar rendimiento", StringComparison.OrdinalIgnoreCase)))
            return "Revisar rendimiento";
        return "OK";
    }

    public static decimal CalculateSaleUnitUf(ApuRow apu, decimal gastosGeneralesPct, decimal utilidadPct,
        MarkupMethod method = MarkupMethod.SobreVenta)
    {
        ArgumentNullException.ThrowIfNull(apu);
        decimal directo = apu.CostoDirectoUf;
        decimal subtotal = directo + directo * gastosGeneralesPct / 100m;
        if (method == MarkupMethod.SobreVenta && utilidadPct < 100m)
            return subtotal / (1m - utilidadPct / 100m);
        return subtotal + subtotal * utilidadPct / 100m;
    }

    public static ApuRow BuildFromCubicacion(
        ItemizadoRow item,
        IReadOnlyList<CubicacionRow> cubicacion,
        IApuService apuService)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(cubicacion);
        ArgumentNullException.ThrowIfNull(apuService);

        var nuevo = apuService.CrearDesdeItem(item);
        nuevo.CantidadPartida = item.Cantidad < 0m ? 0m : item.Cantidad;
        nuevo.FuentePrecio = "Cubicacion valorizada";
        nuevo.FechaPrecio = DateTime.Today;
        nuevo.Recursos.Clear();

        foreach (var row in cubicacion.Where(x =>
            string.Equals(x.ItemId, item.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (row.Cantidad <= 0m && row.MatUf <= 0m && row.MoUf <= 0m) continue;
            decimal rendimiento = nuevo.CantidadPartida > 0m
                ? row.Cantidad / nuevo.CantidadPartida
                : row.Cantidad;

            nuevo.Recursos.Add(new ApuRecursoRow
            {
                Tipo = ApuTipoRecurso.MAT,
                CodigoMaterial = CodeHelper.Normalize(row.Codigo),
                Descripcion = row.Descripcion,
                Unidad = row.Unidad,
                Rendimiento = rendimiento,
                PrecioUf = row.MatUf,
                FuentePrecio = "Cubicacion",
                FechaPrecio = DateTime.Today,
                Estado = row.MatUf > 0m ? "OK" : "Falta precio"
            });

            if (row.MoUf > 0m)
            {
                nuevo.Recursos.Add(new ApuRecursoRow
                {
                    Tipo = ApuTipoRecurso.MO,
                    CodigoMaterial = CodeHelper.Normalize(row.Codigo),
                    Descripcion = "Instalacion " + row.Descripcion,
                    Unidad = "HH",
                    Rendimiento = rendimiento,
                    PrecioUf = row.MoUf,
                    ValorHHUf = row.MoUf,
                    FuentePrecio = "Cubicacion",
                    FechaPrecio = DateTime.Today,
                    Estado = "Revisar rendimiento"
                });
            }
        }

        nuevo.Estado = GetProfessionalStatus(nuevo);
        return nuevo;
    }

    public static (int Creados, int Regenerados, int Omitidos) GenerateBulk(
        IReadOnlyList<ItemizadoRow> itemizado,
        List<ApuRow> apus,
        IReadOnlyList<CubicacionRow> cubicacion,
        IApuService apuService)
    {
        ArgumentNullException.ThrowIfNull(itemizado);
        ArgumentNullException.ThrowIfNull(apus);
        ArgumentNullException.ThrowIfNull(cubicacion);
        ArgumentNullException.ThrowIfNull(apuService);

        var itemsConCubicacion = new HashSet<string>(
            cubicacion.Where(c => !string.IsNullOrWhiteSpace(c.ItemId)).Select(c => c.ItemId),
            StringComparer.OrdinalIgnoreCase);

        int creados = 0, regenerados = 0, omitidos = 0;
        foreach (var item in itemizado.Where(i => !i.EsTitulo && !string.IsNullOrWhiteSpace(i.Id)))
        {
            if (!itemsConCubicacion.Contains(item.Id)) continue;

            var existente = apus.FirstOrDefault(a =>
                string.Equals(a.ItemId, item.Id, StringComparison.OrdinalIgnoreCase));

            if (existente == null)
            {
                apus.Add(BuildFromCubicacion(item, cubicacion, apuService));
                creados++;
            }
            else if (IsAutoManagedApu(existente))
            {
                apus.Remove(existente);
                apus.Add(BuildFromCubicacion(item, cubicacion, apuService));
                regenerados++;
            }
            else
            {
                omitidos++;
            }
        }

        return (creados, regenerados, omitidos);
    }

    public static int RefreshPricesFromCatalog(
        List<ApuRow> apus,
        IReadOnlyList<MaterialRow> materiales,
        bool preciosCongelados,
        ProjectInfoRow? project = null)
    {
        ArgumentNullException.ThrowIfNull(apus);
        ArgumentNullException.ThrowIfNull(materiales);

        if (preciosCongelados) return 0;

        var catalogo = materiales
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => m.CodigoNormalizado, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        if (catalogo.Count == 0) return 0;

        int cambios = 0;
        foreach (var apu in apus)
        {
            bool apuCambio = false;
            foreach (var recurso in apu.Recursos)
            {
                if (string.IsNullOrWhiteSpace(recurso.CodigoMaterial)) continue;
                if (!catalogo.TryGetValue(recurso.CodigoMaterial, out var mat)) continue;

                bool esManoObra = recurso.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH;
                decimal precioCatalogo = esManoObra ? ResolveMoUfEfectiva(mat, project) : mat.MatUf;
                if (precioCatalogo <= 0m) continue;

                decimal precioActual = esManoObra && recurso.ValorHHUf > 0m
                    ? recurso.ValorHHUf
                    : recurso.PrecioUf;
                if (precioActual == precioCatalogo) continue;

                recurso.PrecioUf = precioCatalogo;
                if (esManoObra) recurso.ValorHHUf = precioCatalogo;
                recurso.FuentePrecio = "Catalogo de materiales";
                recurso.FechaPrecio = mat.FechaPrecio == default ? DateTime.Today : mat.FechaPrecio;
                if (string.Equals(recurso.Estado, "Falta precio", StringComparison.OrdinalIgnoreCase))
                    recurso.Estado = "OK";
                cambios++;
                apuCambio = true;
            }

            if (apuCambio)
            {
                apu.FechaPrecio = DateTime.Today;
                apu.Estado = GetProfessionalStatus(apu);
            }
        }

        return cambios;
    }

    /// <summary>MO en UF: histórica (mat.MoUf) o derivada (rendimiento / fórmula porcentaje) si el proyecto lo pide.</summary>
    private static decimal ResolveMoUfEfectiva(MaterialRow mat, ProjectInfoRow? project)
    {
        bool derivada = MaterialMoResolver.DebeUsarRendimiento(mat, project)
                     || MaterialMoResolver.DebeUsarPorcentaje(mat);
        if (!derivada || project == null || project.ValorUfClp <= 0m)
            return mat.MoUf;
        return PriceConversion.ClpToUf(MaterialMoResolver.MoClpEfectiva(mat, project), project.ValorUfClp);
    }
}
