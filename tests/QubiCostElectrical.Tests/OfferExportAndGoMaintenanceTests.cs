using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class OfferPackagePresetCatalogTests
{
    [Fact]
    public void Mandante_SoloOfertaYCarta()
    {
        var ids = OfferPackagePresetCatalog.ResolveDocumentIds(OfferPackagePresetCatalog.Mandante);
        Assert.NotNull(ids);
        Assert.Equal(new[] { "OfferLetter", "ResumenExcel" }, ids);
    }

    [Fact]
    public void Interno_SeleccionaTodos()
    {
        Assert.Null(OfferPackagePresetCatalog.ResolveDocumentIds(OfferPackagePresetCatalog.Interno));
    }

    [Fact]
    public void Banco_OfertaYControl()
    {
        var ids = OfferPackagePresetCatalog.ResolveDocumentIds(OfferPackagePresetCatalog.Banco);
        Assert.Equal(new[] { "OfferLetter", "ResumenExcel", "ControlExcel" }, ids);
    }

    [Fact]
    public void Desconocido_Vacio()
    {
        Assert.Empty(OfferPackagePresetCatalog.ResolveDocumentIds("Otro")!);
        Assert.Empty(OfferPackagePresetCatalog.ResolveDocumentIds(null)!);
    }
}

public sealed class ExportEligibilityEvaluatorTests
{
    [Fact]
    public void Allowed_CuandoProyectoYDocs()
    {
        var e = ExportEligibilityEvaluator.Evaluate(true, true, false, 0, false, true);
        Assert.Equal(ExportEligibilityStatus.Allowed, e.Status);
        Assert.True(e.CanExportActions);
        Assert.True(e.CanDeliverPackage);
    }

    [Fact]
    public void Busy_PermiteAccionesPeroNoEntrega()
    {
        var e = ExportEligibilityEvaluator.Evaluate(true, true, false, 0, true, true);
        Assert.Equal(ExportEligibilityStatus.Busy, e.Status);
        Assert.True(e.CanExportActions);
        Assert.False(e.CanDeliverPackage);
    }

    [Fact]
    public void Blockers_BloqueaTodo()
    {
        var e = ExportEligibilityEvaluator.Evaluate(true, true, false, 2, false, true);
        Assert.Equal(ExportEligibilityStatus.HasBlockers, e.Status);
        Assert.False(e.CanExportActions);
        Assert.False(e.CanDeliverPackage);
    }
}

public sealed class GastoOperacionalCategoryMaintenanceTests
{
    [Fact]
    public void EnsureTrasladoBus_AgregaSiFalta()
    {
        var cats = new List<GastoOperacionalRow>
        {
            new()
            {
                Codigo = "3.3",
                Concepto = "Movilización y viáticos",
                Items = new List<GastoOperacionalItemRow>
                {
                    new() { Concepto = "Pasajes MO directa", Unidad = "día" }
                }
            }
        };

        Assert.True(GastoOperacionalCategoryMaintenance.EnsureTrasladoBusItem(cats));
        Assert.Contains(cats[0].Items, i => i.Concepto == GastoOperacionalCategoryMaintenance.TrasladoBusConcepto);
        Assert.False(GastoOperacionalCategoryMaintenance.EnsureTrasladoBusItem(cats));
    }
}
