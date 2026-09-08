using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Documents;

public sealed partial class DocumentsViewModel : ViewModelBase, IPageWithPendingChanges, IRefreshablePage
{
    public DocumentsViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions, host.Documents) { }

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
        OpenAttachedFileCommand = new RelayCommand(OpenAttachedFile, () => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.RutaArchivo));
        SaveCommand = new RelayCommand(Save, () => HasPendingChanges && !session.IsReadOnly);
        CancelCommand = new RelayCommand(Cancel, () => HasPendingChanges && !session.IsReadOnly);
        SyncPlanosCommand = new RelayCommand(SyncPlanos, () => !ShowEmptyState && !session.IsReadOnly);
        AppendExclusionsCommand = new RelayCommand(() => AppendExclusions(replace: false), () => !ShowEmptyState && !session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !ShowEmptyState && permissions.CanExportDocuments);
        SelectTipoFilterCommand = new RelayCommand<string>(SelectTipoFilter);
    }

    public bool CanEdit => !ShowEmptyState && !session.IsReadOnly;
    public bool IsReadOnly => session.IsReadOnly;
    public bool IsLoading { get; private set; }

    public ObservableCollection<AntecedenteRowVm> FilteredRows { get; }
    public ObservableCollection<string> TipoOptions { get; }
    public ObservableCollection<string> TipoChoices { get; } = new(AntecedenteTecnicoTipo.All);

    public AntecedenteRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            SetProperty(ref selectedRow, value);
            OnPropertyChanged(nameof(HasSelectedRow));
            RaiseSelectionCommands();
        }
    }

    public bool HasSelectedRow => SelectedRow != null;

    public string SearchText
    {
        get => searchText;
        set { if (SetProperty(ref searchText, value)) { ApplyFilter(); NotifyListMeta(); } }
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

    public bool IsTipoTodos => string.Equals(TipoFilter, "Todos", StringComparison.OrdinalIgnoreCase);
    public bool IsTipoPlano => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoEspec => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoAddenda => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoConsulta => string.Equals(TipoFilter, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase);

    public string SyncPlanosHelp => "Copia planos vigentes al listado de planos de la oferta PDF.";
    public string AppendExclusionsHelp => "Agrega exclusiones marcadas al texto de exclusiones de la oferta.";

    public ICommand AddAntecedenteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand MarkVigenteCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand OpenAttachedFileCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SyncPlanosCommand { get; }
    public ICommand AppendExclusionsCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand SelectTipoFilterCommand { get; }

    private void SelectTipoFilter(string? tipo)
    {
        TipoFilter = string.IsNullOrWhiteSpace(tipo) ? "Todos" : tipo.Trim();
    }

    private void NotifyTipoFlags()
    {
        OnPropertyChanged(nameof(IsTipoTodos));
        OnPropertyChanged(nameof(IsTipoPlano));
        OnPropertyChanged(nameof(IsTipoEspec));
        OnPropertyChanged(nameof(IsTipoAddenda));
        OnPropertyChanged(nameof(IsTipoConsulta));
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
        var folderPath = WpfDialogService.PromptSelectFolder($"Seleccionar carpeta con archivos de tipo \"{tipo}\"");
        if (folderPath == null)
            return;

        var filePaths = WpfDialogService.PromptSelectFilesInFolder(folderPath, "Agregar antecedente");
        if (filePaths.Length == 0)
            return;

        AddFiles(tipo, filePaths);
    }

    // Separado de AddNew() para poder testear el comportamiento de agregado masivo (dedup +
    // un solo refiltrado al final) sin depender de los diálogos reales de carpeta/archivo.
    internal void AddFiles(string tipo, string[] filePaths)
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
        foreach (var filePath in filePaths)
        {
            var codigo = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if (!string.IsNullOrWhiteSpace(codigo) && existingCodes.Contains(codigo))
            {
                skipped++;
                continue;
            }
            last = AddOneRowRaw(tipo, filePath);
            if (!string.IsNullOrWhiteSpace(codigo))
                existingCodes.Add(codigo);
        }

        if (last != null)
        {
            EnsureVisibleInFilter(tipo);
            ApplyFilter();
            SelectedRow = last;
            MarkDirtyLocal();
            NotifyKpisChanged();
        }
        if (skipped > 0)
            StatusMessage = $"{skipped} archivo(s) omitido(s) por tener el mismo código que un registro \"{tipo}\" existente.";
    }

    private AntecedenteRowVm AddOneRowRaw(string tipo, string? filePath)
    {
        var row = documents.AddNew(tipo);
        var vm = new AntecedenteRowVm(row, OnRowEdited);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            vm.RutaArchivo = filePath;
            if (string.IsNullOrWhiteSpace(vm.Codigo))
                vm.Codigo = System.IO.Path.GetFileNameWithoutExtension(filePath);
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
        OnPropertyChanged(nameof(KpiTotalCount));
        OnPropertyChanged(nameof(KpiVigentesCount));
        OnPropertyChanged(nameof(KpiPendientesMandanteCount));
        OnPropertyChanged(nameof(HasPendingAddendas));
        OnPropertyChanged(nameof(KpiReadinessPercent));
        OnPropertyChanged(nameof(KpiReadinessLabel));
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
        ((RelayCommand)OpenAttachedFileCommand).RaiseCanExecuteChanged();
        MarkDirtyLocal();
    }

    private void OpenAttachedFile()
    {
        var path = SelectedRow?.RutaArchivo;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!QubiCostElectrical.Wpf.Infrastructure.Security.SafeProcessLauncher.TryOpenFile(path, out var error))
        {
            WpfDialogService.Error("Abrir archivo", error ?? "No se pudo abrir el archivo de forma segura.");
        }
    }

    private void Save()
    {
        if (session.IsReadOnly)
            return;
        documents.SetFechaCorte(SinFechaCorte ? null : FechaCorte);
        documents.SaveChanges();
        isDirty = false;
        StatusMessage = "Antecedentes guardados en el proyecto. Use Guardar en la barra superior para persistir el archivo.";
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
        var filtered = documents.Filter(SearchText, TipoFilter);
        var ids = filtered.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        FilteredRows.Clear();
        foreach (var vm in allRows.Where(x => ids.Contains(x.Model.Id)))
            FilteredRows.Add(vm);
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
        ((RelayCommand)OpenAttachedFileCommand).RaiseCanExecuteChanged();
    }

    public void OnGridEdited() => OnRowEdited();

    private void OnRowEdited()
    {
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
                DocumentsPdfExporter.Export(session.ProjectInfo, FilteredRows.ToList(), sfd.FileName);
                WpfDialogService.Info("Exportación Exitosa", $"Se ha exportado el registro de antecedentes a {sfd.FileName}");
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error al exportar", $"Ocurrió un error al generar el PDF: {ex.Message}");
        }
    }
}
