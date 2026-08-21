using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    }

    public string FullPath { get; }
    public string Name { get; }
    public string Extension { get; }

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
    private SelectableFileItem[] allItems = System.Array.Empty<SelectableFileItem>();

    public string[] SelectedFiles { get; private set; } = System.Array.Empty<string>();

    public SelectFilesFromFolderDialog(string folderPath, string title)
    {
        InitializeComponent();
        TitleBlock.Text = title;
        this.folderPath = folderPath;
        RescanFiles();
    }

    private void RescanFiles()
    {
        bool includeSubfolders = IncludeSubfoldersBox.IsChecked == true;
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var files = Directory.Exists(folderPath)
            ? Directory.GetFiles(folderPath, "*", searchOption)
                .OrderBy(f => Path.GetRelativePath(folderPath, f), System.StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : System.Array.Empty<string>();

        allItems = files
            .Select(f => new SelectableFileItem(f, Path.GetRelativePath(folderPath, f)))
            .ToArray();

        var extensions = allItems
            .Select(x => x.Extension)
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        ExtensionFilterBox.ItemsSource = new[] { "Todas las extensiones" }.Concat(extensions).ToArray();
        ExtensionFilterBox.SelectedIndex = 0;

        ApplyFilter(null);
    }

    private void OnIncludeSubfoldersChanged(object sender, RoutedEventArgs e) => RescanFiles();

    private void OnExtensionFilterChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyFilter(ExtensionFilterBox.SelectedItem as string);

    private void ApplyFilter(string? extension)
    {
        var visible = string.IsNullOrEmpty(extension) || extension == "Todas las extensiones"
            ? allItems
            : allItems.Where(x => string.Equals(x.Extension, extension, System.StringComparison.OrdinalIgnoreCase)).ToArray();
        FilesList.ItemsSource = new ObservableCollection<SelectableFileItem>(visible);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var item in FilesList.ItemsSource.Cast<SelectableFileItem>())
            item.IsSelected = true;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        SelectedFiles = allItems.Where(x => x.IsSelected).Select(x => x.FullPath).ToArray();
        if (SelectedFiles.Length == 0)
        {
            WpfDialogService.InfoModal("Agregar antecedente", "Seleccione al menos un archivo, o cancele.");
            return;
        }
        DialogResult = true;
    }
}
