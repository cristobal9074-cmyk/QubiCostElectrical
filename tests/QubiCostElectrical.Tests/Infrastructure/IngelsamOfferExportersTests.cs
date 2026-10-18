using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class IngelsamOfferExportersTests
{
    private static ExportProjectContext CreateSampleContext()
    {
        var project = new ProjectInfoRow
        {
            Nombre = "EDIFICIO MIRADOR LOS ANDES",
            Cliente = "LD Constructora",
            Ubicacion = "Av. Las Condes 1234, Santiago",
            MonedaPresupuesto = "UF",
            ValorUfClp = 38500m,
            OfertaPdf = new OfferPdfSettings
            {
                CompanyName = "INGELSAM SPA",
                CompanyRut = "76.123.456-7",
                CompanyAddress = "Av. Capellán Benavides 2077. Maipú, Santiago",
                CompanyPhone = "9 5856 3557",
                CompanyEmail = "cperez@ingelsam.cl",
                RecipientName = "Leonardo Sanhueza",
                RecipientCompany = "LD Constructora",
                OfferNumber = "PEL024F2026",
                OfferTitle = "PRESUPUESTO INSTALACIONES ELÉCTRICAS",
                SignatureName = "LUIS SAAVEDRA",
                SignatureRole = "GERENTE GENERAL",
                ScopeText = "Suministro e instalación de ductos de barra.\r\nMontaje de tableros generales.",
                ExclusionsText = "Obras civiles y picados.\r\nTrámites TE1 ante SEC.",
                CommercialConditionsText = "Forma de Pago: 30 días contra estado de pago\r\nPlazo de Ejecución: 90 días corridos\r\nValidez de la Oferta: 30 días",
                IncludePresentationLetter = true,
                PresentationLetterText = "Estimados señores:\r\nJunto con saludar, presentamos nuestra oferta técnica y comercial."
            }
        };

        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "cap-1", Item = "1", Descripcion = "DUCTOS DE BARRA", EsTitulo = true },
            new() { Id = "it-1", Item = "1.1", Descripcion = "Ducto de barra 800A", Unidad = "m", Cantidad = 120m },
            new() { Id = "cap-2", Item = "2", Descripcion = "TABLEROS", EsTitulo = true },
            new() { Id = "it-2", Item = "2.1", Descripcion = "Tablero TDAyF", Unidad = "cu", Cantidad = 5m }
        };

        var cubicacion = new List<CubicacionRow>
        {
            new() { ItemId = "it-1", Codigo = "MAT-001", Cantidad = 120m },
            new() { ItemId = "it-2", Codigo = "MAT-002", Cantidad = 5m }
        };

        var apus = new List<ApuRow>
        {
            new() { ItemId = "it-1", Descripcion = "Ducto 800A" },
            new() { ItemId = "it-2", Descripcion = "Tablero TDAyF" }
        };

        var data = new ProjectFileData
        {
            Proyecto = project,
            Itemizado = itemizado,
            Cubicacion = cubicacion,
            Apus = apus
        };

        return new ExportProjectContext
        {
            Data = data,
            OfferNetAmountUf = 395.040m,
            SensitivityMaterialPct = 0m
        };
    }

    [Fact]
    public void IngelsamOfferSummaryExcelExporter_GeneratesValidWorkbookWithFormulas()
    {
        var exporter = new IngelsamOfferSummaryExcelExporter();
        var ctx = CreateSampleContext();
        string tempPath = Path.Combine(Path.GetTempPath(), $"Test-Ingelsam-{Guid.NewGuid():N}.xlsx");

        try
        {
            exporter.Export(ctx, tempPath);
            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 1000);

            using var wb = new XLWorkbook(tempPath);
            var ws = wb.Worksheet("-=RESUMEN INGELSAM=-");
            Assert.NotNull(ws);

            // Verificar encabezado y datos de emisor
            Assert.Contains("INGELSAM SPA", ws.Cell("B3").GetString());
            Assert.Equal("DATOS CLIENTES", ws.Cell("F3").GetString());
            Assert.Equal("PEL024F2026", ws.Cell("G6").GetString());

            // Verificar tabla y fórmulas
            Assert.Equal("ITEM", ws.Cell(7, 2).GetString());
            Assert.Equal("DESCRIPCIÓN", ws.Cell(7, 3).GetString());
            Assert.Equal("UF UNIT", ws.Cell(7, 9).GetString());
            Assert.Equal("TOTAL UF", ws.Cell(7, 10).GetString());
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void QuestPdfOfferSummaryExporter_GeneratesValidPdf()
    {
        var exporter = new QuestPdfOfferSummaryExporter();
        var ctx = CreateSampleContext();
        string tempPath = Path.Combine(Path.GetTempPath(), $"Test-Resumen-{Guid.NewGuid():N}.pdf");

        try
        {
            exporter.Export(ctx, tempPath);
            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 1000);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void PresentationLetterDocxExporter_GeneratesValidDocxWithTables()
    {
        var exporter = new PresentationLetterDocxExporter();
        var ctx = CreateSampleContext();
        string tempPath = Path.Combine(Path.GetTempPath(), $"Test-Carta-{Guid.NewGuid():N}.docx");

        try
        {
            exporter.Export(ctx, tempPath);
            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 1000);

            using var doc = WordprocessingDocument.Open(tempPath, false);
            var body = doc.MainDocumentPart?.Document.Body;
            Assert.NotNull(body);
            string fullText = body.InnerText;

            Assert.Contains("INGELSAM SPA", fullText);
            Assert.Contains("PRESUPUESTO INSTALACIONES ELÉCTRICAS", fullText);
            Assert.Contains("ALCANCES", fullText);
            Assert.Contains("EXCLUSIONES", fullText);
            Assert.Contains("CONDICIONES COMERCIALES", fullText);
            Assert.Contains("LUIS SAAVEDRA", fullText);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void PartidasAbiertasExcelExporter_GeneratesValidWorkbookWithFormulas()
    {
        var exporter = new PartidasAbiertasExcelExporter();
        var ctx = CreateSampleContext();
        string tempPath = Path.Combine(Path.GetTempPath(), $"Test-PartidasAbiertas-{Guid.NewGuid():N}.xlsx");

        try
        {
            exporter.Export(ctx, tempPath);
            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 1000);

            using var wb = new XLWorkbook(tempPath);
            var ws = wb.Worksheet("-=DETALLE PARTIDAS=-");
            Assert.NotNull(ws);

            // Verificar columnas y datos
            Assert.Contains("INGELSAM SPA", ws.Cell("B3").GetString());
            Assert.Equal("DATOS CLIENTES", ws.Cell("F3").GetString());
            Assert.Equal("ÍTEM", ws.Cell(9, 2).GetString());
            Assert.Equal("CÓDIGO", ws.Cell(9, 3).GetString());
            Assert.Equal("DESCRIPCIÓN", ws.Cell(9, 4).GetString());
            Assert.Equal("COSTO DIR. ($)", ws.Cell(9, 9).GetString());
            Assert.Equal("TOTAL VENTA (UF)", ws.Cell(9, 11).GetString());

            // Verificar que existan fórmulas
            bool hasFormulas = false;
            foreach (var cell in ws.RangeUsed()?.CellsUsed() ?? Enumerable.Empty<IXLCell>())
            {
                if (cell.HasFormula)
                {
                    hasFormulas = true;
                    break;
                }
            }
            Assert.True(hasFormulas);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
