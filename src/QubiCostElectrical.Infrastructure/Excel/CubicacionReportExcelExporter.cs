using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

/// <summary>
/// Reporte imprimible de cubicación/memoria de cálculo por partida y área.
/// Pensado para entregar a constructora/mandante sin exponer todas las hojas internas del presupuesto.
/// </summary>
public sealed class CubicacionReportExcelExporter : ICubicacionReportExcelExporter
{
    private readonly ICurrencyConversionService currencyConversion;

    public CubicacionReportExcelExporter()
        : this(new CurrencyConversionService())
    {
    }

    public CubicacionReportExcelExporter(ICurrencyConversionService currencyConversion)
    {
        this.currencyConversion = currencyConversion;
    }

    public void Export(ExportProjectContext context, string path)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta de exportación vacía.", nameof(path));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var wb = new XLWorkbook();
        ExportMemoria(wb, context);
        ExportResumenAreas(wb, context);
        wb.SaveAs(path);
    }

    private void ExportMemoria(XLWorkbook wb, ExportProjectContext ctx)
    {
        var ws = wb.Worksheets.Add("MEMORIA_CUBICACION");
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Cell(1, 1).Value = "MEMORIA DE CUBICACIÓN POR PARTIDA";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 15;
        ws.Range(1, 1, 1, 12).Merge();
        ws.Cell(2, 1).Value = $"Proyecto: {ctx.ProjectTitle}";
        ws.Cell(2, 5).Value = $"Cliente: {ctx.ClientName}";
        ws.Cell(2, 8).Value = $"Fecha: {ctx.ExportedAt:dd/MM/yyyy HH:mm}";

        int row = 4;
        string etiqueta = ctx.MuestraUf ? "UF" : "$";
        Header(ws, row, new[] { "ÍTEM", "PARTIDA", "ÁREA", "CÓDIGO", "MATERIAL / RECURSO", "UD", "CANTIDAD", "EXPRESIONES / ZONAS", "FÓRMULA TOTAL", "REGLAS", $"UNIT {etiqueta}", $"TOTAL {etiqueta}" });
        row++;

        var rowsByItem = ctx.Cubicacion
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId) && (!string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m))
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in ctx.Itemizado.Where(x => !x.EsTitulo))
        {
            rowsByItem.TryGetValue(item.Id, out var cubRows);
            cubRows ??= new();
            if (cubRows.Count == 0) continue;

            int itemStart = row;
            decimal itemTotal = cubRows.Sum(x => x.UfTotal);
            ws.Cell(row, 1).Value = item.Item;
            ws.Cell(row, 2).Value = item.Descripcion;
            ws.Cell(row, 3).Value = item.Unidad;
            ws.Cell(row, 7).SetValue(ToDouble(item.Cantidad));
            ws.Cell(row, 8).Value = "Subtotal partida";
            ws.Cell(row, 12).SetValue(ToDouble(MontoEnMoneda(ctx, itemTotal)));
            ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FB");
            ws.Range(row, 1, row, 12).Style.Font.Bold = true;
            row++;

            foreach (var line in cubRows.OrderBy(x => x.Area).ThenBy(x => x.Codigo))
            {
                ws.Cell(row, 1).Value = item.Item;
                ws.Cell(row, 2).Value = item.Descripcion;
                ws.Cell(row, 3).Value = string.IsNullOrWhiteSpace(line.Area) ? "General" : line.Area;
                ws.Cell(row, 4).Value = line.Codigo;
                ws.Cell(row, 5).Value = line.Descripcion;
                ws.Cell(row, 6).Value = line.Unidad;
                ws.Cell(row, 7).SetValue(ToDouble(line.Cantidad));
                ws.Cell(row, 8).Value = BuildExpressionSummary(ctx, line);
                ws.Cell(row, 9).Value = line.TotalExpression;
                ws.Cell(row, 10).Value = BuildRuleSummary(line);
                ws.Cell(row, 11).SetValue(ToDouble(MontoEnMoneda(ctx, line.UfUnit)));
                ws.Cell(row, 12).SetValue(ToDouble(MontoEnMoneda(ctx, line.UfTotal)));
                row++;
            }

            ws.Range(itemStart, 1, row - 1, 12).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(itemStart, 1, row - 1, 12).Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        }

        if (row == 5)
        {
            ws.Cell(row, 1).Value = "Sin cubicación registrada";
            ws.Cell(row, 2).Value = "Agrega materiales en el módulo Cubicación para generar la memoria de cálculo.";
            row++;
        }

        ws.Range(5, 7, Math.Max(row, 5), 7).Style.NumberFormat.Format = "#,##0.###";
        ws.Range(5, 11, Math.Max(row, 5), 12).Style.NumberFormat.Format = ctx.MuestraUf ? "#,##0.000" : "$ #,##0";
        ws.Column(1).Width = 10;
        ws.Column(2).Width = 34;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 48;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 46;
        ws.Column(9).Width = 28;
        ws.Column(10).Width = 26;
        ws.Column(11).Width = 14;
        ws.Column(12).Width = 14;
        ws.Range(1, 1, Math.Max(row, 5), 12).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(5, 2, Math.Max(row, 5), 10).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(4);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }

    private void ExportResumenAreas(XLWorkbook wb, ExportProjectContext ctx)
    {
        var ws = wb.Worksheets.Add("RESUMEN_AREAS");
        ws.Style.Font.FontName = "Segoe UI";
        ws.Cell(1, 1).Value = "RESUMEN DE CUBICACIÓN POR ÁREA";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 6).Merge();
        Header(ws, 3, new[] { "ÁREA", "LÍNEAS", "MATERIALES", "CANTIDAD TOTAL", "TOTAL UF", "TOTAL " + ctx.Currency.EncabezadoPrincipal });
        int row = 4;
        foreach (var area in ctx.Cubicacion
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Area) ? "General" : x.Area.Trim())
            .OrderByDescending(g => g.Sum(x => x.UfTotal)))
        {
            decimal uf = area.Sum(x => x.UfTotal);
            ws.Cell(row, 1).Value = area.Key;
            ws.Cell(row, 2).SetValue(area.Count());
            ws.Cell(row, 3).SetValue(area.Select(x => x.Codigo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            ws.Cell(row, 4).SetValue(ToDouble(area.Sum(x => x.Cantidad)));
            ws.Cell(row, 5).SetValue(ToDouble(uf));
            ws.Cell(row, 6).SetValue(ToDouble(ToProjectCurrency(ctx, uf)));
            row++;
        }
        ws.Columns().AdjustToContents(10, 42);
        ws.Range(4, 5, Math.Max(row, 4), 5).Style.NumberFormat.Format = "#,##0.000";
        ws.Range(4, 6, Math.Max(row, 4), 6).Style.NumberFormat.Format = ProjectFormat(ctx);
        if (!ctx.MuestraUf)
            ws.Column(5).Hide();
        ws.SheetView.FreezeRows(3);
    }

    private static void Header(IXLWorksheet ws, int row, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++) ws.Cell(row, i + 1).Value = headers[i];
        var range = ws.Range(row, 1, row, headers.Length);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static string BuildExpressionSummary(ExportProjectContext ctx, CubicacionRow line)
    {
        var expressions = line.QtyExpressions ?? Array.Empty<string>();
        if ((expressions.Length == 0 || expressions.All(string.IsNullOrWhiteSpace)) && string.IsNullOrWhiteSpace(line.TotalExpression)) return "Cantidad directa";

        ctx.QtyHeadersByItem.TryGetValue(line.ItemId, out var headers);
        var parts = expressions
            .Select((expr, i) => new { Expr = (expr ?? "").Trim(), Index = i })
            .Where(x => !string.IsNullOrWhiteSpace(x.Expr))
            .Take(10)
            .Select(x =>
            {
                string label = headers != null && x.Index < headers.Length && !string.IsNullOrWhiteSpace(headers[x.Index])
                    ? headers[x.Index]
                    : $"Q{x.Index + 1}";
                return $"{label}: {x.Expr}";
            })
            .ToList();
        if (!string.IsNullOrWhiteSpace(line.TotalExpression))
            parts.Add("Total: " + line.TotalExpression.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildRuleSummary(CubicacionRow line)
    {
        var rules = new List<string>();
        if (line.MermaPctOverride >= 0m) rules.Add($"Merma {line.MermaPctOverride:0.##}%");
        if (line.HerramientasPctOverride >= 0m) rules.Add($"Herr. {line.HerramientasPctOverride:0.##}%");
        if (line.RedondeoMultiplo > 0m) rules.Add($"Redondeo {line.RedondeoModo} {line.RedondeoMultiplo:0.###}");
        return rules.Count == 0 ? "Global" : string.Join(" · ", rules);
    }

    private decimal ToProjectCurrency(ExportProjectContext ctx, decimal uf)
        => currencyConversion.ConvertUfToCurrency(uf, ctx.CurrencyLabel, ctx.Project.ValorUfClp, ctx.Project.TipoCambioUsdClp);

    /// <summary>
    /// Monto en UF real convertido a la moneda de presentación: en CLP se multiplica por el valor
    /// de la UF (el motor guarda pesos); en UF/USD/EUR se mantiene la cifra en UF.
    /// </summary>
    private static decimal MontoEnMoneda(ExportProjectContext ctx, decimal uf)
        => ctx.MuestraUf ? uf : uf * ctx.Currency.ValorUfClp;

    private static string ProjectFormat(ExportProjectContext ctx)
        => ctx.MuestraUf ? "#,##0.000" : "$ #,##0";

    private static double ToDouble(decimal value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
