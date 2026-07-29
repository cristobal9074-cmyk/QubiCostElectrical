using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public sealed class ApuCalculationService : IApuCalculationService
{
    public ApuCalculationResult Calculate(string apuDefinitionId, ApuCalculationInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apuDefinitionId))
            throw new ArgumentException("APU definition ID is required", nameof(apuDefinitionId));
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ct.ThrowIfCancellationRequested();

        var apu = input.ApuDefinitions.FirstOrDefault(a => a.Id == apuDefinitionId);
        if (apu == null)
            throw new InvalidOperationException($"APU definition '{apuDefinitionId}' not found in input");

        return ApuCalculationEngine.Calculate(apu, input, ct);
    }

    public IReadOnlyList<ApuCalculationResult> CalculateAll(ApuCalculationInput input, CancellationToken ct = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ct.ThrowIfCancellationRequested();

        var cycleValidation = ApuCalculationEngine.ValidateNoCycles(input.ApuDefinitions);
        var results = new List<ApuCalculationResult>();
        var apuIds = input.ApuDefinitions.Select(a => a.Id).ToList();

        foreach (var apuId in apuIds)
        {
            ct.ThrowIfCancellationRequested();
            var apu = input.ApuDefinitions.First(a => a.Id == apuId);
            results.Add(ApuCalculationEngine.Calculate(apu, input, ct));
        }

        return results.AsReadOnly();
    }

    public IReadOnlyList<ApuCalculationResult> CalculateMany(IReadOnlyList<string> apuIds, ApuCalculationInput input, CancellationToken ct = default)
    {
        if (apuIds == null)
            throw new ArgumentNullException(nameof(apuIds));
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ct.ThrowIfCancellationRequested();

        var results = new List<ApuCalculationResult>();
        foreach (var apuId in apuIds)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(Calculate(apuId, input, ct));
        }

        return results.AsReadOnly();
    }

    public ApuCycleValidationResult ValidateAll(ApuCalculationInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        return ApuCalculationEngine.ValidateNoCycles(input.ApuDefinitions);
    }
}
