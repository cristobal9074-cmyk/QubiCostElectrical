using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Documents;

/// <summary>
/// Coordina la carga, renderizado asíncrono y paginación de vista previa de documentos (PDF e imágenes).
/// Desacoplado de DocumentsViewModel para mantener la responsabilidad única y el presupuesto de líneas.
/// </summary>
public sealed class DocumentsPreviewCoordinator : ObservableObject
{
    private ImageSource? previewImage;
    private bool hasPreviewImage;
    private bool isPreviewLoading;
    private string selectedFileExtensionBadge = "DOC";
    private string selectedFileSizeText = "";
    private bool selectedFileExists;
    private bool isPdfMultiPage;
    private string previewPageIndicator = "1 / 1";
    private int currentPreviewPageIndex;
    private int totalPreviewPages;
    private CancellationTokenSource? previewCts;
    private readonly Func<AntecedenteRowVm?> getSelectedRow;

    public DocumentsPreviewCoordinator(Func<AntecedenteRowVm?> getSelectedRow)
    {
        this.getSelectedRow = getSelectedRow ?? throw new ArgumentNullException(nameof(getSelectedRow));
        PreviousPreviewPageCommand = new RelayCommand(PreviousPreviewPage, () => currentPreviewPageIndex > 0);
        NextPreviewPageCommand = new RelayCommand(NextPreviewPage, () => currentPreviewPageIndex < totalPreviewPages - 1);
    }

    public ImageSource? PreviewImage { get => previewImage; private set => SetProperty(ref previewImage, value); }
    public bool HasPreviewImage { get => hasPreviewImage; private set => SetProperty(ref hasPreviewImage, value); }
    public bool IsPreviewLoading { get => isPreviewLoading; private set => SetProperty(ref isPreviewLoading, value); }
    public string SelectedFileExtensionBadge { get => selectedFileExtensionBadge; private set => SetProperty(ref selectedFileExtensionBadge, value); }
    public string SelectedFileSizeText { get => selectedFileSizeText; private set => SetProperty(ref selectedFileSizeText, value); }
    public bool SelectedFileExists { get => selectedFileExists; private set => SetProperty(ref selectedFileExists, value); }
    public bool IsPdfMultiPage { get => isPdfMultiPage; private set => SetProperty(ref isPdfMultiPage, value); }
    public string PreviewPageIndicator { get => previewPageIndicator; private set => SetProperty(ref previewPageIndicator, value); }

    public RelayCommand PreviousPreviewPageCommand { get; }
    public RelayCommand NextPreviewPageCommand { get; }

    public void UpdateFilePreview()
    {
        previewCts?.Cancel();
        var selectedRow = getSelectedRow();
        var path = selectedRow?.RutaArchivo;
        if (string.IsNullOrWhiteSpace(path))
        {
            PreviewImage = null;
            HasPreviewImage = false;
            IsPreviewLoading = false;
            SelectedFileExtensionBadge = selectedRow?.Tipo?.ToUpperInvariant() ?? "DOC";
            SelectedFileSizeText = "Sin archivo adjunto";
            SelectedFileExists = false;
            IsPdfMultiPage = false;
            totalPreviewPages = 0;
            currentPreviewPageIndex = 0;
            PreviewPageIndicator = "";
            RaisePagingCommands();
            return;
        }

        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        SelectedFileExtensionBadge = string.IsNullOrWhiteSpace(ext) ? "DOC" : ext;

        if (!File.Exists(path))
        {
            PreviewImage = null;
            HasPreviewImage = false;
            IsPreviewLoading = false;
            SelectedFileSizeText = "Archivo no encontrado";
            SelectedFileExists = false;
            IsPdfMultiPage = false;
            totalPreviewPages = 0;
            currentPreviewPageIndex = 0;
            PreviewPageIndicator = "";
            RaisePagingCommands();
            return;
        }

        SelectedFileExists = true;
        try
        {
            var fi = new FileInfo(path);
            SelectedFileSizeText = fi.Length >= 1024 * 1024
                ? $"{fi.Length / (1024.0 * 1024.0):N1} MB"
                : $"{fi.Length / 1024.0:N0} KB";
        }
        catch
        {
            SelectedFileSizeText = "Disponible";
        }

        currentPreviewPageIndex = 0;
        LoadCurrentPagePreview(path, ext);
    }

    private async void LoadCurrentPagePreview(string path, string ext)
    {
        previewCts?.Cancel();
        previewCts?.Dispose();
        previewCts = new CancellationTokenSource();
        var ct = previewCts.Token;

        try
        {
            if (string.Equals(ext, "PDF", StringComparison.OrdinalIgnoreCase))
            {
                IsPreviewLoading = true;
                PreviewImage = null;
                HasPreviewImage = false;

                var result = await PdfPageRenderer.RenderPageAsync(path, currentPreviewPageIndex, dpi: 110, ct);

                if (ct.IsCancellationRequested)
                    return;

                totalPreviewPages = result.PageCount;
                IsPdfMultiPage = totalPreviewPages > 1;
                PreviewPageIndicator = totalPreviewPages > 0 ? $"{currentPreviewPageIndex + 1} / {totalPreviewPages}" : "1 / 1";
                PreviewImage = result.Image;
                HasPreviewImage = PreviewImage != null;
                IsPreviewLoading = false;
            }
            else if (ext is "PNG" or "JPG" or "JPEG" or "BMP")
            {
                IsPreviewLoading = true;
                var bmp = await Task.Run(() =>
                {
                    try
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.UriSource = new Uri(path);
                        img.EndInit();
                        img.Freeze();
                        return img;
                    }
                    catch
                    {
                        return null;
                    }
                }, ct);

                if (ct.IsCancellationRequested)
                    return;

                totalPreviewPages = 1;
                IsPdfMultiPage = false;
                PreviewPageIndicator = "1 / 1";
                PreviewImage = bmp;
                HasPreviewImage = bmp != null;
                IsPreviewLoading = false;
            }
            else
            {
                IsPreviewLoading = false;
                totalPreviewPages = 0;
                IsPdfMultiPage = false;
                PreviewPageIndicator = "";
                PreviewImage = null;
                HasPreviewImage = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Operación cancelada por nueva selección de plano
        }
        catch
        {
            PreviewImage = null;
            HasPreviewImage = false;
            IsPdfMultiPage = false;
            PreviewPageIndicator = "";
            IsPreviewLoading = false;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsPreviewLoading = false;
                RaisePagingCommands();
            }
        }
    }

    private void PreviousPreviewPage()
    {
        var row = getSelectedRow();
        if (currentPreviewPageIndex > 0 && row?.RutaArchivo != null)
        {
            currentPreviewPageIndex--;
            var path = row.RutaArchivo;
            var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            LoadCurrentPagePreview(path, ext);
        }
    }

    private void NextPreviewPage()
    {
        var row = getSelectedRow();
        if (currentPreviewPageIndex < totalPreviewPages - 1 && row?.RutaArchivo != null)
        {
            currentPreviewPageIndex++;
            var path = row.RutaArchivo;
            var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            LoadCurrentPagePreview(path, ext);
        }
    }

    private void RaisePagingCommands()
    {
        PreviousPreviewPageCommand.RaiseCanExecuteChanged();
        NextPreviewPageCommand.RaiseCanExecuteChanged();
    }
}
