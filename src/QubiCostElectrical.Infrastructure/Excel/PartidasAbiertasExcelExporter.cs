using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

/// <summary>
/// Exportador del Presupuesto Detallado con Partidas Abiertas en formato Excel,
/// replicando la estructura tradicional de la hoja DETALLE (partidas con desglose de insumos,
/// costos directos de material y mano de obra, precios de venta y fórmulas vivas).
/// </summary>
public sealed class PartidasAbiertasExcelExporter : IPartidasAbiertasExcelExporter
{
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly ICurrencyConversionService currencyConversion;

    public PartidasAbiertasExcelExporter()
        : this(new BudgetCalculationService(), new CurrencyConversionService())
    {
    }

    public PartidasAbiertasExcelExporter(
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

        BuildPartidasAbiertasSheet(wb, context);

        wb.SaveAs(path);
    }

    private void BuildPartidasAbiertasSheet(XLWorkbook wb, ExportProjectContext ctx)
    {
        var ws = wb.Worksheets.Add("-=DETALLE PARTIDAS=-");
        ws.ShowGridLines = true;
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Anchos de columna
        ws.Column(1).Width = 3;   // A: Margen
        ws.Column(2).Width = 10;  // B: ÍTEM
        ws.Column(3).Width = 18;  // C: CÓDIGO
        ws.Column(4).Width = 46;  // D: DESCRIPCIÓN
        ws.Column(5).Width = 9;   // E: UNIDAD
        ws.Column(6).Width = 13;  // F: CANTIDAD
        ws.Column(7).Width = 14;  // G: P.U. MAT
        ws.Column(8).Width = 14;  // H: P.U. M.O.
        ws.Column(9).Width = 15;  // I: P.U. DIRECTO
        ws.Column(10).Width = 16; // J: P.U. VENTA
        ws.Column(11).Width = 18; // K: TOTAL VENTA
        ws.Column(12).Width = 24; // L: OBSERVACIONES / ZONAS

        var offer = ctx.OfferSettings;
        var theme = ctx.CorporateTheme;
        var project = ctx.Project;

        bool hasTheme = theme?.Enabled == true;
        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName) ? offer.CompanyName
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.CompanyName) ? theme.CompanyName : "INGELSAM SPA");
        string companyRut = !string.IsNullOrWhiteSpace(offer.CompanyRut) ? offer.CompanyRut.Trim() : "";
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
        var tableHeaderBg = navyColor;
        var graySectionBg = XLColor.FromArgb(237, 242, 247);
        var partidaHeaderBg = XLColor.FromArgb(234, 241, 251);
        var borderColor = XLColor.FromArgb(203, 213, 225);

        // --- FILA 1: Espaciador superior ---
        ws.Row(1).Height = 16.2;

        // --- FILA 2: Banner del Logo (B2:L2) ---
        ws.Row(2).Height = 79.2;
        var logoRange = ws.Range("B2:L2");
        logoRange.Merge();
        logoRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        logoRange.Style.Border.TopBorderColor = navyColor;
        logoRange.Style.Border.LeftBorderColor = navyColor;
        logoRange.Style.Border.RightBorderColor = navyColor;

        InsertLogo(ws, "B2", logoPath, companyName, navyColor);

        // --- FILAS 3 a 6: Emisor (B3:E6) y DATOS CLIENTES (F3:L6) ---
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
        ws.Range("F3:L3").Merge();
        ws.Cell("F3").Value = "DATOS CLIENTES";
        ws.Cell("F3").Style.Font.Bold = true;
        ws.Cell("F3").Style.Font.FontSize = 8.5;
        ws.Cell("F3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell("F3").Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);

        ws.Cell("F4").Value = "Mandante";
        ws.Cell("F4").Style.Font.Bold = true;
        ws.Cell("F4").Style.Font.FontSize = 8;
        ws.Range("G4:L4").Merge();
        ws.Cell("G4").Value = !string.IsNullOrWhiteSpace(offer.RecipientCompany) ? offer.RecipientCompany : (!string.IsNullOrWhiteSpace(ctx.ClientName) ? ctx.ClientName : "Cliente");
        ws.Cell("G4").Style.Font.FontSize = 10.5;

        ws.Cell("F5").Value = "Nombre";
        ws.Cell("F5").Style.Font.Bold = true;
        ws.Cell("F5").Style.Font.FontSize = 8;
        ws.Range("G5:L5").Merge();
        ws.Cell("G5").Value = !string.IsNullOrWhiteSpace(offer.RecipientName) ? offer.RecipientName : (!string.IsNullOrWhiteSpace(ctx.ClientName) ? ctx.ClientName : "Cliente");
        ws.Cell("G5").Style.Font.FontSize = 10.5;

        ws.Cell("F6").Value = "Carta de Oferta";
        ws.Cell("F6").Style.Font.Bold = true;
        ws.Cell("F6").Style.Font.FontSize = 8;
        ws.Range("G6:L6").Merge();
        ws.Cell("G6").Value = folio;
        ws.Cell("G6").Style.Font.Bold = true;
        ws.Cell("G6").Style.Font.FontSize = 10.5;

        var clientBox = ws.Range("F3:L6");
        clientBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        clientBox.Style.Border.OutsideBorderColor = navyColor;
        clientBox.Style.Border.InsideBorder = XLBorderStyleValues.Dashed;
        clientBox.Style.Border.InsideBorderColor = borderColor;

        // Título del documento
        int headerRow = 9;
        ws.Row(headerRow).Height = 24;
        ws.Cell(headerRow, 2).Value = "ÍTEM";
        ws.Cell(headerRow, 3).Value = "CÓDIGO";
        ws.Cell(headerRow, 4).Value = "DESCRIPCIÓN";
        ws.Cell(headerRow, 5).Value = "UNIDAD";
        ws.Cell(headerRow, 6).Value = "CANTIDAD";
        ws.Cell(headerRow, 7).Value = "P.U. MAT ($)";
        ws.Cell(headerRow, 8).Value = "P.U. M.O. ($)";
        ws.Cell(headerRow, 9).Value = "COSTO DIR. ($)";
        ws.Cell(headerRow, 10).Value = muestraUf ? "P.U. VENTA (UF)" : "P.U. VENTA ($)";
        ws.Cell(headerRow, 11).Value = muestraUf ? "TOTAL VENTA (UF)" : "TOTAL VENTA ($)";
        ws.Cell(headerRow, 12).Value = "OBSERVACIONES";

        var headerRange = ws.Range(headerRow, 2, headerRow, 12);
        headerRange.Style.Fill.BackgroundColor = tableHeaderBg;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontSize = 9.5;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Cálculo de costos y venta
        var cost = ctx.BudgetSettings != null
            ? budgetCalculation.CalculateBudget(ctx.Cubicacion, ctx.BudgetSettings, ctx.Apus, ctx.Itemizado, ctx.OrdenesCambio)
            : new CostBreakdown();

        bool ocultarGgUt = offer.OcultarGgYUtilidadEnResumen;
        decimal targetAmount = ocultarGgUt
            ? (ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf : cost.TotalVentaNetoUf)
            : (cost.CostoDirectoUf > 0m ? cost.CostoDirectoUf : cost.TotalVentaNetoUf);

        var offerRows = OfferClienteRowBuilder.Build(ctx.Itemizado, cost, targetAmount, ctx.ProjectTitle);
        var offerRowByItem = offerRows.Where(r => !r.EsTitulo).ToDictionary(r => r.Item, r => r, StringComparer.OrdinalIgnoreCase);

        var cubByItemId = ctx.Cubicacion
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId) && (!string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m))
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        int row = headerRow + 1;
        var partidaTotalCells = new List<string>();

        foreach (var item in ctx.Itemizado)
        {
            ws.Row(row).Height = 20;

            if (item.EsTitulo)
            {
                // Fila de Capítulo / Título
                ws.Cell(row, 2).Value = item.Item;
                ws.Cell(row, 2).Style.Font.Bold = true;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range(row, 3, row, 4).Merge();
                ws.Cell(row, 3).Value = item.Descripcion;
                ws.Cell(row, 3).Style.Font.Bold = true;

                ws.Cell(row, 5).Value = "";
                ws.Cell(row, 6).Value = "";
                ws.Cell(row, 7).Value = "";
                ws.Cell(row, 8).Value = "";
                ws.Cell(row, 9).Value = "";
                ws.Cell(row, 10).Value = "";
                ws.Cell(row, 11).Value = "";
                ws.Cell(row, 12).Value = "Capítulo";
                ws.Cell(row, 12).Style.Font.Italic = true;

                var chRange = ws.Range(row, 2, row, 12);
                chRange.Style.Fill.BackgroundColor = graySectionBg;
                chRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                chRange.Style.Border.OutsideBorderColor = borderColor;
                row++;
                continue;
            }

            // Fila de Partida
            int partidaHeaderRow = row;
            partidaTotalCells.Add($"K{partidaHeaderRow}");

            offerRowByItem.TryGetValue(item.Item, out var offerItem);
            decimal itemVentaTotalUf = offerItem?.TotalUf ?? 0m;
            decimal itemVentaTotalMoneda = muestraUf ? itemVentaTotalUf : itemVentaTotalUf * valorUf;
            decimal itemPuVentaMoneda = item.Cantidad > 0m ? itemVentaTotalMoneda / item.Cantidad : 0m;

            ws.Cell(row, 2).Value = item.Item;
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 3).Value = ""; // Partida no tiene código directo

            ws.Cell(row, 4).Value = item.Descripcion;
            ws.Cell(row, 4).Style.Font.Bold = true;

            ws.Cell(row, 5).Value = item.Unidad;
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 6).SetValue(decimal.ToDouble(item.Cantidad));
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = item.Cantidad % 1 == 0 ? "#,##0" : "#,##0.##";
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 7).Value = "";
            ws.Cell(row, 8).Value = "";
            ws.Cell(row, 9).Value = "";

            ws.Cell(row, 10).SetValue(decimal.ToDouble(itemPuVentaMoneda));
            ws.Cell(row, 10).Style.Font.Bold = true;
            ws.Cell(row, 10).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 11).Style.Font.Bold = true;
            ws.Cell(row, 11).Style.NumberFormat.Format = currentNumberFormat;
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 12).Value = !string.IsNullOrWhiteSpace(item.Observaciones) ? item.Observaciones : "";

            var partRange = ws.Range(row, 2, row, 12);
            partRange.Style.Fill.BackgroundColor = partidaHeaderBg;
            partRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            partRange.Style.Border.OutsideBorderColor = borderColor;

            cubByItemId.TryGetValue(item.Id, out var cubRows);
            cubRows ??= new();

            int itemsStartRow = row + 1;
            row++;

            // Calcular desglose de venta por recurso
            decimal partidaCostoDirectoClp = cubRows.Sum(c => c.Cantidad * c.PrecioUnitClpConFallback);

            foreach (var line in cubRows.OrderBy(x => x.Area).ThenBy(x => x.Codigo))
            {
                ws.Row(row).Height = 18;

                ws.Cell(row, 2).Value = "";
                ws.Cell(row, 3).Value = line.Codigo;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(row, 4).Value = line.Descripcion;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(row, 5).Value = line.Unidad;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cantidad
                ws.Cell(row, 6).SetValue(decimal.ToDouble(line.Cantidad));
                ws.Cell(row, 6).Style.NumberFormat.Format = line.Cantidad % 1 == 0 ? "#,##0" : "#,##0.##";
                ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Precios de costo directo en CLP
                decimal matClp = line.MatClpConFallback;
                decimal moClp = line.MoClpConFallback;

                ws.Cell(row, 7).SetValue(decimal.ToDouble(matClp));
                ws.Cell(row, 7).Style.NumberFormat.Format = clpNumberFormat;
                ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 8).SetValue(decimal.ToDouble(moClp));
                ws.Cell(row, 8).Style.NumberFormat.Format = clpNumberFormat;
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Fórmula Costo Directo Unitario = P.U. Mat + P.U. MO
                ws.Cell(row, 9).FormulaA1 = $"=G{row}+H{row}";
                ws.Cell(row, 9).Style.NumberFormat.Format = clpNumberFormat;
                ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Precio de Venta Unitario
                decimal lineCostClp = line.Cantidad * line.PrecioUnitClpConFallback;
                decimal lineVentaMoneda = partidaCostoDirectoClp > 0m
                    ? itemVentaTotalMoneda * (lineCostClp / partidaCostoDirectoClp)
                    : (muestraUf ? line.UfTotal : line.SubtotalClp);
                decimal linePuVentaMoneda = line.Cantidad > 0m ? lineVentaMoneda / line.Cantidad : 0m;

                ws.Cell(row, 10).SetValue(decimal.ToDouble(linePuVentaMoneda));
                ws.Cell(row, 10).Style.NumberFormat.Format = currentNumberFormat;
                ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Fórmula Total Venta = Cantidad * P.U. Venta
                ws.Cell(row, 11).FormulaA1 = $"=F{row}*J{row}";
                ws.Cell(row, 11).Style.NumberFormat.Format = currentNumberFormat;
                ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                string obs = !string.IsNullOrWhiteSpace(line.Area) && line.Area != "General" ? $"[{line.Area}]" : "";
                ws.Cell(row, 12).Value = obs;

                var lineRange = ws.Range(row, 2, row, 12);
                lineRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.OutsideBorderColor = borderColor;
                lineRange.Style.Border.InsideBorderColor = borderColor;

                row++;
            }

            // Fórmula para el total de la partida
            if (cubRows.Count > 0)
            {
                int itemsEndRow = row - 1;
                ws.Cell(partidaHeaderRow, 11).FormulaA1 = $"=SUM(K{itemsStartRow}:K{itemsEndRow})";
            }
            else
            {
                ws.Cell(partidaHeaderRow, 11).FormulaA1 = $"=F{partidaHeaderRow}*J{partidaHeaderRow}";
            }
        }

        // --- TOTALES FINALES ---
        row += 1;
        ws.Row(row).Height = 22;

        ws.Cell(row, 9).Value = "TOTAL NETO:";
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        string sumFormula = partidaTotalCells.Count > 0 ? $"=SUM({string.Join(",", partidaTotalCells)})" : "=0";
        ws.Cell(row, 11).FormulaA1 = sumFormula;
        ws.Cell(row, 11).Style.Font.Bold = true;
        ws.Cell(row, 11).Style.NumberFormat.Format = currentNumberFormat;
        ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var netRange = ws.Range(row, 9, row, 11);
        netRange.Style.Fill.BackgroundColor = graySectionBg;
        netRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        netRange.Style.Border.OutsideBorderColor = borderColor;

        int netRow = row;
        row++;

        // Fila IVA (19%)
        ws.Row(row).Height = 20;
        ws.Cell(row, 9).Value = "IVA (19%):";
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(row, 11).FormulaA1 = $"=K{netRow}*0.19";
        ws.Cell(row, 11).Style.Font.Bold = true;
        ws.Cell(row, 11).Style.NumberFormat.Format = currentNumberFormat;
        ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var ivaRange = ws.Range(row, 9, row, 11);
        ivaRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ivaRange.Style.Border.OutsideBorderColor = borderColor;

        int ivaRow = row;
        row++;

        // Fila TOTAL GENERAL CON IVA
        ws.Row(row).Height = 22;
        ws.Cell(row, 9).Value = "TOTAL CON IVA:";
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(row, 11).FormulaA1 = $"=K{netRow}+K{ivaRow}";
        ws.Cell(row, 11).Style.Font.Bold = true;
        ws.Cell(row, 11).Style.Font.FontSize = 10.5;
        ws.Cell(row, 11).Style.NumberFormat.Format = currentNumberFormat;
        ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var totRange = ws.Range(row, 9, row, 11);
        totRange.Style.Fill.BackgroundColor = partidaHeaderBg;
        totRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        totRange.Style.Border.OutsideBorderColor = navyColor;

        // Inmovilizar encabezados de tabla
        ws.SheetView.FreezeRows(headerRow);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
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
            var asm = typeof(PartidasAbiertasExcelExporter).Assembly;
            stream = asm.GetManifestResourceStream("QubiCostElectrical.Assets.ingelsam_logo.png");
        }

        if (stream != null)
        {
            try
            {
                using (stream)
                {
                    var pic = ws.AddPicture(stream, "LogoPartidasAbiertas");
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
}

