using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PdfPageRendererTests : IDisposable
{
    private readonly string letterPath;

    public PdfPageRendererTests()
    {
        letterPath = Path.Combine(Path.GetTempPath(), $"render_letter_{Guid.NewGuid():N}.pdf");
        var exporter = new QuestPdfOfferLetterExporter(new BudgetCalculationService(), new CurrencyConversionService());
        exporter.Export(Context(), letterPath);
    }

    public void Dispose()
    {
        if (File.Exists(letterPath)) File.Delete(letterPath);
    }

    [Fact]
    public async Task RenderPageAsync_Letter_ReturnsPageCountAndImage()
    {
        var result = await PdfPageRenderer.RenderPageAsync(letterPath, 0);

        Assert.InRange(result.PageCount, 1, 6);
        Assert.NotNull(result.Image);
    }

    [Fact]
    public void RenderPage_SyncLetter_ReturnsImage()
    {
        Assert.NotNull(PdfPageRenderer.RenderPage(letterPath, 0));
    }

    [Fact]
    public async Task RenderPageAsync_PageOutOfRange_ReturnsNullImage()
    {
        var result = await PdfPageRenderer.RenderPageAsync(letterPath, 99);

        Assert.Null(result.Image);
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
}
