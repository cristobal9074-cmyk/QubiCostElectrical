using System.Linq;
using ClosedXML.Excel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Fase 5 de la política de moneda única: todos los exportadores (PDF y Excel) respetan la
/// moneda del proyecto. En CLP no puede aparecer ninguna columna, encabezado ni valor en UF;
/// en UF/USD/EUR sí se emite la UF junto al equivalente en pesos.
/// </summary>
public sealed class MonedaUnicaFase5Tests
{
    private static ProjectFileData ClpProject(decimal valorUf = 38000m)
        => new()
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto CLP",
                Cliente = "Constructora",
                MonedaPresupuesto = "CLP",
                ValorUfClp = valorUf
            }
        };

    private static ExportProjectContext Context(string moneda, decimal valorUf = 38000m)
    {
        var data = ClpProject(valorUf);
        data.Proyecto.MonedaPresupuesto = moneda;
        return new ExportProjectContext { Data = data };
    }

    private static string TempFile(string suffix)
        => Path.Combine(Path.GetTempPath(), $"qce-moneda-{Guid.NewGuid():N}{suffix}");

    private static void AssertNoVisibleUf(XLWorkbook wb, string sheetName)
    {
        var ws = wb.Worksheet(sheetName);
        foreach (var cell in ws.CellsUsed())
        {
            if (ws.Column(cell.Address.ColumnNumber).IsHidden)
                continue;

            if (cell.HasFormula)
                continue;

            if (cell.Value.IsText)
                Assert.DoesNotContain("UF", cell.GetString().ToUpperInvariant());
        }
    }

    // ── ExportProjectContext / ExportCurrencyPresenter ────────────────────────
    [Fact]
    public void Context_ClpProject_HidesUfAndNeverRequiresTheRate()
    {
        var ctx = Context("CLP");

        Assert.False(ctx.MuestraUf);
        Assert.Equal("$", ctx.Currency.EncabezadoPrincipal);
        Assert.Equal(1, ctx.Currency.ColumnasMoneda);
        Assert.Equal("", ctx.Currency.EncabezadoEquivalenteClp);
        Assert.Equal("$ 57.000", ctx.Currency.DesdeUf(1.5m));
        Assert.Equal("", ctx.Currency.EquivalenteClpDesdeUf(1.5m));
    }

    [Fact]
    public void Context_UfProject_EmitsUfPlusClpEquivalent()
    {
        var ctx = Context("UF");

        Assert.True(ctx.MuestraUf);
        Assert.Equal("UF", ctx.Currency.EncabezadoPrincipal);
        Assert.Equal(2, ctx.Currency.ColumnasMoneda);
        Assert.Equal("$", ctx.Currency.EncabezadoEquivalenteClp);
        Assert.NotEqual("", ctx.Currency.EquivalenteClpDesdeUf(1.5m));
    }

    [Fact]
    public void Context_NoCurrency_DefaultsToClp()
    {
        var data = ClpProject();
        data.Proyecto.MonedaPresupuesto = "";

        var ctx = new ExportProjectContext { Data = data };

        Assert.Equal("CLP", ctx.CurrencyLabel);
        Assert.False(ctx.MuestraUf);
    }

    // ── Resumen de variaciones (Versiones) ────────────────────────────────────
    [Fact]
    public void ComparisonSummary_DefaultsToClpAndMarksNoUf()
    {
        var summary = new BudgetVersionComparisonSummary();

        Assert.Equal("CLP", summary.MonedaPresupuesto);
        Assert.False(summary.MuestraUf);
    }

    [Fact]
    public void ComparisonSummary_UfProject_MarksUfVisible()
    {
        var summary = new BudgetVersionComparisonSummary { MonedaPresupuesto = "UF" };

        Assert.True(summary.MuestraUf);
    }

    // ── CubicacionReportExcelExporter ─────────────────────────────────────────
    [Fact]
    public void CubicacionExcel_ClpProject_ExportsPesosWithoutUfColumns()
    {
        var data = ClpProject(valorUf: 38000m);
        data.Itemizado.Add(new ItemizadoRow { Id = "I1", Item = "1", Descripcion = "Tablero", Unidad = "UN" });
        data.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "I1",
            Codigo = "MAT1",
            Descripcion = "Cable",
            Unidad = "M",
            Area = "Piso 1",
            Cantidad = 10m,
            MatUf = 1.5m
        });

        var ctx = new ExportProjectContext { Data = data };
        string path = TempFile(".xlsx");
        try
        {
            new CubicacionReportExcelExporter().Export(ctx, path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("MEMORIA_CUBICACION");

            Assert.Equal("UNIT $", ws.Cell(4, 11).GetString());
            Assert.Equal("TOTAL $", ws.Cell(4, 12).GetString());
            Assert.Equal(57000d, ws.Cell(6, 11).GetDouble(), 3);
            AssertNoVisibleUf(wb, "MEMORIA_CUBICACION");
            Assert.True(wb.Worksheet("RESUMEN_AREAS").Column(5).IsHidden);
            AssertNoVisibleUf(wb, "RESUMEN_AREAS");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CubicacionExcel_UfProject_KeepsUfColumns()
    {
        var ctx = Context("UF");
        string path = TempFile(".xlsx");
        try
        {
            new CubicacionReportExcelExporter().Export(ctx, path);

            using var wb = new XLWorkbook(path);
            Assert.Equal("UNIT UF", wb.Worksheet("MEMORIA_CUBICACION").Cell(4, 11).GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── PurchaseListExcelExporter ─────────────────────────────────────────────
    private static CompraMaterialViewModel CompraRow()
        => new(
            "MAT1", "Cable", "M", "Eléctrico", "Proveedor X",
            CantidadNeta: 10m, ToleranciaPct: 5m, CantidadCompra: 10.5m,
            MatUfUnitario: 1.5m, TotalNetoUf: 15m, TotalCompraUf: 15.75m, Lineas: 1,
            MatClpUnitario: 57000m, TotalNetoClp: 570000m, TotalCompraClp: 598500m);

    [Fact]
    public void PurchaseListExcel_ClpProject_ExportsPesosWithoutUf()
    {
        string path = TempFile(".xlsx");
        try
        {
            new PurchaseListExcelExporter().Export(path, new PurchaseListExcelRequest(
                "Proyecto CLP", 5m, new[] { CompraRow() }, MuestraUf: false, ValorUfClp: 38000m));

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("ListaCompra");

            Assert.Equal("PRECIO MAT $", ws.Cell(4, 9).GetString());
            Assert.Equal("TOTAL COMPRA $", ws.Cell(4, 11).GetString());
            Assert.Equal("TOTAL COMPRA $", ws.Cell(7, 10).GetString());
            Assert.Equal(57000d, ws.Cell(5, 9).GetDouble(), 3);
            AssertNoVisibleUf(wb, "ListaCompra");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PurchaseListExcel_UfProject_KeepsUfColumns()
    {
        string path = TempFile(".xlsx");
        try
        {
            new PurchaseListExcelExporter().Export(path, new PurchaseListExcelRequest(
                "Proyecto UF", 5m, new[] { CompraRow() }, MuestraUf: true, ValorUfClp: 38000m));

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("ListaCompra");

            Assert.Equal("PRECIO MAT UF", ws.Cell(4, 9).GetString());
            Assert.Equal(1.5d, ws.Cell(5, 9).GetDouble(), 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── BudgetRevisionDiff Excel / PDF ────────────────────────────────────────
    private static BudgetVersionComparisonSummary DiffSummary(string moneda)
        => new()
        {
            ProjectTitle = "Proyecto",
            ClientName = "Constructora",
            ContractorName = "Contratista",
            ValorUf = 38000m,
            MonedaPresupuesto = moneda,
            BaselineUf = 10m,
            CurrentUf = 12m,
            TotalAdicionalesUf = 3m,
            TotalDeductivosUf = 1m,
            Rows = new[]
            {
                new BudgetVersionComparisonRow("MAT1", "1", "Cable", 10m, 12m, 2m, 20m, "Modificado")
                {
                    Unidad = "M",
                    CantidadBase = 10m,
                    CantidadActual = 12m,
                    PuUfBase = 1m,
                    PuUfActual = 1m,
                    BaselineClp = 380000m,
                    CurrentClp = 456000m,
                    DiffKind = RevisionDiffKind.Modificado,
                    DisplayBadge = "MODIFICADO",
                    DetalleCambio = "Variación de precio / alcance"
                }
            }
        };

    [Fact]
    public void RevisionDiffExcel_ClpProject_HidesUfColumns()
    {
        string path = TempFile(".xlsx");
        try
        {
            new BudgetRevisionDiffExcelExporter().Export(DiffSummary("CLP"), path);

            using var wb = new XLWorkbook(path);
            var ws1 = wb.Worksheet("01_RESUMEN_EJECUTIVO");
            var ws2 = wb.Worksheet("02_DETALLE_VARIACIONES");
            var ws3 = wb.Worksheet("03_SOLICITUD_ADICIONALES");

            Assert.Equal("FACTOR INTERNO:", ws1.Cell(2, 1).GetString());
            Assert.Equal("MONTO $", ws1.Cell(5, 3).GetString());
            Assert.True(ws1.Column(3).IsHidden);
            Assert.True(ws2.Column(13).IsHidden);
            Assert.True(ws3.Column(7).IsHidden);
            Assert.Equal("P.U. BASE $", ws2.Cell(3, 7).GetString());
            Assert.Equal("P.U. $", ws3.Cell(4, 5).GetString());

            AssertNoVisibleUf(wb, "01_RESUMEN_EJECUTIVO");
            AssertNoVisibleUf(wb, "02_DETALLE_VARIACIONES");
            AssertNoVisibleUf(wb, "03_SOLICITUD_ADICIONALES");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RevisionDiffExcel_UfProject_KeepsUfColumns()
    {
        string path = TempFile(".xlsx");
        try
        {
            new BudgetRevisionDiffExcelExporter().Export(DiffSummary("UF"), path);

            using var wb = new XLWorkbook(path);
            var ws1 = wb.Worksheet("01_RESUMEN_EJECUTIVO");
            var ws2 = wb.Worksheet("02_DETALLE_VARIACIONES");
            var ws3 = wb.Worksheet("03_SOLICITUD_ADICIONALES");

            Assert.Equal("VALOR UF REF:", ws1.Cell(2, 1).GetString());
            Assert.Equal("MONTO EN UF", ws1.Cell(5, 3).GetString());
            Assert.False(ws1.Column(3).IsHidden);
            Assert.Equal("P.U. BASE UF", ws2.Cell(3, 7).GetString());
            Assert.False(ws2.Column(13).IsHidden);
            Assert.False(ws3.Column(7).IsHidden);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RevisionDiffPdf_ClpProject_GeneratesFileWithoutUfReference()
    {
        string path = TempFile(".pdf");
        try
        {
            new BudgetRevisionDiffPdfExporter().Export(DiffSummary("CLP"), path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
