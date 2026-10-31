using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>
/// Pantalla Estados de pago (fase Ejecución): cronograma mensual de cobros con
/// anticipo/retención/IVA según docs/propuesta-estados-pago.html. Los valores se
/// derivan del Itemizado (UF) y se persisten en ProjectFileData.EstadoPagoMeses.
/// </summary>
public sealed partial class EstadosPagoViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    // ── Estado general ────────────────────────────────────────────────────────
    private bool hasProject;
    private bool showEmptyState = true;
    private bool isLoading;
    private bool canEdit;
    private bool canApprove;

    public bool HasProject
    {
        get => hasProject;
        private set => SetProperty(ref hasProject, value);
    }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool CanEdit
    {
        get => canEdit;
        private set => SetProperty(ref canEdit, value);
    }

    public bool CanApprove
    {
        get => canApprove;
        private set => SetProperty(ref canApprove, value);
    }

    private int selectedTabIndex;
    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => SetProperty(ref selectedTabIndex, value);
    }

    private bool mostrarAvanceFisico = true;
    public bool MostrarAvanceFisico
    {
        get => mostrarAvanceFisico;
        set => SetProperty(ref mostrarAvanceFisico, value);
    }

    public string PageSubtitle => MuestraUf
        ? "Cronograma mensual de cobros · valores tomados del Itemizado · moneda UF (referencia CLP al pie)"
        : "Cronograma mensual de cobros · valores tomados del Itemizado · moneda CLP";

    public string ConfigHint { get; } =
        "El recupero de anticipo es proporcional al avance acumulado. La retención garantía se aplica sobre el trabajo del mes. Cambia un valor y el estado se recalcula en vivo.";

    // ── Política de moneda ────────────────────────────────────────────────────
    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (KPIs, calendario,
    /// detalle del mes, totales y resumen se muestran directamente en pesos).
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, Moneda.CLP, StringComparison.OrdinalIgnoreCase);

    private decimal UfRate => session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;

    /// <summary>
    /// Formatea un monto del cronograma (que el motor mantiene en UF) según la moneda del
    /// proyecto: UF/USD ⇒ UF; CLP ⇒ pesos chilenos.
    /// </summary>
    internal string FormatMoney(decimal valor) =>
        MuestraUf ? EstadoPagoFormat.Uf(valor) : EstadoPagoFormat.Clp(valor * UfRate);

    // ── KPIs en texto (moneda del proyecto) ───────────────────────────────────
    public string KpiContratadoText => FormatMoney(KpiContratado);
    public string KpiAcumText => FormatMoney(KpiAcum);
    public string KpiSaldoText => FormatMoney(KpiSaldo);
    public string KpiRetencionText => FormatMoney(KpiRetencion);

    // ── KPIs ──────────────────────────────────────────────────────────────────
    private decimal kpiContratado;
    private decimal kpiAcum;
    private decimal kpiSaldo;
    private decimal kpiRetencion;
    private decimal kpiAvancePct;
    private string kpiPagadosLabel = "0 / 0";

    public decimal KpiContratado { get => kpiContratado; private set => SetProperty(ref kpiContratado, value); }
    public decimal KpiAcum { get => kpiAcum; private set => SetProperty(ref kpiAcum, value); }
    public decimal KpiSaldo { get => kpiSaldo; private set => SetProperty(ref kpiSaldo, value); }
    public decimal KpiRetencion { get => kpiRetencion; private set => SetProperty(ref kpiRetencion, value); }
    public decimal KpiAvancePct { get => kpiAvancePct; private set => SetProperty(ref kpiAvancePct, value); }
    public string KpiPagadosLabel { get => kpiPagadosLabel; private set => SetProperty(ref kpiPagadosLabel, value); }

    // ── Calendario ────────────────────────────────────────────────────────────
    private int selectedAnio = AppClock.Now.Year;
    private int selectedMes = 1;
    private string calendarTitle = "Calendario de estados";

    public int SelectedAnio
    {
        get => selectedAnio;
        set => SetProperty(ref selectedAnio, value);
    }

    public ObservableCollection<EstadoPagoMesCellVm> Meses { get; } = new();

    public string CalendarTitle
    {
        get => calendarTitle;
        private set => SetProperty(ref calendarTitle, value);
    }

    public int SelectedMes
    {
        get => selectedMes;
        private set => SetProperty(ref selectedMes, value);
    }

    // ── Detalle del mes seleccionado ──────────────────────────────────────────
    private bool mesEmptyVisible;
    private bool detailVisible;
    private bool isMesEditable;
    private string mesEmptyTitle = "";
    private string mesEmptyDesc = "";
    private string mesEmptyGenerarLabel = "";
    private string detalleTitulo = "";
    private string detalleSub = "";
    private string badgeLabel = "Pendiente";
    private string badgeKind = "Muted";
    private string tableNoteText = "";

    public bool MesEmptyVisible
    {
        get => mesEmptyVisible;
        private set => SetProperty(ref mesEmptyVisible, value);
    }

    public bool DetailVisible
    {
        get => detailVisible;
        private set => SetProperty(ref detailVisible, value);
    }

    public bool IsMesEditable
    {
        get => isMesEditable;
        private set => SetProperty(ref isMesEditable, value);
    }

    public string MesEmptyTitle
    {
        get => mesEmptyTitle;
        private set => SetProperty(ref mesEmptyTitle, value);
    }

    public string MesEmptyDesc
    {
        get => mesEmptyDesc;
        private set => SetProperty(ref mesEmptyDesc, value);
    }

    public string MesEmptyGenerarLabel
    {
        get => mesEmptyGenerarLabel;
        private set => SetProperty(ref mesEmptyGenerarLabel, value);
    }

    public string DetalleTitulo
    {
        get => detalleTitulo;
        private set => SetProperty(ref detalleTitulo, value);
    }

    public string DetalleSub
    {
        get => detalleSub;
        private set => SetProperty(ref detalleSub, value);
    }

    public string BadgeLabel
    {
        get => badgeLabel;
        private set => SetProperty(ref badgeLabel, value);
    }

    public string BadgeKind
    {
        get => badgeKind;
        private set => SetProperty(ref badgeKind, value);
    }

    public string TableNoteText
    {
        get => tableNoteText;
        private set => SetProperty(ref tableNoteText, value);
    }

    public ObservableCollection<EstadoPagoSeccionVm> Secciones { get; } = new();

    // ── Totales del mes (tarjeta detalle) ─────────────────────────────────────
    private string totTrabajoMesText = "—";
    private string totAcumText = "—";
    private string totRecuperoLabel = "Recupero de anticipo";
    private string totRecuperoText = "—";
    private string totRetencionLabel = "Retención garantía";
    private string totRetencionText = "—";
    private string totNetoText = "—";
    private string totIvaLabel = "IVA";
    private string totIvaText = "—";
    private string totMesLabel = "Total a pagar";
    private string totTotalText = "—";

    public string TotTrabajoMesText { get => totTrabajoMesText; private set => SetProperty(ref totTrabajoMesText, value); }
    public string TotAcumText { get => totAcumText; private set => SetProperty(ref totAcumText, value); }
    public string TotRecuperoLabel { get => totRecuperoLabel; private set => SetProperty(ref totRecuperoLabel, value); }
    public string TotRecuperoText { get => totRecuperoText; private set => SetProperty(ref totRecuperoText, value); }
    public string TotRetencionLabel { get => totRetencionLabel; private set => SetProperty(ref totRetencionLabel, value); }
    public string TotRetencionText { get => totRetencionText; private set => SetProperty(ref totRetencionText, value); }
    public string TotNetoText { get => totNetoText; private set => SetProperty(ref totNetoText, value); }
    public string TotIvaLabel { get => totIvaLabel; private set => SetProperty(ref totIvaLabel, value); }
    public string TotIvaText { get => totIvaText; private set => SetProperty(ref totIvaText, value); }
    public string TotMesLabel { get => totMesLabel; private set => SetProperty(ref totMesLabel, value); }
    public string TotTotalText { get => totTotalText; private set => SetProperty(ref totTotalText, value); }

    // ── Resumen del mes (lateral) ─────────────────────────────────────────────
    private string resumenSub = "—";
    private string resumenBadgeLabel = "Pendiente";
    private string resumenBadgeKind = "Muted";
    private string resTotalText = "—";
    private string resClpText = "Genera el mes para ver el cálculo";
    private string resMesText = "—";
    private string resAcumText = "—";
    private string resPctText = "—";
    private string resReteMesText = "—";
    private string resReteAcumText = "—";
    private string resAntSaldoText = "—";

    public string ResumenSub { get => resumenSub; private set => SetProperty(ref resumenSub, value); }
    public string ResumenBadgeLabel { get => resumenBadgeLabel; private set => SetProperty(ref resumenBadgeLabel, value); }
    public string ResumenBadgeKind { get => resumenBadgeKind; private set => SetProperty(ref resumenBadgeKind, value); }
    public string ResTotalText { get => resTotalText; private set => SetProperty(ref resTotalText, value); }
    public string ResClpText { get => resClpText; private set => SetProperty(ref resClpText, value); }
    public string ResMesText { get => resMesText; private set => SetProperty(ref resMesText, value); }
    public string ResAcumText { get => resAcumText; private set => SetProperty(ref resAcumText, value); }
    public string ResPctText { get => resPctText; private set => SetProperty(ref resPctText, value); }
    public string ResReteMesText { get => resReteMesText; private set => SetProperty(ref resReteMesText, value); }
    public string ResReteAcumText { get => resReteAcumText; private set => SetProperty(ref resReteAcumText, value); }
    public string ResAntSaldoText { get => resAntSaldoText; private set => SetProperty(ref resAntSaldoText, value); }

    // ── Avance del proyecto (lateral) ─────────────────────────────────────────
    private string avPctText = "—";
    private double avFillPct;
    private string avLeftText = "";
    private string avRightText = "";
    private string avEjecText = "—";
    private string avSaldoText = "—";
    private string avMesesLabel = "0 / 0";

    public string AvPctText
    {
        get => avPctText;
        private set => SetProperty(ref avPctText, value);
    }

    public double AvFillPct
    {
        get => avFillPct;
        private set => SetProperty(ref avFillPct, value);
    }

    public string AvLeftText
    {
        get => avLeftText;
        private set => SetProperty(ref avLeftText, value);
    }

    public string AvRightText
    {
        get => avRightText;
        private set => SetProperty(ref avRightText, value);
    }

    public string AvEjecText
    {
        get => avEjecText;
        private set => SetProperty(ref avEjecText, value);
    }

    public string AvSaldoText
    {
        get => avSaldoText;
        private set => SetProperty(ref avSaldoText, value);
    }

    public string AvMesesLabel
    {
        get => avMesesLabel;
        private set => SetProperty(ref avMesesLabel, value);
    }

    // ── Configuración ─────────────────────────────────────────────────────────
    private int configAnio;
    private int configCantMeses = 12;
    private decimal configAnticipoPct = 10m;
    private decimal configRetencionPct = 5m;
    private decimal configIvaPct = 19m;

    public sealed record MesOption(int Numero, string Nombre);
    public IReadOnlyList<MesOption> OpcionesMesesInicio { get; } = new List<MesOption>
    {
        new(1, "1 · Enero"),
        new(2, "2 · Febrero"),
        new(3, "3 · Marzo"),
        new(4, "4 · Abril"),
        new(5, "5 · Mayo"),
        new(6, "6 · Junio"),
        new(7, "7 · Julio"),
        new(8, "8 · Agosto"),
        new(9, "9 · Septiembre"),
        new(10, "10 · Octubre"),
        new(11, "11 · Noviembre"),
        new(12, "12 · Diciembre"),
    };

    public string ConfigPeriodoObraTexto
    {
        get
        {
            var cfg = session?.EstadoPagoConfig;
            if (cfg is null) return "No configurado";
            int mInicio = Math.Clamp(cfg.MesInicio, 1, 12);
            var (anioFin, mesFin) = EstadoPagoCalculator.GetMonthForPeriod(cfg, Math.Max(0, cfg.CantMeses - 1));
            string inicio = $"{EstadoPagoFormat.MesesNombres[mInicio - 1]} {cfg.Anio}";
            string fin = $"{EstadoPagoFormat.MesesNombres[mesFin - 1]} {anioFin}";
            return $"{inicio}  →  {fin}  ({cfg.CantMeses} meses)";
        }
    }

    private int configMesInicio = 1;
    public int ConfigMesInicio
    {
        get => configMesInicio;
        set
        {
            int v = Math.Clamp(value, 1, 12);
            if (!SetProperty(ref configMesInicio, v)) return;
            OnPropertyChanged(nameof(ConfigPeriodoObraTexto));
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar mes de inicio de obra");
            session.EstadoPagoConfig.MesInicio = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }

    public int ConfigAnio
    {
        get => configAnio;
        set
        {
            int v = value;
            if (v < 2000) v = 2000;
            if (v > 2100) v = 2100;
            if (!SetProperty(ref configAnio, v)) return;
            OnPropertyChanged(nameof(ConfigPeriodoObraTexto));
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar año base de obra");
            session.EstadoPagoConfig.Anio = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }

    public int ConfigCantMeses
    {
        get => configCantMeses;
        set
        {
            int v = value;
            if (v < 1) v = 1;
            if (v > 48) v = 48;
            if (!SetProperty(ref configCantMeses, v)) return;
            OnPropertyChanged(nameof(ConfigPeriodoObraTexto));
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar plazo en meses de obra");
            session.EstadoPagoConfig.CantMeses = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }

    public decimal ConfigAnticipoPct
    {
        get => configAnticipoPct;
        set
        {
            decimal v = value;
            if (v < 0m) v = 0m;
            if (v > 100m) v = 100m;
            if (!SetProperty(ref configAnticipoPct, v)) return;
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar porcentaje de anticipo");
            session.EstadoPagoConfig.AnticipoPct = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }

    public decimal ConfigRetencionPct
    {
        get => configRetencionPct;
        set
        {
            decimal v = value;
            if (v < 0m) v = 0m;
            if (v > 100m) v = 100m;
            if (!SetProperty(ref configRetencionPct, v)) return;
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar porcentaje de retención");
            session.EstadoPagoConfig.RetencionPct = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }

    public decimal ConfigIvaPct
    {
        get => configIvaPct;
        set
        {
            decimal v = value;
            if (v < 0m) v = 0m;
            if (v > 100m) v = 100m;
            if (!SetProperty(ref configIvaPct, v)) return;
            if (!CanEdit) return;
            undo?.PushUndoPoint(session, "Modificar porcentaje de IVA");
            session.EstadoPagoConfig.IvaPct = v;
            session.MarkDirty(ProjectDirtyScope.EstadosPago);
            RebuildAll();
        }
    }
}
