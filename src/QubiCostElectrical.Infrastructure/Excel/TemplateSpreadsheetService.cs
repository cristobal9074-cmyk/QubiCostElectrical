using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

public sealed class TemplateSpreadsheetService : ITemplateSpreadsheetService
{
    public void Export(string path, IReadOnlyList<TemplateRow> templates, string sourceProject, string sourceClient)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportación requerida.", nameof(path));
        if (templates == null)
            throw new ArgumentNullException(nameof(templates));

        SpreadsheetOperationGuards.Execute(ProjectOperationKind.ExcelExport, () =>
        {
            using var wb = new XLWorkbook();

            var wsTpl = wb.Worksheets.Add("Plantillas");
            string[] tplHeaders = { "Nombre", "Descripcion", "UnidadBase", "Area", "Keywords", "Componentes" };
            for (int c = 0; c < tplHeaders.Length; c++)
                wsTpl.Cell(1, c + 1).Value = tplHeaders[c];

            int r = 2;
            foreach (var tpl in templates.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
            {
                wsTpl.Cell(r, 1).Value = tpl.Nombre;
                wsTpl.Cell(r, 2).Value = tpl.Descripcion;
                wsTpl.Cell(r, 3).Value = tpl.UnidadBase;
                wsTpl.Cell(r, 4).Value = tpl.Area;
                wsTpl.Cell(r, 5).Value = tpl.Keywords;
                wsTpl.Cell(r, 6).Value = tpl.Items.Count;
                r++;
            }
            FormatTemplateWorksheet(wsTpl, tplHeaders.Length);

            var wsItems = wb.Worksheets.Add("Componentes");
            string[] itemHeaders = { "Plantilla", "Clase", "Codigo", "SubPlantilla", "Tipo", "Factor", "Minimo" };
            for (int c = 0; c < itemHeaders.Length; c++)
                wsItems.Cell(1, c + 1).Value = itemHeaders[c];

            r = 2;
            foreach (var tpl in templates.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var item in tpl.Items)
                {
                    wsItems.Cell(r, 1).Value = tpl.Nombre;
                    wsItems.Cell(r, 2).Value = string.IsNullOrWhiteSpace(item.Clase) ? "MATERIAL" : item.Clase;
                    wsItems.Cell(r, 3).Value = item.Codigo;
                    wsItems.Cell(r, 4).Value = item.SubPlantilla;
                    wsItems.Cell(r, 5).Value = item.Tipo;
                    wsItems.Cell(r, 6).SetValue(ClosedXmlExportStyler.ToDouble(item.Factor));
                    wsItems.Cell(r, 7).SetValue(ClosedXmlExportStyler.ToDouble(item.Minimo));
                    r++;
                }
            }
            FormatTemplateWorksheet(wsItems, itemHeaders.Length);
            wsItems.Column(6).Style.NumberFormat.Format = "#,##0.000";
            wsItems.Column(7).Style.NumberFormat.Format = "#,##0.00";

            var wsInfo = wb.Worksheets.Add("Info");
            wsInfo.Cell(1, 1).Value = "Formato";
            wsInfo.Cell(1, 2).Value = "QubiCostElectrical.TemplateLibrary";
            wsInfo.Cell(2, 1).Value = "Version";
            wsInfo.Cell(2, 2).Value = 1;
            wsInfo.Cell(3, 1).Value = "Proyecto origen";
            wsInfo.Cell(3, 2).Value = sourceProject;
            wsInfo.Cell(4, 1).Value = "Cliente origen";
            wsInfo.Cell(4, 2).Value = sourceClient;
            wsInfo.Cell(5, 1).Value = "Exportado";
            wsInfo.Cell(5, 2).Value = AppClock.Now;
            FormatTemplateWorksheet(wsInfo, 2);

            wb.SaveAs(path);
        });
    }

    public List<TemplateRow> Import(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de archivo requerida.", nameof(path));

        return SpreadsheetOperationGuards.Execute(
            ProjectOperationKind.ExcelImport,
            () => ImportCore(path),
            "No se pudo importar plantillas desde Excel.");
    }

    private static List<TemplateRow> ImportCore(string path)
    {
        using var wb = new XLWorkbook(path);
        var wsTpl = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), "Plantillas", StringComparison.OrdinalIgnoreCase))
                    ?? wb.Worksheets.FirstOrDefault();
        var wsItems = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), "Componentes", StringComparison.OrdinalIgnoreCase))
                      ?? wb.Worksheets.FirstOrDefault(w => HeaderRowContains(w, "Plantilla") && HeaderRowContains(w, "Codigo"));

        if (wsTpl == null)
            throw new InvalidDataException("El Excel no contiene hoja de plantillas.");

        var templates = new List<TemplateRow>();
        var map = new Dictionary<string, TemplateRow>(StringComparer.OrdinalIgnoreCase);
        var tplHeaders = ReadHeader(wsTpl);

        int cNombre = ItemizadoImportParser.FindColumn(tplHeaders, "NOMBRE", "PLANTILLA", "TEMPLATE");
        int cDesc = ItemizadoImportParser.FindColumn(tplHeaders, "DESCRIPCION", "DESCRIPCIÓN", "DETALLE");
        int cUnidad = ItemizadoImportParser.FindColumn(tplHeaders, "UNIDADBASE", "UNIDAD", "UD", "UNI");
        int cArea = ItemizadoImportParser.FindColumn(tplHeaders, "AREA", "ÁREA", "FAMILIA");
        int cKeywords = ItemizadoImportParser.FindColumn(tplHeaders, "KEYWORDS", "PALABRASCLAVE", "BUSQUEDA", "BÚSQUEDA");

        if (cNombre < 0)
            throw new InvalidDataException("La hoja Plantillas debe tener una columna Nombre.");

        foreach (var row in wsTpl.RowsUsed().Skip(1))
        {
            string nombre = GetCell(row, cNombre).Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            if (ItemizadoImportParser.HeaderKey(nombre) == "NOMBRE") continue;

            var tpl = new TemplateRow
            {
                Nombre = nombre,
                Descripcion = GetCell(row, cDesc).Trim(),
                UnidadBase = DefaultIfBlank(GetCell(row, cUnidad), "ML"),
                Area = DefaultIfBlank(GetCell(row, cArea), "General"),
                Keywords = GetCell(row, cKeywords).Trim(),
                Items = new List<TemplateItemRow>()
            };

            templates.Add(tpl);
            map[tpl.Nombre] = tpl;
        }

        if (wsItems != null)
        {
            var itemHeaders = ReadHeader(wsItems);
            int cPlantilla = ItemizadoImportParser.FindColumn(itemHeaders, "PLANTILLA", "NOMBREPLANTILLA", "TEMPLATE");
            int cClase = ItemizadoImportParser.FindColumn(itemHeaders, "CLASE", "TIPOREFERENCIA");
            int cCodigo = ItemizadoImportParser.FindColumn(itemHeaders, "CODIGO", "CÓDIGO", "MATERIAL", "CODE");
            int cSub = ItemizadoImportParser.FindColumn(itemHeaders, "SUBPLANTILLA", "SUBPLANTILLA", "PLANTILLAHIJA", "HIJA");
            int cTipo = ItemizadoImportParser.FindColumn(itemHeaders, "TIPO", "RECURSO");
            int cFactor = ItemizadoImportParser.FindColumn(itemHeaders, "FACTOR", "RENDIMIENTO", "CANTIDAD");
            int cMinimo = ItemizadoImportParser.FindColumn(itemHeaders, "MINIMO", "MÍNIMO", "MIN");

            if (cPlantilla >= 0)
            {
                foreach (var row in wsItems.RowsUsed().Skip(1))
                {
                    string parentName = GetCell(row, cPlantilla).Trim();
                    if (string.IsNullOrWhiteSpace(parentName) || ItemizadoImportParser.HeaderKey(parentName) == "PLANTILLA") continue;

                    if (!map.TryGetValue(parentName, out var tpl))
                    {
                        tpl = new TemplateRow { Nombre = parentName, Descripcion = "Importada desde Excel", UnidadBase = "ML", Area = "General", Items = new List<TemplateItemRow>() };
                        templates.Add(tpl);
                        map[tpl.Nombre] = tpl;
                    }

                    string clase = DefaultIfBlank(GetCell(row, cClase), "MATERIAL");
                    string sub = GetCell(row, cSub).Trim();
                    string codigo = GetCell(row, cCodigo).Trim();
                    string tipo = DefaultIfBlank(GetCell(row, cTipo), "MAT");

                    if (string.Equals(clase.Trim(), "PLANTILLA", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(sub))
                    {
                        clase = "PLANTILLA";
                        if (string.IsNullOrWhiteSpace(sub)) sub = codigo;
                        if (string.IsNullOrWhiteSpace(codigo)) codigo = sub;
                        tipo = "TPL";
                    }

                    if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(sub)) continue;

                    decimal factor = ParseDecimal(GetCell(row, cFactor));
                    decimal minimo = ParseDecimal(GetCell(row, cMinimo));

                    tpl.Items.Add(new TemplateItemRow
                    {
                        Clase = clase.Trim(),
                        Codigo = codigo,
                        SubPlantilla = sub,
                        Tipo = tipo.Trim(),
                        Factor = factor <= 0m ? 1m : factor,
                        Minimo = minimo < 0m ? 0m : minimo
                    });
                }
            }
        }

        return templates;
    }

    private static void FormatTemplateWorksheet(IXLWorksheet ws, int headerColumns)
    {
        var header = ws.Range(1, 1, 1, headerColumns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F2742");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        foreach (var col in ws.ColumnsUsed())
            if (col.Width < 12) col.Width = 12;
    }

    private static bool HeaderRowContains(IXLWorksheet ws, string header)
    {
        string wanted = ItemizadoImportParser.HeaderKey(header);
        var firstRow = ws.FirstRowUsed();
        if (firstRow == null) return false;
        return firstRow.CellsUsed().Any(c => ItemizadoImportParser.HeaderKey(c.GetFormattedString()).Contains(wanted));
    }

    private static List<string> ReadHeader(IXLWorksheet ws)
    {
        var firstRow = ws.FirstRowUsed();
        if (firstRow == null) return new List<string>();

        int lastColumn = firstRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var headers = new List<string>();
        for (int c = 1; c <= lastColumn; c++)
            headers.Add(firstRow.Cell(c).GetFormattedString());

        return headers;
    }

    private static string GetCell(IXLRow row, int zeroBasedColumn)
    {
        if (row == null || zeroBasedColumn < 0) return "";
        return row.Cell(zeroBasedColumn + 1).GetFormattedString() ?? "";
    }

    private static string DefaultIfBlank(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result)) return result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("es-CL"), out result)) return result;
        return 0m;
    }
}
