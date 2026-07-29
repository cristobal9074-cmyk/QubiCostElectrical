namespace QubiCostElectrical;

/// <summary>
/// Contrato liviano para validaciones de presupuesto sin depender de WinForms.
/// </summary>
public interface IBudgetValidationRule
{
    string Code { get; }
    IReadOnlyList<string> Validate(ProjectFileData project);
}
