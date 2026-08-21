using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ItemizadoPresenter presenter;
    private readonly IItemizadoWorkspaceService workspace;
    private readonly ItemizadoImportCoordinator importCoordinator;
    private readonly List<ItemizadoRowVm> allRows = new();
    private readonly Dictionary<string, ItemizadoRowVm> rowsById = new(StringComparer.OrdinalIgnoreCase);
    private ICollectionView? rowsView;
    private ItemizadoRowVm? selectedRow;
    private ItemizadoFilterMode filterMode = ItemizadoFilterMode.Todos;
    private string searchText = "";
    private string debouncedSearchText = "";
    private DispatcherTimer? searchDebounceTimer;
    private bool showEmptyState = true;
    private bool showEmptyItemizado;
    private string headerSubtitle = "";
    private string saveStateLabel = "";
    private string statusMessage = "";
    private string summaryPartidas = "0";
    private string summaryCompletas = "0";
    private string summaryIncompletas = "0";
    private string summaryCapitulos = "0";
    private string summarySinPrecio = "0";
    private string summaryStripText = "";
    private string filteredCountLabel = "";
    private List<ItemizadoRowPresentation>? filterPresentationCache;
    private string? pendingSelectionId;
    private readonly Dictionary<string, decimal> costTotalsByItem = new(StringComparer.OrdinalIgnoreCase);
    private bool hasPendingUndoForEdit;
    private readonly HashSet<ItemizadoRowVm> wiredRows = new();
    private EventHandler<ProjectSessionChangedEventArgs>? sessionSavedHandler;

    // La vista se suscribe para entrar en modo edición automáticamente sobre la fila
    // recién agregada (Descripción), sin requerir F2 ni clic manual. Solo se dispara
    // tras Agregar partida / Agregar título; el resto de los flujos de edición no
    // se modifica.
    public event Action<string>? RowAdded;

    public ItemizadoViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        presenter = host.Itemizado;
        workspace = host.ItemizadoWorkspace;
        importCoordinator = new ItemizadoImportCoordinator(presenter);
        rowsView = CollectionViewSource.GetDefaultView(Rows);
        rowsView.Filter = FilterRow;

        searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        searchDebounceTimer.Tick += OnSearchDebounceTick;

        ImportExcelCommand = new RelayCommand(() => _ = ImportExcelAsync(), () => !ShowEmptyState && !host.Session.IsReadOnly);
        PasteExcelCommand = new RelayCommand(() => _ = PasteFromClipboardAsync(), () => !ShowEmptyState && !host.Session.IsReadOnly);
        AddPartidaCommand = new RelayCommand(AddPartida, () => !ShowEmptyState && !host.Session.IsReadOnly);
        AddTitleCommand = new RelayCommand(AddTitle, () => !ShowEmptyState && !host.Session.IsReadOnly);
        CubicarCommand = new RelayCommand(() => _ = CubicarSelectedAsync(), CanCubicar);
        MoveUpCommand = new RelayCommand(MoveUp, () => HasSelection && !host.Session.IsReadOnly);
        MoveDownCommand = new RelayCommand(MoveDown, () => HasSelection && !host.Session.IsReadOnly);
        DuplicateCommand = new RelayCommand(DuplicateSelected, CanDuplicate);
        DeleteCommand = new RelayCommand(DeleteSelected, () => HasSelection && !host.Session.IsReadOnly);
        ClearAllCommand = new RelayCommand(ClearAll, () => !ShowEmptyState && !ShowEmptyItemizado && !host.Session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        SetFilterCommand = new RelayCommand<ItemizadoFilterMode>(mode => FilterMode = mode);
        ClearSearchCommand = new RelayCommand(() => SearchText = "", () => !string.IsNullOrWhiteSpace(SearchText));
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !ShowEmptyState);
        ConfigureCommercialModelCommand = new RelayCommand(ConfigureCommercialModel, () => HasSelection && !host.Session.IsReadOnly && SelectedRow?.EsTitulo != true);

        // Session.Saved/Changed pueden dispararse desde un hilo de fondo: ProjectWorkspaceService
        // guarda con ConfigureAwait(false), así que su continuación final (Session.MarkSaved(),
        // que dispara Saved sincrónicamente) puede correr en un hilo del thread pool en vez del
        // hilo de UI. UpdateSaveState() termina tocando ObservableCollection ligadas a la UI
        // (vía ShellViewModel.RefreshChrome), lo que lanza NotSupportedException si no se
        // marshalla antes al Dispatcher. Se ejecuta sincrónico cuando ya se está en el hilo
        // correcto (o no hay Application real, como en tests) para no romper ese comportamiento
        // síncrono existente.
        sessionSavedHandler = (_, _) => RunOnUiThread(() =>
        {
            hasPendingUndoForEdit = false;
            UpdateSaveState();
        });
        host.Session.Saved += sessionSavedHandler;

        Reload(syncOnly: false);
        ProcessPendingShellAction();
    }

    protected override void DisposeCore()
    {
        if (sessionSavedHandler != null)
            host.Session.Saved -= sessionSavedHandler;
        searchDebounceTimer?.Stop();
        foreach (var vm in wiredRows.ToList())
            UnwireRowVm(vm);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }

    public string FilteredCountLabel
    {
        get => filteredCountLabel;
        private set => SetProperty(ref filteredCountLabel, value);
    }

    public string CubicarButtonLabel => "Cubicar partida";

    public ObservableCollection<ItemizadoRowVm> Rows { get; } = new();

    public ICollectionView FilteredRows => rowsView ?? CollectionViewSource.GetDefaultView(Rows);

    public ItemizadoRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (!SetProperty(ref selectedRow, value))
                return;
            RaiseCommandStates();
        }
    }

    public ItemizadoFilterMode FilterMode
    {
        get => filterMode;
        set
        {
            if (!SetProperty(ref filterMode, value))
                return;
            OnPropertyChanged(nameof(IsFilterTodos));
            OnPropertyChanged(nameof(IsFilterIncompletos));
            OnPropertyChanged(nameof(IsFilterCompletos));
            OnPropertyChanged(nameof(IsFilterSinCubicar));
            OnPropertyChanged(nameof(IsFilterSinPrecio));
            SafeRefreshView();
            EnsureSelectionVisible();
            UpdateFilterCounts();
            OnPropertyChanged(nameof(CubicarButtonLabel));
        }
    }

    public bool IsFilterTodos => FilterMode == ItemizadoFilterMode.Todos;
    public bool IsFilterIncompletos => FilterMode == ItemizadoFilterMode.Incompletos;
    public bool IsFilterCompletos => FilterMode == ItemizadoFilterMode.Completos;
    public bool IsFilterSinCubicar => FilterMode == ItemizadoFilterMode.SinCubicar;
    public bool IsFilterSinPrecio => FilterMode == ItemizadoFilterMode.SinPrecio;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            searchDebounceTimer?.Stop();
            searchDebounceTimer?.Start();
        }
    }

    public bool IsReadOnly => host.Session.IsReadOnly;

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value))
                return;
            RaiseCommandStates();
        }
    }

    public bool ShowEmptyItemizado
    {
        get => showEmptyItemizado;
        private set
        {
            if (!SetProperty(ref showEmptyItemizado, value))
                return;
            RaiseCommandStates();
        }
    }

    public string HeaderSubtitle
    {
        get => headerSubtitle;
        private set => SetProperty(ref headerSubtitle, value);
    }

    public string SaveStateLabel
    {
        get => saveStateLabel;
        private set => SetProperty(ref saveStateLabel, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (!SetProperty(ref statusMessage, value))
                return;
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string SummaryPartidas { get => summaryPartidas; private set => SetProperty(ref summaryPartidas, value); }
    public string SummaryCompletas { get => summaryCompletas; private set => SetProperty(ref summaryCompletas, value); }
    public string SummaryIncompletas { get => summaryIncompletas; private set => SetProperty(ref summaryIncompletas, value); }
    public string SummaryCapitulos { get => summaryCapitulos; private set => SetProperty(ref summaryCapitulos, value); }
    public string SummarySinPrecio { get => summarySinPrecio; private set => SetProperty(ref summarySinPrecio, value); }
    public string SummaryStripText { get => summaryStripText; private set => SetProperty(ref summaryStripText, value); }

    public ICommand ImportExcelCommand { get; }
    public ICommand PasteExcelCommand { get; }
    public ICommand AddPartidaCommand { get; }
    public ICommand AddTitleCommand { get; }
    public ICommand CubicarCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ConfigureCommercialModelCommand { get; }

    private bool HasSelection => IsSelectionVisible && !ShowEmptyState;
    private bool IsSelectionVisible => SelectedRow != null && PassesCurrentFilter(SelectedRow);
    private bool CanEdit => !host.Session.IsReadOnly;

    public void Refresh()
    {
        Reload(syncOnly: false);
        ProcessPendingShellAction();
    }

    public void ProcessPendingShellAction()
    {
        var action = host.Session.PendingItemizadoAction;
        if (action == ItemizadoPendingAction.None)
            return;

        host.Session.PendingItemizadoAction = ItemizadoPendingAction.None;
        if (action == ItemizadoPendingAction.AddPartida)
            AddPartida();
    }

    public void OnCellEdited(string? columnHeader = null, ItemizadoRowVm? editedRow = null)
    {
        if (host.Session.IsReadOnly)
            return;

        if (string.Equals(columnHeader, "Excl. cubic.", StringComparison.OrdinalIgnoreCase))
            return;

        var row = editedRow ?? SelectedRow;
        if (row == null)
            return;

        var peers = host.Session.Itemizado;
        if (string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase))
            row.CommitItemEdit(peers);

        row.ValidateEditableCells(peers);

        if (!hasPendingUndoForEdit)
        {
            host.UndoService.PushUndoPoint(host.Session, "Editar itemizado");
            hasPendingUndoForEdit = true;
        }
        host.Session.MarkDirty("itemizado");
        UpdateSaveState();

        bool needsCostRefresh = string.Equals(columnHeader, "Cantidad", StringComparison.OrdinalIgnoreCase);
        if (needsCostRefresh)
        {
            RefreshPresentation(includeCosts: true);
        }
        else if (string.Equals(columnHeader, "Descripción", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "Ud.", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase))
        {
            RefreshPresentation(includeCosts: false);
            if (string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase))
                SafeRefreshView();
        }
    }

    public void HandleDeleteKey()
    {
        if (!HasSelection)
            return;
        DeleteSelected();
    }

    private double completedPct;

    public double AvancePercent => completedPct;

    public void UpdateHudState(double pct)
    {
        completedPct = pct;
        OnPropertyChanged(nameof(AvancePercent));
    }

    private void ExportPdf()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Itemizado_{host.Session.ProjectInfo.Nombre ?? "Proyecto"}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                ItemizadoPdfExporter.Export(host.Session.ProjectInfo, Rows.ToList(), sfd.FileName);
                WpfDialogService.Info("Exportación Exitosa", $"Se ha exportado el itemizado valorado a {sfd.FileName}");
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error al exportar", $"Ocurrió un error al generar el PDF: {ex.Message}");
        }
    }
}
