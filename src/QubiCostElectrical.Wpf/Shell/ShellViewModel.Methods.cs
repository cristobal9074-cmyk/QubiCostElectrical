using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.CommandPalette;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Itemizado;

namespace QubiCostElectrical.Wpf.Shell;

public sealed partial class ShellViewModel : ViewModelBase, IShellNavigationHost, IDisposable
{
    private readonly WpfAppHost host;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly WpfUndoService undo;
    private readonly ProjectCollaborationLockService projectLock;
    private readonly IPermissionService permissions;
    private readonly ProjectAuditRecorder audit;
    private readonly IAuthService auth;
    private readonly DashboardPresenter dashboard;
    private readonly IProjectEditWorkspaceService projectEdit;
    private readonly IDocumentsWorkspaceService documents;
    private readonly IPriceBankStore priceBankStore;
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly AutoSaveService autoSave;
    private readonly RecentProjectsService recentProjects;
    private readonly ObservableCollection<RecentProjectInfo> recentProjectItems = new();
    private ShellProjectLifecycle projectLifecycle = null!;
    private SessionChangeCoalescer? chromeChangeCoalescer;
    private bool isReadOnly;
    private bool isBusy;
    private ShellRoute currentRoute = ShellRoute.Dashboard;
    private UserControl? currentView;
    private object? currentPageVm;
    private string breadcrumbProject = "Sin proyecto";
    private string pageTitle = "Resumen";
    private string breadcrumbGroup = "Proyecto";
    private string saveStateText = "Sin proyecto abierto";
    private string alertCount = "0";
    private string statusChip = "—";
    private string versionChip = "—";
    private string currencyChip = "—";
    private string tickerCostoDirecto = "—";
    private string tickerMargen = "—";
    private string tickerTotalOfertaUf = "—";
    private string tickerTotalOfertaClp = "";
    private string searchText = "";
    private bool isSearchPaletteOpen;
    private bool isProjectPickerOpen;
    private bool isUserMenuOpen;
    private QuickSearchItemVm? selectedQuickSearchItem;
    private readonly ObservableCollection<QuickSearchItemVm> quickSearchResults = new();

    internal Func<ThreeWayResult> ShowCloseProjectDialog = () =>
        WpfDialogService.ConfirmSaveDiscardCancel("Cerrar proyecto",
            "El proyecto tiene cambios sin guardar.\n¿Desea guardarlos antes de cerrar?");

    /// <summary>Diálogo al cerrar la app (X / Alt+F4) con cambios pendientes.</summary>
    internal Func<ThreeWayResult> ShowExitAppDialog = () =>
        WpfDialogService.ConfirmSaveDiscardCancel("Salir",
            "El proyecto tiene cambios sin guardar.\n¿Desea guardarlos antes de salir?");

    internal Func<ThreeWayResult> ShowLeavePageDialog = () =>
        WpfDialogService.ConfirmSaveDiscardCancel("Cambios sin guardar",
            "Hay cambios sin guardar en esta pantalla.\n¿Desea guardarlos antes de salir?");

    internal Func<bool> ShowPendingChangesOnNewDialog = () =>
        WpfDialogService.Confirm("Cambios sin guardar",
            "Hay cambios sin guardar. ¿Desea descartarlos y crear un nuevo proyecto?", isWarning: true);

    internal Func<bool> ShowPendingChangesOnOpenDialog = () =>
        WpfDialogService.Confirm("Cambios sin guardar",
            "Hay cambios sin guardar. ¿Desea descartarlos y abrir otro proyecto?", isWarning: true);

    internal Action<string> ShowMissingFileWarning = path =>
        WpfDialogService.Warning("Archivo no encontrado",
            $"El proyecto ya no existe en:\n{path}\n\nFue eliminado de la lista de recientes.");

    internal Action PostCloseNavigate = null!;

    // session/undo viven toda la vida del WpfAppHost — sin desuscribirse, cada
    // ShellViewModel nuevo (un ciclo de Cerrar sesión crea uno) deja la instancia vieja y su
    // AutoSaveService (con su propio Timer interno) retenidos para siempre. Mismo patrón de bug
    // ya corregido en otras 8 pantallas, ver SessionChangedSubscriptionLeakTests.cs.
    private readonly PropertyChangedEventHandler autoSavePropertyChangedHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedRefreshChromeHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionSavedHandler;
    private readonly PropertyChangedEventHandler undoServicePropertyChangedHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedSyncReadOnlyHandler;
    private readonly EventHandler projectLockLostHandler;
    private bool disposed;

