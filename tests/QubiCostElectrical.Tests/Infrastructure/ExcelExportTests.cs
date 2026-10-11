using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Verifica que ExcelExportInfrastructure produce archivos xlsx válidos y no silencia errores.
/// No valida el contenido celda a celda: eso es responsabilidad de los tests de dominio.
/// </summary>
public sealed class ExcelExportTests : IDisposable
{
    private readonly string _outputPath;

    public ExcelExportTests()
    {
        _outputPath = Path.Combine(Path.GetTempPath(), $"pe_excel_test_{Guid.NewGuid():N}.xlsx");
    }

    public void Dispose()
    {
        if (File.Exists(_outputPath)) File.Delete(_outputPath);
    }

    private static ExportProjectContext MinimalContext()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto Test Excel",
                Cliente = "Cliente Test",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 37000m
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "TIT001", Item = "1", Descripcion = "INSTALACIONES ELÉCTRICAS", Unidad = "", Cantidad = 0m, EsTitulo = true },
                new() { Id = "P001",   Item = "1.1", Descripcion = "Canalización PVC 20mm", Unidad = "ML", Cantidad = 50m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C001", ItemId = "P001", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Cantidad = 50m, MatUf = 0.12m, MoUf = 0m },
                new() { Id = "C002", ItemId = "P001", Tipo = "MO",  Codigo = "MO001",  Descripcion = "Instalación PVC", Unidad = "ML", Cantidad = 50m, MatUf = 0m, MoUf = 0.08m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Categoria = "Tubería", MatUf = 0.12m },
                new() { Codigo = "MO001",  Descripcion = "Instalación PVC", Unidad = "ML", Categoria = "MO", MoUf = 0.08m }
            }
        };
        return new ExportProjectContext { Data = data, ExportedBy = "Test" };
    }

    [Fact]
    public void Export_ConDatosMinimos_ProduceArchivoXlsx()
    {
        var exporter = new ExcelExportInfrastructure();
        exporter.Export(MinimalContext(), _outputPath);

        Assert.True(File.Exists(_outputPath), "El archivo xlsx no fue creado.");
        var bytes = File.ReadAllBytes(_outputPath);
        Assert.True(bytes.Length > 0, "El archivo xlsx está vacío.");
        // XLSX es un ZIP: empieza con PK (0x50 0x4B)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public void Export_ProyectoVacio_NoLanzaExcepcion()
    {
        var exporter = new ExcelExportInfrastructure();
        var ctx = new ExportProjectContext
        {
            Data = new ProjectFileData { Proyecto = new ProjectInfoRow() },
            ExportedBy = "Test"
        };

        var ex = Record.Exception(() => exporter.Export(ctx, _outputPath));
        Assert.Null(ex);
        Assert.True(File.Exists(_outputPath));
    }

    [Fact]
    public void Export_PartidaConObservacion_ApareceEnHojaItemizadoCliente()
    {
        var ctx = MinimalContext();
        ctx.Data.Itemizado.First(i => i.Id == "P001").Observaciones = "Verificar despeje en losa antes de instalar.";

        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        using var wb = new XLWorkbook(_outputPath);
        var ws = wb.Worksheets.First(w => w.Name == "02_ITEMIZADO_CLIENTE");
        var cellValues = ws.RangeUsed()!.RowsUsed()
            .SelectMany(r => r.Cells())
            .Select(c => c.GetString());
        Assert.Contains(cellValues, v => v.Contains("Verificar despeje en losa antes de instalar."));
    }

    [Fact]
    public void Export_RutaVacia_LanzaArgumentException()
    {
        var exporter = new ExcelExportInfrastructure();
        Assert.Throws<ArgumentException>(() => exporter.Export(MinimalContext(), ""));
    }

    [Fact]
    public void Export_ContextNull_LanzaArgumentNullException()
    {
        var exporter = new ExcelExportInfrastructure();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, _outputPath));
    }

    [Fact]
    public void Export_ItemizadoConTitulosYPartidas_ArchivoTieneContenido()
    {
        var ctx = MinimalContext();
        // Agregar más filas para forzar más rutas de código
        ctx.Data.Itemizado.Add(new ItemizadoRow { Id = "TIT002", Item = "2", Descripcion = "TABLEROS", Unidad = "", Cantidad = 0m, EsTitulo = true });
        ctx.Data.Itemizado.Add(new ItemizadoRow { Id = "P002", Item = "2.1", Descripcion = "Tablero 12 circuitos", Unidad = "UN", Cantidad = 2m });

        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        var info = new FileInfo(_outputPath);
        Assert.True(info.Length > 5000, $"Archivo demasiado pequeño ({info.Length} bytes): posiblemente vacío.");
    }

    [Fact]
    public void Export_HojaCosto_MatchesBudgetEngineTotals()
    {
        var ctx = MinimalContext();
        var expected = new BudgetCalculationService().CalculateBudget(
            ctx.Data.Cubicacion,
            BudgetSettings.FromProject(ctx.Data.Proyecto),
            ctx.Data.Apus,
            ctx.Data.Itemizado,
            ctx.Data.OrdenesCambio);

        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        using var wb = new XLWorkbook(_outputPath);
        var ws = wb.Worksheet("06_HOJA_COSTO");

        Assert.Equal(expected.MaterialesUf, ReadDecimal(ws.Cell(4, 2)), 4);
        Assert.Equal(expected.ManoObraUf, ReadDecimal(ws.Cell(5, 2)), 4);
        Assert.Equal(expected.CostoDirectoUf, ReadDecimal(ws.Cell(11, 2)), 4);
        Assert.Equal(expected.TotalVentaUf, ReadDecimal(ws.Cell(19, 2)), 4);
    }

    [Fact]
    public void Export_OfertaCliente_MatchesBudgetEngineNetAmount()
    {
        var ctx = MinimalContext();
        var expected = new BudgetCalculationService().CalculateBudget(
            ctx.Data.Cubicacion,
            BudgetSettings.FromProject(ctx.Data.Proyecto),
            ctx.Data.Apus,
            ctx.Data.Itemizado,
            ctx.Data.OrdenesCambio);

        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        using var wb = new XLWorkbook(_outputPath);
        var ws = wb.Worksheet("09_OFERTA_CLIENTE");

        decimal netAmount = ReadDecimal(ws.Cell(8, 2));
        // La oferta siempre muestra neto (sin IVA); el IVA se indica en línea separada.
        Assert.Equal(expected.TotalVentaNetoUf, netAmount, 4);
    }

    private static decimal ReadDecimal(IXLCell cell)
    {
        if (cell.TryGetValue(out decimal value))
            return value;
        if (cell.TryGetValue(out double d))
            return (decimal)d;
        return 0m;
    }

    // Regresión: Export() borraba el archivo destino ANTES de construir el libro nuevo. Si
    // algo fallaba a mitad de camino (ClosedXML, datos corruptos, budgetCalculation lanzando),
    // el usuario perdía el archivo anterior sin que se generara un reemplazo — quedaba sin
    // ningún archivo. Ahora el borrado ocurre recién después de que las 11 hojas se construyen
    // con éxito, justo antes de escribir.
    [Fact]
    public void Export_FallaAMitadDeCamino_PreservaArchivoAnterior()
    {
        File.WriteAllText(_outputPath, "contenido previo válido");

        var exporter = new ExcelExportInfrastructure(
            new ThrowingBudgetCalculationService(),
            new FinalReviewService(),
            new PriceTraceabilityService(),
            new CurrencyConversionService());

        Assert.Throws<InvalidOperationException>(() => exporter.Export(MinimalContext(), _outputPath));

        Assert.True(File.Exists(_outputPath), "El archivo anterior no debería desaparecer si la exportación falla.");
        Assert.Equal("contenido previo válido", File.ReadAllText(_outputPath));
    }

    [Fact]
    public void Export_ItemizadoCliente_ContieneFormulasVivas()
    {
        var ctx = MinimalContext();
        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        using var wb = new XLWorkbook(_outputPath);
        var ws = wb.Worksheet("02_ITEMIZADO_CLIENTE");

        // Fila 2 contiene parámetro dinámico de Valor UF en B2
        Assert.Equal(37000m, ReadDecimal(ws.Cell(2, 2)));

        // Fila 4 es el título del capítulo 1 y contiene fórmula de subtotal SUM(F5:F5)
        Assert.True(ws.Cell(4, 6).HasFormula);
        Assert.Equal("SUM(F5:F5)", ws.Cell(4, 6).FormulaA1);
        Assert.True(ws.Cell(4, 8).HasFormula);
        Assert.Equal("SUM(H5:H5)", ws.Cell(4, 8).FormulaA1);

        // Fila 5 es la partida 1.1 y contiene fórmulas activas
        Assert.True(ws.Cell(5, 6).HasFormula, "Total UF debe ser fórmula.");
        Assert.Equal("D5*E5", ws.Cell(5, 6).FormulaA1);

        Assert.True(ws.Cell(5, 7).HasFormula, "P.U. CLP debe ser fórmula referenciando B2.");
        Assert.Equal("E5*$B$2", ws.Cell(5, 7).FormulaA1);

        Assert.True(ws.Cell(5, 8).HasFormula, "Total CLP debe ser fórmula.");
        Assert.Equal("D5*G5", ws.Cell(5, 8).FormulaA1);

        // Fila 6 es Total Neto Presupuesto
        Assert.True(ws.Cell(6, 6).HasFormula);
        Assert.Equal("F4", ws.Cell(6, 6).FormulaA1);

        // Fila 7 es IVA 19%
        Assert.True(ws.Cell(7, 6).HasFormula);
        Assert.Equal("F6*0.19", ws.Cell(7, 6).FormulaA1);

        // Fila 8 es Total General Con IVA
        Assert.True(ws.Cell(8, 6).HasFormula);
        Assert.Equal("F6+F7", ws.Cell(8, 6).FormulaA1);
    }

    [Fact]
    public void Export_HojaCosto_ContieneFormulasVivasYReferenciaUf()
    {
        var ctx = MinimalContext();
        var exporter = new ExcelExportInfrastructure();
        exporter.Export(ctx, _outputPath);

        using var wb = new XLWorkbook(_outputPath);
        var ws = wb.Worksheet("06_HOJA_COSTO");

        // Fila 2 contiene parámetro B2
        Assert.Equal(37000m, ReadDecimal(ws.Cell(2, 2)));

        // Costo directo en fila 11 tiene fórmula viva SUM(B4:B10)
        Assert.True(ws.Cell(11, 2).HasFormula);
        Assert.Equal("SUM(B4:B10)", ws.Cell(11, 2).FormulaA1);

        // Columna 3 (CLP) tiene fórmulas vivas referenciando B2
        Assert.True(ws.Cell(4, 3).HasFormula);
        Assert.Equal("B4*$B$2", ws.Cell(4, 3).FormulaA1);

        Assert.True(ws.Cell(11, 3).HasFormula);
        Assert.Equal("B11*$B$2", ws.Cell(11, 3).FormulaA1);
    }

    private sealed class ThrowingBudgetCalculationService : IBudgetCalculationService
    {
        public void MarkDirtyItem(string? itemId) { }
        public void MarkDirtyItems(IEnumerable<string>? itemIds) { }
        public void ResetIncrementalCache() { }
        public CostBreakdown CalculateBudget(IEnumerable<CubicacionRow> cubicacion, BudgetSettings settings, IEnumerable<ApuRow>? apus = null, IEnumerable<ItemizadoRow>? itemizado = null, IEnumerable<ChangeOrderRow>? changeOrders = null)
            => throw new InvalidOperationException("Fallo simulado a mitad de la construcción del libro.");
        public (decimal Material, decimal ManoObra, decimal Total) GetProjectTotals(IEnumerable<CubicacionRow> cubicacion) => (0m, 0m, 0m);
        public (decimal Material, decimal ManoObra, decimal Total) GetDirectCostTotals(IEnumerable<CubicacionRow> rows) => (0m, 0m, 0m);
        public List<(string Area, decimal Material, decimal ManoObra, decimal Total)> GetAreaCostBreakdown(IEnumerable<CubicacionRow> cubicacion) => new();
        public (decimal Directo, decimal GastosGenerales, decimal Subtotal, decimal Utilidad, decimal Venta, decimal UfM2) GetCostBreakdown(IEnumerable<CubicacionRow> cubicacion, decimal gastosGeneralesPct, decimal utilidadPct, decimal metrosCuadrados) => (0m, 0m, 0m, 0m, 0m, 0m);
        public List<(string Area, decimal Total)> GetAreaTotals(IEnumerable<CubicacionRow> cubicacion) => new();
        public List<(string Codigo, string Descripcion, string Unidad, decimal Cantidad, decimal Total)> GetMaterialTotals(IEnumerable<CubicacionRow> cubicacion) => new();
    }
}
