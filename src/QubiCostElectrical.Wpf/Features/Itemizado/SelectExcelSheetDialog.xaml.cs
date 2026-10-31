using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class SelectExcelSheetDialog : Window
{
    public sealed class SheetItemVm
    {
        public ExcelSheetSummary Summary { get; }
        public string Name => Summary.Name;
        public string RowCountDisplay => $"{Summary.RowCount:N0}";
        public string EstimatedDisplay => Summary.HasHeaderMatch ? $"{Summary.EstimatedItemRows:N0}" : "—";
        public string BadgeText => Summary.HasHeaderMatch ? "⭐ Itemizado" : "Hoja de datos";

        public Brush BadgeBackground => Summary.HasHeaderMatch
            ? new SolidColorBrush(Color.FromArgb(40, 37, 99, 235))
            : new SolidColorBrush(Color.FromArgb(20, 150, 150, 150));

        public Brush BadgeForeground => Summary.HasHeaderMatch
            ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
            : new SolidColorBrush(Color.FromRgb(156, 163, 175));

        public SheetItemVm(ExcelSheetSummary summary)
        {
            Summary = summary;
        }
    }

    public ExcelSheetSummary? SelectedSheet { get; private set; }

    public SelectExcelSheetDialog(IReadOnlyList<ExcelSheetSummary> sheets, string filePath)
    {
        InitializeComponent();

        string fileName = Path.GetFileName(filePath);
        FileSubtitleText.Text = $"Archivo: {fileName} · Seleccione la pestaña que desea cargar:";

        var items = sheets.Select(s => new SheetItemVm(s)).ToList();
        SheetsGrid.ItemsSource = items;

        // Selección por defecto: la mejor hoja recomendada o la primera
        var best = items.OrderByDescending(i => i.Summary.HasHeaderMatch)
                        .ThenByDescending(i => i.Summary.EstimatedItemRows)
                        .FirstOrDefault();

        if (best != null)
        {
            SheetsGrid.SelectedItem = best;
            UpdatePreview(best.Summary);
        }
    }

    public static ExcelSheetSummary? Prompt(IReadOnlyList<ExcelSheetSummary> sheets, string filePath)
    {
        var dialog = new SelectExcelSheetDialog(sheets, filePath);
        if (Application.Current?.MainWindow != null)
            dialog.Owner = Application.Current.MainWindow;

        bool accepted = dialog.ShowDialog() == true;
        return accepted ? dialog.SelectedSheet : null;
    }

    private void SheetsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SheetsGrid.SelectedItem is SheetItemVm item)
        {
            SelectedSheet = item.Summary;
            UpdatePreview(item.Summary);
        }
    }

    private void SheetsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SheetsGrid.SelectedItem is SheetItemVm item)
        {
            SelectedSheet = item.Summary;
            DialogResult = true;
            Close();
        }
    }

    private void UpdatePreview(ExcelSheetSummary sheet)
    {
        var dt = new DataTable();
        int maxCols = 0;
        foreach (var row in sheet.SampleRows)
        {
            if (row.Count > maxCols)
                maxCols = row.Count;
        }

        if (maxCols == 0)
        {
            PreviewSampleGrid.ItemsSource = null;
            return;
        }

        for (int i = 0; i < maxCols; i++)
        {
            dt.Columns.Add($"Col{i + 1}");
        }

        foreach (var row in sheet.SampleRows)
        {
            var dataRow = dt.NewRow();
            for (int i = 0; i < row.Count; i++)
            {
                dataRow[i] = row[i];
            }
            dt.Rows.Add(dataRow);
        }

        PreviewSampleGrid.ItemsSource = dt.DefaultView;
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (SheetsGrid.SelectedItem is SheetItemVm item)
        {
            SelectedSheet = item.Summary;
            DialogResult = true;
            Close();
        }
        else
        {
            DialogResult = false;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
