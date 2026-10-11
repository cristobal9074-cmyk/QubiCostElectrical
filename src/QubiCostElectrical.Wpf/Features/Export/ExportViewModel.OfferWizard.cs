using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed partial class ExportViewModel
{
    private RelayCommand goToFinalReviewCommand = null!;
    private RelayCommand goToConfigureStepCommand = null!;
    private RelayCommand goToPreviewStepCommand = null!;
    private RelayCommand goToExportStepCommand = null!;
    private RelayCommand saveOfferSettingsCommand = null!;
    private RelayCommand deliverPackageCommand = null!;
    private RelayCommand pickLogoCommand = null!;
    private RelayCommand pickClientLogoCommand = null!;
    private RelayCommand pickSignatureCommand = null!;
    private RelayCommand openPreviewFolderCommand = null!;
    private RelayCommand selectAllDocumentsCommand = null!;

    private OfferWizardStep currentStep = OfferWizardStep.Configure;
    private bool isBusy;
    private string deliverySummaryText = "Selecciona los documentos para preparar la exportación.";
    private string selectedOfferPreset = "";
    private RelayCommand<string> applyOfferPresetCommand = null!;

    partial void InitializeOfferWizard()
    {
        goToFinalReviewCommand = new RelayCommand(
            () => shell?.RequestNavigate(ShellRoute.Review),
            () => shell != null && HasOpenProject);
        goToConfigureStepCommand = new RelayCommand(() => CurrentStep = OfferWizardStep.Configure, () => HasOpenProject);
        goToPreviewStepCommand = new RelayCommand(() => CurrentStep = OfferWizardStep.Preview, () => HasOpenProject);
        goToExportStepCommand = new RelayCommand(() => CurrentStep = OfferWizardStep.Export, () => HasOpenProject && CanExportActions);
        saveOfferSettingsCommand = new RelayCommand(SaveOfferSettings, () => HasOpenProject && !session.IsReadOnly);
        deliverPackageCommand = new RelayCommand(async () => await DeliverPackageAsync(), CanDeliverPackage);
        pickLogoCommand = new RelayCommand(PickLogo, () => !session.IsReadOnly);
        pickClientLogoCommand = new RelayCommand(PickClientLogo, () => !session.IsReadOnly);
        pickSignatureCommand = new RelayCommand(PickSignature, () => !session.IsReadOnly);
        openPreviewFolderCommand = new RelayCommand(OpenPreviewFolder, () => ExportResult.PreviewFilePath != null);
        applyOfferPresetCommand = new RelayCommand<string>(ApplyOfferPreset, _ => HasOpenProject && !session.IsReadOnly);
        selectAllDocumentsCommand = new RelayCommand(SelectAllDocuments, () => HasOpenProject && !session.IsReadOnly);

        OfferSettings = new OfferSettingsEditorVm(session, () => !session.IsReadOnly, () => permissions.CanManageAppSettings);
        DocumentSelector = new DeliveryDocumentSelectorVm(dashboard);
        DocumentSelector.SelectionChanged += (_, _) => OnDocumentSelectionChanged();
        ExportResult = new DeliveryExportResultVm();
        HookLivePreview();
    }

    public OfferSettingsEditorVm OfferSettings { get; private set; } = null!;
    public DeliveryDocumentSelectorVm DocumentSelector { get; private set; } = null!;
    public DeliveryExportResultVm ExportResult { get; private set; } = null!;
    public ObservableCollection<string> DocumentErrors { get; } = new();

    public bool HasOpenProject => projectWorkspace.HasOpenProject;
    public bool IsReadOnly => session.IsReadOnly;
    public bool CanEdit => !session.IsReadOnly;
    public bool HasBlockers => Blockers > 0;

    public OfferWizardStep CurrentStep
    {
        get => currentStep;
        set
        {
            if (!SetProperty(ref currentStep, value))
                return;

            OnPropertyChanged(nameof(IsConfigureStep));
            OnPropertyChanged(nameof(IsPreviewStep));
            OnPropertyChanged(nameof(IsExportStep));
            OnPropertyChanged(nameof(WizardStepTitle));
            OnPropertyChanged(nameof(IsStep1Active));
            OnPropertyChanged(nameof(IsStep1Completed));
            OnPropertyChanged(nameof(IsStep2Active));
            OnPropertyChanged(nameof(IsStep2Completed));
            OnPropertyChanged(nameof(IsStep3Active));
            OnPropertyChanged(nameof(IsStep3Completed));

            if (value is OfferWizardStep.Configure or OfferWizardStep.Preview)
                ScheduleLivePreview();
        }
    }

    public bool IsConfigureStep => CurrentStep == OfferWizardStep.Configure;
    public bool IsPreviewStep => CurrentStep == OfferWizardStep.Preview;
    public bool IsExportStep => CurrentStep == OfferWizardStep.Export;

    public bool IsStep1Active => CurrentStep == OfferWizardStep.Configure;
    public bool IsStep1Completed => (int)CurrentStep > (int)OfferWizardStep.Configure;
    public bool IsStep2Active => CurrentStep == OfferWizardStep.Preview;
    public bool IsStep2Completed => (int)CurrentStep > (int)OfferWizardStep.Preview;
    public bool IsStep3Active => CurrentStep == OfferWizardStep.Export;
    public bool IsStep3Completed => false;

    public string WizardStepTitle => CurrentStep switch
    {
        OfferWizardStep.Configure => "1. Configurar oferta",
        OfferWizardStep.Preview => "2. Vista previa",
        _ => "3. Exportar paquete"
    };

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
                RaiseCanExecuteChanged();
        }
    }

    public string DeliverySummaryText
    {
        get => deliverySummaryText;
        private set => SetProperty(ref deliverySummaryText, value);
    }

    public string SelectedOfferPreset
    {
        get => selectedOfferPreset;
        private set => SetProperty(ref selectedOfferPreset, value);
    }

    public ICommand GoToFinalReviewCommand => goToFinalReviewCommand;
    public ICommand GoToConfigureStepCommand => goToConfigureStepCommand;
    public ICommand GoToPreviewStepCommand => goToPreviewStepCommand;
    public ICommand GoToExportStepCommand => goToExportStepCommand;
    public ICommand SaveOfferSettingsCommand => saveOfferSettingsCommand;
    public ICommand DeliverPackageCommand => deliverPackageCommand;
    public ICommand PickLogoCommand => pickLogoCommand;
    public ICommand PickClientLogoCommand => pickClientLogoCommand;
    public ICommand PickSignatureCommand => pickSignatureCommand;
    public ICommand OpenPreviewFolderCommand => openPreviewFolderCommand;
    public ICommand ApplyOfferPresetCommand => applyOfferPresetCommand;

    partial void RefreshOfferWizard()
    {
        OfferSettings.LoadFromSession();
        OfferSettings.NotifyEditAccessChanged();
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(HasBlockers));
        ExportResult.Clear();
        OnDocumentSelectionChanged();
    }

    private void UpdateDeliverySummary()
    {
        var selected = DocumentSelector.Documents.Where(d => d.IsSelected).ToList();
        if (!HasOpenProject)
        {
            DeliverySummaryText = "Abre un proyecto para preparar la oferta.";
            return;
        }

        DeliverySummaryText = selected.Count == 0
            ? "Selecciona al menos un documento para incluir en el paquete."
            : $"{selected.Count} documento(s) seleccionado(s) para exportación.";
    }

    private void ApplyOfferPreset(string? preset)
    {
        if (session.IsReadOnly || string.IsNullOrWhiteSpace(preset) || DocumentSelector == null)
            return;

        SelectedOfferPreset = preset.Trim();
        foreach (var doc in DocumentSelector.Documents)
            doc.IsSelected = false;

        var ids = OfferPackagePresetCatalog.ResolveDocumentIds(SelectedOfferPreset);
        if (ids == null)
        {
            foreach (var doc in DocumentSelector.Documents)
                doc.IsSelected = true;
        }
        else
        {
            foreach (var id in ids)
                SetDocumentSelected(id, true);
        }

        UpdateDeliverySummary();
        ScheduleLivePreview();
    }

    private void SetDocumentSelected(string id, bool selected)
    {
        var doc = DocumentSelector.Documents.FirstOrDefault(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (doc != null)
            doc.IsSelected = selected;
    }

    private bool CanExportActions =>
        EvaluateExportEligibility().CanExportActions;

    private bool CanDeliverPackage() =>
        EvaluateExportEligibility().CanDeliverPackage;

    private ExportEligibility EvaluateExportEligibility() =>
        ExportEligibilityEvaluator.Evaluate(
            hasOpenProject: HasOpenProject,
            canExportDocuments: permissions.CanExportDocuments,
            isReadOnly: session.IsReadOnly,
            blockingCount: Blockers,
            isBusy: IsBusy,
            hasSelectedDocuments: DocumentSelector.Documents.Any(d => d.IsSelected));

    private void SaveOfferSettings()
    {
        if (!HasOpenProject || session.IsReadOnly)
            return;

        OfferSettings.ApplyToSession();
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        WpfDialogService.Info("Oferta", "Configuración de oferta guardada en el proyecto.");
    }

    private void PickLogo()
    {
        var path = fileDialog.PromptOpenFile("Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos|*.*");
        if (path != null)
            OfferSettings.LogoPath = path;
    }

    private void PickClientLogo()
    {
        var path = fileDialog.PromptOpenFile("Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos|*.*");
        if (path != null)
            OfferSettings.ClientLogoPath = path;
    }

    private void PickSignature()
    {
        var path = fileDialog.PromptOpenFile("Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos|*.*");
        if (path != null)
            OfferSettings.SignatureImagePath = path;
    }

    private void OpenPreviewFolder()
    {
        var path = ExportResult.PreviewFilePath;
        if (path == null)
            return;

        var folder = System.IO.Path.GetDirectoryName(path);
        if (folder != null)
            fileOpener.OpenFolder(folder);
    }

    private async Task DeliverPackageAsync()
    {
        if (!CanDeliverPackage() || IsBusy)
            return;

        if (!await EnsureExportAllowedAsync())
            return;

        var folder = fileDialog.PromptFolder("Selecciona la carpeta de destino para la exportación");
        if (folder == null)
            return;

        IsBusy = true;
        try
        {
            SaveOfferSettings();
            var ctx = DocumentSelector.GetSelectedContext(session);
            var selectedIds = DocumentSelector.GetSelectedDocumentIds();
            var baseName = ProjectTextValidator.MakeSafeFileName(ProjectTitle);
            var results = await previewService.ExportAllAsync(ctx, folder, selectedIds, baseName);
            ExportResult.SetResults(results);

            DocumentErrors.Clear();
            foreach (var fail in results.Where(r => !r.IsSuccess))
                DocumentErrors.Add($"{fail.DocumentType}: {fail.ErrorMessage}");

            var successCount = results.Count(r => r.IsSuccess);
            var failCount = results.Count(r => !r.IsSuccess);
            if (failCount == 0)
                ShowExportSuccessWithFolderAction(
                    "Oferta y exportación",
                    $"Todos los documentos ({successCount}) se generaron correctamente en:{Environment.NewLine}{folder}",
                    folder,
                    pathIsFolder: true);
            else
                ShellNotificationService.Instance.ShowWarning("Oferta y exportación", $"{successCount} documento(s) generado(s), {failCount} con errores.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló el asistente de oferta y exportación", ex);
            WpfDialogService.Error("Oferta y exportación", OperationFailureMessages.ToUserMessage(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
