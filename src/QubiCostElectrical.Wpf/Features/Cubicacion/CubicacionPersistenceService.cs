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
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly WpfUndoService undo;
    private readonly IShellNavigationHost shell;
    private readonly CubicacionPresenter cubicacionPresenter;
    private readonly ConjuntoElectricoService conjuntoService;
    private readonly IPriceBankStore priceBank;

    public CubicacionPersistenceService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        IProjectWorkspaceService projectWorkspace,
        WpfUndoService undo,
        IShellNavigationHost shell,
        CubicacionPresenter cubicacionPresenter,
        ConjuntoElectricoService conjuntoService,
        IPriceBankStore priceBank)
    {
        this.parent = parent;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.undo = undo;
        this.shell = shell;
        this.cubicacionPresenter = cubicacionPresenter;
        this.conjuntoService = conjuntoService;
        this.priceBank = priceBank;
    }

    public async Task SaveAsync()
    {
        if (!projectWorkspace.HasOpenProject) return;
        if (session.IsReadOnly)
        {
            WpfDialogService.Warning("Guardar", "El proyecto está en solo lectura. Abra una copia escribible para guardar cambios.");
            return;
        }
        if (!parent.TryCommitOpenCell())
        {
            WpfDialogService.Warning(
                "Guardar",
                string.IsNullOrWhiteSpace(parent.OpenEditorError)
                    ? "Hay una fórmula que no se puede calcular. Corríjala antes de guardar."
                    : parent.OpenEditorError);
            return;
        }

        parent.SaveStatusText = "Guardando…";
        try
        {
            await projectWorkspace.SaveAsync();
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
            session.MarkDirty(ProjectDirtyScope.Cubicacion);
    }

    public void MarkClean()
    {
        parent.HasPendingChanges = false;
        parent.SaveStatusText = $"Guardado a las {AppClock.Now:HH:mm}";
        parent.NotifyCubicacionSummaryChanged();
        parent.ScheduleCaptureBaseline();
    }

    public void UpdatePartidaCount(PartidaListItemVm? partida)
    {
        if (partida == null || !ReferenceEquals(partida, parent.SelectedPartida)) return;
        // Rows ya está filtrado a la partida activa, evita re-escanear toda la sesión.
        partida.RecursoCount = parent.Rows.Count(r => r.HasContent);
        parent.UpdateFilterCounts();
        parent.NotifyGlobalValorizacionChanged();
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

    public void OnRowEdited(CubicacionRowVm row, ProjectFileData? preEditSnapshot = null)
    {
        if (session.IsReadOnly)
            return;
        parent.EnsureRowInSession(row);
        parent.TryAutoResolveFromPriceBank(row);
        if (preEditSnapshot != null)
        {
            string desc = !string.IsNullOrWhiteSpace(row.Codigo) ? $"Modificar recurso {row.Codigo}" : "Editar recurso";
            undo.PushSnapshot(preEditSnapshot, desc);
            parent.HasPendingUndoForEdit = false;
        }
        else if (!parent.HasPendingUndoForEdit)
        {
            string desc = !string.IsNullOrWhiteSpace(row.Codigo) ? $"Modificar recurso {row.Codigo}" : "Editar recurso";
            undo.PushUndoPoint(session, desc);
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
        if (session.IsReadOnly)
        {
            WpfDialogService.Info("Solo lectura", "El proyecto está en solo lectura; no se pueden cargar plantillas.");
            return;
        }

        var selectedPartida = parent.SelectedPartida;
        var taggedPartidas = parent.Partidas
            .Where(p => p.IsTagged && p.EsPartida && !p.ExcluidoCubicacion)
            .ToList();

        var targetPartidaVms = taggedPartidas.Count > 0
            ? taggedPartidas
            : (selectedPartida != null && selectedPartida.EsPartida && !selectedPartida.ExcluidoCubicacion
                ? new List<PartidaListItemVm> { selectedPartida }
                : new List<PartidaListItemVm>());

        if (targetPartidaVms.Count == 0)
        {
            if (selectedPartida != null && selectedPartida.ExcluidoCubicacion)
            {
                WpfDialogService.Info("Partida excluida", "Esta partida está marcada como excluida de cubicación.");
                return;
            }

            WpfDialogService.Info(
                "Seleccione una partida",
                "Elija una partida del listado izquierdo o marque varias partidas con la barra espaciadora para cargar una plantilla.");
            return;
        }

        // Biblioteca global (banco_precios.db) unida con la copia del .qce abierto: los
        // proyectos nuevos ven las plantillas de la app sin importarlas de nuevo.
        var conjuntos = ConjuntoTemplateStore.Combinar(
                ConjuntoTemplateStore.TryGetAll(),
                session.ConjuntosElectricos)
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Nombre))
            .OrderBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (conjuntos.Count == 0)
        {
            WpfDialogService.Info(
                "Sin plantillas",
                "No hay plantillas de materiales en la app.\n\n" +
                "Cree una en la pestaña Plantillas (＋ Nueva plantilla), agréguele materiales y pulse Guardar.");
            return;
        }

        decimal partidaCant = targetPartidaVms.Count == 1 ? targetPartidaVms[0].Cantidad : 0m;
        var dialog = new ConjuntoPickerDialog(conjuntos, partidaCant);
        if (Application.Current?.MainWindow is { IsLoaded: true } main)
            dialog.Owner = main;

        if (dialog.ShowDialog() != true || dialog.SelectedConjunto == null)
            return;

        if ((dialog.SelectedConjunto.Recursos?.Count ?? 0) == 0)
        {
            WpfDialogService.Info(
                "Plantilla vacía",
                $"La plantilla '{dialog.SelectedConjunto.Nombre}' no tiene materiales.\n\n" +
                "Ábrala en Plantillas y agregue materiales antes de cargarla.");
            return;
        }

        string undoDesc = targetPartidaVms.Count > 1
            ? $"Aplicar plantilla '{dialog.SelectedConjunto.Nombre}' a {targetPartidaVms.Count} partidas"
            : $"Aplicar plantilla de materiales '{dialog.SelectedConjunto.Nombre}'";

        undo.PushUndoPoint(session, undoDesc);

        int totalAdded = 0;
        var allWarnings = new List<string>();
        bool enCero = !dialog.UsarCantidadPartida;
        bool trajoDelBanco = false;

        foreach (var targetVm in targetPartidaVms)
        {
            var partida = session.Itemizado.FirstOrDefault(i =>
                string.Equals(i.Id, targetVm.Id, StringComparison.OrdinalIgnoreCase));
            if (partida == null) continue;

            if (dialog.Reemplazar)
                cubicacionPresenter.DeleteRows(
                    session.Cubicacion
                        .Where(c => string.Equals(c.ItemId, targetVm.Id, StringComparison.OrdinalIgnoreCase))
                        .Select(c => c.Id)
                        .ToList());

            // Si UsarCantidadPartida está activo, expande con cantidadesEnCero = false usando la cantidad de la partida.
            // Si está desmarcado, expande en cero para cubicar por casillas (lo de la foto).
            var materiales = MaterialesDePlantilla(dialog.SelectedConjunto, ref trajoDelBanco);
            var result = conjuntoService.ExpandToCubicacion(
                dialog.SelectedConjunto,
                partida,
                materiales,
                cantidadesEnCero: enCero,
                proyecto: session.ProjectInfo);
            foreach (var row in result.Rows)
                CubicacionPartidaOrder.InsertAtSlot(session.Cubicacion, targetVm.Id, row, int.MaxValue);

            totalAdded += result.Rows.Count;
            if (result.Warnings.Count > 0)
                allWarnings.AddRange(result.Warnings);
        }

        // Limpiar marcas de lote tras aplicar
        foreach (var p in taggedPartidas)
            p.IsTagged = false;

        using (parent.SuppressSessionReload())
        {
            if (trajoDelBanco)
                session.MarkDirty(ProjectDirtyScope.Materiales);
            session.MarkDirty(ProjectDirtyScope.Cubicacion);
        }
        parent.LoadCubicacionRows();

        if (targetPartidaVms.Count > 1)
        {
            WpfDialogService.Info(
                "Plantilla aplicada en lote",
                $"Se aplicó la plantilla '{dialog.SelectedConjunto.Nombre}' a {targetPartidaVms.Count} partidas con éxito ({totalAdded} líneas generadas).");
            return;
        }

        if (totalAdded == 0)
        {
            var extra = allWarnings.Count > 0
                ? "\n\n" + string.Join("\n", allWarnings)
                : "";
            WpfDialogService.Info(
                "Nada que cargar",
                $"No se generaron líneas de cubicación desde '{dialog.SelectedConjunto.Nombre}'.{extra}");
            return;
        }

        if (allWarnings.Count > 0)
            WpfDialogService.Info(
                "Plantilla aplicada con advertencias",
                $"{totalAdded} material(es) cargados.\n\n" + string.Join("\n", allWarnings));
    }

    /// <summary>
    /// La plantilla busca el precio en la lista del proyecto. Si el código solo está en el banco,
    /// se incorpora al proyecto para que la fila traiga material y mano de obra y no quede en rojo.
    /// </summary>
    private List<MaterialRow> MaterialesDePlantilla(ConjuntoElectricoRow conjunto, ref bool trajoDelBanco)
    {
        var materiales = session.Materiales.ToList();
        foreach (var recurso in conjunto.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            if (string.IsNullOrWhiteSpace(recurso.Codigo))
                continue;
            if (materiales.Any(m => CodeHelper.Match(m.Codigo, recurso.Codigo)))
                continue;

            var entry = priceBank.GetByCodigo(recurso.Codigo);
            if (entry == null)
                continue;

            var material = CubicacionViewModel.FromBankEntry(entry);
            material.MoClp = MaterialMoResolver.MoClpEfectiva(material, session.ProjectInfo);
            material.RecalcularUf(session.ProjectInfo.ValorUfClp);
            session.Materiales.Add(material);
            materiales.Add(material);
            trajoDelBanco = true;
        }

        if (!trajoDelBanco)
            return materiales;

        parent.ValidationMaterialesIndex = null;
        parent.InvalidateAutocompleteMaterialIndex();
        return materiales;
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

    public void CrearConjuntoDesdePartida()
    {
        if (session.IsReadOnly)
        {
            WpfDialogService.Info("Solo lectura", "El proyecto está en solo lectura; no se pueden crear plantillas.");
            return;
        }

        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida == null || !selectedPartida.EsPartida)
        {
            WpfDialogService.Info("Seleccione una partida", "Seleccione una partida con materiales cubicados para crear una plantilla.");
            return;
        }

        var partida = session.Itemizado.FirstOrDefault(i =>
            string.Equals(i.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));

        var targetRows = parent.Rows.Where(r => r.IsSelected).ToList();
        if (targetRows.Count == 0)
            targetRows = parent.Rows.ToList();

        if (targetRows.Count == 0)
        {
            WpfDialogService.Info("Sin materiales", "Esta partida no tiene materiales cubicados.\n\nAgregue materiales antes de crear una plantilla.");
            return;
        }

        IConjuntoTemplateStore? store = null;
        try { store = new ConjuntoTemplateStore(); } catch { }

        var dialog = new CrearPlantillaDesdeCubicacionDialog(targetRows, partida, session, store);
        if (Application.Current?.MainWindow is { IsLoaded: true } main)
            dialog.Owner = main;

        dialog.ShowDialog();
    }
}
