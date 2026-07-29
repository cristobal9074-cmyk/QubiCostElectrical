using System;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public sealed class EconomicCalculationService : IEconomicCalculationService
{
    public EconomicResult Calculate(EconomicInput input, CancellationToken ct = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ct.ThrowIfCancellationRequested();

        var validation = new EconomicValidation().ValidateForCalculation(input.Config);
        if (!validation.IsValid)
        {
            return new EconomicResult
            {
                Errors = validation.Errors.Select(e => $"[{e.Code}] {e.Message}").ToList().AsReadOnly(),
                UnitDirectCost = input.UnitDirectCost,
                PartidaQuantity = input.PartidaQuantity,
                Currency = input.Currency,
                EngineVersion = input.EngineVersion,
                CalculationDate = DateTime.UtcNow
            };
        }

        return EconomicEngine.Calculate(input, ct);
    }

    public EconomicValidationResult Validate(EconomicConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        return new EconomicValidation().ValidateForCalculation(config);
    }

    public EconomicResult CalculateScenario(Scenario scenario, EconomicInput baseInput, CancellationToken ct = default)
    {
        if (scenario == null)
            throw new ArgumentNullException(nameof(scenario));
        if (baseInput == null)
            throw new ArgumentNullException(nameof(baseInput));

        ct.ThrowIfCancellationRequested();

        return ScenarioEngine.CalculateScenario(scenario, baseInput, ct);
    }

    public ScenarioComparison Compare(EconomicResult baseResult, EconomicResult comparedResult, string baseName, string comparedName)
    {
        if (baseResult == null)
            throw new ArgumentNullException(nameof(baseResult));
        if (comparedResult == null)
            throw new ArgumentNullException(nameof(comparedResult));

        return ScenarioEngine.Compare(baseResult, comparedResult, baseName, comparedName);
    }
}
