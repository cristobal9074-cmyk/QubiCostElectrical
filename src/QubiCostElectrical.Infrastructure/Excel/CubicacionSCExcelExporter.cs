using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

public sealed class CubicacionSCExcelExporter : ICubicacionSCExcelExporter
{
    public void Export(string path, string projectTitle, IReadOnlyList<CubicacionSCExcelRow> rows)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportación requerida.", nameof(path));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Resumen C.SC");

        ws.Cell(1, 1).Value = $"Resumen C.SC — {projectTitle} — {DateTime.Today:dd/MM/yyyy}";
        ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;

        string[] headers = { "Categoría", "Material", "Cantidad", "Unidad", "UF Unit.", "UF Total" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(2, i + 1).Value = headers[i];
            ws.Cell(2, i + 1).Style.Font.Bold = true;
            ws.Cell(2, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(15, 111, 214);
            ws.Cell(2, i + 1).Style.Font.FontColor = XLColor.White;
        }

        int excelRow = 3;
        foreach (var x in rows)
        {
            ws.Cell(excelRow, 1).Value = x.Categoria;
            ws.Cell(excelRow, 2).Value = x.Material;
            ws.Cell(excelRow, 3).Value = ClosedXmlExportStyler.ToDouble(x.Cantidad);
            ws.Cell(excelRow, 4).Value = x.Unidad;
            if (x.UfUnit != 0m)
                ws.Cell(excelRow, 5).Value = ClosedXmlExportStyler.ToDouble(x.UfUnit);
            if (x.UfTotal != 0m)
                ws.Cell(excelRow, 6).Value = ClosedXmlExportStyler.ToDouble(x.UfTotal);
            ws.Cell(excelRow, 3).Style.NumberFormat.Format = "#,##0.##";
            ws.Cell(excelRow, 5).Style.NumberFormat.Format = "#,##0.0000";
            ws.Cell(excelRow, 6).Style.NumberFormat.Format = "#,##0.0000";
            excelRow++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }
}
