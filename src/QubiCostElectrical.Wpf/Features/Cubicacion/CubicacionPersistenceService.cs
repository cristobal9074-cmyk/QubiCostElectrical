using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Guardado, marcado sucio/limpio, validación de filas y aplicación de conjuntos eléctricos —
/// extraído de CubicacionViewModel.Persistence.cs (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md). Comportamiento preservado tal cual.
/// </summary>
internal sealed class CubicacionPersistenceService
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly WpfAppHost host;
    private readonly IShellNavigationHost shell;
    private readonly CubicacionPresenter cubicacionPresenter;
    private readonly ConjuntoElectricoService conjuntoService;

    public CubicacionPersistenceService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        WpfAppHost host,
        IShellNavigationHost shell,
        CubicacionPresenter cubicacionPresenter,
        ConjuntoElectricoService conjuntoService)
    {
        this.parent = parent;
        this.session = session;
        this.host = host;
        this.shell = shell;
        this.cubicacionPresenter = cubicacionPresenter;
        this.conjuntoService = conjuntoService;
    }

    public async Task SaveAsync()
    {
        if (!host.ProjectWorkspace.HasOpenProject) return;
        if (session.IsReadOnly)
        {
            WpfDialogService.Warning("Guardar", "El proyecto está en solo lectura. Abra una copia escribible para guardar cambios.");
            return;
        }
        parent.SaveStatusText = "Guardando…";
        try
        {
            await host.ProjectWorkspace.SaveAsync();
            MarkClean();
            shell.RefreshChrome();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cubicacion", "Falló el guardado desde Cubicación", ex);
            parent.SaveStatusText = "No se pudo guardar. Reintenta y, si persiste, revisa el log para identificar el problema.";
            WpfDialogService.Error("Guardar", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public void MarkDirty()
    {
        if (session.IsReadOnly)
            return;
        parent.HasPendingChanges = true;
        parent.SaveStatusText = "Cambios sin guardar";
        parent.NotifyCubicacionSummaryChanged();
        using (parent.SuppressSessionReload())
            session.MarkDirty("cubicacion");
    }

    public void MarkClean()
    {
        parent.HasPendingChanges = false;
        parent.SaveStatusText = $"Guardado a las {AppClock.Now:HH:mm}";
        parent.NotifyCubicacionSummaryChanged();
    }

    public void UpdatePartidaCount(PartidaListItemVm? partida)
    {
        if (partida == null || !ReferenceEquals(partida, parent.SelectedPartida)) return;
        // Rows ya está filtrado a la partida activa, evita re-escanear toda la sesión.
        partida.RecursoCount = parent.Rows.Count(r => r.HasContent);
    }

    public void ValidateRows()
    {
        var materialesPorCodigo = GetValidationMaterialesIndex();
        foreach (var row in parent.Rows)
            ApplyValidation(row, materialesPorCodigo);
    }

    // Revalida SOLO la fila tocada. Cada edición de celda (una tecla, un sector, un pegado de
    // una celda) llamaba a ValidateRows() completo — reconstruir el índice de materiales y
    // revalidar TODAS las filas de la partida en cada tecleo era la causa principal de la
    // lentitud reportada. La validación de una fila es independiente de las demás (no hay
    // reglas cruzadas entre filas), así que esto es seguro y no cambia ningún resultado.
    public void ValidateRow(CubicacionRowVm row)
        => ApplyValidation(row, GetValidationMaterialesIndex());

    public IReadOnlyDictionary<string, MaterialRow> GetValidationMaterialesIndex()
        => parent.ValidationMaterialesIndex ??= cubicacionPresenter.BuildMaterialesIndex();

    private void ApplyValidation(CubicacionRowVm row, IReadOnlyDictionary<string, MaterialRow> materialesPorCodigo)
    {
        var validation = cubicacionPresenter.ValidateRow(row.Source, materialesPorCodigo);
        row.ApplyCellValidation(validation.CodigoError, null, validation.CantidadError, null, null);
        row.HasError = validation.HasError;
        row.ErrorMessage = validation.ErrorMessage;
        row.PrecioVencido = validation.PrecioVencido;
    }

    public void OnRowEdited(CubicacionRowVm row)
    {
        if (session.IsReadOnly)
            return;
        parent.EnsureRowInSession(row);
        parent.TryAutoResolveFromPriceBank(row);
        if (!parent.HasPendingUndoForEdit)
        {
            host.UndoService.PushUndoPoint(session, $"Editar {row.Codigo}");
            parent.HasPendingUndoForEdit = true;
        }
        row.RefreshDisplayProperties();
        cubicacionPresenter.AfterRowEdited(row.Source);
        if (ReferenceEquals(parent.SelectedRow, row))
            parent.RightPanel.LoadRow(row);
        parent.ScheduleRefreshTotals();
        ValidateRow(row);
        UpdatePartidaCount(parent.SelectedPartida);
        parent.NotifyCubicacionSummaryChanged();
        MarkDirty();
        parent.NotifyIsEmptyChanged();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    public void AplicarConjunto()
    {
        var selectedPartida = parent.SelectedPartida;
        if (session.IsReadOnly)
        {
            WpfDialogService.Info("Solo lectura", "El proyecto está en solo lectura; no se pueden cargar plantillas.");
            return;
        }

        if (selectedPartida == null || !selectedPartida.EsPartida)
        {
            WpfDialogService.Info(
                "Seleccione una partida",
                "Elija una partida del listado izquierdo (no un título de sección) para cargar una plantilla de materiales.");
            return;
        }

        if (selectedPartida.ExcluidoCubicacion)
        {
            WpfDialogService.Info(
                "Partida excluida",
                "Esta partida está marcada como excluida de cubicación.");
            return;
        }

        var conjuntos = (session.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>())
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Nombre))
            .OrderBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (conjuntos.Count == 0)
        {
            WpfDialogService.Info(
                "Sin plantillas",
                "No hay plantillas de materiales en este proyecto.\n\n" +
                "Cree una en la pestaña Plantillas (＋ Nueva Plantilla), agregue materiales y vuelva a Cubicación.");
            return;
        }

        var dialog = new ConjuntoPickerDialog(conjuntos);
        if (Application.Current?.MainWindow is { IsLoaded: true } main)
            dialog.Owner = main;

        if (dialog.ShowDialog() != true || dialog.SelectedConjunto == null)
            return;

        var partida = session.Itemizado.FirstOrDefault(i =>
            string.Equals(i.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
        if (partida == null)
        {
            WpfDialogService.Info("Partida no encontrada", "No se encontró la partida seleccionada en el itemizado.");
            return;
        }

        if ((dialog.SelectedConjunto.Recursos?.Count ?? 0) == 0)
        {
            WpfDialogService.Info(
                "Plantilla vacía",
                $"La plantilla '{dialog.SelectedConjunto.Nombre}' no tiene materiales.\n\n" +
                "Ábrala en Plantillas y agregue materiales antes de cargarla.");
            return;
        }

        host.UndoService.PushUndoPoint(session, "Aplicar plantilla de materiales");

        if (dialog.Reemplazar)
            cubicacionPresenter.DeleteRows(
                session.Cubicacion
                    .Where(c => string.Equals(c.ItemId, selectedPartida.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Id)
                    .ToList());

        bool hasZonas = (session.ZonasProyecto?.Count(z => z.Activa) ?? 0) > 0;
        ConjuntoExpansionResult result;
        if (hasZonas)
            result = conjuntoService.ExpandToCubicacionPerZona(dialog.SelectedConjunto, partida, session.Materiales);
        else
            result = conjuntoService.ExpandToCubicacion(dialog.SelectedConjunto, partida, session.Materiales);

        foreach (var row in result.Rows)
            session.Cubicacion.Add(row);

        using (parent.SuppressSessionReload())
            session.MarkDirty("cubicacion");
        parent.LoadCubicacionRows();

        if (result.Rows.Count == 0)
        {
            var extra = result.Warnings.Count > 0
                ? "\n\n" + string.Join("\n", result.Warnings)
                : "";
            WpfDialogService.Info(
                "Nada que cargar",
                $"No se generaron líneas de cubicación desde '{dialog.SelectedConjunto.Nombre}'.{extra}");
            return;
        }

        if (result.Warnings.Count > 0)
            WpfDialogService.Info(
                "Plantilla aplicada con advertencias",
                $"{result.Rows.Count} material(es) cargados.\n\n" + string.Join("\n", result.Warnings));
    }

    public void SyncGridRowSelection(IEnumerable<CubicacionRowVm> selected)
    {
        var set = selected.ToHashSet();
        foreach (var row in parent.Rows)
            row.IsSelected = set.Contains(row);

        if (set.Count == 0 && parent.SelectedRow != null)
            parent.SelectedRow.IsSelected = true;

        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
