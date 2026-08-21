namespace QubiCostElectrical;

/// <summary>Forma comercial de valorizar una partida. El valor cero conserva el comportamiento histórico.</summary>
public enum CommercialCostingModel
{
    ApuDirectCostWithCommercialFactors = 0,
    UnitPrice = 1,
    LumpSum = 2,
    TimeAndMaterials = 3
}

public enum CommercialProfile
{
    StandardConstructor = 0,
    ElectricalSubcontract = 1,
    PrivateClient = 2,
    PreliminaryBudget = 3,
    CompetitiveOffer = 4
}

public sealed class CommercialPartidaRule
{
    public Guid PartidaId { get; set; }
    public bool UseProjectModel { get; set; } = true;
    public CommercialCostingModel Model { get; set; } = CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public decimal UnitPriceClp { get; set; }
    public decimal LumpSumClp { get; set; }
    public decimal LaborHours { get; set; }
    public decimal LaborHourlyRateClp { get; set; }
    public decimal MaterialsClp { get; set; }
    public decimal ReimbursableExpensesClp { get; set; }
    public string InternalNote { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = AppClock.Now;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PartidaId == Guid.Empty) errors.Add("La excepción comercial debe pertenecer a una partida.");
        if (UseProjectModel) return errors;
        switch (Model)
        {
            case CommercialCostingModel.UnitPrice when UnitPriceClp <= 0m:
                errors.Add("Precio unitario: ingresa un valor mayor que cero."); break;
            case CommercialCostingModel.LumpSum when LumpSumClp <= 0m:
                errors.Add("Precio global: ingresa un monto mayor que cero."); break;
            case CommercialCostingModel.TimeAndMaterials:
                if (LaborHours <= 0m) errors.Add("Time & Materials: ingresa horas mayores que cero.");
                if (LaborHourlyRateClp <= 0m) errors.Add("Time & Materials: ingresa una tarifa hora mayor que cero.");
                if (MaterialsClp < 0m || ReimbursableExpensesClp < 0m) errors.Add("Time & Materials: materiales y gastos no pueden ser negativos.");
                break;
        }
        return errors;
    }

    public CommercialPartidaRule Clone() => (CommercialPartidaRule)MemberwiseClone();
}

public static class CommercialCostingCatalog
{
    public static string DisplayName(this CommercialCostingModel model) => model switch
    {
        CommercialCostingModel.UnitPrice => "Precio unitario",
        CommercialCostingModel.LumpSum => "Precio global",
        CommercialCostingModel.TimeAndMaterials => "Time & Materials",
        _ => "APU / Costo directo con factores comerciales"
    };

    public static string DisplayName(this CommercialProfile profile) => profile switch
    {
        CommercialProfile.ElectricalSubcontract => "Subcontrato eléctrico",
        CommercialProfile.PrivateClient => "Cliente privado",
        CommercialProfile.PreliminaryBudget => "Presupuesto preliminar",
        CommercialProfile.CompetitiveOffer => "Oferta competitiva",
        _ => "Constructora estándar"
    };
}
