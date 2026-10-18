using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Exporta el estado de pago del mes (partidas por capítulo + totales) y el cronograma
/// anual de 12 meses a Excel con ClosedXML. Estilo base compartido con los demás
/// exportadores (<see cref="ClosedXmlExportStyler"/>).
/// </summary>
public sealed class EstadoPagoExcelExporter : IEstadoPagoExcelExporter
{
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
        // Con CLP los montos (que el motor lleva en UF) se escriben en pesos y sin rótulo UF.
        int cols = request.MuestraUf ? 8 : 7;
        string fmt = ClosedXmlExportStyler.ProjectCurrencyExcelFormat(request.MonedaPresupuesto);
        string Hdr(string concepto) => $"{concepto} {(request.MuestraUf ? "UF" : "$")}";

        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, $"ESTADO DE PAGO · {mes.Folio}", cols);

        WriteMeta(ws, 2, "PROYECTO:", request.ProjectTitle, "FOLIO:", mes.Folio, "ESTADO:", mes.EstadoLabel);
        WriteMeta(
            ws, 3,
            "MES:", $"{mes.MesNombre} {mes.Anio}",
            "EMISIÓN:", mes.FechaEmision.ToString("dd-MM-yyyy"),
            "APROBADO POR:", string.IsNullOrWhiteSpace(mes.AprobadoPor) ? "—" : mes.AprobadoPor);

        int headerRow = 5;
        var headers = new List<string>
        {
            "CÓDIGO", "PARTIDA (ITEMIZADO)", Hdr("MONTO CONTRATADO"), "AVANCE ANTERIOR %",
            "AVANCE MES %", Hdr("MONTO DEL MES"), "ACUMULADO %"
        };
        if (request.MuestraUf)
            headers.Add("EQUIV. $");
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, headers);

        int row = headerRow + 1;
        foreach (var sec in mes.Secciones)
        {
            if (!string.IsNullOrWhiteSpace(sec.Capitulo))
            {
                ws.Range(row, 1, row, cols).Merge();
                ws.Cell(row, 1).Value = sec.Capitulo;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
                row++;
            }

            foreach (var f in sec.Filas)
            {
                ws.Cell(row, 1).Value = f.Item;
                ws.Cell(row, 2).Value = f.Descripcion;
                ws.Cell(row, 3).SetValue(ClosedXmlExportStyler.ToDouble(Valor(f.Monto, request)));
                ws.Cell(row, 4).SetValue(ClosedXmlExportStyler.ToDouble(f.AvanceAnterior / 100m));
                ws.Cell(row, 5).SetValue(ClosedXmlExportStyler.ToDouble(f.AvanceMes / 100m));
                ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(Valor(f.MontoMes, request)));
                ws.Cell(row, 7).SetValue(ClosedXmlExportStyler.ToDouble(f.AcumPct / 100m));
                if (request.MuestraUf)
                    ws.Cell(row, 8).SetValue(ClosedXmlExportStyler.ToDouble(Equivalente(f.MontoMes, request)));
                row++;
            }
        }

        int lastRow = Math.Max(row - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, cols, "tbl_estado_pago");
        ws.Range(headerRow + 1, 3, lastRow, 3).Style.NumberFormat.Format = fmt;
        ws.Range(headerRow + 1, 4, lastRow, 5).Style.NumberFormat.Format = "0.00%";
        ws.Range(headerRow + 1, 6, lastRow, 6).Style.NumberFormat.Format = fmt;
        ws.Range(headerRow + 1, 7, lastRow, 7).Style.NumberFormat.Format = "0.00%";
        if (request.MuestraUf)
            ws.Range(headerRow + 1, 8, lastRow, 8).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();

        int tot = lastRow + 2;
        ws.Range(tot, 1, tot, cols).Merge();
        ws.Cell(tot, 1).Value = $"TOTALES DEL MES · {mes.MesNombre} {mes.Anio}";
        ws.Cell(tot, 1).Style.Font.Bold = true;
        ws.Cell(tot, 1).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
        tot++;

        WriteTotalMonto(ws, tot++, "Trabajo del mes", t.TrabajoMes, request, fmt, cols);
        WriteTotalMonto(ws, tot++, "Acumulado a la fecha", t.Acum, request, fmt, cols);
        WriteTotalPct(ws, tot++, "Avance acumulado", t.PctAcum, cols);
        WriteTotalMonto(ws, tot++, $"Recupero de anticipo ({cfg.AnticipoPct:0.##} %)", t.RecuperoMes, request, fmt, cols);
        WriteTotalMonto(ws, tot++, $"Retención garantía ({cfg.RetencionPct:0.##} %)", t.RetencionMes, request, fmt, cols);
        WriteTotalMonto(ws, tot++, "Neto antes de IVA", t.Neto, request, fmt, cols);
        WriteTotalMonto(ws, tot++, $"IVA ({cfg.IvaPct:0.##} %)", t.Iva, request, fmt, cols);
        WriteTotalMonto(ws, tot, "TOTAL A PAGAR", t.Total, request, fmt, cols, resaltar: true);

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 46;
        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
    }

    /// <summary>Valor numérico en la moneda del proyecto: los montos del motor vienen en UF.</summary>
    private static decimal Valor(decimal uf, EstadoPagoExportRequest request) => request.MuestraUf
        ? uf
        : Math.Round(uf * request.UfRate, 0, MidpointRounding.AwayFromZero);

    /// <summary>Equivalente en pesos; 0 cuando el proyecto ya está en CLP (no se emite la columna).</summary>
    private static decimal Equivalente(decimal uf, EstadoPagoExportRequest request) => request.MuestraUf
        ? Math.Round(uf * request.UfRate, 0, MidpointRounding.AwayFromZero)
        : 0m;

    private static void WriteCronograma(IXLWorksheet ws, EstadoPagoExportRequest request)
    {
        int cols = request.MuestraUf ? 5 : 4;
        string fmt = ClosedXmlExportStyler.ProjectCurrencyExcelFormat(request.MonedaPresupuesto);
        string hdrTotal = $"TOTAL {(request.MuestraUf ? "UF" : "$")}";

        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "CRONOGRAMA ANUAL DE ESTADOS DE PAGO", cols);

        ws.Cell(2, 1).Value = "PROYECTO:";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Range(2, 2, 2, 3).Merge();
        ws.Cell(2, 2).Value = request.ProjectTitle;
        ws.Cell(2, 4).Value = $"AÑO {request.Config.Anio}";
        ws.Range(2, 1, 2, cols).Style.Font.Bold = true;

        int headerRow = 4;
        var headers = new List<string> { "MES", "FOLIO", "ESTADO", hdrTotal };
        if (request.MuestraUf)
            headers.Add("EQUIV. $");
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, headers);

        int row = headerRow + 1;
        int first = row;
        foreach (var m in request.Cronograma)
        {
            ws.Cell(row, 1).Value = m.MesNombre;
            ws.Cell(row, 2).Value = string.IsNullOrWhiteSpace(m.Folio) ? "—" : m.Folio;
            ws.Cell(row, 3).Value = m.EstadoLabel;
            if (m.Total is decimal total)
            {
                ws.Cell(row, 4).SetValue(ClosedXmlExportStyler.ToDouble(Valor(total, request)));
                ws.Cell(row, 4).Style.NumberFormat.Format = fmt;
                if (request.MuestraUf)
                {
                    ws.Cell(row, 5).SetValue(ClosedXmlExportStyler.ToDouble(Equivalente(total, request)));
                    ws.Cell(row, 5).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
                }
            }
            else
            {
                ws.Cell(row, 4).Value = "—";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            row++;
        }

        int lastRow = row - 1;
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, cols, "tbl_cronograma_estados");

        int totRow = lastRow + 2;
        ws.Range(totRow, 1, totRow, 3).Merge();
        ws.Cell(totRow, 1).Value = request.MuestraUf ? "SUMA DE TOTALES UF" : "SUMA DE TOTALES $";
        ws.Cell(totRow, 1).Style.Font.Bold = true;
        ws.Cell(totRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(totRow, 4).FormulaA1 = $"SUM(D{first}:D{lastRow})";
        ws.Cell(totRow, 4).Style.NumberFormat.Format = fmt;
        ws.Cell(totRow, 4).Style.Font.Bold = true;
        ws.Range(totRow, 1, totRow, cols).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(totRow, 1, totRow, cols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

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

    private static void WriteTotalMonto(
        IXLWorksheet ws, int row, string label, decimal uf, EstadoPagoExportRequest request, string fmt, int cols, bool resaltar = false)
    {
        WriteTotalBase(ws, row, label, resaltar);
        ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(Valor(uf, request)));
        ws.Cell(row, 6).Style.NumberFormat.Format = fmt;
        ws.Cell(row, 6).Style.Font.Bold = true;
        if (request.MuestraUf)
        {
            ws.Cell(row, 8).SetValue(ClosedXmlExportStyler.ToDouble(Equivalente(uf, request)));
            ws.Cell(row, 8).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        }
        ws.Range(row, 1, row, cols).Style.Fill.BackgroundColor =
            XLColor.FromColor(resaltar ? Color.FromArgb(229, 241, 255) : Color.FromArgb(248, 250, 252));
        if (resaltar)
            ws.Range(row, 1, row, cols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteTotalPct(IXLWorksheet ws, int row, string label, decimal valor, int cols)
    {
        WriteTotalBase(ws, row, label, resaltar: false);
        ws.Cell(row, 7).SetValue(ClosedXmlExportStyler.ToDouble(valor / 100m));
        ws.Cell(row, 7).Style.NumberFormat.Format = "0.00%";
        ws.Cell(row, 7).Style.Font.Bold = true;
        ws.Range(row, 1, row, cols).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(248, 250, 252));
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
