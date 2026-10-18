using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>
/// Cockpit de decisión del Resumen: el veredicto de una línea, acciones rápidas,
/// búsqueda reactiva en partidas críticas y navegación de tarjetas KPI ejecutivas.
/// Vive en su propio partial para respetar el límite de líneas por tipo.
/// </summary>
public sealed partial class DashboardViewModel
{
    private int blockingAlertCount;
    private string topPartidasSearchText = "";

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

    /// <summary>Texto de búsqueda reactiva en la grilla de partidas críticas del cockpit.</summary>
    public string TopPartidasSearchText
    {
        get => topPartidasSearchText;
        set
        {
            if (SetProperty(ref topPartidasSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredCockpitPartidas));
                OnPropertyChanged(nameof(HasTopPartidasSearchText));
                OnPropertyChanged(nameof(FilteredCockpitPartidasCountLabel));
            }
        }
    }

    public bool HasTopPartidasSearchText => !string.IsNullOrWhiteSpace(topPartidasSearchText);

    public void ClearTopPartidasSearch() => TopPartidasSearchText = "";

    public ICommand ClearTopPartidasSearchCommand => new RelayCommand(ClearTopPartidasSearch);

    /// <summary>
    /// Partidas críticas filtradas tanto por clase Pareto (A/B/C) como por búsqueda de texto instantánea.
    /// </summary>
    public IEnumerable<DashboardPartidaVm> FilteredCockpitPartidas
    {
        get
        {
            var query = (IEnumerable<DashboardPartidaVm>)TopPartidas;
            if (!string.IsNullOrEmpty(SelectedCategoryFilter))
                query = query.Where(p => p.Category == SelectedCategoryFilter);
            if (!string.IsNullOrEmpty(SelectedParetoFilter))
                query = query.Where(p => p.ParetoClass == SelectedParetoFilter);
            if (!string.IsNullOrWhiteSpace(topPartidasSearchText))
            {
                var term = topPartidasSearchText.Trim();
                query = query.Where(p =>
                    (p.Item?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Partida?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            return query;
        }
    }

    public string FilteredCockpitPartidasCountLabel
    {
        get
        {
            var total = TopPartidas.Count;
            var current = FilteredCockpitPartidas.Count();
            return current == total ? $"{total} partidas" : $"{current} de {total}";
        }
    }

    /// <summary>Comando rápido para recalcular todas las métricas del cockpit en tiempo real.</summary>
    public ICommand RecalculateDashboardCommand => new RelayCommand(() =>
    {
        Load();
        shell.RefreshChrome();
    });

    /// <summary>Comando rápido para guardar el proyecto actual directamente desde el cockpit.</summary>
    public ICommand SaveProjectQuickCommand => new RelayCommand(async () =>
    {
        await shell.SaveProjectAsync();
        Load();
    });

    /// <summary>Navegación directa al Itemizado desde la barra de accesos rápidos del cockpit.</summary>
    public ICommand GoToItemizadoDirectCommand => new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized));

    /// <summary>Navegación directa a la Cubicación desde el cockpit.</summary>
    public ICommand GoToTakeoffDirectCommand => new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff));

    /// <summary>Navegación directa a APU desde el cockpit.</summary>
    public ICommand GoToApuDirectCommand => new RelayCommand(() => shell.RequestNavigate(ShellRoute.Apu));

    /// <summary>Navegación directa al módulo de Costos desde el cockpit.</summary>
    public ICommand GoToCostoDirectCommand => new RelayCommand(() => shell.RequestNavigate(ShellRoute.Costo));

    /// <summary>Navegación contextual al hacer clic en cualquier tarjeta de KPI ejecutivo.</summary>
    public ICommand NavigateKpiCardCommand => new RelayCommand<DashboardKpiCardVm>(kpi =>
    {
        if (kpi == null) return;
        var title = kpi.Title.ToUpperInvariant();
        if (title.Contains("VENTA") || title.Contains("PRECIO"))
            shell.RequestNavigate(ShellRoute.Itemized);
        else if (title.Contains("LISTAS") || title.Contains("PARTIDAS"))
            shell.RequestNavigate(ShellRoute.Review);
        else if (title.Contains("M²"))
            shell.RequestNavigate(ShellRoute.Project);
        else if (title.Contains("RIESGO") || title.Contains("CONCENTRACIÓN"))
            shell.RequestNavigate(ShellRoute.Review);
        else if (title.Contains("MARGEN") || title.Contains("COSTO"))
            shell.RequestNavigate(ShellRoute.Costo);
        else
            shell.RequestNavigate(ShellRoute.Itemized);
    });

    /// <summary>Ubicación del proyecto o fallback.</summary>
    public string ProjectLocationLabel => string.IsNullOrWhiteSpace(session.CurrentLocation) ? "Sin ubicación" : session.CurrentLocation;

    /// <summary>Estado del presupuesto (Borrador, En Estudio, Presentado, etc.).</summary>
    public string ProjectStatusLabel => string.IsNullOrWhiteSpace(session.CurrentBudgetStatus) ? "Borrador" : session.CurrentBudgetStatus;

    /// <summary>Etiqueta de moneda base con valor UF actual.</summary>
    public string ProjectCurrencyDisplay => session.ProjectCurrency == "CLP"
        ? "Moneda: CLP ($)"
        : $"{session.ProjectCurrency} · UF ${session.ValorUfClp:N0}";

    /// <summary>Estado del bloqueo de colaboración multiusuario.</summary>
    public string ProjectLockStatusDisplay => session.IsReadOnly
        ? (string.IsNullOrWhiteSpace(session.ReadOnlyReason) ? "Solo Lectura" : $"Solo Lectura ({session.ReadOnlyReason})")
        : "Editor Exclusivo";

    /// <summary>Indica si el proyecto abierto se encuentra en modo sólo lectura.</summary>
    public bool IsProjectReadOnly => session.IsReadOnly;
}
