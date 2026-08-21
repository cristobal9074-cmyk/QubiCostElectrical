using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class ItemizadoImportPreviewPolicy
{
    public const int PastePreviewRowThreshold = 50;

    public static bool ShouldShowPastePreview(
        PegarItemizadoResult result,
        bool hasValidationIssues = false,
        bool hasDuplicateWbs = false)
        => result.Rows.Count > PastePreviewRowThreshold
           || !string.IsNullOrWhiteSpace(result.Warning)
           || hasValidationIssues
           || hasDuplicateWbs;

    public static bool ShouldShowImportPreview(
        ItemizadoImportPlan plan,
        bool hasValidationIssues = false,
        bool hasDuplicateWbs = false)
        => plan.Rows.Count > PastePreviewRowThreshold
           || hasValidationIssues
           || hasDuplicateWbs
           || (plan.RequiresReplaceConfirmation && plan.Rows.Count > 5);
}
