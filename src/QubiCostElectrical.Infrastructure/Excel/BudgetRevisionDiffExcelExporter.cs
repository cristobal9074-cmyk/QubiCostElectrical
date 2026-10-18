using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

public sealed class BudgetRevisionDiffExcelExporter : IBudgetRevisionDiffExcelExporter
{
    public void Export(BudgetVersionComparisonSummary summary, string outputPath)
    {
        if (summary == null) throw new ArgumentNullException(nameof(summary));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Ruta de exportación requerida.", nameof(outputPath));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Segoe UI";
        wb.Style.Font.FontSize = 9;

        decimal valorUf = summary.ValorUf > 0m ? summary.ValorUf : PriceConversion.DefaultValorUfClp;
        bool uf = summary.MuestraUf;
        string fmtMonto = uf ? ClosedXmlExportStyler.UfExcelFormat() : ClosedXmlExportStyler.ClpExcelFormat();

        // ══════════════════════════════════════════════════════════════════
        // HOJA 1: RESUMEN EJECUTIVO
        // ══════════════════════════════════════════════════════════════════
        var ws1 = wb.Worksheets.Add("01_RESUMEN_EJECUTIVO");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws1);
        ClosedXmlExportStyler.AddSheetTitle(ws1, "01 · RESUMEN EJECUTIVO DE VARIACIONES Y ADICIONALES", 8);

        // Fila 2: Parámetros del proyecto y anclaje de Valor UF en $B$2
        ws1.Cell(2, 1).Value = uf ? "VALOR UF REF:" : "FACTOR INTERNO:";
        ws1.Cell(2, 1).Style.Font.Bold = true;
        ws1.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(2, 2).SetValue(ClosedXmlExportStyler.ToDouble(valorUf));
        ws1.Cell(2, 2).Style.Font.Bold = true;
        ws1.Cell(2, 2).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();

