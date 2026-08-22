using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Cobertura del corazón del producto: cascada de recargos (riesgo→GG→utilidad→descuento→IVA),
/// mermas y herramientas globales y por fila, clasificación por tipo de recurso, modo manual y
/// órdenes de cambio. Un error aquí presupuesta mal una obra, por eso se fija con valores exactos.
/// </summary>
public sealed class BudgetEngineCostTests
{
    private static CubicacionRow Mat(decimal cantidad, decimal matUf, decimal moUf = 0m, string tipo = "MAT")
        => new()
        {
            ItemId = "I1",
            Codigo = "C1",
            Descripcion = "x",
            Unidad = "UN",
            Tipo = tipo,
            Cantidad = cantidad,
            // Motor calcula en CLP; estos tests fijan la misma magnitud en MatClp/MoClp.
            MatClp = matUf,
            MoClp = moUf,
            MatUf = matUf,
            MoUf = moUf
        };

    [Fact]
    public void Calculate_FullMarkupCascade_IsExactAndDeterministic()
    {
        var engine = new BudgetEngine();
        var rows = new[] { Mat(10m, 1m, 0.5m) }; // material 10 + mano 5 = 15 directo
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 10m, utilidadPct: 20m, metrosCuadrados: 100m);
        settings.Markup.RiesgoPct = 5m;
        settings.Markup.DescuentoPct = 10m;
        settings.Markup.IvaPct = 19m;
        settings.Markup.IncluirIvaEnTotal = true;

        var r = engine.Calculate(rows, settings);

