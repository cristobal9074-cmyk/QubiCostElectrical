using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class FinalReviewService : IFinalReviewService
{
    public bool IsAreaNotAssigned(string? area)
    {
        if (string.IsNullOrWhiteSpace(area)) return true;
        string clean = Normalize(area);
        return clean == "GENERAL" || clean == "SIN AREA" || clean == "NO ASIGNADA" || clean == "NO ASIGNADO" || clean == "N/A" || clean == "NA";
    }

    private static void AddEmptyOrIncompletePartidas(List<FinalReviewIssue> issues, IReadOnlyList<ItemizadoRow> itemList, IReadOnlyList<CubicacionRow> cubList, ISet<string> overriddenItemIds)
    {
        // Agrupar por ItemId UNA vez: antes se filtraba cubList completo por CADA partida
        // (O(N*M) — con miles de recursos y cientos de partidas eso es costoso en cada
        // export/revisión final).
        var rowsByItem = cubList
            .GroupBy(x => S(x.ItemId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CubicacionRow>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemList.Where(x => !x.EsTitulo))
        {
            if (item.ExcluidoCubicacion) continue;
            if (overriddenItemIds.Contains(S(item.Id))) continue;
            var rows = rowsByItem.TryGetValue(S(item.Id), out var found) ? found : Array.Empty<CubicacionRow>();
            bool hasAnyInput = rows.Any(HasAnyInput);
            bool hasCompletedLine = rows.Any(IsCompletedCubicacionLine);
            bool hasIncompleteLine = rows.Any(IsIncompleteCubicacionLine);

            if (!hasAnyInput)
            {
                issues.Add(new FinalReviewIssue($"EMPTY|{S(item.Id)}", FinalReviewSeverity.Blocker, FinalReviewCategory.EmptyPartida, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), 1, "La partida no tiene cubicación.", "Abrir la partida y agregar materiales/cantidades antes de exportar. Si no se cubicará (fuera de alcance), márquela «No cubicar» en Itemizado."));
            }
            else if (!hasCompletedLine || hasIncompleteLine)
            {
                issues.Add(new FinalReviewIssue($"INCOMPLETE|{S(item.Id)}", FinalReviewSeverity.Blocker, FinalReviewCategory.EmptyPartida, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), Math.Max(1, rows.Count(IsIncompleteCubicacionLine)), "La partida tiene cubicación incompleta.", "Revisar código, descripción y cantidades; al menos una línea debe tener código y cantidad mayor que cero."));
            }
        }
    }

    private static void AddZeroQuantities(List<FinalReviewIssue> issues, IReadOnlyList<CubicacionRow> cubList, IReadOnlyDictionary<string, ItemizadoRow> itemById)
    {
        var groups = cubList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) && x.Cantidad == 0m)
            .GroupBy(x => S(x.ItemId) + "|" + S(x.Codigo), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var first = group.First();
            itemById.TryGetValue(S(first.ItemId), out var item);
            issues.Add(new FinalReviewIssue($"ZEROQTY|{S(first.ItemId)}|{S(first.Codigo)}", FinalReviewSeverity.Blocker, FinalReviewCategory.ZeroQuantity, S(first.ItemId), item?.Item ?? "", item?.Descripcion ?? "Partida no encontrada", S(first.Codigo), S(first.Descripcion), group.Count(), "Existe una línea con código pero cantidad cero.", "Corregir cantidad, eliminar línea o justificarla fuera de la oferta final."));
        }
    }

    private static void AddMaterialsWithoutPrice(List<FinalReviewIssue> issues, IReadOnlyList<CubicacionRow> cubList, IReadOnlyList<MaterialRow> materialList, IReadOnlyDictionary<string, ItemizadoRow> itemById)
    {
        var materialByCode = materialList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var groups = cubList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) && x.Cantidad > 0m)
            .Where(x => IsRowWithoutPrice(x, materialByCode))
            .GroupBy(x => S(x.ItemId) + "|" + S(x.Codigo).Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var first = group.First();
            itemById.TryGetValue(S(first.ItemId), out var item);
            string source = materialByCode.ContainsKey(S(first.Codigo).Trim()) ? "lista de precios" : "cubicación sin precio en lista";
            issues.Add(new FinalReviewIssue($"PRICE|{S(first.ItemId)}|{S(first.Codigo)}", FinalReviewSeverity.Blocker, FinalReviewCategory.MaterialWithoutPrice, S(first.ItemId), item?.Item ?? "", item?.Descripcion ?? "Partida no encontrada", S(first.Codigo), S(first.Descripcion), group.Count(), $"Material usado con precio unitario cero o no encontrado en {source}.", "Abrir la lista de precios y completar MAT/M.O. en UF o en la moneda del proyecto."));
        }
    }

    private void AddAreasNotAssigned(List<FinalReviewIssue> issues, IReadOnlyList<CubicacionRow> cubList, IReadOnlyDictionary<string, ItemizadoRow> itemById)
    {
        var groups = cubList
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) && x.Cantidad > 0m)
            .Where(x => IsAreaNotAssigned(x.Area))
            .GroupBy(x => S(x.ItemId), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var first = group.First();
            itemById.TryGetValue(S(first.ItemId), out var item);
            issues.Add(new FinalReviewIssue($"AREA|{S(first.ItemId)}", FinalReviewSeverity.Warning, FinalReviewCategory.AreaNotAssigned, S(first.ItemId), item?.Item ?? "", item?.Descripcion ?? "Partida no encontrada", "", item?.Descripcion ?? "", group.Count(), $"{group.Count():N0} línea(s) cubicadas siguen en área 'General' o sin área.", "Asignar un área real: Servicios Comunes, Departamentos, Subterráneo, Corrientes Débiles, etc."));
        }
    }

    private static void AddMissingOrIncompleteApus(List<FinalReviewIssue> issues, IReadOnlyList<ItemizadoRow> itemList, IReadOnlyList<CubicacionRow> cubList, IReadOnlyList<ApuRow> apuList, BudgetSettings? settings, ISet<string> overriddenItemIds)
    {
        var apuByItem = apuList
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Igual que en AddEmptyOrIncompletePartidas: índice por ItemId en vez de escanear
        // cubList completo por cada partida dentro del loop.
        var hasValuedCubicacionByItem = new HashSet<string>(
            cubList.Where(x => !string.IsNullOrWhiteSpace(x.Codigo) && x.Cantidad > 0m).Select(x => S(x.ItemId)),
            StringComparer.OrdinalIgnoreCase);

        bool apuPolicyRequiresApu = settings != null && settings.ApuMode == BudgetApuMode.ApuManda;
        foreach (var item in itemList.Where(x => !x.EsTitulo))
        {
            if (item.ExcluidoCubicacion) continue;
            if (overriddenItemIds.Contains(S(item.Id))) continue;
            apuByItem.TryGetValue(S(item.Id), out var apu);
            if (apu == null || apu.Recursos == null || apu.Recursos.Count == 0)
            {
                if (apuPolicyRequiresApu)
                {
                    var severity = hasValuedCubicacionByItem.Contains(S(item.Id))
                        ? FinalReviewSeverity.Blocker
                        : FinalReviewSeverity.Critical;
                    issues.Add(new FinalReviewIssue($"APU|{S(item.Id)}", severity, FinalReviewCategory.MissingApu, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), 1, "La partida no tiene APU requerido por la política del proyecto.", "Crear APU o cambiar el modo del proyecto a Cubicación Directa."));
                }
                else
                {
                    // En modo estándar o complementario, no tener APU no bloquea la oferta
                    issues.Add(new FinalReviewIssue($"APU|{S(item.Id)}", FinalReviewSeverity.Info, FinalReviewCategory.MissingApu, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), 1, "Partida presupuestada por cubicación directa (sin APU complementario).", "Opcional: Crear APU si el cliente solicita análisis detallado."));
                }
                continue;
            }

            int missingPrice = apu.Recursos.Count(r => r.PrecioBaseUf <= 0m);
            int badRend = apu.Recursos.Count(r => r.Rendimiento <= 0m || ((r.Tipo == ApuTipoRecurso.MO || r.Tipo == ApuTipoRecurso.HH) && r.HHUnitarias <= 0m));
            if (missingPrice > 0 || badRend > 0)
            {
                var severity = apuPolicyRequiresApu || !hasValuedCubicacionByItem.Contains(S(item.Id))
                    ? FinalReviewSeverity.Blocker
                    : FinalReviewSeverity.Warning;
                issues.Add(new FinalReviewIssue($"APU_INCOMPLETE|{S(item.Id)}", severity, FinalReviewCategory.ApuIncomplete, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), missingPrice + badRend, "El APU tiene recursos sin precio o rendimiento/HH inválido.", "Abrir APU profesional y corregir precio, cuadrilla, rendimiento y HH unitarias."));
            }
        }
    }

    private static void AddCommercialModelIssues(List<FinalReviewIssue> issues, IReadOnlyList<ItemizadoRow> items, IReadOnlyList<CommercialPartidaRule> rules)
    {
        var itemByPartida = items.Where(x => x.PartidaId != Guid.Empty).GroupBy(x => x.PartidaId).ToDictionary(x => x.Key, x => x.First());
        foreach (var rule in rules.Where(x => x != null && !x.UseProjectModel && x.Model != CommercialCostingModel.ApuDirectCostWithCommercialFactors))
        {
            if (!itemByPartida.TryGetValue(rule.PartidaId, out var item)) continue;
            var errors = rule.Validate(); if (errors.Count == 0) continue;
            issues.Add(new FinalReviewIssue($"COMMERCIAL|{S(item.Id)}", FinalReviewSeverity.Blocker, FinalReviewCategory.CommercialModelIncomplete, S(item.Id), S(item.Item), S(item.Descripcion), "", S(item.Descripcion), errors.Count, string.Join(" ", errors), "Abrir Modelo comercial de la partida y completar los datos requeridos."));
        }
    }

    private void AddFinancialGuards(List<FinalReviewIssue> issues, ProjectInfoRow? proyecto, BudgetSettings? settings, IReadOnlyList<CubicacionRow> cubList, IReadOnlyList<ApuRow> apuList, IReadOnlyList<ItemizadoRow> itemList, IEnumerable<ChangeOrderRow>? changeOrders, ISet<string> overriddenItemIds)
    {
        proyecto ??= new ProjectInfoRow();
        settings ??= BudgetSettings.FromProject(proyecto);
        var currency = (settings.Currency?.MonedaPresupuesto ?? proyecto.MonedaPresupuesto ?? "CLP").Trim().ToUpperInvariant();

        // CLP no requiere UF — solo USD necesita ambas tasas para la conversión de moneda
        if (currency == "USD" && (settings.Currency?.ValorUfClp ?? proyecto.ValorUfClp) <= 0m)
            issues.Add(new FinalReviewIssue("CURRENCY|UF", FinalReviewSeverity.Blocker, FinalReviewCategory.MissingCurrencyUf, "", "", "Datos del proyecto", "UF", "Valor UF", 1, "La exportación en USD requiere valor UF, pero no está configurado.", "Configurar valor UF vigente antes de exportar en USD."));

        if (currency == "USD" && (settings.Currency?.TipoCambioUsdClp ?? proyecto.TipoCambioUsdClp) <= 0m)
            issues.Add(new FinalReviewIssue("CURRENCY|USD", FinalReviewSeverity.Blocker, FinalReviewCategory.MissingCurrencyUf, "", "", "Datos del proyecto", "USD", "Tipo de cambio USD", 1, "La exportación USD requiere tipo de cambio USD/CLP.", "Configurar tipo de cambio USD/CLP en Monedas."));

        if (settings.Markup == null || settings.Markup.IvaPct < 0m || settings.Markup.IvaPct > 30m)
            issues.Add(new FinalReviewIssue("IVA|PCT", FinalReviewSeverity.Blocker, FinalReviewCategory.IvaInconsistent, "", "", "Datos del proyecto", "IVA", "IVA", 1, "El IVA configurado está fuera de rango razonable.", "Configurar IVA entre 0% y 30%. Para Chile, usar 19%."));

        foreach (var markupError in (settings.Markup?.Validate() ?? Array.Empty<string>()))
            issues.Add(new FinalReviewIssue("MARKUP|INVALID", FinalReviewSeverity.Blocker, FinalReviewCategory.MarkupInvalid, "", "", "Política de recargo", "", "Markup", 1, markupError, "Corregir el porcentaje a un valor entre 0% y 100%."));

        var cost = _engineFactory().Calculate(cubList, settings, apuList, itemList, changeOrders);

        // Inconsistencias que el motor detecta durante el cálculo (cantidad 0, merma en dos
        // niveles, modelo comercial incompleto, etc.). Antes se calculaban pero nadie las leía.
        foreach (var grupo in (cost.Inconsistencias ?? new List<BudgetInconsistencia>())
                     .Where(x => x != null && !overriddenItemIds.Contains(S(x.ItemId)))
                     .GroupBy(x => (x.Tipo, x.ItemId, x.Detalle)))
        {
            var i = grupo.First();
            var sev = i.Tipo is "CantidadCero" or "ModeloComercialIncompleto" or "UtilidadNegativa"
                ? FinalReviewSeverity.Blocker
                : FinalReviewSeverity.Warning;
            issues.Add(new FinalReviewIssue(
                $"ENGINE|{i.Tipo}|{S(i.ItemId)}", sev, FinalReviewCategory.EngineInconsistency,
                S(i.ItemId), "", S(i.Descripcion), "", S(i.Descripcion), grupo.Count(),
                S(i.Detalle),
                "Revisar la partida/APU indicada antes de exportar."));
        }

        // Política de cierre comercial: sus advertencias de fondo ("margen negativo" / "no cubre
        // el costo de ejecución") bloquean la exportación; el resto son advertencias informativas.
        var closingAdvertencias = cost.ClosingPolicyResult?.Advertencias ?? new List<string>();
        foreach (var grupo in closingAdvertencias.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x))
        {
            var adv = grupo.Key;
            bool blocking = adv.IndexOf("margen negativo", StringComparison.OrdinalIgnoreCase) >= 0
                || adv.IndexOf("no cubre", StringComparison.OrdinalIgnoreCase) >= 0;
            issues.Add(new FinalReviewIssue(
                $"CLOSINGPOLICY|{adv.GetHashCode()}",
                blocking ? FinalReviewSeverity.Blocker : FinalReviewSeverity.Warning,
                FinalReviewCategory.EngineInconsistency,
                "", "", "Política de cierre", "", S(cost.ClosingPolicyResult?.Explicacion), grupo.Count(),
                adv,
                "Revisar la política de cierre comercial y sus parámetros antes de exportar."));
        }

        if (cost.TotalVentaUf <= 0m)
            issues.Add(new FinalReviewIssue("TOTAL|ZERO", FinalReviewSeverity.Blocker, FinalReviewCategory.ZeroTotalSale, "", "", "Presupuesto", "", "Total venta", 1, "El total de venta es cero.", "Revisar cubicación, APU, precios y políticas de recargo antes de exportar."));
        if (cost.TotalVentaUf > 0m && cost.MargenRealPct < 0m)
            issues.Add(new FinalReviewIssue("MARGIN|NEGATIVE", FinalReviewSeverity.Blocker, FinalReviewCategory.NegativeMargin, "", "", "Presupuesto", "", "Margen real", 1, "El margen real es negativo.", "Revisar descuento, utilidad, GG, riesgo y costos directos."));
    }

    private static void AddPriceTraceabilityWarnings(List<FinalReviewIssue> issues, IReadOnlyList<ApuRow> apuList)
    {
        foreach (var apu in apuList)
        {
            var stale = (apu.Recursos ?? new List<ApuRecursoRow>())
                .Where(x => x.FechaPrecio != default && x.FechaPrecio < DateTime.Today.AddMonths(-6))
                .Take(5)
                .ToList();
            if (stale.Count > 0)
            {
                issues.Add(new FinalReviewIssue($"PRICE_STALE|{S(apu.ItemId)}", FinalReviewSeverity.Warning, FinalReviewCategory.PriceTraceability, S(apu.ItemId), "", S(apu.Descripcion), "", "Precio vencido", stale.Count, "El APU tiene precios con más de 6 meses de antigüedad.", "Actualizar fuente, fecha de precio y proveedor antes de ofertar."));
            }
        }
    }

    private void AddAntecedentesTecnicosWarnings(
        List<FinalReviewIssue> issues,
        ProjectInfoRow? proyecto,
        IEnumerable<AntecedenteTecnicoRow>? antecedentes,
        IEnumerable<MeasurementLineRow>? mediciones)
    {
        var antList = (antecedentes ?? Enumerable.Empty<AntecedenteTecnicoRow>()).ToList();
        if (proyecto != null
            && (proyecto.FechaCorteAntecedentes == null || proyecto.FechaCorteAntecedentes == DateTime.MinValue)
            && antList.Count > 0)
        {
            issues.Add(new FinalReviewIssue(
                "ANTECEDENTES|FECHA_CORTE",
                FinalReviewSeverity.Warning,
                FinalReviewCategory.FechaCorteAntecedentesNoDefinida,
                "", "", "", "", "",
                1,
                "Hay antecedentes técnicos registrados pero no se definió fecha de corte.",
                "Abrir Proyecto → Antecedentes técnicos y registrar la fecha de corte del mandante."));
        }

        foreach (var row in antList.Where(x => x.EsPendienteMandante))
        {
            string label = string.IsNullOrWhiteSpace(row.Codigo) ? row.Tipo : $"{row.Tipo} {row.Codigo}";
            issues.Add(new FinalReviewIssue(
                $"ANTECEDENTES|PEND|{S(row.Id)}",
                FinalReviewSeverity.Warning,
                FinalReviewCategory.AntecedentePendienteMandante,
                "", "", label, S(row.Revision), S(row.Descripcion),
                1,
                "Antecedente marcado como pendiente del mandante.",
                "Resolver consulta, addenda o aclaración antes de cerrar la oferta."));
        }

        foreach (var (line, problema) in _antecedentes.FindMedicionesSinRegistro(mediciones, antList))
        {
            issues.Add(new FinalReviewIssue(
                $"ANTECEDENTES|MED|{S(line.Id)}",
                FinalReviewSeverity.Warning,
                FinalReviewCategory.MedicionSinRegistroPlano,
                S(line.ItemId), "", S(line.Plano), S(line.RevisionPlano), S(line.Nota),
                1,
                problema,
                "Registrar el plano en Antecedentes técnicos o corregir la revisión en mediciones."));
        }
    }

    private static bool IsRowWithoutPrice(CubicacionRow row, IReadOnlyDictionary<string, MaterialRow> materialByCode)
    {
        bool rowHasPrice = row.MatUf > 0m || row.MoUf > 0m;
        if (rowHasPrice) return false;
        if (!materialByCode.TryGetValue(S(row.Codigo).Trim(), out var material)) return true;
        return material.MatUf <= 0m && material.MoUf <= 0m;
    }

    private static bool HasAnyInput(CubicacionRow row)
    {
        return !string.IsNullOrWhiteSpace(row.Codigo) || !string.IsNullOrWhiteSpace(row.Descripcion) || row.Cantidad != 0m || row.MatUf != 0m || row.MoUf != 0m;
    }

    private static bool IsCompletedCubicacionLine(CubicacionRow row) => !string.IsNullOrWhiteSpace(row.Codigo) && row.Cantidad > 0m;

    private static bool IsIncompleteCubicacionLine(CubicacionRow row)
    {
        if (!HasAnyInput(row)) return false;
        return string.IsNullOrWhiteSpace(row.Codigo) || row.Cantidad < 0m;
    }

    private static int SeveritySort(FinalReviewSeverity severity) => severity switch
    {
        FinalReviewSeverity.Blocker => 0,
        FinalReviewSeverity.Critical => 1,
        FinalReviewSeverity.Warning => 2,
        _ => 3
    };

    private static int CategorySort(FinalReviewCategory category) => category switch
    {
        FinalReviewCategory.EmptyPartida => 0,
        FinalReviewCategory.ZeroQuantity => 1,
        FinalReviewCategory.MaterialWithoutPrice => 2,
        FinalReviewCategory.MissingCurrencyUf => 3,
        FinalReviewCategory.ZeroTotalSale => 4,
        FinalReviewCategory.NegativeMargin => 5,
        FinalReviewCategory.IvaInconsistent => 6,
        FinalReviewCategory.ApuIncomplete => 7,
        FinalReviewCategory.MissingApu => 8,
        FinalReviewCategory.AreaNotAssigned => 9,
        FinalReviewCategory.MarkupInvalid => 10,
        _ => 20
    };

    private static string NaturalKey(string value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().PadLeft(20, '0');
    private static bool Same(string? left, string? right) => string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value)
    {
        return (value ?? "").Trim()
            .Replace("á", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("í", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ó", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ú", "u", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();
    }
}
