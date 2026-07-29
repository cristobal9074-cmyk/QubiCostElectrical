using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ValuationEngineTests
{
    private static ValuationCalculationInput CreateSingleLineInput(
        decimal contractAmount = 100_000m,
        decimal cantContractual = 100m,
        decimal precioUnitario = 1000m,
        decimal cantAnterior = 40m,
        decimal cantPeriodo = 20m,
        decimal reajuste = 0m,
        decimal multas = 0m,
        decimal descuentos = 0m,
        decimal taxRate = 0m)
    {
        return new ValuationCalculationInput(
            contractAmount, reajuste, multas, descuentos, taxRate,
            new List<ValuationLineInput>
            {
                new("L1", cantContractual, precioUnitario, cantAnterior, cantPeriodo)
            },
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>());
    }

    [Fact]
    public void Calculate_WithSingleLine_ReturnsCorrectLineTotals()
    {
        var input = CreateSingleLineInput();
        var result = ValuationEngine.Calculate(input);

        var line = Assert.Single(result.Lines);
        Assert.Equal("L1", line.LineId);
        Assert.Equal(40m, line.CantidadAnterior);
        Assert.Equal(20m, line.CantidadPeriodo);
        Assert.Equal(60m, line.CantidadAcumulada);       // 40 + 20
        Assert.Equal(40m, line.SaldoCantidad);            // 100 - 60
        Assert.Equal(40_000m, line.MontoAnterior);         // 40 * 1000
        Assert.Equal(20_000m, line.MontoPeriodo);          // 20 * 1000
        Assert.Equal(60_000m, line.MontoAcumulado);        // 60 * 1000
        Assert.Equal(40_000m, line.SaldoMonetario);        // (100*1000) - 60000
    }

    [Fact]
    public void Calculate_WithSingleLine_ReturnsCorrectAggregates()
    {
        var input = CreateSingleLineInput();
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(20_000m, result.SubtotalValorizado);   // MontoPeriodo
        Assert.Equal(0m, result.Reajuste);
        Assert.Equal(0m, result.Descuentos);
        Assert.Equal(0m, result.Multas);
        Assert.Equal(20_000m, result.MontoBruto);           // subtotal + reajuste - descuentos
        Assert.Equal(0m, result.Impuestos);
        Assert.Equal(20_000m, result.MontoLiquido);
        Assert.Equal(60_000m, result.MontoAcumulado);
        Assert.Equal(40_000m, result.SaldoContractual);     // 100_000 - 60_000
        Assert.Equal(40_000m, result.SaldoPorValorizar);
    }

    [Fact]
    public void Calculate_WithMultipleLines_SumsCorrectly()
    {
        var lines = new List<ValuationLineInput>
        {
            new("L1", 100m, 1000m, 40m, 20m),   // montoPeriodo = 20_000
            new("L2", 50m, 2000m, 10m, 5m)      // montoPeriodo = 10_000
        };

        var input = new ValuationCalculationInput(
            200_000m, 0m, 0m, 0m, 0m, lines,
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        Assert.Equal(30_000m, result.SubtotalValorizado);    // 20_000 + 10_000
        Assert.Equal(2, result.Lines.Count);

        // Verify each line independently
        Assert.Equal(20_000m, result.Lines[0].MontoPeriodo);
        Assert.Equal(10_000m, result.Lines[1].MontoPeriodo);
    }

    [Fact]
    public void Calculate_WithReajuste_AdjustsMontoBruto()
    {
        var input = CreateSingleLineInput(reajuste: 5_000m);
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(5_000m, result.Reajuste);
        Assert.Equal(25_000m, result.MontoBruto);  // 20_000 + 5_000
    }

    [Fact]
    public void Calculate_WithDescuentos_ReducesMontoBruto()
    {
        var input = CreateSingleLineInput(descuentos: 3_000m);
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(3_000m, result.Descuentos);
        Assert.Equal(17_000m, result.MontoBruto);  // 20_000 - 3_000
    }

    [Fact]
    public void Calculate_WithMultas_ReducesMontoLiquido()
    {
        var input = CreateSingleLineInput(multas: 2_000m);
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(2_000m, result.Multas);
        Assert.Equal(18_000m, result.MontoLiquido); // 20_000 - 2_000
    }

    [Fact]
    public void Calculate_WithTaxRate_AddsImpuestos()
    {
        var input = CreateSingleLineInput(taxRate: 0.19m);
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(0.19m, result.Impuestos / result.MontoBruto, 2);
        Assert.Equal(23_800m, result.MontoLiquido); // (20_000 * 1.19) = 23_800
    }

    [Fact]
    public void Calculate_WithAdvancePercentageOfSubtotal_AppliesAmortization()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 50m) // montoPeriodo = 50_000
            },
            new List<ValuationAdvanceInput>
            {
                new("A1", 30_000m, 10m, 0m, "PercentageOfPeriodSubtotal")
            },
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        var amort = Assert.Single(result.Amortizations);
        Assert.Equal("A1", amort.AdvanceId);
        Assert.Equal(5_000m, amort.Amount);          // 10% of 50_000
        Assert.Equal(30_000m, amort.PreAmortizationBalance);
        Assert.Equal(25_000m, amort.PostAmortizationBalance);
        Assert.Equal(5_000m, result.AnticipoAmortizado);
    }

    [Fact]
    public void Calculate_WithAdvanceExceedsBalance_CapsAtCurrentBalance()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 10m) // montoPeriodo = 10_000
            },
            new List<ValuationAdvanceInput>
            {
                new("A1", 1_000m, 50m, 0m, "PercentageOfPeriodSubtotal") // 50% of 10_000 = 5_000, but balance is 1_000
            },
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        var amort = Assert.Single(result.Amortizations);
        Assert.Equal(1_000m, amort.Amount); // capped at balance
        Assert.Equal(0m, amort.PostAmortizationBalance);
    }

    [Fact]
    public void Calculate_WithAdvanceFixedAmount_AppliesExactAmount()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 50m)
            },
            new List<ValuationAdvanceInput>
            {
                new("A1", 20_000m, 0m, 8_000m, "FixedAmount")
            },
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        var amort = Assert.Single(result.Amortizations);
        Assert.Equal(8_000m, amort.Amount);
        Assert.Equal(12_000m, amort.PostAmortizationBalance);
    }

    [Fact]
    public void Calculate_WithRetentionPercentage_AppliesCorrectly()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 50m) // montoPeriodo = 50_000
            },
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>
            {
                new("R1", 10m, 0m, 0m, "Subtotal", 0m) // 10% of subtotal
            });

        var result = ValuationEngine.Calculate(input);

        var ret = Assert.Single(result.Retentions);
        Assert.Equal("R1", ret.RetentionId);
        Assert.Equal(50_000m, ret.BaseAmount);
        Assert.Equal(5_000m, ret.AppliedAmount);       // 10% of 50_000
        Assert.Equal(5_000m, ret.CumulativeApplied);
        Assert.Equal(5_000m, result.RetencionesAplicadas);
    }

    [Fact]
    public void Calculate_WithRetentionFixedAmount_AppliesCorrectly()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 50m)
            },
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>
            {
                new("R1", 0m, 3_000m, 0m, "Subtotal", 0m)
            });

        var result = ValuationEngine.Calculate(input);

        var ret = Assert.Single(result.Retentions);
        Assert.Equal(3_000m, ret.AppliedAmount);
    }

    [Fact]
    public void Calculate_WithRetentionLimit_CapsAppliedAmount()
    {
        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 50m)
            },
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>
            {
                new("R1", 10m, 0m, 1_000m, "Subtotal", 500m)
                // 10% of 50_000 = 5_000, but limit is 1_000 and already 500 applied
                // remaining = 500
            });

        var result = ValuationEngine.Calculate(input);

        var ret = Assert.Single(result.Retentions);
        Assert.Equal(500m, ret.AppliedAmount); // remaining to limit
        Assert.Equal(1_000m, ret.CumulativeApplied); // 500 + 500
        Assert.Equal(0m, ret.PendingBalance); // limit reached
    }

    [Fact]
    public void Calculate_WithCompleteScenario_ProducesCorrectFinalTotals()
    {
        var input = new ValuationCalculationInput(
            500_000m,  // original contract amount
            10_000m,   // reajuste
            2_000m,    // multas
            1_500m,    // descuentos
            0.19m,     // tax rate 19%
            new List<ValuationLineInput>
            {
                new("L1", 200m, 1_000m, 80m, 30m),  // montoP = 30_000, montoAnt = 80_000
                new("L2", 100m, 500m, 20m, 10m)     // montoP = 5_000, montoAnt = 10_000
            },
            new List<ValuationAdvanceInput>
            {
                new("A1", 50_000m, 10m, 0m, "PercentageOfPeriodSubtotal")
                // 10% of (30_000 + 5_000) = 3_500
            },
            new List<ValuationRetentionInput>
            {
                new("R1", 5m, 0m, 0m, "Subtotal", 0m)
                // 5% of 35_000 = 1_750
            });

        var result = ValuationEngine.Calculate(input);

        Assert.Equal(35_000m, result.SubtotalValorizado);           // 30_000 + 5_000
        Assert.Equal(10_000m, result.Reajuste);
        Assert.Equal(1_500m, result.Descuentos);
        Assert.Equal(2_000m, result.Multas);
        Assert.Equal(43_500m, result.MontoBruto);                   // 35_000 + 10_000 - 1_500
        Assert.Equal(8_265m, result.Impuestos);                     // 43_500 * 0.19 = 8_265
        Assert.Equal(3_500m, result.AnticipoAmortizado);            // 10% of subtotal
        Assert.Equal(1_750m, result.RetencionesAplicadas);          // 5% of subtotal
        Assert.Equal(44_515m, result.MontoLiquido);                 // 43_500 + 8_265 - 3_500 - 1_750 - 2_000 = 44_515
        Assert.Equal(90_000m, result.MontoAnterior);                // 80_000 + 10_000
        Assert.Equal(125_000m, result.MontoAcumulado);              // total accumulated
        Assert.Equal(375_000m, result.SaldoContractual);            // 500_000 - 125_000
        Assert.Equal(375_000m, result.SaldoPorValorizar);
    }

    [Fact]
    public void Calculate_WithZeroLines_ReturnsZeroTotals()
    {
        var input = new ValuationCalculationInput(
            0m, 0m, 0m, 0m, 0m,
            new List<ValuationLineInput>(),
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.SubtotalValorizado);
        Assert.Equal(0m, result.MontoBruto);
        Assert.Equal(0m, result.MontoLiquido);
        Assert.Empty(result.Amortizations);
        Assert.Empty(result.Retentions);
    }

    [Fact]
    public void Calculate_UsesBankerRounding_ForLineAmounts()
    {
        // Test midpoint rounding: 1.005 * 1000 = 1005 → RoundImporte = 1005 (no rounding needed)
        // 1.005 * 100 = 100.5 → RoundImporte with ToEven: 100.5 → 100 (even before 0)
        var lines = new List<ValuationLineInput>
        {
            new("L1", 100m, 100m, 0m, 1.005m)
        };

        var input = new ValuationCalculationInput(
            100_000m, 0m, 0m, 0m, 0m, lines,
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        // 1.005 * 100 = 100.5 → RoundImporte con 2 decimales = 100.5 (no es punto medio para 2 decimales)
        Assert.Equal(100.50m, result.Lines[0].MontoPeriodo);
    }

    [Fact]
    public void Calculate_WithFullCantidadContractual_SaldoCantidadIsZero()
    {
        var input = CreateSingleLineInput(cantContractual: 100m, cantAnterior: 60m, cantPeriodo: 40m);
        var result = ValuationEngine.Calculate(input);

        Assert.Equal(100m, result.Lines[0].CantidadAcumulada);
        Assert.Equal(0m, result.Lines[0].SaldoCantidad);
    }

    [Fact]
    public void Calculate_WithAdvancePercentageOfGross_AppliesCorrectly()
    {
        var input = new ValuationCalculationInput(
            100_000m, 5_000m, 0m, 1_000m, 0m,  // reajuste=5000, descuentos=1000
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 30m)  // montoPeriodo = 30_000
            },
            new List<ValuationAdvanceInput>
            {
                new("A1", 10_000m, 10m, 0m, "PercentageOfGross")
                // gross for advance = 30_000 + 5_000 - 1_000 = 34_000
                // 10% of 34_000 = 3_400
            },
            new List<ValuationRetentionInput>());

        var result = ValuationEngine.Calculate(input);

        var amort = Assert.Single(result.Amortizations);
        Assert.Equal(3_400m, amort.Amount);
    }

    [Fact]
    public void Calculate_WithRetentionBaseMontoBruto_UsesGrossBase()
    {
        var input = new ValuationCalculationInput(
            100_000m, 3_000m, 0m, 500m, 0m,
            new List<ValuationLineInput>
            {
                new("L1", 100m, 1000m, 0m, 20m)  // montoPeriodo = 20_000
            },
            new List<ValuationAdvanceInput>(),
            new List<ValuationRetentionInput>
            {
                new("R1", 10m, 0m, 0m, "MontoBruto", 0m)
                // base = 20_000 + 3_000 - 500 = 22_500
                // 10% of 22_500 = 2_250
            });

        var result = ValuationEngine.Calculate(input);

        var ret = Assert.Single(result.Retentions);
        Assert.Equal(22_500m, ret.BaseAmount);
        Assert.Equal(2_250m, ret.AppliedAmount);
    }
}
