using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed partial class ExportViewModel
{
    private CancellationTokenSource? livePreviewCts;
    private int livePreviewGeneration;
    private bool isLivePreviewUpdating;

    public bool IsLivePreviewUpdating
    {
        get => isLivePreviewUpdating;
        private set => SetProperty(ref isLivePreviewUpdating, value);
    }

    public PreviewDocumentSwitch PreviewSwitch { get; private set; } = null!;

    partial void HookLivePreview()
    {
        PreviewSwitch = new PreviewDocumentSwitch(ScheduleLivePreview);
        OfferSettings.PropertyChanged += OnOfferSettingsChanged;
    }

    private void OnOfferSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        ScheduleLivePreview();
    }

    public void ScheduleLivePreview()
    {
        if (!HasOpenProject || !permissions.CanExportDocuments)
            return;

        livePreviewCts?.Cancel();
        livePreviewCts = new CancellationTokenSource();
        var token = livePreviewCts.Token;
        var generation = Interlocked.Increment(ref livePreviewGeneration);
        _ = RunLivePreviewDebouncedAsync(token, generation);
    }

    private async Task RunLivePreviewDebouncedAsync(CancellationToken token, int generation)
    {
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsLivePreviewUpdating = true;
                PreviewStatusText = "Actualizando vista previa en vivo…";
            });

            await Task.Delay(850, token);
            if (token.IsCancellationRequested || generation != livePreviewGeneration)
                return;

            var ctx = BuildPreviewContext();
            var result = await previewService.GeneratePreviewAsync(ctx, PreviewSwitch.SelectedId, openExternally: false);

            if (token.IsCancellationRequested || generation != livePreviewGeneration)
                return;

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (generation != livePreviewGeneration)
                    return;

                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.FilePath))
                {
                    ExportResult.SetPreview(result.FilePath, result.OpenResult);
                    PreviewFileText = $"Vista previa en vivo · {System.IO.Path.GetFileName(result.FilePath)}";
                    PreviewStatusText = "Vista previa en vivo actualizada.";
                    await pdfPreview.LoadAsync(result.FilePath);
                }
                else
                {
                    PreviewStatusText = result.ErrorMessage ?? "No se pudo generar la vista previa.";
                    PreviewFileText = "Sin vista previa generada.";
                    ExportResult.SetPreview(null);
                }

                IsLivePreviewUpdating = false;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (generation != livePreviewGeneration)
                    return;

                PreviewStatusText = OperationFailureMessages.ToUserMessage(ex);
                PreviewFileText = "Sin vista previa generada.";
                ExportResult.SetPreview(null);
                IsLivePreviewUpdating = false;
            });
        }
    }

    private ExportProjectContext BuildPreviewContext()
    {
        var snapshot = session.CreateSnapshot();
        var offerSnapshot = snapshot.Proyecto.OfertaPdf != null
            ? CloneOfferSettings(snapshot.Proyecto.OfertaPdf)
            : new OfferPdfSettings();
        OfferSettings.CopyTo(offerSnapshot);
        var prefs = UserPreferencesStore.Load();
        LetterheadApplicator.Apply(prefs, offerSnapshot);
        return exportPresenter.BuildContext(
            ResolveScopeMode(),
            SelectedPartida?.Id,
            SelectedArea,
            offerSnapshot,
            _corporateTheme,
            _reportTemplate);
    }
}
