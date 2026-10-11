using System.Threading;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IEconomicCalculationService
{
    EconomicResult Calculate(EconomicInput input, CancellationToken ct = default(CancellationToken));

    EconomicValidationResult Validate(EconomicConfig config);

    EconomicResult CalculateScenario(Scenario scenario, EconomicInput baseInput, CancellationToken ct = default(CancellationToken));

    ScenarioComparison Compare(EconomicResult baseResult, EconomicResult comparedResult, string baseName, string comparedName);
}
