using System;
using System.Linq;
using System.Threading;
using QubiCostElectrical;

namespace QubiCostElectrical;

public sealed class EconomicCalculationService : IEconomicCalculationService
{
	public EconomicResult Calculate(EconomicInput input, CancellationToken ct = default(CancellationToken))
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		ct.ThrowIfCancellationRequested();
		EconomicValidationResult economicValidationResult = new EconomicValidation().ValidateForCalculation(input.Config);
		if (!economicValidationResult.IsValid)
		{
			return new EconomicResult
			{
				Errors = economicValidationResult.Errors.Select((EconomicValidationError e) => "[" + e.Code + "] " + e.Message).ToList().AsReadOnly(),
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
		{
			throw new ArgumentNullException("config");
		}
		return new EconomicValidation().ValidateForCalculation(config);
	}

	public EconomicResult CalculateScenario(Scenario scenario, EconomicInput baseInput, CancellationToken ct = default(CancellationToken))
	{
		if (scenario == null)
		{
			throw new ArgumentNullException("scenario");
		}
		if (baseInput == null)
		{
			throw new ArgumentNullException("baseInput");
		}
		ct.ThrowIfCancellationRequested();
		return ScenarioEngine.CalculateScenario(scenario, baseInput, ct);
	}

	public ScenarioComparison Compare(EconomicResult baseResult, EconomicResult comparedResult, string baseName, string comparedName)
	{
		if (baseResult == null)
		{
			throw new ArgumentNullException("baseResult");
		}
		if (comparedResult == null)
		{
			throw new ArgumentNullException("comparedResult");
		}
		return ScenarioEngine.Compare(baseResult, comparedResult, baseName, comparedName);
	}
}
