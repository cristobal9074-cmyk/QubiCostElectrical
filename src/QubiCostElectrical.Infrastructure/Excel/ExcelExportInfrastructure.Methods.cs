using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

public sealed partial class ExcelExportInfrastructure : IExcelProjectExporter
{
    public ExcelExportInfrastructure()
        : this(new BudgetCalculationService(), new FinalReviewService(), new PriceTraceabilityService(), new CurrencyConversionService())
    {
    }

    public ExcelExportInfrastructure(
        IBudgetCalculationService budgetCalculation,
        IFinalReviewService finalReview,
        IPriceTraceabilityService priceTraceability,
        ICurrencyConversionService currencyConversion)
    {
        this.budgetCalculation = budgetCalculation;
        this.finalReview = finalReview;
        this.priceTraceability = priceTraceability;
        this.currencyConversion = currencyConversion;
    }


    public void Export(ExportProjectContext context, string path)
    {
        ctx = context ?? throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta de exportación vacía.", nameof(path));

        _cachedCost = null;
        _cachedReview = null;

        ClosedXmlExportStyler.ConfigureCorporateTheme(ctx.CorporateTheme);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            using var wb = new XLWorkbook();
            wb.Style.Font.FontName = "Segoe UI";
            wb.Style.Font.FontSize = 9;

            ExportControl(wb);
            ExportDatosProyecto(wb);
            ExportItemizadoCliente(wb);
            ExportCubicacion(wb);
            ExportApu(wb);
            ExportMateriales(wb);
            ExportHojaCosto(wb);
            ExportRevisionFinal(wb);
            ExportOfertaCliente(wb);
            ExportAuditoriaPrecios(wb);
            ExportFormatoCompra(wb);

            foreach (var ws in wb.Worksheets)
                ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(ws);

            // El archivo destino solo se borra justo antes de escribir el reemplazo, una vez
            // que armar el libro (las 11 hojas de arriba) ya tuvo éxito. Antes se borraba al
            // principio: si algo fallaba a mitad de camino (ClosedXML, datos corruptos), el
            // usuario se quedaba sin el archivo anterior Y sin uno nuevo.
            if (File.Exists(path)) File.Delete(path);
            wb.SaveAs(path);
        }
        catch (ProjectOperationException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new ProjectOperationException(
                ProjectOperationKind.ExcelExport,
                OperationFailureMessages.ToUserMessage(ex),
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProjectOperationException(
                ProjectOperationKind.ExcelExport,
                OperationFailureMessages.ToUserMessage(ex),
                ex);
        }
        finally
        {
            ClosedXmlExportStyler.ConfigureCorporateTheme((ExportCorporateThemeSettings?)null);
        }
    }

    private BudgetSettings Settings => ctx.BudgetSettings;
    private ProjectInfoRow Project => ctx.Project;
    private OfferPdfSettings Offer => ctx.OfferSettings;
    private string Currency => ctx.CurrencyLabel;
    private string UfFmt => ClosedXmlExportStyler.UfExcelFormat();
    private string ProjectFmt => ClosedXmlExportStyler.ProjectCurrencyExcelFormat(Currency);

    /// <summary>
    /// <c>true</c> cuando el proyecto permite columnas en UF (UF/USD/EUR). Con CLP la regla de
    /// producto prohíbe mostrar UF en cualquier documento, incluido este Excel interno.
    /// </summary>
    private bool MuestraUf => ctx.MuestraUf;

    /// <summary>
    /// Oculta una columna de la hoja cuando el proyecto está en CLP. Se oculta en lugar de
    /// eliminarla para no romper las fórmulas vivas del libro (que referencian letras de columna).
    /// </summary>
    private void HideIfClp(IXLWorksheet ws, int column)
    {
        if (!MuestraUf)
            ws.Column(column).Hide();
    }
    private static double D(decimal value) => ClosedXmlExportStyler.ToDouble(value);

    private CostBreakdown Cost()
    {
        if (_cachedCost != null)
            return _cachedCost;
        var settings = ExcelCostosBridge.EnrichFromProjectData(CloneSettings(Settings), ctx.Data);
        return _cachedCost = budgetCalculation.CalculateBudget(ctx.Cubicacion, settings, ctx.Apus, ctx.Itemizado);
    }

    private static BudgetSettings CloneSettings(BudgetSettings source)
    {
        source ??= BudgetSettings.FromProject(null);
        return new BudgetSettings
        {
            MetrosCuadrados = source.MetrosCuadrados,
            HerramientasPct = source.HerramientasPct,
            MermasPct = source.MermasPct,
            FerreteriaActiva = source.FerreteriaActiva,
            FerreteriaPct = source.FerreteriaPct,
            ApuMode = source.ApuMode,
            Markup = source.Markup,
            Currency = source.Currency,
            GastosOperacionalesClp = source.GastosOperacionalesClp,
            ManoObraDotacionClp = source.ManoObraDotacionClp,
            ManualMaterialesUf = source.ManualMaterialesUf,
            ManualManoObraUf = source.ManualManoObraUf,
            ManualEquiposUf = source.ManualEquiposUf,
            ManualSubcontratosUf = source.ManualSubcontratosUf,
            ManualHerramientasUf = source.ManualHerramientasUf,
            ManualMermasUf = source.ManualMermasUf,
        };
    }
    private FinalReviewSummary Review() => _cachedReview ??= ctx.ReviewSummary ?? finalReview.Build(ctx.Itemizado, ctx.Materiales, ctx.Cubicacion, ctx.Apus, Project, Settings);
    private decimal ToCurrency(decimal uf) => currencyConversion.ConvertUfToCurrency(uf, Currency, Project.ValorUfClp, Project.TipoCambioUsdClp);
    private string Money(decimal uf) => currencyConversion.FormatMoneyFromUf(uf, Currency, Project.ValorUfClp, Project.TipoCambioUsdClp);
    private string FormulaCurrency(string ufCell) => string.Equals(Currency, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase)
        ? $"={ufCell}*{Project.ValorUfClp.ToString(CultureInfo.InvariantCulture)}"
        : string.Equals(Currency, CurrencyConversionService.CurrencyUsd, StringComparison.OrdinalIgnoreCase)
            ? $"={ufCell}*{Project.ValorUfClp.ToString(CultureInfo.InvariantCulture)}/{Project.TipoCambioUsdClp.ToString(CultureInfo.InvariantCulture)}"
            : $"={ufCell}";

