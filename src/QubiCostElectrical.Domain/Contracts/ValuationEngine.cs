using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ValuationLineInput(
    string LineId,
    decimal CantidadContractual,
    decimal PrecioUnitarioContractual,
    decimal CantidadAnterior,
    decimal CantidadPeriodo
);

public sealed record ValuationAdvanceInput(
    string AdvanceId,
    decimal CurrentBalance,
    decimal AmortizationPercentage,
    decimal AmortizationFixedAmount,
    string Rule // PercentageOfPeriodSubtotal, PercentageOfGross, FixedAmount
);

public sealed record ValuationRetentionInput(
    string RetentionId,
    decimal Percentage,
    decimal FixedAmount,
    decimal LimitAmount,
    string BaseType, // Subtotal, MontoBruto
    decimal CumulativeAppliedBefore
);

public sealed record ValuationCalculationInput(
    decimal OriginalContractAmount,
    decimal Reajuste,
    decimal Multas,
    decimal Descuentos,
    decimal TaxRate,
    List<ValuationLineInput> Lines,
    List<ValuationAdvanceInput> Advances,
    List<ValuationRetentionInput> Retentions
);

public sealed record ValuationLineResult(
    string LineId,
    decimal CantidadAnterior,
    decimal CantidadPeriodo,
    decimal CantidadAcumulada,
    decimal SaldoCantidad,
    decimal MontoAnterior,
    decimal MontoPeriodo,
    decimal MontoAcumulado,
    decimal SaldoMonetario
);

public sealed record ValuationAdvanceResult(
    string AdvanceId,
    decimal Amount,
    decimal Percentage,
    decimal PreAmortizationBalance,
    decimal PostAmortizationBalance
);

public sealed record ValuationRetentionResult(
    string RetentionId,
    decimal BaseAmount,
    decimal Percentage,
    decimal AppliedAmount,
    decimal CumulativeApplied,
    decimal PendingBalance
);

public sealed record ValuationCalculationResult(
    List<ValuationLineResult> Lines,
    decimal SubtotalValorizado,
    decimal Reajuste,
    decimal AnticipoAmortizado,
    List<ValuationAdvanceResult> Amortizations,
    decimal RetencionesAplicadas,
    List<ValuationRetentionResult> Retentions,
    decimal Descuentos,
    decimal Multas,
    decimal MontoBruto,
    decimal Impuestos,
    decimal MontoLiquido,
    decimal MontoAnterior,
    decimal MontoAcumulado,
    decimal SaldoContractual,
    decimal SaldoPorValorizar
);

