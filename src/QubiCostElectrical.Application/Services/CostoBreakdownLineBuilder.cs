namespace QubiCostElectrical;

public sealed record CostoBreakdownLine(
    string Label,
    decimal AmountUf,
    string Explanation,
    string Kind);

/// <summary>Proyección del desglose de venta (Excel vs clásico) sin dependencia WPF.</summary>
public static class CostoBreakdownLineBuilder
{
    public static IReadOnlyList<CostoBreakdownLine> BuildLines(CostBreakdown cost, ProjectInfoRow project)
    {
        var lines = new List<CostoBreakdownLine>(28);
        var excel = project.PoliticaCostosExcel;

        lines.Add(new("Mano de obra", cost.ManoObraUf,
            excel ? "MO efectiva (módulo o cubicación/APU)" : "Suma de mano de obra valorizada desde APU", "regular"));
        lines.Add(new("Materiales", cost.MaterialesUf, "Suma de materiales valorizados desde cubicación", "regular"));
        lines.Add(new("Equipos", cost.EquiposUf, "Equipos desde APU / costos complementarios", "regular"));
        if (excel || cost.GastosOperacionalesUf > 0m)
            lines.Add(new("Gastos operacionales", cost.GastosOperacionalesUf, "GO 3.1–3.6 del módulo Costos", "regular"));
        lines.Add(new("Subcontratos", cost.SubcontratosUf, "Subcontratos desde APU", "regular"));
        lines.Add(new("Herramientas", cost.HerramientasUf, "Herramientas cuando aplica", "regular"));
        lines.Add(new("Mermas", cost.MermasUf, "Mermas cuando aplica", "regular"));
        lines.Add(new("Transporte", cost.TransporteUf, "Transporte desde APU", "regular"));
        lines.Add(new("Órdenes de cambio", cost.OrdenesCambioUf, "OC aprobadas — separadas de subcontratos", "regular"));

        lines.Add(new(excel ? "Costo de ejecución" : "Costo directo", cost.CostoDirectoUf,
            excel ? "Mat + MO + Equipos + GO + complementarios" : "Costo base del presupuesto", "total"));

        lines.Add(new("Riesgo", cost.RiesgoUf, "Imprevistos / riesgo comercial", "regular"));

        if (excel)
        {
            lines.Add(new($"Gastos generales ({project.GastosGeneralesPct:N1}%)", cost.GastosGeneralesUf,
                "GG sobre Mat + MO + GO (sin equipos) · Excel COSTOS", "coef"));
            lines.Add(new($"Utilidades ({project.UtilidadPct:N1}%)", cost.UtilidadAditivaUf,
                "Utilidad aditiva sobre Mat + MO + GO · Excel COSTOS", "coef"));
            lines.Add(new($"VA sobre equipos ({project.ValorAgregadoEquiposPct:N1}%)", cost.ValorAgregadoEquiposUf,
                "Valor agregado solo sobre equipos · Excel COSTOS", "coef"));
            lines.Add(new("Costo total proyecto", cost.CostoProyectoUf,
                "Ejecución + GG + UT + VA", "subtotal"));
            lines.Add(new("Diferencia por factores", cost.DiferenciaFactoresUf,
                "Venta − costo proyecto (aporte de los × de presentación)", "coef"));
        }
        else
        {
            lines.Add(new($"Gastos generales ({project.GastosGeneralesPct:N1}%)", cost.GastosGeneralesUf,
                "GG sobre costo directo según política", "coef"));
            if (cost.ValorAgregadoEquiposUf > 0m || project.ValorAgregadoEquiposPct > 0m)
                lines.Add(new($"VA sobre equipos ({project.ValorAgregadoEquiposPct:N1}%)", cost.ValorAgregadoEquiposUf,
                    "Valor agregado solo sobre equipos", "coef"));
            lines.Add(new("Subtotal", cost.SubtotalUf, "Costo directo + riesgo + GG + VA", "subtotal"));
            var utilidadLabel = string.Equals(project.MarkupMethod, "SobreVenta", StringComparison.OrdinalIgnoreCase)
                ? $"Margen s/ venta ({project.UtilidadPct:N1}%)"
                : $"Utilidad ({project.UtilidadPct:N1}%)";
            lines.Add(new(utilidadLabel, cost.UtilidadUf, "Utilidad / margen según política comercial", "coef"));
        }

        lines.Add(new("Descuento", cost.DescuentoUf, "Descuento comercial", "regular"));
        lines.Add(new("Total venta neto", cost.TotalVentaNetoUf, "Total antes de IVA", "subtotal"));
        lines.Add(new("IVA", cost.IvaUf, "IVA incluido en total", "regular"));
        lines.Add(new("TOTAL VENTA", cost.TotalVentaUf, "Neto + IVA", "grand"));
        if (project.MetrosCuadrados > 0)
            lines.Add(new($"UF / m² ({project.MetrosCuadrados:N0} m²)", cost.UfM2, "Total venta / m²", "regular"));

        return lines;
    }
}

public static class CostoOrigenDescriptor
{
    public static string Describe(string origen)
    {
        if (string.IsNullOrWhiteSpace(origen)) return "Origen del costo directo";
        if (origen.Contains("Cubic", StringComparison.OrdinalIgnoreCase)) return "Partidas valorizadas desde takeoff";
        if (origen.Contains("APU", StringComparison.OrdinalIgnoreCase)) return "Complementarios y ajustes de APU";
        if (origen.Contains("OC", StringComparison.OrdinalIgnoreCase) || origen.Contains("Orden", StringComparison.OrdinalIgnoreCase))
            return "Órdenes de cambio aprobadas";
        if (origen.Contains("Global", StringComparison.OrdinalIgnoreCase) || origen.Contains("Herramient", StringComparison.OrdinalIgnoreCase) || origen.Contains("Merma", StringComparison.OrdinalIgnoreCase))
            return "Recargos globales del motor";
        return origen;
    }
}
