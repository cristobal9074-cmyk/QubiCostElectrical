namespace QubiCostElectrical;

public sealed record ResourceValidationResult
{
    public bool IsValid { get; init; } = true;
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public static class ResourceValidator
{
    public static ResourceValidationResult Validate(Resource resource)
    {
        var result = new ResourceValidationResult { Errors = new(), Warnings = new() };
        if (string.IsNullOrWhiteSpace(resource.Code))
            result.Errors.Add("El código del recurso es obligatorio.");
        if (string.IsNullOrWhiteSpace(resource.Description))
            result.Errors.Add("La descripción del recurso es obligatoria.");
        if (!UnitOfMeasureCatalog.IsValid(resource.Unit))
            result.Errors.Add($"La unidad '{resource.Unit}' no es válida.");
        if (resource.BasePrice < 0)
            result.Errors.Add("El precio base no puede ser negativo.");
        if (resource.Yield == 0)
            result.Errors.Add("El rendimiento no puede ser cero.");
        if (resource.Yield < 0)
            result.Errors.Add("El rendimiento no puede ser negativo.");
        if (resource.Waste < 0)
            result.Errors.Add("El desperdicio no puede ser negativo.");
        if (resource.Waste > 100)
            result.Warnings.Add("El desperdicio supera el 100% — verifique el valor.");
        if (resource.AdjustmentFactor <= 0)
            result.Errors.Add("El factor de ajuste debe ser positivo.");
        if (!CurrencyCatalog.IsValid(resource.Currency))
            result.Errors.Add($"La moneda '{resource.Currency}' no es válida.");
        if (resource.PriceDate == null && resource.BasePrice > 0)
            result.Warnings.Add("El recurso tiene precio pero no tiene fecha de precio asociada.");

        var unit = UnitOfMeasureCatalog.Find(resource.Unit);
        if (unit != null && unit.Category == UnitCategory.Percentage && resource.BasePrice > 0)
            result.Warnings.Add("Un recurso en porcentaje no debería tener precio base.");

        result = result with { IsValid = result.Errors.Count == 0 };
        return result;
    }

    public static ResourceValidationResult ValidatePriceHistory(ResourcePriceHistoryEntry entry)
    {
        var result = new ResourceValidationResult { Errors = new(), Warnings = new() };
        if (string.IsNullOrWhiteSpace(entry.ResourceId))
            result.Errors.Add("El recurso es obligatorio.");
        if (entry.Price < 0)
            result.Errors.Add("El precio no puede ser negativo.");
        if (!CurrencyCatalog.IsValid(entry.Currency))
            result.Errors.Add($"La moneda '{entry.Currency}' no es válida.");
        if (entry.EffectiveDate == default)
            result.Errors.Add("La fecha de vigencia es obligatoria.");
        result = result with { IsValid = result.Errors.Count == 0 };
        return result;
    }

    public static ResourceValidationResult ValidateExchangeRate(ExchangeRate rate)
    {
        var result = new ResourceValidationResult { Errors = new(), Warnings = new() };
        if (!CurrencyCatalog.IsValid(rate.SourceCurrency))
            result.Errors.Add($"Moneda origen '{rate.SourceCurrency}' no es válida.");
        if (!CurrencyCatalog.IsValid(rate.TargetCurrency))
            result.Errors.Add($"Moneda destino '{rate.TargetCurrency}' no es válida.");
        if (rate.Rate <= 0)
            result.Errors.Add("La tasa de cambio debe ser positiva.");
        if (string.Equals(rate.SourceCurrency, rate.TargetCurrency, StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("La moneda origen y destino son la misma — la tasa debería ser 1.");
        result = result with { IsValid = result.Errors.Count == 0 };
        return result;
    }
}
