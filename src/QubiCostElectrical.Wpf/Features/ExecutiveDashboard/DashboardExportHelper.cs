using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ExecutiveDashboard;

/// <summary>Exporta modelos de dashboard ya calculados (sin recálculo).</summary>
public static class DashboardExportHelper
{
    public static string ExportAll(ProjectExecutiveDashboard dash)
    {
        var csv = ExportCsv(dash);
        var xlsx = ExportExcel(dash);
        var pdf = ExportPdf(dash);
        return csv + " | " + xlsx + " | " + pdf;
    }

    public static string ExportCsv(ProjectExecutiveDashboard dash)
    {
        var path = NewPath("dashboard", "csv");
        var sb = new StringBuilder();
        sb.AppendLine("# Dashboard;" + dash.ProjectName + ";" + dash.StatusDate?.ToString("O"));
        sb.AppendLine("Code;Title;Value;Status;Interpretation");
        foreach (var k in dash.Kpis)
            sb.Append(k.Code).Append(';').Append(k.Title).Append(';').Append(k.DisplayValue).Append(';')
              .Append(k.Status).Append(';').Append(k.Interpretation).AppendLine();
        sb.AppendLine();
        sb.AppendLine("# Alertas");
        foreach (var a in dash.Alerts)
            sb.Append(a.Severity).Append(';').Append(a.Code).Append(';').Append(a.Title).Append(';').Append(a.Message).AppendLine();
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    public static string ExportExcel(ProjectExecutiveDashboard dash)
    {
        var path = NewPath("dashboard", "xlsx");
        using var wb = new XLWorkbook();
        var kpis = wb.Worksheets.Add("KPIs");
        kpis.Cell(1, 1).Value = "Code";
        kpis.Cell(1, 2).Value = "Title";
        kpis.Cell(1, 3).Value = "Value";
        kpis.Cell(1, 4).Value = "Status";
        for (var i = 0; i < dash.Kpis.Count; i++)
        {
            kpis.Cell(i + 2, 1).Value = dash.Kpis[i].Code;
            kpis.Cell(i + 2, 2).Value = dash.Kpis[i].Title;
            kpis.Cell(i + 2, 3).Value = dash.Kpis[i].DisplayValue;
            kpis.Cell(i + 2, 4).Value = dash.Kpis[i].Status.ToString();
        }
        var alerts = wb.Worksheets.Add("Alertas");
        alerts.Cell(1, 1).Value = "Severity";
        alerts.Cell(1, 2).Value = "Code";
        alerts.Cell(1, 3).Value = "Title";
        for (var i = 0; i < dash.Alerts.Count; i++)
        {
            alerts.Cell(i + 2, 1).Value = dash.Alerts[i].Severity.ToString();
            alerts.Cell(i + 2, 2).Value = dash.Alerts[i].Code;
            alerts.Cell(i + 2, 3).Value = dash.Alerts[i].Title;
        }
        var wbs = wb.Worksheets.Add("WBS");
        wbs.Cell(1, 1).Value = "Code";
        wbs.Cell(1, 2).Value = "Name";
        wbs.Cell(1, 3).Value = "Physical%";
        wbs.Cell(1, 4).Value = "AC";
        for (var i = 0; i < dash.WbsNodes.Count; i++)
        {
            wbs.Cell(i + 2, 1).Value = dash.WbsNodes[i].Code;
            wbs.Cell(i + 2, 2).Value = dash.WbsNodes[i].Name;
            wbs.Cell(i + 2, 3).Value = dash.WbsNodes[i].PhysicalProgressPct;
            wbs.Cell(i + 2, 4).Value = dash.WbsNodes[i].Ac;
        }
        wb.SaveAs(path);
        return path;
    }

    public static string ExportPdf(ProjectExecutiveDashboard dash)
    {
        var path = NewPath("dashboard", "pdf");
        QuestPdfBootstrap.EnsureCommunityLicense();
        Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(10));
                page.Header().Text("Qubi Cost Electrical — Dashboard ejecutivo").Bold().FontSize(14);
                page.Content().Column(col =>
                {
                    col.Item().Text(dash.ProjectName).Bold();
                    col.Item().Text($"Fecha estado: {dash.StatusDate:dd/MM/yyyy} · Calidad: {dash.DataQuality}");
                    col.Item().PaddingTop(8).Text(dash.ExecutiveSummary).FontSize(9);
                    col.Item().PaddingTop(12).Text("KPIs").Bold();
                    foreach (var k in dash.Kpis.Take(12))
                        col.Item().Text($"{k.Title}: {k.DisplayValue} [{k.Status}] — {k.Interpretation}").FontSize(9);
                    col.Item().PaddingTop(12).Text("Alertas críticas").Bold();
                    foreach (var a in dash.Alerts.Where(x => x.Severity <= DashboardAlertSeverity.High).Take(10))
                        col.Item().Text($"{a.Severity}: {a.Title} — {a.Message}").FontSize(9);
                    col.Item().PaddingTop(12).Text("Principales desviaciones").Bold();
                    foreach (var d in dash.Deviations.Take(8))
                        col.Item().Text($"{d.Title}: {d.AbsoluteImpact:N0} ({d.Metric})").FontSize(9);
                });
            });
        }).GeneratePdf(path);
        return path;
    }

    private static string NewPath(string prefix, string ext)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QubiCostElectrical", "Exports");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.{ext}");
    }
}
