using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Integración APU + cubicación + itemizado vía BudgetCalculationService (flujo real de la app).
/// </summary>
public sealed class ApuCubicacionIntegrationTests
{
    private readonly BudgetCalculationService _calculation = new();

    [Fact]
    public void ApuManda_UsesApuUnitCostAndItemizadoQuantity_IgnoresCubicacionPrices()
    {
        var cubicacion = new List<CubicacionRow>
        {
            new()
            {
                ItemId = "P1",
                Codigo = "PVC20",
                Descripcion = "PVC 20",
                Unidad = "ML",
                Cantidad = 10m,
                MatClp = 99m, MatUf = 99m,
                MoClp = 99m, MoUf = 99m
            }
        };
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 50m }
        };
        var apus = new List<ApuRow>
        {
            new()
            {
                ItemId = "P1",
                Descripcion = "APU Canalización",
                Recursos = new List<ApuRecursoRow>
                {
                    new()
                    {
                        CodigoMaterial = "MAT1",
                        Descripcion = "Material APU",
                        PrecioUf = 2m,
                        Rendimiento = 1m,
                        Tipo = ApuTipoRecurso.MAT
                    }
                }
            }
        };

        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.ApuManda;
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;

        var cost = _calculation.CalculateBudget(cubicacion, settings, apus, itemizado, null);

        decimal qty = 50m;
        decimal unitMat = apus[0].CostoMaterialUf;
        decimal unitMermasImprev = apus[0].MermaUf + apus[0].ImprevistosUf;
        decimal expectedMateriales = unitMat * qty;
        decimal expectedDirecto = expectedMateriales + unitMermasImprev * qty;

        Assert.Equal(expectedMateriales, cost.MaterialesUf);
        Assert.Equal(expectedDirecto, cost.CostoDirectoUf);
        Assert.Equal(expectedDirecto, cost.TotalVentaUf);
        Assert.DoesNotContain(cost.Origenes, x => x.Origen == BudgetCostOrigin.Cubicacion && x.ItemId == "P1");
        Assert.Contains(cost.Origenes, x => x.Origen == BudgetCostOrigin.Apu && x.ItemId == "P1");
    }

    [Fact]
    public void CubicacionManda_UsesCubicacionQuantityAndUnitPrices_ForDirectCost()
    {
        var cubicacion = new List<CubicacionRow>
        {
            new()
            {
                ItemId = "P1",
                Tipo = "MAT",
                Codigo = "MAT001",
                Descripcion = "Tubo PVC",
                Unidad = "ML",
                Cantidad = 50m,
                MatClp = 0.12m, MatUf = 0.12m,
                MoClp = 0.08m, MoUf = 0.08m
            }
        };
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 50m }
        };

        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.ApuMode = BudgetApuMode.CubicacionManda;
        settings.Markup.RiesgoPct = 0m;
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;

        var cost = _calculation.CalculateBudget(cubicacion, settings, null, itemizado, null);

        const decimal expectedDirecto = 50m * (0.12m + 0.08m);

        Assert.Equal(expectedDirecto, cost.MaterialesUf + cost.ManoObraUf);
        Assert.Equal(expectedDirecto, cost.CostoDirectoUf);
        Assert.Equal(expectedDirecto, cost.TotalVentaUf);
        Assert.Contains(cost.Origenes, x => x.Origen == BudgetCostOrigin.Cubicacion && x.ItemId == "P1");
    }
}
