using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class BudgetRevisionDiffTests : IDisposable
{
    private readonly string _tempExcelPath;
    private readonly string _tempPdfPath;

    public BudgetRevisionDiffTests()
    {
        _tempExcelPath = Path.Combine(Path.GetTempPath(), $"diff_test_{Guid.NewGuid():N}.xlsx");
        _tempPdfPath = Path.Combine(Path.GetTempPath(), $"diff_test_{Guid.NewGuid():N}.pdf");
    }

    public void Dispose()
    {
        if (File.Exists(_tempExcelPath))
        {
            try { File.Delete(_tempExcelPath); } catch { }
        }
        if (File.Exists(_tempPdfPath))
        {
            try { File.Delete(_tempPdfPath); } catch { }
        }
    }

    private static (BudgetVersionData baseline, ProjectFileData current) CreateSampleVersions()
    {
        var baseline = new BudgetVersionData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Alimentador TDF", Unidad = "GL", Cantidad = 1m },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Enchufes Modificados", Unidad = "UN", Cantidad = 10m },
                new ItemizadoRow { Id = "P3", Item = "1.3", Descripcion = "Circuito Eliminado Rev0", Unidad = "GL", Cantidad = 1m }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Codigo = "CAB-01", Descripcion = "Superflex 4x16mm2", Cantidad = 50m, MatUf = 0.5m, Tipo = "MAT" },
                new CubicacionRow { ItemId = "P2", Codigo = "ENC-01", Descripcion = "Enchufe Doble 16A", Cantidad = 10m, MatUf = 0.2m, Tipo = "MAT" },
                new CubicacionRow { ItemId = "P3", Codigo = "LUM-01", Descripcion = "Campana Halógena", Cantidad = 5m, MatUf = 1.0m, Tipo = "MAT" }
            ]
        };

        var current = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Edificio Costanera Tower",
                Cliente = "Constructora Metropolitana S.A.",
                ValorUfClp = 38500m
            },
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Alimentador TDF", Unidad = "GL", Cantidad = 1m },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Enchufes Modificados", Unidad = "UN", Cantidad = 15m }, // +5 unidades
                new ItemizadoRow { Id = "P4", Item = "1.4", Descripcion = "Nuevo Circuito Iluminación Emergencia Rev1", Unidad = "GL", Cantidad = 1m } // Nuevo
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Codigo = "CAB-01", Descripcion = "Superflex 4x16mm2", Cantidad = 50m, MatUf = 0.5m, Tipo = "MAT" },
                new CubicacionRow { ItemId = "P2", Codigo = "ENC-01", Descripcion = "Enchufe Doble 16A", Cantidad = 15m, MatUf = 0.2m, Tipo = "MAT" },
                new CubicacionRow { ItemId = "P4", Codigo = "EMG-01", Descripcion = "Equipo Autónomo LED 2x5W", Cantidad = 8m, MatUf = 1.5m, Tipo = "MAT" }
            ]
        };

        return (baseline, current);
    }

    [Fact]
    public void ComparisonService_TrafficLightClassification_IdentifiesAdicionalDeductivoAndModificado()
    {
        var (baseline, current) = CreateSampleVersions();
        var service = new BudgetVersionComparisonService();

        var summary = service.Compare(baseline, current, "Rev 0 - Licitación Inicial", "Rev 1 - Modificación Mandante");

        Assert.Equal(38500m, summary.ValorUf);
        Assert.Equal("Edificio Costanera Tower", summary.ProjectTitle);
        Assert.Equal("Constructora Metropolitana S.A.", summary.ClientName);

        // Check rows
        var adicionalRow = summary.Rows.FirstOrDefault(r => r.ItemId == "P4");
        Assert.NotNull(adicionalRow);
        Assert.Equal(RevisionDiffKind.Adicional, adicionalRow.DiffKind);
        Assert.Contains("Adicional", adicionalRow.DisplayBadge, StringComparison.OrdinalIgnoreCase);
        Assert.True(adicionalRow.DeltaUf > 0);
        Assert.True(adicionalRow.DeltaClp > 0);

        var deductivoRow = summary.Rows.FirstOrDefault(r => r.ItemId == "P3");
        Assert.NotNull(deductivoRow);
        Assert.Equal(RevisionDiffKind.Deductivo, deductivoRow.DiffKind);
        Assert.Contains("Deductivo", deductivoRow.DisplayBadge, StringComparison.OrdinalIgnoreCase);
        Assert.True(deductivoRow.DeltaUf < 0);
        Assert.True(deductivoRow.DeltaClp < 0);

        var modificadoRow = summary.Rows.FirstOrDefault(r => r.ItemId == "P2");
        Assert.NotNull(modificadoRow);
        Assert.Equal(RevisionDiffKind.Modificado, modificadoRow.DiffKind);
        Assert.Contains("Mayor metraje", modificadoRow.DisplayBadge, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10m, modificadoRow.CantidadBase);
        Assert.Equal(15m, modificadoRow.CantidadActual);
        Assert.Equal(5m, modificadoRow.DeltaCantidad);
        Assert.NotEmpty(modificadoRow.ResourceDiffs);

        // Check totals
        Assert.True(summary.TotalAdicionalesUf > 0);
        Assert.True(summary.TotalDeductivosUf < 0);
        Assert.Equal(summary.TotalAdicionalesUf + summary.TotalDeductivosUf, summary.NetVariationUf, 4);
    }

    [Fact]
    public void ExcelExporter_GeneratesValidWorkbookWithLiveFormulas()
    {
        var (baseline, current) = CreateSampleVersions();
        var service = new BudgetVersionComparisonService();
        var summary = service.Compare(baseline, current, "Rev 0 - Base", "Rev 1 - Actual");

        var exporter = new BudgetRevisionDiffExcelExporter();
        exporter.Export(summary, _tempExcelPath);

        Assert.True(File.Exists(_tempExcelPath));

        using var wb = new XLWorkbook(_tempExcelPath);
        Assert.Equal(3, wb.Worksheets.Count);

        // Check Sheet 1: Executive Summary
        var wsResumen = wb.Worksheet("01_RESUMEN_EJECUTIVO");
        Assert.NotNull(wsResumen);
        Assert.Equal(38500m, wsResumen.Cell("B2").GetValue<decimal>());

        // Check Sheet 2: Detalle Variaciones formulas
        var wsDetalle = wb.Worksheet("02_DETALLE_VARIACIONES");
        Assert.NotNull(wsDetalle);

        // First data row is row 5
        var formulaDeltaCantidad = wsDetalle.Cell("F5").FormulaA1;
        Assert.Equal("E5-D5", formulaDeltaCantidad);

        var formulaDeltaPu = wsDetalle.Cell("I5").FormulaA1;
        Assert.Equal("H5-G5", formulaDeltaPu);

        var formulaDeltaTotalUf = wsDetalle.Cell("L5").FormulaA1;
        Assert.Equal("K5-J5", formulaDeltaTotalUf);

        var formulaDeltaClp = wsDetalle.Cell("M5").FormulaA1;
        Assert.Equal("L5*'01_RESUMEN_EJECUTIVO'!$B$2", formulaDeltaClp);

        // Check Sheet 3: Solicitud Adicionales only includes items with positive net variation
        var wsSolicitud = wb.Worksheet("03_SOLICITUD_ADICIONALES");
        Assert.NotNull(wsSolicitud);
        Assert.NotEmpty(wsSolicitud.RowsUsed());
    }

    [Fact]
    public void PdfExporter_GeneratesFormalExecutiveDocument()
    {
        var (baseline, current) = CreateSampleVersions();
        var service = new BudgetVersionComparisonService();
        var summary = service.Compare(baseline, current, "Rev 0 - Base", "Rev 1 - Actual");

        var exporter = new BudgetRevisionDiffPdfExporter();
        exporter.Export(summary, _tempPdfPath);

        Assert.True(File.Exists(_tempPdfPath));
        var bytes = File.ReadAllBytes(_tempPdfPath);
        Assert.True(bytes.Length > 2000, "El PDF de informe de adicionales debe tener contenido sustancial");

        // Verify PDF Magic Header "%PDF"
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }
}
