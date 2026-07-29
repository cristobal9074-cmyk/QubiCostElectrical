using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.EarnedValueWorkspace;

/// <summary>Exportación sin recálculo — serializa resultados ya calculados.</summary>
public static class EarnedValueExportHelper
{
    public static string ExportAllFormats(EarnedValueResult result, IReadOnlyList<EarnedValueHistoryEntry> history)
    {
        var csv = ExportCsv(result, history);
        var xlsx = ExportExcel(result, history);
        var pdf = ExportPdf(result, history);
        return csv + " | " + xlsx + " | " + pdf;
    }

    public static string ExportCsv(EarnedValueResult result, IReadOnlyList<EarnedValueHistoryEntry> history)
    {
        var path = NewExportPath("evm", "csv", result.StatusDate);
        var sb = new StringBuilder();
        var i = result.Indicators;
        sb.AppendLine("# EVM;StatusDate;" + result.StatusDate.ToString("O"));
        sb.AppendLine("Indicador;Valor");
        AppendCsvRows(sb, i, result);
        sb.AppendLine();
        sb.AppendLine("# Historial");
        sb.AppendLine("StatusDate;PV;EV;AC;SPI;CPI;EAC;By");
        foreach (var h in history)
            sb.Append(h.StatusDate.ToString("yyyy-MM-dd")).Append(';')
              .Append(h.Pv).Append(';').Append(h.Ev).Append(';').Append(h.Ac).Append(';')
              .Append(h.Spi).Append(';').Append(h.Cpi).Append(';').Append(h.Eac).Append(';')
              .Append(h.RecordedBy).AppendLine();
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    public static string ExportExcel(EarnedValueResult result, IReadOnlyList<EarnedValueHistoryEntry> history)
    {
        var path = NewExportPath("evm", "xlsx", result.StatusDate);
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("EVM");
        sheet.Cell(1, 1).Value = "Indicador";
        sheet.Cell(1, 2).Value = "Valor";
        var rows = BuildIndicatorPairs(result);
        for (var r = 0; r < rows.Count; r++)
        {
            sheet.Cell(r + 2, 1).Value = rows[r].Name;
            sheet.Cell(r + 2, 2).Value = rows[r].Value;
        }

        var hist = wb.Worksheets.Add("Historial");
        hist.Cell(1, 1).Value = "StatusDate";
        hist.Cell(1, 2).Value = "PV";
        hist.Cell(1, 3).Value = "EV";
        hist.Cell(1, 4).Value = "AC";
        hist.Cell(1, 5).Value = "SPI";
        hist.Cell(1, 6).Value = "CPI";
        hist.Cell(1, 7).Value = "EAC";
        hist.Cell(1, 8).Value = "By";
        for (var r = 0; r < history.Count; r++)
        {
            var h = history[r];
            hist.Cell(r + 2, 1).Value = h.StatusDate;
            hist.Cell(r + 2, 2).Value = h.Pv;
            hist.Cell(r + 2, 3).Value = h.Ev;
            hist.Cell(r + 2, 4).Value = h.Ac;
            hist.Cell(r + 2, 5).Value = h.Spi;
            hist.Cell(r + 2, 6).Value = h.Cpi;
            hist.Cell(r + 2, 7).Value = h.Eac;
            hist.Cell(r + 2, 8).Value = h.RecordedBy;
        }

        wb.SaveAs(path);
        return path;
    }

    public static string ExportPdf(EarnedValueResult result, IReadOnlyList<EarnedValueHistoryEntry> history)
    {
        var path = NewExportPath("evm", "pdf", result.StatusDate);
        QuestPdfBootstrap.EnsureCommunityLicense();
        var pairs = BuildIndicatorPairs(result);
        Document.Create(composer =>
        {
            composer.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(10));
                page.Header().Text("Qubi Cost Electrical — Valor ganado (EVM)")
                    .Bold().FontSize(14).FontColor("#0B2442");
                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(8).Text($"Fecha de estado: {result.StatusDate:dd/MM/yyyy}");
                    col.Item().PaddingTop(4).Text(result.Explanation).FontSize(9).FontColor("#64748B");
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background("#0B2442").Padding(4).Text("Indicador").FontColor(Colors.White).Bold();
                            h.Cell().Background("#0B2442").Padding(4).Text("Valor").FontColor(Colors.White).Bold();
                        });
                        foreach (var p in pairs)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor("#E2E8F0").Padding(3).Text(p.Name);
                            table.Cell().BorderBottom(0.5f).BorderColor("#E2E8F0").Padding(3).Text(p.Value);
                        }
                    });
                    if (history.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Historial").Bold();
                        col.Item().Text($"Entradas: {history.Count}").FontSize(9);
                    }
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generado ").FontSize(8).FontColor("#94A3B8");
                    t.Span(DateTime.Now.ToString("g")).FontSize(8).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf(path);
        return path;
    }

    private static string NewExportPath(string prefix, string ext, DateTime statusDate)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QubiCostElectrical", "Exports");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{prefix}-{statusDate:yyyyMMdd-HHmmss}.{ext}");
    }

    private static void AppendCsvRows(StringBuilder sb, EarnedValueIndicators i, EarnedValueResult result)
    {
        foreach (var p in BuildIndicatorPairs(result))
            sb.Append(p.Name).Append(';').AppendLine(p.Value);
    }

    private static List<(string Name, string Value)> BuildIndicatorPairs(EarnedValueResult result)
    {
        var i = result.Indicators;
        return new List<(string, string)>
        {
            ("PV", i.Pv.ToString("N2")),
            ("EV", i.Ev.ToString("N2")),
            ("AC", i.Ac.ToString("N2")),
            ("BAC", i.Bac.ToString("N2")),
            ("EAC", i.Eac.ToString("N2")),
            ("ETC", i.Etc.ToString("N2")),
            ("VAC", i.Vac.ToString("N2")),
            ("SV", i.Sv.ToString("N2")),
            ("CV", i.Cv.ToString("N2")),
            ("SPI", i.Spi?.ToString("0.####") ?? "N/D"),
            ("CPI", i.Cpi?.ToString("0.####") ?? "N/D"),
            ("TCPI", i.Tcpi?.ToString("0.####") ?? "N/D"),
            ("PercentPlanned", i.PercentPlanned.ToString("0.##")),
            ("PercentEarned", i.PercentEarned.ToString("0.##")),
            ("PercentActual", i.PercentActual.ToString("0.##")),
            ("ForecastCost", result.Forecast.ForecastCost.ToString("N2")),
            ("ForecastFinishVarianceDays", result.Forecast.ForecastFinishVarianceDays?.ToString("0.##") ?? "N/D")
        };
    }
}
