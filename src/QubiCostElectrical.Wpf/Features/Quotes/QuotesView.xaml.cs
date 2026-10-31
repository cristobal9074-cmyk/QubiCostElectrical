using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Quotes;

public partial class QuotesView : UserControl
{
    private QuotesViewModel? vm;

    public QuotesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachVm();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        vm = DataContext as QuotesViewModel;
        if (vm == null)
            return;

        vm.PropertyChanged += OnVmPropertyChanged;
        ApplyMonedaPolicy();
    }

    private void DetachVm()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= OnVmPropertyChanged;
        vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuotesViewModel.MuestraUf))
            ApplyMonedaPolicy();
    }

    /// <summary>
    /// Política de moneda del proyecto: en CLP se ocultan las columnas UF (PRECIO UF / MEJOR UF /
    /// DELTA UF) y queda solo la comparación en pesos.
    /// </summary>
    private void ApplyMonedaPolicy()
    {
        var ufVisibility = vm?.MuestraUf == true ? Visibility.Visible : Visibility.Collapsed;

        PrecioUfColumn.Visibility = ufVisibility;
        SelectedUfColumn.Visibility = ufVisibility;
        BestUfColumn.Visibility = ufVisibility;
        DeltaUfColumn.Visibility = ufVisibility;
    }

    private void OnUserControlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (vm?.HasActiveFilters == true)
            {
                vm.ClearFiltersCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }
    }

    private void OnDropZoneDragEnter(object sender, DragEventArgs e)
    {
        if (HasValidDroppedFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            SetDropZoneHighlight(true);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDropZoneDragOver(object sender, DragEventArgs e)
    {
        if (HasValidDroppedFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDropZoneDragLeave(object sender, DragEventArgs e)
    {
        SetDropZoneHighlight(false);
        e.Handled = true;
    }

    private void OnDropZoneDrop(object sender, DragEventArgs e)
    {
        SetDropZoneHighlight(false);

        if (!HasValidDroppedFiles(e))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var validFiles = files.Where(IsValidQuoteFile).ToArray();
            if (validFiles.Length > 0)
            {
                vm?.ImportQuoteFiles(validFiles);
            }
        }
        e.Handled = true;
    }

    private void OnDropZoneClick(object sender, MouseButtonEventArgs e)
    {
        vm?.ImportQuoteCommand?.Execute(null);
    }

    private static bool HasValidDroppedFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            return files.Any(IsValidQuoteFile);
        }

        return false;
    }

    private static bool IsValidQuoteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".xlsx" or ".xls" or ".csv" or ".pdf";
    }

    private void SetDropZoneHighlight(bool active)
    {
        var primaryBrush = TryFindResource("Brush.Blue") as Brush ?? Brushes.RoyalBlue;
        var lineBrush = TryFindResource("Brush.Line") as Brush ?? Brushes.LightGray;
        var canvasBrush = TryFindResource("Brush.Canvas") as Brush ?? Brushes.White;
        var blueSoftBrush = TryFindResource("Brush.BlueSoft") as Brush ?? Brushes.LightCyan;

        if (DropZoneBorder != null)
        {
            DropZoneBorder.Stroke = active ? primaryBrush : lineBrush;
            DropZoneBorder.Fill = active ? blueSoftBrush : canvasBrush;
        }

        if (EmptyDropZoneBorder != null)
        {
            EmptyDropZoneBorder.Stroke = active ? primaryBrush : lineBrush;
            EmptyDropZoneBorder.Fill = active ? blueSoftBrush : canvasBrush;
        }
    }
}
