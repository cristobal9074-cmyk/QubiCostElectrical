using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public VersionsViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.host = host;
        this.shell = shell;
        versionComparison = host.Services.VersionComparison;
        versionWorkflow = host.Services.VersionWorkflow;

        refreshCommand = new RelayCommand(Refresh);
        newCommand = new RelayCommand(CreateVersion, () => HasProject);
        activateCommand = new RelayCommand(ActivateVersion, () => HasProject && selectedVersion != null);
        renameCommand = new RelayCommand(RenameVersion, () => HasProject && selectedVersion != null && (selectedVersion.IsDraft || selectedVersion.IsRejected));
        deleteCommand = new RelayCommand(DeleteVersion, () => HasProject && selectedVersion != null && (selectedVersion.IsDraft || selectedVersion.IsRejected));
        refreshComparisonCommand = new RelayCommand(RefreshComparison);
        goToItemCommand = new RelayCommand(GoToItem, () => selectedComparisonRow != null);
        exportComparisonCommand = new RelayCommand(ExportComparison, () => HasProject && comparisonRows.Count > 0);
        enviarARevisionCommand = new RelayCommand(EnviarARevision,
            () => HasProject && selectedVersion != null && selectedVersion.IsDraft
                  && host.Permissions.CanSendVersionToReview);
        iniciarRevisionCommand = new RelayCommand(IniciarRevision,
            () => HasProject && selectedVersion != null && selectedVersion.IsPendingReview
                  && host.Permissions.CanStartVersionReview);
        aprobarVersionCommand = new RelayCommand(AprobarVersion,
            () => HasProject && selectedVersion != null && selectedVersion.IsInReview
                  && host.Permissions.CanApproveVersions && canCurrentUserApprove);
        rechazaronVersionCommand = new RelayCommand(RechazarVersion,
            () => HasProject && selectedVersion != null
                  && (selectedVersion.IsPendingReview || selectedVersion.IsInReview)
                  && host.Permissions.CanStartVersionReview);

        versionsView = CollectionViewSource.GetDefaultView(versions);
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.FechaCreacion), ListSortDirection.Descending));
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.Codigo), ListSortDirection.Ascending));

        comparisonRowsView = CollectionViewSource.GetDefaultView(comparisonRows);
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.DeltaUf), ListSortDirection.Descending));
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.Item), ListSortDirection.Ascending));

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);

    public bool HasProject => host.ProjectWorkspace.HasOpenProject;

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }

    public ObservableCollection<VersionRowVm> Versions => versions;
    public ICollectionView FilteredVersions => versionsView;
    public ObservableCollection<VersionComparisonRowVm> ComparisonRows => comparisonRows;
    public ICollectionView FilteredComparisonRows => comparisonRowsView;

    public VersionRowVm? SelectedVersion
    {
        get => selectedVersion;
        set
        {
            if (!SetProperty(ref selectedVersion, value))
                return;

            selectedVersionHistory.Clear();
            if (value != null && value.Source.HistorialEstados != null)
            {
                foreach (var entry in value.Source.HistorialEstados)
                    selectedVersionHistory.Add(entry);
            }

            UpdateComparisonVersionOptions();
            RefreshComparison();
            UpdateSodState();
            activateCommand.RaiseCanExecuteChanged();
            renameCommand.RaiseCanExecuteChanged();
            deleteCommand.RaiseCanExecuteChanged();
            goToItemCommand.RaiseCanExecuteChanged();
            enviarARevisionCommand.RaiseCanExecuteChanged();
            iniciarRevisionCommand.RaiseCanExecuteChanged();
            aprobarVersionCommand.RaiseCanExecuteChanged();
            rechazaronVersionCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CompareAgainstCurrent
    {
        get => compareAgainstCurrent;
        set
        {
            if (!SetProperty(ref compareAgainstCurrent, value))
                return;

            OnPropertyChanged(nameof(ShowComparisonVersionSelector));
            UpdateComparisonVersionOptions();
            RefreshComparison();
        }
    }

    public bool ShowComparisonVersionSelector => !CompareAgainstCurrent;

    public ObservableCollection<VersionRowVm> ComparisonVersionOptions { get; } = new();

    public VersionRowVm? SelectedComparisonVersion
    {
        get => selectedComparisonVersion;
        set
        {
            if (!SetProperty(ref selectedComparisonVersion, value))
                return;

            RefreshComparison();
        }
    }

    public VersionComparisonRowVm? SelectedComparisonRow
    {
        get => selectedComparisonRow;
        set
        {
            if (!SetProperty(ref selectedComparisonRow, value))
                return;
            goToItemCommand.RaiseCanExecuteChanged();
        }
    }

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string ActiveVersionText
    {
        get => activeVersionText;
        private set => SetProperty(ref activeVersionText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ComparisonSummaryText
    {
        get => comparisonSummaryText;
        private set => SetProperty(ref comparisonSummaryText, value);
    }

    public string ComparisonStatusText
    {
        get => comparisonStatusText;
        private set => SetProperty(ref comparisonStatusText, value);
    }

    public string ComparisonTotalsText
    {
        get => comparisonTotalsText;
        private set => SetProperty(ref comparisonTotalsText, value);
    }

    public string ComparisonAddedText
    {
        get => comparisonAddedText;
        private set => SetProperty(ref comparisonAddedText, value);
    }

    public string ComparisonChangedText
    {
        get => comparisonChangedText;
        private set => SetProperty(ref comparisonChangedText, value);
    }

    public string ComparisonRemovedText
    {
        get => comparisonRemovedText;
        private set => SetProperty(ref comparisonRemovedText, value);
    }

    public decimal ComparisonDeltaUf
    {
        get => comparisonDeltaUf;
        private set => SetProperty(ref comparisonDeltaUf, value);
    }

    public decimal ComparisonDeltaPct
    {
        get => comparisonDeltaPct;
        private set => SetProperty(ref comparisonDeltaPct, value);
    }

    // DeltaUf contiene CLP desde la migración (nombre legado)
    public string ComparisonDeltaText => ComparisonDeltaUf == 0m ? "—"
        : $"{(ComparisonDeltaUf > 0m ? "+" : string.Empty)}$ {ComparisonDeltaUf:N0}";
    public string ComparisonDeltaPctText => ComparisonDeltaPct == 0m ? "—"
        : $"{(ComparisonDeltaPct > 0m ? "+" : string.Empty)}{ComparisonDeltaPct:N2}%";

    public string ComparisonModeHint
    {
        get => comparisonModeHint;
        private set => SetProperty(ref comparisonModeHint, value);
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand NewCommand => newCommand;
    public ICommand ActivateCommand => activateCommand;
    public ICommand RenameCommand => renameCommand;
    public ICommand DeleteCommand => deleteCommand;
    public ICommand RefreshComparisonCommand => refreshComparisonCommand;
    public ICommand GoToItemCommand => goToItemCommand;
    public ICommand ExportComparisonCommand => exportComparisonCommand;
    public ICommand EnviarARevisionCommand => enviarARevisionCommand;
    public ICommand IniciarRevisionCommand => iniciarRevisionCommand;
    public ICommand AprobarVersionCommand => aprobarVersionCommand;
    public ICommand RechazarVersionCommand => rechazaronVersionCommand;

    /// <summary>Indica si el usuario actual puede aprobar la versión seleccionada (no bloqueado por SoD).</summary>
    public bool CanCurrentUserApprove
    {
        get => canCurrentUserApprove;
        private set => SetProperty(ref canCurrentUserApprove, value);
    }

    /// <summary>Mensaje explicativo del bloqueo de aprobación (SoD o falta de rol).</summary>
    public string ApprovalBlockReason
    {
        get => approvalBlockReason;
        private set => SetProperty(ref approvalBlockReason, value);
    }

    /// <summary>Visibilidad del aviso de bloqueo SoD en la UI.</summary>
    public bool ShowApprovalBlockWarning =>
        selectedVersion != null && selectedVersion.IsInReview && !string.IsNullOrEmpty(approvalBlockReason);

    public string ComentarioReview
    {
        get => comentarioReview;
        set => SetProperty(ref comentarioReview, value ?? "");
    }

    public ObservableCollection<BudgetVersionStatusHistoryRow> SelectedVersionHistory => selectedVersionHistory;

    public void Refresh()
    {
        if (!HasProject)
        {
            versions.Clear();
            SelectedVersion = null;
            comparisonRows.Clear();
            SelectedComparisonRow = null;
            ShowEmptyState = true;
            SummaryText = "Sin proyecto abierto";
            ActiveVersionText = "Sin versión activa";
            StatusMessage = "Abre o crea un proyecto para gestionar versiones.";
            ComparisonSummaryText = "Sin comparación";
            ComparisonStatusText = "Abre un proyecto para habilitar la comparación de versiones.";
            ComparisonTotalsText = "—";
            ComparisonAddedText = "0 nuevas";
            ComparisonChangedText = "0 modificadas";
            ComparisonRemovedText = "0 eliminadas";
            ComparisonDeltaUf = 0m;
            ComparisonDeltaPct = 0m;
            ComparisonModeHint = "Abre un proyecto para comparar versiones.";
            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            newCommand.RaiseCanExecuteChanged();
            activateCommand.RaiseCanExecuteChanged();
            renameCommand.RaiseCanExecuteChanged();
            deleteCommand.RaiseCanExecuteChanged();
            return;
        }

        string? selectedVersionId = SelectedVersion?.Source.Id;
        var sorted = host.Session.Versiones
            .OrderByDescending(x => x.FechaCreacion)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        versions.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            bool isActive = string.Equals(current.Id, host.Session.ActiveVersionId, StringComparison.OrdinalIgnoreCase);
            decimal delta = i + 1 < sorted.Count
                ? current.MontoVentaUf - sorted[i + 1].MontoVentaUf
                : 0m;

            versions.Add(new VersionRowVm(current, isActive, delta));
        }

        if (versions.Count == 0)
        {
            SelectedVersion = null;
        }
        else
        {
            var match = versions.FirstOrDefault(v => string.Equals(v.Source.Id, selectedVersionId, StringComparison.OrdinalIgnoreCase));
            SelectedVersion = match ?? versions.FirstOrDefault(v => v.IsActive) ?? versions[0];
        }

        ShowEmptyState = versions.Count == 0;
        UpdateSummary();
        UpdateComparisonVersionOptions();
        RefreshComparison();
        UpdateSodState();
        newCommand.RaiseCanExecuteChanged();
        activateCommand.RaiseCanExecuteChanged();
        renameCommand.RaiseCanExecuteChanged();
        deleteCommand.RaiseCanExecuteChanged();
        enviarARevisionCommand.RaiseCanExecuteChanged();
        iniciarRevisionCommand.RaiseCanExecuteChanged();
        aprobarVersionCommand.RaiseCanExecuteChanged();
        rechazaronVersionCommand.RaiseCanExecuteChanged();
    }

    private void UpdateSodState()
    {
        if (selectedVersion == null || !selectedVersion.IsInReview)
        {
            CanCurrentUserApprove = true;
            ApprovalBlockReason = "";
            OnPropertyChanged(nameof(ShowApprovalBlockWarning));
            return;
        }

        if (!host.Permissions.CanApproveVersions)
        {
            CanCurrentUserApprove = false;
            ApprovalBlockReason = "Su rol no tiene permisos para aprobar versiones. Solo el Administrador puede aprobar.";
            OnPropertyChanged(nameof(ShowApprovalBlockWarning));
            return;
        }

        var sod = versionWorkflow.CheckApprovalSod(selectedVersion.Source, host.Session.CurrentUserEmail);
        if (sod.IsBlocked)
        {
            CanCurrentUserApprove = false;
            ApprovalBlockReason = sod.Reason;
        }
        else
        {
            CanCurrentUserApprove = true;
            ApprovalBlockReason = "";
        }

        OnPropertyChanged(nameof(ShowApprovalBlockWarning));
        aprobarVersionCommand.RaiseCanExecuteChanged();
    }

    private void UpdateSummary()
    {
        SummaryText = versions.Count == 0
            ? "Sin versiones guardadas"
            : $"{versions.Count:N0} versiones · {versions.Count(v => v.IsActive):N0} activa(s)";

        var active = versions.FirstOrDefault(v => v.IsActive);
        ActiveVersionText = active != null
            ? $"Activa: {active.Codigo} · {active.Nombre}"
            : "Sin versión activa";

        StatusMessage = versions.Count == 0
            ? "Este proyecto todavía no tiene versiones guardadas."
            : "Usa una versión para comparar, congelar o restaurar estados del proyecto.";
    }

    private void UpdateComparisonVersionOptions()
    {
        var previousId = SelectedComparisonVersion?.Source.Id;
        ComparisonVersionOptions.Clear();

        foreach (var version in versions.Where(v => v.Source.Id != SelectedVersion?.Source.Id))
            ComparisonVersionOptions.Add(version);

        SelectedComparisonVersion = ComparisonVersionOptions.FirstOrDefault(v =>
                string.Equals(v.Source.Id, previousId, StringComparison.OrdinalIgnoreCase))
            ?? ComparisonVersionOptions.FirstOrDefault();
    }

    private void RefreshComparison()
    {
        comparisonRows.Clear();

        var selected = SelectedVersion?.Source;
        if (selected == null)
        {
            ComparisonSummaryText = "Sin comparación";
            ComparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
            ComparisonTotalsText = "—";
            ComparisonAddedText = "0 nuevas";
            ComparisonChangedText = "0 modificadas";
            ComparisonRemovedText = "0 eliminadas";
            ComparisonDeltaUf = 0m;
            ComparisonDeltaPct = 0m;
            ComparisonModeHint = "Selecciona una versión para comparar.";
            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            SelectedComparisonRow = null;
            return;
        }

        string baselineLabel = $"{selected.Codigo} · {selected.Nombre}";
        BudgetVersionComparisonSummary summary;

        if (CompareAgainstCurrent)
        {
            summary = versionComparison.Compare(
                selected.Snapshot,
                host.Session.CreateSnapshot(),
                baselineLabel,
                "Estado actual");
        }
        else
        {
            var comparison = SelectedComparisonVersion?.Source;
            if (comparison == null)
            {
                ComparisonSummaryText = "Sin comparación";
                ComparisonStatusText = "Selecciona una segunda versión para comparar.";
                ComparisonTotalsText = "—";
                ComparisonAddedText = "0 nuevas";
                ComparisonChangedText = "0 modificadas";
                ComparisonRemovedText = "0 eliminadas";
                ComparisonDeltaUf = 0m;
                ComparisonDeltaPct = 0m;
                ComparisonModeHint = "Selecciona la versión B para comparar A vs B.";
                OnPropertyChanged(nameof(ComparisonDeltaText));
                OnPropertyChanged(nameof(ComparisonDeltaPctText));
                SelectedComparisonRow = null;
                return;
            }

            summary = versionComparison.Compare(
                selected.Snapshot,
                comparison.Snapshot,
                baselineLabel,
                $"{comparison.Codigo} · {comparison.Nombre}");
        }

        foreach (var row in summary.Rows)
            comparisonRows.Add(new VersionComparisonRowVm(row));

        SelectedComparisonRow = comparisonRows.FirstOrDefault();

        ComparisonSummaryText = $"{summary.Rows.Count:N0} partidas comparadas";
        ComparisonStatusText = summary.Rows.Count == 0
            ? "No hay diferencias entre la versión seleccionada y el estado actual."
            : $"Comparando {summary.BaselineLabel} contra {summary.CurrentLabel}.";
        ComparisonTotalsText = $"Base: {summary.BaselineUf:N3} UF · Actual: {summary.CurrentUf:N3} UF · Delta: {(summary.DeltaUf > 0m ? "+" : "")}{summary.DeltaUf:N3} UF ({summary.DeltaPct:N2}%)" +
                               $"\n[Impacto] Agregado: +{summary.CostoAgregadoUf:N3} UF · Eliminado: -{summary.CostoEliminadoUf:N3} UF";
        ComparisonAddedText = $"{summary.AddedCount:N0} nuevas";
        ComparisonChangedText = $"{summary.ChangedCount:N0} modificadas";
        ComparisonRemovedText = $"{summary.RemovedCount:N0} eliminadas";
        ComparisonDeltaUf = summary.DeltaUf;
        ComparisonDeltaPct = summary.DeltaPct;
        ComparisonModeHint = CompareAgainstCurrent
            ? $"Versión A = {summary.BaselineLabel} · Versión B = estado actual"
            : $"Versión A = {summary.BaselineLabel} · Versión B = {summary.CurrentLabel}";
        OnPropertyChanged(nameof(ComparisonDeltaText));
        OnPropertyChanged(nameof(ComparisonDeltaPctText));
        exportComparisonCommand.RaiseCanExecuteChanged();
    }

    private void CreateVersion()
    {
        int nextNum = host.Session.Versiones.Count + 1;
        string codigo = $"V{nextNum}";
        string nombre = $"Versión {nextNum}";

        if (!PromptVersionDialog("Nueva versión", ref codigo, ref nombre))
            return;

        var snapshot = BudgetVersionData.FromProjectFileData(host.Session.CreateSnapshot());
        decimal ventaUf = host.Dashboard.BuildPageState().Cost.TotalVentaUf;
        var version = new BudgetVersionRow
        {
            Codigo = string.IsNullOrWhiteSpace(codigo) ? $"V{nextNum}" : codigo.Trim(),
            Nombre = string.IsNullOrWhiteSpace(nombre) ? $"Versión {nextNum}" : nombre.Trim(),
            Estado = "Preliminar",
            CreadoPor = host.Session.ProjectInfo.UsuarioNombre,
            FechaCreacion = AppClock.Now,
            FechaModificacion = AppClock.Now,
            MontoVentaUf = ventaUf,
            Snapshot = snapshot
        };

        host.Session.Versiones.Add(version);
        host.Session.ActiveVersionId = version.Id;
        host.Session.MarkDirty("versiones");
        Refresh();
    }

    private void ActivateVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        host.Session.ActiveVersionId = version.Id;
        host.Session.MarkDirty("versiones");
        Refresh();
    }

    private void RenameVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        string codigo = version.Codigo;
        string nombre = version.Nombre;
        if (!PromptVersionDialog("Renombrar versión", ref codigo, ref nombre))
            return;

        version.Codigo = string.IsNullOrWhiteSpace(codigo) ? version.Codigo : codigo.Trim();
        version.Nombre = string.IsNullOrWhiteSpace(nombre) ? version.Nombre : nombre.Trim();
        version.FechaModificacion = AppClock.Now;
        host.Session.MarkDirty("versiones");
        Refresh();
    }

    private void DeleteVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        if (!WpfDialogService.Confirm(
                "Eliminar versión",
                $"¿Eliminar la versión «{version.Codigo} — {version.Nombre}»?",
                isWarning: true))
            return;

        host.Session.Versiones.Remove(version);
        if (string.Equals(host.Session.ActiveVersionId, version.Id, StringComparison.OrdinalIgnoreCase))
            host.Session.ActiveVersionId = host.Session.Versiones.LastOrDefault()?.Id ?? string.Empty;

        host.Session.MarkDirty("versiones");
        Refresh();
    }

    private void GoToItem()
    {
        var row = selectedComparisonRow;
        if (row == null)
            return;

        shell.RequestNavigate(
            ShellRoute.Takeoff,
            new TakeoffNavigationContext(row.ItemId, row.Item, row.Descripcion));
    }

    private static bool PromptVersionDialog(string title, ref string codigo, ref string nombre)
    {
        string? responseCodigo = WpfDialogService.PromptText(title, "Código de versión", codigo);
        if (responseCodigo == null)
            return false;

        string? responseNombre = WpfDialogService.PromptText(title, "Nombre de versión", nombre);
        if (responseNombre == null)
            return false;

        codigo = responseCodigo;
        nombre = responseNombre;
        return true;
    }

    private void ExportComparison()
    {
        if (comparisonRows.Count == 0)
        {
            WpfDialogService.Warning("Exportar comparación", "No hay datos comparados para exportar.");
            return;
        }

        var path = host.FileDialog.PromptSaveFile("Exportar comparación", "comparacion_versiones.csv", "Archivo CSV (*.csv)|*.csv");
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Item,Descripcion,EsTitulo,Base UF,Actual UF,Delta UF,Delta %,Estado,Detalle Cambios");

            foreach (var row in comparisonRows)
            {
                var desc = (row.Descripcion ?? "").Replace("\"", "\"\"");
                var det = (row.DetalleCambios ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"\"{row.Item}\",\"{desc}\",{row.EsTitulo},{row.BaselineUf:0.000},{row.CurrentUf:0.000},{row.DeltaUf:0.000},{row.DeltaPct:0.00},\"{row.Estado}\",\"{det}\"");
            }

            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            WpfDialogService.Info("Exportar comparación", "El informe de diferencias se ha exportado con éxito.");
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Exportar comparación", $"Error al guardar el archivo: {ex.Message}");
        }
    }

    private void EnviarARevision()
    {
        if (selectedVersion == null) return;

        string? comentario = WpfDialogService.PromptText("Enviar a revisión", "Introduce un comentario de envío:", ComentarioReview);
        if (comentario == null) return;

        var baseVersion = selectedComparisonVersion ?? versions.FirstOrDefault(v => v.IsActive) ?? selectedVersion;
        var summary = versionComparison.Compare(baseVersion.Source.Snapshot, host.Session.Data, baseVersion.Codigo, selectedVersion.Codigo);

        var result = versionWorkflow.EnviarARevision(
            host.Session,
            selectedVersion.Source.Id,
            host.Session.CurrentUserName,
            comentario,
            baseVersion.Source.Codigo,
            summary.DeltaUf);

        if (result.IsSuccess)
        {
            ComentarioReview = "";
            Refresh();
            WpfDialogService.Info("Enviar a revisión", "La versión ha sido enviada a revisión con éxito.");
        }
        else
        {
            WpfDialogService.Error("Enviar a revisión", result.Error ?? "Error al enviar a revisión.");
        }
    }

    private void IniciarRevision()
    {
        if (selectedVersion == null) return;

        var result = versionWorkflow.IniciarRevision(
            host.Session,
            selectedVersion.Source.Id,
            host.Session.CurrentUserName);

        if (result.IsSuccess)
        {
            Refresh();
            WpfDialogService.Info("Iniciar revisión", "Se ha iniciado la revisión de la versión.");
        }
        else
        {
            WpfDialogService.Error("Iniciar revisión", result.Error ?? "Error al iniciar revisión.");
        }
    }

    private void AprobarVersion()
    {
        if (selectedVersion == null) return;

        bool confirm = WpfDialogService.Confirm("Aprobar versión",
            $"¿Está seguro de que desea APROBAR la versión '{selectedVersion.Codigo}'?\n\n" +
            "Una vez aprobada, la versión quedará bloqueada en SOLO LECTURA permanente.");
        if (!confirm) return;

        string? comentario = WpfDialogService.PromptText("Aprobar versión", "Comentario de aprobación (opcional):", "");
        if (comentario == null) return;

        var result = versionWorkflow.AprobarVersion(
            host.Session,
            selectedVersion.Source.Id,
            host.Session.CurrentUserName,
            comentario);

        if (result.IsSuccess)
        {
            foreach (var v in host.Session.Versiones.Where(x => x.Id != selectedVersion.Source.Id && x.Estado == "Aprobada"))
            {
                versionWorkflow.MarcarObsoleta(host.Session, v.Id, host.Session.CurrentUserName);
            }

            Refresh();
            WpfDialogService.Info("Aprobar versión", "La versión ha sido aprobada con éxito.");
        }
        else
        {
            WpfDialogService.Error("Aprobar versión", result.Error ?? "Error al aprobar versión.");
        }
    }

    private void RechazarVersion()
    {
        if (selectedVersion == null) return;

        string? motivo = WpfDialogService.PromptText("Rechazar versión", "Introduce obligatoriamente el motivo del rechazo:", "");
        if (string.IsNullOrWhiteSpace(motivo))
        {
            WpfDialogService.Warning("Rechazar versión", "Debe especificar un motivo para poder rechazar la versión.");
            return;
        }

        var result = versionWorkflow.RechazarVersion(
            host.Session,
            selectedVersion.Source.Id,
            host.Session.CurrentUserName,
            motivo);

        if (result.IsSuccess)
        {
            Refresh();
            WpfDialogService.Info("Rechazar versión", "La versión ha sido rechazada.");
        }
        else
        {
            WpfDialogService.Error("Rechazar versión", result.Error ?? "Error al rechazar versión.");
        }
    }
}
