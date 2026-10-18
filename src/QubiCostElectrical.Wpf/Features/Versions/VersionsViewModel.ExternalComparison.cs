using System;
using System.IO;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed partial class VersionsViewModel
{
    public void CompareWithProjectFile()
    {
        if (!HasProject)
            return;

        string? filePath = fileDialog?.PromptOpenFile(
            "Proyectos QubiCost (*.qce)|*.qce|Todos los archivos (*.*)|*.*");

        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (string.Equals(filePath, session.CurrentProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            WpfDialogService.InfoModal(
                "Mismo archivo",
                "El archivo seleccionado es el mismo que está actualmente abierto en la aplicación. Seleccione una versión anterior u otro proyecto para comparar.");
            return;
        }

        try
        {
            var repo = projectRepository ?? new ProjectRepository();
            var externalData = repo.LoadSQLiteSnapshot(filePath);
            if (externalData == null || (externalData.Itemizado.Count == 0 && externalData.Cubicacion.Count == 0))
            {
                WpfDialogService.InfoModal("Sin datos", "El archivo de proyecto seleccionado no contiene partidas legibles.");
                return;
            }

            string baselineLabel = Path.GetFileNameWithoutExtension(filePath);
            string currentLabel = !string.IsNullOrWhiteSpace(session.CurrentProjectPath)
                ? Path.GetFileNameWithoutExtension(session.CurrentProjectPath)
                : "Presupuesto Actual";

            var baselineSnapshot = BudgetVersionData.FromProjectFileData(externalData);
            var currentSnapshot = session.CreateSnapshot();

            var summary = versionComparison.Compare(
                baselineSnapshot,
                currentSnapshot,
                baselineLabel: baselineLabel,
                currentLabel: currentLabel);

            currentSummary = summary;
            comparisonRows.Clear();
            foreach (var row in summary.Rows)
                comparisonRows.Add(new VersionComparisonRowVm(row, CurrencyFormat));

            SelectedComparisonRow = comparisonRows.FirstOrDefault();

            ComparisonSummaryText = $"{summary.Rows.Count:N0} partidas comparadas";
            ComparisonStatusText = $"Comparando archivo «{baselineLabel}» contra «{currentLabel}».";
            ComparisonTotalsText = BuildTotalsText(summary, "Actual");
            ComparisonAddedText = $"{summary.AddedCount:N0} nuevas";
            ComparisonChangedText = $"{summary.ChangedCount:N0} modificadas";
            ComparisonRemovedText = $"{summary.RemovedCount:N0} eliminadas";
            ComparisonUnchangedText = $"{summary.UnchangedCount:N0} sin cambio";
            (ComparisonAdicionalesText, ComparisonDeductivosText, ComparisonNetVariationText) = BuildVariationTexts(summary);
            ComparisonDeltaUf = summary.DeltaUf;
            ComparisonDeltaPct = summary.DeltaPct;
            ComparisonModeHint = $"Archivo externo A = «{baselineLabel}» · Archivo actual B = «{currentLabel}»";

            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            NotifyComparisonSummaryChanged();
            exportPdfCommand.RaiseCanExecuteChanged();
            exportExcelCommand.RaiseCanExecuteChanged();
            createChangeOrderFromDiffCommand.RaiseCanExecuteChanged();
            comparisonRowsView.Refresh();

            int mayoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad > 0);
            int menoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad < 0);
            int cambioPrecio = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaPuUf != 0);

            bool incorporar = WpfDialogService.Confirm(
                "Comparación de Versiones Exitosa",
                $"Se ha comparado «{baselineLabel}» contra el proyecto actual:\n\n" +
                $"• 🟢 Partidas Nuevas: {summary.AddedCount} partidas\n" +
                $"• 🟡 Partidas Modificadas: {summary.ChangedCount} partidas ({mayoresMetrajes} con aumento metraje, {menoresMetrajes} con reducción, {cambioPrecio} con cambio P.U.)\n" +
                $"• 🔴 Partidas Eliminadas: {summary.RemovedCount} partidas\n" +
                $"• ⚪ Partidas Sin Cambio: {summary.UnchangedCount} partidas\n" +
                $"• ⚡ Variación Neta: {CurrencyFormat.FromUfSigned(summary.NetVariationUf)} ({(summary.NetVariationClp >= 0 ? "+$ " : "$ ")}{summary.NetVariationClp:N0})\n\n" +
                $"¿Deseas agregar también «{baselineLabel}» a la lista de versiones históricas de este proyecto?");

            if (incorporar && !session.IsReadOnly)
            {
                string code = baselineLabel.Length > 12 ? baselineLabel[..12] : baselineLabel;
                var version = new BudgetVersionRow
                {
                    Codigo = code,
                    Nombre = $"Importada: {baselineLabel}",
                    Estado = "Congelada",
                    CreadoPor = externalData.Proyecto?.UsuarioNombre ?? "Archivo externo",
                    FechaCreacion = externalData.Proyecto?.FechaModificacion ?? AppClock.Now,
                    MontoVentaUf = summary.BaselineUf,
                    Snapshot = baselineSnapshot
                };
                session.Versiones.Add(version);
                session.MarkDirty(ProjectDirtyScope.Versiones);
                Refresh();
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al comparar archivo", $"No se pudo leer o comparar el proyecto seleccionado:\n{ex.Message}");
        }
    }

    public void SaveAsNewVersion()
    {
        if (!HasProject || session.IsReadOnly)
            return;

        if (string.IsNullOrWhiteSpace(session.CurrentProjectPath))
        {
            WpfDialogService.InfoModal("Guardar versión", "Guarde el proyecto antes de generar una nueva versión.");
            return;
        }

        var suggestion = RevisionNamingHelper.SuggestNextRevision(session.CurrentProjectPath);

        // 1. Congelar versión base previa si aún no existe en el historial
        bool hasBase = session.Versiones.Any(v => string.Equals(v.Codigo, suggestion.CurrentRevisionCode, StringComparison.OrdinalIgnoreCase));
        if (!hasBase)
        {
            var baseSnapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
            decimal baseVentaUf = dashboard.BuildPageState().Cost.TotalVentaUf;
            var baseVersion = new BudgetVersionRow
            {
                Codigo = suggestion.CurrentRevisionCode,
                Nombre = $"Línea Base {suggestion.CurrentRevisionCode}",
                Estado = "Aprobada",
                CreadoPor = session.ProjectInfo.UsuarioNombre,
                FechaCreacion = AppClock.Now,
                FechaModificacion = AppClock.Now,
                MontoVentaUf = baseVentaUf,
                Snapshot = baseSnapshot
            };
            session.Versiones.Add(baseVersion);
        }

        // 2. Pedir destino del nuevo archivo .qce
        string? targetPath = fileDialog?.PromptSaveFile(
            "Guardar como nueva versión",
            suggestion.NextSuggestedFileName,
            "Proyectos QubiCost (*.qce)|*.qce");

        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        try
        {
            // 3. Crear registro para la nueva versión
            var newSnapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
            decimal currentVentaUf = dashboard.BuildPageState().Cost.TotalVentaUf;
            var newVersion = new BudgetVersionRow
            {
                Codigo = suggestion.NextRevisionCode,
                Nombre = $"Versión {suggestion.NextRevisionCode}",
                Estado = "En elaboración",
                CreadoPor = session.ProjectInfo.UsuarioNombre,
                FechaCreacion = AppClock.Now,
                FechaModificacion = AppClock.Now,
                MontoVentaUf = currentVentaUf,
                Snapshot = newSnapshot
            };
            session.Versiones.Add(newVersion);
            session.ActiveVersionId = newVersion.Id;

            // Actualizar nombre de proyecto si contiene la versión previa
            if (!string.IsNullOrWhiteSpace(session.ProjectInfo.Nombre) &&
                session.ProjectInfo.Nombre.Contains(suggestion.CurrentRevisionCode, StringComparison.OrdinalIgnoreCase))
            {
                session.ProjectInfo.Nombre = session.ProjectInfo.Nombre.Replace(
                    suggestion.CurrentRevisionCode,
                    suggestion.NextRevisionCode,
                    StringComparison.OrdinalIgnoreCase);
            }

            session.MarkDirty(ProjectDirtyScope.Versiones);
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);

            // 4. Guardar como nuevo archivo
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(async () => await PerformSaveAsAsync(targetPath, suggestion));
            }
            else
            {
                _ = PerformSaveAsAsync(targetPath, suggestion);
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al guardar versión", $"Ocurrió un error al generar la nueva versión:\n{ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task PerformSaveAsAsync(string targetPath, RevisionSuggestion suggestion)
    {
        try
        {
            await workspace.SaveAsAsync(targetPath);
            Refresh();

            // Seleccionar versión previa para comparar de inmediato
            SelectedVersion = versions.FirstOrDefault(v => string.Equals(v.Codigo, suggestion.CurrentRevisionCode, StringComparison.OrdinalIgnoreCase))
                ?? versions.FirstOrDefault();

            WpfDialogService.Info(
                "Nueva Versión Creada",
                $"El proyecto ha sido guardado exitosamente como:\n«{Path.GetFileName(targetPath)}»\n\n" +
                $"• Línea base congelada: {suggestion.CurrentRevisionCode}\n" +
                $"• Versión activa en edición: {suggestion.NextRevisionCode}\n\n" +
                $"Ahora puedes realizar modificaciones en el itemizado, cubicación y precios. Los cambios respecto a {suggestion.CurrentRevisionCode} se verán reflejados en el Comparador.");
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al guardar archivo", $"No se pudo guardar la nueva versión:\n{ex.Message}");
        }
    }
}
