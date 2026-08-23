using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ContractValidation
{
	public static List<string> Validate(Contract contract)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(contract.VisibleNumber))
		{
			list.Add("El número visible de contrato es requerido.");
		}
		if (string.IsNullOrWhiteSpace(contract.Title))
		{
			list.Add("El título del contrato es requerido.");
		}
		if (contract.OriginalAmount < 0m)
		{
			list.Add("El monto original no puede ser negativo.");
		}
		if (contract.TaxAmount < 0m)
		{
			list.Add("El monto de impuestos no puede ser negativo.");
		}
		if (contract.StartDate > contract.EndDate)
		{
			list.Add("La fecha de inicio no puede ser posterior a la fecha de término.");
		}
		if (contract.ContractualDurationDays < 0)
		{
			list.Add("El plazo contractual en días no puede ser negativo.");
		}
		if (contract.AdvancePercentage < 0m || contract.AdvancePercentage > 100m)
		{
			list.Add("El porcentaje de anticipo debe estar entre 0 y 100.");
		}
		if (contract.RetentionPercentage < 0m || contract.RetentionPercentage > 100m)
		{
			list.Add("El porcentaje de retención debe estar entre 0 y 100.");
		}
		return list;
	}
}
