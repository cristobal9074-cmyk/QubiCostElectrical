using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

public sealed partial class EstadosPagoViewModel
{
    private sealed class SeccionTemplate
    {
        public SeccionTemplate(string capitulo, List<EstadoPagoPartidaInput> partidas)
        {
            Capitulo = capitulo;
            Partidas = partidas;
        }

        public string Capitulo { get; }
        public List<EstadoPagoPartidaInput> Partidas { get; }
    }

    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IPermissionService permissions;
    private readonly ItemizadoPresenter presenter;
    private readonly IItemizadoWorkspaceService itemizadoWorkspace;
    private readonly EstadosPagoExportCoordinator exportCoordinator;
    private readonly EstadosPagoWorkflowCoordinator workflowCoordinator;
    private readonly WpfUndoService? undo;
    internal WpfUndoService? UndoService => undo;

    private readonly List<SeccionTemplate> seccionesTemplate = new();
    private List<EstadoPagoPartidaInput> partidas = new();
    private readonly Dictionary<int, EstadoPagoMesCalc> totalesPorMes = new();
    private EstadoPagoMesRow? currentRow;
    private bool hayPendienteGenerable;

    public EstadoPagoMesRow? CurrentRow => currentRow;

    public RelayCommand<EstadoPagoMesCellVm> SelectMesCommand { get; }
    public RelayCommand GenerarMesCommand { get; }
    public RelayCommand GenerarEstadoMesCommand { get; }
    public RelayCommand DuplicarAnteriorCommand { get; }
    public RelayCommand AprobarCommand { get; }
    public RelayCommand RegistrarPagoCommand { get; }
    public RelayCommand ReabrirCommand { get; }
    public RelayCommand OpenProjectCommand { get; }
    public RelayCommand ExportarExcelCommand { get; }
    public RelayCommand ExportarPdfCommand { get; }
    public RelayCommand IrAConfiguracionCommand { get; }
    public RelayCommand VolverAEstadosCommand { get; }

