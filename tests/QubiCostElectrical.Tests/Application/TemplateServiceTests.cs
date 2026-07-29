using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class TemplateServiceTests
{
    private readonly TemplateService _service = new();

    private static TemplateRow Tpl(string nombre, params TemplateItemRow[] items)
        => new() { Nombre = nombre, Items = new List<TemplateItemRow>(items) };

    private static TemplateItemRow MatItem(string codigo, decimal factor = 1m, decimal minimo = 0m, string tipo = "MAT")
        => new() { Clase = "MATERIAL", Codigo = codigo, Factor = factor, Minimo = minimo, Tipo = tipo };

    private static TemplateItemRow SubTpl(string nombre, decimal factor = 1m)
        => new() { Clase = "PLANTILLA", Codigo = nombre, SubPlantilla = nombre, Factor = factor };

    private static MaterialRow Mat(string codigo, string desc, string unidad = "UN", decimal matUf = 1m, decimal moUf = 0m)
        => new() { Codigo = codigo, Descripcion = desc, Unidad = unidad, MatUf = matUf, MoUf = moUf };

    private static ItemizadoRow Partida(string id, decimal cantidad = 1m)
        => new() { Id = id, Item = id, Descripcion = id, Cantidad = cantidad };

    [Fact]
    public void BuildCubicacionRowsFromTemplate_SimpleTemplate_ProducesRows()
    {
        var template = Tpl("Canalizacion", MatItem("PVC20", 1m));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 50m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Single(result.Rows);
        Assert.Equal("PVC20", result.Rows[0].Codigo);
        Assert.Equal(50m, result.Rows[0].Cantidad);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_WithFactor_ScalesQuantity()
    {
        var template = Tpl("Canalizacion", MatItem("PVC20", 2.5m));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Equal(25m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_WithMinimo_ClampsQuantity()
    {
        var template = Tpl("Canalizacion", MatItem("PVC20", 1m, minimo: 100m));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 5m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Equal(100m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_MaterialNotFound_AddsWarning()
    {
        var template = Tpl("Canalizacion", MatItem("INEXISTENTE", 1m));
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), Enumerable.Empty<MaterialRow>());

        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("INEXISTENTE", result.Warnings[0]);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_NullTemplate_ReturnsEmpty()
    {
        var result = _service.BuildCubicacionRowsFromTemplate(null!, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), Enumerable.Empty<MaterialRow>());

        Assert.Empty(result.Rows);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_SubTemplate_ExpandsRecursively()
    {
        var sub = Tpl("Base", MatItem("CABLE25", 1m));
        var parent = Tpl("Principal", SubTpl("Base", 1m), MatItem("PVC20", 1m));
        var materiales = new[]
        {
            Mat("CABLE25", "Cable 2.5mm²", "ML", 0.5m),
            Mat("PVC20", "PVC 20mm", "ML", 0.35m)
        };
        var result = _service.BuildCubicacionRowsFromTemplate(parent, Partida("P1", 30m),
            new[] { sub }, materiales);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Codigo == "PVC20");
        Assert.Contains(result.Rows, r => r.Codigo == "CABLE25");
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_CycleDetection_AddsWarning()
    {
        var templateA = Tpl("A", SubTpl("B"));
        var templateB = Tpl("B", SubTpl("A"));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(templateA, Partida("P1", 10m),
            new[] { templateA, templateB }, materiales);

        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Ciclo", result.Warnings[0]);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_SubTemplateNotFound_AddsWarning()
    {
        var template = Tpl("Principal", SubTpl("Inexistente"));
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), Enumerable.Empty<MaterialRow>());

        Assert.NotEmpty(result.Warnings);
        Assert.Contains("no encontrada", result.Warnings[0]);
    }

    [Fact]
    public void MergeTemplateRows_GroupsDuplicateCodes()
    {
        var rows = new List<CubicacionRow>
        {
            new() { Codigo = "PVC20", Tipo = "MAT", Unidad = "ML", Area = "General",
                Cantidad = 30m, MatUf = 0.35m, MoUf = 0m, Descripcion = "PVC 20mm", ItemId = "P1" },
            new() { Codigo = "PVC20", Tipo = "MAT", Unidad = "ML", Area = "General",
                Cantidad = 20m, MatUf = 0.35m, MoUf = 0m, Descripcion = "PVC 20mm", ItemId = "P1" }
        };
        var merged = _service.MergeTemplateRows(rows);

        Assert.Single(merged);
        Assert.Equal(50m, merged[0].Cantidad);
    }

    [Fact]
    public void MergeTemplateRows_SkipsEmptyCode()
    {
        var rows = new List<CubicacionRow>
        {
            new() { Codigo = "", Tipo = "MAT", Unidad = "UN", Area = "General",
                Cantidad = 10m, MatUf = 1m, MoUf = 0m, Descripcion = "Test", ItemId = "P1" }
        };
        var merged = _service.MergeTemplateRows(rows);

        Assert.Empty(merged);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_WithMultipleItems_ProducesAll()
    {
        var template = Tpl("Canalizacion",
            MatItem("PVC20", 1m),
            MatItem("CABLE25", 1m));
        var materiales = new[]
        {
            Mat("PVC20", "PVC 20mm", "ML", 0.35m),
            Mat("CABLE25", "Cable 2.5mm²", "ML", 0.5m)
        };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 40m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_NegativeFactor_DefaultsToOne()
    {
        var template = Tpl("Test", MatItem("PVC20", -1m));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Equal(10m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void BuildCubicacionRowsFromTemplate_ZeroFactor_DefaultsToOne()
    {
        var template = Tpl("Test", MatItem("PVC20", 0m));
        var materiales = new[] { Mat("PVC20", "PVC 20mm", "ML", 0.35m) };
        var result = _service.BuildCubicacionRowsFromTemplate(template, Partida("P1", 10m),
            Enumerable.Empty<TemplateRow>(), materiales);

        Assert.Equal(10m, result.Rows[0].Cantidad);
    }
}
