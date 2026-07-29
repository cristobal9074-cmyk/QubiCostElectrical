using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

public sealed class PurchaseListExcelExporter : IPurchaseListExcelExporter
{
    public void Export(string path, PurchaseListExcelRequest request)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportación requerida.", nameof(path));
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (File.Exists(path))
            File.Delete(path);

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Segoe UI";
        wb.Style.Font.FontSize = 9;

        var ws = wb.Worksheets.Add("ListaCompra");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "LISTA DE COMPRA CONSOLIDADA", 12);

        ws.Cell(2, 1).Value = "Proyecto";
        ws.Cell(2, 2).Value = request.ProjectTitle;
        ws.Cell(2, 4).Value = "Tolerancia";
        ws.Cell(2, 5).SetValue(ClosedXmlExportStyler.ToDouble(request.ToleranciaPct / 100m));
        ws.Cell(2, 5).Style.NumberFormat.Format = "0.00%";
        ws.Cell(2, 7).Value = "Fecha";
        ws.Cell(2, 8).SetValue(AppClock.Now);
        ws.Cell(2, 8).Style.NumberFormat.Format = "dd-mm-yyyy hh:mm";
        ws.Range(2, 1, 2, 8).Style.Font.Bold = true;

        int headerRow = 4;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "RANK", "CÓDIGO", "DESCRIPCIÓN", "UNIDAD", "CATEGORÍA", "CANTIDAD NETA",
            "TOLERANCIA %", "CANTIDAD COMPRA", "PRECIO MAT UF", "TOTAL NETO UF", "TOTAL COMPRA UF", "LÍNEAS"
        });

        int row = headerRow + 1;
        int rank = 1;
        foreach (var x in request.Rows)
        {
            ws.Cell(row, 1).SetValue(rank++);
            ws.Cell(row, 2).Value = x.Codigo;
            ws.Cell(row, 3).Value = x.Descripcion;
            ws.Cell(row, 4).Value = x.Unidad;
            ws.Cell(row, 5).Value = x.Categoria;
            ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(x.CantidadNeta));
            ws.Cell(row, 7).SetValue(ClosedXmlExportStyler.ToDouble(x.ToleranciaPct / 100m));
            ws.Cell(row, 8).FormulaA1 = $"F{row}*(1+G{row})";
            ws.Cell(row, 9).SetValue(ClosedXmlExportStyler.ToDouble(x.MatUfUnitario));
            ws.Cell(row, 10).FormulaA1 = $"F{row}*I{row}";
            ws.Cell(row, 11).FormulaA1 = $"H{row}*I{row}";
            ws.Cell(row, 12).SetValue(x.Lineas);
            row++;
        }

        int lastRow = Math.Max(row - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, 12, "tbl_lista_compra");
        ws.Range(headerRow + 1, 6, Math.Max(lastRow, headerRow + 1), 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(headerRow + 1, 7, Math.Max(lastRow, headerRow + 1), 7).Style.NumberFormat.Format = "0.00%";
        ws.Range(headerRow + 1, 8, Math.Max(lastRow, headerRow + 1), 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(headerRow + 1, 9, Math.Max(lastRow, headerRow + 1), 11).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Range(headerRow + 1, 12, Math.Max(lastRow, headerRow + 1), 12).Style.NumberFormat.Format = "#,##0";

        int totalRow = lastRow + 2;
        ws.Cell(totalRow, 10).Value = "TOTAL COMPRA UF";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;
        ws.Cell(totalRow, 11).FormulaA1 = lastRow > headerRow ? $"SUM(K{headerRow + 1}:K{lastRow})" : "0";
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Range(totalRow, 10, totalRow, 11).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(totalRow, 10, totalRow, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(3).Width = 48;
        ws.Column(5).Width = 22;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
        wb.SaveAs(path);
    }
}