    public ShellViewModel(WpfAppHost host)
        : this(host ?? throw new ArgumentNullException(nameof(host)),
               host.Session, host.ProjectWorkspace, host.UndoService, host.ProjectLock, host.Permissions,
               host.Audit, host.Auth, host.Dashboard, host.ProjectEdit, host.Documents,
               host.Services.PriceBankStore, host.Services.BudgetCalculation)
    {
    }

    public ShellViewModel(
        WpfAppHost host,
        BudgetProjectSession session,
        IProjectWorkspaceService projectWorkspace,
        WpfUndoService undo,
        ProjectCollaborationLockService projectLock,
        IPermissionService permissions,
        ProjectAuditRecorder audit,
        IAuthService auth,
        DashboardPresenter dashboard,
        IProjectEditWorkspaceService projectEdit,
        IDocumentsWorkspaceService documents,
        IPriceBankStore priceBankStore,
        IBudgetCalculationService budgetCalculation)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.undo = undo;
        this.projectLock = projectLock;
        this.permissions = permissions;
        this.audit = audit;
        this.auth = auth;
        this.dashboard = dashboard;
        this.projectEdit = projectEdit;
        this.documents = documents;
        this.priceBankStore = priceBankStore;
        this.budgetCalculation = budgetCalculation;
        autoSave = new AutoSaveService(session, projectWorkspace);
        autoSavePropertyChangedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
        autoSave.PropertyChanged += autoSavePropertyChangedHandler;
        recentProjects = new RecentProjectsService();

        var pageCache = new ShellPageCache();
        navigation = new ShellNavigationCoordinator(
            pageCache,
            session,
            projectWorkspace,
            autoSave,
            TryApplyCurrentPageChanges,
            () => IsReadOnly,
            RefreshContext);
        projectLifecycle = new ShellProjectLifecycle(
            session, projectWorkspace, undo, budgetCalculation, projectEdit, documents,
            projectLock, audit, permissions, autoSave, ClearPageCache);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            chromeChangeCoalescer = new SessionChangeCoalescer(dispatcher, TimeSpan.FromMilliseconds(75), RefreshChrome);

