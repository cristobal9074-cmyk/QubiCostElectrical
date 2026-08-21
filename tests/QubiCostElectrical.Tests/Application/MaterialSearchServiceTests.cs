using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class MaterialSearchServiceTests
{
    private readonly MaterialSearchService _service = new();

    private static MaterialRow Mat(string codigo, string desc, string categoria = "", string subcategoria = "",
        string tipoInstalacion = "", string unidad = "UN", string marca = "")
        => new() { Codigo = codigo, Descripcion = desc, Categoria = categoria, Subcategoria = subcategoria,
            TipoInstalacion = tipoInstalacion, Unidad = unidad, Marca = marca, MatUf = 1m };

    [Fact]
    public void Normalize_RemovesDiacritics()
    {
        Assert.Equal("CONDUCTOR", _service.Normalize("Conductor"));
        Assert.Equal("CABLE", _service.Normalize("Cable"));
        Assert.Contains("THHN10", _service.Normalize("THHN 10 mm²"));
    }

    [Fact]
    public void Normalize_StripsSpecialChars()
    {
        Assert.Equal("PVC20", _service.Normalize("PVC-20"));
        Assert.Equal("EMT34", _service.Normalize("EMT 3/4"));
        Assert.DoesNotContain("/", _service.Normalize("1/2\"x2"));
    }

    [Fact]
    public void Normalize_PipeKeepsFirstPart()
    {
        Assert.Equal("THHN10", _service.Normalize("THHN 10 | Cable THHN 10 AWG"));
    }

    [Fact]
    public void BuildIndex_ProducesEntries()
    {
        var mats = new[] { Mat("THHN10", "Cable THHN 10 AWG") };
        var index = _service.BuildIndex(mats);
        Assert.Single(index);
        Assert.Equal("THHN10", index[0].CodigoKey);
        Assert.Contains("THHN10", index[0].SearchKey);
    }

    [Fact]
    public void FindMatches_ExactCode_ReturnsFirst()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm"), Mat("PVC25", "PVC 25mm") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("PVC20", mats, index);
        Assert.Contains(result, m => m.Codigo == "PVC20");
    }

    [Fact]
    public void FindMatches_PrefixCode_Matches()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm"), Mat("PVC25", "PVC 25mm"), Mat("EMT20", "EMT 20mm") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("PVC", mats, index);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindMatches_DescriptionSearch()
    {
        var mats = new[] { Mat("CAB25", "Cable 2.5mm²"), Mat("CAB4", "Cable 4mm²") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("Cable", mats, index);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindMatches_NoMatch_ReturnsEmpty()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("NOEXISTE", mats, index);
        Assert.Empty(result);
    }

    [Fact]
    public void FindMatches_EmptySearch_ReturnsAll()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm"), Mat("THHN10", "THHN 10 AWG") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("", mats, index);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindMatches_WithFilter_FiltersByCategory()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm", categoria: "Canalización"), Mat("THHN10", "THHN 10 AWG", categoria: "Conductor") };
        var index = _service.BuildIndex(mats);
        var filter = new MaterialCatalogFilter { Categoria = "Canalización" };
        var result = _service.FindMatches("", mats, index, filter);
        Assert.Single(result);
    }

    [Fact]
    public void MatchesFilter_ExactCategory_ReturnsTrue()
    {
        var mat = Mat("PVC20", "PVC 20mm", categoria: "Canalización");
        Assert.True(_service.MatchesFilter(mat, new MaterialCatalogFilter { Categoria = "Canalización" }));
    }

    [Fact]
    public void MatchesFilter_EmptyFilter_ReturnsTrue()
    {
        var mat = Mat("PVC20", "PVC 20mm");
        Assert.True(_service.MatchesFilter(mat, new MaterialCatalogFilter()));
    }

    [Fact]
    public void MatchesFilter_NullFilter_ReturnsTrue()
    {
        var mat = Mat("PVC20", "PVC 20mm");
        Assert.True(_service.MatchesFilter(mat, null));
    }

    [Fact]
    public void MatchesFilter_WrongCategory_ReturnsFalse()
    {
        var mat = Mat("PVC20", "PVC 20mm", categoria: "Canalización");
        Assert.False(_service.MatchesFilter(mat, new MaterialCatalogFilter { Categoria = "Conductor" }));
    }

    [Fact]
    public void GetHierarchy_JoinsCategories()
    {
        var mat = Mat("PVC20", "PVC 20mm", categoria: "Canalización", subcategoria: "PVC");
        Assert.Equal("Canalización → PVC", _service.GetHierarchy(mat));
    }

    [Fact]
    public void BuildQuickFilters_GroupsByCategory()
    {
        var mats = new[]
        {
            Mat("PVC20", "PVC 20mm", categoria: "Canalización"),
            Mat("PVC25", "PVC 25mm", categoria: "Canalización"),
            Mat("THHN10", "THHN 10 AWG", categoria: "Conductor")
        };
        var filters = _service.BuildQuickFilters(mats);
        Assert.NotEmpty(filters);
        Assert.Contains(filters, f => f.Filter.Categoria == "Canalización");
    }

    [Fact]
    public void FindMatches_Limit_Respects()
    {
        var mats = Enumerable.Range(1, 10).Select(i => Mat($"PVC{i:D2}", $"PVC {i}mm")).ToList();
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("PVC", mats, index, limit: 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FindMatches_MultiToken_Intersects()
    {
        var mats = new[] { Mat("PVC20", "PVC 20mm Conduit"), Mat("THHN10", "THHN 10 AWG") };
        var index = _service.BuildIndex(mats);
        var result = _service.FindMatches("PVC Conduit", mats, index);
        Assert.Single(result);
    }

    [Fact]
    public void IsEmpty_AllEmpty_True()
    {
        Assert.True(new MaterialCatalogFilter().IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithCategory_False()
    {
        Assert.False(new MaterialCatalogFilter { Categoria = "Canalización" }.IsEmpty);
    }
}
