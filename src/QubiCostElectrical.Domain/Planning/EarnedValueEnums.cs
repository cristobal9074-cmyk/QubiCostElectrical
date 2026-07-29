namespace QubiCostElectrical;

/// <summary>Método de cálculo de EV (PMI).</summary>
public enum EarnedValueMethod
{
    PercentComplete,
    PhysicalPercentComplete,
    MilestoneWeighted,
    UnitsComplete,
    FixedFormula50_50,
    FixedFormula0_100
}

/// <summary>Método de estimación EAC.</summary>
public enum EacMethod
{
    /// <summary>EAC = AC + (BAC − EV) — desempeño futuro = planificado.</summary>
    AcPlusRemainingBudget,
    /// <summary>EAC = BAC / CPI — desempeño futuro = CPI actual.</summary>
    BacOverCpi,
    /// <summary>EAC = AC + (BAC − EV) / (CPI × SPI) — combina costo y plazo.</summary>
    AcPlusRemainingOverCpiSpi
}

/// <summary>Semáforo de interpretación EVM.</summary>
public enum EvmTrafficLight
{
    Green,
    Amber,
    Red,
    Neutral,
    Unknown
}
