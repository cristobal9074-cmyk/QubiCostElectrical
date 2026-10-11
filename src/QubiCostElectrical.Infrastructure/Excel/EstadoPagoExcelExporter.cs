using System;
using System.Drawing;
using System.IO;
using ClosedXML.Excel;

namespace QubiCostElectrical;

/// <summary>
/// Exporta el estado de pago del mes (partidas por capítulo + totales) y el cronograma
/// anual de 12 meses a Excel con ClosedXML. Estilo base compartido con los demás
/// exportadores (<see cref="ClosedXmlExportStyler"/>).
/// </summary>
public sealed class EstadoPagoExcelExporter : IEstadoPagoExcelExporter
{
    private const int Cols = 7;

    public void Export(EstadoPagoExportRequest request, string path)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportación requerida.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (File.Exists(path))
            File.Delete(path);

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Segoe UI";
        wb.Style.Font.FontSize = 9;

        WriteMes(wb.Worksheets.Add("EstadoPago"), request);
        WriteCronograma(wb.Worksheets.Add("Cronograma"), request);

        wb.SaveAs(path);
    }

    private static void WriteMes(IXLWorksheet ws, EstadoPagoExportRequest request)
    {
        var mes = request.Mes;
        var t = mes.Totales;
        var cfg = request.Config;

        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, $"ESTADO DE PAGO · {mes.Folio}", Cols);

        WriteMeta(ws, 2, "PROYECTO:", request.ProjectTitle, "FOLIO:", mes.Folio, "ESTADO:", mes.EstadoLabel);
        WriteMeta(
            ws, 3,
            "MES:", $"{mes.MesNombre} {mes.Anio}",
            "EMISIÓN:", mes.FechaEmision.ToString("dd-MM-yyyy"),
            "APROBADO POR:", string.IsNullOrWhiteSpace(mes.AprobadoPor) ? "—" : mes.AprobadoPor);

        int headerRow = 5;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "CÓDIGO", "PARTIDA (ITEMIZADO)", "MONTO CONTRATADO UF", "AVANCE ANTERIOR %",
            "AVANCE MES %", "MONTO DEL MES UF", "ACUMULADO %"
        });

        int row = headerRow + 1;
        foreach (var sec in mes.Secciones)
        {
            if (!string.IsNullOrWhiteSpace(sec.Capitulo))
            {
                ws.Range(row, 1, row, Cols).Merge();
                ws.Cell(row, 1).Value = sec.Capitulo;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
                row++;
            }

            foreach (var f in sec.Filas)
            {
                ws.Cell(row, 1).Value = f.Item;
                ws.Cell(row, 2).Value = f.Descripcion;
                ws.Cell(row, 3).SetValue(ClosedXmlExportStyler.ToDouble(f.Monto));
                ws.Cell(row, 4).SetValue(ClosedXmlExportStyler.ToDouble(f.AvanceAnterior / 100m));
                ws.Cell(row, 5).SetValue(ClosedXmlExportStyler.ToDouble(f.AvanceMes / 100m));
                ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(f.MontoMes));
                ws.Cell(row, 7).SetValue(ClosedXmlExportStyler.ToDouble(f.AcumPct / 100m));
                row++;
            }
        }

        int lastRow = Math.Max(row - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, Cols, "tbl_estado_pago");
        ws.Range(headerRow + 1, 3, lastRow, 3).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Range(headerRow + 1, 4, lastRow, 5).Style.NumberFormat.Format = "0.00%";
        ws.Range(headerRow + 1, 6, lastRow, 6).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Range(headerRow + 1, 7, lastRow, 7).Style.NumberFormat.Format = "0.00%";

        int tot = lastRow + 2;
        ws.Range(tot, 1, tot, Cols).Merge();
        ws.Cell(tot, 1).Value = $"TOTALES DEL MES · {mes.MesNombre} {mes.Anio}";
        ws.Cell(tot, 1).Style.Font.Bold = true;
        ws.Cell(tot, 1).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
        tot++;

        WriteTotalUf(ws, tot++, "Trabajo del mes", t.TrabajoMes);
        WriteTotalUf(ws, tot++, "Acumulado a la fecha", t.Acum);
        WriteTotalPct(ws, tot++, "Avance acumulado", t.PctAcum);
        WriteTotalUf(ws, tot++, $"Recupero de anticipo ({cfg.AnticipoPct:0.##} %)", t.RecuperoMes);
        WriteTotalUf(ws, tot++, $"Retención garantía ({cfg.RetencionPct:0.##} %)", t.RetencionMes);
        WriteTotalUf(ws, tot++, "Neto antes de IVA", t.Neto);
        WriteTotalUf(ws, tot++, $"IVA ({cfg.IvaPct:0.##} %)", t.Iva);
        WriteTotalUf(ws, tot, "TOTAL A PAGAR", t.Total, resaltar: true);

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 46;
        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
    }

    private static void WriteCronograma(IXLWorksheet ws, EstadoPagoExportRequest request)
    {
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "CRONOGRAMA ANUAL DE ESTADOS DE PAGO", 4);

        ws.Cell(2, 1).Value = "PROYECTO:";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Range(2, 2, 2, 3).Merge();
        ws.Cell(2, 2).Value = request.ProjectTitle;
        ws.Cell(2, 4).Value = $"AÑO {request.Config.Anio}";
        ws.Range(2, 1, 2, 4).Style.Font.Bold = true;

        int headerRow = 4;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[] { "MES", "FOLIO", "ESTADO", "TOTAL UF" });

        int row = headerRow + 1;
        int first = row;
        foreach (var m in request.Cronograma)
        {
            ws.Cell(row, 1).Value = m.MesNombre;
            ws.Cell(row, 2).Value = string.IsNullOrWhiteSpace(m.Folio) ? "—" : m.Folio;
            ws.Cell(row, 3).Value = m.EstadoLabel;
            if (m.Total is decimal total)
            {
                ws.Cell(row, 4).SetValue(ClosedXmlExportStyler.ToDouble(total));
                ws.Cell(row, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
            }
            else
            {
                ws.Cell(row, 4).Value = "—";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            row++;
        }

        int lastRow = row - 1;
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, 4, "tbl_cronograma_estados");

        int totRow = lastRow + 2;
        ws.Range(totRow, 1, totRow, 3).Merge();
        ws.Cell(totRow, 1).Value = "SUMA DE TOTALES UF";
        ws.Cell(totRow, 1).Style.Font.Bold = true;
        ws.Cell(totRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(totRow, 4).FormulaA1 = $"SUM(D{first}:D{lastRow})";
        ws.Cell(totRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Cell(totRow, 4).Style.Font.Bold = true;
        ws.Range(totRow, 1, totRow, 4).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(totRow, 1, totRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(1).Width = 16;
        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
    }

    private static void WriteMeta(
        IXLWorksheet ws, int row,
        string labelA, string valorA,
        string labelB, string valorB,
        string labelC, string valorC)
    {
        ws.Cell(row, 1).Value = labelA;
        ws.Cell(row, 2).Value = valorA;
        ws.Cell(row, 3).Value = labelB;
        ws.Cell(row, 4).Value = valorB;
        ws.Cell(row, 5).Value = labelC;
        ws.Range(row, 6, row, 7).Merge();
        ws.Cell(row, 6).Value = valorC;
        ws.Range(row, 1, row, 7).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.Bold = false;
        ws.Cell(row, 4).Style.Font.Bold = false;
        ws.Cell(row, 6).Style.Font.Bold = false;
        ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
    }

    private static void WriteTotalUf(IXLWorksheet ws, int row, string label, decimal valor, bool resaltar = false)
    {
        WriteTotalBase(ws, row, label, resaltar);
        ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(valor));
        ws.Cell(row, 6).Style.NumberFormat.Format = ClosedXmlExportStyler.UfExcelFormat();
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, Cols).Style.Fill.BackgroundColor =
            XLColor.FromColor(resaltar ? Color.FromArgb(229, 241, 255) : Color.FromArgb(248, 250, 252));
        if (resaltar)
            ws.Range(row, 1, row, Cols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteTotalPct(IXLWorksheet ws, int row, string label, decimal valor)
    {
        WriteTotalBase(ws, row, label, resaltar: false);
        ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(valor / 100m));
        ws.Cell(row, 6).Style.NumberFormat.Format = "0.00%";
        ws.Range(row, 1, row, Cols).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(248, 250, 252));
    }

    private static void WriteTotalBase(IXLWorksheet ws, int row, string label, bool resaltar)
    {
        ws.Range(row, 1, row, 5).Merge();
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(row, 1).Style.Font.Bold = true;
        if (resaltar)
            ws.Cell(row, 1).Style.Font.FontSize = 11;
    }
}
