using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class BudgetEngine
{
    public void MarkDirtyItem(string? itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
            dirtyItems.Add(itemId.Trim());
    }

    public void MarkDirtyItems(IEnumerable<string>? itemIds)
    {
        foreach (string itemId in itemIds ?? Enumerable.Empty<string>())
            MarkDirtyItem(itemId);
    }

    public void ClearDirtyItems() => dirtyItems.Clear();

    public CostBreakdown Calculate(
        IEnumerable<CubicacionRow>? cubicacion,
        BudgetSettings? settings = null,
        IEnumerable<ApuRow>? apus = null,
        IEnumerable<ItemizadoRow>? itemizado = null,
        IEnumerable<ChangeOrderRow>? changeOrders = null)
    {
        settings ??= new BudgetSettings();
        var direct = CalculateDirectCostsDetailed(
            cubicacion ?? Enumerable.Empty<CubicacionRow>(),
            settings,
            apus,
            itemizado,
            changeOrders);

        return BuildCostBreakdown(direct, settings);
    }

    public CostBreakdown CalculateIncremental(
        IEnumerable<CubicacionRow>? cubicacion,
        BudgetSettings? settings,
        IEnumerable<ApuRow>? apus,
        IEnumerable<ItemizadoRow>? itemizado,
        IEnumerable<ChangeOrderRow>? changeOrders,
        CostBreakdown? previous,
        IEnumerable<string>? dirtyItemIds = null)
    {
        settings ??= new BudgetSettings();
        var dirty = (dirtyItemIds ?? dirtyItems)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // El modo manual, APUs y órdenes de cambio tienen dependencias cruzadas; en esos casos
        // la ruta segura es el cálculo completo. La ruta incremental se usa para edición pura
        // de cubicación, que es el caso de mayor frecuencia en proyectos grandes.
        if (previous == null || dirty.Count == 0 || settings.ApuMode == BudgetApuMode.Manual)
        {
            var full = Calculate(cubicacion, settings, apus, itemizado, changeOrders);
            ClearDirtyItems();
            return full;
        }

        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).Where(x => x != null).ToList();
        var itemList = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();
        var rowsByItem = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var apusByItem = apuList
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var itemizadoById = BuildItemizadoIndex(itemList);
        var areaByItemId = BuildAreaIndex(rows);
        var result = new DirectCostResult();

        foreach (var origin in previous.Origenes ?? new List<CostOriginBreakdown>())
        {
            if (origin.IsGlobalAutoCharge) continue;
            if (!string.IsNullOrWhiteSpace(origin.ItemId) && dirty.Contains(origin.ItemId)) continue;
            var clone = CloneOrigin(origin);
            AddOrigin(result, clone);
        }

        foreach (var row in rows.Where(x => string.IsNullOrWhiteSpace(x.ItemId) || !dirty.Contains(x.ItemId)))
        {
            AddMaterialTotalFromCubicacionRow(result, row);
        }

        var dirtyItemIdsToProcess = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId) && dirty.Contains(x.ItemId))
            .Select(x => x.ItemId!)
            .Concat(dirty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var itemId in dirtyItemIdsToProcess)
        {
            rowsByItem.TryGetValue(itemId, out var itemRows);
            itemRows ??= new List<CubicacionRow>();
            apusByItem.TryGetValue(itemId, out var itemApus);
            itemApus ??= new List<ApuRow>();

            bool hasValuedCubicacion = itemRows.Any(HasValuedCubicacionRow);
            switch (settings.ApuMode)
            {
                case BudgetApuMode.CubicacionManda:
                    foreach (var row in itemRows)
                    {
                        if (string.IsNullOrWhiteSpace(row.Codigo) && string.IsNullOrWhiteSpace(row.Descripcion) && row.Cantidad == 0m && row.MatClpConFallback == 0m && row.MoClpConFallback == 0m) continue;
                        AddCubicacionRow(result, row, settings);
                    }

                    if (!hasValuedCubicacion)
                    {
                        foreach (var apu in itemApus)
                            AddApuRow(result, apu, GetItemQuantity(itemizadoById, itemId, result), fullApu: true, area: InferArea(areaByItemId, itemId), settings: settings);
                    }
                    break;

                case BudgetApuMode.ApuManda:
                    if (itemApus.Count > 0)
                    {
                        foreach (var apu in itemApus)
                            AddApuRow(result, apu, GetItemQuantity(itemizadoById, itemId, result), fullApu: true, area: InferArea(areaByItemId, itemId), settings: settings);
                    }
                    else
                    {
                        foreach (var row in itemRows)
                        {
                            if (string.IsNullOrWhiteSpace(row.Codigo) && string.IsNullOrWhiteSpace(row.Descripcion) && row.Cantidad == 0m && row.MatClpConFallback == 0m && row.MoClpConFallback == 0m) continue;
                            AddCubicacionRow(result, row, settings);
                        }
                    }
                    break;

                case BudgetApuMode.CubicacionMasApuComplementarios:
                    foreach (var row in itemRows)
                    {
                        if (string.IsNullOrWhiteSpace(row.Codigo) && string.IsNullOrWhiteSpace(row.Descripcion) && row.Cantidad == 0m && row.MatClpConFallback == 0m && row.MoClpConFallback == 0m) continue;
                        AddCubicacionRow(result, row, settings);
                    }

                    foreach (var apu in itemApus)
                        AddApuRow(result, apu, GetItemQuantity(itemizadoById, itemId, result), fullApu: !hasValuedCubicacion, area: InferArea(areaByItemId, itemId), settings: settings);
                    break;
            }
        }

        AddGlobalPercentages(result, settings);
        ClearDirtyItems();
        return BuildCostBreakdown(result, settings);
    }

    private static CostBreakdown BuildCostBreakdown(DirectCostResult direct, BudgetSettings settings)
        => BudgetTotalsCalculator.Build(direct, settings);

    private static CostOriginBreakdown CloneOrigin(CostOriginBreakdown source) => new()
    {
        Origen = source.Origen,
        ItemId = source.ItemId,
        Codigo = source.Codigo,
        Descripcion = source.Descripcion,
        Area = source.Area,
        MaterialesUf = source.MaterialesUf,
        ManoObraUf = source.ManoObraUf,
        EquiposUf = source.EquiposUf,
        SubcontratosUf = source.SubcontratosUf,
        HerramientasUf = source.HerramientasUf,
        TransporteUf = source.TransporteUf,
        MermasUf = source.MermasUf,
        OrdenesCambioUf = source.OrdenesCambioUf,
        VentaClp = source.VentaClp,
        HasRowLevelMarkup = source.HasRowLevelMarkup,
        MermaPctOverride = source.MermaPctOverride,
        HerramientasPctOverride = source.HerramientasPctOverride,
        IsGlobalAutoCharge = source.IsGlobalAutoCharge
    };

    public AreaCostBreakdown CalculateDirectCosts(IEnumerable<CubicacionRow> rows, BudgetSettings? settings = null)
    {
        settings ??= new BudgetSettings { ApuMode = BudgetApuMode.CubicacionManda };
        var result = CalculateDirectCostsDetailed(rows ?? Enumerable.Empty<CubicacionRow>(), settings, null, null, null);
        return result.ToArea();
    }

    public List<AreaCostBreakdown> CalculateByArea(
        IEnumerable<CubicacionRow>? cubicacion,
        BudgetSettings? settings = null,
        IEnumerable<ApuRow>? apus = null,
        IEnumerable<ItemizadoRow>? itemizado = null)
    {
        settings ??= new BudgetSettings();
        var cost = Calculate(cubicacion, settings, apus, itemizado);
        return cost.Origenes
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Area) ? "General" : x.Area.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new AreaCostBreakdown
            {
                Area = g.Key,
                MaterialesUf = g.Sum(x => x.MaterialesUf),
                ManoObraUf = g.Sum(x => x.ManoObraUf),
                EquiposUf = g.Sum(x => x.EquiposUf),
                SubcontratosUf = g.Sum(x => x.SubcontratosUf),
                HerramientasUf = g.Sum(x => x.HerramientasUf),
                TransporteUf = g.Sum(x => x.TransporteUf),
                MermasUf = g.Sum(x => x.MermasUf),
                OrdenesCambioUf = g.Sum(x => x.OrdenesCambioUf),
                TotalUf = g.Sum(x => x.TotalUf)
            })
            .OrderByDescending(x => x.TotalUf)
            .ToList();
    }

    public List<MaterialCostTotal> CalculateMaterialTotals(IEnumerable<CubicacionRow>? cubicacion)
    {
        return (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => new { Codigo = (x.Codigo ?? "").Trim(), Descripcion = x.Descripcion ?? "", Unidad = x.Unidad ?? "" })
            .Select(g => new MaterialCostTotal
            {
                Codigo = g.Key.Codigo,
                Descripcion = g.Key.Descripcion,
                Unidad = g.Key.Unidad,
                Cantidad = g.Sum(x => x.Cantidad),
                TotalUf = g.Sum(x => RowTotalUf(x))
            })
            .OrderByDescending(x => x.TotalUf)
            .ToList();
    }

    private static DirectCostResult CalculateDirectCostsDetailed(
        IEnumerable<CubicacionRow> cubicacion,
        BudgetSettings settings,
        IEnumerable<ApuRow>? apus,
        IEnumerable<ItemizadoRow>? itemizado,
        IEnumerable<ChangeOrderRow>? changeOrders)
    {
        var result = new DirectCostResult();
        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).Where(x => x != null).ToList();
        var itemList = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();

        if (settings.ApuMode == BudgetApuMode.Manual)
        {
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.Manual,
                Descripcion = "Costo directo manual",
                MaterialesUf = settings.ManualMaterialesUf,
                ManoObraUf = settings.ManualManoObraUf,
                EquiposUf = settings.ManualEquiposUf,
                SubcontratosUf = settings.ManualSubcontratosUf,
                HerramientasUf = settings.ManualHerramientasUf,
                MermasUf = settings.ManualMermasUf
            });
            AddApprovedChangeOrders(result, changeOrders);
            return result;
        }

        var itemsWithApu = apuList
            .Where(a => !string.IsNullOrWhiteSpace(a.ItemId))
            .Select(a => a.ItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var itemsWithValuedCubicacion = rows
            .Where(HasValuedCubicacionRow)
            .Select(x => x.ItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var itemizadoById = BuildItemizadoIndex(itemList);
        var areaByItemId = BuildAreaIndex(rows);

        bool includeCubicacion = settings.ApuMode != BudgetApuMode.ApuManda;
        foreach (var row in rows)
        {
            // Filtro de fila vacía: usa MatClpConFallback/MoClpConFallback (fuente real) para no depender de ValorUfClp
            if (string.IsNullOrWhiteSpace(row.Codigo) && string.IsNullOrWhiteSpace(row.Descripcion) && row.Cantidad == 0m && row.MatClpConFallback == 0m && row.MoClpConFallback == 0m) continue;
            if (settings.ApuMode == BudgetApuMode.ApuManda && !string.IsNullOrWhiteSpace(row.ItemId) && itemsWithApu.Contains(row.ItemId))
                continue;

            if (includeCubicacion)
                AddCubicacionRow(result, row, settings);
        }

        switch (settings.ApuMode)
        {
            case BudgetApuMode.CubicacionManda:
                foreach (var apu in apuList.Where(a => !itemsWithValuedCubicacion.Contains(a.ItemId)))
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: true, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                break;

            case BudgetApuMode.ApuManda:
                foreach (var apu in apuList)
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: true, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                foreach (var row in rows.Where(x => string.IsNullOrWhiteSpace(x.ItemId) || !itemsWithApu.Contains(x.ItemId)))
                    AddCubicacionRow(result, row, settings);
                break;

            case BudgetApuMode.CubicacionMasApuComplementarios:
                foreach (var apu in apuList)
                {
                    bool hasCubicacion = !string.IsNullOrWhiteSpace(apu.ItemId) && itemsWithValuedCubicacion.Contains(apu.ItemId);
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: !hasCubicacion, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                }
                break;
        }

        AddGlobalPercentages(result, settings);
        AddApprovedChangeOrders(result, changeOrders);
        return result;
    }

    private static void AddCubicacionRow(DirectCostResult result, CubicacionRow row, BudgetSettings settings)
    {
        // Fuente CLP. Los campos *Uf de CostOriginBreakdown conservan el nombre histórico
        // pero almacenan pesos; preferir alias *Clp en código nuevo.
        decimal material = row.Cantidad * row.MatClpConFallback;
        decimal manoObra = row.Cantidad * row.MoClpConFallback;
        decimal total = material + manoObra;
        string tipo = (row.Tipo ?? "MAT").Trim().ToUpperInvariant();
        decimal valorUf = settings.Currency.ValorUfClp > 0m ? settings.Currency.ValorUfClp : 39000m;

        var origin = new CostOriginBreakdown
        {
            Origen = IsLikelyTemplate(row) ? BudgetCostOrigin.Plantilla : BudgetCostOrigin.Cubicacion,
            ItemId = row.ItemId,
            Codigo = row.Codigo,
            Descripcion = row.Descripcion,
            Area = string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area.Trim(),
            MermaPctOverride = row.MermaPctOverride,
            HerramientasPctOverride = row.HerramientasPctOverride,
            VentaClp = row.CalcularUfUnit(valorUf) * valorUf * row.Cantidad,
            HasRowLevelMarkup = row.GgUtFactor != 1.0m
        };

        switch (tipo)
        {
            case "EQ":
            case "EQUIPO":
            case "EQUIPOS":
                origin.EquiposClp = total;
                break;
            case "SC":
            case "SUBCONTRATO":
            case "SUBCONTRATOS":
                origin.SubcontratosClp = total;
                break;
            case "HERR":
            case "HERRAMIENTA":
            case "HERRAMIENTAS":
                origin.HerramientasClp = total;
                break;
            case "TR":
            case "TRANSPORTE":
            case "TRANSPORTES":
                origin.TransporteClp = total;
                break;
            case "MERMA":
            case "MERMAS":
                origin.MermasClp = total;
                break;
            case "MO":
            case "HH":
                origin.ManoObraClp = total;
                break;
            default:
                origin.MaterialesClp = material;
                origin.ManoObraClp = manoObra;
                break;
        }

        if (row.HerramientasPctOverride >= 0m && origin.ManoObraClp != 0m)
            origin.HerramientasClp += origin.ManoObraClp * MarkupPolicy.ClampPct(row.HerramientasPctOverride) / 100m;

        if (row.MermaPctOverride >= 0m && origin.MaterialesClp != 0m)
            origin.MermasClp += origin.MaterialesClp * MarkupPolicy.ClampPct(row.MermaPctOverride) / 100m;

        AddMaterialTotalFromCubicacionRow(result, row);

        AddOrigin(result, origin);
    }

    private static bool HasValuedCubicacionRow(CubicacionRow row)
    {
        if (row == null) return false;
        if (string.IsNullOrWhiteSpace(row.ItemId)) return false;
        if (row.Cantidad == 0m) return false;
        // MIGRACIÓN CLP: detectar cubicación valorizada usando la fuente real con fallback
        return row.MatClpConFallback != 0m || row.MoClpConFallback != 0m;
    }

    private static void AddMaterialTotalFromCubicacionRow(DirectCostResult result, CubicacionRow row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Codigo)) return;
        decimal material = MaterialTotalForMaterialSummary(row);
        if (material != 0m)
            AddMaterialTotal(result, row.Codigo, row.Descripcion, row.Unidad, row.Cantidad, material);
    }


    private static decimal MaterialTotalForMaterialSummary(CubicacionRow row)
    {
        string tipo = (row.Tipo ?? "MAT").Trim().ToUpperInvariant();
        // MIGRACIÓN CLP: usa MatClpConFallback para el resumen de materiales críticos
        return tipo switch
        {
            "EQ" or "EQUIPO" or "EQUIPOS" => 0m,
            "SC" or "SUBCONTRATO" or "SUBCONTRATOS" => 0m,
            "HERR" or "HERRAMIENTA" or "HERRAMIENTAS" => 0m,
            "TR" or "TRANSPORTE" or "TRANSPORTES" => 0m,
            "MERMA" or "MERMAS" => 0m,
            "MO" or "HH" => 0m,
            _ => row.Cantidad * row.MatClpConFallback
        };
    }

    private static bool IsLikelyTemplate(CubicacionRow row)
    {
        var code = (row.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        return code.StartsWith("TPL-", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("PLANTILLA", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddApuRow(DirectCostResult result, ApuRow apu, decimal cantidadPartida, bool fullApu, string area, BudgetSettings settings)
    {
        if (apu == null) return;
        if (cantidadPartida == 0m)
        {
            result.Inconsistencias.Add(new BudgetInconsistencia
            {
                ItemId = apu.ItemId,
                Descripcion = apu.Descripcion,
                Tipo = "CantidadCero",
                Detalle = $"APU '{apu.Descripcion}' tiene cantidad 0; el costo de esta partida es 0."
            });
            return;
        }

        decimal materiales = fullApu ? apu.CostoMaterialUf * cantidadPartida : 0m;
        decimal manoObra = fullApu ? (apu.CostoMoUf + apu.CostoHhUf) * cantidadPartida : 0m;
        decimal equipos = apu.CostoEquipoUf * cantidadPartida;
        decimal subcontratos = apu.CostoScUf * cantidadPartida;
        decimal herramientas = apu.HerramientasCalculadoUf * cantidadPartida;
        decimal transporte = apu.TransporteUf * cantidadPartida;
        decimal mermas = (apu.MermaUf + apu.ImprevistosUf) * cantidadPartida;

        if (!fullApu && equipos == 0m && subcontratos == 0m && herramientas == 0m && transporte == 0m && mermas == 0m) return;

        if (fullApu && materiales > 0m)
        {
            foreach (var r in apu.Recursos.Where(x => x.Tipo == ApuTipoRecurso.MAT))
            {
                var cantidad = r.Rendimiento * cantidadPartida;
                var totalUf = r.CostoUnitUf * cantidadPartida;
                if (totalUf > 0m)
                    AddMaterialTotal(result, r.CodigoMaterial, r.Descripcion, r.Unidad, cantidad, totalUf);
            }
        }

        AddOrigin(result, new CostOriginBreakdown
        {
            Origen = BudgetCostOrigin.Apu,
            ItemId = apu.ItemId,
            Descripcion = fullApu ? apu.Descripcion : $"Recursos complementarios APU: {apu.Descripcion}",
            Area = area,
            MaterialesUf = materiales,
            ManoObraUf = manoObra,
            EquiposUf = equipos,
            SubcontratosUf = subcontratos,
            HerramientasUf = herramientas,
            TransporteUf = transporte,
            MermasUf = mermas,
            VentaClp = PriceConversion.UfToClp(apu.PrecioUnitUf * cantidadPartida, settings.Currency.ValorUfClp)
        });
    }

    private static void AddGlobalPercentages(DirectCostResult result, BudgetSettings settings)
    {
        decimal herramientasGlobalBase = result.Origenes
            .Where(x => x.HerramientasPctOverride < 0m)
            .Sum(x => x.ManoObraUf);
        decimal herramientasAuto = herramientasGlobalBase * MarkupPolicy.ClampPct(settings.HerramientasPct) / 100m;
        if (herramientasAuto != 0m)
        {
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.Manual,
                Descripcion = $"Herramientas globales {settings.HerramientasPct:0.##}% sobre mano de obra",
                HerramientasUf = herramientasAuto,
                IsGlobalAutoCharge = true
            });
        }

        decimal mermasGlobalBase = result.Origenes
            .Where(x => x.MermaPctOverride < 0m)
            .Sum(x => x.MaterialesUf);
        decimal mermasAuto = mermasGlobalBase * MarkupPolicy.ClampPct(settings.MermasPct) / 100m;
        if (mermasAuto != 0m)
        {
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.Manual,
                Descripcion = $"Mermas globales {settings.MermasPct:0.##}% sobre materiales",
                MermasUf = mermasAuto,
                IsGlobalAutoCharge = true
            });
        }
    }

    private static void AddApprovedChangeOrders(DirectCostResult result, IEnumerable<ChangeOrderRow>? changeOrders)
    {
        foreach (var oc in changeOrders ?? Enumerable.Empty<ChangeOrderRow>())
        {
            if (!string.Equals(NormalizeStatus(oc.Estado), "APROBADA", StringComparison.OrdinalIgnoreCase)) continue;
            if (oc.DeltaCostoUf == 0m) continue;
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.OrdenCambio,
                Codigo = oc.Numero,
                Descripcion = oc.Titulo,
                Area = "OC aprobadas",
                OrdenesCambioUf = oc.DeltaCostoUf,
                VentaClp = oc.DeltaCostoUf
            });
        }
    }

    private static string NormalizeStatus(string? status)
    {
        return (status ?? string.Empty).Trim().ToUpperInvariant()
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U");
    }

    private static void AddMaterialTotal(DirectCostResult result, string? codigo, string? descripcion, string? unidad, decimal cantidad, decimal totalUf)
    {
        if (totalUf == 0m) return;
        result.Materiales.Add(new MaterialCostTotal
        {
            Codigo = (codigo ?? string.Empty).Trim(),
            Descripcion = (descripcion ?? string.Empty).Trim(),
            Unidad = (unidad ?? string.Empty).Trim(),
            Cantidad = cantidad,
            TotalUf = totalUf
        });
    }

    private static void AddOrigin(DirectCostResult result, CostOriginBreakdown origin)
    {
        result.MaterialesUf += origin.MaterialesUf;
        result.ManoObraUf += origin.ManoObraUf;
        result.EquiposUf += origin.EquiposUf;
        result.SubcontratosUf += origin.SubcontratosUf;
        result.HerramientasUf += origin.HerramientasUf;
        result.TransporteUf += origin.TransporteUf;
        result.MermasUf += origin.MermasUf;
        result.OrdenesCambioUf += origin.OrdenesCambioUf;
        result.SumVentaClp += origin.VentaClp;
        if (origin.HasRowLevelMarkup)
        {
            result.HasRowLevelMarkups = true;
        }
        result.Origenes.Add(origin);
    }

    // Total de fila en CLP (nombre histórico RowTotalUf).
    private static decimal RowTotalClp(CubicacionRow row) => row.Cantidad * (row.MatClpConFallback + row.MoClpConFallback);
    private static decimal RowTotalUf(CubicacionRow row) => RowTotalClp(row);

    // itemizadoById/areaByItemId se precomputan UNA vez por cálculo (ver
    // BuildItemizadoIndex/BuildAreaIndex) en vez de escanear itemizado/rows completos por cada
    // APU — con N partidas con APU y M filas/ítems, el escaneo lineal por llamada era O(N×M).
    private static decimal GetItemQuantity(IReadOnlyDictionary<string, ItemizadoRow> itemizadoById, string? itemId, DirectCostResult result)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 1m;
        if (!itemizadoById.TryGetValue(itemId, out var item)) return 1m;
        if (item.Cantidad == 0m)
        {
            result.Inconsistencias.Add(new BudgetInconsistencia
            {
                ItemId = itemId,
                Descripcion = item.Descripcion,
                Tipo = "CantidadCero",
                Detalle = $"Partida '{item.Item}' tiene cantidad 0; se retorna 0."
            });
            return 0m;
        }
        return item.Cantidad;
    }

    private static string InferArea(IReadOnlyDictionary<string, string> areaByItemId, string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return "General";
        return areaByItemId.TryGetValue(itemId, out var area) ? area : "General";
    }

    // GroupBy conserva el orden relativo original dentro de cada grupo, así que g.First() aquí
    // es exactamente el mismo elemento que devolvía el FirstOrDefault(...) anterior para ese
    // itemId — mismo resultado, sin reescanear la lista completa por cada partida con APU.
    private static IReadOnlyDictionary<string, ItemizadoRow> BuildItemizadoIndex(List<ItemizadoRow> itemList)
        => itemList
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> BuildAreaIndex(List<CubicacionRow> rows)
        => rows
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId) && !string.IsNullOrWhiteSpace(x.Area))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Area, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Devuelve los ItemId que tienen TANTO cubicación valorizada COMO APU con materiales o MO,
    /// en el modo CubicacionMasApuComplementarios. En ese modo, el APU silenciosamente ignora
    /// sus materiales/MO para esas partidas — lo cual puede ser correcto o un error involuntario.
    /// </summary>
    public static IReadOnlyList<string> DetectDoubleCountingRisk(
        IEnumerable<CubicacionRow>? cubicacion,
        IEnumerable<ApuRow>? apus,
        BudgetApuMode mode)
    {
        if (mode != BudgetApuMode.CubicacionMasApuComplementarios)
            return Array.Empty<string>();

        var itemsWithValuedCubicacion = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(HasValuedCubicacionRow)
            .Select(x => x.ItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (apus ?? Enumerable.Empty<ApuRow>())
            .Where(a => !string.IsNullOrWhiteSpace(a.ItemId)
                     && itemsWithValuedCubicacion.Contains(a.ItemId)
                     // CostoMaterial*/CostoMo* son sumas de PrecioUf de recursos, que ahora son CLP
                     && (a.CostoMaterialUf > 0m || a.CostoMoUf > 0m || a.CostoHhUf > 0m))
            .Select(a => a.ItemId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
