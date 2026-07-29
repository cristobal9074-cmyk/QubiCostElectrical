using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ContractAdjustment
{
    public AdjustmentId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public int Period { get; set; }
    public DateTime BaseDate { get; set; }
    public DateTime CalculationDate { get; set; }
    public AdjustmentType AdjustmentType { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal AdjustedAmount { get; set; }
    public decimal Difference => AdjustedAmount - BaseAmount;
    public AdjustmentStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public int VisibleNumber { get; set; }
}

public sealed class AdjustmentFormula
{
    public AdjustmentFormulaId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FormulaComponent> Components { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class FormulaComponent
{
    public string Label { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal BaseIndex { get; set; }
    public decimal CurrentIndex { get; set; }
}

public sealed class AdjustmentIndex
{
    public AdjustmentIndexId Id { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public string Source { get; set; } = string.Empty;
}
