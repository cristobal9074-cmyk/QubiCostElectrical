using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>
/// Cockpit de decisión del Resumen: el veredicto de una línea y la acción que lo resuelve.
/// Vive en su propio partial porque el gate de arquitectura mide el tipo completo
/// (<c>DashboardViewModel</c> + <c>.Methods</c>) y ese presupuesto está agotado.
/// El número que gobierna todo esto es el mismo del contrato de preparación: no hay un segundo
/// criterio de "listo para ofertar".
/// </summary>
public sealed partial class DashboardViewModel
{
    private int blockingAlertCount;

    /// <summary>Alertas bloqueantes del contrato de preparación (misma fuente que el KPI y el anillo).</summary>
    public int BlockingAlertCount
    {
        get => blockingAlertCount;
        private set
        {
            if (SetProperty(ref blockingAlertCount, value))
            {
                OnPropertyChanged(nameof(VerdictActionLabel));
                OnPropertyChanged(nameof(BlockingAlertLabel));
                OnPropertyChanged(nameof(HasBlockingAlerts));
            }
        }
    }

    /// <summary>
    /// Etiqueta del botón principal del veredicto. Con alertas bloqueantes deja de ser un genérico
    /// "Ver revisión" y nombra la acción concreta que desbloquea la oferta; el módulo de Revisión
    /// entra en modo War Room y lista esos bloqueos.
    /// </summary>
    public string VerdictActionLabel => BlockingAlertCount > 0
        ? $"Resolver {DashboardCockpitComposer.BlockingLabel(BlockingAlertCount)}"
        : "Ver revisión";

    /// <summary>Chip del veredicto con concordancia singular/plural.</summary>
    public string BlockingAlertLabel => DashboardCockpitComposer.BlockingLabel(BlockingAlertCount);
}
