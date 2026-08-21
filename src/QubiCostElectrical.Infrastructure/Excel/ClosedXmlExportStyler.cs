using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

public static class ClosedXmlExportStyler
{
    public static readonly Color Navy = Color.FromArgb(11, 31, 55);
    public static readonly Color Blue = Color.FromArgb(20, 84, 145);
    public static readonly Color Orange = Color.FromArgb(245, 130, 32);
    public static readonly Color TextDark = Color.FromArgb(30, 41, 59);
    public static readonly Color Border = Color.FromArgb(214, 221, 230);

    private static CorporateThemePreferences? corporateTheme;

    public static void ConfigureCorporateTheme(CorporateThemePreferences? theme)
    {
        corporateTheme = theme?.Enabled == true ? theme : null;
    }

    public static void ConfigureCorporateTheme(ExportCorporateThemeSettings? theme)
    {
        if (theme?.Enabled == true)
        {
            corporateTheme = new CorporateThemePreferences
            {
                Enabled = true,
                Name = theme.Name,
                CompanyName = theme.CompanyName,
                LogoPath = theme.LogoPath,
                PrimaryHex = theme.PrimaryHex,
                AccentHex = theme.AccentHex,
                BackgroundHex = theme.BackgroundHex,
                FontFamily = theme.FontFamily
            };
        }
        else
        {
            corporateTheme = null;
        }
    }

    private static Color ThemeNavy => ParseHex(corporateTheme?.PrimaryHex, Navy);
    private static Color ThemeBlue => ParseHex(corporateTheme?.AccentHex, Blue);
    private static string ThemeFont => string.IsNullOrWhiteSpace(corporateTheme?.FontFamily) ? "Segoe UI" : corporateTheme!.FontFamily.Trim();

    public static void AddSheetTitle(IXLWorksheet ws, string title, int columnCount)
    {
        ws.Range(1, 1, 1, columnCount).Merge();
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromColor(ThemeNavy);
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Row(1).Height = 26;
    }

    public static void SetHeaderRow(IXLWorksheet ws, int row, IEnumerable<string> headers, int startColumn = 1)
    {
        int col = startColumn;
        foreach (string header in headers)
        {
            ws.Cell(row, col).Value = header;
            ws.Cell(row, col).Style.Font.Bold = true;
            ws.Cell(row, col).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, col).Style.Fill.BackgroundColor = XLColor.FromColor(ThemeBlue);
            ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Cell(row, col).Style.Border.OutsideBorderColor = XLColor.FromColor(Border);
            col++;
        }
    }

    public static void StyleClosedXmlTable(IXLWorksheet ws, int headerRow, int firstColumn, int lastRow, int lastColumn, string tableName)
    {
        if (lastRow < headerRow) lastRow = headerRow;
        var range = ws.Range(headerRow, firstColumn, lastRow, lastColumn);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromColor(Border);
        range.Style.Border.InsideBorderColor = XLColor.FromColor(Border);
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            if ((r - headerRow) % 2 == 0)
                ws.Range(r, firstColumn, r, lastColumn).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(248, 250, 252));
        }
    }

    public static void PrepareClosedXmlSheet(IXLWorksheet ws)
    {
        ws.Style.Font.FontName = ThemeFont;
        ws.Style.Font.FontSize = 9;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.SheetView.FreezeRows(3);
    }

    public static void FinalizeClosedXmlWorksheet(IXLWorksheet ws)
    {
        try
        {
            int rowCount = ws.RowsUsed().Count();
            if (rowCount > 150)
            {
                ws.Columns().AdjustToContents(1, 100);
            }
            else
            {
                ws.Columns().AdjustToContents();
            }
            foreach (var col in ws.ColumnsUsed())
                col.Width = Math.Min(60, Math.Max(8, col.Width));
        }
        catch
        {
            // ClosedXML can fail AdjustToContents with some fonts in headless/test environments.
        }
    }

    public static void WriteInfoPair(IXLWorksheet ws, int row, string label, object? value, string? numberFormat = null)
    {
        ws.Cell(row, 1).Value = label;
        if (value is decimal dec)
            ws.Cell(row, 2).SetValue(ToDouble(dec));
        else if (value is double dbl)
            ws.Cell(row, 2).SetValue(dbl);
        else if (value is DateTime dt)
            ws.Cell(row, 2).SetValue(dt);
        else
            ws.Cell(row, 2).Value = value?.ToString() ?? "";

        if (!string.IsNullOrWhiteSpace(numberFormat))
            ws.Cell(row, 2).Style.NumberFormat.Format = numberFormat;

        ws.Range(row, 1, row, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 1, row, 2).Style.Border.OutsideBorderColor = XLColor.FromColor(Border);
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(240, 244, 249));
    }

    private static Color ParseHex(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6) return fallback;
        try
        {
            return Color.FromArgb(
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16));
        }
        catch
        {
            return fallback;
        }
    }

    public static double ToDouble(decimal value) => decimal.ToDouble(value);

    public static string UfExcelFormat() => "#,##0.000 \"UF\"";
    public static string ClpExcelFormat() => "$ #,##0";
    public static string UsdExcelFormat() => "US$ #,##0.00";

    public static string ProjectCurrencyExcelFormat(string currency)
    {
        return string.Equals(currency, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase) ? ClpExcelFormat()
            : string.Equals(currency, CurrencyConversionService.CurrencyUsd, StringComparison.OrdinalIgnoreCase) ? UsdExcelFormat()
            : UfExcelFormat();
    }
}
