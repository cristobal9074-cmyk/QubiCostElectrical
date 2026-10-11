using System.Collections.Generic;
using System.IO;
using System.Text;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class QuestPdfOfferLetterTests : IDisposable
{
    private readonly string outputPath;

    public QuestPdfOfferLetterTests()
    {
        outputPath = Path.Combine(Path.GetTempPath(), $"pe_letter_{Guid.NewGuid():N}.pdf");
    }

    public void Dispose()
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    private static ExportProjectContext Context()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto Carta",
                Cliente = "Cliente Carta",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 37000m,
                OfertaPdf = new OfferPdfSettings
                {
                    IncludeDrawingList = true,
                    IncludeEquipmentBrands = true,
                    Planos = new List<OfferPlanReferenceRow>
                    {
                        new() { Codigo = "E-01", Revision = "Rev 1", Descripcion = "Diagrama unilineal", Disciplina = "Electricidad" },
                        new() { Codigo = "CD-01", Revision = "Rev 0", Descripcion = "Puntos corrientes debiles", Disciplina = "Corrientes Debiles" }
                    },
                    Productos = new List<OfferProductSpecificationRow>
                    {
                        new() { Categoria = "Tableros", Marca = "BETEL", Especificacion = "o equivalente" }
                    }
                }
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "P001", Item = "1.1", Descripcion = "Canalizacion PVC 20mm", Unidad = "ML", Cantidad = 50m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C001", ItemId = "P001", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Cantidad = 50m, MatUf = 0.12m, MoUf = 0m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Categoria = "Tuberia", MatUf = 0.12m }
            }
        };

        var budget = new BudgetCalculationService().CalculateBudget(
            data.Cubicacion, BudgetSettings.FromProject(data.Proyecto), data.Apus, data.Itemizado, data.OrdenesCambio);

        return new ExportProjectContext { Data = data, ExportedBy = "Test", OfferNetAmountUf = budget.TotalVentaNetoUf };
    }

    private static int CountPages(byte[] bytes)
    {
        // QuestPDF no comprime los objetos de pagina; contamos "/Type /Page" y "/Type/Page".
        string latin = Encoding.Latin1.GetString(bytes);
        int a = System.Text.RegularExpressions.Regex.Matches(latin, @"/Type\s*/Page\b").Count;
        return a;
    }

    [Fact]
    public void OfferLetter_GeneratesCompactLetter_NotFullOffer()
    {
        var ctx = Context();
        var exporter = new QuestPdfOfferLetterExporter(new BudgetCalculationService(), new CurrencyConversionService());
        exporter.Export(ctx, outputPath);

        Assert.True(File.Exists(outputPath));
        var bytes = File.ReadAllBytes(outputPath);
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'

        int pages = CountPages(bytes);
        Assert.InRange(pages, 1, 6); // carta compacta, nunca 19 hojas
    }

    [Fact]
    public void OfferLetter_WithoutDrawingList_StillGenerates()
    {
        var ctx = Context();
        ctx.Data.Proyecto.OfertaPdf.IncludeDrawingList = false;
        var exporter = new QuestPdfOfferLetterExporter(new BudgetCalculationService(), new CurrencyConversionService());
        exporter.Export(ctx, outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public void OfferLetter_WithoutBrands_StillGenerates()
    {
        var ctx = Context();
        ctx.Data.Proyecto.OfertaPdf.IncludeEquipmentBrands = false;
        var exporter = new QuestPdfOfferLetterExporter(new BudgetCalculationService(), new CurrencyConversionService());
        exporter.Export(ctx, outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }
}
