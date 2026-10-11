using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Sección "M.O. instalación" de la pantalla Costo: modo por rendimiento (Tarifa D/H ÷
/// rendimiento del material) vs M.O. directa del catálogo, tarifa editable y derivación
/// desde el plan de M.O. Vive fuera de CostoViewModel por el gate de MaxLineCount.
/// </summary>
public sealed class MoInstalacionSectionVm : ViewModelBase
{
    private readonly BudgetProjectSession session;
    private readonly Func<bool> canEdit;
    private readonly Func<IReadOnlyList<MoDirectaRowVm>> moDirectaRows;
    private readonly Action<Action> runSuppressed;
    private readonly Action refreshCost;
    private bool modoMoRendimiento;
    private string tarifaDhClpText = "";
    private bool tarifaDhConLeyes;
    private string indiceMoFaText = "";
    private string hint = "";

    public MoInstalacionSectionVm(
        BudgetProjectSession session,
        Func<bool> canEdit,
        Func<IReadOnlyList<MoDirectaRowVm>> moDirectaRows,
        Action<Action> runSuppressed,
        Action refreshCost)
    {
        this.session = session;
        this.canEdit = canEdit;
        this.moDirectaRows = moDirectaRows;
        this.runSuppressed = runSuppressed;
        this.refreshCost = refreshCost;
        DerivarTarifaDhCommand = new RelayCommand(DerivarTarifaDh, () => canEdit());
    }

    public RelayCommand DerivarTarifaDhCommand { get; }

    /// <summary>M.O. de instalación por rendimiento (true) o directa del catálogo (false).</summary>
    public bool ModoMoRendimiento
    {
        get => modoMoRendimiento;
        set
        {
            if (!SetProperty(ref modoMoRendimiento, value) || !canEdit())
                return;
            Persist(() => session.ProjectInfo.ModoMoInstalacion = value
                ? MaterialMoResolver.ModoRendimiento
                : MaterialMoResolver.ModoDirecto);
        }
    }

    /// <summary>Tarifa por día-hombre en CLP (texto localizado, se formatea al cargar).</summary>
    public string TarifaDhClpText
    {
        get => tarifaDhClpText;
        set
        {
            if (!SetProperty(ref tarifaDhClpText, value ?? ""))
                return;
            if (!canEdit() || !TryParseClp(value, out var clp))
                return;
            Persist(() => session.ProjectInfo.TarifaDhClp = clp);
        }
    }

    /// <summary>Al derivar la tarifa desde el plan MO, incluir leyes sociales sobre el sueldo.</summary>
    public bool TarifaDhConLeyes
    {
        get => tarifaDhConLeyes;
        set
        {
            if (!SetProperty(ref tarifaDhConLeyes, value) || !canEdit())
                return;
            Persist(() => session.ProjectInfo.TarifaDhConLeyes = value);
        }
    }

    /// <summary>
    /// Índice global de M.O. "FA" (paridad COSTOS!E192 = 62,80). Solo multiplica dentro de la
    /// fórmula "M.O. por %" (Material × % × FA); las M.O. fijas y el modo rendimiento no cambian.
    /// </summary>
    public string IndiceMoFaText
    {
        get => indiceMoFaText;
        set
        {
            if (!SetProperty(ref indiceMoFaText, value ?? ""))
                return;
            if (!canEdit() || !TryParseClp(value, out var fa))
                return;
            if (fa <= 0m)
                return;
            Persist(() => session.ProjectInfo.IndiceMoFa = fa);
        }
    }

    public string MoInstalacionHint => hint;

    /// <summary>Carga el estado desde el proyecto (inicializa el modo desde la preferencia global si está vacío).</summary>
    public void Load(ProjectInfoRow project)
    {
        if (string.IsNullOrEmpty(project.ModoMoInstalacion))
        {
            var prefs = UserPreferencesStore.Load();
            project.ModoMoInstalacion = prefs.MoInstalacionPorRendimientoDefault
                ? MaterialMoResolver.ModoRendimiento
                : MaterialMoResolver.ModoDirecto;
        }

        modoMoRendimiento = string.Equals(
            project.ModoMoInstalacion, MaterialMoResolver.ModoRendimiento, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(ModoMoRendimiento));
        tarifaDhClpText = project.TarifaDhClp > 0m
            ? project.TarifaDhClp.ToString("N0", CultureInfo.CurrentCulture)
            : "";
        OnPropertyChanged(nameof(TarifaDhClpText));
        tarifaDhConLeyes = project.TarifaDhConLeyes;
        OnPropertyChanged(nameof(TarifaDhConLeyes));
        indiceMoFaText = project.IndiceMoFa > 0m
            ? project.IndiceMoFa.ToString("N2", CultureInfo.CurrentCulture)
            : "";
        OnPropertyChanged(nameof(IndiceMoFaText));
        UpdateHint();
    }

    /// <summary>
    /// Deriva la tarifa D/H desde el plan de M.O. directa: promedio de los cargos maestros
    /// (o el mayor sueldo si no hay maestros) ÷ 22 jornadas, con leyes sociales opcionales.
    /// </summary>
    private void DerivarTarifaDh()
    {
        if (!canEdit())
            return;
        var conSueldo = moDirectaRows().Where(r => r.SueldoMensualClp > 0m).ToList();
        if (conSueldo.Count == 0)
            return;
        var maestros = conSueldo
            .Where(r => r.Cargo.Contains("MAESTR", StringComparison.OrdinalIgnoreCase))
            .ToList();
        decimal sueldo = maestros.Count > 0
            ? maestros.Average(r => r.SueldoMensualClp)
            : conSueldo.Max(r => r.SueldoMensualClp);
        decimal tarifa = MaterialMoResolver.DerivarTarifaDh(
            sueldo, session.ProjectInfo.LeyesSocialesMoPct, session.ProjectInfo.TarifaDhConLeyes);
        if (tarifa <= 0m)
            return;
        TarifaDhClpText = tarifa.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>Escribe el campo en ProjectInfo, marca dirty, reempuja MO a cubicación y refresca costo.</summary>
    private void Persist(Action write)
    {
        runSuppressed(() =>
        {
            write();
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            int synced = session.SyncCubicacionPricesFromMateriales();
            if (synced > 0)
                session.MarkDirty(ProjectDirtyScope.Cubicacion);
            refreshCost();
        });
        UpdateHint();
    }

    private void UpdateHint()
    {
        int conPorcentaje = session.Materiales.Count(m => MaterialMoResolver.DebeUsarPorcentaje(m));
        string porcentajeSuffix = conPorcentaje > 0
            ? " · " + conPorcentaje + " materiales con M.O. por % (Precio × % × FA)"
            : "";
        if (!modoMoRendimiento)
        {
            hint = "M.O. directa del catálogo (comportamiento histórico)." + porcentajeSuffix;
        }
        else
        {
            int conRend = session.Materiales.Count(m => m.RendimientoDh > 0m);
            hint = (conRend == 0
                ? "Sin rendimientos en el catálogo: se usa M.O. directa."
                : conRend + " materiales con rendimiento · " + (session.ProjectInfo.TarifaDhClp > 0m
                    ? session.ProjectInfo.TarifaDhClp.ToString("N0", CultureInfo.CurrentCulture) + " CLP/D-H"
                    : "tarifa sin definir (fallback a directo)")) + porcentajeSuffix;
        }
        OnPropertyChanged(nameof(MoInstalacionHint));
    }

    private static bool TryParseClp(string? text, out decimal clp)
    {
        clp = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var limpio = text.Trim().Replace(" ", "").Replace("$", "");
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.CurrentCulture, out clp)
            || decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out clp);
    }
}
