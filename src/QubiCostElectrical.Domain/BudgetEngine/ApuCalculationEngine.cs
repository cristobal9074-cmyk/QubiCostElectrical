using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public static class ApuCalculationEngine
{
    public static ApuCycleValidationResult ValidateNoCycles(IReadOnlyList<ApuDefinition> definitions)
    {
        if (definitions == null || definitions.Count == 0)
            return new ApuCycleValidationResult();

        var detector = new ApuCycleDetector();
        var affected = new List<string>();
        var cyclePaths = new List<IReadOnlyList<string>>();

        foreach (var apu in definitions)
        {
            var path = detector.FindCyclePath(apu.Id, apu.Components, definitions);
            if (path.Count > 0)
            {
                affected.Add(apu.Id);
                cyclePaths.Add(path);
            }
        }

        return new ApuCycleValidationResult
        {
            HasCycles = affected.Count > 0,
            AffectedApuIds = affected.AsReadOnly(),
            CyclePaths = cyclePaths.AsReadOnly()
        };
    }

    public static ApuCalculationResult Calculate(
        ApuDefinition apu,
        ApuCalculationInput input,
        CancellationToken ct = default)
    {
        if (apu == null) throw new ArgumentNullException(nameof(apu));
        if (input == null) throw new ArgumentNullException(nameof(input));
        ct.ThrowIfCancellationRequested();

        var detector = new ApuCycleDetector();
        var visited = new HashSet<string>();
        var inProgress = new HashSet<string>();
        if (detector.HasCycle(apu.Id, apu.Components, input.ApuDefinitions, visited, inProgress))
        {
            return new ApuCalculationResult
            {
                ApuId = apu.Id,
                ApuCode = apu.Code,
                ApuDescription = apu.Description,
                ApuUnit = apu.Unit,
                ApuQuantity = apu.Quantity,
                Trace = new ApuCalculationTrace
                {
                    Steps = new[] { $"CYCLE DETECTED: APU '{apu.Code}' has a circular reference" }
                }
            };
        }

        var steps = new List<string>();
        var resolving = new HashSet<string>();
        var components = new List<ComponentCostDetail>();

        decimal matTotal = 0m, laborTotal = 0m, equipTotal = 0m;
        decimal toolsTotal = 0m, subTotal = 0m, transportTotal = 0m, auxTotal = 0m;

        var start = DateTime.UtcNow;

        foreach (var component in apu.Components.OrderBy(c => c.SortOrder))
        {
            ct.ThrowIfCancellationRequested();
            var detail = CalculateComponent(component, input, resolving, steps, ct);
            components.Add(detail);
            steps.Add(detail.Explanation);

            switch (detail.ResourceType)
            {
                case ResourceType.Material: matTotal += detail.Subtotal; break;
                case ResourceType.Labor: laborTotal += detail.Subtotal; break;
                case ResourceType.Equipment: equipTotal += detail.Subtotal; break;
                case ResourceType.Tool: toolsTotal += detail.Subtotal; break;
                case ResourceType.Subcontract: subTotal += detail.Subtotal; break;
                case ResourceType.Transport: transportTotal += detail.Subtotal; break;
                case ResourceType.Auxiliary: auxTotal += detail.Subtotal; break;
            }
        }

        matTotal = PrecisionPolicy.RoundImporte(matTotal);
        laborTotal = PrecisionPolicy.RoundImporte(laborTotal);
        equipTotal = PrecisionPolicy.RoundImporte(equipTotal);
        toolsTotal = PrecisionPolicy.RoundImporte(toolsTotal);
        subTotal = PrecisionPolicy.RoundImporte(subTotal);
        transportTotal = PrecisionPolicy.RoundImporte(transportTotal);
        auxTotal = PrecisionPolicy.RoundImporte(auxTotal);

        var totalDirect = PrecisionPolicy.RoundImporte(
            matTotal + laborTotal + equipTotal + toolsTotal + subTotal + transportTotal + auxTotal);

        var totalUnit = apu.Quantity > 0m
            ? PrecisionPolicy.RoundPrecioUnitario(totalDirect / apu.Quantity)
            : 0m;

        var elapsed = DateTime.UtcNow - start;

        return new ApuCalculationResult
        {
            ApuId = apu.Id,
            ApuCode = apu.Code,
            ApuDescription = apu.Description,
            ApuUnit = apu.Unit,
            ApuQuantity = apu.Quantity,
            Currency = "CLP",
            CategoryBreakdown = new CostCategoryBreakdown
            {
                Materials = matTotal,
                Labor = laborTotal,
                Equipment = equipTotal,
                Tools = toolsTotal,
                Subcontract = subTotal,
                Transport = transportTotal,
                AuxiliaryApus = auxTotal
            },
            Components = components.AsReadOnly(),
            TotalDirectCost = totalDirect,
            TotalUnitPrice = totalUnit,
            TotalLinePrice = PrecisionPolicy.RoundImporte(totalDirect),
            Trace = new ApuCalculationTrace
            {
                ComponentsProcessed = components.Count,
                AuxiliaryApusResolved = components.Count(c => c.ComponentType == ApuComponentType.AuxiliaryApu),
                CurrencyConversionApplied = !string.IsNullOrEmpty(input.TargetCurrency),
                Elapsed = elapsed,
                Steps = steps.AsReadOnly()
            }
        };
    }

    private static ComponentCostDetail CalculateComponent(
        ApuComponent component,
        ApuCalculationInput input,
        HashSet<string> resolving,
        List<string> steps,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (component.ComponentType == ApuComponentType.AuxiliaryApu)
        {
            return CalculateAuxiliaryComponent(component, input, resolving, steps, ct);
        }

        var resource = input.Resources.FirstOrDefault(r => r.Id == component.ReferenceId);

        if (resource == null)
        {
            return new ComponentCostDetail
            {
                ComponentId = component.Id,
                ReferenceId = component.ReferenceId,
                Description = component.Description,
                ComponentType = ApuComponentType.Resource,
                ResourceType = ResourceType.Other,
                Unit = component.Unit,
                Yield = component.Yield,
                WastePercent = component.WastePercent,
                AdjustmentFactor = component.AdjustmentFactor,
                Explanation = $"Resource '{component.ReferenceId}' not found in input"
            };
        }

        decimal basePrice = resource.EffectivePrice;
        string priceCurrency = resource.Currency;

        decimal convertedPrice = ConvertCurrency(basePrice, priceCurrency, "CLP", input);

        if (resource.Type == ResourceType.Labor && input.OverrideLaborRate.HasValue)
        {
            convertedPrice = input.OverrideLaborRate.Value;
        }

        decimal wasteFactor = 1m + component.WastePercent / 100m;
        decimal effectiveQty = PrecisionPolicy.RoundCantidad(
            component.Yield * wasteFactor * component.AdjustmentFactor);

        decimal subtotal = PrecisionPolicy.RoundImporte(convertedPrice * effectiveQty);

        string explanation = $"{resource.Code}: {PrecisionPolicy.RoundPrecioUnitario(convertedPrice)} × {effectiveQty} (yield={component.Yield}, waste={component.WastePercent}%, factor={component.AdjustmentFactor}) = {subtotal}";

        return new ComponentCostDetail
        {
            ComponentId = component.Id,
            ReferenceId = component.ReferenceId,
            Description = resource.Description,
            ComponentType = ApuComponentType.Resource,
            ResourceType = resource.Type,
            Unit = component.Unit,
            BasePrice = PrecisionPolicy.RoundPrecioUnitario(convertedPrice),
            Yield = component.Yield,
            WastePercent = component.WastePercent,
            AdjustmentFactor = component.AdjustmentFactor,
            EffectiveQuantity = effectiveQty,
            Subtotal = subtotal,
            Currency = "CLP",
            Explanation = explanation
        };
    }

    private static ComponentCostDetail CalculateAuxiliaryComponent(
        ApuComponent component,
        ApuCalculationInput input,
        HashSet<string> resolving,
        List<string> steps,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (resolving.Contains(component.ReferenceId))
        {
            return new ComponentCostDetail
            {
                ComponentId = component.Id,
                ReferenceId = component.ReferenceId,
                Description = component.Description,
                ComponentType = ApuComponentType.AuxiliaryApu,
                ResourceType = ResourceType.Auxiliary,
                Explanation = $"CYCLE: auxiliary APU '{component.ReferenceId}' is already being resolved"
            };
        }

        var childApu = input.ApuDefinitions.FirstOrDefault(a => a.Id == component.ReferenceId);
        if (childApu == null)
        {
            return new ComponentCostDetail
            {
                ComponentId = component.Id,
                ReferenceId = component.ReferenceId,
                Description = component.Description,
                ComponentType = ApuComponentType.AuxiliaryApu,
                ResourceType = ResourceType.Auxiliary,
                Explanation = $"Auxiliary APU '{component.ReferenceId}' not found in definitions"
            };
        }

        resolving.Add(component.ReferenceId);
        var childResult = Calculate(childApu, input, ct);
        resolving.Remove(component.ReferenceId);

        decimal wasteFactor = 1m + component.WastePercent / 100m;
        decimal effectiveQty = PrecisionPolicy.RoundCantidad(
            component.Yield * wasteFactor * component.AdjustmentFactor);

        decimal childUnitPrice = childResult.TotalUnitPrice;
        decimal subtotal = PrecisionPolicy.RoundImporte(childUnitPrice * effectiveQty);

        string explanation = $"Aux APU '{childApu.Code}': unit={PrecisionPolicy.RoundPrecioUnitario(childUnitPrice)} × {effectiveQty} (yield={component.Yield}, waste={component.WastePercent}%) = {subtotal}";

        return new ComponentCostDetail
        {
            ComponentId = component.Id,
            ReferenceId = component.ReferenceId,
            Description = childApu.Description,
            ComponentType = ApuComponentType.AuxiliaryApu,
            ResourceType = ResourceType.Auxiliary,
            Unit = childApu.Unit,
            BasePrice = PrecisionPolicy.RoundPrecioUnitario(childUnitPrice),
            Yield = component.Yield,
            WastePercent = component.WastePercent,
            AdjustmentFactor = component.AdjustmentFactor,
            EffectiveQuantity = effectiveQty,
            Subtotal = subtotal,
            Currency = "CLP",
            Explanation = explanation,
            AuxiliaryComponents = childResult.Components
        };
    }

    private static decimal ConvertCurrency(decimal amount, string from, string to, ApuCalculationInput input)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
            return amount;

        var rate = input.ExchangeRates
            .Where(r => r.SourceCurrency == from && r.TargetCurrency == to)
            .OrderByDescending(r => r.Date)
            .FirstOrDefault();

        if (rate == null)
            return amount;

        return PrecisionPolicy.RoundPrecioUnitario(amount * rate.Rate);
    }
}