    private void Title(IXLWorksheet ws, string text, int columns) => ClosedXmlExportStyler.AddSheetTitle(ws, text, columns);
    private void Header(IXLWorksheet ws, int row, IEnumerable<string> headers, int start = 1) => ClosedXmlExportStyler.SetHeaderRow(ws, row, headers, start);
    private void StyleTable(IXLWorksheet ws, int hr, int fc, int lr, int lc, string name) => ClosedXmlExportStyler.StyleClosedXmlTable(ws, hr, fc, lr, lc, name);
    private void Prep(IXLWorksheet ws) => ClosedXmlExportStyler.PrepareClosedXmlSheet(ws);
    private void Info(IXLWorksheet ws, int row, string label, object? value, string? fmt = null) => ClosedXmlExportStyler.WriteInfoPair(ws, row, label, value, fmt);

    private void ExportControl(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("00_CONTROL");
        Prep(ws); Title(ws, "00 · CONTROL INTERNO DEL PRESUPUESTO", 8);
        ws.TabColor = XLColor.FromColor(ClosedXmlExportStyler.Orange);
        var cost = Cost();
        var review = Review();

        int r = 3;
        Info(ws, r++, "Tipo de archivo", "Excel interno de control / no enviar al cliente");
        Info(ws, r++, "Proyecto", ctx.ProjectTitle);
        Info(ws, r++, "Cliente", ctx.ClientName);
        Info(ws, r++, "Estado formal", ctx.BudgetStatus);
        Info(ws, r++, "Fecha exportación", ctx.ExportedAt);
        Info(ws, r++, "Versión / archivo", string.IsNullOrWhiteSpace(ctx.CurrentProjectPath) ? "Sin guardar" : Path.GetFileName(ctx.CurrentProjectPath));
        Info(ws, r++, "Version producto", ctx.ProductVersion);
        Info(ws, r++, "Version base datos", ctx.DatabaseSchemaVersion);
        Info(ws, r++, "Version formato proyecto", ctx.ProjectFormatVersion);
        Info(ws, r++, "Version motor calculo", ctx.CalculationEngineVersion);
        Info(ws, r++, "Exportado por", ctx.ExportedBy);
        Info(ws, r++, "Revisión final", review.ReadyToExport ? "LISTO PARA OFERTAR" : "REVISAR ANTES DE ENVIAR");
        Info(ws, r++, "Críticos", review.CriticalCount);
        Info(ws, r++, "Advertencias", review.WarningCount);
        Info(ws, r++, "Confianza", D(review.ConfidencePct / 100m), "0%");

        Header(ws, 3, new[] { "KPI", "VALOR UF", "VALOR " + Currency, "FUENTE" }, 4);
        WriteKpi(ws, 4, "Materiales", cost.MaterialesUf, "BudgetEngine");
        WriteKpi(ws, 5, "Mano de obra", cost.ManoObraUf, "BudgetEngine");
        WriteKpi(ws, 6, "Costo directo", cost.CostoDirectoUf, "BudgetEngine");
        WriteKpi(ws, 7, "Gastos generales", cost.GastosGeneralesUf, "MarkupPolicy");
        WriteKpi(ws, 8, "Utilidad", cost.UtilidadUf, "MarkupPolicy");
        WriteKpi(ws, 9, "Total venta neto", cost.TotalVentaNetoUf, "BudgetEngine");
        WriteKpi(ws, 10, "Total venta", cost.TotalVentaUf, Project.IncluirIvaEnTotal ? "Neto + IVA" : "Neto");
        WriteKpi(ws, 11, "UF/m²", cost.UfM2, "Total / m²");
        StyleTable(ws, 3, 4, 11, 7, "tbl_control_kpis");
        // Proyecto en CLP: la columna "VALOR UF" no debe existir a la vista.
        HideIfClp(ws, 5);

        int idx = 14;
        ws.Cell(idx++, 1).Value = "ÍNDICE DE HOJAS";
        Header(ws, idx++, new[] { "HOJA", "TIPO", "USO", "DESTINO" });
        string[,] sheets =
        {
            { "00_CONTROL", "Interna", "Control general, estado y checklist", "Equipo presupuestos" },
            { "01_DATOS_PROYECTO", "Interna", "Datos base, membrete, moneda y oferta", "Equipo presupuestos" },
            { "02_ITEMIZADO_CLIENTE", "Cliente", "Resumen de partidas valorizadas", "Cliente / respaldo" },
            { "03_CUBICACION_DETALLE", "Interna", "Detalle de cantidades y materiales", "Equipo presupuestos" },
            { "04_APU", "Interna", "Análisis de precios unitarios", "Equipo presupuestos" },
            { "05_LISTA_PRECIOS", "Interna", "Base de precios utilizada", "Equipo presupuestos" },
            { "06_HOJA_COSTO", "Interna", "Costo directo, GG, utilidad, IVA", "Gerencia" },
            { "08_REVISION_FINAL", "Interna", "Hallazgos antes de ofertar", "Jefe de presupuesto" },
            { "09_OFERTA_CLIENTE", "Cliente", "Resumen formal para oferta", "Cliente" },
            { "10_AUDITORIA_PRECIOS", "Interna", "Trazabilidad de precios", "Equipo presupuestos" },
            { "11_FORMATO_COMPRA", "Interna", "Consolidado para cotizar/comprar materiales", "Compras / bodega" }
        };
        for (int i = 0; i < sheets.GetLength(0); i++)
        {
            for (int c = 0; c < 4; c++) ws.Cell(idx, c + 1).Value = sheets[i, c];
            idx++;
        }
        StyleTable(ws, 15, 1, idx - 1, 4, "tbl_control_hojas");

        void WriteKpi(IXLWorksheet s, int row, string label, decimal clpValue, string source)
        {
            decimal valorUf = Project.ValorUfClp > 0m ? Project.ValorUfClp : PriceConversion.DefaultValorUfClp;
            decimal ufValue = PriceConversion.ClpToUf(clpValue, valorUf);
            s.Cell(row, 4).Value = label;
            s.Cell(row, 5).SetValue(D(ufValue));
            s.Cell(row, 6).SetValue(D(clpValue));
            s.Cell(row, 7).Value = source;
            s.Cell(row, 5).Style.NumberFormat.Format = UfFmt;
            s.Cell(row, 6).Style.NumberFormat.Format = ProjectFmt;
        }
    }

