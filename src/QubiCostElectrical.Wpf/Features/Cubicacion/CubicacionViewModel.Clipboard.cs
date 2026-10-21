using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionClipboardCoordinator (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que CubicacionView.xaml.cs ya conoce, sin cambiar ninguna firma.
    private string? quickAddCodigoDraft;

    /// <summary>Columnas fijas del pegado: Código, Descripción, UD, Cantidad. Desde aquí empiezan los sectores.</summary>
    public const int FixedPasteColumnCount = 4;

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

    /// <summary>
    /// Índice de pegado: 0 Código, 1 Descripción, 2 UD, 3 Cantidad, 4+ sector (slot = index - 4).
    /// -1 = columna no pegable (índice, solo lectura, etc.).
    /// </summary>
    internal static int ResolveAnchoredPasteColumn(DataGridColumn? column)
    {
        if (column is DisplayBindingTextColumn sectorCol
            && sectorCol.SectorSlot >= 0
            && sectorCol.SectorSlot < MaximumSectorCount)
            return FixedPasteColumnCount + sectorCol.SectorSlot;

        return ResolveAnchoredPasteColumn(ExtractColumnHeaderText(column));
    }

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

    /// <summary>Código/Descripción/UD/Cantidad: el pegado genérico de casillas no las toca.</summary>
    internal static bool IsFixedPasteColumn(DataGridColumn? column)
    {
        int index = ResolveAnchoredPasteColumn(column);
        return index is >= 0 and < FixedPasteColumnCount;
    }

    private static string ExtractColumnHeaderText(DataGridColumn? column)
    {
        if (column?.Header is string text)
            return text;
        if (column?.Header is TextBlock direct)
            return direct.Text ?? "";
        if (column?.Header is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is TextBlock tb)
                    return tb.Text ?? "";
            }
        }

        return "";
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

    public void ApplyClipboardRows(IEnumerable<CubicacionClipboardRow> rows)
        => clipboardCoordinator.ApplyClipboardRows(rows);
}