public static class ValuationEngine
{
    public static ValuationCalculationResult Calculate(ValuationCalculationInput input)
    {
        var lineResults = new List<ValuationLineResult>();
        decimal subtotalValorizado = 0m;
        decimal totalMontoAnterior = 0m;
        decimal totalMontoAcumulado = 0m;

        foreach (var line in input.Lines)
        {
            decimal cantAcum = PrecisionPolicy.RoundCantidad(line.CantidadAnterior + line.CantidadPeriodo);
            decimal saldoCant = PrecisionPolicy.RoundCantidad(line.CantidadContractual - cantAcum);

            decimal montoAnt = PrecisionPolicy.RoundImporte(line.CantidadAnterior * line.PrecioUnitarioContractual);
            decimal montoPer = PrecisionPolicy.RoundImporte(line.CantidadPeriodo * line.PrecioUnitarioContractual);
            decimal montoAcum = PrecisionPolicy.RoundImporte(cantAcum * line.PrecioUnitarioContractual);
            decimal saldoMon = PrecisionPolicy.RoundImporte((line.CantidadContractual * line.PrecioUnitarioContractual) - montoAcum);

            lineResults.Add(new ValuationLineResult(
                line.LineId,
                line.CantidadAnterior,
                line.CantidadPeriodo,
                cantAcum,
                saldoCant,
                montoAnt,
                montoPer,
                montoAcum,
                saldoMon
            ));

            subtotalValorizado += montoPer;
            totalMontoAnterior += montoAnt;
            totalMontoAcumulado += montoAcum;
        }

        subtotalValorizado = PrecisionPolicy.RoundImporte(subtotalValorizado);
        totalMontoAnterior = PrecisionPolicy.RoundImporte(totalMontoAnterior);
        totalMontoAcumulado = PrecisionPolicy.RoundImporte(totalMontoAcumulado);

        // Amortizations
        var amortResults = new List<ValuationAdvanceResult>();
        decimal totalAmortizado = 0m;
        foreach (var adv in input.Advances)
        {
            decimal amortAmt = 0m;
            if (adv.Rule == "PercentageOfPeriodSubtotal")
            {
                amortAmt = PrecisionPolicy.RoundImporte(subtotalValorizado * adv.AmortizationPercentage / 100m);
            }
            else if (adv.Rule == "PercentageOfGross")
            {
                decimal grossForAdv = subtotalValorizado + input.Reajuste - input.Descuentos;
                amortAmt = PrecisionPolicy.RoundImporte(grossForAdv * adv.AmortizationPercentage / 100m);
            }
            else if (adv.Rule == "FixedAmount")
            {
                amortAmt = PrecisionPolicy.RoundImporte(adv.AmortizationFixedAmount);
            }

            // Constraints
            amortAmt = Math.Min(amortAmt, adv.CurrentBalance);
            amortAmt = Math.Max(0m, amortAmt);

            decimal postBal = PrecisionPolicy.RoundImporte(adv.CurrentBalance - amortAmt);
            amortResults.Add(new ValuationAdvanceResult(
                adv.AdvanceId,
                amortAmt,
                adv.AmortizationPercentage,
                adv.CurrentBalance,
                postBal
            ));

            totalAmortizado += amortAmt;
        }
        totalAmortizado = PrecisionPolicy.RoundImporte(totalAmortizado);

        // Retentions
        var retResults = new List<ValuationRetentionResult>();
        decimal totalRetenido = 0m;
        foreach (var ret in input.Retentions)
        {
            decimal baseAmt = ret.BaseType == "MontoBruto" 
                ? (subtotalValorizado + input.Reajuste - input.Descuentos) 
                : subtotalValorizado;

            decimal applied = 0m;
            if (ret.Percentage > 0m)
            {
                applied = PrecisionPolicy.RoundImporte(baseAmt * ret.Percentage / 100m);
            }
            else
            {
                applied = PrecisionPolicy.RoundImporte(ret.FixedAmount);
            }

            if (ret.LimitAmount > 0m)
            {
                decimal remainingToLimit = Math.Max(0m, ret.LimitAmount - ret.CumulativeAppliedBefore);
                applied = Math.Min(applied, remainingToLimit);
            }
            applied = Math.Max(0m, applied);

            decimal cumApplied = PrecisionPolicy.RoundImporte(ret.CumulativeAppliedBefore + applied);
            decimal pendingBal = ret.LimitAmount > 0m 
                ? PrecisionPolicy.RoundImporte(Math.Max(0m, ret.LimitAmount - cumApplied))
                : 0m;

            retResults.Add(new ValuationRetentionResult(
                ret.RetentionId,
                baseAmt,
                ret.Percentage,
                applied,
                cumApplied,
                pendingBal
            ));

            totalRetenido += applied;
        }
        totalRetenido = PrecisionPolicy.RoundImporte(totalRetenido);

        // Totals
        decimal montoBruto = PrecisionPolicy.RoundImporte(subtotalValorizado + input.Reajuste - input.Descuentos);
        decimal impuestos = PrecisionPolicy.RoundImporte(montoBruto * input.TaxRate);
        decimal montoLiquido = PrecisionPolicy.RoundImporte(montoBruto + impuestos - totalAmortizado - totalRetenido - input.Multas);

        decimal saldoContractual = PrecisionPolicy.RoundImporte(input.OriginalContractAmount - totalMontoAcumulado);
        decimal saldoPorValorizar = PrecisionPolicy.RoundImporte(input.OriginalContractAmount - totalMontoAcumulado);

        return new ValuationCalculationResult(
            lineResults,
            subtotalValorizado,
            input.Reajuste,
            totalAmortizado,
            amortResults,
            totalRetenido,
            retResults,
            input.Descuentos,
            input.Multas,
            montoBruto,
            impuestos,
            montoLiquido,
            totalMontoAnterior,
            totalMontoAcumulado,
            saldoContractual,
            saldoPorValorizar
        );
    }
}
