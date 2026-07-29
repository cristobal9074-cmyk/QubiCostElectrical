namespace QubiCostElectrical;

/// <summary>Política explícita de ponderación del avance global del cronograma.</summary>
public enum GlobalProgressWeighting
{
    ByPlannedCost,
    ByDuration,
    ByQuantity,
    ByManualWeight,
    ByMilestones
}
