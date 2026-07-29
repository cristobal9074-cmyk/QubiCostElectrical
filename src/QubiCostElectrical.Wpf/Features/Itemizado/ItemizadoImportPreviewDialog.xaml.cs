using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public partial class ItemizadoImportPreviewDialog : Window
{
    private readonly List<ItemizadoImportRowVm> _rows;
    public int RejectedCount { get; private set; }
    public int WarningCount { get; private set; }

    public ItemizadoImportPreviewDialog(string title, string summary, string note, string? warning, List<ItemizadoImportRowVm> rows)
    {
        _rows = rows;
        InitializeComponent();
        Title = title;
        SummaryText.Text = summary;
        NoteText.Text = note;

        if (!string.IsNullOrWhiteSpace(warning))
        {
            WarningText.Text = warning;
            WarningText.Visibility = Visibility.Visible;
        }

        PreviewGrid.ItemsSource = _rows;
        PreviewGrid.CellEditEnding += OnCellEditEnding;
        UpdateState();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
                return;
            if (!AcceptButton.IsEnabled)
                return;
            if (Keyboard.FocusedElement is TextBox)
                return;

            e.Handled = true;
            Accept();
        };
    }

    public IReadOnlyList<ItemizadoRow> AcceptedRows { get; private set; } = [];

    private void OnAcceptClick(object sender, RoutedEventArgs e) => Accept();

    private void Accept()
    {
        if (!AcceptButton.IsEnabled)
            return;

        AcceptedRows = _rows.Where(r => !r.IsRejected).Select(r => r.ToRow()).ToList();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnFixClick(object sender, RoutedEventArgs e)
    {
        var pending = new List<(ItemizadoImportRowVm Row, string Trace)>();
        foreach (var row in _rows)
        {
            if (row.Cantidad < 0)
                pending.Add((row, $"{row.Item}: cantidad {row.Cantidad} -> 0"));
            else if (string.IsNullOrWhiteSpace(row.Item))
                pending.Add((row, "Sin código: se aplicará '(sin código)'"));
        }

        if (pending.Count == 0)
            return;

        var preview = string.Join(Environment.NewLine, pending.Select(p => p.Trace).Take(6));
        if (pending.Count > 6)
            preview += $"{Environment.NewLine}... y {pending.Count - 6} cambio(s) adicional(es).";

        bool confirmed = WpfDialogService.Confirm(
            "Aplicar corrección sugerida",
            $"Se aplicarán {pending.Count} corrección(es) trazables:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}¿Confirmas aplicar estos cambios?",
            isWarning: true);
        if (!confirmed)
            return;

        foreach (var (row, _) in pending)
        {
            if (row.Cantidad < 0)
                row.FixQuantity();
            else if (string.IsNullOrWhiteSpace(row.Item))
                row.FixEmptyCode();
        }

        UpdateState();
        PreviewGrid.Items.Refresh();
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Row.Item is ItemizadoImportRowVm row)
            row.Revalidate();
        RefreshDuplicateFlags();
        UpdateState();
    }

    private void RefreshDuplicateFlags()
    {
        var previews = _rows.Select(r => r.Preview).Where(p => p != null).Cast<ItemizadoImportPreviewRow>().ToList();
        if (previews.Count == 0)
            return;
        ItemizadoImportPreviewService.RefreshDuplicateFlags(previews);
        foreach (var vm in _rows)
            vm.NotifyValidationChanged();
    }

    private void UpdateState()
    {
        var previewRows = _rows
            .Select(r => r.Preview)
            .Where(p => p != null)
            .Cast<ItemizadoImportPreviewRow>()
            .ToList();
        var (imported, rejected, warnings) = ItemizadoImportSummary.CountPreviewRows(previewRows);
        int valid = imported - warnings;
        RejectedCount = rejected;
        WarningCount = warnings;

        AcceptButton.Content = rejected > 0 ? $"Aceptar ({valid:N0} filas)" : "Aceptar";
        AcceptButton.IsEnabled = valid > 0;

        if (rejected > 0)
        {
            RejectedBanner.Style = (Style)FindResource("Dialog.Banner.Error");
            RejectedText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Red");
            RejectedText.Text = $"✗ {rejected} fila(s) rechazada(s) — descripción requerida. Edítelas manualmente para continuar.";
            RejectedBanner.Visibility = Visibility.Visible;
        }
        else if (warnings > 0)
        {
            RejectedBanner.Style = (Style)FindResource("Dialog.Banner.Warning");
            RejectedText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Amber");
            RejectedText.Text = $"⚠ {warnings} fila(s) con advertencia(s) (importables). Revísalas antes de confirmar.";
            RejectedBanner.Visibility = Visibility.Visible;
        }
        else
        {
            RejectedBanner.Visibility = Visibility.Collapsed;
        }

        bool hasFixable = _rows.Any(r =>
            r.Cantidad < 0
            || (string.IsNullOrWhiteSpace(r.Item) && !r.EsTitulo));
        FixButton.Visibility = hasFixable ? Visibility.Visible : Visibility.Collapsed;
    }
}
