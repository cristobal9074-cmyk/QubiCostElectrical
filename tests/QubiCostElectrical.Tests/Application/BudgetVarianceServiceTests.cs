using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetVarianceServiceTests
{
    private readonly BudgetVarianceService _service = new();

    [Fact]
    public void Analyze_EmptyData_ReturnsZeros()
    {
        var result = _service.Analyze(null, null, null, null);
        Assert.Equal(0m, result.BaseUf);
        Assert.Equal(0m, result.ActualUf);
        Assert.Contains("Controlado", result.Estado);
    }

    [Fact]
    public void Analyze_WithBaseVersion_ReturnsDeltas()
    {
        var items = new List<ItemizadoRow> { new() { Id = "P1", Item = "1.1", Descripcion = "Canalización" } };
        var actual = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 50m, MatUf = 0.35m }
        };
        var versions = new List<BudgetVersionRow>
        {
            new()
            {
                FechaCreacion = DateTime.Now.AddDays(-1),
                Snapshot = new BudgetVersionData
                {
                    Cubicacion = new List<CubicacionRow>
                    {
                        new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 40m, MatUf = 0.35m }
                    }
                }
            }
        };
        var result = _service.Analyze(items, actual, versions, null);
        Assert.NotEqual(0m, result.ActualUf);
        Assert.NotEqual(0m, result.DeltaActualVsBaseUf);
    }

    [Fact]
    public void Analyze_WithRealCosts_IncludesReal()
    {
        var items = new List<ItemizadoRow> { new() { Id = "P1", Item = "1.1", Descripcion = "Canalización" } };
        var actual = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 50m, MatUf = 0.35m }
        };
        var reals = new List<RealCostRow>
        {
            new() { ItemId = "P1", CostoRealUf = 20m }
        };
        var result = _service.Analyze(items, actual, null, reals);
        Assert.Equal(20m, result.RealUf);
    }

    [Fact]
    public void Analyze_AlertThreshold_FlagsAlerts()
    {
        var items = new List<ItemizadoRow> { new() { Id = "P1", Item = "1.1", Descripcion = "Canalización" } };
        var actual = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 100m, MatUf = 1m }
        };
        var versions = new List<BudgetVersionRow>
        {
            new()
            {
                FechaCreacion = DateTime.Now.AddDays(-1),
                Snapshot = new BudgetVersionData
                {
                    Cubicacion = new List<CubicacionRow>
                    {
                        new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 10m, MatUf = 1m }
                    }
                }
            }
        };
        var result = _service.Analyze(items, actual, versions, null, alertThresholdPct: 5m);
        Assert.Equal("Alerta", result.Estado);
    }

    [Fact]
    public void Analyze_MultipleItems_ProducesRows()
    {
        var items = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1.1", Descripcion = "Canalización" },
            new() { Id = "P2", Item = "1.2", Descripcion = "Conductores" }
        };
        var actual = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 50m, MatUf = 0.35m },
            new() { ItemId = "P2", Codigo = "THHN10", Cantidad = 100m, MatUf = 0.2m }
        };
        var result = _service.Analyze(items, actual, null, null);
        Assert.Equal(2, result.Rows.Count);
    }
}
