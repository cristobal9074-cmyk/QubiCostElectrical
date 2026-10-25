using System.Globalization;
using System.Text;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

internal static class CubicacionPasteHelpers
{
    public const int FixedPasteColumnCount = 4;

    public static int ResolveAnchoredPasteColumn(DataGridColumn? column)
    {
        if (column is DisplayBindingTextColumn sectorCol
            && sectorCol.SectorSlot >= 0
            && sectorCol.SectorSlot < CubicacionViewModel.MaximumSectorCount)
            return FixedPasteColumnCount + sectorCol.SectorSlot;

        return ResolveAnchoredPasteColumn(ExtractColumnHeaderText(column));
    }

    public static int ResolveAnchoredPasteColumn(string header)
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

    public static bool IsFixedPasteColumn(DataGridColumn? column)
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
}
