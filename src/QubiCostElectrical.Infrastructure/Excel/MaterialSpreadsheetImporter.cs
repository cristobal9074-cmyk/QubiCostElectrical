using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class MaterialSpreadsheetImporter : IMaterialSpreadsheetImporter
{
    private readonly IItemizadoSpreadsheetReader _reader;

    public MaterialSpreadsheetImporter() : this(new ItemizadoSpreadsheetReader())
    {
    }

    public MaterialSpreadsheetImporter(IItemizadoSpreadsheetReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public IReadOnlyList<MaterialSpreadsheetRow> ReadRows(string filePath)
    {
        return SpreadsheetOperationGuards.Execute(
            ProjectOperationKind.ExcelImport,
            () => ReadRowsCore(filePath));
    }

    private IReadOnlyList<MaterialSpreadsheetRow> ReadRowsCore(string filePath)
    {
        var sheet = _reader.ReadFirstWorksheet(filePath);
        if (sheet.Count == 0)
            return Array.Empty<MaterialSpreadsheetRow>();

        var headers = sheet[0].ToList();
        int colCodigo = ItemizadoImportParser.FindColumn(headers, "CÓDIGO", "CODIGO", "CODE");
        int colDesc = ItemizadoImportParser.FindColumn(headers, "DESCRIPCIÓN", "DESCRIPCION", "DESC", "NOMBRE", "NAME");
        int colUnidad = ItemizadoImportParser.FindColumn(headers, "UNIDAD", "UN", "UD", "U.");
        int colMat = ItemizadoImportParser.FindColumn(headers, "MAT", "MATERIAL", "PRECIO", "COSTO");
        int colMo = ItemizadoImportParser.FindColumn(headers, "M/O", "MO", "MANO", "MONTO");

        if (colCodigo < 0 || colDesc < 0)
            throw new ProjectOperationException(
                ProjectOperationKind.ExcelImport,
                "Formato no reconocido: el Excel debe tener columnas CÓDIGO y DESCRIPCIÓN.");

        var rows = new List<MaterialSpreadsheetRow>();
        for (int i = 1; i < sheet.Count; i++)
        {
            var row = sheet[i];
            string code = GetCell(row, colCodigo);
            string desc = GetCell(row, colDesc);
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(desc))
                continue;
            if (string.IsNullOrWhiteSpace(code))
                continue;

            string unidad = colUnidad >= 0 ? GetCell(row, colUnidad) : "UN";
            if (string.IsNullOrWhiteSpace(unidad))
                unidad = "UN";

            decimal mat = colMat >= 0 ? ParseDecimal(GetCell(row, colMat)) : 0m;
            decimal mo = colMo >= 0 ? ParseDecimal(GetCell(row, colMo)) : 0m;
            rows.Add(new MaterialSpreadsheetRow(code, desc, unidad, mat, mo));
        }

        return rows;
    }

    private static string GetCell(IReadOnlyList<string> row, int column)
    {
        if (column < 0 || column >= row.Count) return "";
        return (row[column] ?? "").Trim();
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("es-CL"), out decimal result))
            return result;
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            return result;
        string normalized = value.Replace(",", "", StringComparison.Ordinal);
        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            return result;
        return 0m;
    }
}
