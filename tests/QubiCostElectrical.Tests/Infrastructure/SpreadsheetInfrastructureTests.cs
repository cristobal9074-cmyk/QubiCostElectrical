using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class SpreadsheetInfrastructureTests : IDisposable
{
    private readonly string _tempDir;

    public SpreadsheetInfrastructureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pe_sheet_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void PurchaseListExcelExporter_ProducesValidXlsx()
    {
        string path = Path.Combine(_tempDir, "lista.xlsx");
        var exporter = new PurchaseListExcelExporter();
        exporter.Export(path, new PurchaseListExcelRequest(
            "Proyecto Test",
            5m,
            new[]
            {
                new CompraMaterialViewModel("MAT001", "Cable", "ML", "Cables", "Proveedor A", 10m, 5m, 10.5m, 0.12m, 1.2m, 1.26m, 2)
            }));

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public void TemplateSpreadsheetService_RoundTrip_PreservesTemplateName()
    {
        string path = Path.Combine(_tempDir, "plantillas.xlsx");
        var templates = new List<TemplateRow>
        {
            new()
            {
                Nombre = "Tubo PVC",
                Descripcion = "Canalización",
                UnidadBase = "ML",
                Area = "General",
                Items = new List<TemplateItemRow>
                {
                    new() { Clase = "MATERIAL", Codigo = "MAT001", Tipo = "MAT", Factor = 1m }
                }
            }
        };

        var service = new TemplateSpreadsheetService();
        service.Export(path, templates, "Proyecto", "Cliente");
        var imported = service.Import(path);

        Assert.Single(imported);
        Assert.Equal("Tubo PVC", imported[0].Nombre);
        Assert.Single(imported[0].Items);
        Assert.Equal("MAT001", imported[0].Items[0].Codigo);
    }

    [Fact]
    public void MaterialSpreadsheetImporter_ReadsCodigoAndDescripcion()
    {
        string path = Path.Combine(_tempDir, "materiales.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Precios");
            ws.Cell(1, 1).Value = "Código";
            ws.Cell(1, 2).Value = "Descripción";
            ws.Cell(1, 3).Value = "Unidad";
            ws.Cell(1, 4).Value = "MAT";
            ws.Cell(2, 1).Value = "MAT001";
            ws.Cell(2, 2).Value = "Cable THHN";
            ws.Cell(2, 3).Value = "ML";
            ws.Cell(2, 4).Value = 1.25m;
            wb.SaveAs(path);
        }

        var importer = new MaterialSpreadsheetImporter();
        var rows = importer.ReadRows(path);

        Assert.Single(rows);
        Assert.Equal("MAT001", rows[0].Codigo);
        Assert.Equal("Cable THHN", rows[0].Descripcion);
        Assert.Equal(1.25m, rows[0].MatValue);
    }

    [Fact]
    public void CubicacionSCExcelExporter_ProducesValidXlsx()
    {
        string path = Path.Combine(_tempDir, "csc.xlsx");
        var exporter = new CubicacionSCExcelExporter();
        exporter.Export(path, "Proyecto SC", new[]
        {
            new CubicacionSCExcelRow("Iluminación", "Luminaria LED", 12m, "UN", 0.5m, 6m)
        });

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0x50, bytes[0]);
    }
}
