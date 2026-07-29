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
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Itemizado;

namespace QubiCostElectrical.Wpf.Shell;

public sealed partial class ShellViewModel : ViewModelBase, IShellNavigationHost, IDisposable
{
    private readonly WpfAppHost host;
    private readonly AutoSaveService autoSave;
    private readonly RecentProjectsService recentProjects;
    private readonly ObservableCollection<RecentProjectInfo> recentProjectItems = new();
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
    private string searchText = "";
    private bool isSearchPaletteOpen;
    private bool isProjectPickerOpen;
    private bool isUserMenuOpen;
    private QuickSearchItemVm? selectedQuickSearchItem;
    private readonly ObservableCollection<QuickSearchItemVm> quickSearchResults = new();

    internal Func<ThreeWayResult> ShowCloseProjectDialog = () =>
        WpfDialogService.ConfirmSaveDiscardCancel("Cerrar proyecto",
            "El proyecto tiene cambios sin guardar.\n¿Desea guardarlos antes de cerrar?");

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

    // host.Session/host.UndoService viven toda la vida del WpfAppHost — sin desuscribirse, cada
    // ShellViewModel nuevo (un ciclo de Cerrar sesión crea uno) deja la instancia vieja y su
    // AutoSaveService (con su propio Timer interno) retenidos para siempre. Mismo patrón de bug
    // ya corregido en otras 8 pantallas, ver SessionChangedSubscriptionLeakTests.cs.
    private readonly PropertyChangedEventHandler autoSavePropertyChangedHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedRefreshChromeHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionSavedHandler;
    private readonly PropertyChangedEventHandler undoServicePropertyChangedHandler;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedSyncReadOnlyHandler;
    private bool disposed;

