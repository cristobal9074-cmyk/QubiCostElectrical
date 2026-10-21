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
        // Las excepciones comerciales reemplazan orígenes técnicos completos por partida.
        // Se recalculan en ruta completa para evitar mezclar un origen incremental antiguo.
        if (previous == null || dirty.Count == 0 || settings.ApuMode == BudgetApuMode.Manual
            || (settings.CommercialPartidaRules?.Any(x => x != null && !x.UseProjectModel) ?? false))
        {
            var full = Calculate(cubicacion, settings, apus, itemizado, changeOrders);
            ClearDirtyItems();
            return full;
        }

        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).Where(x => x != null).ToList();
        var itemList = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();
        var itemizadoByPartidaId = itemList.Where(x => x.PartidaId != Guid.Empty).GroupBy(x => x.PartidaId).ToDictionary(x => x.Key, x => x.First());
        var commercialRulesByItemId = (settings.AllowPartidaCommercialOverrides ? settings.CommercialPartidaRules ?? new List<CommercialPartidaRule>() : new List<CommercialPartidaRule>())
            .Where(x => x != null && !x.UseProjectModel && x.PartidaId != Guid.Empty && itemizadoByPartidaId.ContainsKey(x.PartidaId))
            .GroupBy(x => itemizadoByPartidaId[x.PartidaId].Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
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
            AddMaterialTotalFromCubicacionRow(result, row, settings);
        }

        foreach (var apu in apuList.Where(a => !string.IsNullOrWhiteSpace(a.ItemId) && !dirty.Contains(a.ItemId)))
        {
            bool hasValuedCubicacion = rowsByItem.TryGetValue(apu.ItemId, out var rList) && rList.Any(HasValuedCubicacionRow);
            bool fullApu = settings.ApuMode == BudgetApuMode.ApuManda || (settings.ApuMode == BudgetApuMode.CubicacionManda && !hasValuedCubicacion);
            if (fullApu && apu.CostoMaterialUf > 0m)
            {
                decimal qty = GetItemQuantity(itemizadoById, apu.ItemId, result);
                if (qty > 0m)
                {
                    foreach (var r in apu.Recursos.Where(x => x.Tipo == ApuTipoRecurso.MAT))
                    {
                        var cantidad = r.Rendimiento * qty;
                        var totalUf = r.CostoUnitUf * qty;
                        if (totalUf > 0m)
                            AddMaterialTotal(result, r.CodigoMaterial, r.Descripcion, r.Unidad, cantidad, totalUf);
                    }
                }
            }
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
        var itemizadoByPartidaId = itemList.Where(x => x.PartidaId != Guid.Empty).GroupBy(x => x.PartidaId).ToDictionary(x => x.Key, x => x.First());
        var commercialRulesByItemId = (settings.AllowPartidaCommercialOverrides ? settings.CommercialPartidaRules : new List<CommercialPartidaRule>())
            .Where(x => x != null && !x.UseProjectModel && x.PartidaId != Guid.Empty && itemizadoByPartidaId.ContainsKey(x.PartidaId))
            .GroupBy(x => itemizadoByPartidaId[x.PartidaId].Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

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
            AddApprovedChangeOrders(result, changeOrders, settings);
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
            if (!string.IsNullOrWhiteSpace(row.ItemId) && commercialRulesByItemId.ContainsKey(row.ItemId))
                continue;
            if (settings.ApuMode == BudgetApuMode.ApuManda && !string.IsNullOrWhiteSpace(row.ItemId) && itemsWithApu.Contains(row.ItemId))
                continue;

            if (includeCubicacion)
                AddCubicacionRow(result, row, settings);
        }

        switch (settings.ApuMode)
        {
            case BudgetApuMode.CubicacionManda:
                foreach (var apu in apuList.Where(a => !commercialRulesByItemId.ContainsKey(a.ItemId) && !itemsWithValuedCubicacion.Contains(a.ItemId)))
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: true, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                break;

            case BudgetApuMode.ApuManda:
                foreach (var apu in apuList.Where(a => !commercialRulesByItemId.ContainsKey(a.ItemId)))
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: true, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                foreach (var row in rows.Where(x => (string.IsNullOrWhiteSpace(x.ItemId) || !itemsWithApu.Contains(x.ItemId)) && (string.IsNullOrWhiteSpace(x.ItemId) || !commercialRulesByItemId.ContainsKey(x.ItemId))))
                {
                    if (string.IsNullOrWhiteSpace(row.Codigo) && string.IsNullOrWhiteSpace(row.Descripcion) && row.Cantidad == 0m && row.MatClpConFallback == 0m && row.MoClpConFallback == 0m) continue;
                    AddCubicacionRow(result, row, settings);
                }
                break;

            case BudgetApuMode.CubicacionMasApuComplementarios:
                foreach (var apu in apuList.Where(a => !commercialRulesByItemId.ContainsKey(a.ItemId)))
                {
                    bool hasCubicacion = !string.IsNullOrWhiteSpace(apu.ItemId) && itemsWithValuedCubicacion.Contains(apu.ItemId);
                    AddApuRow(result, apu, GetItemQuantity(itemizadoById, apu.ItemId, result), fullApu: !hasCubicacion, area: InferArea(areaByItemId, apu.ItemId), settings: settings);
                }
                break;
        }

        foreach (var pair in commercialRulesByItemId)
            AddCommercialRule(result, pair.Key, itemizadoById[pair.Key], pair.Value);

        AddGlobalPercentages(result, settings);
        AddApprovedChangeOrders(result, changeOrders, settings);
        return result;
    }

    private static void AddCommercialRule(DirectCostResult result, string itemId, ItemizadoRow item, CommercialPartidaRule rule)
    {
        var errors = rule.Validate();
        if (errors.Count > 0)
        {
            result.Inconsistencias.Add(new BudgetInconsistencia { ItemId = itemId, Descripcion = item.Descripcion, Tipo = "ModeloComercialIncompleto", Detalle = string.Join(" ", errors) });
            return;
        }

        decimal quantity = item.Cantidad != 0m ? item.Cantidad : item.CantidadOriginal;
        if (quantity == 0m) quantity = 1m;
        var origin = new CostOriginBreakdown { Origen = BudgetCostOrigin.Manual, ItemId = itemId, Descripcion = $"{rule.Model.DisplayName()}: {item.Descripcion}", Area = "Modelo comercial" };
        switch (rule.Model)
        {
            case CommercialCostingModel.UnitPrice:
                origin.MaterialesClp = quantity * rule.UnitPriceClp;
                origin.VentaClp = origin.MaterialesClp;
                break;
            case CommercialCostingModel.LumpSum:
                origin.MaterialesClp = rule.LumpSumClp;
                origin.VentaClp = rule.LumpSumClp;
                break;
            case CommercialCostingModel.TimeAndMaterials:
                origin.ManoObraClp = rule.LaborHours * rule.LaborHourlyRateClp;
                origin.MaterialesClp = rule.MaterialsClp;
                origin.EquiposClp = rule.ReimbursableExpensesClp;
                origin.VentaClp = origin.ManoObraClp + origin.MaterialesClp + origin.EquiposClp;
                break;
            default:
                return;
        }
        AddOrigin(result, origin);
    }

    private static void AddCubicacionRow(DirectCostResult result, CubicacionRow row, BudgetSettings settings)
    {
        // Fuente CLP. Los campos *Uf de CostOriginBreakdown conservan el nombre histórico
        // pero almacenan pesos; preferir alias *Clp en código nuevo.
        decimal material = row.Cantidad * row.MatClpConFallback;
        decimal manoObra = row.Cantidad * row.MoClpConFallback;
        decimal negotiatedDiscountPct = LookupNegotiatedDiscountPct(settings.NegotiatedDiscounts, row.Codigo);
        if (negotiatedDiscountPct > 0m)
            material *= 1m - negotiatedDiscountPct / 100m;
        decimal total = material + manoObra;
        string tipo = (row.Tipo ?? "MAT").Trim().ToUpperInvariant();

        // C4: la venta de la línea se calcula DIRECTO en CLP (precio unitario × factor × cantidad).
        // Antes se hacía CLP → UF (redondeo a 3 decimales) → CLP, lo que introducía ±20 CLP por
        // línea y dependía de un valor UF de respaldo hardcodeado. La UF es solo presentación.
        bool usaFactoresFamilia = settings.Markup?.FactoresPresentacionPorFamilia ?? false;

        // C-1: venta = costo_origen × factor de presentación de la familia. El mapa por familia
        // (BudgetSettings.FromProject, desde Variables) es la fuente de verdad; el factor suelto
        // de la fila (GgUtFactor) solo se usa como respaldo si la categoría no está mapeada.
        // H-7: así una familia fijada deliberadamente en 1.0 no cae al respaldo.
        decimal factorPresentacion = 1m;
        if (usaFactoresFamilia)
        {
            factorPresentacion = settings.TryResolvePresentationFactor(row.Categoria, out var famFactor)
                ? famFactor
                : (row.GgUtFactor != 1m ? row.GgUtFactor : settings.DefaultPresentationFactor);
        }

        var origin = new CostOriginBreakdown
        {
            Origen = IsLikelyTemplate(row) ? BudgetCostOrigin.Plantilla : BudgetCostOrigin.Cubicacion,
            ItemId = row.ItemId,
            Codigo = row.Codigo,
            Descripcion = row.Descripcion,
            Area = string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area.Trim(),
            MermaPctOverride = row.MermaPctOverride,
            HerramientasPctOverride = row.HerramientasPctOverride,
            VentaClp = row.PrecioUnitClpConFallback * factorPresentacion * row.Cantidad,
            // C-1: el flag ahora significa "el proyecto usa factores por familia"; toda línea
            // lleva factor (aunque sea el default del proyecto), así la venta es reproducible.
            HasRowLevelMarkup = usaFactoresFamilia
        };

        // Paridad Excel: un bucket de costo material (equipo, subcontrato, herramienta, transporte,
        // merma) recibe SOLO el material de la línea. La M.O. de esa línea sigue en el bucket de mano
        // de obra, porque en la ruta de costos Excel la M.O. del proyecto la define la dotación
        // (ManoObraDotacionClp) y el motor solo la resta de la ejecución cuando está en ese bucket.
        // Si la M.O. quedara dentro del bucket de equipo, se sumaría dos veces al costo de ejecución.
        switch (tipo)
        {
            case "EQ":
            case "EQUIPO":
            case "EQUIPOS":
                origin.EquiposClp = material;
                origin.ManoObraClp = manoObra;
                break;
            case "SC":
            case "SUBCONTRATO":
            case "SUBCONTRATOS":
                origin.SubcontratosClp = material;
                origin.ManoObraClp = manoObra;
                break;
            case "HERR":
            case "HERRAMIENTA":
            case "HERRAMIENTAS":
                origin.HerramientasClp = material;
                origin.ManoObraClp = manoObra;
                break;
            case "TR":
            case "TRANSPORTE":
            case "TRANSPORTES":
                origin.TransporteClp = material;
                origin.ManoObraClp = manoObra;
                break;
            case "MERMA":
            case "MERMAS":
                origin.MermasClp = material;
                origin.ManoObraClp = manoObra;
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

        AddMaterialTotalFromCubicacionRow(result, row, settings);

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

    private static void AddMaterialTotalFromCubicacionRow(DirectCostResult result, CubicacionRow row, BudgetSettings settings)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Codigo)) return;
        decimal material = MaterialTotalForMaterialSummary(row);
        decimal negotiatedDiscountPct = LookupNegotiatedDiscountPct(settings.NegotiatedDiscounts, row.Codigo);
        if (negotiatedDiscountPct > 0m)
            material *= 1m - negotiatedDiscountPct / 100m;
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

    private static decimal LookupNegotiatedDiscountPct(List<NegotiatedDiscountRow>? discounts, string? codigo)
    {
        if (discounts == null || discounts.Count == 0 || string.IsNullOrWhiteSpace(codigo)) return 0m;
        var match = discounts.FirstOrDefault(d => string.Equals((d.Codigo ?? "").Trim(), codigo.Trim(), StringComparison.OrdinalIgnoreCase));
        return match != null ? MarkupPolicy.ClampPct(match.DescuentoPct) : 0m;
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

        if (fullApu && apu.TieneMermaEnDosNiveles)
        {
            result.Inconsistencias.Add(new BudgetInconsistencia
            {
                ItemId = apu.ItemId,
                Descripcion = apu.Descripcion,
                Tipo = "MermaDobleNivel",
                Detalle = $"APU '{apu.Descripcion}': hay recursos con merma propia y además merma de APU " +
                          $"({apu.MermaPct:0.##}%). La merma de APU solo aplica a los materiales sin merma propia."
            });
        }

        decimal materiales = fullApu ? apu.CostoMaterialUf * cantidadPartida : 0m;
        decimal manoObra = fullApu ? (apu.CostoMoUf + apu.CostoHhUf) * cantidadPartida : 0m;
        decimal equipos = apu.CostoEquipoUf * cantidadPartida;
        decimal subcontratos = apu.CostoScUf * cantidadPartida;

        decimal herramientas;
        decimal transporte;
        decimal mermas;
        if (fullApu)
        {
            herramientas = apu.HerramientasCalculadoUf * cantidadPartida;
            transporte = apu.TransporteUf * cantidadPartida;
            mermas = (apu.MermaUf + apu.ImprevistosUf) * cantidadPartida;
        }
        else
        {
            // C1: en modo complementario la cubicación aporta materiales y MO; el APU solo suma
            // equipos, subcontratos y los recursos HERR/TR explícitos. Herramientas, transporte,
            // mermas e imprevistos porcentuales del APU se calculaban sobre bases (CostoMoUf,
            // CostoMaterialUf, CostoDirectoUf) que incluyen materiales/MO que NO entran aquí:
            // eso inflaba el complemento. Ahora solo entran los recursos explícitos y los
            // imprevistos de alza se aplican sobre lo que realmente aporta el complemento.
            herramientas = apu.CostoHerramientasRecursoUf * cantidadPartida;
            transporte = apu.CostoTransporteRecursoUf * cantidadPartida;
            decimal baseComplementaria = equipos + subcontratos + herramientas + transporte;
            mermas = baseComplementaria * MarkupPolicy.ClampPct(apu.ImprevistosAlzaPct) / 100m;
        }

        if (!fullApu && equipos == 0m && subcontratos == 0m && herramientas == 0m && transporte == 0m && mermas == 0m) return;

        bool usaFactoresFamilia = settings.Markup?.FactoresPresentacionPorFamilia ?? false;
        decimal factorPresentacion = usaFactoresFamilia ? settings.ResolvePresentationFactor(area) : 1m;
        decimal costoAporteApu = fullApu
            ? apu.PrecioUnitUf * cantidadPartida
            : equipos + subcontratos + herramientas + transporte + mermas;

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
            // C4: apu.PrecioUnitUf ya está en CLP (migración de moneda del motor).
            // C-1: cuando el proyecto usa factores por familia, el APU también los lleva
            // (por el área de la partida; área desconocida → factor por defecto del proyecto).
            VentaClp = costoAporteApu * factorPresentacion,
            HasRowLevelMarkup = usaFactoresFamilia
        });
    }

    private static void AddGlobalPercentages(DirectCostResult result, BudgetSettings settings)
    {
        decimal herramientasGlobalBase = result.Origenes
            .Where(x => x.HerramientasPctOverride < 0m)
            .Sum(x => x.ManoObraUf);
        decimal herramientasAuto = herramientasGlobalBase * MarkupPolicy.ClampPct(settings.HerramientasPct) / 100m;
        bool usaFactoresFamilia = settings.Markup?.FactoresPresentacionPorFamilia ?? false;
        // Con factores de presentación la venta es el detalle cubicado. Ferretería, herramientas
        // y mermas globales son estimaciones de costo (hoja COSTOS), no líneas de venta.
        decimal ventaFactor = 0m;
        if (herramientasAuto != 0m)
        {
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.Manual,
                Descripcion = $"Herramientas globales {settings.HerramientasPct:0.##}% sobre mano de obra",
                HerramientasUf = herramientasAuto,
                VentaClp = herramientasAuto * ventaFactor,
                HasRowLevelMarkup = usaFactoresFamilia,
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
                VentaClp = mermasAuto * ventaFactor,
                HasRowLevelMarkup = usaFactoresFamilia,
                IsGlobalAutoCharge = true
            });
        }

        if (settings.FerreteriaActiva && settings.FerreteriaPct > 0m)
        {
            decimal baseMateriales = result.Origenes
                .Where(x => !x.IsGlobalAutoCharge)
                .Sum(x => x.MaterialesUf);
            decimal ferreteria = baseMateriales * MarkupPolicy.ClampPct(settings.FerreteriaPct) / 100m;
            if (ferreteria != 0m)
            {
                AddOrigin(result, new CostOriginBreakdown
                {
                    Origen = BudgetCostOrigin.Manual,
                    Descripcion = $"Ferretería {settings.FerreteriaPct:0.##}% sobre materiales",
                    MaterialesUf = ferreteria,
                    VentaClp = ferreteria * ventaFactor,
                    HasRowLevelMarkup = usaFactoresFamilia,
                    IsGlobalAutoCharge = true
                });
            }
        }
    }

    private static void AddApprovedChangeOrders(DirectCostResult result, IEnumerable<ChangeOrderRow>? changeOrders, BudgetSettings settings)
    {
        bool usaFactoresFamilia = settings.Markup?.FactoresPresentacionPorFamilia ?? false;
        decimal factorPresentacion = usaFactoresFamilia ? settings.DefaultPresentationFactor : 1m;
        foreach (var oc in changeOrders ?? Enumerable.Empty<ChangeOrderRow>())
        {
            var st = NormalizeStatus(oc.Estado);
            if (!string.Equals(st, "APROBADA", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(st, "APROBADO", StringComparison.OrdinalIgnoreCase))
                continue;
            if (oc.DeltaCostoUf == 0m) continue;
            AddOrigin(result, new CostOriginBreakdown
            {
                Origen = BudgetCostOrigin.OrdenCambio,
                Codigo = oc.Numero,
                Descripcion = oc.Titulo,
                Area = "OC aprobadas",
                OrdenesCambioUf = oc.DeltaCostoUf,
                // C-1: la OC aprobada también lleva el factor por defecto del proyecto.
                VentaClp = oc.DeltaCostoUf * factorPresentacion,
                HasRowLevelMarkup = usaFactoresFamilia
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

}
