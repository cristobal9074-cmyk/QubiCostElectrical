using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Documents;

public sealed partial class DocumentsViewModel : ViewModelBase, IPageWithPendingChanges, IRefreshablePage
{
    public DocumentsViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions, host.Documents) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public DocumentsViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, IPermissionService permissions, IDocumentsWorkspaceService documents)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.permissions = permissions;
        this.documents = documents;
        FilteredRows = new ObservableCollection<AntecedenteRowVm>();
        TipoOptions = new ObservableCollection<string>(new[] { "Todos" }.Concat(AntecedenteTecnicoTipo.All));

        AddAntecedenteCommand = new RelayCommand(AddAntecedenteConTipo, () => !ShowEmptyState && !session.IsReadOnly);
        DuplicateCommand = new RelayCommand(DuplicateSelected, () => SelectedRow != null && !session.IsReadOnly);
        RemoveCommand = new RelayCommand(RemoveSelected, () => SelectedRow != null && !session.IsReadOnly);
        MarkVigenteCommand = new RelayCommand(MarkVigente, () => SelectedRow != null && !session.IsReadOnly);
        AttachFileCommand = new RelayCommand(AttachFile, () => SelectedRow != null && !session.IsReadOnly);
        OpenAttachedFileCommand = new RelayCommand<object>(OpenAttachedFile, param => (param is AntecedenteRowVm r && !string.IsNullOrWhiteSpace(r.RutaArchivo)) || (SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.RutaArchivo)));
        OpenContainingFolderCommand = new RelayCommand(OpenContainingFolder, () => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.RutaArchivo));
        SaveCommand = new RelayCommand(Save, () => HasPendingChanges && !session.IsReadOnly);
        CancelCommand = new RelayCommand(Cancel, () => HasPendingChanges && !session.IsReadOnly);
        SyncPlanosCommand = new RelayCommand(SyncPlanos, () => !ShowEmptyState && !session.IsReadOnly);
        AppendExclusionsCommand = new RelayCommand(() => AppendExclusions(replace: false), () => !ShowEmptyState && !session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !ShowEmptyState && permissions.CanExportDocuments);
        SelectTipoFilterCommand = new RelayCommand<string>(SelectTipoFilter);
        SelectNavFilterCommand = new RelayCommand<string>(SelectNavFilter);
        ToggleInspectionFilterCommand = new RelayCommand<string>(ToggleInspectionFilter);
        previewCoordinator = new DocumentsPreviewCoordinator(() => SelectedRow);
        previewCoordinator.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                OnPropertyChanged(e.PropertyName);
        };
        PreviousPreviewPageCommand = previewCoordinator.PreviousPreviewPageCommand;
        NextPreviewPageCommand = previewCoordinator.NextPreviewPageCommand;
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => !string.IsNullOrEmpty(SearchText));
        CopyFilePathCommand = new RelayCommand(CopyFilePath, () => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.RutaArchivo));
    }

    public bool CanEdit => !ShowEmptyState && !session.IsReadOnly;
    public bool IsReadOnly => session.IsReadOnly;
    public bool IsLoading { get; private set; }

    public ObservableCollection<AntecedenteRowVm> FilteredRows { get; }
    public ObservableCollection<PlanoEspecialidadFilterItem> EspecialidadesPlanos { get; } = new();
    public ObservableCollection<string> TipoOptions { get; }

    public ImageSource? PreviewImage => previewCoordinator.PreviewImage;
    public bool HasPreviewImage => previewCoordinator.HasPreviewImage;
    public bool IsPreviewLoading => previewCoordinator.IsPreviewLoading;
    public string SelectedFileExtensionBadge => previewCoordinator.SelectedFileExtensionBadge;
    public string SelectedFileSizeText => previewCoordinator.SelectedFileSizeText;
    public bool SelectedFileExists => previewCoordinator.SelectedFileExists;
    public bool IsPdfMultiPage => previewCoordinator.IsPdfMultiPage;
    public string PreviewPageIndicator => previewCoordinator.PreviewPageIndicator;
    public ICommand PreviousPreviewPageCommand { get; }
    public ICommand NextPreviewPageCommand { get; }

    public AntecedenteRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            SetProperty(ref selectedRow, value);
            OnPropertyChanged(nameof(HasSelectedRow));
            RaiseSelectionCommands();
            UpdateFilePreview();
        }
    }

    public bool HasSelectedRow => SelectedRow != null;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                ApplyFilter();
                NotifyListMeta();
                ((RelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string TipoFilter
    {
        get => tipoFilter;
        set
        {
            if (!SetProperty(ref tipoFilter, value))
                return;
            ApplyFilter();
            NotifyTipoFlags();
            NotifyListMeta();
        }
    }

    public bool SinFechaCorte
    {
        get => sinFechaCorte;
        set
        {
            if (!SetProperty(ref sinFechaCorte, value))
                return;
            if (value)
                FechaCorte = null;
            OnPropertyChanged(nameof(FechaCorteChip));
            MarkDirtyLocal();
            StatusMessage = "Cambios pendientes de guardar";
        }
    }

    public DateTime? FechaCorte
    {
        get => fechaCorte;
        set
        {
            if (!SetProperty(ref fechaCorte, value))
                return;
            if (value != null)
                sinFechaCorte = false;
            OnPropertyChanged(nameof(SinFechaCorte));
            OnPropertyChanged(nameof(FechaCorteChip));
            MarkDirtyLocal();
            StatusMessage = "Cambios pendientes de guardar";
        }
    }

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value))
                return;
            RaiseToolbarCommandStates();
        }
    }

    public bool HasPendingChanges => isDirty;

    public int KpiPlanosCount => allRows.Count(r => string.Equals(r.Tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase));
    public int KpiEspecCount => allRows.Count(r => string.Equals(r.Tipo, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase));
    public int KpiAddendasCount => allRows.Count(r =>
        string.Equals(r.Tipo, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(r.Tipo, AntecedenteTecnicoTipo.Aclaracion, StringComparison.OrdinalIgnoreCase));
    public int KpiConsultasCount => allRows.Count(r => string.Equals(r.Tipo, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase));
    public int KpiTotalCount => allRows.Count;
    public int KpiVigentesCount => allRows.Count(r => r.EsVigente && !r.EsPendienteMandante);
    public int KpiPendientesMandanteCount => allRows.Count(r => r.EsPendienteMandante);
    public bool HasPendingAddendas => KpiAddendasCount > 0;

    // Indicadores de control documental: se calculan desde el expediente actual y no
    // persisten datos nuevos ni alteran el flujo de antecedentes.
    public string KpiReadinessPercent
    {
        get
        {
            if (KpiTotalCount == 0)
                return "0%";
            return $"{(int)Math.Round(KpiVigentesCount * 100d / KpiTotalCount, MidpointRounding.AwayFromZero)}%";
        }
    }

    public string KpiReadinessLabel => KpiTotalCount == 0
        ? "Aun no hay documentos incorporados"
        : $"{KpiVigentesCount} vigentes · {KpiPendientesMandanteCount} pendientes de confirmar";

    public string KpiLastReviewLabel
    {
        get
        {
            var lastDate = allRows
                .Select(row => row.FechaDocumento)
                .Where(date => date != null)
                .Select(date => date!.Value.Date)
                .DefaultIfEmpty()
                .Max();

            if (lastDate == default)
                return "Sin registro";
            if (lastDate == DateTime.Today)
                return "Hoy";
            return lastDate.ToString("dd MMM", CultureInfo.GetCultureInfo("es-CL"));
        }
    }

    public string KpiLastReviewResponsible
    {
        get
        {
            var owner = allRows
                .Where(row => row.FechaDocumento != null)
                .OrderByDescending(row => row.FechaDocumento)
                .Select(row => row.ResponsableRevision?.Trim())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            return string.IsNullOrWhiteSpace(owner) ? "Sin responsable asignado" : $"Responsable: {owner}";
        }
    }

    public string KpiExpedienteSubtext => KpiTotalCount == 0
        ? "Sin documentos incorporados"
        : $"{KpiPlanosCount} Planos · {KpiEspecCount} EETT · {KpiAddendasCount + KpiConsultasCount} Anexos";

    public string KpiVigentesRatioText => KpiTotalCount == 0
        ? "0 / 0"
        : $"{KpiVigentesCount} / {KpiTotalCount} ({KpiReadinessPercent})";

    public string KpiVigenciaSubtext
    {
        get
        {
            int hist = allRows.Count(r => !r.EsVigente && !r.EsPendienteMandante);
            return hist == 0 ? "● Todo el expediente vigente" : $"● {hist} revisiones archivadas/sustituidas";
        }
    }

    public int KpiCcddCount => allRows.Count(r =>
        (r.Disciplina != null && (r.Disciplina.IndexOf("débil", StringComparison.OrdinalIgnoreCase) >= 0 || r.Disciplina.IndexOf("ccdd", StringComparison.OrdinalIgnoreCase) >= 0)) ||
        (r.Codigo != null && r.Codigo.IndexOf("ccdd", StringComparison.OrdinalIgnoreCase) >= 0));

    public int KpiConArchivoCount => allRows.Count(r => r.HasFile);

    public string FechaCorteChip => SinFechaCorte || FechaCorte == null
        ? "Sin fecha de corte"
        : $"Corte · {FechaCorte.Value.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("es-CL"))}";

    public string ListCountLabel
    {
        get
        {
            int n = FilteredRows.Count;
            return n == 1 ? "1 documento" : $"{n} documentos";
        }
    }

    public string SelectedNavFilter => selectedNavFilter;
    public string? SelectedPlanoDisciplina => selectedPlanoDisciplina;
    public string InspectionFilter => inspectionFilter;

    public bool IsTipoTodos => string.Equals(TipoFilter, "Todos", StringComparison.OrdinalIgnoreCase);
    public bool IsTipoPlano => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoEspec => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoAddenda => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoConsulta => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase);

    public bool IsNavTodos => string.Equals(selectedNavFilter, "Todos", StringComparison.OrdinalIgnoreCase) && string.Equals(inspectionFilter, "Todos", StringComparison.OrdinalIgnoreCase);
    public bool IsNavPlanosTodos => string.Equals(selectedNavFilter, "Planos_Todos", StringComparison.OrdinalIgnoreCase) || (string.Equals(TipoFilter, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(selectedPlanoDisciplina));
    public bool IsNavEspec => string.Equals(selectedNavFilter, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase) || string.Equals(TipoFilter, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    public bool IsNavAddenda => string.Equals(selectedNavFilter, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase) || string.Equals(TipoFilter, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase);
    public bool IsNavConsulta => string.Equals(selectedNavFilter, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase) || string.Equals(TipoFilter, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase);

    public bool IsFilterSoloVigentes => string.Equals(inspectionFilter, "SoloVigentes", StringComparison.OrdinalIgnoreCase);
    public bool IsFilterConArchivo => string.Equals(inspectionFilter, "ConArchivo", StringComparison.OrdinalIgnoreCase);

    public string SyncPlanosHelp => "Copia planos vigentes al listado de planos de la oferta PDF.";
    public string AppendExclusionsHelp => "Agrega exclusiones marcadas al texto de exclusiones de la oferta.";

    public ICommand AddAntecedenteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand MarkVigenteCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand OpenAttachedFileCommand { get; }
    public ICommand OpenContainingFolderCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SyncPlanosCommand { get; }
    public ICommand AppendExclusionsCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand SelectTipoFilterCommand { get; }
    public ICommand SelectNavFilterCommand { get; }
    public ICommand ToggleInspectionFilterCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand CopyFilePathCommand { get; }

    private void SelectTipoFilter(string? tipo) => SelectNavFilter(tipo);

    public void SelectNavFilter(string? target)
    {
        var filter = string.IsNullOrWhiteSpace(target) ? "Todos" : target.Trim();
        selectedNavFilter = filter;

        if (filter.StartsWith("Plano_Esp:", StringComparison.OrdinalIgnoreCase))
        {
            tipoFilter = "Plano";
            selectedPlanoDisciplina = filter.Substring("Plano_Esp:".Length);
        }
        else if (string.Equals(filter, "Planos_Todos", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "Plano", StringComparison.OrdinalIgnoreCase))
        {
            tipoFilter = "Plano";
            selectedPlanoDisciplina = null;
        }
        else
        {
            tipoFilter = filter;
            selectedPlanoDisciplina = null;
        }

        UpdatePlanoEspecialidadSelection();
        ApplyFilter();
        NotifyNavFlags();
    }

    public void ToggleInspectionFilter(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        inspectionFilter = string.Equals(inspectionFilter, target, StringComparison.OrdinalIgnoreCase) ? "Todos" : target;
        ApplyFilter();
        NotifyNavFlags();
    }

    private void NotifyNavFlags()
    {
        OnPropertyChanged(nameof(SelectedNavFilter));
        OnPropertyChanged(nameof(SelectedPlanoDisciplina));
        OnPropertyChanged(nameof(InspectionFilter));
        OnPropertyChanged(nameof(IsNavTodos));
        OnPropertyChanged(nameof(IsNavPlanosTodos));
        OnPropertyChanged(nameof(IsNavEspec));
        OnPropertyChanged(nameof(IsNavAddenda));
        OnPropertyChanged(nameof(IsNavConsulta));
        OnPropertyChanged(nameof(IsFilterSoloVigentes));
        OnPropertyChanged(nameof(IsFilterConArchivo));
        NotifyTipoFlags();
    }

    private void NotifyTipoFlags()
    {
        OnPropertyChanged(nameof(IsTipoTodos));
        OnPropertyChanged(nameof(IsTipoPlano));
        OnPropertyChanged(nameof(IsTipoEspec));
        OnPropertyChanged(nameof(IsTipoAddenda));
        OnPropertyChanged(nameof(IsTipoConsulta));
    }

    private void UpdateEspecialidadesPlanos()
    {
        var planos = allRows.Where(r => r.IsTipoPlano).ToList();
        var groups = planos
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Disciplina) ? "Sin clasificar" : r.Disciplina.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var existingMap = EspecialidadesPlanos.ToDictionary(x => x.Disciplina, StringComparer.OrdinalIgnoreCase);
        var toRemove = EspecialidadesPlanos.Where(x => !groups.Any(g => string.Equals(g.Key, x.Disciplina, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var rem in toRemove)
            EspecialidadesPlanos.Remove(rem);

        foreach (var g in groups)
        {
            var disc = g.Key;
            var count = g.Count();
            var icon = GetIconForDisciplina(disc);
            var isSel = string.Equals(tipoFilter, "Plano", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(selectedPlanoDisciplina)
                && string.Equals(selectedPlanoDisciplina, disc, StringComparison.OrdinalIgnoreCase);

            if (existingMap.TryGetValue(disc, out var existing))
            {
                existing.Count = count;
                existing.IsSelected = isSel;
            }
            else
            {
                var item = new PlanoEspecialidadFilterItem
                {
                    Disciplina = disc,
                    DisplayName = disc,
                    Icon = icon,
                    Count = count,
                    IsSelected = isSel,
                    SelectCommand = new RelayCommand(() => SelectNavFilter($"Plano_Esp:{disc}"))
                };
                EspecialidadesPlanos.Add(item);
            }
        }
        UpdatePlanoEspecialidadSelection();
    }

    private void UpdatePlanoEspecialidadSelection()
    {
        foreach (var item in EspecialidadesPlanos)
        {
            item.IsSelected = string.Equals(tipoFilter, "Plano", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(selectedPlanoDisciplina)
                && string.Equals(item.Disciplina, selectedPlanoDisciplina, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetIconForDisciplina(string? disciplina)
    {
        if (string.IsNullOrWhiteSpace(disciplina)) return "📐";
        var d = disciplina.Trim();
        if (d.IndexOf("eléctr", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("electr", StringComparison.OrdinalIgnoreCase) >= 0)
            return "⚡";
        if (d.IndexOf("débil", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("debil", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("ccdd", StringComparison.OrdinalIgnoreCase) >= 0)
            return "📡";
        if (d.IndexOf("telecom", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0)
            return "📶";
        if (d.IndexOf("clima", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("hvac", StringComparison.OrdinalIgnoreCase) >= 0)
            return "❄️";
        if (d.IndexOf("sanitar", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("agua", StringComparison.OrdinalIgnoreCase) >= 0)
            return "💧";
        if (d.IndexOf("arquitec", StringComparison.OrdinalIgnoreCase) >= 0)
            return "🏛️";
        if (d.IndexOf("estructur", StringComparison.OrdinalIgnoreCase) >= 0)
            return "🏗️";
        if (d.IndexOf("seguridad", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("incendio", StringComparison.OrdinalIgnoreCase) >= 0)
            return "🚨";
        return "📐";
    }

    private void NotifyListMeta()
    {
        OnPropertyChanged(nameof(ListCountLabel));
    }

    public void Refresh() => Load();

    public void Load()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(IsLoading));
        try
        {
            ShowEmptyState = !projectWorkspace.HasOpenProject;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsReadOnly));
            if (ShowEmptyState)
            {
                allRows.Clear();
                FilteredRows.Clear();
                SummaryText = "";
                isDirty = false;
                return;
            }

            documents.DiscardChanges();
            allRows.Clear();
            foreach (var row in documents.GetRows())
                allRows.Add(new AntecedenteRowVm(row, OnRowEdited));

            fechaCorte = documents.GetFechaCorte();
            sinFechaCorte = fechaCorte == null;
            OnPropertyChanged(nameof(FechaCorte));
            OnPropertyChanged(nameof(SinFechaCorte));

            var summary = documents.Summarize();
            SummaryText = $"{summary.Total} documentos · {summary.Planos} planos · {summary.PendientesMandante} pendientes";
            isDirty = false;
            StatusMessage = "";
            UpdateEspecialidadesPlanos();
            ApplyFilter();
            NotifyDirty();
            NotifyKpisChanged();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public bool TryDiscardChanges()
    {
        if (!HasPendingChanges)
            return true;
        Cancel();
        return true;
    }

    private void AddAntecedenteConTipo()
    {
        if (session.IsReadOnly)
            return;
        var tipo = WpfDialogService.PromptChoice(
            "Agregar antecedente",
            "Tipo de antecedente a agregar:",
            AntecedenteTecnicoTipo.All);
        if (tipo == null)
            return;
        AddNew(tipo);
    }

    private void AddNew(string tipo)
    {
        if (session.IsReadOnly)
            return;

        string? disciplina = null;
        if (string.Equals(tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase))
        {
            disciplina = WpfDialogService.PromptChoice(
                "Especialidad del plano",
                "¿De qué especialidad son los planos a agregar?",
                AntecedenteDisciplina.All,
                AntecedenteDisciplina.Electricidad);
            if (disciplina == null)
                return;

            var modo = WpfDialogService.PromptChoice(
                $"Incorporar planos · {disciplina}",
                "Seleccione cómo desea seleccionar los planos:",
                new[] { "Seleccionar archivos de planos (con vista previa)", "Importar carpeta completa" },
                "Seleccionar archivos de planos (con vista previa)");
            if (modo == null)
                return;

            string[] filePaths;
            if (modo.StartsWith("Seleccionar archivos", StringComparison.OrdinalIgnoreCase))
            {
                filePaths = WpfDialogService.PromptSelectBlueprintFiles($"Seleccionar planos de {disciplina}");
            }
            else
            {
                var folderPath = WpfDialogService.PromptSelectFolder($"Seleccionar carpeta de planos · {disciplina}");
                if (folderPath == null)
                    return;
                filePaths = WpfDialogService.PromptSelectFilesInFolder(folderPath, "Agregar planos desde carpeta");
            }

            if (filePaths == null || filePaths.Length == 0)
                return;

            AddFiles(tipo, filePaths, disciplina);
        }
        else
        {
            var modo = WpfDialogService.PromptChoice(
                $"Incorporar {tipo}",
                "Seleccione cómo desea agregar los documentos:",
                new[] { "Seleccionar archivos directamente", "Importar carpeta completa" },
                "Seleccionar archivos directamente");
            if (modo == null)
                return;

            string[] filePaths;
            if (modo.StartsWith("Seleccionar archivos", StringComparison.OrdinalIgnoreCase))
            {
                filePaths = WpfDialogService.PromptOpenDocuments();
            }
            else
            {
                var folderPath = WpfDialogService.PromptSelectFolder($"Seleccionar carpeta con archivos de tipo \"{tipo}\"");
                if (folderPath == null)
                    return;
                filePaths = WpfDialogService.PromptSelectFilesInFolder(folderPath, "Agregar antecedente");
            }

            if (filePaths == null || filePaths.Length == 0)
                return;

            AddFiles(tipo, filePaths, null);
        }
    }

    // Separado de AddNew() para poder testear el comportamiento de agregado masivo (dedup +
    // un solo refiltrado al final) sin depender de los diálogos reales de carpeta/archivo.
    internal void AddFiles(string tipo, string[] filePaths, string? disciplina = null)
    {
        var existingCodes = allRows
            .Where(x => string.Equals(x.Tipo, tipo, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Codigo?.Trim() ?? "")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ApplyFilter() recorre y reordena TODA la lista (documents.Filter). Llamarlo una
        // vez POR ARCHIVO al importar una carpeta grande de planos (habitual: decenas o cientos
        // de PDFs) es O(n²) y se siente lento/pegado; se agrega cada fila en crudo y se refiltra
        // una sola vez al final.
        AntecedenteRowVm? last = null;
        int skipped = 0;
        bool isPlano = string.Equals(tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase);
        foreach (var filePath in filePaths)
        {
            var baseCodigo = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var codigo = isPlano && disciplina != null
                ? AntecedenteDisciplina.ApplyCodigoPrefix(baseCodigo, disciplina)
                : baseCodigo;
            if (!string.IsNullOrWhiteSpace(codigo) && existingCodes.Contains(codigo))
            {
                skipped++;
                continue;
            }
            last = AddOneRowRaw(tipo, filePath, disciplina);
            if (!string.IsNullOrWhiteSpace(codigo))
                existingCodes.Add(codigo);
        }

        if (last != null)
        {
            UpdateEspecialidadesPlanos();
            if (!string.IsNullOrWhiteSpace(disciplina) && isPlano)
            {
                SelectNavFilter($"Plano_Esp:{disciplina}");
            }
            else
            {
                EnsureVisibleInFilter(tipo);
                ApplyFilter();
            }
            SelectedRow = last;
            MarkDirtyLocal();
            NotifyKpisChanged();
        }
        if (skipped > 0)
            StatusMessage = $"{skipped} archivo(s) omitido(s) por tener el mismo código que un registro \"{tipo}\" existente.";
    }

    private AntecedenteRowVm AddOneRowRaw(string tipo, string? filePath, string? disciplina = null)
    {
        var row = documents.AddNew(tipo);
        var vm = new AntecedenteRowVm(row, OnRowEdited);
        if (!string.IsNullOrWhiteSpace(disciplina))
            vm.Disciplina = disciplina;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            vm.RutaArchivo = filePath;
            if (string.IsNullOrWhiteSpace(vm.Codigo))
            {
                var baseCodigo = System.IO.Path.GetFileNameWithoutExtension(filePath);
                vm.Codigo = !string.IsNullOrWhiteSpace(disciplina)
                    && string.Equals(tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase)
                    ? AntecedenteDisciplina.ApplyCodigoPrefix(baseCodigo, disciplina)
                    : baseCodigo;
            }
        }
        allRows.Add(vm);
        return vm;
    }

    private void EnsureVisibleInFilter(string tipo)
    {
        if (!string.Equals(TipoFilter, "Todos", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(TipoFilter, tipo, StringComparison.OrdinalIgnoreCase))
            TipoFilter = "Todos";
    }

    private void DuplicateSelected()
    {
        if (session.IsReadOnly || SelectedRow == null) return;
        var copy = documents.Duplicate(SelectedRow.Model);
        var vm = new AntecedenteRowVm(copy, OnRowEdited);
        allRows.Add(vm);
        UpdateEspecialidadesPlanos();
        EnsureVisibleInFilter(copy.Tipo);
        ApplyFilter();
        SelectedRow = vm;
        MarkDirtyLocal();
        NotifyKpisChanged();
    }

    private void RemoveSelected()
    {
        if (session.IsReadOnly || SelectedRow == null) return;
        if (!WpfDialogService.Confirm("Quitar antecedente", "¿Quitar el registro seleccionado?", isWarning: true))
            return;
        documents.Remove(SelectedRow.Model);
        allRows.Remove(SelectedRow);
        SelectedRow = null;
        UpdateEspecialidadesPlanos();
        ApplyFilter();
        MarkDirtyLocal();
        NotifyKpisChanged();
    }

    private void NotifyKpisChanged()
    {
        OnPropertyChanged(nameof(KpiPlanosCount));
        OnPropertyChanged(nameof(KpiEspecCount));
        OnPropertyChanged(nameof(KpiAddendasCount));
        OnPropertyChanged(nameof(KpiConsultasCount));
        OnPropertyChanged(nameof(KpiCcddCount));
        OnPropertyChanged(nameof(KpiConArchivoCount));
        OnPropertyChanged(nameof(KpiTotalCount));
        OnPropertyChanged(nameof(KpiVigentesCount));
        OnPropertyChanged(nameof(KpiPendientesMandanteCount));
        OnPropertyChanged(nameof(HasPendingAddendas));
        OnPropertyChanged(nameof(KpiReadinessPercent));
        OnPropertyChanged(nameof(KpiReadinessLabel));
        OnPropertyChanged(nameof(KpiExpedienteSubtext));
        OnPropertyChanged(nameof(KpiVigentesRatioText));
        OnPropertyChanged(nameof(KpiVigenciaSubtext));
        OnPropertyChanged(nameof(KpiLastReviewLabel));
        OnPropertyChanged(nameof(KpiLastReviewResponsible));
        OnPropertyChanged(nameof(FechaCorteChip));
        NotifyListMeta();
    }

    private void MarkVigente()
    {
        if (session.IsReadOnly || SelectedRow == null) return;
        documents.MarkVigente(SelectedRow.Model);
        foreach (var row in allRows)
            row.RefreshEstado();
        MarkDirtyLocal();
    }

    private void AttachFile()
    {
        if (session.IsReadOnly || SelectedRow == null) return;
        var path = WpfDialogService.PromptOpenDocument();
        if (path == null) return;
        SelectedRow.RutaArchivo = path;
        ((RelayCommand<object>)OpenAttachedFileCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenContainingFolderCommand).RaiseCanExecuteChanged();
        UpdateFilePreview();
        MarkDirtyLocal();
    }

    private void OpenAttachedFile(object? param = null)
    {
        var row = param as AntecedenteRowVm ?? SelectedRow;
        var path = row?.RutaArchivo;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!QubiCostElectrical.Wpf.Infrastructure.Security.SafeProcessLauncher.TryOpenFile(path, out var error))
        {
            WpfDialogService.Error("Abrir archivo", error ?? "No se pudo abrir el archivo de forma segura.");
        }
    }

    private void OpenContainingFolder()
    {
        var path = SelectedRow?.RutaArchivo;
        if (string.IsNullOrWhiteSpace(path))
            return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
        {
            if (!QubiCostElectrical.Wpf.Infrastructure.Security.SafeProcessLauncher.TryOpenFolder(dir, out var error))
            {
                WpfDialogService.Error("Abrir carpeta", error ?? "No se pudo abrir la carpeta del archivo.");
            }
        }
        else
        {
            WpfDialogService.Error("Abrir carpeta", "La carpeta contenedora del archivo no existe o no es accesible.");
        }
    }

    private void CopyFilePath()
    {
        var path = SelectedRow?.RutaArchivo;
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            System.Windows.Clipboard.SetText(path);
            StatusMessage = "Ruta de archivo copiada al portapapeles.";
            ShellNotificationService.Instance.ShowCopied("Ruta de archivo copiada al portapapeles.");
        }
        catch
        {
        }
    }

    private void UpdateFilePreview() => previewCoordinator.UpdateFilePreview();

    private void Save()
    {
        if (session.IsReadOnly)
            return;
        documents.SetFechaCorte(SinFechaCorte ? null : FechaCorte);
        documents.SaveChanges();
        isDirty = false;
        StatusMessage = "Antecedentes guardados en el proyecto. Use Guardar en la barra superior para persistir el archivo.";
        ShellNotificationService.Instance.ShowSuccess("Antecedentes guardados", "Los cambios se guardaron en el proyecto activo.");
        shell.RefreshChrome();
        NotifyDirty();
        Load();
    }

    private void Cancel()
    {
        documents.DiscardChanges();
        Load();
    }

    private void SyncPlanos()
    {
        if (session.IsReadOnly)
            return;
        bool fromDraft = HasPendingChanges;
        int count = documents.SyncPlanosToOffer();
        if (count > 0)
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        StatusMessage = count > 0
            ? fromDraft
                ? $"Se sincronizaron {count} planos vigentes del borrador actual a la oferta."
                : $"Se sincronizaron {count} planos vigentes a la oferta."
            : "No había planos vigentes para sincronizar.";
        if (count > 0)
        {
            ShellNotificationService.Instance.ShowSuccess("Planos sincronizados", $"Se sincronizaron {count} planos vigentes a la oferta.");
        }
        shell.RefreshChrome();
    }

    private void AppendExclusions(bool replace)
    {
        if (session.IsReadOnly)
            return;
        int count = documents.AppendExclusionsToOffer(replace);
        if (count > 0)
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        StatusMessage = count > 0
            ? $"Se incorporaron {count} exclusiones al texto de oferta."
            : "No había exclusiones marcadas para incorporar.";
        shell.RefreshChrome();
    }

    private void ApplyFilter()
    {
        IEnumerable<AntecedenteRowVm> query = allRows;

        if (string.Equals(tipoFilter, "Plano", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsTipoPlano);
            if (!string.IsNullOrWhiteSpace(selectedPlanoDisciplina))
            {
                query = query.Where(x => string.Equals(x.Disciplina?.Trim(), selectedPlanoDisciplina.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }
        else if (string.Equals(tipoFilter, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsTipoEspec);
        }
        else if (string.Equals(tipoFilter, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsTipoAddenda);
        }
        else if (string.Equals(tipoFilter, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsTipoConsulta);
        }

        if (string.Equals(inspectionFilter, "SoloVigentes", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.EsVigente && !x.EsPendienteMandante);
        }
        else if (string.Equals(inspectionFilter, "ConArchivo", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.HasFile);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string s = SearchText.Trim();
            query = query.Where(x =>
                (x.Codigo ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Descripcion ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Revision ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Disciplina ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Notas ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.ResponsableRevision ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.RutaArchivoNombre ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var result = query
            .OrderByDescending(x => x.FechaDocumento)
            .ThenBy(x => x.Codigo)
            .ToList();

        FilteredRows.Clear();
        foreach (var vm in result)
            FilteredRows.Add(vm);

        if (SelectedRow == null || !FilteredRows.Contains(SelectedRow))
            SelectedRow = FilteredRows.FirstOrDefault();

        NotifyListMeta();
    }

    private void MarkDirtyLocal()
    {
        if (session.IsReadOnly)
            return;
        isDirty = true;
        if (string.IsNullOrWhiteSpace(StatusMessage) || StatusMessage.StartsWith("Antecedentes guardados", StringComparison.Ordinal))
            StatusMessage = "Cambios pendientes de guardar";
        NotifyDirty();
    }

    private void NotifyDirty()
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        RaiseToolbarCommandStates();
        shell.RefreshChrome();
    }

    private void RaiseToolbarCommandStates()
    {
        ((RelayCommand)AddAntecedenteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SyncPlanosCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AppendExclusionsCommand).RaiseCanExecuteChanged();
    }

    private void RaiseSelectionCommands()
    {
        ((RelayCommand)DuplicateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MarkVigenteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AttachFileCommand).RaiseCanExecuteChanged();
        ((RelayCommand<object>)OpenAttachedFileCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenContainingFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CopyFilePathCommand).RaiseCanExecuteChanged();
    }

    private void OnRowEdited()
    {
        UpdateEspecialidadesPlanos();
        MarkDirtyLocal();
        NotifyKpisChanged();
    }

    private void ExportPdf()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Antecedentes_{session.ProjectInfo.Nombre ?? "Proyecto"}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                if (OperationFailureMessages.IsFileLocked(sfd.FileName))
                {
                    WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                    return;
                }

                DocumentsPdfExporter.Export(session.ProjectInfo, FilteredRows.ToList(), sfd.FileName);
                WpfDialogService.Info("Exportación Exitosa", $"Se ha exportado el registro de antecedentes a {sfd.FileName}");
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error al exportar", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
