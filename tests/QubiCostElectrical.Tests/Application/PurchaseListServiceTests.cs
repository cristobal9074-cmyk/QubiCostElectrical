using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class PurchaseListServiceTests
{
    private readonly PurchaseListService _service = new();

    private static CubicacionRow Cub(string codigo, decimal cantidad, decimal matUf, string tipo = "MAT")
        => new() { Codigo = codigo, Cantidad = cantidad, MatUf = matUf, Tipo = tipo, Descripcion = codigo };

    private static MaterialRow Mat(string codigo, string desc, decimal matUf = 1m, string equivalencia = "")
        => new() { Codigo = codigo, Descripcion = desc, Unidad = "UN", MatUf = matUf, EquivalenciaKey = equivalencia };

    [Fact]
    public void Build_AggregatesByCode()
    {
        var cub = new List<CubicacionRow>
        {
            Cub("PVC20", 30m, 0.35m),
            Cub("PVC20", 20m, 0.35m)
        };
        var mats = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var result = _service.Build(cub, mats, 0m);

        Assert.Single(result);
        Assert.Equal(50m, result[0].CantidadNeta);
    }

    [Fact]
    public void Build_FiltersNonPurchasable()
    {
        var cub = new List<CubicacionRow>
        {
            Cub("PVC20", 50m, 0.35m),
            Cub("MO01", 10m, 0m, tipo: "MO")
        };
        var mats = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var result = _service.Build(cub, mats, 0m);

        Assert.Single(result);
    }

    [Fact]
    public void Build_EmptyCubicacion_EmptyResult()
    {
        var result = _service.Build(Enumerable.Empty<CubicacionRow>(), Enumerable.Empty<MaterialRow>(), 0m);
        Assert.Empty(result);
    }

    [Fact]
    public void Build_WithTolerance_ScalesQuantity()
    {
        var cub = new List<CubicacionRow> { Cub("PVC20", 100m, 1m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm", 1m) };
        var result = _service.Build(cub, mats, 10m);

        Assert.Equal(110m, result[0].CantidadCompra);
    }

    [Fact]
    public void Build_ToleranceCapped_At100()
    {
        var cub = new List<CubicacionRow> { Cub("PVC20", 100m, 1m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm", 1m) };
        var result = _service.Build(cub, mats, 200m);

        Assert.Equal(200m, result[0].CantidadCompra);
    }

    [Fact]
    public void Build_ResolvesEquivalenciaKey()
    {
        var cub = new List<CubicacionRow> { Cub("PVC20", 50m, 0.35m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm", 0.35m, equivalencia: "PVC-GEN") };
        var result = _service.Build(cub, mats, 0m);

        Assert.Single(result);
        Assert.Equal("PVC-GEN", result[0].Codigo);
    }

    [Fact]
    public void Build_OrdersByTotalDesc()
    {
        var cub = new List<CubicacionRow>
        {
            Cub("THHN10", 100m, 0.5m),
            Cub("PVC20", 50m, 1m)
        };
        var mats = new[]
        {
            Mat("THHN10", "THHN 10 AWG", 0.5m),
            Mat("PVC20", "PVC 20mm", 1m)
        };
        var result = _service.Build(cub, mats, 0m);

        Assert.Contains(result, r => r.Codigo == "THHN10");
        Assert.Contains(result, r => r.Codigo == "PVC20");
    }

    [Fact]
    public void IsPurchasable_FiltersCorrectly()
    {
        var result = _service.Build(new List<CubicacionRow>(), Enumerable.Empty<MaterialRow>(), 0m);
        Assert.Empty(result);
    }
}
