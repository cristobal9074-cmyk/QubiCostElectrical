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

    public EstadosPagoViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        IPermissionService permissions,
        ItemizadoPresenter presenter,
        IItemizadoWorkspaceService itemizadoWorkspace,
        EstadosPagoExportCoordinator exportCoordinator)
        : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;
        this.presenter = presenter;
        this.itemizadoWorkspace = itemizadoWorkspace;
        this.exportCoordinator = exportCoordinator;

        SelectMesCommand = new RelayCommand<EstadoPagoMesCellVm>(SelectMes);
        GenerarMesCommand = new RelayCommand(GenerarMes, () => CanEdit && HasProject && hayPendienteGenerable);
        GenerarEstadoMesCommand = new RelayCommand(
            GenerarEstadoMes,
            () => CanEdit && HasProject && currentRow is null && SelectedMes <= session.EstadoPagoConfig.CantMeses);
        DuplicarAnteriorCommand = new RelayCommand(
            DuplicarAnterior,
            () => CanEdit && HasProject && currentRow is null
                && SelectedMes <= session.EstadoPagoConfig.CantMeses
                && PreviousGeneratedRow(SelectedMes) is not null);
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
        // Autosave/guardado: nada estructural cambió; tampoco debe pisar la edición.
        if (string.Equals(e.Scope, "saved", StringComparison.OrdinalIgnoreCase))
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
                Secciones.Clear();
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
                var partida = new EstadoPagoPartidaInput(row.Id, row.Item, row.Descripcion, row.TotalUf);
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
        SetBacking(ref configCantMeses, cfg.CantMeses, nameof(ConfigCantMeses));
        SetBacking(ref configAnticipoPct, cfg.AnticipoPct, nameof(ConfigAnticipoPct));
        SetBacking(ref configRetencionPct, cfg.RetencionPct, nameof(ConfigRetencionPct));
        SetBacking(ref configIvaPct, cfg.IvaPct, nameof(ConfigIvaPct));
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
        currentRow = FindRow(SelectedMes);
        if (currentRow is not null)
            return;

        int pendiente = FirstPendiente();
        if (pendiente > 0)
            SelectedMes = pendiente;
        else
        {
            int ultimo = LastGeneratedMes();
            if (ultimo > 0)
                SelectedMes = ultimo;
        }
        currentRow = FindRow(SelectedMes);
    }

    // ─── Reconstrucción ───────────────────────────────────────────────────────

    private void RebuildAll()
    {
        if (!HasProject)
            return;

        var cfg = session.EstadoPagoConfig;
        CalendarTitle = $"Calendario de estados · {cfg.Anio}";
        currentRow = FindRow(SelectedMes);
        RecomputeMonthTotals();
        RebuildCells();
        RebuildKpis();
        RebuildDetail();
        RebuildAside();
        hayPendienteGenerable = FirstPendiente() > 0;
        RaiseCommandStates();
    }

    private void RecomputeMonthTotals()
    {
        totalesPorMes.Clear();
        if (!HasProject)
            return;

        var cfg = session.EstadoPagoConfig;
        foreach (var row in GeneratedRows())
        {
            var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, row.Mes - 1);
            var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, row.AvancesPorItem, cfg);
            totalesPorMes[row.Mes] = detalle.Totales;
        }
    }

    private void RebuildCells()
    {
        Meses.Clear();
        var cfg = session.EstadoPagoConfig;
        var rows = GeneratedRows()
            .GroupBy(r => r.Mes)
            .ToDictionary(g => g.Key, g => g.Last());

        for (int m = 1; m <= 12; m++)
        {
            rows.TryGetValue(m, out var row);
            bool generado = row is not null;
            string estado = row?.Estado ?? "pendiente";
            var (label, _) = EstadoPagoFormat.EstadoBadge(estado);
            bool generable = !generado && m <= cfg.CantMeses;

            var cell = new EstadoPagoMesCellVm(
                m, EstadoPagoFormat.MesesCortos[m - 1], EstadoPagoFormat.MesesNombres[m - 1], estado, label, generado, generable)
            {
                IsSelected = m == SelectedMes
            };

            if (generado && totalesPorMes.TryGetValue(m, out var totales))
                cell.MontoLabel = EstadoPagoFormat.Uf(totales.Total);

            Meses.Add(cell);
        }
    }

    private void RebuildKpis()
    {
        var cfg = session.EstadoPagoConfig;
        int mesBase = MesBase();
        var acumBase = mesBase > 0
            ? EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, mesBase)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var kpis = EstadoPagoCalculator.BuildKpis(partidas, acumBase, session.EstadoPagoMeses, cfg);
        KpiContratado = kpis.TotalContratado;
        KpiPagadosLabel = $"{kpis.Pagados} / {kpis.CantMeses}";
        KpiAcum = kpis.Acumulado;
        KpiSaldo = kpis.Saldo;
        KpiRetencion = kpis.RetencionAcum;
        KpiAvancePct = kpis.AvanceFisicoPct;
    }

    private void RebuildDetail()
    {
        Secciones.Clear();
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
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, SelectedMes - 1);
        var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, currentRow.AvancesPorItem, cfg);

        DetalleTitulo = $"Estado {currentRow.Folio} · {EstadoPagoFormat.MesesNombres[SelectedMes - 1]} {cfg.Anio}";
        DetalleSub = $"Emitido {currentRow.FechaEmision:dd/MM/yyyy} · Valores en UF · {partidas.Count} partidas del Itemizado";
        var (badgeLabel, badgeKind) = EstadoPagoFormat.EstadoBadge(currentRow.Estado);
        BadgeLabel = badgeLabel;
        BadgeKind = badgeKind;
        TableNoteText = IsMesEditable
            ? "Modo edición: escribe el % de avance de cada partida del mes (Enter/Tab/↓ avanzan de fila, ↑ retrocede; el valor se confirma al salir de la celda). Acumulado, retención, anticipo e IVA se recalculan en vivo."
            : "Estado cerrado: los % de avance quedan registrados y alimentan el acumulado de los meses siguientes.";

        var porId = detalle.Filas.ToDictionary(f => f.ItemId, StringComparer.OrdinalIgnoreCase);
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
        int mesBase = MesBase();

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

        var rowBase = FindRow(mesBase);
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, mesBase - 1);
        var detalleBase = EstadoPagoCalculator.CalcularMes(
            partidas, acumAntes, rowBase?.AvancesPorItem ?? new Dictionary<string, decimal>(), cfg);
        var t = detalleBase.Totales;

        ResumenSub = rowBase is not null
            ? $"{rowBase.Folio} · {EstadoPagoFormat.MesesNombres[mesBase - 1]} {cfg.Anio}"
            : $"{EstadoPagoFormat.MesesNombres[mesBase - 1]} de {cfg.Anio}";
        var (label, kind) = EstadoPagoFormat.EstadoBadge(rowBase?.Estado ?? "pendiente");
        ResumenBadgeLabel = label;
        ResumenBadgeKind = kind;
        ResTotalText = EstadoPagoFormat.Uf(t.Total);

        decimal ufRate = session.ProjectInfo.ValorUfClp > 0m
            ? session.ProjectInfo.ValorUfClp
            : PriceConversion.DefaultValorUfClp;
        ResClpText = $"≈ $ {Math.Round(t.Total * ufRate):N0} CLP";
        ResMesText = EstadoPagoFormat.Uf(t.TrabajoMes);
        ResAcumText = EstadoPagoFormat.Uf(t.Acum);
        ResPctText = EstadoPagoFormat.Pct(t.PctAcum);
        ResReteMesText = EstadoPagoFormat.Uf(t.RetencionMes);
        ResReteAcumText = EstadoPagoFormat.Uf(t.RetencionAcum);
        ResAntSaldoText = EstadoPagoFormat.Uf(t.AnticipoSaldo);
        ApplyAvanceTexts();
    }

    private void ApplyAvanceTexts()
    {
        AvPctText = EstadoPagoFormat.Pct(KpiAvancePct);
        double fill = (double)KpiAvancePct;
        if (fill < 0d) fill = 0d;
        if (fill > 100d) fill = 100d;
        AvFillPct = fill;
        AvLeftText = $"Ejecutado {EstadoPagoFormat.Uf(KpiAcum)}";
        AvRightText = $"Total {EstadoPagoFormat.Uf(KpiContratado)}";
        AvEjecText = EstadoPagoFormat.Uf(KpiAcum);
        AvSaldoText = EstadoPagoFormat.Uf(KpiSaldo);
        AvMesesLabel = KpiPagadosLabel;
    }

    private void ApplyTotalesTexts(EstadoPagoMesCalc t, EstadoPagoConfig cfg)
    {
        TotTrabajoMesText = EstadoPagoFormat.Uf(t.TrabajoMes);
        TotAcumText = EstadoPagoFormat.Uf(t.Acum);
        TotRecuperoLabel = $"Recupero de anticipo ({EstadoPagoFormat.PctConfig(cfg.AnticipoPct)})";
        TotRecuperoText = EstadoPagoFormat.Uf(t.RecuperoMes);
        TotRetencionLabel = $"Retención garantía ({EstadoPagoFormat.PctConfig(cfg.RetencionPct)})";
        TotRetencionText = EstadoPagoFormat.Uf(t.RetencionMes);
        TotNetoText = EstadoPagoFormat.Uf(t.Neto);
        TotIvaLabel = $"IVA ({EstadoPagoFormat.PctConfig(cfg.IvaPct)})";
        TotIvaText = EstadoPagoFormat.Uf(t.Iva);
        TotMesLabel = $"Total a pagar · {EstadoPagoFormat.MesesCortos[SelectedMes - 1].ToLowerInvariant()}";
        TotTotalText = EstadoPagoFormat.Uf(t.Total);
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
        if (!CanEdit || currentRow is null)
            return;

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
        var acum = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, SelectedMes);
        acum.TryGetValue(itemId, out decimal running);

        foreach (var row in GeneratedRows())
        {
            if (row.Mes <= SelectedMes)
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
                cell.MontoLabel = EstadoPagoFormat.Uf(totales.Total);
        }

        if (currentRow is not null && totalesPorMes.TryGetValue(SelectedMes, out var t))
            ApplyTotalesTexts(t, cfg);

        RebuildKpis();
        RebuildAside();
    }

    // ─── Comandos ─────────────────────────────────────────────────────────────

    private void SelectMes(EstadoPagoMesCellVm? cell)
    {
        if (cell is null || cell.Mes == SelectedMes || !HasProject)
            return;
        SelectedMes = cell.Mes;
        RebuildAll();
    }

    private void GenerarMes()
    {
        int mes = FirstPendiente();
        if (mes > 0)
            GenerateMonth(mes, duplicadoDe: null);
    }

    private void GenerarEstadoMes() => GenerateMonth(SelectedMes, duplicadoDe: null);

    private void DuplicarAnterior()
    {
        var prev = PreviousGeneratedRow(SelectedMes);
        if (prev is not null)
            GenerateMonth(SelectedMes, prev);
    }

    private void GenerateMonth(int mes, EstadoPagoMesRow? duplicadoDe)
    {
        if (!CanEdit || mes < 1 || mes > 12)
            return;

        var cfg = session.EstadoPagoConfig;
        if (mes > cfg.CantMeses)
        {
            WpfDialogService.Warning(
                "Estados de pago",
                $"La configuración del año tiene {cfg.CantMeses} meses: no se puede generar el estado de {EstadoPagoFormat.MesesNombres[mes - 1].ToLowerInvariant()}.");
            return;
        }

        if (FindRow(mes) is not null)
            return;

        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, mes - 1);
        Dictionary<string, decimal> avances = duplicadoDe is null
            ? partidas.ToDictionary(p => p.Id, _ => 0m, StringComparer.OrdinalIgnoreCase)
            : EstadoPagoCalculator.ClampedCopy(duplicadoDe, acumAntes);

        var row = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(cfg.Anio, mes),
            Anio = cfg.Anio,
            Mes = mes,
            Estado = EstadoPagoEst.Borrador,
            Folio = EstadoPagoMesRow.BuildFolio(cfg.Anio, mes),
            FechaEmision = AppClock.Now,
            AvancesPorItem = avances
        };

        session.EstadoPagoMeses.Add(row);
        session.EstadoPagoMeses.Sort((a, b) => a.Anio != b.Anio
            ? a.Anio.CompareTo(b.Anio)
            : a.Mes.CompareTo(b.Mes));
        session.MarkDirty(ProjectDirtyScope.EstadosPago);

        SelectedMes = mes;
        RebuildAll();
    }

    private void Aprobar()
    {
        if (!CanApprove || currentRow is null || currentRow.Estado != EstadoPagoEst.Borrador)
            return;
        currentRow.Estado = EstadoPagoEst.Revision;
        currentRow.AprobadoPor = session.ProjectInfo.UsuarioNombre;
        currentRow.FechaAprobacion = AppClock.Now;
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Estado aprobado",
            $"El estado {currentRow.Folio} pasó a Revisión. Un administrador puede registrarlo como pagado.");
        RebuildAll();
    }

    private void RegistrarPago()
    {
        if (!CanApprove || currentRow is null || currentRow.Estado != EstadoPagoEst.Revision)
            return;
        currentRow.Estado = EstadoPagoEst.Pago;
        if (string.IsNullOrWhiteSpace(currentRow.AprobadoPor))
        {
            currentRow.AprobadoPor = session.ProjectInfo.UsuarioNombre;
            currentRow.FechaAprobacion = AppClock.Now;
        }
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Pago registrado",
            $"El estado {currentRow.Folio} quedó registrado como Pagado.");
        RebuildAll();
    }

    private void Reabrir()
    {
        if (!CanApprove || currentRow is null || currentRow.Estado == EstadoPagoEst.Borrador)
            return;
        currentRow.Estado = EstadoPagoEst.Borrador;
        currentRow.AprobadoPor = "";
        currentRow.FechaAprobacion = null;
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Estado reabierto",
            $"El estado {currentRow.Folio} volvió a Borrador y puede editarse.");
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

    // ─── Consultas ────────────────────────────────────────────────────────────

    private EstadoPagoMesRow? FindRow(int mes)
    {
        int anio = session.EstadoPagoConfig.Anio;
        return session.EstadoPagoMeses.FirstOrDefault(r =>
            r is not null && r.Anio == anio && r.Mes == mes);
    }

    private List<EstadoPagoMesRow> GeneratedRows()
    {
        int anio = session.EstadoPagoConfig.Anio;
        return session.EstadoPagoMeses
            .Where(r => r is not null && r.Anio == anio && r.Mes >= 1)
            .OrderBy(r => r.Mes)
            .ToList();
    }

    /// <summary>Mes base para KPIs/resumen: el seleccionado si existe, si no el último con estado.</summary>
    private int MesBase()
    {
        if (currentRow is not null)
            return SelectedMes;
        return LastGeneratedMes();
    }

    private int LastGeneratedMes()
    {
        var rows = GeneratedRows();
        return rows.Count > 0 ? rows[rows.Count - 1].Mes : 0;
    }

    private int FirstPendiente()
    {
        int cant = session.EstadoPagoConfig.CantMeses;
        for (int m = 1; m <= cant && m <= 12; m++)
        {
            if (FindRow(m) is null)
                return m;
        }
        return 0;
    }

    private EstadoPagoMesRow? PreviousGeneratedRow(int mes)
    {
        return GeneratedRows()
            .Where(r => r.Mes < mes)
            .LastOrDefault();
    }
}
