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
    public const int FixedPasteColumnCount = CubicacionPasteHelpers.FixedPasteColumnCount;

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

    internal static int ResolveAnchoredPasteColumn(DataGridColumn? column)
        => CubicacionPasteHelpers.ResolveAnchoredPasteColumn(column);

    internal static int ResolveAnchoredPasteColumn(string header)
        => CubicacionPasteHelpers.ResolveAnchoredPasteColumn(header);

    internal static bool IsFixedPasteColumn(DataGridColumn? column)
        => CubicacionPasteHelpers.IsFixedPasteColumn(column);

    public void ApplyClipboardRows(IEnumerable<CubicacionClipboardRow> rows)
        => clipboardCoordinator.ApplyClipboardRows(rows);
}