        ws1.Cell(2, 3).Value = "CLIENTE / CONSTRUCTORA:";
        ws1.Cell(2, 3).Style.Font.Bold = true;
        ws1.Cell(2, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(2, 4).Value = summary.ClientName;

        ws1.Cell(2, 5).Value = "PROYECTO:";
        ws1.Cell(2, 5).Style.Font.Bold = true;
        ws1.Cell(2, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(2, 6).Value = summary.ProjectTitle;

        ws1.Cell(2, 7).Value = "FECHA:";
        ws1.Cell(2, 7).Style.Font.Bold = true;
        ws1.Cell(2, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(2, 8).Value = summary.FechaComparacion.ToString("dd-MM-yyyy");

        // Fila 3: Versiones comparadas
        ws1.Cell(3, 1).Value = "VERSIÓN BASE:";
        ws1.Cell(3, 1).Style.Font.Bold = true;
        ws1.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(3, 2).Value = summary.BaselineLabel;

        ws1.Cell(3, 3).Value = "VERSIÓN MODIFICADA:";
        ws1.Cell(3, 3).Style.Font.Bold = true;
        ws1.Cell(3, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(3, 4).Value = summary.CurrentLabel;

        ws1.Cell(3, 5).Value = "CONTRATISTA:";
        ws1.Cell(3, 5).Style.Font.Bold = true;
        ws1.Cell(3, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws1.Cell(3, 6).Value = summary.ContractorName;

        // Tabla Resumen Financiero
        int tRow = 5;
        ClosedXmlExportStyler.SetHeaderRow(ws1, tRow, new[]
        {
            "CONCEPTO FINANCIERO", "PARTIDAS", uf ? "MONTO EN UF" : "MONTO $", uf ? "MONTO EN CLP" : "MONTO $", "INCIDENCIA %", "OBSERVACIÓN"
        });

        // Filas de datos
        // 1. Presupuesto Base
        tRow++;
        ws1.Cell(tRow, 1).Value = $"Presupuesto Base ({summary.BaselineLabel})";
        ws1.Cell(tRow, 2).SetValue(summary.Rows.Count(r => r.BaselineUf > 0));
        ws1.Cell(tRow, 3).SetValue(ClosedXmlExportStyler.ToDouble(summary.BaselineUf));
        ws1.Cell(tRow, 3).Style.NumberFormat.Format = fmtMonto;
        ws1.Cell(tRow, 4).FormulaA1 = $"C{tRow}*$B$2";
        ws1.Cell(tRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws1.Cell(tRow, 5).SetValue(1.0);
        ws1.Cell(tRow, 5).Style.NumberFormat.Format = "0.00%";
        ws1.Cell(tRow, 6).Value = "Oferta contractual de referencia";

        // 2. Presupuesto Modificado
        tRow++;
        ws1.Cell(tRow, 1).Value = $"Presupuesto Modificado ({summary.CurrentLabel})";
        ws1.Cell(tRow, 2).SetValue(summary.Rows.Count(r => r.CurrentUf > 0));
        ws1.Cell(tRow, 3).SetValue(ClosedXmlExportStyler.ToDouble(summary.CurrentUf));
        ws1.Cell(tRow, 3).Style.NumberFormat.Format = fmtMonto;
        ws1.Cell(tRow, 4).FormulaA1 = $"C{tRow}*$B$2";
        ws1.Cell(tRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws1.Cell(tRow, 5).FormulaA1 = $"C{tRow}/C{tRow - 1}";
        ws1.Cell(tRow, 5).Style.NumberFormat.Format = "0.00%";
        ws1.Cell(tRow, 6).Value = "Estado resultante post-modificación";

        // 3. Adicionales (+)
        tRow++;
        ws1.Cell(tRow, 1).Value = "🟢 Obras Adicionales y Mayores Metrajes (+)";
        ws1.Cell(tRow, 1).Style.Font.FontColor = XLColor.FromHtml("#059669");
        ws1.Cell(tRow, 2).SetValue(summary.AddedCount + summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaUf > 0));
        ws1.Cell(tRow, 3).SetValue(ClosedXmlExportStyler.ToDouble(summary.TotalAdicionalesUf));
        ws1.Cell(tRow, 3).Style.NumberFormat.Format = fmtMonto;
        ws1.Cell(tRow, 3).Style.Font.Bold = true;
        ws1.Cell(tRow, 3).Style.Font.FontColor = XLColor.FromHtml("#059669");
        ws1.Cell(tRow, 4).FormulaA1 = $"C{tRow}*$B$2";
        ws1.Cell(tRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws1.Cell(tRow, 4).Style.Font.Bold = true;
        ws1.Cell(tRow, 4).Style.Font.FontColor = XLColor.FromHtml("#059669");
        ws1.Cell(tRow, 5).FormulaA1 = $"C{tRow}/C6";
        ws1.Cell(tRow, 5).Style.NumberFormat.Format = "0.00%";
        ws1.Cell(tRow, 6).Value = "Aprobación y cobro a constructora";

        // 4. Deductivos (-)
        tRow++;
        ws1.Cell(tRow, 1).Value = "🔴 Obras Deductivas y Menores Metrajes (-)";
        ws1.Cell(tRow, 1).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
        ws1.Cell(tRow, 2).SetValue(summary.RemovedCount + summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaUf < 0));
        ws1.Cell(tRow, 3).SetValue(ClosedXmlExportStyler.ToDouble(summary.TotalDeductivosUf));
        ws1.Cell(tRow, 3).Style.NumberFormat.Format = fmtMonto;
        ws1.Cell(tRow, 3).Style.Font.Bold = true;
        ws1.Cell(tRow, 3).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
        ws1.Cell(tRow, 4).FormulaA1 = $"C{tRow}*$B$2";
        ws1.Cell(tRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws1.Cell(tRow, 4).Style.Font.Bold = true;
        ws1.Cell(tRow, 4).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
        ws1.Cell(tRow, 5).FormulaA1 = $"C{tRow}/C6";
        ws1.Cell(tRow, 5).Style.NumberFormat.Format = "0.00%";
        ws1.Cell(tRow, 6).Value = "Deducción de presupuesto contratado";

        // 5. Total Neto de Variación (Cobro)
        tRow++;
        ws1.Cell(tRow, 1).Value = "⚡ SALDO NETO RECLAMO DE ADICIONALES";
        ws1.Cell(tRow, 1).Style.Font.Bold = true;
        ws1.Cell(tRow, 2).SetValue(summary.Rows.Count(r => r.DeltaUf != 0));
        ws1.Cell(tRow, 3).FormulaA1 = $"C8+C9";
        ws1.Cell(tRow, 3).Style.NumberFormat.Format = fmtMonto;
        ws1.Cell(tRow, 3).Style.Font.Bold = true;
        ws1.Cell(tRow, 4).FormulaA1 = $"C{tRow}*$B$2";
        ws1.Cell(tRow, 4).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws1.Cell(tRow, 4).Style.Font.Bold = true;
        ws1.Cell(tRow, 5).FormulaA1 = $"C{tRow}/C6";
        ws1.Cell(tRow, 5).Style.NumberFormat.Format = "0.00%";
        ws1.Cell(tRow, 6).Value = "Monto a facturar vía Orden de Cambio";
        ws1.Range(tRow, 1, tRow, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");

        ws1.Columns().AdjustToContents();
        if (!uf)
            ws1.Column(3).Hide();

        // ══════════════════════════════════════════════════════════════════
        // HOJA 2: DETALLE COMPLETO DE VARIACIONES CON FÓRMULAS VIVAS
        // ══════════════════════════════════════════════════════════════════
        var ws2 = wb.Worksheets.Add("02_DETALLE_VARIACIONES");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws2);
        ClosedXmlExportStyler.AddSheetTitle(ws2, "02 · DETALLE COMPARATIVO DE PARTIDAS", 15);

        // Header
        int hRow = 3;
        ClosedXmlExportStyler.SetHeaderRow(ws2, hRow, new[]
        {
            "ÍTEM", "DESCRIPCIÓN DE LA PARTIDA", "UNIDAD",
            "CANT. BASE", "CANT. ACT.", "DELTA CANTIDAD",
            uf ? "P.U. BASE UF" : "P.U. BASE $", uf ? "P.U. ACT. UF" : "P.U. ACT. $", uf ? "DELTA P.U. UF" : "DELTA P.U. $",
            uf ? "TOTAL BASE UF" : "TOTAL BASE $", uf ? "TOTAL ACT. UF" : "TOTAL ACT. $", uf ? "DELTA TOTAL UF" : "DELTA TOTAL $", "DELTA TOTAL CLP",
            "ESTADO SEMÁFORO", "DETALLE TÉCNICO DE LA MODIFICACIÓN"
        });

        int dRow = hRow + 1;
        foreach (var r in summary.Rows)
        {
            ws2.Cell(dRow, 1).SetValue(r.Item);
            ws2.Cell(dRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws2.Cell(dRow, 2).SetValue(r.Descripcion);
            ws2.Cell(dRow, 3).SetValue(r.Unidad);
            ws2.Cell(dRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Cantidades
            ws2.Cell(dRow, 4).SetValue(ClosedXmlExportStyler.ToDouble(r.CantidadBase));
            ws2.Cell(dRow, 4).Style.NumberFormat.Format = "#,##0.##";
            ws2.Cell(dRow, 5).SetValue(ClosedXmlExportStyler.ToDouble(r.CantidadActual));
            ws2.Cell(dRow, 5).Style.NumberFormat.Format = "#,##0.##";

            // FÓRMULA VIVA Delta Cantidad = E - D
            ws2.Cell(dRow, 6).FormulaA1 = $"E{dRow}-D{dRow}";
            ws2.Cell(dRow, 6).Style.NumberFormat.Format = "+#,##0.##;-#,##0.##;\"—\"";
            ws2.Cell(dRow, 6).Style.Font.Bold = true;

            // Precios Unitarios
            ws2.Cell(dRow, 7).SetValue(ClosedXmlExportStyler.ToDouble(uf ? r.PuUfBase : r.PuUfBase * valorUf));
            ws2.Cell(dRow, 7).Style.NumberFormat.Format = fmtMonto;
            ws2.Cell(dRow, 8).SetValue(ClosedXmlExportStyler.ToDouble(uf ? r.PuUfActual : r.PuUfActual * valorUf));
            ws2.Cell(dRow, 8).Style.NumberFormat.Format = fmtMonto;

            // FÓRMULA VIVA Delta PU = H - G
            ws2.Cell(dRow, 9).FormulaA1 = $"H{dRow}-G{dRow}";
            ws2.Cell(dRow, 9).Style.NumberFormat.Format = uf ? "+0.000;-0.000;\"—\"" : "+$ #,##0;-$ #,##0;\"—\"";

            // Totales
            ws2.Cell(dRow, 10).SetValue(ClosedXmlExportStyler.ToDouble(uf ? r.BaselineUf : r.BaselineUf * valorUf));
            ws2.Cell(dRow, 10).Style.NumberFormat.Format = fmtMonto;
            ws2.Cell(dRow, 11).SetValue(ClosedXmlExportStyler.ToDouble(uf ? r.CurrentUf : r.CurrentUf * valorUf));
            ws2.Cell(dRow, 11).Style.NumberFormat.Format = fmtMonto;

            // FÓRMULA VIVA Delta Total = K - J
            ws2.Cell(dRow, 12).FormulaA1 = $"K{dRow}-J{dRow}";
            ws2.Cell(dRow, 12).Style.NumberFormat.Format = uf ? "+#,##0.000;-#,##0.000;\"—\"" : "+$ #,##0;-$ #,##0;\"—\"";
            ws2.Cell(dRow, 12).Style.Font.Bold = true;

            // FÓRMULA VIVA Delta Total CLP = L{row} * '01_RESUMEN_EJECUTIVO'!$B$2
            ws2.Cell(dRow, 13).FormulaA1 = $"L{dRow}*'01_RESUMEN_EJECUTIVO'!$B$2";
            ws2.Cell(dRow, 13).Style.NumberFormat.Format = "+$ #,##0;-$ #,##0;\"—\"";
            ws2.Cell(dRow, 13).Style.Font.Bold = true;

            // Semáforo y estilo visual
            ws2.Cell(dRow, 14).SetValue(r.DisplayBadge);
            ws2.Cell(dRow, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws2.Cell(dRow, 14).Style.Font.Bold = true;

            if (r.DiffKind == RevisionDiffKind.Adicional)
            {
                ws2.Cell(dRow, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("#ECFDF5");
                ws2.Cell(dRow, 14).Style.Font.FontColor = XLColor.FromHtml("#059669");
                ws2.Cell(dRow, 12).Style.Font.FontColor = XLColor.FromHtml("#059669");
                ws2.Cell(dRow, 13).Style.Font.FontColor = XLColor.FromHtml("#059669");
            }
            else if (r.DiffKind == RevisionDiffKind.Deductivo)
            {
                ws2.Cell(dRow, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
                ws2.Cell(dRow, 14).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                ws2.Cell(dRow, 12).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                ws2.Cell(dRow, 13).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
            }
            else if (r.DiffKind == RevisionDiffKind.Modificado)
            {
                ws2.Cell(dRow, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFBEB");
                ws2.Cell(dRow, 14).Style.Font.FontColor = XLColor.FromHtml("#D97706");
                if (r.DeltaUf > 0)
                {
                    ws2.Cell(dRow, 12).Style.Font.FontColor = XLColor.FromHtml("#059669");
                    ws2.Cell(dRow, 13).Style.Font.FontColor = XLColor.FromHtml("#059669");
                }
                else if (r.DeltaUf < 0)
                {
                    ws2.Cell(dRow, 12).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                    ws2.Cell(dRow, 13).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                }
            }

            ws2.Cell(dRow, 15).SetValue(r.DetalleCambio);
            dRow++;
        }

        // Fila de Total General con fórmulas =SUM(...)
        int totRow = dRow;
        ws2.Cell(totRow, 1).Value = "TOTALES";
        ws2.Cell(totRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws2.Cell(totRow, 2).Value = $"{summary.Rows.Count} PARTIDAS COMPARADAS";
        ws2.Range(totRow, 1, totRow, 9).Style.Font.Bold = true;

        ws2.Cell(totRow, 10).FormulaA1 = $"SUM(J4:J{totRow - 1})";
        ws2.Cell(totRow, 10).Style.NumberFormat.Format = fmtMonto;
        ws2.Cell(totRow, 10).Style.Font.Bold = true;

        ws2.Cell(totRow, 11).FormulaA1 = $"SUM(K4:K{totRow - 1})";
        ws2.Cell(totRow, 11).Style.NumberFormat.Format = fmtMonto;
        ws2.Cell(totRow, 11).Style.Font.Bold = true;

        ws2.Cell(totRow, 12).FormulaA1 = $"SUM(L4:L{totRow - 1})";
        ws2.Cell(totRow, 12).Style.NumberFormat.Format = fmtMonto;
        ws2.Cell(totRow, 12).Style.Font.Bold = true;

        ws2.Cell(totRow, 13).FormulaA1 = $"SUM(M4:M{totRow - 1})";
        ws2.Cell(totRow, 13).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws2.Cell(totRow, 13).Style.Font.Bold = true;

        ws2.Range(totRow, 1, totRow, 15).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        ws2.Columns().AdjustToContents();
        if (!uf)
            ws2.Column(13).Hide();

        // ══════════════════════════════════════════════════════════════════
        // HOJA 3: SOLICITUD FORMAL DE ADICIONALES (SOLO INCREMENTOS NETOS)
        // ══════════════════════════════════════════════════════════════════
        var ws3 = wb.Worksheets.Add("03_SOLICITUD_ADICIONALES");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(ws3);
        ClosedXmlExportStyler.AddSheetTitle(ws3, "03 · SOLICITUD DE APROBACIÓN DE OBRAS ADICIONALES", 8);

        ws3.Cell(2, 1).Value = "PROYECTO:";
        ws3.Cell(2, 1).Style.Font.Bold = true;
        ws3.Cell(2, 2).Value = summary.ProjectTitle;

        ws3.Cell(2, 4).Value = "CLIENTE / ITO:";
        ws3.Cell(2, 4).Style.Font.Bold = true;
        ws3.Cell(2, 5).Value = summary.ClientName;

        ws3.Cell(2, 7).Value = uf ? "VALOR UF:" : "FACTOR INTERNO:";
        ws3.Cell(2, 7).Style.Font.Bold = true;
        ws3.Cell(2, 8).FormulaA1 = "'01_RESUMEN_EJECUTIVO'!$B$2";
        ws3.Cell(2, 8).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();

        int aHeadRow = 4;
        ClosedXmlExportStyler.SetHeaderRow(ws3, aHeadRow, new[]
        {
            "ÍTEM", "DESCRIPCIÓN DE LA PARTIDA ADICIONAL", "UNIDAD",
            "CANTIDAD", uf ? "P.U. UF" : "P.U. $", uf ? "TOTAL ADICIONAL UF" : "TOTAL ADICIONAL $", "TOTAL ADICIONAL CLP",
            "JUSTIFICACIÓN TÉCNICA"
        });

        int aRow = aHeadRow + 1;
        var adicionalesRows = summary.Rows.Where(r => r.DeltaUf > 0).ToList();
        foreach (var r in adicionalesRows)
        {
            ws3.Cell(aRow, 1).SetValue(r.Item);
            ws3.Cell(aRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws3.Cell(aRow, 2).SetValue(r.Descripcion);
            ws3.Cell(aRow, 3).SetValue(r.Unidad);
            ws3.Cell(aRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            decimal qAdic = r.DeltaCantidad > 0 ? r.DeltaCantidad : (r.CantidadActual > 0 ? r.CantidadActual : 1m);
            decimal puUf = r.PuUfActual > 0 ? r.PuUfActual : (r.DeltaUf / qAdic);

            ws3.Cell(aRow, 4).SetValue(ClosedXmlExportStyler.ToDouble(qAdic));
            ws3.Cell(aRow, 4).Style.NumberFormat.Format = "#,##0.##";
            ws3.Cell(aRow, 5).SetValue(ClosedXmlExportStyler.ToDouble(uf ? puUf : puUf * valorUf));
            ws3.Cell(aRow, 5).Style.NumberFormat.Format = fmtMonto;

            // Fórmulas
            ws3.Cell(aRow, 6).FormulaA1 = $"D{aRow}*E{aRow}";
            ws3.Cell(aRow, 6).Style.NumberFormat.Format = fmtMonto;
            ws3.Cell(aRow, 6).Style.Font.Bold = true;

            ws3.Cell(aRow, 7).FormulaA1 = $"F{aRow}*'01_RESUMEN_EJECUTIVO'!$B$2";
            ws3.Cell(aRow, 7).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
            ws3.Cell(aRow, 7).Style.Font.Bold = true;

            ws3.Cell(aRow, 8).SetValue(r.DetalleCambio);
            aRow++;
        }

        // Fila Total Adicionales
        int aTotRow = aRow;
        ws3.Cell(aTotRow, 1).Value = "TOTAL ADICIONALES APROBACIÓN:";
        ws3.Range(aTotRow, 1, aTotRow, 5).Merge().Style.Font.Bold = true;
        ws3.Range(aTotRow, 1, aTotRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws3.Cell(aTotRow, 6).FormulaA1 = $"SUM(F5:F{aTotRow - 1})";
        ws3.Cell(aTotRow, 6).Style.NumberFormat.Format = fmtMonto;
        ws3.Cell(aTotRow, 6).Style.Font.Bold = true;

        ws3.Cell(aTotRow, 7).FormulaA1 = $"SUM(G5:G{aTotRow - 1})";
        ws3.Cell(aTotRow, 7).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws3.Cell(aTotRow, 7).Style.Font.Bold = true;

        ws3.Range(aTotRow, 1, aTotRow, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#ECFDF5");
        if (!uf)
            ws3.Column(7).Hide();

        // Bloque de Firmas en Hoja 3
        int signRow = aTotRow + 4;
        ws3.Cell(signRow, 2).Value = "____________________________________";
        ws3.Cell(signRow, 5).Value = "____________________________________";
        ws3.Cell(signRow, 7).Value = "____________________________________";

        ws3.Cell(signRow + 1, 2).Value = summary.ContractorName;
        ws3.Cell(signRow + 1, 2).Style.Font.Bold = true;
        ws3.Cell(signRow + 2, 2).Value = "Contratista Eléctrico / Reg. SEC";

        ws3.Cell(signRow + 1, 5).Value = "Inspección Técnica de Obra (ITO)";
        ws3.Cell(signRow + 1, 5).Style.Font.Bold = true;
        ws3.Cell(signRow + 2, 5).Value = "Conforme Replanteo en Terreno";

        ws3.Cell(signRow + 1, 7).Value = summary.ClientName;
        ws3.Cell(signRow + 1, 7).Style.Font.Bold = true;
        ws3.Cell(signRow + 2, 7).Value = "Administración de Obra / Constructora";

        ws3.Columns().AdjustToContents();

        wb.SaveAs(outputPath);
    }
}
