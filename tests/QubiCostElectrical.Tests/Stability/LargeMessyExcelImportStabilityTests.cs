using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Xunit;

namespace QubiCostElectrical.Tests.Stability;

// Fase de estabilización: el import de Excel es el otro gran punto de contacto con datos
// reales (junto con la exportación). ItemizadoDirtyExcelImportTests ya cubre 10 escenarios
// "sucios" pequeños; lo que falta es la combinación realista: un archivo GRANDE (miles de
// filas) Y desprolijo a la vez — filas en blanco intercaladas, WBS duplicados, formatos de
// número mixtos, encabezados con acentos — para encontrar crashes o degradación de
// rendimiento antes de que un cliente suba su Excel real de 3000 partidas.
public sealed class LargeMessyExcelImportStabilityTests : IDisposable
{
    private readonly string _path;
    private readonly ItemizadoSpreadsheetReader _reader = new();
    private readonly ImportarItemizadoUseCase _import = new();

    public LargeMessyExcelImportStabilityTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"qce-large-messy-{Guid.NewGuid():N}.xlsx");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private void BuildMessyWorkbook(int rowCount)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Itemizado");

        // Encabezados con acentos y variantes — a propósito, para forzar la detección
        // tolerante de columnas en vez de un match exacto.
        ws.Cell(1, 1).Value = "Ítem";
        ws.Cell(1, 2).Value = "Descripción";
        ws.Cell(1, 3).Value = "Ud.";
        ws.Cell(1, 4).Value = "Cant.";

        int row = 2;
        int chapter = 0;
        for (int i = 1; i <= rowCount; i++)
        {
            // Fila en blanco cada ~50 filas.
            if (i % 50 == 0)
            {
                ws.Row(row).Clear();
                row++;
                continue;
            }

            // Nuevo capítulo cada ~40 ítems.
            if (i % 40 == 1)
            {
                chapter++;
                ws.Cell(row, 1).Value = chapter.ToString();
                ws.Cell(row, 2).Value = $"Capítulo {chapter}";
                ws.Cell(row, 3).Value = "GL";
                ws.Cell(row, 4).Value = 1;
                row++;
            }

            string wbs = i % 200 == 0
                ? $"{chapter},1" // WBS duplicado a propósito cada 200 filas
                : $"{chapter},{i % 40}";

            object cantidad = (i % 30) switch
            {
                0 => "1,5",           // decimal con coma
                1 => "1.250,50",      // formato chileno con miles
                2 => "=2+3",          // fórmula
                _ => i % 7 + 1,
            };

            ws.Cell(row, 1).Value = wbs;
            ws.Cell(row, 2).Value = i % 90 == 0 ? "" : $"Partida eléctrica {i}"; // descripción vacía ocasional
            ws.Cell(row, 3).Value = i % 3 == 0 ? "UN" : "ML";
            ws.Cell(row, 4).Value = XLCellValue.FromObject(cantidad);
            row++;
        }

        wb.SaveAs(_path);
    }

    [Theory]
    [InlineData(3000)]
    [InlineData(8000)]
    public void ImportacionGrandeYDesprolija_NoLanzaExcepcionYEsRazonablementeRapida(int rowCount)
    {
        BuildMessyWorkbook(rowCount);

        var sw = Stopwatch.StartNew();
        var table = _reader.ReadFirstWorksheet(_path).Select(r => r.ToList()).ToList();
        var result = _import.Execute(table);
        sw.Stop();

        Assert.Equal("", result.Error);
        Assert.NotEmpty(result.Rows);

        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(result.Rows);
        var (imported, rejected, warnings) = ItemizadoImportSummary.CountPreviewRows(previews);

        int nonTitles = previews.Count(p => !p.EsTitulo);
        Assert.Equal(nonTitles, imported + rejected);
        Assert.True(warnings <= imported);

        // Umbral generoso: lo que importa es detectar una regresión de orden de magnitud
        // (ej. un algoritmo O(n²) en la detección de duplicados), no micro-variaciones ni
        // contención de CPU al correr junto al resto de la suite completa.
        Assert.True(sw.Elapsed.TotalSeconds < 40,
            $"Importar {rowCount} filas desprolijas tardó {sw.Elapsed.TotalSeconds:N1}s (límite: 40s).");
    }
}
