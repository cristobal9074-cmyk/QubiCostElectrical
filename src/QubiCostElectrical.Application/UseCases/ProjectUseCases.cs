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

    public ProjectUseCases(IBudgetCalculationService budgetCalculation, IValidationService validation, IFinalReviewService finalReview)
    {
        BudgetCalculation = budgetCalculation;
        Validation = validation;
        FinalReview = finalReview;
    }

    public CostBreakdown Calculate(ProjectFileData data)
    {
        data ??= new ProjectFileData();
        return new BudgetEngine().Calculate(
            data.Cubicacion,
            ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(data.Proyecto), data),
            data.Apus,
            data.Itemizado);
    }
}
