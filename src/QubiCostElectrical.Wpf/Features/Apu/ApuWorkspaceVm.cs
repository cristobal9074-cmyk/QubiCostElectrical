using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
// JsonSerializerOptions used for APU discard baseline (see Methods).
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed partial class ApuWorkspaceVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IApuService apuSvc;
    private readonly ConjuntoElectricoService conjuntoSvc = new();
    private readonly IApuTemplateStore? templateStore;
    private readonly IProjectWorkspaceService workspace;
    private readonly WpfUndoService undo;
    private string templateStoreStatusText = "";

    private readonly List<ApuPartidaVm> allPartidas = new();
    private ApuPartidaVm? selectedPartida;
    private ApuRecursoVm? selectedRecurso;
    private ApuRow? currentApu;
    private string partidaFilter = "";
    private bool showEmptyState = true;
    private bool hasPendingChanges;
    private bool isLoading;
    private System.Windows.Threading.DispatcherTimer? filterDebounceTimer;
    private bool pendingUndoForEdit;
    /// <summary>Snapshot profundo de session.Apus al Load/Refresh/Apply; restaura en Discard.</summary>
    private List<ApuRow>? baselineApus;

    // Resumen del APU seleccionado
    private decimal costoMat, costoMo, costoEquipo, costoSc, costoHerr, costoDirecto, imprevistos, precioUnit, ventaUnit, totalPartida;
    public ApuCostDistributionCoordinator CostDistribution { get; } = new();

    // Alertas
    private string alertText = "";
    private bool hasAlerts;

    // Autocompletado e IntelliSense
    private readonly ApuAutocompleteEngine autocompleteEngine;
    private ObservableCollection<ApuAutocompleteSuggestionVm> autocompleteResults = new();
    private bool autocompleteVisible;
    private ApuAutocompleteSuggestionVm? selectedAutocompleteItem;
    private string autocompleteStatusText = "";
    private System.Threading.CancellationTokenSource? autocompleteCts;
}
