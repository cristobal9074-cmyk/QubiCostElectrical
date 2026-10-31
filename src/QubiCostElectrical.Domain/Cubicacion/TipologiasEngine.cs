using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Motor de cálculo y expansión paramétrica de tipologías habitacionales a cubicación de obra.
/// Reemplaza la matriz manual de miles de filas (ej. DETALLE de 8.760 filas) multiplicando
/// las cubicaciones unitarias por la cantidad de departamentos de cada piso en 1 solo paso.
/// </summary>
public static class TipologiasEngine
{
    /// <summary>
    /// Resumen de departamentos totales por cada tipología en el edificio.
    /// </summary>
    public static List<TipologiaResumenDto> CalcularResumenTipologias(TipologiasEdificioData? data)
    {
        var result = new List<TipologiaResumenDto>();
        if (data?.Tipologias == null || data.Tipologias.Count == 0)
            return result;

        var distPorTipo = (data.Distribucion ?? new List<TipologiaDistribucionPiso>())
            .GroupBy(d => d.TipologiaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.CantidadDeptos)));

        foreach (var t in data.Tipologias)
        {
            distPorTipo.TryGetValue(t.Id, out int totalDeptos);
            result.Add(new TipologiaResumenDto
            {
                TipologiaId = t.Id,
                Codigo = t.Codigo,
                Nombre = t.Nombre,
                SuperficieM2 = t.SuperficieM2,
                CantidadDeptosTotal = totalDeptos,
                TotalRecursosDistintos = t.Items?.Count ?? 0
            });
        }

        return result;
    }

    /// <summary>
    /// Consolida todos los materiales y recursos multiplicando la dotación unitaria
    /// por la cantidad de unidades habitacionales de cada tipología en el proyecto.
    /// </summary>
    public static List<MaterialConsolidadoTipologiaDto> CalcularConsolidadoMateriales(TipologiasEdificioData? data)
    {
        var result = new Dictionary<string, MaterialConsolidadoTipologiaDto>(StringComparer.OrdinalIgnoreCase);
        if (data?.Tipologias == null || data.Tipologias.Count == 0)
            return result.Values.ToList();

        var distPorTipo = (data.Distribucion ?? new List<TipologiaDistribucionPiso>())
            .GroupBy(d => d.TipologiaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.CantidadDeptos)));

        foreach (var tipologia in data.Tipologias)
        {
            if (!distPorTipo.TryGetValue(tipologia.Id, out int totalDeptos) || totalDeptos <= 0)
                continue;

            if (tipologia.Items == null)
                continue;

            foreach (var item in tipologia.Items)
            {
                if (item.CantidadPorDepto <= 0m)
                    continue;

                string key = !string.IsNullOrWhiteSpace(item.Codigo) ? item.Codigo.Trim() : item.Descripcion.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                decimal subtotalCant = item.CantidadPorDepto * totalDeptos;

                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new MaterialConsolidadoTipologiaDto
                    {
                        Codigo = item.Codigo,
                        Descripcion = item.Descripcion,
                        Unidad = item.Unidad,
                        Categoria = item.Categoria,
                        PartidaCodigo = item.PartidaCodigo,
                        CantidadTotal = subtotalCant,
                        MatClp = item.MatClp,
                        MoClp = item.MoClp
                    };
                    result[key] = dto;
                }
                else
                {
                    dto.CantidadTotal += subtotalCant;
                    if (dto.MatClp == 0m && item.MatClp > 0m) dto.MatClp = item.MatClp;
                    if (dto.MoClp == 0m && item.MoClp > 0m) dto.MoClp = item.MoClp;
                    if (string.IsNullOrWhiteSpace(dto.PartidaCodigo) && !string.IsNullOrWhiteSpace(item.PartidaCodigo))
                        dto.PartidaCodigo = item.PartidaCodigo;
                }
            }
        }

        return result.Values.OrderBy(x => x.Categoria).ThenBy(x => x.Descripcion).ToList();
    }

    /// <summary>
    /// Genera filas de cubicación de obra (CubicacionRow) para volcar a las partidas
    /// del itemizado en base a los totales calculados por tipología.
    /// </summary>
    public static List<CubicacionRow> GenerarFilasCubicacion(
        TipologiasEdificioData? data,
        IEnumerable<ItemizadoRow>? itemizado,
        decimal valorUfClp = 38500m)
    {
        var filas = new List<CubicacionRow>();
        if (data == null)
            return filas;

        var consolidados = CalcularConsolidadoMateriales(data);
        if (consolidados.Count == 0)
            return filas;

        var itemsMap = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(it => !it.EsTitulo && !string.IsNullOrWhiteSpace(it.Item))
            .ToDictionary(it => it.Item.Trim(), it => it, StringComparer.OrdinalIgnoreCase);

        // Si no hay mapeo explícito de partida, buscar una partida genérica de departamentos
        ItemizadoRow? fallbackPartida = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .FirstOrDefault(it => !it.EsTitulo && it.Descripcion.Contains("departamento", StringComparison.OrdinalIgnoreCase))
            ?? (itemizado ?? Enumerable.Empty<ItemizadoRow>()).FirstOrDefault(it => !it.EsTitulo);

        decimal uf = valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;

        foreach (var mat in consolidados)
        {
            string itemId = "";
            if (!string.IsNullOrWhiteSpace(mat.PartidaCodigo) && itemsMap.TryGetValue(mat.PartidaCodigo.Trim(), out var targetPartida))
            {
                itemId = targetPartida.Id;
            }
            else if (fallbackPartida != null)
            {
                itemId = fallbackPartida.Id;
            }

            var row = new CubicacionRow
            {
                ItemId = itemId,
                Tipo = TipoRecurso.MAT,
                Codigo = mat.Codigo,
                Descripcion = mat.Descripcion,
                Unidad = mat.Unidad,
                Categoria = mat.Categoria,
                Cantidad = mat.CantidadTotal,
                MatClp = mat.MatClp,
                MoClp = mat.MoClp,
                MatUf = uf > 0m ? Math.Round(mat.MatClp / uf, 4) : 0m,
                MoUf = uf > 0m ? Math.Round(mat.MoClp / uf, 4) : 0m,
                Area = "Departamentos"
            };

            filas.Add(row);
        }

        return filas;
    }
}
