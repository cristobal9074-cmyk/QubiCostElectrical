namespace QubiCostElectrical;

public interface IVersionWorkflowService
{
    Result EnviarARevision(BudgetProjectSession session, string versionId, string usuario, string comentarioEnvio, string versionBaseId, decimal impactoUf);
    Result IniciarRevision(BudgetProjectSession session, string versionId, string usuario);
    Result AprobarVersion(BudgetProjectSession session, string versionId, string usuario, string comentario);
    Result RechazarVersion(BudgetProjectSession session, string versionId, string usuario, string motivo);
    Result MarcarObsoleta(BudgetProjectSession session, string versionId, string usuario);
    bool VerificarIntegridad(BudgetVersionRow version);
    SodCheckResult CheckApprovalSod(BudgetVersionRow version, string userEmail);
}
