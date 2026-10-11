using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public sealed class SelectableFileItem : INotifyPropertyChanged
{
    private bool isSelected;

    public SelectableFileItem(string fullPath, string displayName)
    {
        FullPath = fullPath;
        Name = displayName;
        Extension = Path.GetExtension(fullPath);
        ExtensionBadge = string.IsNullOrWhiteSpace(Extension) ? "FILE" : Extension.TrimStart('.').ToUpperInvariant();

        try
        {
            var fi = new FileInfo(fullPath);
            SizeText = fi.Length >= 1024 * 1024
                ? $"{fi.Length / (1024.0 * 1024.0):N1} MB"
                : $"{fi.Length / 1024.0:N0} KB";
            LastModifiedText = fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm");
        }
        catch
        {
            SizeText = "—";
            LastModifiedText = "";
        }
    }

    public string FullPath { get; }
    public string Name { get; }
    public string Extension { get; }
    public string ExtensionBadge { get; }
    public string SizeText { get; }
    public string LastModifiedText { get; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class SelectFilesFromFolderDialog : Window
{
    private readonly string folderPath;
    private SelectableFileItem[] allItems = Array.Empty<SelectableFileItem>();
    private string? currentPreviewPath;
    private int currentPreviewPage;
    private int totalPreviewPages;

    public string[] SelectedFiles { get; private set; } = Array.Empty<string>();

    public SelectFilesFromFolderDialog(string folderPath, string title)
    {
        InitializeComponent();
        TitleBlock.Text = title;
        FolderPathBlock.Text = $"Carpeta: {folderPath}";
        this.folderPath = folderPath;
        RescanFiles();
    }

    private void RescanFiles()
    {
        bool includeSubfolders = IncludeSubfoldersBox.IsChecked == true;
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var files = Directory.Exists(folderPath)
            ? Directory.GetFiles(folderPath, "*", searchOption)
                .OrderBy(f => Path.GetRelativePath(folderPath, f), StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

        allItems = files
            .Select(f => new SelectableFileItem(f, Path.GetRelativePath(folderPath, f)))
            .ToArray();

        var extensions = allItems
            .Select(x => x.Extension)
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ExtensionFilterBox.ItemsSource = new[] { "Todas las extensiones" }.Concat(extensions).ToArray();
        ExtensionFilterBox.SelectedIndex = 0;

        ApplyFilter(null);
        UpdateSelectionCount();

        if (allItems.Length > 0)
        {
            FilesList.SelectedIndex = 0;
        }
        else
        {
            ClearPreview();
        }
    }

    private void OnIncludeSubfoldersChanged(object sender, RoutedEventArgs e) => RescanFiles();

    private void OnExtensionFilterChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyFilter(ExtensionFilterBox.SelectedItem as string);

    private void ApplyFilter(string? extension)
    {
        var visible = string.IsNullOrEmpty(extension) || extension == "Todas las extensiones"
            ? allItems
            : allItems.Where(x => string.Equals(x.Extension, extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        FilesList.ItemsSource = new ObservableCollection<SelectableFileItem>(visible);
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        int selected = allItems.Count(x => x.IsSelected);
        int total = allItems.Length;
        SelectionCountBlock.Text = $"{selected} de {total} archivos seleccionados";
    }

    public void OnItemCheckboxClicked(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCount();
    }

    private void OnSelectedFileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilesList.SelectedItem is SelectableFileItem item)
        {
            LoadFilePreview(item.FullPath, item.Name, item.SizeText, item.ExtensionBadge);
        }
        else
        {
            ClearPreview();
        }
    }

    private System.Threading.CancellationTokenSource? previewCts;

    private async void LoadFilePreview(string path, string displayName, string sizeText, string badge)
    {
        previewCts?.Cancel();
        previewCts?.Dispose();
        previewCts = new System.Threading.CancellationTokenSource();
        var ct = previewCts.Token;

        currentPreviewPath = path;
        currentPreviewPage = 0;
        totalPreviewPages = 0;

        PreviewNameBlock.Text = displayName;
        PreviewMetaBlock.Text = $"{badge} · {sizeText}";

        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();

        // Mostrar estado de carga intermedio
        PreviewImageCtrl.Source = null;
        PreviewImageCtrl.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PageControlsPanel.Visibility = Visibility.Collapsed;
        PlaceholderTitle.Text = "Cargando vista previa...";
        PlaceholderSubtitle.Text = "Procesando plano técnico";

        try
        {
            if (string.Equals(ext, "PDF", StringComparison.OrdinalIgnoreCase))
            {
                var result = await PdfPageRenderer.RenderPageAsync(path, 0, dpi: 110, ct);
                if (ct.IsCancellationRequested)
                    return;

                totalPreviewPages = result.PageCount;
                if (result.Image != null)
                {
                    PreviewImageCtrl.Source = result.Image;
                    PreviewImageCtrl.Visibility = Visibility.Visible;
                    PreviewPlaceholder.Visibility = Visibility.Collapsed;

                    if (totalPreviewPages > 1)
                    {
                        PageControlsPanel.Visibility = Visibility.Visible;
                        UpdatePageIndicator();
                    }
                    return;
                }
            }
            else if (ext is "PNG" or "JPG" or "JPEG" or "BMP")
            {
                var bmp = await System.Threading.Tasks.Task.Run(() =>
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

                if (bmp != null)
                {
                    PreviewImageCtrl.Source = bmp;
                    PreviewImageCtrl.Visibility = Visibility.Visible;
                    PreviewPlaceholder.Visibility = Visibility.Collapsed;
                    return;
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
        catch
        {
            // Error al renderizar, se muestra placeholder abajo
        }

        if (ct.IsCancellationRequested)
            return;

        // Formato no visualizable directamente (DWG, DXF, XLSX, etc.) o error
        PreviewImageCtrl.Source = null;
        PreviewImageCtrl.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PageControlsPanel.Visibility = Visibility.Collapsed;

        if (ext is "DWG" or "DXF")
        {
            PlaceholderTitle.Text = "Plano CAD (AutoCAD)";
            PlaceholderSubtitle.Text = "Se vinculará y abrirá en su visor CAD";
        }
        else
        {
            PlaceholderTitle.Text = "Documento técnico";
            PlaceholderSubtitle.Text = "Formato de antecedente";
        }
    }

    private void ClearPreview()
    {
        previewCts?.Cancel();
        currentPreviewPath = null;
        PreviewNameBlock.Text = "Seleccione un archivo";
        PreviewMetaBlock.Text = "Para ver su contenido";
        PreviewImageCtrl.Source = null;
        PreviewImageCtrl.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PlaceholderTitle.Text = "Sin selección";
        PlaceholderSubtitle.Text = "Elija un plano de la lista";
        PageControlsPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdatePageIndicator()
    {
        PageIndicatorBlock.Text = $"{currentPreviewPage + 1} / {totalPreviewPages}";
        PrevPageBtn.IsEnabled = currentPreviewPage > 0;
        NextPageBtn.IsEnabled = currentPreviewPage < totalPreviewPages - 1;
    }

    private async void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (currentPreviewPage > 0 && currentPreviewPath != null)
        {
            currentPreviewPage--;
            UpdatePageIndicator();
            var result = await PdfPageRenderer.RenderPageAsync(currentPreviewPath, currentPreviewPage, dpi: 110);
            if (result.Image != null)
                PreviewImageCtrl.Source = result.Image;
        }
    }

    private async void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (currentPreviewPage < totalPreviewPages - 1 && currentPreviewPath != null)
        {
            currentPreviewPage++;
            UpdatePageIndicator();
            var result = await PdfPageRenderer.RenderPageAsync(currentPreviewPath, currentPreviewPage, dpi: 110);
            if (result.Image != null)
                PreviewImageCtrl.Source = result.Image;
        }
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        if (FilesList.ItemsSource is ObservableCollection<SelectableFileItem> items)
        {
            foreach (var item in items)
                item.IsSelected = true;
        }
        UpdateSelectionCount();
    }

    private void OnDeselectAll(object sender, RoutedEventArgs e)
    {
        if (FilesList.ItemsSource is ObservableCollection<SelectableFileItem> items)
        {
            foreach (var item in items)
                item.IsSelected = false;
        }
        UpdateSelectionCount();
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        SelectedFiles = allItems.Where(x => x.IsSelected).Select(x => x.FullPath).ToArray();
        if (SelectedFiles.Length == 0)
        {
            WpfDialogService.InfoModal("Agregar antecedente", "Seleccione al menos un archivo con la casilla de verificación, o pulse Cancelar.");
            return;
        }
        DialogResult = true;
    }
}
