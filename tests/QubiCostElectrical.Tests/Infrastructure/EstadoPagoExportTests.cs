using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Exportación de Estados de pago: builder (agrupado por capítulo + totales + cronograma),
/// exportador Excel (ClosedXML) y exportador PDF (QuestPDF). No valida formato a formato,
/// solo que el archivo se genera con el contenido clave y sin excepciones.
/// </summary>
public sealed class EstadoPagoExportTests : IDisposable
{
    private readonly string xlsxPath;
    private readonly string pdfPath;

    public EstadoPagoExportTests()
    {
        xlsxPath = Path.Combine(Path.GetTempPath(), $"estado_pago_{Guid.NewGuid():N}.xlsx");
        pdfPath = Path.Combine(Path.GetTempPath(), $"estado_pago_{Guid.NewGuid():N}.pdf");
    }

    public void Dispose()
    {
        if (File.Exists(xlsxPath)) File.Delete(xlsxPath);
        if (File.Exists(pdfPath)) File.Delete(pdfPath);
    }

    private static EstadoPagoConfig Config() => new()
    {
        Anio = 2026,
        CantMeses = 12,
        AnticipoPct = 10m,
        RetencionPct = 5m,
        IvaPct = 19m
    };

    private static EstadoPagoMesRow MesDeMuestra() => new()
    {
        Id = EstadoPagoMesRow.BuildId(2026, 3),
        Anio = 2026,
        Mes = 3,
        Estado = EstadoPagoEst.Revision,
        Folio = EstadoPagoMesRow.BuildFolio(2026, 3),
        FechaEmision = new DateTime(2026, 3, 5),
        AprobadoPor = "Juan Pérez",
        AvancesPorItem = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["P001"] = 10m,
            ["P002"] = 8m,
            ["P003"] = 12m
        }
    };

    private static IReadOnlyList<EstadoPagoExportSeccionInput> Secciones() => new List<EstadoPagoExportSeccionInput>
    {
        new("1 · INSTALACIONES ELÉCTRICAS", new List<EstadoPagoPartidaInput>
        {
            new("P001", "1.1", "Canalización PVC 20mm", 100m),
            new("P002", "1.2", "Conductores 2.5 mm2", 50m)
        }),
        new("", new List<EstadoPagoPartidaInput>
        {
            new("P003", "2.1", "Tablero general", 150m)
        })
    };

    private static EstadoPagoExportRequest SampleRequest()
    {
        var row = MesDeMuestra();
        var request = EstadoPagoExportBuilder.Build(
            "Proyecto Test Estados de pago",
            Config(),
            row,
            Secciones(),
            new List<EstadoPagoMesRow> { row },
            "Tester",
            new DateTime(2026, 3, 6));

        Assert.NotNull(request);
        return request!;
    }

    [Fact]
    public void Builder_AgrupaPorCapitulo_YCalculaTotalesDelMes()
    {
        var request = SampleRequest();

        Assert.Equal("EP-2026-03", request.Mes.Folio);
        Assert.Equal(3, request.Mes.Mes);
        Assert.Equal("Marzo", request.Mes.MesNombre);
        Assert.Equal("Revisión", request.Mes.EstadoLabel);

        Assert.Equal(2, request.Mes.Secciones.Count);
        Assert.Equal("1 · INSTALACIONES ELÉCTRICAS", request.Mes.Secciones[0].Capitulo);
        Assert.Equal(2, request.Mes.Secciones[0].Filas.Count);
        Assert.Equal(1, request.Mes.Secciones[1].Filas.Count); // capítulo vacío conserva su partida

        var t = request.Mes.Totales;
        Assert.Equal(32m, t.TrabajoMes);
        Assert.Equal(3.2m, t.RecuperoMes);
        Assert.Equal(1.6m, t.RetencionMes);
        Assert.Equal(27.2m, t.Neto);
        Assert.Equal(5.168m, t.Iva);
        Assert.Equal(32.368m, t.Total);
    }

    [Fact]
    public void Builder_Cronograma_Tiene12MesesYMesesPendientesSinTotal()
    {
        var request = SampleRequest();

        Assert.Equal(12, request.Cronograma.Count);
        var marzo = request.Cronograma.Single(m => m.Mes == 3);
        Assert.Equal("EP-2026-03", marzo.Folio);
        Assert.Equal("Revisión", marzo.EstadoLabel);
        Assert.Equal(32.368m, marzo.Total);

        var enero = request.Cronograma.Single(m => m.Mes == 1);
        Assert.Equal("Pendiente", enero.EstadoLabel);
        Assert.Null(enero.Total);
    }

    [Fact]
    public void Builder_SinRow_DevuelveNull()
    {
        var request = EstadoPagoExportBuilder.Build(
            "Proyecto", Config(), null, Secciones(), new List<EstadoPagoMesRow>(), "Tester", AppClock.Now);

        Assert.Null(request);
    }

    [Fact]
    public void ExportExcel_ProduceArchivoConMesYCronograma()
    {
        var exporter = new EstadoPagoExcelExporter();
        var ex = Record.Exception(() => exporter.Export(SampleRequest(), xlsxPath));
        Assert.Null(ex);

        Assert.True(File.Exists(xlsxPath), "El archivo xlsx no fue creado.");
        var bytes = File.ReadAllBytes(xlsxPath);
        Assert.True(bytes.Length > 0, "El archivo xlsx está vacío.");
        Assert.Equal(0x50, bytes[0]); // PK (ZIP)
        Assert.Equal(0x4B, bytes[1]);

        using var wb = new XLWorkbook(xlsxPath);
        Assert.Contains("EstadoPago", wb.Worksheets.Select(ws => ws.Name));
        Assert.Contains("Cronograma", wb.Worksheets.Select(ws => ws.Name));

        var ws = wb.Worksheet("EstadoPago");
        Assert.Equal("ESTADO DE PAGO · EP-2026-03", ws.Cell(1, 1).GetString());
        Assert.Equal("EP-2026-03", ws.Cell(2, 4).GetString());
        Assert.Equal("Proyecto Test Estados de pago", ws.Cell(2, 2).GetString());

        var usada = ws.CellsUsed();
        var textos = usada.Select(c => c.GetString()).ToList();
        Assert.Contains("1 · INSTALACIONES ELÉCTRICAS", textos);
        Assert.Contains("Canalización PVC 20mm", textos);
        Assert.Contains("TOTAL A PAGAR", textos);

        var crono = wb.Worksheet("Cronograma");
        int filasCrono = crono.RowsUsed().Count();
        Assert.True(filasCrono >= 16, $"Cronograma con pocas filas: {filasCrono}");
        var textosCrono = crono.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Marzo", textosCrono);
        Assert.Contains("Pendiente", textosCrono);
    }

    [Fact]
    public void ExportExcel_SobrescribeArchivoExistente()
    {
        File.WriteAllText(xlsxPath, "basura previa");

        var exporter = new EstadoPagoExcelExporter();
        exporter.Export(SampleRequest(), xlsxPath);

        using var wb = new XLWorkbook(xlsxPath);
        Assert.Equal("EstadoPago", wb.Worksheets.First().Name);
    }

    [Fact]
    public void ExportPdf_ProduceDocumentoValido()
    {
        var exporter = new EstadoPagoPdfExporter();
        var ex = Record.Exception(() => exporter.Export(SampleRequest(), pdfPath));
        Assert.Null(ex);

        Assert.True(File.Exists(pdfPath), "El archivo pdf no fue creado.");
        var bytes = File.ReadAllBytes(pdfPath);
        Assert.True(bytes.Length > 1000, "El pdf generado es demasiado pequeño.");
        Assert.Equal(0x25, bytes[0]); // %
        Assert.Equal(0x50, bytes[1]); // P
        Assert.Equal(0x44, bytes[2]); // D
        Assert.Equal(0x46, bytes[3]); // F
    }

    [Fact]
    public void Export_RutaInvalida_LanzaArgumentException()
    {
        var excel = new EstadoPagoExcelExporter();
        Assert.Throws<ArgumentNullException>(() => excel.Export(null!, xlsxPath));
        Assert.Throws<ArgumentException>(() => excel.Export(SampleRequest(), "   "));

        var pdf = new EstadoPagoPdfExporter();
        Assert.Throws<ArgumentNullException>(() => pdf.Export(null!, pdfPath));
        Assert.Throws<ArgumentException>(() => pdf.Export(SampleRequest(), ""));
    }
}
