namespace QubiCostElectrical;

public enum ExportEligibilityStatus
{
    Allowed,
    Busy,
    NoProject,
    NoPermission,
    ReadOnly,
    HasBlockers,
    NoDocumentsSelected
}

public sealed record ExportEligibility(
    ExportEligibilityStatus Status,
    bool CanExportActions,
    bool CanDeliverPackage);

/// <summary>Gate puro de elegibilidad de exportación / entrega de paquete.</summary>
public static class ExportEligibilityEvaluator
{
    public static ExportEligibility Evaluate(
        bool hasOpenProject,
        bool canExportDocuments,
        bool isReadOnly,
        int blockingCount,
        bool isBusy,
        bool hasSelectedDocuments)
    {
        if (!hasOpenProject)
            return new(ExportEligibilityStatus.NoProject, false, false);
        if (!canExportDocuments)
            return new(ExportEligibilityStatus.NoPermission, false, false);
        if (isReadOnly)
            return new(ExportEligibilityStatus.ReadOnly, false, false);
        if (blockingCount > 0)
            return new(ExportEligibilityStatus.HasBlockers, false, false);

        var canActions = true;
        if (isBusy)
            return new(ExportEligibilityStatus.Busy, canActions, false);
        if (!hasSelectedDocuments)
            return new(ExportEligibilityStatus.NoDocumentsSelected, canActions, false);

        return new(ExportEligibilityStatus.Allowed, true, true);
    }
}
