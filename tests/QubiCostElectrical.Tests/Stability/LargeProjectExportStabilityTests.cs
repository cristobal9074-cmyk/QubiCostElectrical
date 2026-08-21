using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Stability;

// Fase de estabilización: el documento exportado (oferta) es lo único que la constructora
// realmente ve — si esto falla o tarda demasiado con un proyecto real, nada de lo demás
// importa comercialmente. Prueba el exportador principal con 300+ partidas y miles de
// recursos, igual que la carga de trabajo real de un presupuestador.
public sealed class LargeProjectExportStabilityTests : IDisposable
{
    private readonly string _outputPath;

    public LargeProjectExportStabilityTests()
    {
        _outputPath = Path.Combine(Path.GetTempPath(), $"pe_export_stability_{Guid.NewGuid():N}.xlsx");
    }

    public void Dispose()
    {
        if (File.Exists(_outputPath)) File.Delete(_outputPath);
    }

    private static ExportProjectContext BuildLargeContext(int partidaCount, int rowsPerPartida, int materialCount)
    {
        var itemizado = new List<ItemizadoRow>();
        var cubicacion = new List<CubicacionRow>();
        var materiales = new List<MaterialRow>();

        for (int m = 1; m <= materialCount; m++)
        {
            materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{m:00000}",
                Descripcion = $"Material eléctrico {m}",
                Unidad = m % 3 == 0 ? "UN" : "ML",
                Categoria = m % 2 == 0 ? "Material" : "MO",
                MatUf = 0.01m * (m % 20 + 1),
                MoUf = 0.005m * (m % 10 + 1)
            });
        }

        for (int p = 1; p <= partidaCount; p++)
        {
            string itemId = $"P{p}";
            itemizado.Add(new ItemizadoRow
            {
                Id = itemId,
                Item = $"{p / 20 + 1}.{p % 20 + 1}",
                Descripcion = $"Partida eléctrica real número {p}",
                Unidad = "GL",
                Cantidad = 1m,
                Observaciones = p % 15 == 0 ? $"Observación de constructora para partida {p}." : ""
            });

            for (int r = 1; r <= rowsPerPartida; r++)
            {
                var mat = materiales[(p * 7 + r) % materialCount];
                cubicacion.Add(new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    Codigo = mat.Codigo,
                    Descripcion = mat.Descripcion,
                    Unidad = mat.Unidad,
                    Cantidad = (r % 50) + 1,
                    MatUf = mat.MatUf,
                    MoUf = mat.MoUf,
                    Area = "General"
                });
            }
        }

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto grande de estabilización",
                Cliente = "Constructora de prueba",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 37000m
            },
            Itemizado = itemizado,
            Cubicacion = cubicacion,
            Materiales = materiales
        };

        return new ExportProjectContext { Data = data, ExportedBy = "Test" };
    }

    [Fact]
    public void ExportExcel_ProyectoGrande_NoLanzaExcepcionYEsRazonablementeRapido()
    {
        var ctx = BuildLargeContext(partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        var exporter = new ExcelExportInfrastructure();

        var sw = Stopwatch.StartNew();
        var ex = Record.Exception(() => exporter.Export(ctx, _outputPath));
        sw.Stop();

        Assert.Null(ex);
        Assert.True(File.Exists(_outputPath));
        var bytes = File.ReadAllBytes(_outputPath);
        Assert.True(bytes.Length > 0);
        // Margen ampliado para tolerar contención de CPU al correr junto al resto de la
        // suite completa (miles de pruebas); lo que importa es detectar una regresión de
        // orden de magnitud, no micro-variaciones de carga de máquina.
        Assert.True(sw.Elapsed.TotalSeconds < 120,
            $"Exportar Excel con {ctx.Data.Cubicacion.Count} recursos tardó {sw.Elapsed.TotalSeconds:N1}s (límite: 120s).");
    }

    [Fact]
    public void ExportPdf_ProyectoGrande_NoLanzaExcepcionYEsRazonablementeRapido()
    {
        var ctx = BuildLargeContext(partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        var pdfPath = Path.ChangeExtension(_outputPath, ".pdf");
        try
        {
            var exporter = new QuestPdfProjectExporter(
                new BudgetCalculationService(),
                new CurrencyConversionService());

            var sw = Stopwatch.StartNew();
            var ex = Record.Exception(() => exporter.Export(ctx, pdfPath));
            sw.Stop();

            Assert.Null(ex);
            Assert.True(File.Exists(pdfPath));
            Assert.True(sw.Elapsed.TotalSeconds < 60,
                $"Exportar PDF con {ctx.Data.Cubicacion.Count} recursos tardó {sw.Elapsed.TotalSeconds:N1}s (límite: 60s).");
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }
}
