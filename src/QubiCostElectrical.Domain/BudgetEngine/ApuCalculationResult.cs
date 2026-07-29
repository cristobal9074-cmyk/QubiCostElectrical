namespace QubiCostElectrical;

public sealed record ApuCalculationResult
{
    public string ApuId { get; init; } = "";
    public string ApuCode { get; init; } = "";
    public string ApuDescription { get; init; } = "";
    public string ApuUnit { get; init; } = "";
    public decimal ApuQuantity { get; init; } = 1m;
    public string Currency { get; init; } = "CLP";

    public CostCategoryBreakdown CategoryBreakdown { get; init; } = new();
    public IReadOnlyList<ComponentCostDetail> Components { get; init; } = Array.Empty<ComponentCostDetail>();

    public decimal TotalDirectCost { get; init; }
    public decimal TotalUnitPrice { get; init; }
    public decimal TotalLinePrice { get; init; }

    public ApuCalculationTrace Trace { get; init; } = new();
}

public sealed record CostCategoryBreakdown
{
    public decimal Materials { get; init; }
    public decimal Labor { get; init; }
    public decimal Equipment { get; init; }
    public decimal Tools { get; init; }
    public decimal Subcontract { get; init; }
    public decimal Transport { get; init; }
    public decimal AuxiliaryApus { get; init; }

    public decimal Total => Materials + Labor + Equipment + Tools + Subcontract + Transport + AuxiliaryApus;
}

public sealed record ComponentCostDetail
{
    public string ComponentId { get; init; } = "";
    public string ReferenceId { get; init; } = "";
    public string Description { get; init; } = "";
    public ApuComponentType ComponentType { get; init; }
    public ResourceType ResourceType { get; init; }
    public string Unit { get; init; } = "UN";

    public decimal BasePrice { get; init; }
    public decimal Yield { get; init; } = 1m;
    public decimal WastePercent { get; init; }
    public decimal AdjustmentFactor { get; init; } = 1m;
    public decimal EffectiveQuantity { get; init; }
    public decimal Subtotal { get; init; }
    public string Currency { get; init; } = "CLP";

    public string Explanation { get; init; } = "";
    public IReadOnlyList<ComponentCostDetail>? AuxiliaryComponents { get; init; }
}

public sealed record ApuCalculationTrace
{
    public int ComponentsProcessed { get; init; }
    public int AuxiliaryApusResolved { get; init; }
    public bool CurrencyConversionApplied { get; init; }
    public string SourceCurrency { get; init; } = "";
    public decimal? ExchangeRateUsed { get; init; }
    public decimal? OverrideLaborRateUsed { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
}
