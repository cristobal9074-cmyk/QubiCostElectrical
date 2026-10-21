using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PastePreviewRowVm
{
    public string StatusIcon { get; init; } = "";
    public Brush StatusColor { get; init; } = Brushes.Transparent;
    public string? Codigo { get; init; }
    public string? Descripcion { get; init; }
    public string? Unidad { get; init; }
    public string? CantidadRaw { get; init; }
    public string? RejectionReason { get; init; }
    public bool IsRejected { get; init; }
    public CubicacionClipboardRow Source { get; init; } = null!;
}

public partial class ExcelPastePreviewDialog : Window
{
    private readonly CubicacionClipboardParseResult parseResult;

    public bool HasWarnings => parseResult.Warnings.Count > 0;
    public bool HasRejected => parseResult.RejectedCount > 0;
    public IReadOnlyList<CubicacionClipboardRow> RowsToImport { get; private set; } = [];

    public ExcelPastePreviewDialog(CubicacionClipboardParseResult result)
    {
        parseResult = result;
        InitializeComponent();

        int total = result.Rows.Count;
        int rejected = result.RejectedCount;
        int valid = total - rejected;

        HeaderText.Text = $"{total} fila{(total != 1 ? "s" : "")} detectada{(total != 1 ? "s" : "")} · {valid} a importar" +
                          (rejected > 0 ? $" · {rejected} rechazada{(rejected != 1 ? "s" : "")}" : "");

        var vms = result.Rows.Select(r => new PastePreviewRowVm
        {
            StatusIcon = r.IsRejected ? "✗" : (string.IsNullOrWhiteSpace(r.Codigo) ? "⚠" : "✓"),
            StatusColor = r.IsRejected ? FindBrush("Brush.Red")
                : (string.IsNullOrWhiteSpace(r.Codigo) ? FindBrush("Brush.Amber")
                : FindBrush("Brush.Green")),
            Codigo = r.Codigo,
            Descripcion = r.Descripcion,
            Unidad = r.Unidad,
            CantidadRaw = r.CantidadRaw,
            RejectionReason = r.RejectionReason,
            IsRejected = r.IsRejected,
            Source = r
        }).ToList();

        PreviewGrid.ItemsSource = vms;
        WarningsList.ItemsSource = result.Warnings;

        if (rejected > 0)
        {
            string? reason = result.Rows.FirstOrDefault(r => r.IsRejected)?.RejectionReason;
            RejectedText.Text = string.IsNullOrWhiteSpace(reason)
                ? $"{rejected} fila{(rejected != 1 ? "s" : "")} rechazada{(rejected != 1 ? "s" : "")} — cantidad no válida."
                : $"{rejected} fila{(rejected != 1 ? "s" : "")} en rojo — {reason}";
        }

        ImportButton.Content = $"Importar {valid} fila{(valid != 1 ? "s" : "")}";
        ImportButton.IsEnabled = valid > 0;
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        RowsToImport = parseResult.Rows.Where(r => !r.IsRejected).ToList();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static Brush FindBrush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
}