        // subtotal=17.325; sobre-venta 20%: bruto=17.325/0.8=21.65625
        // descuento 10%: 2.165625; neto=19.490625; IVA 19%=3.70321875; conIva=23.19384375
        Assert.Equal(15m, r.CostoDirectoUf);
        Assert.Equal(0.75m, r.RiesgoUf);
        Assert.Equal(15.75m, r.BaseCostoUf);
        Assert.Equal(1.575m, r.GastosGeneralesUf);
        Assert.Equal(17.325m, r.SubtotalUf);
        Assert.Equal(4.33125m, r.UtilidadUf);
        Assert.Equal(2.165625m, r.DescuentoUf);
        Assert.Equal(19.490625m, r.TotalVentaNetoUf);
        Assert.Equal(3.70321875m, r.IvaUf);
        Assert.Equal(23.19384375m, r.TotalVentaConIvaUf);
        Assert.Equal(23.19384375m, r.TotalVentaUf);
    }

    [Fact]
    public void Calculate_WithoutIva_VentaIsNeto()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.Markup.IvaPct = 19m;
        settings.Markup.IncluirIvaEnTotal = false;

        var r = engine.Calculate(new[] { Mat(1m, 100m) }, settings);

        Assert.Equal(100m, r.TotalVentaNetoUf);
        Assert.Equal(100m, r.TotalVentaUf); // sin IVA en el total
        Assert.Equal(19m, r.IvaUf);         // pero el IVA queda informado
    }

    [Fact]
    public void Calculate_MargenReal_UsesVentaOverDirectCost()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 25m, 0m); // utilidad 25%
        settings.Markup.IvaPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;
        var r = engine.Calculate(new[] { Mat(1m, 100m) }, settings);

        // sobre-venta 25%: venta = 100/0.75 = 133.333...; margen = 25% exacto
        Assert.Equal(25m, r.MargenRealPct, 4);
    }

    [Fact]
    public void Calculate_UfM2_IsZero_WhenNoSurface()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, metrosCuadrados: 0m);
        var r = engine.Calculate(new[] { Mat(1m, 50m) }, settings);
        Assert.Equal(0m, r.UfM2);
    }

    [Fact]
    public void Calculate_GlobalMerma_AppliesOverMaterialsOnly()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.MermasPct = 5m;
        var r = engine.Calculate(new[] { Mat(10m, 2m) }, settings); // material 20

        Assert.Equal(1m, r.MermasUf);          // 20 * 5%
        Assert.Equal(21m, r.CostoDirectoUf);   // 20 + 1
    }

    [Fact]
    public void Calculate_GlobalHerramientas_AppliesOverLabourOnly()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.HerramientasPct = 3m;
        var r = engine.Calculate(new[] { Mat(10m, 0m, 1m, tipo: "MO") }, settings); // mano 10

        Assert.Equal(0.3m, r.HerramientasUf);  // 10 * 3%
        Assert.Equal(10.3m, r.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_RowMermaOverride_ExcludesRowFromGlobalBase()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.MermasPct = 5m; // global que NO debe tocar la fila con override
        var row = Mat(10m, 2m);  // material 20
        row.MermaPctOverride = 10m;

        var r = engine.Calculate(new[] { row }, settings);

        Assert.Equal(2m, r.MermasUf);        // solo 20 * 10% del override, sin global
        Assert.Equal(22m, r.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_RowHerramientasOverride_ExcludesRowFromGlobalBase()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        settings.HerramientasPct = 3m;
        var row = Mat(10m, 0m, 1m, tipo: "MO"); // mano 10
        row.HerramientasPctOverride = 5m;

        var r = engine.Calculate(new[] { row }, settings);

        Assert.Equal(0.5m, r.HerramientasUf); // 10 * 5% override, sin global
    }

    [Theory]
    [InlineData("EQ")]
    [InlineData("SC")]
    [InlineData("TR")]
    [InlineData("HERR")]
    public void Calculate_ClassifiesRowByTipo(string tipo)
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var r = engine.Calculate(new[] { Mat(2m, 5m, tipo: tipo) }, settings); // total 10

        Assert.Equal(0m, r.MaterialesUf);
        Assert.Equal(10m, r.CostoDirectoUf);
        decimal bucket = tipo switch
        {
            "EQ" => r.EquiposUf,
            "SC" => r.SubcontratosUf,
            "TR" => r.TransporteUf,
            "HERR" => r.HerramientasUf,
            _ => 0m
        };
        Assert.Equal(10m, bucket);
    }

    [Fact]
    public void Calculate_ManualMode_IgnoresCubicacionAndUsesManualValues()
    {
        var engine = new BudgetEngine();
        var settings = new BudgetSettings
        {
            ApuMode = BudgetApuMode.Manual,
            ManualMaterialesUf = 100m,
            ManualManoObraUf = 50m
        };

        var r = engine.Calculate(new[] { Mat(999m, 999m) }, settings);

        Assert.Equal(100m, r.MaterialesUf);
        Assert.Equal(50m, r.ManoObraUf);
        Assert.Equal(150m, r.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_ApprovedChangeOrder_AddsDelta_NonApprovedIgnored()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m);
        var rows = new[] { Mat(1m, 100m) };
        var changeOrders = new List<ChangeOrderRow>
        {
            new() { Numero = "OC-1", Titulo = "Aprobada", Estado = "Aprobada", DeltaCostoUf = 10m },
            new() { Numero = "OC-2", Titulo = "Borrador", Estado = "Borrador", DeltaCostoUf = 999m }
        };

        var r = engine.Calculate(rows, settings, apus: null, itemizado: null, changeOrders: changeOrders);

        Assert.Equal(110m, r.CostoDirectoUf); // 100 + 10, la borrador no entra
    }

    [Fact]
    public void Calculate_WithFamilyMarkups_AppliesCustomMarkupCorrectly()
    {
        var engine = new BudgetEngine();
        var settings = BudgetSettings.FromValues(0m, 0m, 0m); // global gg = 0, utilidad = 0
        settings.Currency.ValorUfClp = 40000m;

        var row = new CubicacionRow
        {
            ItemId = "P001",
            Tipo = "MAT",
            Codigo = "C001",
            Descripcion = "Cable",
            Unidad = "M",
            Cantidad = 10m,
            MatClp = 1000m,
            MoClp = 0m,
            GgUtFactor = 1.50m,
            Categoria = "Conductores"
        };

        var r = engine.Calculate(new[] { row }, settings);

        // Costo = 10 * 1000 = 10000 CLP
        Assert.Equal(10000m, r.CostoDirectoUf);

        // Venta unitaria: ROUND(1000 * 1.50 / 40000, 3) = ROUND(0.0375, 3) = 0.038 UF
        // Venta total neta en CLP = 0.038 * 40000 * 10 = 15200 CLP
        Assert.Equal(15200m, r.TotalVentaNetoUf);

        // Con IVA del 19%: 15200 * 1.19 = 18088 CLP
        Assert.Equal(18088m, r.TotalVentaUf);
    }

    [Fact]
    public void Calculate_NegotiatedDiscount_ReducesMaterialCostForMatchingCodigo()
    {
        var engine = new BudgetEngine();
        var row = Mat(10m, 1000m); // material 10*1000 = 10000 CLP, sin MO
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 0m, utilidadPct: 0m, metrosCuadrados: 0m);
        settings.NegotiatedDiscounts = new List<NegotiatedDiscountRow>
        {
            new() { Codigo = "C1", DescuentoPct = 10m }
        };

        var r = engine.Calculate(new[] { row }, settings);

        // 10000 * (1 - 10%) = 9000
        Assert.Equal(9000m, r.MaterialesUf);
        Assert.Equal(9000m, r.CostoDirectoUf);
    }

    [Fact]
    public void Calculate_NegotiatedDiscount_DoesNotAffectNonMatchingCodigo()
    {
        var engine = new BudgetEngine();
        var row = Mat(10m, 1000m);
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 0m, utilidadPct: 0m, metrosCuadrados: 0m);
        settings.NegotiatedDiscounts = new List<NegotiatedDiscountRow>
        {
            new() { Codigo = "OTRO", DescuentoPct = 50m }
        };

        var r = engine.Calculate(new[] { row }, settings);

        Assert.Equal(10000m, r.MaterialesUf);
    }
}
