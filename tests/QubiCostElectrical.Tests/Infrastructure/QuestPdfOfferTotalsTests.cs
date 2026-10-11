using System.Collections.Generic;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class QuestPdfOfferTotalsTests : IDisposable
{
    private readonly string outputPath;

    public QuestPdfOfferTotalsTests()
    {
        outputPath = Path.Combine(Path.GetTempPath(), $"pe_pdf_offer_{Guid.NewGuid():N}.pdf");
    }

    public void Dispose()
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private static ExportProjectContext MinimalContext()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto Test PDF",
                Cliente = "Cliente Test",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 37000m
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "TIT001", Item = "1", Descripcion = "INSTALACIONES ELÉCTRICAS", Unidad = "", Cantidad = 0m, EsTitulo = true },
                new() { Id = "P001", Item = "1.1", Descripcion = "Canalización PVC 20mm", Unidad = "ML", Cantidad = 50m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C001", ItemId = "P001", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Cantidad = 50m, MatUf = 0.12m, MoUf = 0m },
                new() { Id = "C002", ItemId = "P001", Tipo = "MO", Codigo = "MO001", Descripcion = "Instalación PVC", Unidad = "ML", Cantidad = 50m, MatUf = 0m, MoUf = 0.08m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Categoria = "Tubería", MatUf = 0.12m },
                new() { Codigo = "MO001", Descripcion = "Instalación PVC", Unidad = "ML", Categoria = "MO", MoUf = 0.08m }
            }
        };

        var budget = new BudgetCalculationService().CalculateBudget(
            data.Cubicacion,
            BudgetSettings.FromProject(data.Proyecto),
            data.Apus,
            data.Itemizado,
            data.OrdenesCambio);

        return new ExportProjectContext
        {
            Data = data,
            ExportedBy = "Test",
            OfferNetAmountUf = budget.TotalVentaNetoUf
        };
    }

    [Fact]
    public void Export_MinimalContext_ProducesNonEmptyPdf()
    {
        var ctx = MinimalContext();
        var exporter = new QuestPdfProjectExporter(
            new BudgetCalculationService(),
            new CurrencyConversionService());

        exporter.Export(ctx, outputPath);

        Assert.True(File.Exists(outputPath));
        var bytes = File.ReadAllBytes(outputPath);
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x25, bytes[0]); // %
        Assert.Equal(0x50, bytes[1]); // P
        Assert.Equal(0x44, bytes[2]); // D
        Assert.Equal(0x46, bytes[3]); // F
    }

    [Fact]
    public void Export_OfferNetAmount_MatchesBudgetEngine()
    {
        var ctx = MinimalContext();
        var expected = new BudgetCalculationService().CalculateBudget(
            ctx.Cubicacion,
            ctx.BudgetSettings,
            ctx.Apus,
            ctx.Itemizado,
            ctx.OrdenesCambio);

        Assert.Equal(expected.TotalVentaNetoUf, ctx.OfferNetAmountUf, 4);

        var exporter = new QuestPdfProjectExporter(
            new BudgetCalculationService(),
            new CurrencyConversionService());

        var ex = Record.Exception(() => exporter.Export(ctx, outputPath));
        Assert.Null(ex);
        Assert.True(new FileInfo(outputPath).Length > 1000);
    }

    [Fact]
    public void Export_FullExecutiveDossier_GeneratesCompletePdf()
    {
        var ctx = MinimalContext();
        ctx.ReportTemplate.ShowCover = true;
        ctx.ReportTemplate.ShowExecutiveSummary = true;
        ctx.ReportTemplate.ShowChapterDistributionChart = true;
        ctx.ReportTemplate.ShowTermsAndConditions = true;
        ctx.ReportTemplate.ShowNormativeAssumptions = true;
        ctx.ReportTemplate.ShowItemizedDetail = true;

        ctx.Data.Proyecto.OfertaPdf = new OfferPdfSettings
        {
            CompanyName = "Electro Ingenieros SpA",
            CompanyRut = "76.123.456-7",
            CompanyAddress = "Av. Providencia 1234, Of. 601",
            CompanyPhone = "+56 9 8765 4321",
            CompanyEmail = "contacto@electroingenieros.cl",
            RecipientName = "Ing. Roberto Muñoz",
            RecipientCompany = "Constructora del Valle SA",
            OfferNumber = "OFE-2026-0042",
            RevisionTag = "Rev 0",
            ProjectDuration = "120 días corridos",
            ValidityDays = 30,
            IncludePresentationLetter = true,
            PresentationLetterTitle = "CARTA FORMAL DE PRESENTACIÓN",
            PresentationLetterText = "Estimados señores: Presentamos nuestra mejor oferta técnica y económica para las instalaciones eléctricas.",
            NormativeAssumptionsText = "La oferta cumple con el DS N° 8/2019 y los Pliegos Técnicos Normativos RIC (01 al 19) de la SEC. Se incluye tramitación TE1.",
            ScopeText = "Canalizaciones, alimentadores, conductores y tableros.",
            ExclusionsText = "Obras civiles, derechos municipales y transformador.",
            CommercialConditionsText = "30% anticipo, estados de pago mensuales y 5% de retención.",
            SignatureName = "Carlos Contreras Silva",
            SignatureRole = "Gerente de Operaciones",
            SignatureSecClass = "Clase A",
            SignatureRegistroSec = "SEC-84920"
        };

        var exporter = new QuestPdfProjectExporter(
            new BudgetCalculationService(),
            new CurrencyConversionService());

        exporter.Export(ctx, outputPath);

        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 5000, "El dossier ejecutivo PDF debe contener páginas estructuradas completas.");
    }
}
