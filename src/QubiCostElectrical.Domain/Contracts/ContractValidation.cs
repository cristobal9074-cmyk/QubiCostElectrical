using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ContractValidation
{
    public static List<string> Validate(Contract contract)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(contract.VisibleNumber))
            errors.Add("El número visible de contrato es requerido.");

        if (string.IsNullOrWhiteSpace(contract.Title))
            errors.Add("El título del contrato es requerido.");

        if (contract.OriginalAmount < 0)
            errors.Add("El monto original no puede ser negativo.");

        if (contract.TaxAmount < 0)
            errors.Add("El monto de impuestos no puede ser negativo.");

        if (contract.StartDate > contract.EndDate)
            errors.Add("La fecha de inicio no puede ser posterior a la fecha de término.");

        if (contract.ContractualDurationDays < 0)
            errors.Add("El plazo contractual en días no puede ser negativo.");

        if (contract.AdvancePercentage < 0 || contract.AdvancePercentage > 100)
            errors.Add("El porcentaje de anticipo debe estar entre 0 y 100.");

        if (contract.RetentionPercentage < 0 || contract.RetentionPercentage > 100)
            errors.Add("El porcentaje de retención debe estar entre 0 y 100.");

        return errors;
    }
}
