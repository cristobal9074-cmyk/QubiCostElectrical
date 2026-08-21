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
        if (!priceFreezePolicy.RequiresConfirmation(host.Session.ProjectInfo))
            return true;

        var decision = await Application.Current.Dispatcher.InvokeAsync(
            () => WpfDialogService.PromptExportWithoutFrozenPrices());

        switch (decision)
        {
            case ExportPriceFreezeDecision.FreezeAndProceed:
                priceFreezeService.Freeze(
                    host.Session.Data,
                    host.Session.ProjectInfo.UsuarioNombre ?? string.Empty);
                host.Session.MarkDirty("project_info");
                return true;
            case ExportPriceFreezeDecision.ProceedWithRisk:
                host.Audit.RecordLifecycle("ExportSinCongelar", "Exportación sin congelar precios del proyecto");
                return true;
            default:
                return false;
        }
    }
}