    private void ExportDatosProyecto(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("01_DATOS_PROYECTO");
        Prep(ws); Title(ws, "01 · DATOS DEL PROYECTO Y OFERTA", 6);
        int r = 3;
        Info(ws, r++, "Razón social", Offer.CompanyName);
        Info(ws, r++, "RUT", Offer.CompanyRut);
        Info(ws, r++, "Dirección", Offer.CompanyAddress);
        Info(ws, r++, "Teléfono", Offer.CompanyPhone);
        Info(ws, r++, "Correo", Offer.CompanyEmail);
        Info(ws, r++, "Web", Offer.CompanyWebsite);
        Info(ws, r++, "Logo", Offer.LogoPath);
        r++;
        Info(ws, r++, "Proyecto", ctx.ProjectTitle);
        Info(ws, r++, "Cliente", ctx.ClientName);
        Info(ws, r++, "Ubicación", ctx.Location);
        Info(ws, r++, "N° oferta", Offer.OfferNumber);
        Info(ws, r++, "Revisión", Offer.RevisionTag);
        Info(ws, r++, "Fecha oferta", Offer.OfferDate == default ? DateTime.Today : Offer.OfferDate);
        Info(ws, r++, "Plazo ejecución", string.IsNullOrWhiteSpace(Offer.ProjectDuration) ? "Por definir" : Offer.ProjectDuration);
        Info(ws, r++, "Validez oferta", $"{Offer.ValidityDays} días");
        Info(ws, r++, "Moneda", Currency);
        int filaUfRef = r;
        Info(ws, r++, "Valor UF CLP", Project.ValorUfClp, ClosedXmlExportStyler.ClpExcelFormat());
        // Proyecto en CLP: nunca se expone la UF de referencia.
        if (!MuestraUf)
            ws.Row(filaUfRef).Hide();
        Info(ws, r++, "Dólar CLP", Project.TipoCambioUsdClp, ClosedXmlExportStyler.ClpExcelFormat());
        Info(ws, r++, "Estado formal", ctx.BudgetStatus);
        Info(ws, r++, "Último cambio", LastStatusChangeText());
        Info(ws, r++, "Nota estado", LastStatusNote());
    }

    private void ExportItemizadoCliente(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("02_ITEMIZADO_CLIENTE");
        Prep(ws); Title(ws, "02 · ITEMIZADO CLIENTE", 9);

        decimal valorUf = Project.ValorUfClp > 0m ? Project.ValorUfClp : PriceConversion.DefaultValorUfClp;

        // Fila 2: Parámetros del presupuesto y anclaje dinámico de Valor UF en B2 ($B$2)
        ws.Cell(2, 1).Value = "VALOR UF REF:";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 2).SetValue(D(valorUf));
        ws.Cell(2, 2).Style.Font.Bold = true;
        ws.Cell(2, 2).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws.Cell(2, 3).Value = "CLIENTE:";
        ws.Cell(2, 3).Style.Font.Bold = true;
        ws.Cell(2, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 4).Value = ctx.ClientName;
        ws.Cell(2, 5).Value = "OBRA:";
        ws.Cell(2, 5).Style.Font.Bold = true;
        ws.Cell(2, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 6).Value = ctx.ProjectTitle;
        ws.Cell(2, 7).Value = "FECHA:";
        ws.Cell(2, 7).Style.Font.Bold = true;
        ws.Cell(2, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 8).Value = Offer.OfferDate == default ? DateTime.Today.ToString("dd-MM-yyyy") : Offer.OfferDate.ToString("dd-MM-yyyy");

        // Proyecto en CLP: el factor se mantiene (lo usan las fórmulas vivas de P.U. $),
        // pero la celda no debe anunciar UF.
        if (!MuestraUf)
            ws.Cell(2, 1).Value = "FACTOR INTERNO:";

        // Fila 3: Encabezados de tabla
        Header(ws, 3, new[] { "ÍTEM", "DESCRIPCIÓN", "UNIDAD", "CANTIDAD", "P.U. UF", "TOTAL UF", "P.U. " + Currency, "TOTAL " + Currency, "OBSERVACIÓN" });

        var offerRows = BuildOfferRows();

        // Identificar capítulos y sus rangos de filas para fórmulas de subtotales
        var chapterHeaderRows = new List<int>();
        var chapterRanges = new List<(int HeaderRow, int StartPartida, int EndPartida)>();
        int currentHeaderRow = -1;
        int currentStartPartida = -1;
        int currentEndPartida = -1;

        int scanRow = 4;
        for (int i = 0; i < offerRows.Count; i++)
        {
            if (offerRows[i].EsTitulo)
            {
                if (currentHeaderRow != -1 && currentEndPartida >= currentStartPartida)
                {
                    chapterRanges.Add((currentHeaderRow, currentStartPartida, currentEndPartida));
                }
                currentHeaderRow = scanRow;
                chapterHeaderRows.Add(scanRow);
                currentStartPartida = scanRow + 1;
                currentEndPartida = scanRow;
            }
            else
            {
                if (currentHeaderRow != -1)
                {
                    currentEndPartida = scanRow;
                }
            }
            scanRow++;
        }
        if (currentHeaderRow != -1 && currentEndPartida >= currentStartPartida)
        {
            chapterRanges.Add((currentHeaderRow, currentStartPartida, currentEndPartida));
        }

