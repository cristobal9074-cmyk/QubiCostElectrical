using System.IO;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>Simula 10 Excels “sucios” y verifica que el resumen de importación siempre cuadra.</summary>
public sealed class ItemizadoDirtyExcelImportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ItemizadoSpreadsheetReader _reader = new();
    private readonly ImportarItemizadoUseCase _import = new();

    public ItemizadoDirtyExcelImportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"qce-dirty-xlsx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Theory]
    [InlineData("portada-sin-itemizado", false)]
    [InlineData("formulas-cantidad", true)]
    [InlineData("formato-mixto-coma", true)]
    [InlineData("descripcion-vacia", true)]
    [InlineData("wbs-duplicado", true)]
    [InlineData("titulos-y-partidas", true)]
    [InlineData("filas-en-blanco", true)]
    [InlineData("encabezados-acento", true)]
    [InlineData("solo-encabezados", false)]
    [InlineData("sin-columna-unidad", true)]
    public void DirtyExcel_import_summary_always_balances(string scenario, bool expectData)
    {
        string path = BuildWorkbook(scenario);
        var table = _reader.ReadFirstWorksheet(path).Select(r => r.ToList()).ToList();
        var result = _import.Execute(table);

        if (!expectData)
        {
            Assert.NotEqual("", result.Error);
            return;
        }

        Assert.Equal("", result.Error);
        Assert.NotEmpty(result.Rows);

        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(result.Rows);
        var (imported, rejected, warnings) = ItemizadoImportSummary.CountPreviewRows(previews);

        int nonTitles = previews.Count(p => !p.EsTitulo);
        Assert.Equal(nonTitles, imported + rejected);
        int acceptedNonTitles = ItemizadoImportPreviewService.AcceptRows(previews).Count(r => !r.EsTitulo);
        Assert.Equal(acceptedNonTitles, imported);
        Assert.True(warnings <= imported);

        string summary = ItemizadoImportSummary.FormatStatus("Importación", imported, rejected, warnings);
        Assert.Contains(imported.ToString(), summary);
        Assert.Contains(rejected.ToString(), summary);
        Assert.Contains(warnings.ToString(), summary);
    }

    private string BuildWorkbook(string scenario)
    {
        string path = Path.Combine(_tempDir, $"{scenario}.xlsx");
        using var wb = new XLWorkbook();

        switch (scenario)
        {
            case "portada-sin-itemizado":
                wb.AddWorksheet("Portada").Cell(1, 1).Value = "Proyecto sin itemizado";
                break;

            case "formulas-cantidad":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1", "Capítulo", "GL", 1);
                    Data(ws, 3, "1,1", "Con fórmula", "UN", "=1+2");
                    Data(ws, 4, "1,2", "Cantidad cero", "UN", "=0");
                });
                break;

            case "formato-mixto-coma":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1,1", "Decimal coma", "ML", "1,5");
                    Data(ws, 3, "1,2", "Miles", "UN", "12.250");
                    Data(ws, 4, "1,3", "Chileno", "UN", "1.250,50");
                });
                break;

            case "descripcion-vacia":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1,1", "Válida", "UN", 1);
                    Data(ws, 3, "1,2", "", "UN", 1);
                });
                break;

            case "wbs-duplicado":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1,1", "A", "UN", 1);
                    Data(ws, 3, "1,1", "B", "UN", 1);
                });
                break;

            case "titulos-y-partidas":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1", "Capítulo 1", "GL", 1);
                    Data(ws, 3, "1,1", "Partida A", "UN", 2);
                    Data(ws, 4, "2", "Capítulo 2", "GL", 1);
                    Data(ws, 5, "2,1", "Partida B", "ML", 10);
                });
                break;

            case "filas-en-blanco":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                    Data(ws, 2, "1,1", "Antes del hueco", "UN", 1);
                    ws.Row(3).Clear();
                    Data(ws, 4, "1,2", "Después del hueco", "UN", 3);
                });
                break;

            case "encabezados-acento":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "Ítem", "Descripción", "Ud.", "Cant.");
                    Data(ws, 2, "1,1", "Con acentos", "UN", 1);
                });
                break;

            case "solo-encabezados":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD");
                });
                break;

            case "sin-columna-unidad":
                AddItemizadoSheet(wb, "Itemizado", ws =>
                {
                    Header(ws, "ITEM", "DESCRIPCION", "CANTIDAD");
                    Data(ws, 2, "1,1", "Sin unidad explícita", 4);
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        wb.SaveAs(path);
        return path;
    }

    private static void AddItemizadoSheet(XLWorkbook wb, string name, Action<IXLWorksheet> configure)
    {
        var ws = wb.AddWorksheet(name);
        configure(ws);
    }

    private static void Header(IXLWorksheet ws, params string[] cols)
    {
        for (int i = 0; i < cols.Length; i++)
            ws.Cell(1, i + 1).Value = cols[i];
    }

    private static void Data(IXLWorksheet ws, int row, params object[] cols)
    {
        for (int i = 0; i < cols.Length; i++)
            ws.Cell(row, i + 1).Value = XLCellValue.FromObject(cols[i]);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }
}
