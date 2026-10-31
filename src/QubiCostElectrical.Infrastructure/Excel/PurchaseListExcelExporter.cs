using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

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
        string etiquetaMoneda = request.MuestraUf ? "UF" : "$";
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "RANK", "CÓDIGO", "DESCRIPCIÓN", "UNIDAD", "CATEGORÍA", "CANTIDAD NETA",
            "TOLERANCIA %", "CANTIDAD COMPRA", $"PRECIO MAT {etiquetaMoneda}", $"TOTAL NETO {etiquetaMoneda}", $"TOTAL COMPRA {etiquetaMoneda}", "LÍNEAS"
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
            ws.Cell(row, 9).SetValue(ClosedXmlExportStyler.ToDouble(PrecioUnitario(x, request)));
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
        ws.Range(headerRow + 1, 9, Math.Max(lastRow, headerRow + 1), 11).Style.NumberFormat.Format = FormatoMoneda(request);
        ws.Range(headerRow + 1, 12, Math.Max(lastRow, headerRow + 1), 12).Style.NumberFormat.Format = "#,##0";

        int totalRow = lastRow + 2;
        ws.Cell(totalRow, 10).Value = $"TOTAL COMPRA {etiquetaMoneda}";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;
        ws.Cell(totalRow, 11).FormulaA1 = lastRow > headerRow ? $"SUM(K{headerRow + 1}:K{lastRow})" : "0";
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = FormatoMoneda(request);
        ws.Range(totalRow, 10, totalRow, 11).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(totalRow, 10, totalRow, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(3).Width = 48;
        ws.Column(5).Width = 22;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
        wb.SaveAs(path);
    }

    /// <summary>
    /// Precio unitario del material en la moneda de presentación: UF cuando el proyecto lo
    /// permite; en CLP se usa el valor en pesos y se recurre a la conversión si no viniera informado.
    /// </summary>
    private static decimal PrecioUnitario(CompraMaterialViewModel x, PurchaseListExcelRequest request)
        => PrecioUnitario(x, request.MuestraUf, request.ValorUfClp);

    private static decimal PrecioUnitario(CompraMaterialViewModel x, bool muestraUf, decimal valorUfClp)
    {
        if (muestraUf)
            return x.MatUfUnitario;

        return x.MatClpUnitario > 0m
            ? x.MatClpUnitario
            : x.MatUfUnitario * ValorUf(valorUfClp);
    }

    private static decimal ValorUf(decimal valorUfClp)
        => valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;

    private static string FormatoMoneda(PurchaseListExcelRequest request)
        => request.MuestraUf ? ClosedXmlExportStyler.UfExcelFormat() : "$ #,##0";

    public void ExportRfq(string path, PurchaseListRfqRequest request)
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

        var ws = wb.Worksheets.Add("Cotizacion_RFQ");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "SOLICITUD DE COTIZACIÓN DE MATERIALES (RFQ)", 12);

        string company = string.IsNullOrWhiteSpace(request.CompanyName) ? "Qubi Cost Electrical" : request.CompanyName.Trim();
        ws.Cell(2, 1).Value = "Proyecto:";
        ws.Cell(2, 2).Value = request.ProjectTitle;
        ws.Cell(2, 4).Value = "Empresa solicitante:";
        ws.Cell(2, 5).Value = company;
        ws.Cell(2, 7).Value = "Fecha emisión:";
        ws.Cell(2, 8).SetValue(AppClock.Now);
        ws.Cell(2, 8).Style.NumberFormat.Format = "dd-mm-yyyy";
        ws.Range(2, 1, 2, 8).Style.Font.Bold = true;

        ws.Cell(3, 1).Value = "Instrucciones:";
        ws.Cell(3, 2).Value = "Favor ingresar Precio Unitario Neto, Plazo de Entrega y Marca Ofertada en las columnas correspondientes. Enviar planilla formalmente cotizada.";
        ws.Range(3, 2, 3, 12).Merge();
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 2).Style.Font.Italic = true;

        int headerRow = 5;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "ÍTEM", "CÓDIGO", "DESCRIPCIÓN DEL MATERIAL", "UD.", "CANTIDAD REQUERIDA",
            "CATEGORÍA", "MARCA / REF.", "PRECIO UNITARIO NETO ($)", "TOTAL NETO ($)",
            "PLAZO ENTREGA (DÍAS)", "MARCA OFERTADA", "OBSERVACIONES / EQUIVALENCIAS"
        });

        // Resaltar cabecera de precios para el proveedor
        ws.Cell(headerRow, 8).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(254, 240, 138));
        ws.Cell(headerRow, 8).Style.Font.FontColor = XLColor.FromColor(Color.FromArgb(113, 63, 18));

        int row = headerRow + 1;
        int itemIndex = 1;
        foreach (var x in request.Rows)
        {
            ws.Cell(row, 1).SetValue(itemIndex++);
            ws.Cell(row, 2).Value = x.Codigo;
            ws.Cell(row, 3).Value = x.Descripcion;
            ws.Cell(row, 4).Value = x.Unidad;
            ws.Cell(row, 5).SetValue(ClosedXmlExportStyler.ToDouble(x.CantidadCompra));
            ws.Cell(row, 6).Value = x.Categoria;
            ws.Cell(row, 7).Value = string.IsNullOrWhiteSpace(x.Proveedor) ? "—" : x.Proveedor;

            // Columna editable de precio
            ws.Cell(row, 8).Value = Blank.Value;
            ws.Cell(row, 8).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(254, 252, 232));

            // Fórmula total fila
            ws.Cell(row, 9).FormulaA1 = $"E{row}*H{row}";

            // Columnas de proveedor en blanco
            ws.Cell(row, 10).Value = Blank.Value;
            ws.Cell(row, 11).Value = Blank.Value;
            ws.Cell(row, 12).Value = Blank.Value;
            row++;
        }

        int lastRow = Math.Max(row - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, 12, "tbl_rfq_proveedor");
        ws.Range(headerRow + 1, 1, Math.Max(lastRow, headerRow + 1), 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(headerRow + 1, 4, Math.Max(lastRow, headerRow + 1), 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(headerRow + 1, 5, Math.Max(lastRow, headerRow + 1), 5).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(headerRow + 1, 8, Math.Max(lastRow, headerRow + 1), 9).Style.NumberFormat.Format = "$ #,##0";
        ws.Range(headerRow + 1, 10, Math.Max(lastRow, headerRow + 1), 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int totalRow = lastRow + 2;
        ws.Cell(totalRow, 8).Value = "TOTAL OFERTA PROVEEDOR NETO ($)";
        ws.Cell(totalRow, 8).Style.Font.Bold = true;
        ws.Cell(totalRow, 9).FormulaA1 = lastRow > headerRow ? $"SUM(I{headerRow + 1}:I{lastRow})" : "0";
        ws.Cell(totalRow, 9).Style.NumberFormat.Format = "$ #,##0";
        ws.Cell(totalRow, 9).Style.Font.Bold = true;
        ws.Range(totalRow, 8, totalRow, 9).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(totalRow, 8, totalRow, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 44;
        ws.Column(4).Width = 8;
        ws.Column(5).Width = 18;
        ws.Column(6).Width = 18;
        ws.Column(7).Width = 18;
        ws.Column(8).Width = 24;
        ws.Column(9).Width = 20;
        ws.Column(10).Width = 18;
        ws.Column(11).Width = 24;
        ws.Column(12).Width = 32;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
        wb.SaveAs(path);
    }

    public void ExportPurchaseOrder(string path, PurchaseOrderExcelRequest request)
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

        var ws = wb.Worksheets.Add("OrdenDeCompra");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "ORDEN DE COMPRA — MATERIALES ELÉCTRICOS", 7);

        string company = string.IsNullOrWhiteSpace(request.CompanyName) ? "Qubi Cost Electrical" : request.CompanyName.Trim();
        string numeroOc = !string.IsNullOrWhiteSpace(request.NumeroOc) ? request.NumeroOc : $"OC-{AppClock.Now:yyyyMMdd}-{AppClock.Now:HHmm}";

        ws.Cell(2, 1).Value = "N° Orden de Compra:";
        ws.Cell(2, 2).Value = numeroOc;
        ws.Cell(2, 4).Value = "Empresa Emisora:";
        ws.Cell(2, 5).Value = company;
        ws.Cell(2, 6).Value = "Fecha Emisión:";
        ws.Cell(2, 7).SetValue(AppClock.Now);
        ws.Cell(2, 7).Style.NumberFormat.Format = "dd-mm-yyyy";
        ws.Range(2, 1, 2, 7).Style.Font.Bold = true;

        ws.Cell(3, 1).Value = "Proyecto / Obra:";
        ws.Cell(3, 2).Value = request.ProjectTitle;
        ws.Cell(3, 4).Value = "Proveedor / Distribuidor:";
        ws.Cell(3, 5).Value = string.IsNullOrWhiteSpace(request.Proveedor) ? "Distribuidor Autorizado" : request.Proveedor;
        ws.Cell(3, 6).Value = "Moneda:";
        ws.Cell(3, 7).Value = request.MuestraUf ? "UF" : "CLP ($)";
        ws.Range(3, 1, 3, 7).Style.Font.Bold = true;

        ws.Cell(4, 1).Value = "Lugar de Entrega:";
        ws.Cell(4, 2).Value = "En Faena / Obra según coordinación con Jefe de Terreno.";
        ws.Range(4, 2, 4, 7).Merge();
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 2).Style.Font.Italic = true;

        int headerRow = 6;
        string etiquetaMoneda = request.MuestraUf ? "UF" : "$";
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "ÍTEM", "CÓDIGO", "DESCRIPCIÓN DEL MATERIAL", "UD.",
            "CANTIDAD A COMPRAR", $"PRECIO UNITARIO NETO ({etiquetaMoneda})", $"TOTAL NETO ({etiquetaMoneda})"
        });

        int row = headerRow + 1;
        int itemIndex = 1;
        foreach (var x in request.Rows)
        {
            ws.Cell(row, 1).SetValue(itemIndex++);
            ws.Cell(row, 2).Value = x.Codigo;
            ws.Cell(row, 3).Value = x.Descripcion;
            ws.Cell(row, 4).Value = x.Unidad;
            ws.Cell(row, 5).SetValue(ClosedXmlExportStyler.ToDouble(x.CantidadCompra));
            ws.Cell(row, 6).SetValue(ClosedXmlExportStyler.ToDouble(PrecioUnitario(x, request.MuestraUf, request.ValorUfClp)));
            ws.Cell(row, 7).FormulaA1 = $"E{row}*F{row}";
            row++;
        }

        int lastRow = Math.Max(row - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, 7, "tbl_orden_compra");
        ws.Range(headerRow + 1, 1, Math.Max(lastRow, headerRow + 1), 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(headerRow + 1, 4, Math.Max(lastRow, headerRow + 1), 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(headerRow + 1, 5, Math.Max(lastRow, headerRow + 1), 5).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(headerRow + 1, 6, Math.Max(lastRow, headerRow + 1), 7).Style.NumberFormat.Format = request.MuestraUf ? ClosedXmlExportStyler.UfExcelFormat() : "$ #,##0";

        // Totales: Subtotal Neto, IVA 19%, Total Bruto
        int subtotalRow = lastRow + 2;
        int ivaRow = subtotalRow + 1;
        int totalBrutoRow = ivaRow + 1;

        string formatoMoneda = request.MuestraUf ? ClosedXmlExportStyler.UfExcelFormat() : "$ #,##0";

        ws.Cell(subtotalRow, 6).Value = $"SUBTOTAL NETO ({etiquetaMoneda})";
        ws.Cell(subtotalRow, 6).Style.Font.Bold = true;
        ws.Cell(subtotalRow, 7).FormulaA1 = lastRow > headerRow ? $"SUM(G{headerRow + 1}:G{lastRow})" : "0";
        ws.Cell(subtotalRow, 7).Style.NumberFormat.Format = formatoMoneda;
        ws.Cell(subtotalRow, 7).Style.Font.Bold = true;

        ws.Cell(ivaRow, 6).Value = $"IVA (19%) ({etiquetaMoneda})";
        ws.Cell(ivaRow, 6).Style.Font.Bold = true;
        ws.Cell(ivaRow, 7).FormulaA1 = $"G{subtotalRow}*0.19";
        ws.Cell(ivaRow, 7).Style.NumberFormat.Format = formatoMoneda;

        ws.Cell(totalBrutoRow, 6).Value = $"TOTAL ORDEN DE COMPRA ({etiquetaMoneda})";
        ws.Cell(totalBrutoRow, 6).Style.Font.Bold = true;
        ws.Cell(totalBrutoRow, 7).FormulaA1 = $"G{subtotalRow}+G{ivaRow}";
        ws.Cell(totalBrutoRow, 7).Style.NumberFormat.Format = formatoMoneda;
        ws.Cell(totalBrutoRow, 7).Style.Font.Bold = true;

        ws.Range(subtotalRow, 6, totalBrutoRow, 7).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(229, 241, 255));
        ws.Range(subtotalRow, 6, totalBrutoRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        ws.Range(subtotalRow, 6, totalBrutoRow, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Firmas y aprobaciones
        int signRow = totalBrutoRow + 3;
        ws.Cell(signRow, 2).Value = "Solicitado por (Adquisiciones / Estimador)";
        ws.Cell(signRow, 2).Style.Font.Bold = true;
        ws.Cell(signRow, 5).Value = "Aprobado por (Jefatura de Obra / Gerencia)";
        ws.Cell(signRow, 5).Style.Font.Bold = true;
        ws.Cell(signRow + 2, 2).Value = "Firma: ___________________________";
        ws.Cell(signRow + 2, 5).Value = "Firma: ___________________________";

        ws.SheetView.FreezeRows(headerRow);
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 46;
        ws.Column(4).Width = 8;
        ws.Column(5).Width = 20;
        ws.Column(6).Width = 22;
        ws.Column(7).Width = 22;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);
        wb.SaveAs(path);
    }
}
