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
    private decimal costoMat;
    private decimal costoMo;
    private decimal costoEquipo;
    private decimal costoSc;
    private decimal costoHerr;
    private decimal costoDirecto;
    private decimal imprevistos;
    private decimal precioUnit;
    private decimal ventaUnit;
    private decimal totalPartida;

    // Alertas
    private string alertText = "";
    private bool hasAlerts;
}
