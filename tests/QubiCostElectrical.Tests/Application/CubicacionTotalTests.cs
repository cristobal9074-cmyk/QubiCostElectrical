using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Verifica que el cálculo de totales de cubicación con materiales y mano de obra
/// produce resultados correctos end-to-end usando BudgetCalculationService.
/// Magnitudes en MatClp/MoClp (fuente CLP del motor); MatUf/MoUf se mantienen alineados
/// solo por compatibilidad de fixtures.
/// </summary>
public sealed class CubicacionTotalTests
{
    private readonly BudgetCalculationService _service = new();

    [Fact]
    public void Calculate_MaterialesYManoObra_SumanCostoDirectoCorrectamente()
    {
        // 50 ML: MAT 0.35 + MO 0.15 → 50 × 0.50 = 25
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MAT", Codigo = "PVC20", Descripcion = "Canalización PVC 20mm", Unidad = "ML", Cantidad = 50m, MatClp = 0.35m, MoClp = 0m, MatUf = 0.35m, MoUf = 0m },
            new() { ItemId = "P001", Tipo = "MO",  Codigo = "MO01",  Descripcion = "Instalación canalización", Unidad = "ML", Cantidad = 50m, MatClp = 0m, MoClp = 0.15m, MatUf = 0m, MoUf = 0.15m }
        };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);

        var result = _service.CalculateBudget(rows, settings);

        Assert.Equal(25m, result.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_SoloMateriales_IgnoraMoEnCero()
    {
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MAT", Codigo = "CAB25", Descripcion = "Cable 2.5mm²", Unidad = "ML", Cantidad = 100m, MatClp = 0.30m, MoClp = 0m, MatUf = 0.30m, MoUf = 0m }
        };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);

        var result = _service.CalculateBudget(rows, settings);

        Assert.Equal(30m, result.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_SoloManoObra_IgnoraMatEnCero()
    {
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MO", Codigo = "MOE01", Descripcion = "Electricista", Unidad = "HH", Cantidad = 20m, MatClp = 0m, MoClp = 0.08m, MatUf = 0m, MoUf = 0.08m }
        };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);

        var result = _service.CalculateBudget(rows, settings);

        Assert.Equal(1.6m, result.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_MultiplePartidas_SumaTodosCostos()
    {
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MAT", Codigo = "TB12", Descripcion = "Tablero 12 ctos", Unidad = "UN", Cantidad = 10m, MatClp = 5m, MoClp = 0m, MatUf = 5m, MoUf = 0m },
            new() { ItemId = "P002", Tipo = "MAT", Codigo = "CAB", Descripcion = "Cable", Unidad = "ML", Cantidad = 5m, MatClp = 1m, MoClp = 0m, MatUf = 1m, MoUf = 0m },
            new() { ItemId = "P002", Tipo = "MO",  Codigo = "MO",  Descripcion = "MO Cable", Unidad = "ML", Cantidad = 5m, MatClp = 0m, MoClp = 0.5m, MatUf = 0m, MoUf = 0.5m }
        };
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);

        var result = _service.CalculateBudget(rows, settings);

        Assert.Equal(57.5m, result.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_ConGastosGeneralesYUtilidad_AplicaMarkupCorrectamente()
    {
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MAT", Codigo = "MAT", Descripcion = "Materiales", Unidad = "GL", Cantidad = 1m, MatClp = 100m, MoClp = 0m, MatUf = 100m, MoUf = 0m }
        };
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 10m, utilidadPct: 20m, metrosCuadrados: 0m);
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;

        var result = _service.CalculateBudget(rows, settings);

        Assert.Equal(100m, result.CostoDirectoUf);
        Assert.Equal(10m, result.GastosGeneralesUf);
        Assert.Equal(110m, result.SubtotalUf);
        Assert.Equal(27.5m, result.UtilidadUf);
        Assert.Equal(137.5m, result.TotalVentaUf);
    }

    [Fact]
    public void Calculate_CubicacionVacia_RetornaCerosSinExcepcion()
    {
        var result = _service.CalculateBudget(new List<CubicacionRow>(), BudgetSettings.FromValues(0m, 0m, 0m));

        Assert.Equal(0m, result.CostoDirectoUf);
        Assert.Equal(0m, result.TotalVentaUf);
    }

    [Fact]
    public void GetDirectCostTotals_SeparaMatYMoCorrectamente()
    {
        var rows = new List<CubicacionRow>
        {
            new() { ItemId = "P001", Tipo = "MAT", Codigo = "M1", Descripcion = "Material", Unidad = "UN", Cantidad = 10m, MatClp = 2m, MoClp = 0m, MatUf = 2m, MoUf = 0m },
            new() { ItemId = "P001", Tipo = "MO",  Codigo = "O1", Descripcion = "Mano obra", Unidad = "HH", Cantidad = 8m,  MatClp = 0m, MoClp = 0.5m, MatUf = 0m, MoUf = 0.5m }
        };

        var totals = _service.GetDirectCostTotals(rows);

        Assert.Equal(20m, totals.Material);
        Assert.Equal(4m, totals.ManoObra);
        Assert.Equal(24m, totals.Total);
    }
}
