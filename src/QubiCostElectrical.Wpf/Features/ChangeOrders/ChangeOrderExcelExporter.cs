using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public static class ChangeOrderExcelExporter
{
    public static void Export(ChangeOrderRow oc, ProjectInfoRow project, bool muestraUf, decimal ufRate, string filePath)
    {
        using var wb = new XLWorkbook();

        string currencyFormat = muestraUf ? "#,##0.000 \"UF\"" : "\"$\"#,##0";
        double ToMoney(decimal uf)
        {
            if (muestraUf)
                return (double)uf;
            return (double)Math.Round(uf * ufRate, 0, MidpointRounding.AwayFromZero);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // HOJA 1: RESUMEN DEL ADICIONAL
        // ════════════════════════════════════════════════════════════════════════════
        var ws = wb.Worksheets.Add("Resumen Adicional");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, $"ORDEN DE CAMBIO / ADICIONAL DE OBRA — {oc.Numero}", 9);

        // Metadata
        ws.Cell(3, 1).Value = "Proyecto / Obra:";
        ws.Cell(3, 2).Value = project.Nombre ?? "Sin nombre";
        ws.Cell(3, 5).Value = "Tipo:";
        ws.Cell(3, 6).Value = project.TipoProyecto ?? "Instalación Eléctrica";
        ws.Cell(3, 8).Value = "Fecha:";
        ws.Cell(3, 9).Value = oc.FechaCreacion.ToString("dd/MM/yyyy");

        ws.Cell(4, 1).Value = "Cliente / Mandante:";
        ws.Cell(4, 2).Value = project.Cliente ?? "Sin mandante";
        ws.Cell(4, 5).Value = "Ubicación:";
        ws.Cell(4, 6).Value = project.Ubicacion ?? "Sin ubicación";
        ws.Cell(4, 8).Value = "Estado:";
        ws.Cell(4, 9).Value = oc.Estado;

        ws.Cell(5, 1).Value = "Título Adicional:";
        ws.Cell(5, 2).Value = oc.Titulo;
        ws.Cell(5, 5).Value = "Elaborado por:";
        ws.Cell(5, 6).Value = string.IsNullOrWhiteSpace(oc.CreadoPor) ? (project.UsuarioNombre ?? "Estimador") : oc.CreadoPor;
        ws.Cell(5, 8).Value = "Moneda:";
        ws.Cell(5, 9).Value = muestraUf ? "UF" : $"CLP (1 UF = ${ufRate:N0})";

        if (!string.IsNullOrWhiteSpace(oc.Nota))
        {
            ws.Cell(6, 1).Value = "Nota / Justificación:";
            ws.Cell(6, 2).Value = oc.Nota;
            ws.Range(6, 2, 6, 9).Merge();
        }

        ws.Range(3, 1, 6, 9).Style.Font.FontSize = 9.5;
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 5).Style.Font.Bold = true;
        ws.Cell(3, 8).Style.Font.Bold = true;
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 5).Style.Font.Bold = true;
        ws.Cell(4, 8).Style.Font.Bold = true;
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 5).Style.Font.Bold = true;
        ws.Cell(5, 8).Style.Font.Bold = true;
        ws.Cell(6, 1).Style.Font.Bold = true;

        // Header de Tabla de Partidas
        int headerRow = 8;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "ÍTEM", "DESCRIPCIÓN DE LA PARTIDA", "TIPO", "UNIDAD",
            "CANT. BASE", "Δ CANT. ADICIONAL", "CANT. NUEVA", "P.U. VENTA", "SUBTOTAL VENTA"
        });

        int r = headerRow + 1;
        int firstDataRow = r;

        foreach (var linea in oc.Lineas)
        {
            if (linea.EsTitulo)
            {
                ws.Range(r, 1, r, 9).Merge();
                ws.Cell(r, 1).Value = $"{linea.Item}  {linea.DescripcionPartida}";
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                ws.Cell(r, 1).Style.Font.FontColor = XLColor.FromHtml("#0B2447");
                r++;
                continue;
            }

            ws.Cell(r, 1).Value = linea.Item;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 2).Value = linea.DescripcionPartida;

            ws.Cell(r, 3).Value = linea.EsContractual ? "Contractual" : "Extraordinaria";
            ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 4).Value = linea.Unidad;
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 5).SetValue((double)linea.CantidadBase);
            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.0";

            ws.Cell(r, 6).SetValue((double)linea.CantidadAdicional);
            ws.Cell(r, 6).Style.NumberFormat.Format = "+#,##0.0;-#,##0.0;0.0";
            ws.Cell(r, 6).Style.Font.Bold = true;

            ws.Cell(r, 7).SetValue((double)linea.CantidadNueva);
            ws.Cell(r, 7).Style.NumberFormat.Format = "#,##0.0";

            ws.Cell(r, 8).SetValue(ToMoney(linea.PrecioUnitarioContratoUf));
            ws.Cell(r, 8).Style.NumberFormat.Format = currencyFormat;

            ws.Cell(r, 9).SetValue(ToMoney(linea.SubtotalUf));
            ws.Cell(r, 9).Style.NumberFormat.Format = currencyFormat;
            ws.Cell(r, 9).Style.Font.Bold = true;

            r++;
        }

        int lastDataRow = Math.Max(r - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastDataRow, 9, "tbl_partidas_adicional");

        // Totales de la Orden de Cambio
        int subtotalRow = r;
        ws.Range(subtotalRow, 1, subtotalRow, 8).Merge();
        ws.Cell(subtotalRow, 1).Value = "SUBTOTAL COSTO DIRECTO:";
        ws.Cell(subtotalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(subtotalRow, 1).Style.Font.Bold = true;
        ws.Cell(subtotalRow, 9).SetValue(ToMoney(oc.DeltaCostoUf));
        ws.Cell(subtotalRow, 9).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(subtotalRow, 9).Style.Font.Bold = true;
        ws.Range(subtotalRow, 1, subtotalRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        r++;

        int ggRow = r;
        ws.Range(ggRow, 1, ggRow, 8).Merge();
        ws.Cell(ggRow, 1).Value = $"GASTOS GENERALES ({oc.GastosGeneralesPct:N1}%):";
        ws.Cell(ggRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(ggRow, 9).SetValue(ToMoney(oc.DeltaGastosGeneralesUf));
        ws.Cell(ggRow, 9).Style.NumberFormat.Format = currencyFormat;
        ws.Range(ggRow, 1, ggRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        r++;

        int utRow = r;
        ws.Range(utRow, 1, utRow, 8).Merge();
        ws.Cell(utRow, 1).Value = $"UTILIDAD ({oc.UtilidadPct:N1}%):";
        ws.Cell(utRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(utRow, 9).SetValue(ToMoney(oc.DeltaUtilidadUf));
        ws.Cell(utRow, 9).Style.NumberFormat.Format = currencyFormat;
        ws.Range(utRow, 1, utRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        r++;

        int totalRow = r;
        ws.Range(totalRow, 1, totalRow, 8).Merge();
        ws.Cell(totalRow, 1).Value = "TOTAL VENTA NETO ADICIONAL:";
        ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 1).Style.Font.FontSize = 11;
        ws.Cell(totalRow, 9).SetValue(ToMoney(oc.DeltaVentaUf));
        ws.Cell(totalRow, 9).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(totalRow, 9).Style.Font.Bold = true;
        ws.Cell(totalRow, 9).Style.Font.FontSize = 11;
        ws.Range(totalRow, 1, totalRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
        ws.Range(totalRow, 1, totalRow, 9).Style.Font.FontColor = XLColor.FromHtml("#15803D");

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 46;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 8;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 16;
        ws.Column(9).Width = 18;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);

        // ════════════════════════════════════════════════════════════════════════════
        // HOJA 2: CUBICACIÓN Y MATERIALES POR PARTIDA
        // ════════════════════════════════════════════════════════════════════════════
        var wsMat = wb.Worksheets.Add("Cubicación y Materiales");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(wsMat);
        ClosedXmlExportStyler.AddSheetTitle(wsMat, $"ANEXO: PLANILLA DE MATERIALES Y CUBICACIÓN — {oc.Numero}", 8);

        int mHeaderRow = 4;
        ClosedXmlExportStyler.SetHeaderRow(wsMat, mHeaderRow, new[]
        {
            "PARTIDA ÍTEM", "DESCRIPCIÓN PARTIDA", "CÓDIGO MATERIAL", "DESCRIPCIÓN MATERIAL",
            "UNIDAD", "CANTIDAD", "P.U. COSTO", "TOTAL COSTO"
        });

        int mr = mHeaderRow + 1;
        foreach (var linea in oc.Lineas.Where(l => !l.EsTitulo && l.Materiales != null && l.Materiales.Count > 0))
        {
            foreach (var mat in linea.Materiales)
            {
                wsMat.Cell(mr, 1).Value = linea.Item;
                wsMat.Cell(mr, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsMat.Cell(mr, 2).Value = linea.DescripcionPartida;

                wsMat.Cell(mr, 3).Value = mat.Codigo;
                wsMat.Cell(mr, 4).Value = mat.Descripcion;

                wsMat.Cell(mr, 5).Value = mat.Unidad;
                wsMat.Cell(mr, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsMat.Cell(mr, 6).SetValue((double)mat.Cantidad);
                wsMat.Cell(mr, 6).Style.NumberFormat.Format = "#,##0.00";

                wsMat.Cell(mr, 7).SetValue(ToMoney(mat.UfUnit));
                wsMat.Cell(mr, 7).Style.NumberFormat.Format = currencyFormat;

                wsMat.Cell(mr, 8).SetValue(ToMoney(mat.UfTotal));
                wsMat.Cell(mr, 8).Style.NumberFormat.Format = currencyFormat;
                wsMat.Cell(mr, 8).Style.Font.Bold = true;

                mr++;
            }
        }

        int lastMatRow = Math.Max(mr - 1, mHeaderRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(wsMat, mHeaderRow, 1, lastMatRow, 8, "tbl_materiales_adicional");

        if (lastMatRow > mHeaderRow)
        {
            wsMat.Range(mr, 1, mr, 7).Merge();
            wsMat.Cell(mr, 1).Value = "TOTAL MATERIALES ADICIONAL:";
            wsMat.Cell(mr, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsMat.Cell(mr, 1).Style.Font.Bold = true;

            wsMat.Cell(mr, 8).FormulaA1 = $"=SUM(H{mHeaderRow + 1}:H{lastMatRow})";
            wsMat.Cell(mr, 8).Style.NumberFormat.Format = currencyFormat;
            wsMat.Cell(mr, 8).Style.Font.Bold = true;
            wsMat.Range(mr, 1, mr, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        }

        wsMat.Column(1).Width = 14;
        wsMat.Column(2).Width = 36;
        wsMat.Column(3).Width = 18;
        wsMat.Column(4).Width = 44;
        wsMat.Column(5).Width = 8;
        wsMat.Column(6).Width = 12;
        wsMat.Column(7).Width = 16;
        wsMat.Column(8).Width = 18;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(wsMat);

        wb.SaveAs(filePath);
    }

    public static void ExportList(IReadOnlyList<ChangeOrderRow> orders, ProjectInfoRow project, decimal originalContractUf, bool muestraUf, decimal ufRate, string filePath)
    {
        using var wb = new XLWorkbook();

        string currencyFormat = muestraUf ? "#,##0.000 \"UF\"" : "\"$\"#,##0";
        double ToMoney(decimal uf)
        {
            if (muestraUf)
                return (double)uf;
            return (double)Math.Round(uf * ufRate, 0, MidpointRounding.AwayFromZero);
        }

        var ws = wb.Worksheets.Add("Control Adicionales");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
        ClosedXmlExportStyler.AddSheetTitle(ws, "CONTROL GENERAL DE ADICIONALES Y AUMENTOS DE CONTRATO", 9);

        // Metadata
        ws.Cell(3, 1).Value = "Proyecto / Obra:";
        ws.Cell(3, 2).Value = project.Nombre ?? "Sin nombre";
        ws.Cell(3, 5).Value = "Tipo:";
        ws.Cell(3, 6).Value = project.TipoProyecto ?? "Instalación Eléctrica";
        ws.Cell(3, 8).Value = "Fecha Emisión:";
        ws.Cell(3, 9).Value = DateTime.Now.ToString("dd/MM/yyyy");

        ws.Cell(4, 1).Value = "Cliente / Mandante:";
        ws.Cell(4, 2).Value = project.Cliente ?? "Sin mandante";
        ws.Cell(4, 5).Value = "Ubicación:";
        ws.Cell(4, 6).Value = project.Ubicacion ?? "Sin ubicación";
        ws.Cell(4, 8).Value = "Total Adicionales:";
        ws.Cell(4, 9).Value = orders.Count;

        ws.Range(3, 1, 4, 9).Style.Font.FontSize = 9.5;
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 5).Style.Font.Bold = true;
        ws.Cell(3, 8).Style.Font.Bold = true;
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 5).Style.Font.Bold = true;
        ws.Cell(4, 8).Style.Font.Bold = true;

        // KPI Box
        decimal totalAprobadoUf = orders.Where(x => x.Estado == "Aprobado").Sum(x => x.DeltaVentaUf);
        decimal totalTramiteUf = orders.Where(x => x.Estado is "Borrador" or "En revisión").Sum(x => x.DeltaVentaUf);
        decimal totalProyectadoUf = originalContractUf + totalAprobadoUf;

        ws.Cell(6, 1).Value = "CONTRATO ORIGINAL:";
        ws.Cell(6, 2).SetValue(ToMoney(originalContractUf));
        ws.Cell(6, 2).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(6, 2).Style.Font.Bold = true;

        ws.Cell(6, 4).Value = "APROBADOS MANDANTE:";
        ws.Cell(6, 5).SetValue(ToMoney(totalAprobadoUf));
        ws.Cell(6, 5).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(6, 5).Style.Font.Bold = true;
        ws.Cell(6, 5).Style.Font.FontColor = XLColor.FromHtml("#15803D");

        ws.Cell(6, 7).Value = "EN TRÁMITE / ITO:";
        ws.Cell(6, 8).SetValue(ToMoney(totalTramiteUf));
        ws.Cell(6, 8).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(6, 8).Style.Font.Bold = true;
        ws.Cell(6, 8).Style.Font.FontColor = XLColor.FromHtml("#B45309");

        ws.Cell(7, 1).Value = "TOTAL OBRA PROYECTADO:";
        ws.Cell(7, 2).SetValue(ToMoney(totalProyectadoUf));
        ws.Cell(7, 2).Style.NumberFormat.Format = currencyFormat;
        ws.Cell(7, 2).Style.Font.Bold = true;
        ws.Cell(7, 2).Style.Font.FontSize = 11;

        ws.Range(6, 1, 7, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
        ws.Range(6, 1, 7, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(6, 1, 7, 9).Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

        // Table
        int headerRow = 9;
        ClosedXmlExportStyler.SetHeaderRow(ws, headerRow, new[]
        {
            "N° ADICIONAL", "TÍTULO", "ESTADO", "PARTIDAS", "FECHA",
            "CREADA POR", "COSTO DIRECTO", "VENTA NETA", "NOTA / JUSTIFICACIÓN"
        });

        int r = headerRow + 1;
        int firstRow = r;
        foreach (var oc in orders)
        {
            ws.Cell(r, 1).Value = oc.Numero;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 1).Style.Font.Bold = true;

            ws.Cell(r, 2).Value = oc.Titulo;

            ws.Cell(r, 3).Value = oc.Estado;
            ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 4).Value = oc.Lineas.Count;
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 5).Value = oc.FechaCreacion.ToString("dd/MM/yyyy");
            ws.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 6).Value = oc.CreadoPor;

            ws.Cell(r, 7).SetValue(ToMoney(oc.DeltaCostoUf));
            ws.Cell(r, 7).Style.NumberFormat.Format = currencyFormat;

            ws.Cell(r, 8).SetValue(ToMoney(oc.DeltaVentaUf));
            ws.Cell(r, 8).Style.NumberFormat.Format = currencyFormat;
            ws.Cell(r, 8).Style.Font.Bold = true;

            ws.Cell(r, 9).Value = oc.Nota;

            r++;
        }

        int lastRow = Math.Max(r - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(ws, headerRow, 1, lastRow, 9, "tbl_control_adicionales");

        if (lastRow > headerRow)
        {
            ws.Range(r, 1, r, 6).Merge();
            ws.Cell(r, 1).Value = "TOTALES ACUMULADOS:";
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(r, 1).Style.Font.Bold = true;

            ws.Cell(r, 7).FormulaA1 = $"=SUM(G{firstRow}:G{lastRow})";
            ws.Cell(r, 7).Style.NumberFormat.Format = currencyFormat;
            ws.Cell(r, 7).Style.Font.Bold = true;

            ws.Cell(r, 8).FormulaA1 = $"=SUM(H{firstRow}:H{lastRow})";
            ws.Cell(r, 8).Style.NumberFormat.Format = currencyFormat;
            ws.Cell(r, 8).Style.Font.Bold = true;

            ws.Range(r, 1, r, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        }

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 36;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 10;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 18;
        ws.Column(7).Width = 18;
        ws.Column(8).Width = 18;
        ws.Column(9).Width = 30;

        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);

        wb.SaveAs(filePath);
    }
}
