using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Exportador de la Hoja de Resumen de Presupuesto para el Mandante en formato Excel,
/// replicando con fidelidad absoluta la estructura, membrete corporativo, dimensiones,
/// logo y fórmulas vivas de Ingelsam (Pel 024F2026 MAIL.xlsx).
/// </summary>
public sealed class IngelsamOfferSummaryExcelExporter : IIngelsamOfferSummaryExcelExporter
{
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly ICurrencyConversionService currencyConversion;

    public IngelsamOfferSummaryExcelExporter()
        : this(new BudgetCalculationService(), new CurrencyConversionService())
    {
    }

    public IngelsamOfferSummaryExcelExporter(
        IBudgetCalculationService budgetCalculation,
        ICurrencyConversionService currencyConversion)
    {
        this.budgetCalculation = budgetCalculation ?? throw new ArgumentNullException(nameof(budgetCalculation));
        this.currencyConversion = currencyConversion ?? throw new ArgumentNullException(nameof(currencyConversion));
    }

    public void Export(ExportProjectContext context, string path)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta de exportación requerida.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (File.Exists(path))
            File.Delete(path);

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Segoe UI";
        wb.Style.Font.FontSize = 9;

        BuildResumenSheet(wb, context);

        wb.SaveAs(path);
    }

    private void BuildResumenSheet(XLWorkbook wb, ExportProjectContext ctx)
    {
        var ws = wb.Worksheets.Add("-=RESUMEN INGELSAM=-");
        ws.ShowGridLines = true;
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Anchos de columna exactos de Pel 024F2026 MAIL.xlsx
        ws.Column(1).Width = 4.1;  // A: Margen
        ws.Column(2).Width = 8.7;  // B: ITEM
        ws.Column(3).Width = 16.4; // C: DESCRIPCIÓN (C:F)
        ws.Column(4).Width = 11.5; // D
        ws.Column(5).Width = 9.1;  // E
        ws.Column(6).Width = 52.7; // F
        ws.Column(7).Width = 16.5; // G: UNIDAD
        ws.Column(8).Width = 11.9; // H: CANTIDAD T.
        ws.Column(9).Width = 11.7; // I: UF UNIT / $ UNIT
        ws.Column(10).Width = 12.5;// J: TOTAL UF / TOTAL $
        ws.Column(11).Width = 63.8;// K: COMENTARIOS

        var offer = ctx.OfferSettings;
        var theme = ctx.CorporateTheme;
        var project = ctx.Project;

        bool hasTheme = theme?.Enabled == true;
        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName) ? offer.CompanyName
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.CompanyName) ? theme.CompanyName : "INGELSAM SPA");
        string companyAddress = !string.IsNullOrWhiteSpace(offer.CompanyAddress) ? offer.CompanyAddress.Trim() : "Av. Capellán Benavides 2077. Maipú, Santiago";
        string companyPhone = !string.IsNullOrWhiteSpace(offer.CompanyPhone) ? offer.CompanyPhone.Trim() : "9 5856 3557";
        string companyEmail = !string.IsNullOrWhiteSpace(offer.CompanyEmail) ? offer.CompanyEmail.Trim() : "cperez@ingelsam.cl";

        string? logoPath = !string.IsNullOrWhiteSpace(offer.LogoPath) && File.Exists(offer.LogoPath) ? offer.LogoPath
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.LogoPath) && File.Exists(theme.LogoPath) ? theme.LogoPath : null);

        var date = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        string folio = !string.IsNullOrWhiteSpace(offer.OfferNumber) ? offer.OfferNumber.Trim() : "PEL024F2026";

        decimal valorUf = project.ValorUfClp > 0m ? project.ValorUfClp : PriceConversion.DefaultValorUfClp;
        bool muestraUf = ctx.MuestraUf;
        string currencyLabel = muestraUf ? "UF" : "$";
        string ufNumberFormat = "#,##0.000";
        string clpNumberFormat = "$ #,##0";
        string currentNumberFormat = muestraUf ? ufNumberFormat : clpNumberFormat;

        var navyColor = XLColor.FromHtml("#004B87");
        var graySectionBg = XLColor.FromArgb(237, 242, 247);
        var borderColor = XLColor.FromArgb(203, 213, 225);

        // --- FILA 1: Espaciador superior ---
        ws.Row(1).Height = 16.2;

        // --- FILA 2: Banner del Logo (B2:K2) ---
        ws.Row(2).Height = 79.2;
        var logoRange = ws.Range("B2:K2");
        logoRange.Merge();
        logoRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.TopBorderColor = navyColor;
        logoRange.Style.Border.LeftBorderColor = navyColor;
        logoRange.Style.Border.RightBorderColor = navyColor;

        InsertLogo(ws, "B2", logoPath, companyName, navyColor);

        // --- FILAS 3 a 6: Emisor (B3:E6) y DATOS CLIENTES (F3:K6) ---
        ws.Row(3).Height = 16.2;
        ws.Row(4).Height = 16.2;
        ws.Row(5).Height = 16.2;
        ws.Row(6).Height = 16.2;

        // Lado izquierdo: Datos del emisor
        ws.Cell("B3").Value = companyName;
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontSize = 8.5;

        ws.Cell("B4").Value = companyAddress;
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontSize = 8;

        ws.Cell("B5").Value = string.IsNullOrWhiteSpace(companyPhone) ? "" : $"FONO: {companyPhone}";
        ws.Cell("B5").Style.Font.Bold = true;
        ws.Cell("B5").Style.Font.FontSize = 8;

        ws.Cell("B6").Value = companyEmail;
        ws.Cell("B6").Style.Font.FontSize = 9.5;
        ws.Cell("B6").Style.Font.FontColor = navyColor;

        var emisorRange = ws.Range("B3:E6");
        emisorRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        emisorRange.Style.Border.LeftBorderColor = navyColor;
        emisorRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        emisorRange.Style.Border.RightBorderColor = navyColor;

        // Lado derecho: DATOS CLIENTES
        ws.Range("F3:K3").Merge();
        ws.Cell("F3").Value = "DATOS CLIENTES";
        ws.Cell("F3").Style.Font.Bold = true;
        ws.Cell("F3").Style.Font.FontSize = 8.5;
        ws.Cell("F3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell("F3").Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);

        ws.Cell("F4").Value = "Mandante";
        ws.Cell("F4").Style.Font.Bold = true;
        ws.Cell("F4").Style.Font.FontSize = 8;
        ws.Range("G4:K4").Merge();
        ws.Cell("G4").Value = !string.IsNullOrWhiteSpace(offer.RecipientCompany) ? offer.RecipientCompany : (!string.IsNullOrWhiteSpace(ctx.ClientName) ? ctx.ClientName : "Cliente");
        ws.Cell("G4").Style.Font.FontSize = 10.5;

        ws.Cell("F5").Value = "Nombre";
        ws.Cell("F5").Style.Font.Bold = true;
        ws.Cell("F5").Style.Font.FontSize = 8;
        ws.Range("G5:K5").Merge();
        ws.Cell("G5").Value = !string.IsNullOrWhiteSpace(offer.RecipientName) ? offer.RecipientName : (!string.IsNullOrWhiteSpace(ctx.ClientName) ? ctx.ClientName : "Cliente");
        ws.Cell("G5").Style.Font.FontSize = 10.5;

        ws.Cell("F6").Value = "Carta de Oferta";
        ws.Cell("F6").Style.Font.Bold = true;
        ws.Cell("F6").Style.Font.FontSize = 8;
        ws.Range("G6:K6").Merge();
        ws.Cell("G6").Value = folio;
        ws.Cell("G6").Style.Font.Bold = true;
        ws.Cell("G6").Style.Font.FontSize = 10.5;

        var clientBox = ws.Range("F3:K6");
        clientBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        clientBox.Style.Border.OutsideBorderColor = navyColor;
        clientBox.Style.Border.InsideBorder = XLBorderStyleValues.Dashed;
        clientBox.Style.Border.InsideBorderColor = borderColor;

        // --- FILA 7: ENCABEZADOS DE TABLA ---
        int headerRow = 7;
        ws.Row(headerRow).Height = 31.95;

        ws.Cell(headerRow, 2).Value = "ITEM";
        ws.Range(headerRow, 3, headerRow, 6).Merge();
        ws.Cell(headerRow, 3).Value = "DESCRIPCIÓN";
        ws.Cell(headerRow, 7).Value = "UNIDAD";
        ws.Cell(headerRow, 8).Value = "CANTIDAD T.";
        ws.Cell(headerRow, 9).Value = $"{currencyLabel} UNIT";
        ws.Cell(headerRow, 10).Value = $"TOTAL {currencyLabel}";
        ws.Cell(headerRow, 11).Value = "COMENTARIOS";

        var headerRange = ws.Range(headerRow, 2, headerRow, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontSize = 8.5;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = navyColor;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        headerRange.Style.Border.OutsideBorderColor = navyColor;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorderColor = XLColor.FromArgb(100, 150, 200);

        // --- FILAS DEL ITEMIZADO ---
        var cost = ctx.BudgetSettings != null
            ? budgetCalculation.CalculateBudget(ctx.Cubicacion, ctx.BudgetSettings, ctx.Apus, ctx.Itemizado, ctx.OrdenesCambio)
            : new CostBreakdown();

        bool ocultarGgUt = offer.OcultarGgYUtilidadEnResumen;
        decimal targetAmount = ocultarGgUt
            ? (ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf : cost.TotalVentaNetoUf)
            : (cost.CostoDirectoUf > 0m ? cost.CostoDirectoUf : cost.TotalVentaNetoUf);

        var offerRows = OfferClienteRowBuilder.Build(ctx.Itemizado, cost, targetAmount, ctx.ProjectTitle);

        int row = headerRow + 1;
        var chapterSubtotalCells = new List<string>();
        int currentChapterStart = -1;
        int currentChapterHeaderRow = -1;

        for (int i = 0; i < offerRows.Count; i++)
        {
            var item = offerRows[i];

            if (item.EsTitulo)
            {
                if (currentChapterHeaderRow != -1 && row - 1 >= currentChapterStart)
                {
                    ws.Cell(currentChapterHeaderRow, 10).FormulaA1 = $"=SUM(J{currentChapterStart}:J{row - 1})";
                }

                currentChapterHeaderRow = row;
                chapterSubtotalCells.Add($"J{row}");
                currentChapterStart = row + 1;

                ws.Row(row).Height = 21;
                ws.Cell(row, 2).Value = item.Item;
                ws.Cell(row, 2).Style.Font.Bold = true;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range(row, 3, row, 6).Merge();
                ws.Cell(row, 3).Value = item.Descripcion;
                ws.Cell(row, 3).Style.Font.Bold = true;

                ws.Cell(row, 7).Value = "";
                ws.Cell(row, 8).Value = "";
                ws.Cell(row, 9).Value = "";

                ws.Cell(row, 10).Style.Font.Bold = true;
                ws.Cell(row, 10).Style.NumberFormat.Format = currentNumberFormat;
                ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 11).Value = "Subtotal Capítulo";
                ws.Cell(row, 11).Style.Font.Italic = true;

                var chRange = ws.Range(row, 2, row, 11);
                chRange.Style.Fill.BackgroundColor = graySectionBg;
                chRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                chRange.Style.Border.OutsideBorderColor = borderColor;
            }
            else
            {
                ws.Row(row).Height = 19;
                ws.Cell(row, 2).Value = item.Item;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range(row, 3, row, 6).Merge();
                ws.Cell(row, 3).Value = item.Descripcion;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(row, 7).Value = item.Unidad;
                ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (item.Cantidad % 1 == 0)
                {
                    ws.Cell(row, 8).SetValue(decimal.ToDouble(item.Cantidad));
                    ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                }
                else
                {
                    ws.Cell(row, 8).SetValue(decimal.ToDouble(item.Cantidad));
                    ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.##";
                }
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                decimal unitPrice = muestraUf
                    ? (item.Cantidad > 0m ? PriceConversion.ClpToUf(item.TotalUf, valorUf) / item.Cantidad : 0m)
                    : (item.Cantidad > 0m ? item.TotalUf / item.Cantidad : 0m);

                ws.Cell(row, 9).SetValue(decimal.ToDouble(unitPrice));
                ws.Cell(row, 9).Style.NumberFormat.Format = currentNumberFormat;
                ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 10).FormulaA1 = $"=H{row}*I{row}";
                ws.Cell(row, 10).Style.NumberFormat.Format = currentNumberFormat;
                ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 11).Value = item.Observaciones;
                ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                var lineRange = ws.Range(row, 2, row, 11);
                lineRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.OutsideBorderColor = borderColor;
                lineRange.Style.Border.InsideBorderColor = borderColor;
            }

            // Borde lateral continuo para toda la tabla
            ws.Cell(row, 2).Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            ws.Cell(row, 2).Style.Border.LeftBorderColor = navyColor;
            ws.Cell(row, 11).Style.Border.RightBorder = XLBorderStyleValues.Medium;
            ws.Cell(row, 11).Style.Border.RightBorderColor = navyColor;

            row++;
        }

        if (currentChapterHeaderRow != -1 && row - 1 >= currentChapterStart)
        {
            ws.Cell(currentChapterHeaderRow, 10).FormulaA1 = $"=SUM(J{currentChapterStart}:J{row - 1})";
        }

        int lastDataRow = Math.Max(row - 1, headerRow + 1);

        // --- BLOQUE DE CIERRE Y TOTALES ---
        int netoRow;
        if (ocultarGgUt)
        {
            netoRow = row;
            ws.Row(netoRow).Height = 22;
            ws.Range(netoRow, 2, netoRow, 9).Merge();
            ws.Cell(netoRow, 2).Value = $"TOTAL PRESUPUESTO NETO {currencyLabel}";
            ws.Cell(netoRow, 2).Style.Font.Bold = true;
            ws.Cell(netoRow, 2).Style.Font.FontColor = XLColor.White;
            ws.Cell(netoRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (chapterSubtotalCells.Count > 0)
                ws.Cell(netoRow, 10).FormulaA1 = "=" + string.Join("+", chapterSubtotalCells);
            else
                ws.Cell(netoRow, 10).FormulaA1 = $"=SUM(J{headerRow + 1}:J{lastDataRow})";

            ws.Cell(netoRow, 10).Style.Font.Bold = true;
            ws.Cell(netoRow, 10).Style.Font.FontColor = XLColor.White;
            ws.Cell(netoRow, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(netoRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var netoRange = ws.Range(netoRow, 2, netoRow, 11);
            netoRange.Style.Fill.BackgroundColor = navyColor;
            netoRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            netoRange.Style.Border.OutsideBorderColor = navyColor;

            row++;
        }
        else
        {
            int subtotalRow = row;
            ws.Row(subtotalRow).Height = 20;
            ws.Range(subtotalRow, 2, subtotalRow, 9).Merge();
            ws.Cell(subtotalRow, 2).Value = "Subtotal";
            ws.Cell(subtotalRow, 2).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (chapterSubtotalCells.Count > 0)
                ws.Cell(subtotalRow, 10).FormulaA1 = "=" + string.Join("+", chapterSubtotalCells);
            else
                ws.Cell(subtotalRow, 10).FormulaA1 = $"=SUM(J{headerRow + 1}:J{lastDataRow})";

            ws.Cell(subtotalRow, 10).Style.Font.Bold = true;
            ws.Cell(subtotalRow, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(subtotalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var subtotalRange = ws.Range(subtotalRow, 2, subtotalRow, 11);
            subtotalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            subtotalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            subtotalRange.Style.Border.TopBorderColor = borderColor;
            subtotalRange.Style.Border.BottomBorderColor = borderColor;
            subtotalRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            subtotalRange.Style.Border.LeftBorderColor = navyColor;
            subtotalRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            subtotalRange.Style.Border.RightBorderColor = navyColor;

            row++;

            decimal ggMonto = muestraUf
                ? (cost.GastosGeneralesUf > 0 ? PriceConversion.ClpToUf(cost.GastosGeneralesUf, valorUf) : 0m)
                : cost.GastosGeneralesUf;

            int ggRow = row;
            ws.Row(ggRow).Height = 19;
            ws.Range(ggRow, 2, ggRow, 6).Merge();
            ws.Cell(ggRow, 2).Value = "Gastos Generales";
            ws.Cell(ggRow, 2).Style.Font.Bold = true;
            ws.Cell(ggRow, 7).Value = "GL";
            ws.Cell(ggRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(ggRow, 8).SetValue(1);
            ws.Cell(ggRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(ggRow, 9).SetValue(decimal.ToDouble(ggMonto));
            ws.Cell(ggRow, 9).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(ggRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(ggRow, 10).FormulaA1 = $"=H{ggRow}*I{ggRow}";
            ws.Cell(ggRow, 10).Style.Font.Bold = true;
            ws.Cell(ggRow, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(ggRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var ggRange = ws.Range(ggRow, 2, ggRow, 11);
            ggRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ggRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ggRange.Style.Border.OutsideBorderColor = borderColor;
            ggRange.Style.Border.InsideBorderColor = borderColor;
            ggRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            ggRange.Style.Border.LeftBorderColor = navyColor;
            ggRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            ggRange.Style.Border.RightBorderColor = navyColor;

            row++;

            decimal utilidadMonto = muestraUf
                ? (cost.UtilidadUf > 0 ? PriceConversion.ClpToUf(cost.UtilidadUf, valorUf) : 0m)
                : cost.UtilidadUf;

            int utRow = row;
            ws.Row(utRow).Height = 19;
            ws.Range(utRow, 2, utRow, 6).Merge();
            ws.Cell(utRow, 2).Value = "Utilidad";
            ws.Cell(utRow, 2).Style.Font.Bold = true;
            ws.Cell(utRow, 7).Value = "GL";
            ws.Cell(utRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(utRow, 8).SetValue(1);
            ws.Cell(utRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(utRow, 9).SetValue(decimal.ToDouble(utilidadMonto));
            ws.Cell(utRow, 9).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(utRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(utRow, 10).FormulaA1 = $"=H{utRow}*I{utRow}";
            ws.Cell(utRow, 10).Style.Font.Bold = true;
            ws.Cell(utRow, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(utRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var utRange = ws.Range(utRow, 2, utRow, 11);
            utRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            utRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            utRange.Style.Border.OutsideBorderColor = borderColor;
            utRange.Style.Border.InsideBorderColor = borderColor;
            utRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            utRange.Style.Border.LeftBorderColor = navyColor;
            utRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            utRange.Style.Border.RightBorderColor = navyColor;

            row++;

            netoRow = row;
            ws.Row(netoRow).Height = 22;
            ws.Range(netoRow, 2, netoRow, 9).Merge();
            ws.Cell(netoRow, 2).Value = $"TOTAL PRESUPUESTO NETO {currencyLabel}";
            ws.Cell(netoRow, 2).Style.Font.Bold = true;
            ws.Cell(netoRow, 2).Style.Font.FontColor = XLColor.White;
            ws.Cell(netoRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(netoRow, 10).FormulaA1 = $"=J{subtotalRow}+J{ggRow}+J{utRow}";
            ws.Cell(netoRow, 10).Style.Font.Bold = true;
            ws.Cell(netoRow, 10).Style.Font.FontColor = XLColor.White;
            ws.Cell(netoRow, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(netoRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var netoRange = ws.Range(netoRow, 2, netoRow, 11);
            netoRange.Style.Fill.BackgroundColor = navyColor;
            netoRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            netoRange.Style.Border.OutsideBorderColor = navyColor;

            row++;
        }

        // IVA (19%) con condiciones comerciales al lado izquierdo
        int ivaRow = row;
        ws.Row(ivaRow).Height = 20;
        ws.Cell(ivaRow, 2).Value = "CONDICIONES COMERCIALES";
        ws.Cell(ivaRow, 2).Style.Font.Bold = true;
        ws.Cell(ivaRow, 2).Style.Font.FontSize = 8;

        ws.Range(ivaRow, 3, ivaRow, 6).Merge();
        ws.Cell(ivaRow, 3).Value = "VER CARTA OFERTA";
        ws.Cell(ivaRow, 3).Style.Font.Italic = true;

        ws.Range(ivaRow, 7, ivaRow, 9).Merge();
        ws.Cell(ivaRow, 7).Value = "IVA (19%)";
        ws.Cell(ivaRow, 7).Style.Font.Bold = true;
        ws.Cell(ivaRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(ivaRow, 10).FormulaA1 = $"=J{netoRow}*0.19";
        ws.Cell(ivaRow, 10).Style.Font.Bold = true;
        ws.Cell(ivaRow, 10).Style.NumberFormat.Format = currentNumberFormat;
        ws.Cell(ivaRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var ivaRange = ws.Range(ivaRow, 2, ivaRow, 11);
        ivaRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ivaRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ivaRange.Style.Border.OutsideBorderColor = borderColor;
        ivaRange.Style.Border.InsideBorderColor = borderColor;
        ivaRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        ivaRange.Style.Border.LeftBorderColor = navyColor;
        ivaRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        ivaRange.Style.Border.RightBorderColor = navyColor;

        row++;

        // TOTAL GENERAL (NETO + IVA)
        int totalRow = row;
        ws.Row(totalRow).Height = 24;
        ws.Range(totalRow, 2, totalRow, 9).Merge();
        ws.Cell(totalRow, 2).Value = $"TOTAL {currencyLabel}";
        ws.Cell(totalRow, 2).Style.Font.Bold = true;
        ws.Cell(totalRow, 2).Style.Font.FontSize = 11;
        ws.Cell(totalRow, 2).Style.Font.FontColor = XLColor.White;
        ws.Cell(totalRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(totalRow, 10).FormulaA1 = $"=J{netoRow}+J{ivaRow}";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Style.Font.FontSize = 11;
        ws.Cell(totalRow, 10).Style.Font.FontColor = XLColor.White;
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = currentNumberFormat;
        ws.Cell(totalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var totalRange = ws.Range(totalRow, 2, totalRow, 11);
        totalRange.Style.Fill.BackgroundColor = navyColor;
        totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        totalRange.Style.Border.OutsideBorderColor = navyColor;

        row += 3;

        // --- BLOQUE DE FIRMAS (Emisor y Mandante) ---
        int signRow = row;
        ws.Row(signRow).Height = 35;

        // Intentar insertar stamps de firma si existen
        InsertSignatureImage(ws, $"B{signRow}", "QubiCostElectrical.Assets.ingelsam_firma.png", 220, 77);
        InsertSignatureImage(ws, $"G{signRow}", "QubiCostElectrical.Assets.ingelsam_cliente_firma.png", 220, 65);

        // Línea de firma Emisor (Col B a E)
        ws.Range(signRow, 2, signRow, 5).Merge();
        ws.Range(signRow, 2, signRow, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(signRow, 2, signRow, 5).Style.Border.BottomBorderColor = XLColor.Black;

        // Línea de firma Mandante (Col G a K)
        ws.Range(signRow, 7, signRow, 11).Merge();
        ws.Range(signRow, 7, signRow, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(signRow, 7, signRow, 11).Style.Border.BottomBorderColor = XLColor.Black;

        row++;
        string signName = !string.IsNullOrWhiteSpace(offer.SignatureName) ? offer.SignatureName : "LUIS SAAVEDRA";
        string signRole = !string.IsNullOrWhiteSpace(offer.SignatureRole) ? offer.SignatureRole : "GERENTE GENERAL";

        ws.Range(row, 2, row, 5).Merge();
        ws.Cell(row, 2).Value = signName;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(row, 7, row, 11).Merge();
        ws.Cell(row, 7).Value = "ACEPTO PRESUPUESTO";
        ws.Cell(row, 7).Style.Font.Bold = true;
        ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        row++;
        ws.Range(row, 2, row, 5).Merge();
        ws.Cell(row, 2).Value = signRole;
        ws.Cell(row, 2).Style.Font.FontSize = 8.5;
        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(row, 7, row, 11).Merge();
        ws.Cell(row, 7).Value = "Nombre, Apellidos y Firma del Mandante";
        ws.Cell(row, 7).Style.Font.FontSize = 8.5;
        ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        row++;
        ws.Range(row, 2, row, 5).Merge();
        ws.Cell(row, 2).Value = companyName;
        ws.Cell(row, 2).Style.Font.FontSize = 8.5;
        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(row, 7, row, 11).Merge();
        ws.Cell(row, 7).Value = "RUT y Fecha:";
        ws.Cell(row, 7).Style.Font.FontSize = 8.5;
        ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void InsertLogo(IXLWorksheet ws, string cellRef, string? customLogoPath, string companyName, XLColor navyColor)
    {
        Stream? stream = null;
        if (!string.IsNullOrWhiteSpace(customLogoPath) && File.Exists(customLogoPath))
        {
            try { stream = File.OpenRead(customLogoPath); } catch { }
        }

        if (stream == null)
        {
            var asm = typeof(IngelsamOfferSummaryExcelExporter).Assembly;
            stream = asm.GetManifestResourceStream("QubiCostElectrical.Assets.ingelsam_logo.png");
        }

        if (stream != null)
        {
            try
            {
                using (stream)
                {
                    var pic = ws.AddPicture(stream, "LogoIngelsam");
                    pic.MoveTo(ws.Cell(cellRef), 8, 4);
                    pic.WithSize(354, 89);
                    return;
                }
            }
            catch
            {
            }
        }

        var cell = ws.Cell(cellRef);
        cell.Value = companyName;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 18;
        cell.Style.Font.FontColor = navyColor;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void InsertSignatureImage(IXLWorksheet ws, string cellRef, string resourceName, int width, int height)
    {
        try
        {
            var asm = typeof(IngelsamOfferSummaryExcelExporter).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var pic = ws.AddPicture(stream);
                pic.MoveTo(ws.Cell(cellRef), 8, 2);
                pic.WithSize(width, height);
            }
        }
        catch { }
    }
}
