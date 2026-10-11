using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class OfferLetterBrandsTests
{
    [Fact]
    public void OfferPdfSettings_Defaults_EnableDrawingListAndBrands()
    {
        var s = new OfferPdfSettings();
        Assert.True(s.IncludeDrawingList);
        Assert.True(s.IncludeEquipmentBrands);
        Assert.False(string.IsNullOrWhiteSpace(s.EquipmentBrandsText));
    }

    [Fact]
    public void OfferPdfSettings_EquipmentBrandsText_HasWordLikeCategories()
    {
        var s = new OfferPdfSettings();
        Assert.Contains("Tableros", s.EquipmentBrandsText);
        Assert.Contains("Artefactos", s.EquipmentBrandsText);
    }

    [Fact]
    public void OfferProductSpecificationRow_Defaults_AreEmpty()
    {
        var row = new OfferProductSpecificationRow();
        Assert.Equal("", row.Categoria);
        Assert.Equal("", row.Marca);
        Assert.Equal("", row.Especificacion);
    }

    [Fact]
    public void OfferPdfSettings_Productos_CanHoldBrandRows()
    {
        var s = new OfferPdfSettings();
        s.Productos.Add(new OfferProductSpecificationRow
        {
            Categoria = "Tableros Electricos",
            Marca = "BETEL",
            Especificacion = "o equivalente tecnico"
        });

        Assert.Single(s.Productos);
        Assert.Equal("BETEL", s.Productos[0].Marca);
    }
}
