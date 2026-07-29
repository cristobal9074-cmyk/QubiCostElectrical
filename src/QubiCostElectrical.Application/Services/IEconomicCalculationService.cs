using System.Threading;

namespace QubiCostElectrical;

public interface IEconomicCalculationService
{
    EconomicResult Calculate(EconomicInput input, CancellationToken ct = default);
    EconomicValidationResult Validate(EconomicConfig config);
    EconomicResult CalculateScenario(Scenario scenario, EconomicInput baseInput, CancellationToken ct = default);
    ScenarioComparison Compare(EconomicResult baseResult, EconomicResult comparedResult, string baseName, string comparedName);
}