        int row = 4;
        foreach (var x in offerRows)
        {
            if (x.EsTitulo)
            {
                ws.Cell(row, 1).Value = x.Item;
                ws.Cell(row, 2).Value = x.Descripcion;
                ws.Range(row, 1, row, 9).Style.Font.Bold = true;
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(237, 242, 247));

                // Fórmula viva de subtotal del capítulo
                var rng = chapterRanges.FirstOrDefault(c => c.HeaderRow == row);
                if (rng.HeaderRow == row && rng.EndPartida >= rng.StartPartida)
                {
                    ws.Cell(row, 6).FormulaA1 = $"=SUM(F{rng.StartPartida}:F{rng.EndPartida})";
                    ws.Cell(row, 8).FormulaA1 = $"=SUM(H{rng.StartPartida}:H{rng.EndPartida})";
                }
                ws.Cell(row, 9).Value = "Subtotal Capítulo";
            }
            else
            {
                decimal rowClp = x.TotalUf; // En el flujo de oferta, TotalUf contiene el monto CLP base
                decimal rowUf = PriceConversion.ClpToUf(rowClp, valorUf);
                decimal puUf = x.Cantidad > 0m ? Math.Round(rowUf / x.Cantidad, 4) : 0m;

                ws.Cell(row, 1).Value = x.Item;
                ws.Cell(row, 2).Value = x.Descripcion;
                ws.Cell(row, 3).Value = x.Unidad;
                ws.Cell(row, 4).SetValue(D(x.Cantidad));
                ws.Cell(row, 5).SetValue(D(puUf));

                // Fórmulas matemáticas vivas
                ws.Cell(row, 6).FormulaA1 = $"=D{row}*E{row}";
                ws.Cell(row, 7).FormulaA1 = $"=E{row}*$B$2";
                ws.Cell(row, 8).FormulaA1 = $"=D{row}*G{row}";

                ws.Cell(row, 9).Value = string.IsNullOrWhiteSpace(x.Observaciones)
                    ? (rowClp > 0 ? "Valorizado" : "")
                    : x.Observaciones;
            }
            row++;
        }

        int dataEndRow = Math.Max(row - 1, 4);

        // Bloque de Totales Vivos al pie del itemizado
        int totalNetoRow = row;
        ws.Cell(totalNetoRow, 2).Value = "TOTAL NETO PRESUPUESTO";
        ws.Cell(totalNetoRow, 2).Style.Font.Bold = true;
        if (chapterRanges.Count > 0)
        {
            ws.Cell(totalNetoRow, 6).FormulaA1 = "=" + string.Join("+", chapterRanges.Select(c => $"F{c.HeaderRow}"));
            ws.Cell(totalNetoRow, 8).FormulaA1 = "=" + string.Join("+", chapterRanges.Select(c => $"H{c.HeaderRow}"));
        }
        else
        {
            ws.Cell(totalNetoRow, 6).FormulaA1 = $"=SUM(F4:F{dataEndRow})";
            ws.Cell(totalNetoRow, 8).FormulaA1 = $"=SUM(H4:H{dataEndRow})";
        }
        ws.Range(totalNetoRow, 1, totalNetoRow, 9).Style.Font.Bold = true;
        ws.Range(totalNetoRow, 1, totalNetoRow, 9).Style.Fill.BackgroundColor = XLColor.FromColor(ClosedXmlExportStyler.Navy);
        ws.Range(totalNetoRow, 1, totalNetoRow, 9).Style.Font.FontColor = XLColor.White;

        int ivaRow = totalNetoRow + 1;
        ws.Cell(ivaRow, 2).Value = "IVA (19%)";
        ws.Cell(ivaRow, 2).Style.Font.Bold = true;
        ws.Cell(ivaRow, 6).FormulaA1 = $"=F{totalNetoRow}*0.19";
        ws.Cell(ivaRow, 8).FormulaA1 = $"=H{totalNetoRow}*0.19";
        ws.Range(ivaRow, 1, ivaRow, 9).Style.Font.Bold = true;
        ws.Range(ivaRow, 1, ivaRow, 9).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(241, 245, 249));

        int totalBrutoRow = ivaRow + 1;
        ws.Cell(totalBrutoRow, 2).Value = "TOTAL GENERAL (CON IVA)";
        ws.Cell(totalBrutoRow, 2).Style.Font.Bold = true;
        ws.Cell(totalBrutoRow, 6).FormulaA1 = $"=F{totalNetoRow}+F{ivaRow}";
        ws.Cell(totalBrutoRow, 8).FormulaA1 = $"=H{totalNetoRow}+H{ivaRow}";
        ws.Range(totalBrutoRow, 1, totalBrutoRow, 9).Style.Font.Bold = true;
        ws.Range(totalBrutoRow, 1, totalBrutoRow, 9).Style.Fill.BackgroundColor = XLColor.FromColor(ClosedXmlExportStyler.Blue);
        ws.Range(totalBrutoRow, 1, totalBrutoRow, 9).Style.Font.FontColor = XLColor.White;

        StyleTable(ws, 3, 1, totalBrutoRow, 9, "tbl_itemizado_cliente");
        ws.Range(4, 4, totalBrutoRow, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(4, 5, totalBrutoRow, 6).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 7, totalBrutoRow, 8).Style.NumberFormat.Format = ProjectFmt;
        // Proyecto en CLP: se ocultan P.U. UF (E) y TOTAL UF (F); quedan las columnas en pesos.
        HideIfClp(ws, 5);
        HideIfClp(ws, 6);
    }

    private void ExportCubicacion(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("03_CUBICACION_DETALLE");
        Prep(ws); Title(ws, "03 · CUBICACIÓN DETALLADA", 22);
        Header(ws, 3, new[]
        {
            "ÍTEM", "TIPO", "CÓDIGO", "DESCRIPCIÓN", "UNIDAD", "CANTIDAD", "MAT UF", "M/O UF", "UNIT UF", "TOTAL UF",
            "ÁREA", "Q1", "Q2", "Q3", "Q TOTAL EXCEL", "FÓRMULA TOTAL", "MERMA % FILA", "HERR. % FILA", "REDONDEO MULT.",
            "REDONDEO MODO", "TOTAL UF EXCEL", "TOTAL " + Currency
        });
        int row = 4;
        foreach (var x in ctx.Cubicacion.Where(x => !string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m))
        {
            ws.Cell(row, 1).Value = x.ItemId;
            ws.Cell(row, 2).Value = x.Tipo;
            ws.Cell(row, 3).Value = x.Codigo;
            ws.Cell(row, 4).Value = x.Descripcion;
            ws.Cell(row, 5).Value = x.Unidad;
            ws.Cell(row, 6).SetValue(D(x.Cantidad));
            ws.Cell(row, 7).SetValue(D(x.MatUf));
            ws.Cell(row, 8).SetValue(D(x.MoUf));
            ws.Cell(row, 9).SetValue(D(x.UfUnit));
            ws.Cell(row, 10).SetValue(D(x.UfTotal));
            ws.Cell(row, 11).Value = x.Area;
            WriteQuantityExpressionCell(ws.Cell(row, 12), (x.QtyExpressions ?? Array.Empty<string>()).ElementAtOrDefault(0) ?? "", row);
            WriteQuantityExpressionCell(ws.Cell(row, 13), (x.QtyExpressions ?? Array.Empty<string>()).ElementAtOrDefault(1) ?? "", row);
            WriteQuantityExpressionCell(ws.Cell(row, 14), (x.QtyExpressions ?? Array.Empty<string>()).ElementAtOrDefault(2) ?? "", row);
            ws.Cell(row, 15).FormulaA1 = $"=SUM(L{row}:N{row})";
            WriteQuantityExpressionCell(ws.Cell(row, 16), x.TotalExpression, row);
            if (x.MermaPctOverride < 0m) ws.Cell(row, 17).Value = "Global"; else ws.Cell(row, 17).SetValue(D(x.MermaPctOverride / 100m));
            if (x.HerramientasPctOverride < 0m) ws.Cell(row, 18).Value = "Global"; else ws.Cell(row, 18).SetValue(D(x.HerramientasPctOverride / 100m));
            ws.Cell(row, 19).SetValue(D(x.RedondeoMultiplo));
            ws.Cell(row, 20).Value = string.IsNullOrWhiteSpace(x.RedondeoModo) ? "Ninguno" : x.RedondeoModo;
            ws.Cell(row, 21).FormulaA1 = $"=F{row}*I{row}";
            ws.Cell(row, 22).SetValue(D(ToCurrency(x.UfTotal)));
            row++;
        }
        StyleTable(ws, 3, 1, Math.Max(row - 1, 3), 22, "tbl_cubicacion_detalle");
        ws.Range(4, 7, Math.Max(row - 1, 4), 10).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 15, Math.Max(row - 1, 4), 15).Style.NumberFormat.Format = "#,##0.###";
        ws.Range(4, 17, Math.Max(row - 1, 4), 18).Style.NumberFormat.Format = "0.00%";
        ws.Range(4, 21, Math.Max(row - 1, 4), 21).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 22, Math.Max(row - 1, 4), 22).Style.NumberFormat.Format = ProjectFmt;
        // Proyecto en CLP: se ocultan MAT/M-O/UNIT/TOTAL UF y el total UF en vivo de la col. 21.
        foreach (int col in new[] { 7, 8, 9, 10, 21 })
            HideIfClp(ws, col);
    }


    private static void WriteQuantityExpressionCell(IXLCell cell, string? expression, int row)
    {
        string text = (expression ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            cell.Value = "";
            return;
        }

        string formula = ToExcelQuantityFormula(text, row);
        if (!string.IsNullOrWhiteSpace(formula))
            cell.FormulaA1 = formula;
        else
            cell.Value = text;
    }

    private static string ToExcelQuantityFormula(string expression, int row)
    {
        string text = expression.Trim().Replace(',', '.');
        if (!FormulaCharRegex.IsMatch(text))
            return "";

        string[] cols = { "L", "M", "N" };
        for (int i = 0; i < cols.Length; i++)
        {
            string cell = cols[i] + row.ToString(CultureInfo.InvariantCulture);
            text = QRegexes[i].Replace(text, cell);
            text = ZonaRegexes[i].Replace(text, cell);
        }

        if (LetterRegex.IsMatch(text) || string.IsNullOrWhiteSpace(text))
            return "";

        return "=" + text;
    }

    private void ExportApu(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("04_APU");
        Prep(ws); Title(ws, "04 · APU PROFESIONAL POR PARTIDA", 16);
        Header(ws, 3, new[] { "Ítem", "Partida", "Unidad", "Cantidad", "Tipo", "Código", "Recurso", "UD", "Consumo/Rend.", "Precio UF", "Merma %", "Imprev. %", "HH/u", "Proveedor", "Estado", "Total UF" });
        int row = 4;
        foreach (var apu in ctx.Apus)
        {
            var item = ctx.Itemizado.FirstOrDefault(i => string.Equals(i.Id, apu.ItemId, StringComparison.OrdinalIgnoreCase));
            decimal cantidad = apu.CantidadPartida > 0m ? apu.CantidadPartida : (item?.Cantidad > 0m ? item.Cantidad : 1m);
            foreach (var r in (apu.Recursos ?? new List<ApuRecursoRow>()).DefaultIfEmpty(new ApuRecursoRow { Descripcion = apu.Descripcion, Unidad = apu.Unidad, PrecioUf = apu.PrecioUnitUf, Rendimiento = 1m, Estado = apu.Estado }))
            {
                ws.Cell(row, 1).Value = item?.Item ?? apu.ItemId;
                ws.Cell(row, 2).Value = item?.Descripcion ?? apu.Descripcion;
                ws.Cell(row, 3).Value = apu.Unidad;
                ws.Cell(row, 4).SetValue(D(cantidad));
                ws.Cell(row, 5).Value = r.Tipo.ToString();
                ws.Cell(row, 6).Value = r.CodigoMaterial;
                ws.Cell(row, 7).Value = r.Descripcion;
                ws.Cell(row, 8).Value = r.Unidad;
                ws.Cell(row, 9).SetValue(D(r.Rendimiento));
                ws.Cell(row, 10).SetValue(D(r.PrecioBaseUf));
                ws.Cell(row, 11).SetValue(D(r.MermaPct));
                ws.Cell(row, 12).SetValue(D(r.ImprevistosPct));
                ws.Cell(row, 13).SetValue(D(r.HHUnitarias));
                ws.Cell(row, 14).Value = r.Proveedor;
                ws.Cell(row, 15).Value = string.IsNullOrWhiteSpace(r.Estado) ? "OK" : r.Estado;
                ws.Cell(row, 16).SetValue(D(r.CostoUnitUf * cantidad));
                row++;
            }
        }
        if (row == 4)
        {
            ws.Cell(row, 1).Value = "Sin APU";
            ws.Cell(row, 7).Value = "No existen análisis de precio unitario cargados.";
            row++;
        }
        StyleTable(ws, 3, 1, Math.Max(row - 1, 3), 16, "tbl_apu_profesional");
        ws.Range(4, 10, Math.Max(row - 1, 4), 10).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 16, Math.Max(row - 1, 4), 16).Style.NumberFormat.Format = UfFmt;
        // Proyecto en CLP: se ocultan Precio UF y Total UF del APU.
        HideIfClp(ws, 10);
        HideIfClp(ws, 16);
    }

    private void ExportMateriales(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("05_LISTA_PRECIOS");
        Prep(ws); Title(ws, "05 · LISTA DE PRECIOS", 17);
        Header(ws, 3, new[] { "CÓDIGO", "DESCRIPCIÓN", "UNIDAD", "CATEGORÍA", "SUBCAT.", "SUBCAT.2", "TIPO INST.", "ESPECIFICACIÓN", "MARCA", "EQUIVALENCIA", "MAT UF", "M/O UF", "TOTAL UF", "TOTAL " + Currency, "PROVEEDOR", "FECHA", "ANTIGÜEDAD DÍAS" });
        int row = 4;
        foreach (var m in ctx.Materiales)
        {
            ws.Cell(row, 1).Value = m.Codigo;
            ws.Cell(row, 2).Value = m.Descripcion;
            ws.Cell(row, 3).Value = m.Unidad;
            ws.Cell(row, 4).Value = m.Categoria;
            ws.Cell(row, 5).Value = m.Subcategoria;
            ws.Cell(row, 6).Value = m.Subcategoria2;
            ws.Cell(row, 7).Value = m.TipoInstalacion;
            ws.Cell(row, 8).Value = m.EspecificacionTecnica;
            ws.Cell(row, 9).Value = m.Marca;
            ws.Cell(row, 10).Value = m.EquivalenciaKey;
            ws.Cell(row, 11).SetValue(D(m.MatUf));
            ws.Cell(row, 12).SetValue(D(m.MoUf));
            ws.Cell(row, 13).SetValue(D(m.MatUf + m.MoUf));
            ws.Cell(row, 14).SetValue(D(ToCurrency(m.MatUf + m.MoUf)));
            ws.Cell(row, 15).Value = m.Proveedor;
            ws.Cell(row, 16).Value = m.FechaPrecio == default ? DateTime.Today : m.FechaPrecio;
            ws.Cell(row, 17).SetValue(m.PrecioAntiguedadDias);
            row++;
        }
        StyleTable(ws, 3, 1, Math.Max(row - 1, 3), 17, "tbl_lista_precios");
        ws.Range(4, 11, Math.Max(row - 1, 4), 13).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 14, Math.Max(row - 1, 4), 14).Style.NumberFormat.Format = ProjectFmt;
        // Proyecto en CLP: se ocultan MAT/M-O/TOTAL UF; queda TOTAL en pesos.
        foreach (int col in new[] { 11, 12, 13 })
            HideIfClp(ws, col);
    }

    private void ExportHojaCosto(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("06_HOJA_COSTO");
        Prep(ws); Title(ws, "06 · HOJA DE COSTO", 4);

        decimal valorUf = Project.ValorUfClp > 0m ? Project.ValorUfClp : PriceConversion.DefaultValorUfClp;

        // Fila 2: Parámetros del presupuesto y celda de referencia dinámica B2 ($B$2)
        ws.Cell(2, 1).Value = MuestraUf ? "VALOR UF REF:" : "FACTOR INTERNO:";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 2).SetValue(D(valorUf));
        ws.Cell(2, 2).Style.Font.Bold = true;
        ws.Cell(2, 2).Style.NumberFormat.Format = ClosedXmlExportStyler.ClpExcelFormat();
        ws.Cell(2, 3).Value = "MONEDA:";
        ws.Cell(2, 3).Style.Font.Bold = true;
        ws.Cell(2, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, 4).Value = Currency;

        Header(ws, 3, new[] { "CONCEPTO", "VALOR UF", "VALOR " + Currency, "EXPLICACIÓN / FÓRMULA" });
        var cost = Cost();
        WriteCost(ws, 4, "Materiales", cost.MaterialesUf, "Suma de materiales valorizados");
        WriteCost(ws, 5, "Mano de obra", cost.ManoObraUf, "Suma de mano de obra valorizada");
        WriteCost(ws, 6, "Equipos", cost.EquiposUf, "Equipos desde APU / costos complementarios");
        WriteCost(ws, 7, "Subcontratos", cost.SubcontratosUf, "Subcontratos desde APU");
        WriteCost(ws, 8, "Herramientas", cost.HerramientasUf, $"Herramientas {Project.HerramientasPct:N2}% cuando aplica");
        WriteCost(ws, 9, "Mermas", cost.MermasUf, $"Mermas {Project.MermasPct:N2}% cuando aplica");
        WriteCost(ws, 10, "Órdenes de cambio", cost.OrdenesCambioUf, "OC aprobadas — separadas de subcontratos");

        // Fila 11: Costo directo con fórmula viva
        WriteCost(ws, 11, "Costo directo", cost.CostoDirectoUf, "=SUM(B4:B10) · Costo base del presupuesto");
        ws.Cell(11, 2).FormulaA1 = "=SUM(B4:B10)";
        ws.Range(11, 1, 11, 4).Style.Font.Bold = true;

        WriteCost(ws, 12, $"Riesgo {Project.RiesgoPct:N2}%", cost.RiesgoUf, "Imprevistos / riesgo comercial");
        if (Project.PoliticaCostosExcel)
        {
            WriteCost(ws, 13, $"Gastos generales {Project.GastosGeneralesPct:N2}%", cost.GastosGeneralesUf, "GG sobre Mat+MO+GO");
            WriteCost(ws, 14, $"Utilidades {Project.UtilidadPct:N2}%", cost.UtilidadAditivaUf, "Utilidad aditiva");
            WriteCost(ws, 15, $"VA equipos {Project.ValorAgregadoEquiposPct:N2}%", cost.ValorAgregadoEquiposUf, "Valor agregado solo sobre equipos");
            WriteCost(ws, 16, "Costo total proyecto", cost.CostoProyectoUf, "Ejecución + GG + UT + VA");
            WriteCost(ws, 17, "Diferencia por factores", cost.DiferenciaFactoresUf, "Venta − costo proyecto");
            WriteCost(ws, 18, $"Descuento {Project.DescuentoPct:N2}%", cost.DescuentoUf, "Descuento comercial");
            WriteCost(ws, 19, "Total venta neto", cost.TotalVentaNetoUf, "Total antes de IVA");
            WriteCost(ws, 20, $"IVA {Project.IvaPct:N2}%", cost.IvaUf, Project.IncluirIvaEnTotal ? "IVA incluido" : "IVA informativo");
            WriteCost(ws, 21, "TOTAL VENTA", cost.TotalVentaUf, Project.IncluirIvaEnTotal ? "Neto + IVA" : "Neto");
            WriteCost(ws, 22, "UF/m²", cost.UfM2, "Total venta / m²");
        }
        else
        {
            WriteCost(ws, 13, $"Gastos generales {Project.GastosGeneralesPct:N2}%", cost.GastosGeneralesUf, "GG sobre costo directo según política");
            WriteCost(ws, 14, $"VA equipos {Project.ValorAgregadoEquiposPct:N2}%", cost.ValorAgregadoEquiposUf, "Valor agregado solo sobre equipos");
            WriteCost(ws, 15, "Subtotal", cost.SubtotalUf, "Costo directo + riesgo + GG + VA equipos");
            WriteCost(ws, 16, $"Utilidad {Project.UtilidadPct:N2}%", cost.UtilidadUf, "Utilidad / margen según política");
            WriteCost(ws, 17, $"Descuento {Project.DescuentoPct:N2}%", cost.DescuentoUf, "Descuento comercial");
            WriteCost(ws, 18, "Total venta neto", cost.TotalVentaNetoUf, "Total antes de IVA");
            WriteCost(ws, 19, $"IVA {Project.IvaPct:N2}%", cost.IvaUf, Project.IncluirIvaEnTotal ? "IVA incluido" : "IVA informativo");
            WriteCost(ws, 20, "TOTAL VENTA", cost.TotalVentaUf, Project.IncluirIvaEnTotal ? "Neto + IVA" : "Neto");
            WriteCost(ws, 21, "UF/m²", cost.UfM2, "Total venta / m²");
        }
        int lastRow = Project.PoliticaCostosExcel ? 22 : 21;
        StyleTable(ws, 3, 1, lastRow, 4, "tbl_hoja_costo");
        ws.Range(4, 2, lastRow, 2).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 3, lastRow, 3).Style.NumberFormat.Format = ProjectFmt;
        // Proyecto en CLP: se oculta la columna de valores en UF; la columna 3 queda en pesos.
        HideIfClp(ws, 2);

        void WriteCost(IXLWorksheet s, int row, string label, decimal clpValue, string note)
        {
            decimal ufValue = PriceConversion.ClpToUf(clpValue, valorUf);
            s.Cell(row, 1).Value = label;
            s.Cell(row, 2).SetValue(D(ufValue));
            s.Cell(row, 3).FormulaA1 = $"=B{row}*$B$2";
            s.Cell(row, 4).Value = note;
        }
    }



    private void ExportRevisionFinal(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("08_REVISION_FINAL");
        Prep(ws); Title(ws, "08 · REVISIÓN FINAL DEL PRESUPUESTO", 9);
        var summary = Review();
        Info(ws, 3, "Estado", summary.ReadyToExport ? "LISTO" : "BLOQUEADO");
        Info(ws, 4, "Bloqueantes", summary.BlockingCount);
        Info(ws, 5, "Críticos", summary.CriticalCount);
        Info(ws, 6, "Advertencias", summary.WarningCount);
        Info(ws, 7, "Confianza", summary.ConfidencePct / 100m, "0%");
        Header(ws, 9, new[] { "Nivel", "Checklist", "Ítem", "Partida", "Código", "Descripción", "Cantidad", "Problema", "Recomendación" });
        int row = 10;
        foreach (var issue in summary.Issues)
        {
            ws.Cell(row, 1).Value = issue.SeverityText;
            ws.Cell(row, 2).Value = issue.CategoryText;
            ws.Cell(row, 3).Value = issue.Item;
            ws.Cell(row, 4).Value = issue.Partida;
            ws.Cell(row, 5).Value = issue.Codigo;
            ws.Cell(row, 6).Value = issue.Descripcion;
            ws.Cell(row, 7).Value = issue.Count;
            ws.Cell(row, 8).Value = issue.Problem;
            ws.Cell(row, 9).Value = issue.Recommendation;
            row++;
        }
        if (summary.Issues.Count == 0)
        {
            ws.Cell(row, 1).Value = "OK";
            ws.Cell(row, 2).Value = "Sin hallazgos";
            ws.Cell(row, 8).Value = "No hay problemas críticos ni advertencias para exportar.";
            row++;
        }
        StyleTable(ws, 9, 1, Math.Max(9, row - 1), 9, "tbl_revision_final");
    }

    private void ExportOfertaCliente(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("09_OFERTA_CLIENTE");
        Prep(ws); Title(ws, "09 · OFERTA CLIENTE", 8);
        var cost = Cost();
        // "Monto neto" en la oferta siempre es el neto sin IVA; el IVA se muestra por separado.
        decimal netAmountClp = ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf : cost.TotalVentaNetoUf;
        decimal valorUf = Project.ValorUfClp > 0m ? Project.ValorUfClp : PriceConversion.DefaultValorUfClp;
        decimal netAmountUf = PriceConversion.ClpToUf(netAmountClp, valorUf);
        int r = 3;
        Info(ws, r++, "Proyecto", ctx.ProjectTitle);
        Info(ws, r++, "Cliente", ctx.ClientName);
        Info(ws, r++, "Fecha", Offer.OfferDate == default ? DateTime.Today : Offer.OfferDate);
        Info(ws, r++, "N° oferta", Offer.OfferNumber);
        Info(ws, r++, "Revisión", Offer.RevisionTag);
        if (MuestraUf)
        {
            Info(ws, r++, "Monto neto UF", netAmountUf, UfFmt);
            Info(ws, r++, "Monto neto " + Currency, netAmountClp, ProjectFmt);
        }
        else
        {
            // Proyecto en CLP: el monto neto se informa sólo en pesos.
            Info(ws, r++, "Monto neto $", netAmountClp, ProjectFmt);
        }
        Info(ws, r++, "IVA", Offer.IncludeIvaNote ? "Se debe agregar IVA cuando corresponda" : "Según condiciones comerciales");
        Info(ws, r++, "Plazo ejecución", string.IsNullOrWhiteSpace(Offer.ProjectDuration) ? "Por definir" : Offer.ProjectDuration);
        Info(ws, r++, "Validez", $"{Offer.ValidityDays} días");
        Info(ws, r++, "Estado", ctx.BudgetStatus);

        r += 2;
        ws.Cell(r++, 1).Value = "Resumen por áreas";
        Header(ws, r, new[] { "ÁREA", "TOTAL UF", "TOTAL " + Currency, "%" });
        int header = r++;
        var areas = budgetCalculation.GetAreaTotals(ctx.Cubicacion);
        if (areas.Count == 0) areas.Add(("General", netAmountClp));
        decimal totalArea = areas.Sum(a => a.Total);
        foreach (var a in areas)
        {
            decimal areaUf = PriceConversion.ClpToUf(a.Total, valorUf);
            ws.Cell(r, 1).Value = a.Area;
            ws.Cell(r, 2).SetValue(D(areaUf));
            ws.Cell(r, 3).SetValue(D(a.Total));
            ws.Cell(r, 4).SetValue(D(totalArea > 0 ? a.Total / totalArea : 1m));
            r++;
        }
        StyleTable(ws, header, 1, Math.Max(header, r - 1), 4, "tbl_oferta_areas");
        ws.Range(header + 1, 2, Math.Max(r - 1, header + 1), 2).Style.NumberFormat.Format = UfFmt;
        ws.Range(header + 1, 3, Math.Max(r - 1, header + 1), 3).Style.NumberFormat.Format = ProjectFmt;
        ws.Range(header + 1, 4, Math.Max(r - 1, header + 1), 4).Style.NumberFormat.Format = "0.00%";
        // Proyecto en CLP: se oculta TOTAL UF del resumen por áreas.
        HideIfClp(ws, 2);

        r += 2;
        ws.Cell(r++, 1).Value = "Marcas y especificaciones";
        Header(ws, r, new[] { "CATEGORÍA", "MARCA", "ESPECIFICACIÓN", "ESTADO" });
        int prodHeader = r++;
        foreach (var p in Offer.Productos.DefaultIfEmpty(new OfferProductSpecificationRow { Categoria = "Productos", Marca = "Por definir", Especificacion = "Editar en oferta PDF" }))
        {
            ws.Cell(r, 1).Value = p.Categoria;
            ws.Cell(r, 2).Value = p.Marca;
            ws.Cell(r, 3).Value = p.Especificacion;
            ws.Cell(r, 4).Value = "Editable";
            r++;
        }
        StyleTable(ws, prodHeader, 1, Math.Max(prodHeader, r - 1), 4, "tbl_oferta_productos");
    }

    private void ExportAuditoriaPrecios(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("10_AUDITORIA_PRECIOS");
        Prep(ws); Title(ws, "10 · AUDITORÍA DE PRECIOS", 13);
        Header(ws, 3, new[] { "Estado", "Código", "Descripción", "Unidad", "Precio UF", "Moneda", "UF usada CLP", "Proveedor", "Origen", "Fecha precio", "Usuario", "Usado en", "Observación" });
        int row = 4;
        foreach (var x in priceTraceability.Build(ctx.Materiales, ctx.Cubicacion, ctx.Apus, Project, ctx.ExportedBy))
        {
            ws.Cell(row, 1).Value = x.EstadoTexto;
            ws.Cell(row, 2).Value = x.Codigo;
            ws.Cell(row, 3).Value = x.Descripcion;
            ws.Cell(row, 4).Value = x.Unidad;
            ws.Cell(row, 5).SetValue(D(x.PrecioUf));
            ws.Cell(row, 6).Value = x.Moneda;
            ws.Cell(row, 7).SetValue(D(x.UfUsadaClp));
            ws.Cell(row, 8).Value = x.Proveedor;
            ws.Cell(row, 9).Value = x.Origen;
            ws.Cell(row, 10).Value = x.FechaPrecio;
            ws.Cell(row, 11).Value = x.Usuario;
            ws.Cell(row, 12).Value = x.UsadoEn;
            ws.Cell(row, 13).Value = x.Observacion;
            row++;
        }
        if (row == 4)
        {
            ws.Cell(row, 1).Value = "OK";
            ws.Cell(row, 3).Value = "No hay precios usados para auditar.";
            row++;
        }
        StyleTable(ws, 3, 1, Math.Max(3, row - 1), 13, "tbl_auditoria_precios");
        ws.Range(4, 5, Math.Max(row - 1, 4), 5).Style.NumberFormat.Format = UfFmt;
        // Proyecto en CLP: se ocultan Precio UF y la UF usada (columna de trazabilidad).
        HideIfClp(ws, 5);
        HideIfClp(ws, 7);
    }


    private void ExportFormatoCompra(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("11_FORMATO_COMPRA");
        Prep(ws); Title(ws, "11 · FORMATO DE COMPRA / COTIZACIÓN", 13);
        Header(ws, 3, new[] { "CÓDIGO", "DESCRIPCIÓN", "UNIDAD", "CATEGORÍA", "CANT. NETA", "TOL. %", "CANT. COMPRA", "PRECIO UF", "TOTAL NETO UF", "TOTAL COMPRA UF", "TOTAL " + Currency, "LÍNEAS", "OBSERVACIÓN" });
        int row = 4;
        foreach (var x in purchaseList.Build(ctx.Cubicacion, ctx.Materiales, Settings.MermasPct))
        {
            ws.Cell(row, 1).Value = x.Codigo;
            ws.Cell(row, 2).Value = x.Descripcion;
            ws.Cell(row, 3).Value = x.Unidad;
            ws.Cell(row, 4).Value = x.Categoria;
            ws.Cell(row, 5).SetValue(D(x.CantidadNeta));
            ws.Cell(row, 6).SetValue(D(x.ToleranciaPct / 100m));
            ws.Cell(row, 7).SetValue(D(x.CantidadCompra));
            ws.Cell(row, 8).SetValue(D(x.MatUfUnitario));
            ws.Cell(row, 9).SetValue(D(x.TotalNetoUf));
            ws.Cell(row, 10).SetValue(D(x.TotalCompraUf));
            ws.Cell(row, 11).SetValue(D(ToCurrency(x.TotalCompraUf)));
            ws.Cell(row, 12).SetValue(x.Lineas);
            ws.Cell(row, 13).Value = "Completar proveedor / fecha de entrega";
            row++;
        }
        if (row == 4)
        {
            ws.Cell(row, 1).Value = "Sin materiales para compra";
            ws.Cell(row, 2).Value = "Agrega cubicación con códigos de material para generar este formato.";
            row++;
        }
        StyleTable(ws, 3, 1, Math.Max(row - 1, 3), 13, "tbl_formato_compra");
        ws.Range(4, 5, Math.Max(row - 1, 4), 7).Style.NumberFormat.Format = "#,##0.###";
        ws.Range(4, 6, Math.Max(row - 1, 4), 6).Style.NumberFormat.Format = "0.00%";
        ws.Range(4, 8, Math.Max(row - 1, 4), 10).Style.NumberFormat.Format = UfFmt;
        ws.Range(4, 11, Math.Max(row - 1, 4), 11).Style.NumberFormat.Format = ProjectFmt;
        // Proyecto en CLP: se ocultan PRECIO UF, TOTAL NETO UF y TOTAL COMPRA UF.
        foreach (int col in new[] { 8, 9, 10 })
            HideIfClp(ws, col);
    }

    private List<OfferItemizadoClienteRow> BuildOfferRows()
    {
        var cost = Cost();
        return OfferClienteRowBuilder
            .Build(ctx.Itemizado, cost, ctx.OfferNetAmountUf, ctx.ProjectTitle)
            .Select(r => new OfferItemizadoClienteRow
            {
                Item = r.Item,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Cantidad = r.Cantidad,
                TotalUf = r.TotalUf,
                EsTitulo = r.EsTitulo,
                Observaciones = r.Observaciones
            })
            .ToList();
    }

    private string LastStatusChangeText()
    {
        var last = Project.HistorialEstados.OrderByDescending(x => x.Fecha).FirstOrDefault();
        return last == null ? "Sin historial" : $"{last.Fecha:dd/MM/yyyy HH:mm} · {last.EstadoNuevo}";
    }

    private string LastStatusNote()
    {
        var last = Project.HistorialEstados.OrderByDescending(x => x.Fecha).FirstOrDefault();
        return last?.Nota ?? "";
    }

    private sealed class OfferItemizadoClienteRow
    {
        public string Item { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Unidad { get; set; } = "";
        public decimal Cantidad { get; set; }
        public decimal TotalUf { get; set; }
        public bool EsTitulo { get; set; }
        public string Observaciones { get; set; } = "";
    }
}