        sessionChangedRefreshChromeHandler = (_, _) =>
        {
            if (chromeChangeCoalescer != null)
                chromeChangeCoalescer.Signal();
            else
                Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
        };
        session.Changed += sessionChangedRefreshChromeHandler;
        sessionSavedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(() => { autoSave.MarkSaved(); OnSaved(); });
        session.Saved += sessionSavedHandler;
        undoServicePropertyChangedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(RefreshUndoRedo);
        undo.PropertyChanged += undoServicePropertyChangedHandler;
        sessionChangedSyncReadOnlyHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(SyncReadOnlyFromSession);
        session.Changed += sessionChangedSyncReadOnlyHandler;
        projectLockLostHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(OnProjectLockLost);
        projectLock.LockLost += projectLockLostHandler;
        RoleSessionSync.Apply(session, permissions.CurrentRole);
        SyncReadOnlyFromSession();
        ProductVersion = $"v{ProductVersionInfo.ProductVersion}";
        RefreshUserContext();
        PostCloseNavigate = () => RequestNavigate(ShellRoute.Dashboard, force: true);
        RefreshChrome();
        RefreshQuickSearchResults();
        RefreshRecentProjects();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        autoSave.PropertyChanged -= autoSavePropertyChangedHandler;
        session.Changed -= sessionChangedRefreshChromeHandler;
        session.Saved -= sessionSavedHandler;
        undo.PropertyChanged -= undoServicePropertyChangedHandler;
        session.Changed -= sessionChangedSyncReadOnlyHandler;
        projectLock.LockLost -= projectLockLostHandler;
        chromeChangeCoalescer?.Dispose();
        autoSave.Dispose();
    }

    private void OnProjectLockLost()
    {
        session.SetLockReadOnly(true, "Otro usuario tomó el control de edición. Solo lectura.");
        autoSave.CancelPendingPersist();
        try
        {
            audit.RecordLifecycle("BloqueoPerdido", session.CurrentProjectPath);
        }
        catch
        {
            // Auditoría best-effort: no bloquear transición a LockLostPending.
        }

        SyncReadOnlyFromSession();
        ShellNotificationService.Instance.ShowWarning(
            "Bloqueo perdido",
            "Otro editor tomó el control. Ya no se auto-guarda el .qce; se conserva una copia local (.autosave). Guarde una copia o reabra en solo lectura.");

        // Preservar trabajo dirty en sidecar sin tocar el .qce.
        _ = autoSave.FlushSidecarAsync();
    }

    public WpfAppHost Host => host;

    public bool IsReadOnly
    {
        get => isReadOnly;
        private set
        {
            if (!SetProperty(ref isReadOnly, value))
                return;
            OnPropertyChanged(nameof(ReadOnlyBannerText));
            OnPropertyChanged(nameof(CollaborationModeLabel));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(CanSaveProject));
            OnPropertyChanged(nameof(CanSaveProjectAs));
            try
            {
                CommandManager.InvalidateRequerySuggested();
            }
            catch
            {
                // Entornos de prueba / headless sin message pump
            }
        }
    }

    public string ReadOnlyBannerText
    {
        get
        {
            if (!session.IsReadOnly && projectLock.CurrentLockPath != null)
                return $"Edición exclusiva · {CollaborationSessionBridge.CurrentUserContext().DisplayName}";

            return string.IsNullOrWhiteSpace(session.ReadOnlyReason)
                ? "El proyecto está en solo lectura."
                : session.ReadOnlyReason;
        }
    }

    public string CollaborationModeLabel
    {
        get
        {
            if (!HasOpenProject) return "";
            return IsReadOnly ? "🔒 Solo lectura" : "✏️ Modo exclusivo (Edición)";
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            OnPropertyChanged(nameof(CanExecuteActions));
            // CanSaveProject/CanSaveProjectAs dependen de IsBusy — sin notificarlos acá, el botón
            // "Guardar" (ligado a CanSaveProject) queda con el valor que WPF evaluó la última vez
            // que SÍ se notificó (solo pasaba al cambiar IsReadOnly), y no se refresca cuando
            // termina de abrirse un proyecto (IsBusy true→false): quedaba gris para siempre.
            OnPropertyChanged(nameof(CanSaveProject));
            OnPropertyChanged(nameof(CanSaveProjectAs));
            if (!value) BusyMessage = string.Empty;
        }
    }

    private string busyMessage = string.Empty;
    public string BusyMessage
    {
        get => busyMessage;
        internal set => SetProperty(ref busyMessage, value);
    }



    public bool CanExecuteActions => !isBusy;
    public bool CanSaveProject => !isBusy && HasOpenProject && !IsReadOnly;
    public bool CanSaveProjectAs => !isBusy && HasOpenProject && !IsReadOnly;

    public string ActiveProjectPath => projectWorkspace.HasOpenProject
        ? session.CurrentProjectPath
        : string.Empty;

    public string ApplicationName => "QUBI COST";
    public string ApplicationSub => "ELECTRICAL";
    public string ProductVersion { get; }

    public string ActiveProjectLabel
    {
        get
        {
            if (!projectWorkspace.HasOpenProject)
                return "Sin proyecto abierto";
            var name = session.ProjectInfo.Nombre;
            return string.IsNullOrWhiteSpace(name) ? "Proyecto sin nombre" : name;
        }
    }

    public bool HasUnsavedChanges => projectWorkspace.HasOpenProject && session.IsDirty;

    public string WindowTitleText
    {
        get
        {
            if (!projectWorkspace.HasOpenProject)
                return "QUBI COST Electrical";
            return HasUnsavedChanges
                ? $"● {ActiveProjectLabel} — QUBI COST Electrical"
                : $"{ActiveProjectLabel} — QUBI COST Electrical";
        }
    }

    public string SaveStateText
    {
        get => saveStateText;
        private set => SetProperty(ref saveStateText, value);
    }

    public string AlertCount
    {
        get => alertCount;
        private set
        {
            if (SetProperty(ref alertCount, value))
                OnPropertyChanged(nameof(HasAlerts));
        }
    }

    public bool HasAlerts => int.TryParse(alertCount, out var count) && count > 0;

    public string UserInitials { get; private set; } = "";
    public string UserDisplayName { get; private set; } = "";
    public string UserEmail { get; private set; } = "";
    public string UserRoleLabel => permissions.RoleDisplayName;

    public string UserStatusLabel => string.IsNullOrWhiteSpace(UserRoleLabel)
        ? UserDisplayName
        : $"{UserDisplayName} · {UserRoleLabel}";

    public string PriceBankSyncLabel
    {
        get
        {
            try
            {
                var count = priceBankStore.Count();
                return count > 0
                    ? $"Banco de precios · {count:N0} materiales"
                    : "Banco de precios vacío";
            }
            catch
            {
                return "Banco de precios no disponible";
            }
        }
    }

    private void SyncReadOnlyFromSession()
    {
        IsReadOnly = session.IsReadOnly;
    }

    public bool IsUserMenuOpen
    {
        get => isUserMenuOpen;
        set => SetProperty(ref isUserMenuOpen, value);
    }

    [RelayCommand]
    private async Task Logout()
    {
        IsUserMenuOpen = false;
        if (!await CloseProjectAsync())
            return;

        auth.Logout();
        SessionManager.ClearSession();

        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var login = new LoginWindow(auth);
            login.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            foreach (Window window in Application.Current.Windows)
            {
                if (window == login)
                    continue;
                if (window is ShellWindow shellWindow)
                    shellWindow.SuppressShutdownOnClose = true;
                window.Close();
            }

            if (login.ShowDialog() == true)
            {
                var shell = new ShellWindow(host);
                Application.Current.MainWindow = shell;
                shell.Show();
            }
            else
            {
                Application.Current.Shutdown();
            }
        });
    }

    public string StatusChip
    {
        get => statusChip;
        private set => SetProperty(ref statusChip, value);
    }

    public string VersionChip
    {
        get => versionChip;
        private set => SetProperty(ref versionChip, value);
    }

    public string CurrencyChip
    {
        get => currencyChip;
        private set => SetProperty(ref currencyChip, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            OnPropertyChanged(nameof(HasSearchText));
            RefreshQuickSearchResults();
            IsSearchPaletteOpen = true;
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(searchText);
    public bool HasQuickSearchResults => quickSearchResults.Count > 0;
    public string ResultCountLabel => quickSearchResults.Count == 1 ? "1 coincidencia" : $"{quickSearchResults.Count} coincidencias";

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public bool IsSearchPaletteOpen
    {
        get => isSearchPaletteOpen;
        set => SetProperty(ref isSearchPaletteOpen, value);
    }

    public bool IsProjectPickerOpen
    {
        get => isProjectPickerOpen;
        set => SetProperty(ref isProjectPickerOpen, value);
    }

    public bool HasOpenProject => projectWorkspace.HasOpenProject;

    public bool CanUndo => undo.CanUndo && !IsReadOnly;
    public bool CanRedo => undo.CanRedo && !IsReadOnly;

    public string UndoToolTip => CanUndo && !string.IsNullOrWhiteSpace(undo.LastUndoDescription)
        ? $"Deshacer: {undo.LastUndoDescription} (Ctrl+Z)"
        : "Deshacer (Ctrl+Z)";

    public string RedoToolTip => CanRedo && !string.IsNullOrWhiteSpace(undo.LastRedoDescription)
        ? $"Rehacer: {undo.LastRedoDescription} (Ctrl+Y)"
        : "Rehacer (Ctrl+Y)";

    public string StatusBarHint => CurrentRoute switch
    {
        ShellRoute.Itemized => "Ins agregar partida · Alt+↑↓ mover · Supr eliminar · Ctrl+V pegar en grilla",
        ShellRoute.Takeoff => "Alt+↑↓ mover filas · Ctrl+V pegar · Tab/Enter navegar celdas · Ctrl+Izq/Der sectores",
        ShellRoute.Apu => "Enter editar · Supr quitar recurso · Generar APU desde cubicación",
        ShellRoute.Review => "Clic en alerta para ir al módulo · Actualizar recalcula revisión",
        ShellRoute.OfferDocuments => "Paso a paso: configurar → vista previa → exportar",
        _ => "Ctrl+P / Ctrl+K búsqueda · Ctrl+S guardar · F1 atajos"
    };

    public void Undo()
    {
        if (undo.Undo(session, out var desc))
        {
            RefreshCurrentPage();
            string label = string.IsNullOrWhiteSpace(desc) ? "Edición" : desc;
            UndoRedoHudCoordinator.Instance.NotifyUndo(label, () => Redo());
        }
    }

    public void Redo()
    {
        if (undo.Redo(session, out var desc))
        {
            RefreshCurrentPage();
            string label = string.IsNullOrWhiteSpace(desc) ? "Edición" : desc;
            UndoRedoHudCoordinator.Instance.NotifyRedo(label, () => Undo());
        }
    }

    private void RefreshUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoToolTip));
        OnPropertyChanged(nameof(RedoToolTip));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    public ObservableCollection<QuickSearchItemVm> QuickSearchResults => quickSearchResults;

    public QuickSearchItemVm? SelectedQuickSearchItem
    {
        get => selectedQuickSearchItem;
        set
        {
            if (!SetProperty(ref selectedQuickSearchItem, value))
                return;
            ExecuteQuickSearchItemCommand.NotifyCanExecuteChanged();
        }
    }

    public ObservableCollection<RecentProjectInfo> RecentProjects => recentProjectItems;

    public ShellRoute CurrentRoute
    {
        get => currentRoute;
        private set
        {
            if (!SetProperty(ref currentRoute, value))
                return;
            OnPropertyChanged(nameof(IsDashboardActive));
            OnPropertyChanged(nameof(IsOfferDocumentsActive));
            OnPropertyChanged(nameof(IsLandingScreen));
            OnPropertyChanged(nameof(IsWorkstationMode));
            OnPropertyChanged(nameof(ShowContextBar));
            OnPropertyChanged(nameof(ShowWorkflowStepper));
            OnPropertyChanged(nameof(ShowContextPageTitle));
            OnPropertyChanged(nameof(ShowContextIdentity));
            OnPropertyChanged(nameof(UseCompactPagePadding));
            OnPropertyChanged(nameof(StatusBarHint));
            RefreshWorkflowSteps();
        }
    }

    public UserControl? CurrentView
    {
        get => currentView;
        private set => SetProperty(ref currentView, value);
    }

    public string BreadcrumbGroup
    {
        get => breadcrumbGroup;
        private set => SetProperty(ref breadcrumbGroup, value);
    }

    public string BreadcrumbProject
    {
        get => breadcrumbProject;
        private set => SetProperty(ref breadcrumbProject, value);
    }

    public string PageTitle
    {
        get => pageTitle;
        private set => SetProperty(ref pageTitle, value);
    }

    public string TickerCostoDirecto { get => tickerCostoDirecto; private set => SetProperty(ref tickerCostoDirecto, value); }
    public string TickerMargen { get => tickerMargen; private set => SetProperty(ref tickerMargen, value); }
    public string TickerTotalOfertaUf { get => tickerTotalOfertaUf; private set => SetProperty(ref tickerTotalOfertaUf, value); }
    public string TickerTotalOfertaClp { get => tickerTotalOfertaClp; private set => SetProperty(ref tickerTotalOfertaClp, value); }

    public bool IsDashboardActive => CurrentRoute == ShellRoute.Dashboard;
    public bool IsOfferDocumentsActive => CurrentRoute == ShellRoute.OfferDocuments;
    public bool IsLandingScreen => CurrentRoute == ShellRoute.Dashboard && !HasOpenProject;

    public void Navigate(ShellRoute route) => RequestNavigate(route);

    public void OpenQuickSearch()
    {
        RefreshQuickSearchResults();
        IsSearchPaletteOpen = true;
    }

    public void CloseQuickSearch() => IsSearchPaletteOpen = false;

    public async Task ExecuteQuickSearchAsync()
    {
        var item = SelectedQuickSearchItem ?? QuickSearchResults.FirstOrDefault();
        if (item == null)
            return;

        CloseQuickSearch();
        await item.ExecuteAsync();
    }

    public void SelectQuickSearchIndex(int offset)
    {
        if (QuickSearchResults.Count == 0)
            return;

        var currentIndex = SelectedQuickSearchItem != null ? QuickSearchResults.IndexOf(SelectedQuickSearchItem) : -1;
        var nextIndex = currentIndex < 0 ? 0 : currentIndex + offset;
        if (nextIndex < 0)
            nextIndex = QuickSearchResults.Count - 1;
        if (nextIndex >= QuickSearchResults.Count)
            nextIndex = 0;

        SelectedQuickSearchItem = QuickSearchResults[nextIndex];
    }

    public void SelectFirstQuickSearchItem()
    {
        if (QuickSearchResults.Count > 0)
            SelectedQuickSearchItem = QuickSearchResults[0];
    }

    public void RefreshChrome()
    {
        OnPropertyChanged(nameof(ActiveProjectLabel));
        OnPropertyChanged(nameof(ActiveProjectPath));
        OnPropertyChanged(nameof(HasOpenProject));
        OnPropertyChanged(nameof(CollaborationModeLabel));
        // CanSaveProject/CanSaveProjectAs también dependen de HasOpenProject — sin esto, el botón
        // "Guardar" no se refresca al abrir/cerrar un proyecto (ver comentario en el setter de IsBusy).
        OnPropertyChanged(nameof(CanSaveProject));
        OnPropertyChanged(nameof(CanSaveProjectAs));
        OnPropertyChanged(nameof(IsLandingScreen));
        OnPropertyChanged(nameof(IsWorkstationMode));
        OnPropertyChanged(nameof(ShowContextBar));
        OnPropertyChanged(nameof(ShowWorkflowStepper));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(WindowTitleText));
        RefreshUserContext();
        RefreshQuickSearchResults();

        if (!projectWorkspace.HasOpenProject)
        {
            dashboard.ClearAlertCache();
            SaveStateText = "Sin proyecto abierto";
            AlertCount = "0";
            StatusChip = "—";
            VersionChip = "—";
            CurrencyChip = "—";
            TickerCostoDirecto = "—";
            TickerMargen = "—";
            TickerTotalOfertaUf = "—";
            TickerTotalOfertaClp = "";
            RefreshWorkflowSteps();
            return;
        }

        string autoState = autoSave.StatusText;
        SaveStateText = session.IsDirty
            ? "Sin guardar"
            : (string.IsNullOrWhiteSpace(autoState) ? "Guardado" : autoState);
        // No CreateSnapshot/BuildAlerts aquí: Session.Changed dispara RefreshChrome con alta frecuencia.
        // Usar el último total cacheado por Dashboard/Revisión; si aún no hay cache, "—".
        AlertCount = dashboard.LastAlertTotalIssues?.ToString() ?? "—";
        StatusChip = string.IsNullOrWhiteSpace(session.ProjectInfo.EstadoPresupuesto)
            ? "Borrador"
            : session.ProjectInfo.EstadoPresupuesto;
        VersionChip = session.ProjectInfo.OfertaPdf?.RevisionTag ?? "Rev 0";
        string moneda = string.IsNullOrWhiteSpace(session.ProjectInfo.MonedaPresupuesto)
            ? "CLP"
            : session.ProjectInfo.MonedaPresupuesto;
        // UF es tasa opcional: con el presupuesto en CLP no se expone (política de moneda única).
        CurrencyChip = !string.Equals(moneda, "CLP", StringComparison.OrdinalIgnoreCase) && session.ProjectInfo.ValorUfClp > 0
            ? $"{moneda} (UF={session.ProjectInfo.ValorUfClp:N0})"
            : moneda;
        RefreshContext();
        UpdateFinancialTicker();
    }

    private void UpdateFinancialTicker()
    {
        if (!projectWorkspace.HasOpenProject)
        {
            TickerCostoDirecto = "—"; TickerMargen = "—"; TickerTotalOfertaUf = "—"; TickerTotalOfertaClp = "";
            return;
        }
        try
        {
            var cost = dashboard.BuildCost(session.Data);
            var project = session.ProjectInfo;
            decimal valorUf = project.ValorUfClp > 0 ? project.ValorUfClp : session.ValorUfClp;
            if (valorUf <= 0) valorUf = PriceConversion.DefaultValorUfClp;
            TickerMargen = $"{project.UtilidadPct:N1}%";

            // Política de moneda: si el presupuesto está en CLP no se muestra UF (solo pesos);
            // en UF se muestra el monto en UF y el equivalente CLP entre paréntesis.
            bool esClp = string.Equals(project.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);
            if (esClp)
            {
                TickerCostoDirecto = $"$ {cost.CostoDirectoClp:N0}";
                TickerTotalOfertaUf = $"$ {cost.TotalVentaClp:N0}";
                TickerTotalOfertaClp = "";
            }
            else
            {
                TickerCostoDirecto = $"{PriceConversion.ClpToUf(cost.CostoDirectoClp, valorUf):N3} UF";
                TickerTotalOfertaUf = $"{PriceConversion.ClpToUf(cost.TotalVentaClp, valorUf):N3} UF";
                TickerTotalOfertaClp = $"(${cost.TotalVentaClp:N0})";
            }
        }
        catch
        {
            TickerCostoDirecto = "—"; TickerMargen = $"{session.ProjectInfo.UtilidadPct:N1}%"; TickerTotalOfertaUf = "—"; TickerTotalOfertaClp = "";
        }
    }

    private void RefreshContext()
    {
        var info = ShellRouteCatalog.Get(CurrentRoute);
        BreadcrumbGroup = info.Group;
        BreadcrumbProject = projectWorkspace.HasOpenProject
            ? ActiveProjectLabel
            : "Sin proyecto";
        PageTitle = info.Title;
        RefreshWorkflowSteps();
    }

    private void RefreshUserContext()
    {
        var user = CollaborationSessionBridge.CurrentUserContext();
        UserInitials = string.IsNullOrWhiteSpace(user.DisplayName)
            ? BuildInitials(user.Email)
            : BuildInitials(user.DisplayName);
        UserDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
        UserEmail = user.Email;
        OnPropertyChanged(nameof(UserInitials));
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(UserRoleLabel));
        OnPropertyChanged(nameof(UserStatusLabel));
        OnPropertyChanged(nameof(PriceBankSyncLabel));
    }

    public void RefreshCurrentPage()
    {
        if (currentPageVm is IRefreshablePage refreshable)
            refreshable.Refresh();
    }

    private bool TryApplyCurrentPageChanges()
    {
        if (currentPageVm is IPageWithPendingChanges pending &&
            pending.HasPendingChanges &&
            currentPageVm is IPageWithCommitChanges commitPage)
            return commitPage.TryApplyChanges();

        return true;
    }

    private CommandPaletteCategory selectedPaletteCategory = CommandPaletteCategory.All;
    public CommandPaletteCategory SelectedPaletteCategory
    {
        get => selectedPaletteCategory;
        set
        {
            if (SetProperty(ref selectedPaletteCategory, value))
                RefreshQuickSearchResults();
        }
    }

    [RelayCommand]
    public void SetPaletteCategory(object? param)
    {
        if (param is CommandPaletteCategory cat)
            SelectedPaletteCategory = cat;
        else if (param is string str && Enum.TryParse<CommandPaletteCategory>(str, true, out var parsed))
            SelectedPaletteCategory = parsed;
    }

    private CommandPaletteEngine? commandPaletteEngine;
    private CommandPaletteEngine PaletteEngine => commandPaletteEngine ??= new CommandPaletteEngine(
        session,
        this,
        priceBankStore,
        projectWorkspace,
        undo);

    private void RefreshQuickSearchResults()
    {
        var query = SearchText ?? string.Empty;
        var previousSelection = SelectedQuickSearchItem;

        quickSearchResults.Clear();
        var results = PaletteEngine.Search(query, SelectedPaletteCategory, msg => ShellNotificationService.Instance.ShowSuccess("Comando ejecutado", msg));
        foreach (var item in results)
        {
            quickSearchResults.Add(new QuickSearchItemVm(item));
        }

        OnPropertyChanged(nameof(HasQuickSearchResults));
        OnPropertyChanged(nameof(ResultCountLabel));

        if (quickSearchResults.Count == 0)
        {
            SelectedQuickSearchItem = null;
            return;
        }

        if (previousSelection != null)
        {
            var match = quickSearchResults.FirstOrDefault(item => item.Title == previousSelection.Title);
            if (match != null)
            {
                SelectedQuickSearchItem = match;
                return;
            }
        }

        SelectedQuickSearchItem = quickSearchResults[0];
    }

    [RelayCommand(CanExecute = nameof(CanExecuteQuickSearchItem))]
    private Task ExecuteQuickSearchItemAsync(QuickSearchItemVm? item)
    {
        if (item == null)
            return Task.CompletedTask;

        CloseQuickSearch();
        return item.ExecuteAsync();
    }

    private bool CanExecuteQuickSearchItem(QuickSearchItemVm? item) => item != null;

    private static string BuildInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "US";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    [RelayCommand]
    public void OpenGlobalQuickJump()
    {
        var hasPartidas = session.Itemizado.Count > 0;
        var hasSc = session.CubicacionSC.Count > 0;

        if (!hasPartidas && !hasSc)
        {
            ShellNotificationService.Instance.ShowInfo("Quick Jump", "No hay partidas ni tableros en el proyecto activo.");
            return;
        }

        var items = new List<QuickJumpPartidaItem>();

        // 1. Partidas y Capítulos del Itemizado
        string currentCapitulo = "";
        foreach (var p in session.Itemizado)
        {
            if (p.EsTitulo)
            {
                currentCapitulo = p.Descripcion;
                items.Add(new QuickJumpPartidaItem
                {
                    Id = p.Id ?? "",
                    Codigo = p.Item ?? "",
                    Descripcion = p.Descripcion,
                    EsTitulo = true,
                    IsSelectable = true,
                    Kind = QuickJumpItemKind.Capitulo,
                    Capitulo = "Capítulo / Título WBS"
                });
            }
            else
            {
                items.Add(new QuickJumpPartidaItem
                {
                    Id = p.Id ?? "",
                    Codigo = p.Item ?? "",
                    Descripcion = p.Descripcion,
                    Unidad = p.Unidad ?? "",
                    Cantidad = p.Cantidad,
                    EsTitulo = false,
                    IsSelectable = true,
                    Kind = QuickJumpItemKind.Partida,
                    Capitulo = currentCapitulo,
                    BadgeText = p.ExcluidoCubicacion ? "Excluida" : null
                });
            }
        }

        // 2. Tableros y Circuitos de Servicios Comunes (SC)
        foreach (var cuadro in session.CubicacionSC)
        {
            items.Add(new QuickJumpPartidaItem
            {
                Id = cuadro.Id,
                Codigo = cuadro.Nombre,
                Descripcion = string.IsNullOrWhiteSpace(cuadro.Descripcion)
                    ? $"Tablero SC · {cuadro.Circuitos.Count} circuito(s)"
                    : cuadro.Descripcion,
                EsTitulo = false,
                IsSelectable = true,
                Kind = QuickJumpItemKind.TableroSc,
                BadgeText = $"{cuadro.Circuitos.Count} ctos",
                Capitulo = "Servicios Comunes"
            });

            foreach (var cto in cuadro.Circuitos)
            {
                items.Add(new QuickJumpPartidaItem
                {
                    Id = cuadro.Id,
                    SecondaryId = cto.Id,
                    Codigo = $"C{cto.Numero}",
                    Descripcion = string.IsNullOrWhiteSpace(cto.Ubicacion)
                        ? cto.Descripcion
                        : $"{cto.Descripcion} · {cto.Ubicacion}",
                    EsTitulo = false,
                    IsSelectable = true,
                    Kind = QuickJumpItemKind.CircuitoSc,
                    Capitulo = $"Tablero {cuadro.Nombre}"
                });
            }
        }

        var owner = Application.Current?.MainWindow;
        var dialog = new QuickJumpPartidaDialog(items) { Owner = owner };

        if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
        {
            var selected = dialog.SelectedItem;
            switch (selected.Kind)
            {
                case QuickJumpItemKind.Capitulo:
                    RequestNavigate(ShellRoute.Itemized, new ItemizadoNavigationContext(TargetRowId: selected.Id, TargetCode: selected.Codigo));
                    break;

                case QuickJumpItemKind.Partida:
                    if (CurrentRoute == ShellRoute.Takeoff)
                    {
                        RequestNavigate(ShellRoute.Takeoff, new TakeoffNavigationContext(selected.Id, selected.Codigo, selected.Descripcion));
                    }
                    else if (CurrentRoute == ShellRoute.Apu)
                    {
                        RequestNavigate(ShellRoute.Apu, new ApuNavContext(selected.Id, selected.Codigo, selected.Descripcion));
                    }
                    else if (CurrentRoute == ShellRoute.Itemized)
                    {
                        RequestNavigate(ShellRoute.Itemized, new ItemizadoNavigationContext(TargetRowId: selected.Id, TargetCode: selected.Codigo));
                    }
                    else
                    {
                        RequestNavigate(ShellRoute.Takeoff, new TakeoffNavigationContext(selected.Id, selected.Codigo, selected.Descripcion));
                    }
                    break;

                case QuickJumpItemKind.TableroSc:
                    RequestNavigate(ShellRoute.CubicacionSc, new CubicacionScNavigationContext(CuadroId: selected.Id, CuadroNombre: selected.Codigo));
                    break;

                case QuickJumpItemKind.CircuitoSc:
                    RequestNavigate(ShellRoute.CubicacionSc, new CubicacionScNavigationContext(CuadroId: selected.Id, CircuitoId: selected.SecondaryId));
                    break;
            }
        }
    }
}
