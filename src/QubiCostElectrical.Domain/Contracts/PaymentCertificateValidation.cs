using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class PaymentCertificateValidation
{
    public static List<string> Validate(PaymentCertificate certificate)
    {
        var errors = new List<string>();

        if (certificate.ContractId == null)
            errors.Add("El contrato es requerido.");

        if (certificate.PeriodStart == default)
            errors.Add("La fecha de inicio del período es requerida.");

        if (certificate.PeriodEnd == default)
            errors.Add("La fecha de fin del período es requerida.");

        if (certificate.PeriodStart > certificate.PeriodEnd)
            errors.Add("La fecha de inicio no puede ser posterior a la fecha de fin.");

        if (certificate.CutOffDate == default)
            errors.Add("La fecha de corte es requerida.");

        if (certificate.ExchangeRate <= 0)
            errors.Add("El tipo de cambio debe ser mayor que cero.");

        if (string.IsNullOrWhiteSpace(certificate.Presenter))
            errors.Add("El presentador es requerido.");

        if (certificate.SubtotalValorizado < 0)
            errors.Add("El subtotal valorizado no puede ser negativo.");

        if (certificate.MontoBruto < 0)
            errors.Add("El monto bruto no puede ser negativo.");

        if (certificate.MontoLiquido < 0)
            errors.Add("El monto líquido no puede ser negativo.");

        if (certificate.MontoAcumulado < 0)
            errors.Add("El monto acumulado no puede ser negativo.");

        return errors;
    }

    public static List<string> ValidateLine(PaymentCertificateLine line)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(line.Description))
            errors.Add("La descripción de la línea es requerida.");

        if (string.IsNullOrWhiteSpace(line.Unit))
            errors.Add("La unidad de la línea es requerida.");

        if (line.CantidadContractual < 0)
            errors.Add("La cantidad contractual no puede ser negativa.");

        if (line.PrecioUnitarioContractual < 0)
            errors.Add("El precio unitario contractual no puede ser negativo.");

        if (line.CantidadPeriodo < 0)
            errors.Add("La cantidad del período no puede ser negativa.");

        if (line.CantidadAnterior < 0)
            errors.Add("La cantidad anterior no puede ser negativa.");

        if (line.CantidadAcumulada < 0)
            errors.Add("La cantidad acumulada no puede ser negativa.");

        if (line.CantidadAcumulada > line.CantidadContractual + 0.001m)
            errors.Add("La cantidad acumulada no puede superar la cantidad contractual.");

        return errors;
    }

    public static List<string> ValidateAdvance(ContractAdvance advance)
    {
        var errors = new List<string>();

        if (advance.Amount <= 0)
            errors.Add("El monto del anticipo debe ser mayor que cero.");

        if (advance.AmortizationPercentage < 0 || advance.AmortizationPercentage > 100)
            errors.Add("El porcentaje de amortización debe estar entre 0 y 100.");

        if (advance.InitialBalance < 0)
            errors.Add("El saldo inicial no puede ser negativo.");

        if (advance.CurrentBalance < 0)
            errors.Add("El saldo actual no puede ser negativo.");

        if (advance.TotalAmortized < 0)
            errors.Add("El total amortizado no puede ser negativo.");

        return errors;
    }

    public static List<string> ValidateRetention(ContractRetention retention)
    {
        var errors = new List<string>();

        if (retention.Percentage < 0 || retention.Percentage > 100)
            errors.Add("El porcentaje de retención debe estar entre 0 y 100.");

        if (retention.FixedAmount < 0)
            errors.Add("El monto fijo no puede ser negativo.");

        if (retention.LimitAmount < 0)
            errors.Add("El monto límite no puede ser negativo.");

        return errors;
    }

    public static List<string> ValidateObservation(PaymentObservation observation)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(observation.Author))
            errors.Add("El autor de la observación es requerido.");

        if (string.IsNullOrWhiteSpace(observation.Comment))
            errors.Add("El comentario de la observación es requerido.");

        return errors;
    }
}
