namespace QubiCostElectrical;

/// <summary>
/// Fachada de casos de uso para ir moviendo lógica desde MainForm sin romper la UI actual.
/// Las siguientes versiones deben agregar aquí: CrearProyecto, AbrirProyecto, ExportarOferta, ValidarProyecto.
/// </summary>
public sealed class ProjectUseCases
{
    public IBudgetCalculationService BudgetCalculation { get; }
    public IValidationService Validation { get; }
    public IFinalReviewService FinalReview { get; }

    private readonly System.Func<BudgetEngine> _engineFactory;

    public ProjectUseCases(IBudgetCalculationService budgetCalculation, IValidationService validation, IFinalReviewService finalReview, System.Func<BudgetEngine>? engineFactory = null)
    {
        BudgetCalculation = budgetCalculation;
        Validation = validation;
        FinalReview = finalReview;
        _engineFactory = engineFactory ?? (static () => new BudgetEngine());
    }

    public CostBreakdown Calculate(ProjectFileData data)
    {
        data ??= new ProjectFileData();
        return _engineFactory().Calculate(
            data.Cubicacion,
            ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(data.Proyecto), data),
            data.Apus,
            data.Itemizado);
    }
}
