namespace QubiCostElectrical;

public enum ExportPriceFreezeDecision
{
    Cancel,
    FreezeAndProceed,
    ProceedWithRisk
}

public sealed class ExportPriceFreezePolicy
{
    private readonly ProjectPriceFreezeService freezeService;

    public ExportPriceFreezePolicy(ProjectPriceFreezeService? freezeService = null)
    {
        this.freezeService = freezeService ?? new ProjectPriceFreezeService();
    }

    public bool IsFrozen(ProjectInfoRow? proyecto) => freezeService.IsFrozen(proyecto);

    public bool RequiresConfirmation(ProjectInfoRow? proyecto) => !IsFrozen(proyecto);
}
