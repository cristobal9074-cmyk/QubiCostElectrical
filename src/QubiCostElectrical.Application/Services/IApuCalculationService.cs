namespace QubiCostElectrical;

public interface IApuCalculationService
{
    ApuCalculationResult Calculate(string apuDefinitionId, ApuCalculationInput input, CancellationToken ct = default);
    IReadOnlyList<ApuCalculationResult> CalculateAll(ApuCalculationInput input, CancellationToken ct = default);
    IReadOnlyList<ApuCalculationResult> CalculateMany(IReadOnlyList<string> apuIds, ApuCalculationInput input, CancellationToken ct = default);
    ApuCycleValidationResult ValidateAll(ApuCalculationInput input);
}