    public EstadosPagoViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        IPermissionService permissions,
        ItemizadoPresenter presenter,
        IItemizadoWorkspaceService itemizadoWorkspace,
        EstadosPagoExportCoordinator exportCoordinator,
        EstadosPagoWorkflowCoordinator workflowCoordinator,
        WpfUndoService? undo = null)
        : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;
        this.presenter = presenter;
        this.itemizadoWorkspace = itemizadoWorkspace;
        this.exportCoordinator = exportCoordinator;
        this.workflowCoordinator = workflowCoordinator;
        this.undo = undo;

        IrAConfiguracionCommand = new RelayCommand(() => SelectedTabIndex = 1);
        VolverAEstadosCommand = new RelayCommand(() => SelectedTabIndex = 0);
        SelectMesCommand = new RelayCommand<EstadoPagoMesCellVm>(SelectMes);
        GenerarMesCommand = new RelayCommand(GenerarMes, () => CanEdit && HasProject && hayPendienteGenerable);
        GenerarEstadoMesCommand = new RelayCommand(
            GenerarEstadoMes,
            () => CanEdit && HasProject && currentRow is null);
        DuplicarAnteriorCommand = new RelayCommand(
            DuplicarAnterior,
            () => CanEdit && HasProject && currentRow is null
                && PreviousGeneratedRow(SelectedAnio, SelectedMes) is not null);
        AprobarCommand = new RelayCommand(
            Aprobar,
            () => CanApprove && currentRow?.Estado == EstadoPagoEst.Borrador);
        RegistrarPagoCommand = new RelayCommand(
            RegistrarPago,
            () => CanApprove && currentRow?.Estado == EstadoPagoEst.Revision);
        ReabrirCommand = new RelayCommand(
            Reabrir,
            () => CanApprove && currentRow is not null && currentRow.Estado != EstadoPagoEst.Borrador);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ExportarExcelCommand = new RelayCommand(
            () => exportCoordinator.ExportarExcel(SelectedMes), CanExportEstado);
        ExportarPdfCommand = new RelayCommand(
            () => exportCoordinator.ExportarPdf(SelectedMes), CanExportEstado);

        Reload();
    }

    public void Refresh() => Reload();

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        // Escritura propia de la pantalla (avances/config/transiciones): la vista ya
        // está al día; recargar rompería el foco de edición en curso.
        if (string.Equals(e.Scope, ProjectDirtyScope.EstadosPago.Key, StringComparison.OrdinalIgnoreCase))
            return;
        // Autosave/guardado regular: nada estructural cambió; pero si es Snapshot aplicado (Undo/Redo), sí debe recargar.
        if (string.Equals(e.Scope, "saved", StringComparison.OrdinalIgnoreCase)
            && !e.Reason.StartsWith("Snapshot aplicado", StringComparison.OrdinalIgnoreCase))
            return;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Reload);
    }

    // ─── Carga ────────────────────────────────────────────────────────────────

    private void Reload()
    {
        IsLoading = true;
        try
        {
            HasProject = workspace.HasOpenProject;
            ShowEmptyState = !HasProject;
            CanEdit = HasProject && !session.IsReadOnly;
            CanApprove = HasProject && permissions.CurrentRole == UserRole.Admin;

            if (!HasProject)
            {
                partidas = new List<EstadoPagoPartidaInput>();
                seccionesTemplate.Clear();
                totalesPorMes.Clear();
                currentRow = null;
                hayPendienteGenerable = false;
                Meses.Clear();
                ClearSecciones();
                MesEmptyVisible = false;
                DetailVisible = false;
                RaiseCommandStates();
                return;
            }

            LoadPartidas();
            SyncConfigFromSession();
            EnsureSelection();
            RebuildAll();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Reconstruye las partidas planas y las secciones por capítulo desde el Itemizado.</summary>
    private void LoadPartidas()
    {
        partidas = new List<EstadoPagoPartidaInput>();
        seccionesTemplate.Clear();

        var state = itemizadoWorkspace.BuildPageState(presenter, session);
        var secciones = new List<SeccionTemplate>();
        var actual = new SeccionTemplate("", new List<EstadoPagoPartidaInput>());

        foreach (var row in state.Rows)
        {
            if (row.EsTitulo)
            {
                if (actual.Partidas.Count > 0)
                    secciones.Add(actual);
                string capitulo = string.IsNullOrWhiteSpace(row.Item)
                    ? row.Descripcion
                    : $"{row.Item} · {row.Descripcion}";
                actual = new SeccionTemplate(capitulo, new List<EstadoPagoPartidaInput>());
            }
            else
            {
                var partida = new EstadoPagoPartidaInput(row.Id, row.Item, row.Descripcion, row.TotalUf, row.Unidad, row.Cantidad, row.UnitPriceUf);
                actual.Partidas.Add(partida);
                partidas.Add(partida);
            }
        }

        if (actual.Partidas.Count > 0)
            secciones.Add(actual);

        seccionesTemplate.AddRange(secciones);
    }

    private void SyncConfigFromSession()
    {
        var cfg = session.EstadoPagoConfig;
        SetBacking(ref configAnio, cfg.Anio, nameof(ConfigAnio));
        SetBacking(ref configMesInicio, cfg.MesInicio, nameof(ConfigMesInicio));
        SetBacking(ref configCantMeses, cfg.CantMeses, nameof(ConfigCantMeses));
        SetBacking(ref configAnticipoPct, cfg.AnticipoPct, nameof(ConfigAnticipoPct));
        SetBacking(ref configRetencionPct, cfg.RetencionPct, nameof(ConfigRetencionPct));
        SetBacking(ref configIvaPct, cfg.IvaPct, nameof(ConfigIvaPct));
        OnPropertyChanged(nameof(ConfigPeriodoObraTexto));
    }

    private void SetBacking<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void EnsureSelection()
    {
        currentRow = FindRow(SelectedAnio, SelectedMes);
        if (currentRow is not null)
            return;

        var pend = workflowCoordinator.FirstPendiente();
        if (pend.HasValue)
        {
            SelectedAnio = pend.Value.Anio;
            SelectedMes = pend.Value.Mes;
        }
        else
        {
            var last = workflowCoordinator.LastGeneratedRow();
            if (last is not null)
            {
                SelectedAnio = last.Anio;
                SelectedMes = last.Mes;
            }
            else
            {
                var (initA, initM) = EstadoPagoCalculator.GetMonthForPeriod(session.EstadoPagoConfig, 0);
                SelectedAnio = initA;
                SelectedMes = initM;
            }
        }
        currentRow = FindRow(SelectedAnio, SelectedMes);
    }

    // ─── Reconstrucción ───────────────────────────────────────────────────────

    private void RebuildAll()
    {
        if (!HasProject)
            return;

        var cfg = session.EstadoPagoConfig;
        CalendarTitle = $"Calendario de estados · {cfg.Anio}";
        currentRow = FindRow(SelectedAnio, SelectedMes);
        RecomputeMonthTotals();
        RebuildCells();
        RebuildKpis();
        RebuildDetail();
        RebuildAside();
        hayPendienteGenerable = FirstPendiente().HasValue;
        RaiseCommandStates();
    }

    private void RecomputeMonthTotals()
    {
        totalesPorMes.Clear();
        if (!HasProject)
            return;
        workflowCoordinator.RecomputeMonthTotals(partidas, totalesPorMes);
    }

    private void RebuildCells()
    {
        Meses.Clear();
        var cfg = session.EstadoPagoConfig;
        int cant = Math.Max(1, cfg.CantMeses);

        var rows = session.EstadoPagoMeses
            .Where(r => r is not null)
            .GroupBy(r => (r.Anio, r.Mes))
            .ToDictionary(g => g.Key, g => g.Last());

        for (int i = 0; i < cant; i++)
        {
            var (anio, m) = EstadoPagoCalculator.GetMonthForPeriod(cfg, i);
            rows.TryGetValue((anio, m), out var row);
            bool generado = row is not null;
            string estado = row?.Estado ?? "pendiente";
            var (label, _) = EstadoPagoFormat.EstadoBadge(estado);
            bool generable = !generado;

            var cell = new EstadoPagoMesCellVm(
                anio, m, EstadoPagoFormat.MesesCortos[m - 1], EstadoPagoFormat.MesesNombres[m - 1],
                estado, label, generado, generable, i + 1)
            {
                IsSelected = anio == SelectedAnio && m == SelectedMes
            };

            if (generado && totalesPorMes.TryGetValue(m, out var totales))
                cell.MontoLabel = FormatMoney(totales.Total);

            Meses.Add(cell);
        }
    }

    private void RebuildKpis()
    {
        var cfg = session.EstadoPagoConfig;
        var (anioBase, mesBase) = workflowCoordinator.PeriodoBase(currentRow, SelectedAnio, SelectedMes);
        var acumBase = mesBase > 0
            ? EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, anioBase, mesBase, cfg.Anio)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var kpis = EstadoPagoCalculator.BuildKpis(partidas, acumBase, session.EstadoPagoMeses, cfg);
        KpiContratado = kpis.TotalContratado;
        KpiPagadosLabel = $"{kpis.Pagados} / {kpis.CantMeses}";
        KpiAcum = kpis.Acumulado;
        KpiSaldo = kpis.Saldo;
        KpiRetencion = kpis.RetencionAcum;
        KpiAvancePct = kpis.AvanceFisicoPct;

        // El texto de los KPIs depende de la moneda del proyecto (CLP no muestra UF).
        OnPropertyChanged(nameof(KpiContratadoText));
        OnPropertyChanged(nameof(KpiAcumText));
        OnPropertyChanged(nameof(KpiSaldoText));
        OnPropertyChanged(nameof(KpiRetencionText));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    private void ClearSecciones()
    {
        foreach (var sec in Secciones)
        {
            foreach (var p in sec.Partidas)
                p.IsDisposed = true;
        }
        Secciones.Clear();
    }

    private void RebuildDetail()
    {
        ClearSecciones();
        var cfg = session.EstadoPagoConfig;
        bool generado = currentRow is not null;

        MesEmptyVisible = HasProject && !generado;
        DetailVisible = HasProject && generado;
        if (!HasProject)
            return;

        MesEmptyTitle = $"Sin estado de pago para {EstadoPagoFormat.MesesLargos[SelectedMes - 1]} de {cfg.Anio}";
        MesEmptyDesc = "Genera el estado del mes con avance 0 % en cada partida. Escribe el % de avance real del mes y el acumulado, la retención y el IVA se recalculan en vivo.";
        MesEmptyGenerarLabel = $"Generar estado de {EstadoPagoFormat.MesesCortos[SelectedMes - 1].ToLowerInvariant()}";

        if (!generado || currentRow is null)
        {
            IsMesEditable = false;
            TableNoteText = "";
            DetalleTitulo = "";
            DetalleSub = "";
            BadgeLabel = "Pendiente";
            BadgeKind = "Muted";
            ResetTotalesTexts();
            return;
        }

        IsMesEditable = CanEdit && currentRow.Estado == EstadoPagoEst.Borrador;
        var (prevAnio, prevMes) = EstadoPagoCalculator.PreviousMonth(SelectedAnio, SelectedMes);
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, prevAnio, prevMes, cfg.Anio);
        var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, currentRow.AvancesPorItem, cfg);

        DetalleTitulo = $"Estado {currentRow.Folio} · {EstadoPagoFormat.MesesNombres[SelectedMes - 1]} {SelectedAnio}";
        DetalleSub = $"Emitido {currentRow.FechaEmision:dd/MM/yyyy} · {(MuestraUf ? "Valores en UF" : "Valores en CLP")} · {partidas.Count} partidas del Itemizado";
        var (badgeLabel, badgeKind) = EstadoPagoFormat.EstadoBadge(currentRow.Estado);
        BadgeLabel = badgeLabel;
        BadgeKind = badgeKind;
        TableNoteText = IsMesEditable
            ? "Modo edición: escribe el % de avance de cada partida del mes (Enter/Tab/↓ avanzan de fila, ↑ retrocede; el valor se confirma al salir de la celda). Acumulado, retención, anticipo e IVA se recalculan en vivo."
            : "Estado cerrado: los % de avance quedan registrados y alimentan el acumulado de los meses siguientes.";

        var porId = detalle.Filas.GroupBy(f => f.ItemId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var tpl in seccionesTemplate)
        {
            var filas = new List<EstadoPagoPartidaVm>(tpl.Partidas.Count);
            foreach (var p in tpl.Partidas)
            {
                if (!porId.TryGetValue(p.Id, out var f))
                    continue;
                filas.Add(new EstadoPagoPartidaVm(
                    this, p, f.AvanceAnterior, f.AvanceMes, f.MontoMes, f.AcumPct, IsMesEditable));
            }

            if (filas.Count > 0)
                Secciones.Add(new EstadoPagoSeccionVm { Capitulo = tpl.Capitulo, Partidas = filas });
        }

        ApplyTotalesTexts(detalle.Totales, cfg);
    }

    private void RebuildAside()
    {
        var cfg = session.EstadoPagoConfig;
        var (anioBase, mesBase) = workflowCoordinator.PeriodoBase(currentRow, SelectedAnio, SelectedMes);

        if (mesBase <= 0)
        {
            ResumenSub = "Aún no hay estados";
            ResumenBadgeLabel = "Pendiente";
            ResumenBadgeKind = "Muted";
            ResTotalText = "—";
            ResClpText = "Genera el mes para ver el cálculo";
            ResMesText = "—";
            ResAcumText = "—";
            ResPctText = "—";
            ResReteMesText = "—";
            ResReteAcumText = "—";
            ResAntSaldoText = "—";
            ApplyAvanceTexts();
            return;
        }

        var rowBase = FindRow(anioBase, mesBase);
        var (prevAnio, prevMes) = EstadoPagoCalculator.PreviousMonth(anioBase, mesBase);
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, prevAnio, prevMes, cfg.Anio);
        var detalleBase = EstadoPagoCalculator.CalcularMes(
            partidas, acumAntes, rowBase?.AvancesPorItem ?? new Dictionary<string, decimal>(), cfg);
        var t = detalleBase.Totales;

        ResumenSub = rowBase is not null
            ? $"{rowBase.Folio} · {EstadoPagoFormat.MesesNombres[mesBase - 1]} {anioBase}"
            : $"{EstadoPagoFormat.MesesNombres[mesBase - 1]} de {anioBase}";
        var (label, kind) = EstadoPagoFormat.EstadoBadge(rowBase?.Estado ?? "pendiente");
        ResumenBadgeLabel = label;
        ResumenBadgeKind = kind;
        ResTotalText = FormatMoney(t.Total);

        ResClpText = MuestraUf
            ? $"≈ $ {Math.Round(t.Total * UfRate):N0} CLP"
            : "Valores en pesos chilenos (CLP)";
        ResMesText = FormatMoney(t.TrabajoMes);
        ResAcumText = FormatMoney(t.Acum);
        ResPctText = EstadoPagoFormat.Pct(t.PctAcum);
        ResReteMesText = FormatMoney(t.RetencionMes);
        ResReteAcumText = FormatMoney(t.RetencionAcum);
        ResAntSaldoText = FormatMoney(t.AnticipoSaldo);
        ApplyAvanceTexts();
    }

    private void ApplyAvanceTexts()
    {
        AvPctText = EstadoPagoFormat.Pct(KpiAvancePct);
        double fill = (double)KpiAvancePct;
        if (fill < 0d) fill = 0d;
        if (fill > 100d) fill = 100d;
        AvFillPct = fill;
        AvLeftText = $"Ejecutado {FormatMoney(KpiAcum)}";
        AvRightText = $"Total {FormatMoney(KpiContratado)}";
        AvEjecText = FormatMoney(KpiAcum);
        AvSaldoText = FormatMoney(KpiSaldo);
        AvMesesLabel = KpiPagadosLabel;
    }

    private void ApplyTotalesTexts(EstadoPagoMesCalc t, EstadoPagoConfig cfg)
    {
        TotTrabajoMesText = FormatMoney(t.TrabajoMes);
        TotAcumText = FormatMoney(t.Acum);
        TotRecuperoLabel = $"Recupero de anticipo ({EstadoPagoFormat.PctConfig(cfg.AnticipoPct)})";
        TotRecuperoText = FormatMoney(t.RecuperoMes);
        TotRetencionLabel = $"Retención garantía ({EstadoPagoFormat.PctConfig(cfg.RetencionPct)})";
        TotRetencionText = FormatMoney(t.RetencionMes);
        TotNetoText = FormatMoney(t.Neto);
        TotIvaLabel = $"IVA ({EstadoPagoFormat.PctConfig(cfg.IvaPct)})";
        TotIvaText = FormatMoney(t.Iva);
        TotMesLabel = $"Total a pagar · {EstadoPagoFormat.MesesCortos[SelectedMes - 1].ToLowerInvariant()}";
        TotTotalText = FormatMoney(t.Total);
    }

    private void ResetTotalesTexts()
    {
        TotTrabajoMesText = "—";
        TotAcumText = "—";
        TotRecuperoLabel = "Recupero de anticipo";
        TotRecuperoText = "—";
        TotRetencionLabel = "Retención garantía";
        TotRetencionText = "—";
        TotNetoText = "—";
        TotIvaLabel = "IVA";
        TotIvaText = "—";
        TotMesLabel = "Total a pagar";
        TotTotalText = "—";
    }

    // ─── Recalculo ligero (edición de avance, sin reconstruir filas) ──────────

    internal void OnAvanceMesChanged(EstadoPagoPartidaVm partida)
    {
        if (!CanEdit || currentRow is null || isLoading || partida.IsDisposed || undo?.IsUndoingOrRedoing == true)
            return;

        undo?.PushUndoPoint(session, $"Modificar avance {partida.Item} ({partida.AvanceMes:N1}%)");
        currentRow.AvancesPorItem[partida.ItemId] = partida.AvanceMes;
        ClampLaterMonths(partida.ItemId);
        session.MarkDirty(ProjectDirtyScope.EstadosPago);

        partida.UpdateComputed(
            partida.AvanceAnterior + partida.AvanceMes,
            partida.Monto * partida.AvanceMes / 100m);

        RecomputeValues();
    }

    /// <summary>
    /// Mantiene la monotonicidad: ningún mes posterior puede superar 100 % acumulado
    /// para la partida editada (recorta sus avances guardados si era necesario).
    /// </summary>
    private void ClampLaterMonths(string itemId)
    {
        var cfg = session.EstadoPagoConfig;
        var acum = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, SelectedAnio, SelectedMes, cfg.Anio);
        acum.TryGetValue(itemId, out decimal running);
        int selectedKey = SelectedAnio * 12 + SelectedMes;

        foreach (var row in GeneratedRows())
        {
            if (row.Anio * 12 + row.Mes <= selectedKey)
                continue;
            if (!row.AvancesPorItem.TryGetValue(itemId, out decimal av))
                continue;

            decimal max = 100m - running;
            if (max < 0m) max = 0m;
            if (av > max)
            {
                av = max;
                row.AvancesPorItem[itemId] = av;
            }
            running += av;
        }
    }

    private void RecomputeValues()
    {
        if (!HasProject)
            return;

        var cfg = session.EstadoPagoConfig;
        RecomputeMonthTotals();

        foreach (var cell in Meses)
        {
            if (totalesPorMes.TryGetValue(cell.Mes, out var totales))
                cell.MontoLabel = FormatMoney(totales.Total);
        }

        if (currentRow is not null && totalesPorMes.TryGetValue(SelectedMes, out var t))
            ApplyTotalesTexts(t, cfg);

        RebuildKpis();
        RebuildAside();
    }

    // ─── Comandos ─────────────────────────────────────────────────────────────

    private void SelectMes(EstadoPagoMesCellVm? cell)
    {
        if (cell is null || (cell.Anio == SelectedAnio && cell.Mes == SelectedMes) || !HasProject)
            return;
        SelectedAnio = cell.Anio;
        SelectedMes = cell.Mes;
        RebuildAll();
    }

    private void GenerarMes()
    {
        var pend = workflowCoordinator.FirstPendiente();
        if (pend.HasValue)
            GenerateMonth(pend.Value.Anio, pend.Value.Mes, duplicadoDe: null);
    }

    private void GenerarEstadoMes() => GenerateMonth(SelectedAnio, SelectedMes, duplicadoDe: null);

    private void DuplicarAnterior()
    {
        var prev = workflowCoordinator.PreviousGeneratedRow(SelectedAnio, SelectedMes);
        if (prev is not null)
            GenerateMonth(SelectedAnio, SelectedMes, prev);
    }

    private void GenerateMonth(int anio, int mes, EstadoPagoMesRow? duplicadoDe)
    {
        string desc = duplicadoDe is not null
            ? $"Duplicar estado de pago a {EstadoPagoFormat.MesesNombres[mes - 1]}"
            : $"Generar estado {EstadoPagoFormat.MesesNombres[mes - 1]} {anio}";
        undo?.PushUndoPoint(session, desc);

        if (workflowCoordinator.GenerateMonth(anio, mes, duplicadoDe, partidas, CanEdit, out var generatedRow))
        {
            if (generatedRow is not null)
            {
                SelectedAnio = generatedRow.Anio;
                SelectedMes = generatedRow.Mes;
            }
            RebuildAll();
        }
    }

    private void GenerateMonth(int mes, EstadoPagoMesRow? duplicadoDe) =>
        GenerateMonth(SelectedAnio, mes, duplicadoDe);

    private void Aprobar()
    {
        undo?.PushUndoPoint(session, $"Aprobar estado {currentRow?.Folio}");
        if (workflowCoordinator.Aprobar(currentRow, CanApprove))
            RebuildAll();
    }

    private void RegistrarPago()
    {
        undo?.PushUndoPoint(session, $"Registrar pago {currentRow?.Folio}");
        if (workflowCoordinator.RegistrarPago(currentRow, CanApprove))
            RebuildAll();
    }

    private void Reabrir()
    {
        undo?.PushUndoPoint(session, $"Reabrir estado {currentRow?.Folio}");
        if (workflowCoordinator.Reabrir(currentRow, CanApprove))
            RebuildAll();
    }

    private void RaiseCommandStates()
    {
        GenerarMesCommand.RaiseCanExecuteChanged();
        GenerarEstadoMesCommand.RaiseCanExecuteChanged();
        DuplicarAnteriorCommand.RaiseCanExecuteChanged();
        AprobarCommand.RaiseCanExecuteChanged();
        RegistrarPagoCommand.RaiseCanExecuteChanged();
        ReabrirCommand.RaiseCanExecuteChanged();
        ExportarExcelCommand.RaiseCanExecuteChanged();
        ExportarPdfCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Exportar exige proyecto abierto, mes generado y permiso de exportación del rol.</summary>
    private bool CanExportEstado() => HasProject && currentRow is not null && permissions.CanExportDocuments;

    // ─── Consultas delegadas al coordinador ───────────────────────────────────

        private EstadoPagoMesRow? FindRow(int anio, int mes) => workflowCoordinator.FindRow(anio, mes);
    private EstadoPagoMesRow? FindRow(int mes) => workflowCoordinator.FindRow(SelectedAnio, mes);
    private EstadoPagoMesRow? PreviousGeneratedRow(int anio, int mes) => workflowCoordinator.PreviousGeneratedRow(anio, mes);

    private List<EstadoPagoMesRow> GeneratedRows() => workflowCoordinator.GeneratedRows();

    private int MesBase() => workflowCoordinator.MesBase(currentRow, SelectedMes);

    private int LastGeneratedMes() => workflowCoordinator.LastGeneratedMes();

    private (int Anio, int Mes)? FirstPendiente() => workflowCoordinator.FirstPendiente();

    private EstadoPagoMesRow? PreviousGeneratedRow(int mes) => workflowCoordinator.PreviousGeneratedRow(mes);
}
