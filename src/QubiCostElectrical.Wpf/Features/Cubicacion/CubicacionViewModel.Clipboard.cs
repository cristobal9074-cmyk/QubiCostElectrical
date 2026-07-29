using System.Globalization;
using System.Text;
using System.Windows.Controls;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionClipboardCoordinator (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que CubicacionView.xaml.cs ya conoce, sin cambiar ninguna firma.
    private string? quickAddCodigoDraft;

    public string? QuickAddCodigoDraft
    {
        get => quickAddCodigoDraft;
        internal set => SetProperty(ref quickAddCodigoDraft, value);
    }

    public bool CanCreateMaterial =>
        CanEdit && SelectedPartida?.EsPartida == true && !session.IsReadOnly;

    public void BeginQuickAddMaterial(string? codigo = null, string? descripcion = null, string? unidad = null)
        => clipboardCoordinator.BeginQuickAddMaterial(codigo, descripcion, unidad);

    public bool AddMaterialToCatalog(MaterialRow material, bool applyToSelectedRow)
        => clipboardCoordinator.AddMaterialToCatalog(material, applyToSelectedRow);

    public void RunBatchedEdit(string undoLabel, Action action)
        => clipboardCoordinator.RunBatchedEdit(undoLabel, action);

    public bool TryPasteAnchored(DataGrid grid, string text)
        => clipboardCoordinator.TryPasteAnchored(grid, text);

    public void PasteClipboardDirect(string text, bool showPreviewDialog = false)
        => clipboardCoordinator.PasteClipboardDirect(text, showPreviewDialog);

    internal static int ResolveAnchoredPasteColumn(string header)
    {
        var key = NormalizePasteHeader(header);
        return key switch
        {
            "CODIGO" => 0,
            "DESCRIPCION" => 1,
            "UD" => 2,
            "CANT" or "CANTIDAD" => 3,
            _ => -1
        };
    }

    private static string NormalizePasteHeader(string header)
    {
        var normalized = header.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public CubicacionClipboardParseResult ParseClipboardText(string text)
        => CubicacionClipboardService.ParseExcelText(text);

    public void ApplyClipboardRows(IEnumerable<CubicacionClipboardRow> rows)
        => clipboardCoordinator.ApplyClipboardRows(rows);
}
