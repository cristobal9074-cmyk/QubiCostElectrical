namespace QubiCostElectrical;

public static class ItemizadoImportSummary
{
    public static (int Imported, int Rejected, int Warnings) CountPreviewRows(IEnumerable<ItemizadoImportPreviewRow> rows)
    {
        int rejected = 0;
        int warnings = 0;
        int imported = 0;

        foreach (var row in rows)
        {
            if (row.EsTitulo)
                continue;

            if (row.IsRejected)
            {
                rejected++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.RejectionReason))
                warnings++;

            imported++;
        }

        return (imported, rejected, warnings);
    }

    public static string FormatStatus(string action, int imported, int rejected, int warnings)
        => $"{action} completada: {imported:N0} filas importadas, {rejected:N0} rechazadas, {warnings:N0} advertencias";
}
