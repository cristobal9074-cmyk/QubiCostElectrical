using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed partial class ExportViewModel
{
    private readonly ExportPriceFreezePolicy priceFreezePolicy = new();
    private readonly ProjectPriceFreezeService priceFreezeService = new();

    private async Task<bool> EnsureExportAllowedAsync()
    {
        if (!permissions.CanExportDocuments)
        {
            var roleMsg = UserRolePermissions.GetRoleReadOnlyMessage(permissions.CurrentRole);
            await Application.Current.Dispatcher.InvokeAsync(() =>
                WpfDialogService.Warning(
                    "Exportar",
                    string.IsNullOrWhiteSpace(roleMsg)
                        ? "Su rol no permite exportar documentos."
                        : roleMsg));
            return false;
        }

        if (session.IsReadOnly)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                WpfDialogService.Warning(
                    "Exportar",
                    "El proyecto está en solo lectura; no se puede exportar."));
            return false;
        }

        if (!priceFreezePolicy.RequiresConfirmation(session.ProjectInfo))
            return true;

        var decision = await Application.Current.Dispatcher.InvokeAsync(
            () => WpfDialogService.PromptExportWithoutFrozenPrices());

        switch (decision)
        {
            case ExportPriceFreezeDecision.FreezeAndProceed:
                priceFreezeService.Freeze(
                    session.Data,
                    session.ProjectInfo.UsuarioNombre ?? string.Empty);
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                return true;
            case ExportPriceFreezeDecision.ProceedWithRisk:
                audit.RecordLifecycle("ExportSinCongelar", "Exportación sin congelar precios del proyecto");
                return true;
            default:
                return false;
        }
    }
}
