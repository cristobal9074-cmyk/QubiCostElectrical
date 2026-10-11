using System;
using System.IO;
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
    public void PurchaseListExcelExporter_ProducesValidRfqXlsx()
    {
        string path = Path.Combine(_tempDir, "rfq.xlsx");
        var exporter = new PurchaseListExcelExporter();
        exporter.ExportRfq(path, new PurchaseListRfqRequest(
            "Edificio Vista Parque",
            "Instalaciones Eléctricas SpA",
            5m,
            new[]
            {
                new CompraMaterialViewModel("MAT001", "Cable EVA 2.5 mm²", "ML", "Cables", "Proveedor A", 100m, 5m, 105m, 0.12m, 12m, 12.6m, 3)
            }));

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
