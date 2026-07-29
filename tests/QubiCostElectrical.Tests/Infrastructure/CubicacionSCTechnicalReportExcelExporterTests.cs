using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using QubiCostElectrical.Tests.Fixtures;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class CubicacionSCTechnicalReportExcelExporterTests : IDisposable
{
    private readonly string tempDir;

    public CubicacionSCTechnicalReportExcelExporterTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"pe_csec_report_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void Export_ProducesTechnicalCsecWorkbook()
    {
        var data = ProjectFileDataFixtures.BuildFullProject();
        data.CubicacionSC[0].Nombre = "TDA Principal";
        data.CubicacionSC[0].Descripcion = "Tablero de prueba";
        data.CubicacionSC[0].Circuitos = new List<CircuitoSCRow>
        {
            new()
            {
                Numero = 1,
                Tipo = "Alumbrado",
                Estado = "Activo",
                Descripcion = "Circuito prueba",
                DistanciaTablero = 12m,
                MetrosTuberia = 34m,
                TipoTuberia = "PVC",
                SeccionTuberia = "20mm",
                ConductorDerivacion = "H07V-U",
                SeccionDerivacion = "4mm2",
                ConductorDistribucion = "H07V-U",
                SeccionDistribucion = "1.5mm2",
                NumConductores = 3m,
                Sistema = "Monofasico",
                Artefactos = new List<ArtefactoSCRow>
                {
                    new() { Nombre = "INT 9/12", Cantidad = 2m }
                }
            }
        };

        string path = Path.Combine(tempDir, "reporte-csec.xlsx");
        var exporter = new CubicacionSCTechnicalReportExcelExporter();
        exporter.Export(new ExportProjectContext
        {
            Data = data,
            ExportedBy = "Test",
            ExportedAt = new DateTime(2026, 6, 18, 10, 30, 0)
        }, path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);

        Assert.Equal(5, wb.Worksheets.Count);
        Assert.Equal("REPORTE TECNICO C.SEC", wb.Worksheet("00_RESUMEN").Cell(1, 1).GetString());
        Assert.Equal("TDA Principal", wb.Worksheet("01_TABLEROS").Cell(5, 1).GetString());
        Assert.Equal("PVC", wb.Worksheet("02_CIRCUITOS").Cell(5, 8).GetString());
        Assert.NotEmpty(wb.Worksheet("03_MATERIALES").Cell(5, 2).GetString());
    }
}
