using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Matriz de sectores (300 casillas), Hoja de Cubicación y Zonas del proyecto — extraído de
/// CubicacionViewModel.Sectors.cs (ver plan en ~/.claude/plans/glittery-stargazing-hippo.md).
/// Comportamiento preservado tal cual.
/// </summary>
internal sealed class CubicacionSectorService
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService undo;
    private readonly ICubicacionTakeoffQueryService querySvc;
    private readonly CubicacionPresenter cubicacionPresenter;

    public CubicacionSectorService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        WpfUndoService undo,
        ICubicacionTakeoffQueryService querySvc,
        CubicacionPresenter cubicacionPresenter)
    {
        this.parent = parent;
        this.session = session;
        this.undo = undo;
        this.querySvc = querySvc;
        this.cubicacionPresenter = cubicacionPresenter;
    }

    // Alt+clic (o "Ir a Hoja de Cubicación" del menú contextual) en una casilla de la matriz de
    // sectores: abre la Hoja de Cubicación en modo selección y, si el usuario elige un valor
    // (columna Suma de un ítem), lo escribe directo en ESA casilla — misma ruta que escribir la
    // expresión a mano (undo, recálculo, guardado, todo igual).
    public void PickSectorValueFromHojaCubicacion(CubicacionRowVm row, int absoluteOffset)
    {
        if (session.IsReadOnly)
            return;

        var itemsGuardados = session.HojaCubicacion.Select(HojaCubicacionItemVm.FromSaved).ToList();
        var (pickedValue, itemsActuales) = HojaCubicacionDialog.PickValue(System.Windows.Application.Current.MainWindow, itemsGuardados);

        session.HojaCubicacion.Clear();
        session.HojaCubicacion.AddRange(itemsActuales.Select(vm => vm.ToSaved()));
        session.MarkDirty("hoja_cubicacion");

        if (pickedValue == null)
            return;

        string expression = pickedValue.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        ApplySectorExpression(row, absoluteOffset, expression, pushUndo: true, syncSectorValuesSlot: true);
    }

    // Herramienta de cubicación con trazabilidad: se guarda con el proyecto (tabla propia en el
    // .qce), no solo en memoria — cerrar el diálogo (Esc, botón Cerrar o la X), cerrar el
    // proyecto o cerrar la app entera no debe perder lo escrito. Se permite abrirla aunque la
    // sesión esté en modo solo lectura; simplemente no se guardan los cambios en ese caso.
    public void OpenHojaCubicacion()
    {
        var itemsGuardados = session.HojaCubicacion.Select(HojaCubicacionItemVm.FromSaved).ToList();
        var dialog = new HojaCubicacionDialog(itemsGuardados) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();

        if (session.IsReadOnly)
            return;

        session.HojaCubicacion.Clear();
        session.HojaCubicacion.AddRange(dialog.Items.Select(vm => vm.ToSaved()));
        parent.HasPendingChanges = true;
        parent.SaveStatusText = "Cambios sin guardar";
        session.MarkDirty("hoja_cubicacion");
    }

    public void ManageZonas()
    {
        if (session.IsReadOnly)
            return;

        var dialog = new ZonasProyectoDialog(session.ZonasProyecto);
        if (dialog.ShowDialog() != true)
            return;

        undo.PushUndoPoint(session, "Editar zonas del proyecto");
        session.ZonasProyecto.Clear();
        session.ZonasProyecto.AddRange(dialog.Zonas);

        RefreshSectorWindow();
        if (parent.SelectedRow != null)
            parent.RightPanel.LoadRow(parent.SelectedRow);
        parent.MarkDirty();
    }

    public void InitializeSectorRow(CubicacionRowVm row)
    {
        row.SectorExpressionChanged = OnSectorExpressionChanged;
        row.SectorBatchExpressionChanged = OnSectorBatchExpressionChanged;
        row.CantidadChanged = OnCantidadChanged;
        row.LoadSectorWindow(0);
        RefreshSectorDisplayValues(row);
    }

    // Recalcula y publica en SectorDisplayValues el RESULTADO de cada fórmula de sector (ej.
    // celda con "2+3" muestra "5"), igual que una celda de Excel que muestra el resultado y no
    // el texto de la fórmula. SectorValues (el texto crudo) no se toca acá — solo se usa
    // mientras se está editando la celda.
    private void RefreshSectorDisplayValues(CubicacionRowVm row)
    {
        querySvc.EvaluateStoredExpressions(session, row.Source, CubicacionViewModel.MaximumSectorCount, (offset, value) =>
            row.SetSectorDisplayValue(offset, CubicacionRowVm.FormatDecimal(value)));
    }

    public void RefreshSectorWindow()
    {
        parent.VisibleSectorHeaders.Clear();
        for (int offset = 0; offset < CubicacionViewModel.MaximumSectorCount; offset++)
            parent.VisibleSectorHeaders.Add(ResolveSectorHeader(offset));

        foreach (var row in parent.Rows)
        {
            row.LoadSectorWindow(0);
            RefreshSectorDisplayValues(row);
        }

        parent.NotifyVisibleSectorHeadersChanged();
    }

    /// <summary>
    /// Zonas activas en el orden de las columnas de casillas: el índice en esta lista ES el número
    /// de casilla (offset) que se guarda en <see cref="CubicacionRow.QtyExpressions"/>. Única fuente
    /// de verdad, compartida con el diálogo que pregunta en qué zona entra un material recién creado
    /// (ver CubicacionRowMutationService) para que encabezados y escritura no se desalineen.
    /// </summary>
    public static IReadOnlyList<ZonaProyecto> ZonasActivasOrdenadas(BudgetProjectSession session)
        => (session?.ZonasProyecto ?? new List<ZonaProyecto>())
            .Where(z => z != null && z.Activa)
            .OrderBy(z => z.Orden)
            .ToList();

    private string ResolveSectorHeader(int offset)
    {
        var zones = ZonasActivasOrdenadas(session);

        if (offset < zones.Count && !string.IsNullOrWhiteSpace(zones[offset].Nombre))
            return zones[offset].Nombre.Trim();

        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida != null &&
            session.QtyHeadersByItem.TryGetValue(selectedPartida.Id, out var headers) &&
            headers != null && offset < headers.Length &&
            !string.IsNullOrWhiteSpace(headers[offset]))
        {
            return headers[offset].Trim();
        }

        return "";
    }

    private void OnSectorExpressionChanged(CubicacionRowVm row, int offset, string expression)
        => ApplySectorExpression(row, offset, expression, pushUndo: true, syncSectorValuesSlot: false);

    private void OnSectorBatchExpressionChanged(CubicacionRowVm row, HashSet<int> slots)
        => ApplyBatchSectorExpressions(row, slots);

    // ── Propagación maestro→esclavo (fila con Esclavo heredado de la plantilla) ────────────
    // Cuando cambia la CANTIDAD (columna CANT) de una fila maestro, cada esclavo de la misma
    // partida recalcula su cantidad con ConjuntoElectricoService.ComputeEsclavoValue usando
    // los parámetros guardados en CubicacionRow.Esclavo. Solo los maestros propagan (un nivel,
    // sin cadenas); las ediciones de zona las propaga ApplySectorExpression por casilla.
    private void OnCantidadChanged(CubicacionRowVm row)
    {
        if (session.IsReadOnly) return;
        bool esOperacion = !string.IsNullOrWhiteSpace(row.Source.Operacion);
        if (row.Source.Esclavo != null) return;
        // Filas con fórmula de zona: quien propaga es el camino de zona, no el total. Las
        // filas Operación sí propagan (su total es su fórmula, no tiene zonas propias).
        if (row.HasFormula && !esOperacion) return;
        PropagarEsclavos(row, -1, null);
        // Siempre (la fórmula manda): una edición manual sobre una fila Operación se revierte
        // al valor de su fórmula en el mismo evento; el guard evita bucles durante una tanda.
        RecalcularOperaciones(row.Source.ItemId);
    }

    // ── Recálculo en vivo de filas Operación (fórmula libre por fila) ──────────────────────
    // Cada edición de cantidad o zona de la partida reevalúa las filas con CubicacionRow.Operacion
    // (ej. "PAZ * 2 + TEEG500") contra las cantidades vigentes de las demás filas, en orden
    // topológico con exclusiones por ciclo (OperacionFilaService). El guard evita reentrada:
    // setear la cantidad de una operación vuelve a entrar por OnCantidadChanged solo para
    // propagar a SUS esclavos, nunca para reevaluar la misma tanda.
    private bool recalculandoOperaciones;

    private void RecalcularOperaciones(string? itemId)
    {
        if (session.IsReadOnly || recalculandoOperaciones) return;
        if (string.IsNullOrWhiteSpace(itemId)) return;

        var todasVm = parent.Rows
            .Where(r => string.Equals(r.Source.ItemId ?? "", itemId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var filasVm = todasVm
            .Where(r => !string.IsNullOrWhiteSpace(r.Source.Operacion))
            .ToList();
        if (filasVm.Count == 0) return;

        recalculandoOperaciones = true;
        try
        {
            // Se pasa toda la partida (no solo las operaciones): las filas normales aportan
            // sus cantidades como variables de fórmula; Resolver solo modifica filas Operación.
            var cambiadas = OperacionFilaService.Resolver(todasVm.Select(r => r.Source).ToList());
            if (cambiadas.Count == 0) return;

            foreach (var vm in filasVm)
            {
                if (!cambiadas.Contains(vm.Source)) continue;
                // Resolver ya seteó Source.Cantidad; el setter del VM sincroniza el cache,
                // notifica la UI y dispara CantidadChanged → PropagarEsclavos de ESTA fila.
                vm.Cantidad = vm.Source.Cantidad;
                vm.RefreshDisplayProperties();
                parent.ValidateRow(vm);
                session.SyncCubicacionRowComposition(vm.Source);
            }

            parent.ScheduleRefreshTotals();
            parent.NotifyCubicacionSummaryChanged();
            parent.MarkDirty();
        }
        finally
        {
            recalculandoOperaciones = false;
        }
    }

    /// <summary>
    /// Escribe el resultado de la fórmula maestro→esclavo en los esclavos de <paramref name="maestro"/>.
    /// offset &gt;= 0: edición de una casilla de zona (solo ese offset se refleja en los esclavos
    /// con zonas; los esclavos sin zonas siguen el total). offset == -1: edición de la columna CANT.
    /// </summary>
    private void PropagarEsclavos(CubicacionRowVm maestro, int offset, decimal? zoneValue)
    {
        if (maestro.Source.Esclavo != null) return; // solo los maestros propagan (sin cadenas)
        string maestroCodigo = maestro.Source.Codigo ?? "";
        if (string.IsNullOrWhiteSpace(maestroCodigo)) return;

        bool any = false;
        foreach (var slaveVm in parent.Rows)
        {
            if (ReferenceEquals(slaveVm, maestro)) continue;
            var vinculo = slaveVm.Source.Esclavo;
            if (vinculo == null) continue;
            if (!string.Equals(slaveVm.Source.ItemId ?? "", maestro.Source.ItemId ?? "", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CodeHelper.Match(vinculo.MaestroCodigo, maestroCodigo)) continue;

            if (slaveVm.HasFormula)
            {
                // Esclavo con zonas: recibe el valor de la casilla editada (o la cantidad
                // total del maestro en la casilla 0, cuando se editó la columna CANT).
                string text;
                if (offset < 0)
                    text = FormatExpression(ConjuntoElectricoService.ComputeEsclavoValue(maestro.Cantidad, vinculo));
                else if (zoneValue == null)
                    text = string.Empty;
                else
                    text = FormatExpression(ConjuntoElectricoService.ComputeEsclavoValue(zoneValue.Value, vinculo));
                // SetSectorExpressionDirect ya recalcula, revalida, sincroniza composición y
                // marca la sesión como modificada para el esclavo.
                SetSectorExpressionDirect(slaveVm, offset < 0 ? 0 : offset, text);
            }
            else
            {
                // Esclavo sin zonas: su CANTIDAD refleja el total vigente del maestro.
                decimal computed = ConjuntoElectricoService.ComputeEsclavoValue(maestro.Cantidad, vinculo);
                if (slaveVm.Cantidad == computed) continue;
                slaveVm.Cantidad = computed;
                slaveVm.RefreshDisplayProperties();
                parent.ValidateRow(slaveVm);
                session.SyncCubicacionRowComposition(slaveVm.Source);
            }
            any = true;
        }

        if (!any) return;
        parent.ScheduleRefreshTotals();
        parent.NotifyCubicacionSummaryChanged();
        parent.MarkDirty();
    }

    private static string FormatExpression(decimal value)
        => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    public void ApplyBatchSectorExpressions(CubicacionRowVm row, HashSet<int> slots)
    {
        if (session.IsReadOnly || slots == null || slots.Count == 0)
            return;

        parent.EnsureRowInSession(row);

        int maxOffset = slots.Max();
        int requiredLength = Math.Max(row.Source.QtyExpressions?.Length ?? 0, maxOffset + 1);
        var expressions = querySvc.NormalizeExpressions(row.Source.QtyExpressions, requiredLength);

        foreach (int offset in slots)
        {
            if (offset < 0 || offset >= CubicacionViewModel.MaximumSectorCount)
                continue;

            string expression = row.SectorValues[offset];
            expressions[offset] = expression?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expressions[offset]))
                row.SetSectorDisplayValue(offset, string.Empty);
        }

        var compactedExpressions = CubicacionSectorExpressionCompactor.TrimTrailingEmpty(expressions);
        row.Source.QtyExpressions = compactedExpressions;
        row.Source.TotalExpression = string.Empty;

        // Valores por casilla (para propagar a esclavos) + supresión del eco por CANTIDAD:
        // en filas con zonas quien propaga es el camino de zona, no el total.
        var zoneValues = new Dictionary<int, decimal>();
        row.SuppressCantidadPropagation = true;
        try
        {
            row.Cantidad = querySvc.EvaluateStoredExpressions(session, row.Source, CubicacionViewModel.MaximumSectorCount, (zoneOffset, zoneValue) =>
            {
                row.SetSectorDisplayValue(zoneOffset, CubicacionRowVm.FormatDecimal(zoneValue));
                zoneValues[zoneOffset] = zoneValue;
            });

            decimal calculatedQuantity = row.Cantidad;
            row.RefreshDisplayProperties();

            cubicacionPresenter.AfterRowEdited(row.Source);

            row.Source.QtyExpressions = compactedExpressions;
            row.Cantidad = calculatedQuantity;
            session.SyncCubicacionRowComposition(row.Source);
        }
        finally
        {
            row.SuppressCantidadPropagation = false;
        }

        foreach (int offset in slots)
        {
            if (offset < 0 || offset >= CubicacionViewModel.MaximumSectorCount)
                continue;
            PropagarEsclavos(row, offset, zoneValues.TryGetValue(offset, out var v) ? v : null);
        }

        RecalcularOperaciones(row.Source.ItemId);
    }

    public void SetSectorExpressionDirect(CubicacionRowVm row, int offset, string expression)
    {
        // Llamado desde el panel de zonas (derecho), que no toca row.SectorValues directamente,
        // por eso debe resincronizarlo acá.
        ApplySectorExpression(row, offset, expression, pushUndo: false, syncSectorValuesSlot: true);
    }

    private void ApplySectorExpression(CubicacionRowVm row, int offset, string? expression, bool pushUndo, bool syncSectorValuesSlot)
    {
        if (session.IsReadOnly || offset < 0 || offset >= CubicacionViewModel.MaximumSectorCount)
            return;

        if (pushUndo && !parent.HasPendingUndoForEdit)
        {
            undo.PushUndoPoint(session, $"Editar sector {offset + 1}");
            parent.HasPendingUndoForEdit = true;
        }

        // Editar SOLO una casilla de sector (sin tocar antes Código/Descripción/Cantidad) en una
        // fila de relleno nueva nunca la persistía en session.Cubicacion — a diferencia de
        // OnRowEdited (usado por las demás columnas), este camino no llamaba a
        // EnsureRowInSession. La fila quedaba huérfana y su contenido se perdía al cambiar de
        // partida o guardar, dando la impresión de que "solo se pegó la última casilla" al
        // pegar/escribir varias casillas seguidas de una fila nueva.
        parent.EnsureRowInSession(row);

        int requiredLength = Math.Max(row.Source.QtyExpressions?.Length ?? 0, offset + 1);
        var expressions = querySvc.NormalizeExpressions(row.Source.QtyExpressions, requiredLength);
        expressions[offset] = expression?.Trim() ?? string.Empty;
        var compactedExpressions = CubicacionSectorExpressionCompactor.TrimTrailingEmpty(expressions);
        row.Source.QtyExpressions = compactedExpressions;
        row.Source.TotalExpression = string.Empty;
        // Blanquear primero la celda editada: EvaluateStoredExpressions solo invoca el callback
        // para sectores con expresión NO vacía, así que si se borró el contenido de la celda
        // (offset justo editado ahora vacío), el callback no la toca y quedaría mostrando el
        // valor calculado anterior si no se limpia acá.
        if (string.IsNullOrWhiteSpace(expressions[offset]))
            row.SetSectorDisplayValue(offset, string.Empty);
        // Valor de la casilla editada para la propagación maestro→esclavo (null = celda vacía)
        // + supresión del eco por CANTIDAD: en filas con zonas quien propaga es esta ruta.
        decimal? zoneEditValue = null;
        row.SuppressCantidadPropagation = true;
        try
        {
            row.Cantidad = querySvc.EvaluateStoredExpressions(session, row.Source, CubicacionViewModel.MaximumSectorCount, (zoneOffset, zoneValue) =>
            {
                row.SetSectorDisplayValue(zoneOffset, CubicacionRowVm.FormatDecimal(zoneValue));
                if (zoneOffset == offset) zoneEditValue = zoneValue;
            });
            decimal calculatedQuantity = row.Cantidad;
            row.RefreshDisplayProperties();

            if (syncSectorValuesSlot)
                row.SectorValues[offset] = expression ?? string.Empty;

            cubicacionPresenter.AfterRowEdited(row.Source);
            parent.ScheduleRefreshTotals();
            parent.ValidateRow(row);
            parent.UpdatePartidaCount(parent.SelectedPartida);
            parent.NotifyCubicacionSummaryChanged();
            parent.MarkDirty();
            // Los observadores de sesión pueden recomponer la fila legacy al publicar el cambio.
            // Se consolida al final para conservar sectores vacíos intermedios y el sector 300.
            row.Source.QtyExpressions = compactedExpressions;
            row.Cantidad = calculatedQuantity;
            session.SyncCubicacionRowComposition(row.Source);
        }
        finally
        {
            row.SuppressCantidadPropagation = false;
        }

        // Propagación a esclavos solo en la ruta sin syncSectorValuesSlot: las rutas que
        // escriben SectorValues (grid y panel derecho) vuelven a entrar por
        // OnSectorExpressionChanged con sync=false, y así cada edición propaga exactamente una vez.
        if (!syncSectorValuesSlot)
        {
            PropagarEsclavos(row, offset, zoneEditValue);
            if (ReferenceEquals(parent.SelectedRow, row))
                parent.RightPanel.LoadRow(row);
            parent.HasPendingUndoForEdit = false;
        }

        // Operaciones de la partida: la fila editada pudo cambiar de cantidad y las filas con
        // Operación que la referencian por código deben reevaluarse (en vivo).
        RecalcularOperaciones(row.Source.ItemId);
    }
}