    public ShellViewModel(WpfAppHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        autoSave = new AutoSaveService(this.host);
        autoSavePropertyChangedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
        autoSave.PropertyChanged += autoSavePropertyChangedHandler;
        recentProjects = new RecentProjectsService();
        sessionChangedRefreshChromeHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
        host.Session.Changed += sessionChangedRefreshChromeHandler;
        sessionSavedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(() => { autoSave.MarkSaved(); OnSaved(); });
        host.Session.Saved += sessionSavedHandler;
        undoServicePropertyChangedHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(RefreshUndoRedo);
        host.UndoService.PropertyChanged += undoServicePropertyChangedHandler;
        sessionChangedSyncReadOnlyHandler = (_, _) => Application.Current?.Dispatcher.InvokeAsync(SyncReadOnlyFromSession);
        host.Session.Changed += sessionChangedSyncReadOnlyHandler;
        host.ProjectLock.LockLost += OnProjectLockLost;
        RoleSessionSync.Apply(host.Session, host.Permissions.CurrentRole);
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
        host.Session.Changed -= sessionChangedRefreshChromeHandler;
        host.Session.Saved -= sessionSavedHandler;
        host.UndoService.PropertyChanged -= undoServicePropertyChangedHandler;
        host.Session.Changed -= sessionChangedSyncReadOnlyHandler;
        host.ProjectLock.LockLost -= OnProjectLockLost;
        autoSave.Dispose();
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
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ReadOnlyBannerText
    {
        get
        {
            if (!host.Session.IsReadOnly && host.ProjectLock.CurrentLockPath != null)
                return $"Edición exclusiva · {CollaborationSessionBridge.CurrentUserContext().DisplayName}";

            return string.IsNullOrWhiteSpace(host.Session.ReadOnlyReason)
                ? "El proyecto está en solo lectura."
                : host.Session.ReadOnlyReason;
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
        }
    }

    public bool CanExecuteActions => !isBusy;
    public bool CanSaveProject => !isBusy && HasOpenProject && !IsReadOnly;
    public bool CanSaveProjectAs => !isBusy && HasOpenProject && !IsReadOnly;

    public string ActiveProjectPath => host.ProjectWorkspace.HasOpenProject
        ? host.Session.CurrentProjectPath
        : string.Empty;

    public string ApplicationName => "QUBI COST";
    public string ApplicationSub => "ELECTRICAL";
    public string ProductVersion { get; }

    public string ActiveProjectLabel
    {
        get
        {
            if (!host.ProjectWorkspace.HasOpenProject)
                return "Sin proyecto abierto";
            var name = host.Session.ProjectInfo.Nombre;
            return string.IsNullOrWhiteSpace(name) ? "Proyecto sin nombre" : name;
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
    public string UserRoleLabel => host.Permissions.RoleDisplayName;

    public string UserStatusLabel => string.IsNullOrWhiteSpace(UserRoleLabel)
        ? UserDisplayName
        : $"{UserDisplayName} · {UserRoleLabel}";

    public string PriceBankSyncLabel
    {
        get
        {
            try
            {
                var count = host.Services.PriceBankStore.Count();
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
        IsReadOnly = host.Session.IsReadOnly;
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

        host.Auth.Logout();
        SessionManager.ClearSession();

        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var login = new LoginWindow(host.Auth);
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
            RefreshQuickSearchResults();
            IsSearchPaletteOpen = true;
        }
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

    public bool HasOpenProject => host.ProjectWorkspace.HasOpenProject;

    public bool CanUndo => host.UndoService.CanUndo && !IsReadOnly;
    public bool CanRedo => host.UndoService.CanRedo && !IsReadOnly;

    public string UndoToolTip => CanUndo && !string.IsNullOrWhiteSpace(host.UndoService.LastUndoDescription)
        ? $"Deshacer: {host.UndoService.LastUndoDescription} (Ctrl+Z)"
        : "Deshacer (Ctrl+Z)";

    public string RedoToolTip => CanRedo && !string.IsNullOrWhiteSpace(host.UndoService.LastRedoDescription)
        ? $"Rehacer: {host.UndoService.LastRedoDescription} (Ctrl+Y)"
        : "Rehacer (Ctrl+Y)";

    public string StatusBarHint => CurrentRoute switch
    {
        ShellRoute.Itemized => "Ins agregar partida · Alt+↑↓ mover · Supr eliminar · Ctrl+V pegar en grilla",
        ShellRoute.Takeoff => "Alt+↑↓ mover filas · Ctrl+V pegar · Tab/Enter navegar celdas · Ctrl+Izq/Der sectores",
        ShellRoute.Apu => "Enter editar · Supr quitar recurso · Generar APU desde cubicación",
        ShellRoute.Review => "Clic en alerta para ir al módulo · Actualizar recalcula revisión",
        ShellRoute.OfferDocuments => "Paso a paso: validar → configurar → vista previa → exportar",
        _ => "Ctrl+K búsqueda · Ctrl+S guardar · F1 atajos"
    };

    public void Undo()
    {
        if (host.UndoService.Undo(host.Session))
            RefreshCurrentPage();
    }

    public void Redo()
    {
        if (host.UndoService.Redo(host.Session))
            RefreshCurrentPage();
    }

    private void RefreshUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoToolTip));
        OnPropertyChanged(nameof(RedoToolTip));
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
        RefreshUserContext();
        RefreshQuickSearchResults();

        if (!host.ProjectWorkspace.HasOpenProject)
        {
            SaveStateText = "Sin proyecto abierto";
            AlertCount = "0";
            StatusChip = "—";
            VersionChip = "—";
            CurrencyChip = "—";
            RefreshWorkflowSteps();
            return;
        }

        string autoState = autoSave.StatusText;
        SaveStateText = string.IsNullOrWhiteSpace(autoState)
            ? (host.Session.IsDirty ? "Sin guardar" : "Guardado")
            : autoState;
        var review = host.Dashboard.BuildAlerts(host.Session.CreateSnapshot());
        AlertCount = review.TotalIssues.ToString();
        StatusChip = string.IsNullOrWhiteSpace(host.Session.ProjectInfo.EstadoPresupuesto)
            ? "Borrador"
            : host.Session.ProjectInfo.EstadoPresupuesto;
        VersionChip = host.Session.ProjectInfo.OfertaPdf?.RevisionTag ?? "Rev 0";
        string moneda = string.IsNullOrWhiteSpace(host.Session.ProjectInfo.MonedaPresupuesto)
            ? "CLP"
            : host.Session.ProjectInfo.MonedaPresupuesto;
        // UF es tasa opcional de referencia; no es requerida para calcular en CLP
        CurrencyChip = host.Session.ProjectInfo.ValorUfClp > 0
            ? $"{moneda} (UF={host.Session.ProjectInfo.ValorUfClp:N0})"
            : moneda;
        RefreshContext();
    }

    private void RefreshContext()
    {
        var info = ShellRouteCatalog.Get(CurrentRoute);
        BreadcrumbGroup = info.Group;
        BreadcrumbProject = host.ProjectWorkspace.HasOpenProject
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

    private void RefreshCurrentPage()
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

    private void RefreshQuickSearchResults()
    {
        var query = Normalize(SearchText);
        var previousSelection = SelectedQuickSearchItem;

        quickSearchResults.Clear();
        foreach (var item in BuildQuickSearchResults())
        {
            if (!MatchesQuery(item, query))
                continue;

            quickSearchResults.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var item in BuildBusinessSearchResults(query))
                quickSearchResults.Add(item);
        }

        if (quickSearchResults.Count == 0)
        {
            SelectedQuickSearchItem = null;
            return;
        }

        if (previousSelection != null)
        {
            var match = quickSearchResults.FirstOrDefault(item => ReferenceEquals(item, previousSelection));
            if (match != null)
            {
                SelectedQuickSearchItem = match;
                return;
            }
        }

        SelectedQuickSearchItem = quickSearchResults[0];
    }

    private IReadOnlyList<QuickSearchItemVm> BuildQuickSearchResults()
    {
        return
        [
            new QuickSearchItemVm("Resumen", "Ir al panel principal del proyecto", "Alt+1", () => NavigateAsync(ShellRoute.Dashboard)),
            new QuickSearchItemVm("Proyecto", "Datos generales y documentos", "Alt+P", () => NavigateAsync(ShellRoute.Project)),
            new QuickSearchItemVm("Itemizado", "Importar y editar partidas", "Alt+2", () => NavigateAsync(ShellRoute.Itemized)),
            new QuickSearchItemVm("Cubicación", "Desarrollar materiales y cantidades", "Alt+3", () => NavigateAsync(ShellRoute.Takeoff)),
            new QuickSearchItemVm("Cubicación SC", "Tableros y circuitos de servicios comunes", "Alt+Shift+3", () => NavigateAsync(ShellRoute.CubicacionSc)),
            new QuickSearchItemVm("APU", "Analizar precios unitarios", "Alt+4", () => NavigateAsync(ShellRoute.Apu)),
            new QuickSearchItemVm("Plantillas", "Aplicar plantillas de cubicación", "Alt+5", () => NavigateAsync(ShellRoute.Templates)),
            new QuickSearchItemVm("Banco de precios", "Buscar y mantener precios", "Alt+6", () => NavigateAsync(ShellRoute.Prices)),
            new QuickSearchItemVm("Cotizaciones", "Comparar proveedores y ofertas", "Alt+7", () => NavigateAsync(ShellRoute.Quotes)),
            new QuickSearchItemVm("Lista de compra", "Consolidar materiales por comprar", "Alt+8", () => NavigateAsync(ShellRoute.Purchases)),
            new QuickSearchItemVm("Revisión", "Validar pendientes y alertas", "Alt+9", () => NavigateAsync(ShellRoute.Review)),
            new QuickSearchItemVm("Versiones", "Administrar versiones del presupuesto", "Alt+0", () => NavigateAsync(ShellRoute.Versions)),
            new QuickSearchItemVm("Proveedores", "Maestro de proveedores y contactos", "", () => NavigateAsync(ShellRoute.ProcurementSuppliers)),
            new QuickSearchItemVm("Requerimientos de compra", "Requerimientos, consolidación y aprobación", "", () => NavigateAsync(ShellRoute.ProcurementRequisitions)),
            new QuickSearchItemVm("Solicitudes de cotización", "RFQ, invitaciones y respuestas", "", () => NavigateAsync(ShellRoute.ProcurementRfq)),
            new QuickSearchItemVm("Oferta y exportación", "Validar, configurar, previsualizar y exportar", "Alt+O", () => NavigateAsync(ShellRoute.OfferDocuments)),
            new QuickSearchItemVm("Nuevo proyecto", "Crear un nuevo presupuesto", "Ctrl+N", NewProjectAsync),
            new QuickSearchItemVm("Abrir proyecto", "Abrir un presupuesto existente", "Ctrl+O", OpenProjectAsync),
            new QuickSearchItemVm("Guardar proyecto", "Guardar los cambios actuales", "Ctrl+S", SaveProjectAsync),
            new QuickSearchItemVm("Guardar como", "Crear una copia con nuevo nombre", "Ctrl+Shift+S", SaveProjectAsAsync),
            new QuickSearchItemVm("Cerrar proyecto", "Cerrar el proyecto activo", "Ctrl+W", () => CloseProjectAsync()),
            new QuickSearchItemVm("Ir a revisión", "Abrir la vista de revisión", "F8", () => NavigateAsync(ShellRoute.Review)),
            new QuickSearchItemVm("Monedas", "Ver y editar tasas UF y dólar", "Alt+M", () => NavigateAsync(ShellRoute.Currency)),
            new QuickSearchItemVm("Preferencias", "Configuración global de la aplicación", "Alt+,", () => NavigateAsync(ShellRoute.Preferences)),
            new QuickSearchItemVm("Órdenes de cambio", "Administrar órdenes de cambio del proyecto", "Alt+C", () => NavigateAsync(ShellRoute.ChangeOrders)),
            new QuickSearchItemVm("Deshacer", "Deshacer la última acción", "Ctrl+Z", () => { Undo(); return Task.CompletedTask; }),
            new QuickSearchItemVm("Rehacer", "Rehacer la última acción deshecha", "Ctrl+Y", () => { Redo(); return Task.CompletedTask; })
        ];
    }

    private IReadOnlyList<QuickSearchItemVm> BuildBusinessSearchResults(string query)
    {
        var results = new List<QuickSearchItemVm>();

        if (!host.ProjectWorkspace.HasOpenProject)
            return results;

        var session = host.Session;
        const int maxPerCategory = 5;

        AddActionSuggestions(query, results);

        var partidaHits = 0;
        foreach (var row in session.Itemizado)
        {
            if (partidaHits >= maxPerCategory) break;
            if (row.EsTitulo) continue;

            var code = Normalize(row.Item);
            var desc = Normalize(row.Descripcion);
            if (!code.Contains(query) && !desc.Contains(query))
                continue;

            partidaHits++;
            var label = !string.IsNullOrWhiteSpace(row.Item)
                ? $"{row.Item} — {row.Descripcion}"
                : row.Descripcion;
            var itemId = row.Id;
            var itemCode = row.Item;
            var itemDesc = row.Descripcion;
            results.Add(new QuickSearchItemVm(
                label, "Ir a cubicación", "",
                () => { host.Session.PendingTakeoffNavigation = new TakeoffNavigationContext(itemId, itemCode, itemDesc); RequestNavigate(ShellRoute.Takeoff); return Task.CompletedTask; }));
        }

        var apuHits = 0;
        foreach (var apu in session.Apus)
        {
            if (apuHits >= maxPerCategory) break;
            if (apu.Recursos == null) continue;

            foreach (var recurso in apu.Recursos)
            {
                if (apuHits >= maxPerCategory) break;

                var codigo = Normalize(recurso.CodigoMaterial);
                var descN = Normalize(recurso.Descripcion);
                if (!codigo.Contains(query) && !descN.Contains(query))
                    continue;

                apuHits++;
                var label = !string.IsNullOrWhiteSpace(recurso.CodigoMaterial)
                    ? $"{recurso.CodigoMaterial} — {recurso.Descripcion}"
                    : recurso.Descripcion;
                var aItemId = apu.ItemId;
                var aCodigo = recurso.CodigoMaterial;
                var aDesc = recurso.Descripcion;
                results.Add(new QuickSearchItemVm(
                    label, "APU", "",
                    () => { RequestNavigate(ShellRoute.Apu, new ApuNavContext(aItemId, aCodigo ?? "", aDesc ?? "")); return Task.CompletedTask; }));
            }
        }

        var bankHits = 0;
        foreach (var entry in host.Services.PriceBankStore.GetAll())
        {
            if (bankHits >= maxPerCategory) break;

            var codigo = Normalize(entry.Codigo);
            var desc = Normalize(entry.Descripcion);
            if (!codigo.Contains(query) && !desc.Contains(query))
                continue;

            bankHits++;
            var label = !string.IsNullOrWhiteSpace(entry.Codigo)
                ? $"{entry.Codigo} — {entry.Descripcion}"
                : entry.Descripcion;
            var bankCode = entry.Codigo ?? "";
            results.Add(new QuickSearchItemVm(
                label, "Banco de precios", "",
                () => { RequestNavigate(ShellRoute.Prices, new PriceBankNavigationContext(bankCode)); return Task.CompletedTask; }));
        }

        return results;
    }

    private void AddActionSuggestions(string query, List<QuickSearchItemVm> results)
    {
        var q = Normalize(query);

        if (ContainsAny(q, ["NUEV", "CRE", "AGREG"]))
        {
            results.Add(new QuickSearchItemVm("Crear nueva partida", "Itemizado → agregar partida", "",
                () =>
                {
                    host.Session.PendingItemizadoAction = ItemizadoPendingAction.AddPartida;
                    if (currentRoute == ShellRoute.Itemized && currentPageVm is ItemizadoViewModel itemizadoVm)
                    {
                        itemizadoVm.ProcessPendingShellAction();
                        return Task.CompletedTask;
                    }

                    RequestNavigate(ShellRoute.Itemized);
                    return Task.CompletedTask;
                }));
        }

        if (ContainsAny(q, ["REVIS", "ALERT", "ERROR", "PENDIENT"]))
        {
            results.Add(new QuickSearchItemVm("Ir a revisión de alertas", "Validar pendientes y problemas", "",
                () => { RequestNavigate(ShellRoute.Review); return Task.CompletedTask; }));
        }

        if (ContainsAny(q, ["VERS", "VERSION"]))
        {
            results.Add(new QuickSearchItemVm("Nueva versión del presupuesto", "Crear versionado del proyecto", "",
                () => { RequestNavigate(ShellRoute.Versions); return Task.CompletedTask; }));
        }

        if (ContainsAny(q, ["EXCEL", "EXPORT", "PDF", "DOCX", "XLSX"]))
        {
            results.Add(new QuickSearchItemVm("Oferta y exportación", "Validar, configurar y exportar el paquete comercial", "",
                () => { RequestNavigate(ShellRoute.OfferDocuments); return Task.CompletedTask; }));
        }

        if (ContainsAny(q, ["COMPR", "PURCH", "MATERIAL"]))
        {
            results.Add(new QuickSearchItemVm("Ir a lista de compra", "Materiales por adquirir del proyecto", "",
                () => { RequestNavigate(ShellRoute.Purchases); return Task.CompletedTask; }));
        }
    }

    private static bool ContainsAny(string text, string[] candidates)
        => candidates.Any(c => text.Contains(c));

    private static bool MatchesQuery(QuickSearchItemVm item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Normalize(item.Title).Contains(query)
            || Normalize(item.Subtitle).Contains(query)
            || Normalize(item.Shortcut).Contains(query);
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

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
}
