using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public sealed partial class ManoDeObraViewModel : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    private static readonly Dictionary<string, decimal> DefaultSueldos = new()
    {
        ["M1"] = 750000m,
        ["AA"] = 650000m,
        ["AD"] = 1237500m,
        ["SV"] = 1466667m,
        ["CA"] = 916667m,
        ["OT"] = 779167m,
        ["BG"] = 595833m,
        ["PR"] = 641667m,
    };

    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly WpfUndoService? undo;
    private bool showEmptyState = true;
    private bool canEdit;
    private bool isLoading;
    private bool hasLoaded;
    private bool hasRecalculated;
    private bool suppressSalaryLoop;
    private int totalMeses = 12;
    private decimal lastTotalMoClp;
    private decimal lastTotalDirClp;
    private decimal valorUf = PriceConversion.DefaultValorUfClp;
    private decimal leyesSocialesPct = 35m;
    private string curvePreset = "bell";
    private int peakHeadcount; // Proyecto limpio: el usuario define la dotación
    private bool viewInUf;
    private readonly Dictionary<string, int> cellOverrides = new();

    private string kpiTotalMo = "$ 0";
    private string kpiTotalMoSub = "";
    private string kpiBase = "$ 0";
    private string kpiBasePct = "0% del total MO";
    private string kpiLeyes = "$ 0";
    private string kpiLeyesPct = "0% del total MO";
    private string kpiDirecta = "$ 0";
    private string kpiDirectaSub = "";
    private string kpiDirectaPct = "0% del total MO";
    private string kpiIndirecta = "$ 0";
    private string kpiIndirectaSub = "";
    private string kpiIndirectaPct = "0% del total MO";
    private string kpiBaseTrend = "";
    private string kpiLeyesTrend = "";
    private string exDirPico = "";
    private string exDirSueldoProm = "";
    private string exDirLeyVal = "";
    private string exIndDur = "";
    private string exIndRoles = "";
    private string exIndSueldoProm = "";
    private string peakLabel = "M4 - M8";
    private string totalBarLabel = "";
    private string totalBarSub = "";
    private string totalBarValue = "$ 0";
    private string totalBarUf = "";
    private string sumBase = "$ 0";
    private string sumLeyes = "$ 0";
    private string sumDirecta = "$ 0";
    private string sumIndirecta = "$ 0";
    private string sumPromMes = "$ 0";
    private string sumRight = "";
    private string footDir = "";
    private string footInd = "";
    private string costHint = "CLP · incluye 35% leyes";
    private string chipMeses = "12 meses";
    private string chipUf = "$ 37.998";

    private string Fmt(decimal n)
    {
        if (viewInUf)
            return (n / valorUf).ToString("N3", CultureInfo.GetCultureInfo("es-CL")) + " UF";
        return "$ " + Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));
    }

    private static string FmtClp(decimal n)
        => "$ " + Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));

    private string FmtUf(decimal n, decimal vUf)
        => (n / vUf).ToString("N3", CultureInfo.GetCultureInfo("es-CL")) + " UF";

    private readonly ManoDeObraProjectionCoordinator _projectionCoordinator;

    public MoProjectionTaskVm[] ProjectionTasks => _projectionCoordinator.ProjectionTasks;
    public bool IsApplyingProjection => _projectionCoordinator.IsApplyingProjection;

    internal BudgetProjectSession Session => session;
    internal IProjectWorkspaceService Workspace => workspace;
    internal Dictionary<string, int> CellOverrides => cellOverrides;
    internal int InternalPeakHeadcount { get => peakHeadcount; set => peakHeadcount = value; }
    internal void TriggerPropertyChanged(string name) => OnPropertyChanged(name);
    internal void TriggerRecalcular() => Recalcular();
    internal void TriggerPersistToSession() => PersistToSession();

    public void FitProjectionMonths() => _projectionCoordinator.FitProjectionMonths();
    public void AddProjectionTask(string? name = null) => _projectionCoordinator.AddProjectionTask(name);
    public void RemoveProjectionTask(string id) => _projectionCoordinator.RemoveProjectionTask(id);
    public void RenameProjectionTask(string id, string name) => _projectionCoordinator.RenameProjectionTask(id, name);
    public bool SetProjectionQuantity(string id, int monthIndex, int value) => _projectionCoordinator.SetProjectionQuantity(id, monthIndex, value);
    public void FlushProjectionEdits() => _projectionCoordinator.FlushProjectionEdits();
    public int ProjectionMonthTotal(int monthIndex) => _projectionCoordinator.ProjectionMonthTotal(monthIndex);
    public double ProjectionMonthlyAverage() => _projectionCoordinator.ProjectionMonthlyAverage();
    public int ProjectionMonthlyAverageRounded() => _projectionCoordinator.ProjectionMonthlyAverageRounded();
    public bool ProjectionMonthIsPeak(int monthIndex) => _projectionCoordinator.ProjectionMonthIsPeak(monthIndex);

    public MoCargoDef[] CargosDef { get; private set; } = Array.Empty<MoCargoDef>();
    public MoMesInfo[] Meses { get; private set; } = Array.Empty<MoMesInfo>();
    public MoTableRow[] DotacionRows { get; private set; } = Array.Empty<MoTableRow>();
    public MoCostRow[] CostRows { get; private set; } = Array.Empty<MoCostRow>();
    public ObservableCollection<MoCargoEditorVm> DirectaEditors { get; } = new();
    public ObservableCollection<MoCargoEditorVm> IndirectaEditors { get; } = new();
    public ObservableCollection<MoChartBarVm> ChartBars { get; } = new();
    public int TotalMonths => totalMeses;

    public ManoDeObraViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.UndoService) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ManoDeObraViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, WpfUndoService? undo = null)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.undo = undo;
        _projectionCoordinator = new ManoDeObraProjectionCoordinator(this);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ApplyCommand = new RelayCommand(ExecuteApply, () => CanEdit);
        ResetParamsCommand = new RelayCommand(ExecuteResetParams, () => CanEdit);
        ResetSueldosCommand = new RelayCommand(ExecuteResetSueldos, () => CanEdit);
        TakeProjectionCommand = new RelayCommand(ExecuteTakeProjection, () => CanEdit);
        ApplyAverageProjectionCommand = new RelayCommand(ExecuteApplyAverageProjection, () => CanEdit);
        AddDirectaCargoCommand = new RelayCommand(ExecuteAddDirectaCargo, () => CanEdit);
        AddIndirectaCargoCommand = new RelayCommand(ExecuteAddIndirectaCargo, () => CanEdit);
        SetCurrencyClpCommand = new RelayCommand(() => SetCurrency(false));
        SetCurrencyUfCommand = new RelayCommand(() => SetCurrency(true));
    }

    public ICommand OpenProjectCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ResetParamsCommand { get; }
    public ICommand ResetSueldosCommand { get; }
    public ICommand TakeProjectionCommand { get; }
    public ICommand ApplyAverageProjectionCommand { get; }
    public ICommand AddDirectaCargoCommand { get; }
    public ICommand AddIndirectaCargoCommand { get; }
    public ICommand SetCurrencyClpCommand { get; }
    public ICommand SetCurrencyUfCommand { get; }

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

    public bool CanEdit
    {
        get => canEdit;
        private set
        {
            if (SetProperty(ref canEdit, value))
            {
                (ApplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ResetParamsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ResetSueldosCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (TakeProjectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ApplyAverageProjectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddDirectaCargoCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddIndirectaCargoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal ValorUF { get => valorUf; private set => SetProperty(ref valorUf, value); }

    public int TotalMeses
    {
        get => totalMeses;
        set
        {
            value = Math.Clamp(value, 1, 120);
            if (SetProperty(ref totalMeses, value))
            {
                FitProjectionMonths();
                Recalcular();
                OnPropertyChanged(nameof(TotalMonths));
                PersistToSession();
            }
        }
    }

    public decimal LeyesSocialesPct
    {
        get => leyesSocialesPct;
        set
        {
            value = Math.Clamp(value, 0m, 100m);
            if (SetProperty(ref leyesSocialesPct, value))
            {
                Recalcular();
                PersistToSession();
            }
        }
    }

    public string CurvePreset
    {
        get => curvePreset;
        set
        {
            value = value == "flat" ? "flat" : "bell";
            if (SetProperty(ref curvePreset, value))
                Recalcular();
        }
    }

    public int PeakHeadcount
    {
        get => peakHeadcount;
        set
        {
            value = Math.Clamp(value, 0, 500);
            if (SetProperty(ref peakHeadcount, value))
            {
                foreach (var c in CargosDef.Where(x => x.EsDirecta))
                    c.DotPico = value;
                Recalcular();
                PersistToSession();
            }
        }
    }

    public bool ViewInUf
    {
        get => viewInUf;
        set
        {
            // Regla de moneda: si el proyecto está en CLP no se permite ver UF.
            if (value && !MuestraUf)
                value = false;
            if (SetProperty(ref viewInUf, value))
            {
                OnPropertyChanged(nameof(IsClp));
                OnPropertyChanged(nameof(IsUf));
                Recalcular();
            }
        }
    }

    public bool IsClp => !viewInUf;
    public bool IsUf => viewInUf;

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone UF (ni el conmutador CLP/UF, ni el
    /// indicador "VALOR UF"). Con UF/USD se mantiene la equivalencia en pesos.
    /// </summary>
    public bool MuestraUf
        => !string.Equals(session.ProjectInfo.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);

    public string KpiTotalMo { get => kpiTotalMo; private set => SetProperty(ref kpiTotalMo, value); }
    public string KpiTotalMoSub { get => kpiTotalMoSub; private set => SetProperty(ref kpiTotalMoSub, value); }
    public string KpiBase { get => kpiBase; private set => SetProperty(ref kpiBase, value); }
    public string KpiBasePct { get => kpiBasePct; private set => SetProperty(ref kpiBasePct, value); }
    public string KpiLeyes { get => kpiLeyes; private set => SetProperty(ref kpiLeyes, value); }
    public string KpiLeyesPct { get => kpiLeyesPct; private set => SetProperty(ref kpiLeyesPct, value); }
    public string KpiDirecta { get => kpiDirecta; private set => SetProperty(ref kpiDirecta, value); }
    public string KpiDirectaSub { get => kpiDirectaSub; private set => SetProperty(ref kpiDirectaSub, value); }
    public string KpiDirectaPct { get => kpiDirectaPct; private set => SetProperty(ref kpiDirectaPct, value); }
    public string KpiIndirecta { get => kpiIndirecta; private set => SetProperty(ref kpiIndirecta, value); }
    public string KpiIndirectaSub { get => kpiIndirectaSub; private set => SetProperty(ref kpiIndirectaSub, value); }
    public string KpiIndirectaPct { get => kpiIndirectaPct; private set => SetProperty(ref kpiIndirectaPct, value); }
    public string KpiBaseTrend { get => kpiBaseTrend; private set => SetProperty(ref kpiBaseTrend, value); }
    public string KpiLeyesTrend { get => kpiLeyesTrend; private set => SetProperty(ref kpiLeyesTrend, value); }
    public string ExDirPico { get => exDirPico; private set => SetProperty(ref exDirPico, value); }
    public string ExDirSueldoProm { get => exDirSueldoProm; private set => SetProperty(ref exDirSueldoProm, value); }
    public string ExDirLeyVal { get => exDirLeyVal; private set => SetProperty(ref exDirLeyVal, value); }
    public string ExIndDur { get => exIndDur; private set => SetProperty(ref exIndDur, value); }
    public string ExIndRoles { get => exIndRoles; private set => SetProperty(ref exIndRoles, value); }
    public string ExIndSueldoProm { get => exIndSueldoProm; private set => SetProperty(ref exIndSueldoProm, value); }
    public string PeakLabel { get => peakLabel; private set => SetProperty(ref peakLabel, value); }
    public string TotalBarLabel { get => totalBarLabel; private set => SetProperty(ref totalBarLabel, value); }
    public string TotalBarSub { get => totalBarSub; private set => SetProperty(ref totalBarSub, value); }
    public string TotalBarValue { get => totalBarValue; private set => SetProperty(ref totalBarValue, value); }
    public string TotalBarUf { get => totalBarUf; private set => SetProperty(ref totalBarUf, value); }
    public string SumBase { get => sumBase; private set => SetProperty(ref sumBase, value); }
    public string SumLeyes { get => sumLeyes; private set => SetProperty(ref sumLeyes, value); }
    public string SumDirecta { get => sumDirecta; private set => SetProperty(ref sumDirecta, value); }
    public string SumIndirecta { get => sumIndirecta; private set => SetProperty(ref sumIndirecta, value); }
    public string SumPromMes { get => sumPromMes; private set => SetProperty(ref sumPromMes, value); }
    public string SumRight { get => sumRight; private set => SetProperty(ref sumRight, value); }
    public string FootDir { get => footDir; private set => SetProperty(ref footDir, value); }
    public string FootInd { get => footInd; private set => SetProperty(ref footInd, value); }
    public string CostHint { get => costHint; private set => SetProperty(ref costHint, value); }
    public string ChipMeses { get => chipMeses; private set => SetProperty(ref chipMeses, value); }
    public string ChipUf { get => chipUf; private set => SetProperty(ref chipUf, value); }

    public void Refresh() => Load();

    public void Load()
    {
        IsLoading = true;
        try
        {
            ShowEmptyState = !workspace.HasOpenProject;
            CanEdit = !ShowEmptyState && !session.IsReadOnly;
            if (ShowEmptyState)
                return;

            var syncProjection = false;

            ValorUF = session.ValorUfClp > 0 ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;
            // Regla de moneda: en un proyecto CLP la vista de mano de obra nunca muestra UF.
            OnPropertyChanged(nameof(MuestraUf));
            if (!MuestraUf)
                ViewInUf = false;

            if (!hasLoaded)
            {
                hasLoaded = true;
                var project = session.ProjectInfo;
                if (project.MoDuracionMeses > 0)
                    totalMeses = Math.Clamp(project.MoDuracionMeses, 1, 120);
                leyesSocialesPct = Math.Clamp(project.LeyesSocialesMoPct, 0m, 100m);
                OnPropertyChanged(nameof(TotalMeses));
                OnPropertyChanged(nameof(TotalMonths));
                OnPropertyChanged(nameof(LeyesSocialesPct));
                SeedCargos();
                LoadProjectionTasks();
                FitProjectionMonths();
                syncProjection = ApplySavedProjectionToDirecta();
            }

            Recalcular();
            if (syncProjection)
                PersistToSession();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadProjectionTasks() => _projectionCoordinator.LoadProjectionTasks();
    private bool ApplySavedProjectionToDirecta() => _projectionCoordinator.ApplySavedProjectionToDirecta();

    private void ExecuteApply()
    {
        if (!CanEdit)
            return;
        SyncEditorsToCargos();
        Recalcular();
        PersistToSession();
    }

    private void ExecuteResetParams()
    {
        if (!CanEdit)
            return;
        TotalMeses = 12;
        LeyesSocialesPct = 35m;
        CurvePreset = "bell";
        cellOverrides.Clear();
        peakHeadcount = 0;
        OnPropertyChanged(nameof(PeakHeadcount));
        foreach (var c in CargosDef)
            c.DotPico = 0;
        RebuildEditors();
        Recalcular();
        PersistToSession();
    }

    private void ExecuteResetSueldos()
    {
        if (!CanEdit)
            return;
        foreach (var c in CargosDef)
        {
            if (DefaultSueldos.TryGetValue(c.Tag, out var def) || DefaultSueldos.TryGetValue(c.Id, out def))
                c.SueldoBase = def;
        }

        suppressSalaryLoop = true;
        try
        {
            RebuildEditors();
        }
        finally
        {
            suppressSalaryLoop = false;
        }

        Recalcular();
        PersistToSession();
    }

    private void ExecuteTakeProjection() => _projectionCoordinator.ExecuteTakeProjection();
    private void ExecuteApplyAverageProjection() => _projectionCoordinator.ExecuteApplyAverageProjection();

    private void ExecuteAddDirectaCargo()
    {
        if (!CanEdit)
            return;

        var nextIndex = CargosDef.Count(c => c.EsDirecta) + 1;
        var suggestedTag = "D" + nextIndex;
        while (CargosDef.Any(c => string.Equals(c.Tag, suggestedTag, StringComparison.OrdinalIgnoreCase)))
        {
            nextIndex++;
            suggestedTag = "D" + nextIndex;
        }

        var dlg = new AddCargoMoDialog(
            isDirecta: true,
            suggestedTag: suggestedTag,
            defaultSueldo: 750000m,
            tagExistsCheck: tag => CargosDef.Any(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase)))
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() != true || dlg.Result == null)
            return;

        var res = dlg.Result;
        var newDef = new MoCargoDef
        {
            Id = Guid.NewGuid().ToString("N"),
            Nombre = res.Nombre,
            Tag = res.Tag,
            EsDirecta = true,
            DotPico = peakHeadcount,
            SueldoBase = res.SueldoBase,
            FraccionObra = 1m
        };

        undo?.PushUndoPoint(session, $"Agregar puesto directo '{newDef.Nombre}'");
        CargosDef = CargosDef.Concat(new[] { newDef }).ToArray();
        RebuildEditors();
        Recalcular();
        PersistToSession();
        OnPropertyChanged(nameof(CargosDef));
        ShellNotificationService.Instance.ShowSuccess("Mano de Obra", $"Puesto directo '{newDef.Nombre}' agregado exitosamente.");
    }

    private void ExecuteAddIndirectaCargo()
    {
        if (!CanEdit)
            return;

        var nextIndex = CargosDef.Count(c => !c.EsDirecta) + 1;
        var suggestedTag = "I" + nextIndex;
        while (CargosDef.Any(c => string.Equals(c.Tag, suggestedTag, StringComparison.OrdinalIgnoreCase)))
        {
            nextIndex++;
            suggestedTag = "I" + nextIndex;
        }

        var dlg = new AddCargoMoDialog(
            isDirecta: false,
            suggestedTag: suggestedTag,
            defaultSueldo: 850000m,
            tagExistsCheck: tag => CargosDef.Any(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase)))
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() != true || dlg.Result == null)
            return;

        var res = dlg.Result;
        var newDef = new MoCargoDef
        {
            Id = Guid.NewGuid().ToString("N"),
            Nombre = res.Nombre,
            Tag = res.Tag,
            EsDirecta = false,
            DotPico = res.Dotacion,
            SueldoBase = res.SueldoBase,
            FraccionObra = res.FraccionObra
        };

        undo?.PushUndoPoint(session, $"Agregar puesto indirecto '{newDef.Nombre}'");
        CargosDef = CargosDef.Concat(new[] { newDef }).ToArray();
        RebuildEditors();
        Recalcular();
        PersistToSession();
        OnPropertyChanged(nameof(CargosDef));
        ShellNotificationService.Instance.ShowSuccess("Mano de Obra", $"Puesto indirecto '{newDef.Nombre}' agregado exitosamente.");
    }

    private void DeleteCargo(MoCargoEditorVm editor)
    {
        if (!CanEdit)
            return;

        var isDirect = editor.Def.EsDirecta;
        var countInGroup = CargosDef.Count(c => c.EsDirecta == isDirect);
        if (isDirect && countInGroup <= 1)
        {
            WpfDialogService.Warning("Mano de Obra", "Debe existir al menos un puesto en Mano de Obra Directa.");
            return;
        }

        var confirm = WpfDialogService.Confirm(
            "Eliminar puesto",
            $"¿Está seguro de que desea eliminar el puesto '{editor.Nombre}'?\nSe eliminarán sus dotaciones y cálculos asociados.",
            isWarning: true);

        if (!confirm)
            return;

        undo?.PushUndoPoint(session, $"Eliminar puesto '{editor.Nombre}'");
        CargosDef = CargosDef.Where(c => c.Id != editor.Id).ToArray();

        var prefix = $"{editor.Id}_";
        var keysToRemove = cellOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var k in keysToRemove)
            cellOverrides.Remove(k);

        RebuildEditors();
        Recalcular();
        PersistToSession();
        OnPropertyChanged(nameof(CargosDef));
        ShellNotificationService.Instance.ShowSuccess("Mano de Obra", $"Puesto '{editor.Nombre}' eliminado.");
    }

    private void SetCurrency(bool uf) => ViewInUf = uf;

    public bool HasOverride(string cargoId, int monthIdx)
        => cellOverrides.ContainsKey($"{cargoId}_{monthIdx}");

    public void SetOverride(string cargoId, int monthIdx, int count)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        cellOverrides[$"{cargoId}_{monthIdx}"] = Math.Max(0, count);
        Recalcular();
        PersistToSession();
    }

    public void ClearOverride(string cargoId, int monthIdx)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        if (!cellOverrides.Remove($"{cargoId}_{monthIdx}"))
            return;
        Recalcular();
        PersistToSession();
    }

    private bool staffingEditsPending;

    /// <summary>Deja el número en memoria. La grilla se recalcula una sola vez al dejar de escribir.</summary>
    public void StageOverride(string cargoId, int monthIdx, int count)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return;
        var key = $"{cargoId}_{monthIdx}";
        count = Math.Max(0, count);
        if (cellOverrides.TryGetValue(key, out var current) && current == count)
            return;
        cellOverrides[key] = count;
        staffingEditsPending = true;
    }

    public bool StageClearOverride(string cargoId, int monthIdx)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId) || monthIdx < 0)
            return false;
        if (!cellOverrides.Remove($"{cargoId}_{monthIdx}"))
            return false;
        staffingEditsPending = true;
        return true;
    }

    public void FlushStaffingEdits()
    {
        if (!staffingEditsPending)
            return;
        staffingEditsPending = false;
        ApplyStagedDotPico();
        Recalcular();
        PersistToSession();
    }

    // ─── IPageWithPendingChanges / IPageWithCommitChanges ────────────────────
    public bool HasPendingChanges => staffingEditsPending || dotPicoPending;

    bool IPageWithPendingChanges.HasPendingChanges => HasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() => TryDiscardChanges();
    bool IPageWithCommitChanges.TryApplyChanges() => TryApplyChanges();

    public bool TryDiscardChanges()
    {
        dotPicoPending = false;
        pendingDotCargo = null;
        staffingEditsPending = false;
        cellOverrides.Clear();
        Load();
        return true;
    }

    public bool TryApplyChanges()
    {
        FlushStaffingEdits();
        return true;
    }

    private bool dotPicoPending;
    private string? pendingDotCargo;
    private int pendingDotPico;

    /// <summary>Deja la dotación pico de un rol indirecto en memoria; se aplica al dejar de escribir.</summary>
    public void StageIndirectDotPico(string cargoId, int value)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId))
            return;
        pendingDotCargo = cargoId;
        pendingDotPico = Math.Max(0, value);
        dotPicoPending = true;
        staffingEditsPending = true;
    }

    /// <summary>Cancela la dotación staged para ese rol (Escape). Devuelve false si no había nada staged.</summary>
    public bool StageClearDotPico(string cargoId)
    {
        if (!dotPicoPending || !string.Equals(pendingDotCargo, cargoId, StringComparison.Ordinal))
            return false;
        dotPicoPending = false;
        pendingDotCargo = null;
        return true;
    }

    /// <summary>
    /// Fija la dotación de un rol indirecto en todos los meses: deja de usar la curva/overrides
    /// y limpia las ediciones mes a mes de ese cargo.
    /// </summary>
    public void SetIndirectDotPico(string cargoId, int value)
    {
        if (!TrySetIndirectDotPico(cargoId, value))
            return;
        Recalcular();
        PersistToSession();
    }

    /// <summary>
    /// Establece la fracción de obra (dedicación) para un cargo indirecto (ej: 0.3 o 1.0)
    /// y actualiza su editor, cálculos y persistencia.
    /// </summary>
    public void SetCargoFraccion(string cargoId, decimal fraccion)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(cargoId))
            return;
        var cargo = CargosDef.FirstOrDefault(c => c.Id == cargoId && !c.EsDirecta);
        if (cargo == null)
            return;
        fraccion = ClampFraccion(fraccion);
        if (cargo.FraccionObra == fraccion && cargo.DotPico == 1)
            return;
        cargo.FraccionObra = fraccion;
        cargo.DotPico = 1;
        var editor = IndirectaEditors.FirstOrDefault(e => e.Id == cargoId);
        if (editor != null)
        {
            editor.FraccionEditText = FraccionStr(fraccion);
            editor.CommitEdit();
        }
        else
        {
            Recalcular();
            PersistToSession();
        }
    }

    private void ApplyStagedDotPico()
    {
        if (!dotPicoPending)
            return;
        dotPicoPending = false;
        var cargoId = pendingDotCargo;
        pendingDotCargo = null;
        TrySetIndirectDotPico(cargoId, pendingDotPico);
    }

    private bool TrySetIndirectDotPico(string? cargoId, int value)
    {
        if (string.IsNullOrWhiteSpace(cargoId))
            return false;
        var cargo = CargosDef.FirstOrDefault(c => c.Id == cargoId && !c.EsDirecta);
        if (cargo == null)
            return false;
        value = Math.Max(0, value);
        if (cargo.DotPico == value)
            return false;
        cargo.DotPico = value;
        ClearMonthlyOverrides(cargoId);
        return true;
    }

    private void ClearMonthlyOverrides(string cargoId)
    {
        var prefix = cargoId + "_";
        foreach (var key in cellOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            var mes = key.Substring(prefix.Length);
            if (mes.Length > 0 && mes.All(char.IsDigit))
                cellOverrides.Remove(key);
        }
    }

    private void OnEditorChanged()
    {
        if (suppressSalaryLoop)
            return;
        SyncEditorsToCargos();
        Recalcular();
        PersistToSession();
    }

    private void SyncEditorsToCargos()
    {
        foreach (var editor in DirectaEditors.Concat(IndirectaEditors))
        {
            var cargo = CargosDef.FirstOrDefault(c => c.Id == editor.Id);
            if (cargo == null)
                continue;
            cargo.Nombre = editor.Nombre;
            cargo.Tag = editor.Tag;
            cargo.SueldoBase = editor.SueldoBase;
            if (!cargo.EsDirecta)
                cargo.FraccionObra = ClampFraccion(editor.FraccionObra);
        }
    }

    private static decimal ClampFraccion(decimal n)
        => Math.Clamp(n, 0m, 1m);

    private static string FraccionStr(decimal n)
        => n.ToString("N2", CultureInfo.GetCultureInfo("es-CL"));

    private void PersistToSession()
    {
        if (!workspace.HasOpenProject || session.IsReadOnly)
            return;

        var data = session.Data;
        var n = totalMeses;

        data.MoDirecta = CargosDef.Where(c => c.EsDirecta).Select(c => new MoDirectaRow
        {
            Id = c.Id,
            Cargo = c.Nombre,
            Tag = c.Tag,
            Dotacion = c.DotPico,
            SueldoMensualClp = c.SueldoBase,
            DotacionMensual = BuildMensualList(c.Id, n),
        }).ToList();

        data.MoIndirecta = CargosDef.Where(c => !c.EsDirecta).Select(c => new MoIndirectaRow
        {
            Id = c.Id,
            Cargo = c.Nombre,
            Tag = c.Tag,
            Dotacion = c.DotPico,
            SueldoMensualClp = c.SueldoBase,
            FraccionObra = ClampFraccion(c.FraccionObra),
            DotacionMensual = BuildMensualList(c.Id, n),
        }).ToList();

        var project = data.Proyecto ?? session.ProjectInfo;
        project.MoDuracionMeses = totalMeses;
        project.LeyesSocialesMoPct = leyesSocialesPct;
        // Si ya recalculamos, reflejar el valor actual (incluso si es 0); si no, preservar lo existente.
        if (hasRecalculated)
        {
            project.MoTotalClp = lastTotalMoClp;
            project.MoDirectaClp = lastTotalDirClp;
        }
        else
        {
            if (lastTotalMoClp > 0m)
                project.MoTotalClp = lastTotalMoClp;
            if (lastTotalDirClp > 0m)
                project.MoDirectaClp = lastTotalDirClp;
        }
        data.Proyecto = project;

        // Cadena: MO → cantidades EPP → GO 3.5 en Desglose de costos.
        EppBudgetData.SyncEppAndGoFromMo(data);
        session.MarkDirty(
            ProjectDirtyScope.Parse("mo_directa"),
            ProjectDirtyScope.Parse("mo_indirecta"),
            ProjectDirtyScope.Epp,
            ProjectDirtyScope.GastosOperacionales,
            ProjectDirtyScope.ProjectInfo);
    }

    private List<int> BuildMensualList(string cargoId, int n)
    {
        var list = new List<int>(n);
        var hasAny = false;
        for (var i = 0; i < n; i++)
        {
            if (cellOverrides.TryGetValue($"{cargoId}_{i}", out var v))
            {
                list.Add(v);
                hasAny = true;
            }
            else
            {
                list.Add(-1); // sentinel: sin override
            }
        }
        return hasAny ? list : new List<int>();
    }

    private void LoadMensualOverrides(string cargoId, List<int>? mensual)
    {
        if (string.IsNullOrWhiteSpace(cargoId) || mensual == null || mensual.Count == 0)
            return;
        for (var i = 0; i < mensual.Count; i++)
        {
            if (mensual[i] >= 0)
                cellOverrides[$"{cargoId}_{i}"] = mensual[i];
        }
    }

    private void SeedCargos()
    {
        var data = session.Data;
        var list = new List<MoCargoDef>();
        var renamedCargoNames = false;

        if (data.MoDirecta is { Count: > 0 })
        {
            var i = 0;
            foreach (var r in data.MoDirecta)
            {
                var tag = !string.IsNullOrWhiteSpace(r.Tag) ? r.Tag : GuessTag(r.Cargo, true, i);
                var nombre = MoCargoNameNormalizer.ResolveDirectaNombre(r.Cargo, i);
                if (MoCargoNameNormalizer.IsGuidLike(r.Cargo)) renamedCargoNames = true;
                list.Add(new MoCargoDef
                {
                    Id = string.IsNullOrWhiteSpace(r.Id) ? tag : r.Id,
                    Nombre = string.IsNullOrWhiteSpace(nombre) ? tag : nombre,
                    Tag = tag,
                    EsDirecta = true,
                    DotPico = Math.Max(0, r.Dotacion),
                    SueldoBase = r.SueldoMensualClp > 0 ? r.SueldoMensualClp : DefaultSueldos.GetValueOrDefault(tag, 700000m),
                });
                i++;
            }
        }
        else
        {
            // Plantilla de cargos (dotación 0) — sin inventar personas.
            list.Add(new MoCargoDef { Id = "M1", Nombre = "Maestro eléctrico", Tag = "M1", EsDirecta = true, DotPico = 0, SueldoBase = 750000m });
            list.Add(new MoCargoDef { Id = "AA", Nombre = "Ayudante eléctrico", Tag = "AA", EsDirecta = true, DotPico = 0, SueldoBase = 650000m });
        }

        if (data.MoIndirecta is { Count: > 0 })
        {
            var i = 0;
            foreach (var r in data.MoIndirecta)
            {
                var tag = !string.IsNullOrWhiteSpace(r.Tag) ? r.Tag : GuessTag(r.Cargo, false, i++);
                list.Add(new MoCargoDef
                {
                    Id = string.IsNullOrWhiteSpace(r.Id) ? tag : r.Id,
                    Nombre = string.IsNullOrWhiteSpace(r.Cargo) ? tag : r.Cargo,
                    Tag = tag,
                    EsDirecta = false,
                    DotPico = Math.Max(0, r.Dotacion),
                    SueldoBase = r.SueldoMensualClp > 0 ? r.SueldoMensualClp : DefaultSueldos.GetValueOrDefault(tag, 800000m),
                    FraccionObra = r.FraccionEfectiva,
                });
            }
        }
        else
        {
            list.Add(new MoCargoDef { Id = "AD", Nombre = "Administrador de obra", Tag = "AD", EsDirecta = false, DotPico = 0, SueldoBase = 1237500m });
            list.Add(new MoCargoDef { Id = "SV", Nombre = "Supervisor de obra", Tag = "SV", EsDirecta = false, DotPico = 0, SueldoBase = 1466667m });
            list.Add(new MoCargoDef { Id = "CA", Nombre = "Capataz de obra", Tag = "CA", EsDirecta = false, DotPico = 0, SueldoBase = 916667m });
            list.Add(new MoCargoDef { Id = "OT", Nombre = "Oficina técnica", Tag = "OT", EsDirecta = false, DotPico = 0, SueldoBase = 779167m });
            list.Add(new MoCargoDef { Id = "BG", Nombre = "Encargado de bodega", Tag = "BG", EsDirecta = false, DotPico = 0, SueldoBase = 595833m });
            list.Add(new MoCargoDef { Id = "PR", Nombre = "Prevencionista de riesgos", Tag = "PR", EsDirecta = false, DotPico = 0, SueldoBase = 641667m });
        }

        CargosDef = list.ToArray();
        peakHeadcount = list.Where(c => c.EsDirecta).Select(c => c.DotPico).DefaultIfEmpty(0).Max();
        OnPropertyChanged(nameof(PeakHeadcount));

        cellOverrides.Clear();
        if (data.MoDirecta is { Count: > 0 })
        {
            foreach (var r in data.MoDirecta)
            {
                var id = string.IsNullOrWhiteSpace(r.Id) ? GuessTag(r.Cargo, true, 0) : r.Id;
                LoadMensualOverrides(id, r.DotacionMensual);
            }
        }
        if (data.MoIndirecta is { Count: > 0 })
        {
            foreach (var r in data.MoIndirecta)
            {
                var id = string.IsNullOrWhiteSpace(r.Id) ? GuessTag(r.Cargo, false, 0) : r.Id;
                LoadMensualOverrides(id, r.DotacionMensual);
            }
        }

        // Limpiar plantilla demo histórica (77 pico / 1 indirecta) → proyecto limpio en 0.
        if (IsLegacyDemoDotacion(list))
        {
            foreach (var c in list)
                c.DotPico = 0;
            peakHeadcount = 0;
            OnPropertyChanged(nameof(PeakHeadcount));
            cellOverrides.Clear();
            PersistToSession();
        }
        else if (data.MoDirecta is not { Count: > 0 } && data.MoIndirecta is not { Count: > 0 })
        {
            PersistToSession();
        }
        else if (renamedCargoNames)
            PersistToSession();

        RebuildEditors();
        OnPropertyChanged(nameof(CargosDef));
    }

    /// <summary>Detecta la plantilla inventada (pico 77 + indirecta fija en 1) para resetearla a 0.</summary>
    private static bool IsLegacyDemoDotacion(List<MoCargoDef> list)
    {
        var dir = list.Where(c => c.EsDirecta).ToArray();
        var ind = list.Where(c => !c.EsDirecta).ToArray();
        if (dir.Length == 0 || ind.Length == 0)
            return false;
        return dir.All(c => c.DotPico is 77) && ind.All(c => c.DotPico is 1);
    }

    private void RebuildEditors()
    {
        DirectaEditors.Clear();
        IndirectaEditors.Clear();
        var leyes = leyesSocialesPct / 100m;

        foreach (var c in CargosDef.Where(x => x.EsDirecta))
        {
            var ed = new MoCargoEditorVm(c, "Dotación variable según etapa", OnEditorChanged, DeleteCargo);
            ed.UpdateConLeyes(leyes);
            DirectaEditors.Add(ed);
        }

        foreach (var c in CargosDef.Where(x => !x.EsDirecta))
        {
            var ed = new MoCargoEditorVm(c, "Dotación fija todos los meses", OnEditorChanged, DeleteCargo);
            ed.UpdateConLeyes(leyes);
            IndirectaEditors.Add(ed);
        }
    }

    private static string GuessTag(string cargo, bool directa, int index)
    {
        var n = (cargo ?? "").ToLowerInvariant();
        if (n.Contains("maestro")) return "M1";
        if (n.Contains("ayudante")) return "AA";
        if (n.Contains("administrador")) return "AD";
        if (n.Contains("supervisor")) return "SV";
        if (n.Contains("capataz")) return "CA";
        if (n.Contains("oficina")) return "OT";
        if (n.Contains("bodega")) return "BG";
        if (n.Contains("prevencion")) return "PR";
        return (directa ? "D" : "I") + (index + 1);
    }

    public void Recalcular()
    {
        if (CargosDef.Length == 0)
            return;

        var n = totalMeses;
        var vUf = valorUf;
        var leyes = leyesSocialesPct / 100m;
        var directa = CargosDef.Where(c => c.EsDirecta).ToArray();
        var indirecta = CargosDef.Where(c => !c.EsDirecta).ToArray();

        foreach (var c in directa)
        {
            if (c.DotPico <= 0 && peakHeadcount > 0)
                c.DotPico = peakHeadcount;
        }

        var rampUp = Math.Max(1, (int)Math.Round(n * 0.25));
        var peak = Math.Max(1, (int)Math.Round(n * 0.40));
        var rampDown = Math.Max(0, n - rampUp - peak);
        var peakInicio = rampUp + 1;
        var peakFin = rampUp + peak;

        var meses = new MoMesInfo[n];
        var dotacionPorCargo = new Dictionary<string, int[]>();
        foreach (var c in CargosDef)
            dotacionPorCargo[c.Id] = new int[n];

        decimal totalBase = 0, totalLeyes = 0, totalMo = 0, totalDir = 0, totalInd = 0;

        for (var i = 0; i < n; i++)
        {
            double frac;
            string fase;
            if (i < rampUp)
            {
                frac = (i + 1.0) / rampUp;
                fase = "ramp-up";
            }
            else if (i < rampUp + peak)
            {
                frac = 1.0;
                fase = "peak";
            }
            else
            {
                frac = 1.0 - ((i - rampUp - peak + 1.0) / (rampDown + 1));
                fase = "ramp-down";
            }

            int head = curvePreset is "fixed" or "flat"
                ? peakHeadcount
                : (int)Math.Round(frac * peakHeadcount);

            foreach (var c in CargosDef)
            {
                var key = $"{c.Id}_{i}";
                if (cellOverrides.TryGetValue(key, out var count))
                {
                    dotacionPorCargo[c.Id][i] = count;
                }
                else if (c.EsDirecta)
                {
                    dotacionPorCargo[c.Id][i] = head;
                }
                else
                {
                    dotacionPorCargo[c.Id][i] = c.DotPico;
                }
            }

            decimal baseDirMes = directa.Sum(c => c.SueldoBase * dotacionPorCargo[c.Id][i]);
            decimal baseIndMes = indirecta.Sum(c => c.SueldoBase * c.FraccionObra * dotacionPorCargo[c.Id][i]);
            var leyesDirMes = baseDirMes * leyes;
            var leyesIndMes = baseIndMes * leyes;
            var baseMes = baseDirMes + baseIndMes;
            var leyesMes = leyesDirMes + leyesIndMes;
            var totalMes = baseMes + leyesMes;
            var dotDir = directa.Sum(c => dotacionPorCargo[c.Id][i]);
            var dotInd = indirecta.Sum(c => dotacionPorCargo[c.Id][i]);

            totalBase += baseMes;
            totalLeyes += leyesMes;
            totalMo += totalMes;
            totalDir += baseDirMes + leyesDirMes;
            totalInd += baseIndMes + leyesIndMes;

            meses[i] = new MoMesInfo
            {
                Numero = i + 1,
                Fase = fase,
                DotDir = dotDir,
                DotInd = dotInd,
                CostoStr = Fmt(totalMes),
                CostoTotal = totalMes,
            };
        }

        Meses = meses;

        var dotRows = new List<MoTableRow>();
        var picoDir = directa.Sum(c => c.DotPico);
        dotRows.Add(new MoTableRow
        {
            IsGroupHeader = true,
            GroupLabel = "MO Directa",
            GroupBadge = $"{picoDir} pico",
            GroupBadgeType = "dir",
        });

        foreach (var c in directa)
        {
            var dots = dotacionPorCargo[c.Id];
            decimal totalCargo = 0;
            for (var i = 0; i < n; i++)
                totalCargo += c.SueldoBase * dots[i] * (1 + leyes);

            dotRows.Add(new MoTableRow
            {
                Tag = c.Tag,
                CargoId = c.Id,
                Nombre = c.Nombre,
                EsDirecta = true,
                DotPico = c.DotPico,
                DotacionStr = c.DotPico.ToString(CultureInfo.InvariantCulture),
                SueldoBaseStr = Fmt(c.SueldoBase),
                FraccionStr = "—",
                LeyesStr = Fmt(c.SueldoBase * leyes),
                TotalMensualStr = Fmt(c.SueldoBase * (1 + leyes)),
                Dotaciones = dots,
                TotalStr = Fmt(totalCargo),
            });
        }

        var subDirDots = new int[n];
        decimal subDirTotal = 0;
        for (var i = 0; i < n; i++)
        {
            subDirDots[i] = directa.Sum(c => dotacionPorCargo[c.Id][i]);
            subDirTotal += directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * (1 + leyes));
        }

        dotRows.Add(new MoTableRow
        {
            IsSubtotal = true,
            EsDirecta = true,
            Tag = "",
            Nombre = "Subtotal MO Directa",
            DotPico = picoDir,
            DotacionStr = picoDir.ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(directa.Sum(c => c.SueldoBase)),
            FraccionStr = "—",
            LeyesStr = Fmt(directa.Sum(c => c.SueldoBase) * leyes),
            TotalMensualStr = Fmt(directa.Sum(c => c.SueldoBase) * (1 + leyes)),
            Dotaciones = subDirDots,
            TotalStr = Fmt(subDirTotal),
        });

        var picoInd = indirecta.Sum(c => c.DotPico);
        dotRows.Add(new MoTableRow
        {
            IsGroupHeader = true,
            GroupLabel = "MO Indirecta",
            GroupBadge = $"{picoInd} roles fijos",
            GroupBadgeType = "ind",
        });

        foreach (var c in indirecta)
        {
            var dots = dotacionPorCargo[c.Id];
            var baseCobrada = c.SueldoBase * c.FraccionObra;
            decimal totalCargo = 0;
            for (var i = 0; i < n; i++)
                totalCargo += baseCobrada * dots[i] * (1 + leyes);

            var dotStr = (c.FraccionObra > 0m && c.FraccionObra < 1m)
                ? c.FraccionObra.ToString("0.##", CultureInfo.GetCultureInfo("es-CL"))
                : (c.DotPico * c.FraccionObra).ToString("0.##", CultureInfo.GetCultureInfo("es-CL"));
            if (string.IsNullOrWhiteSpace(dotStr) || dotStr == "0")
                dotStr = c.FraccionObra == 0m ? "0" : c.DotPico.ToString(CultureInfo.InvariantCulture);

            dotRows.Add(new MoTableRow
            {
                Tag = c.Tag,
                CargoId = c.Id,
                Nombre = c.Nombre,
                EsDirecta = false,
                DotPico = c.DotPico,
                DotacionStr = dotStr,
                SueldoBaseStr = Fmt(baseCobrada),
                FraccionObra = c.FraccionObra,
                FraccionStr = FraccionStr(c.FraccionObra),
                LeyesStr = Fmt(baseCobrada * leyes),
                TotalMensualStr = Fmt(baseCobrada * (1 + leyes)),
                Dotaciones = dots,
                TotalStr = Fmt(totalCargo),
            });
        }

        var subIndDots = new int[n];
        decimal subIndTotal = 0;
        for (var i = 0; i < n; i++)
        {
            subIndDots[i] = indirecta.Sum(c => dotacionPorCargo[c.Id][i]);
            subIndTotal += indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra * (1 + leyes));
        }

        dotRows.Add(new MoTableRow
        {
            IsSubtotal = true,
            EsDirecta = false,
            Tag = "",
            Nombre = "Subtotal MO Indirecta",
            DotPico = picoInd,
            DotacionStr = picoInd.ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra)),
            FraccionStr = "—",
            LeyesStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra) * leyes),
            TotalMensualStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra) * (1 + leyes)),
            Dotaciones = subIndDots,
            TotalStr = Fmt(subIndTotal),
        });

        var grandDots = new int[n];
        for (var i = 0; i < n; i++)
            grandDots[i] = subDirDots[i] + subIndDots[i];

        var basePorMesDir = directa.Sum(c => c.SueldoBase);
        var basePorMesInd = indirecta.Sum(c => c.SueldoBase * c.FraccionObra);
        dotRows.Add(new MoTableRow
        {
            IsGrandTotal = true,
            Tag = "",
            Nombre = "Total dotación",
            DotPico = grandDots.Length > 0 ? grandDots.Max() : 0,
            DotacionStr = (grandDots.Length > 0 ? grandDots.Max() : 0).ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(basePorMesDir + basePorMesInd),
            FraccionStr = "—",
            LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
            TotalMensualStr = Fmt((basePorMesDir + basePorMesInd) * (1 + leyes)),
            Dotaciones = grandDots,
            TotalStr = Fmt(totalMo),
        });

        DotacionRows = dotRows.ToArray();
        lastTotalMoClp = totalMo;
        lastTotalDirClp = totalDir;
        hasRecalculated = true;

        var costRows = new List<MoCostRow>
        {
            new()
            {
                Label = "MO Directa (base + leyes)",
                Tag = "dir",
                BaseStr = Fmt(basePorMesDir),
                LeyesStr = Fmt(basePorMesDir * leyes),
                TotalStr = Fmt(basePorMesDir * (1 + leyes)),
                CostosStr = meses.Select((_, i) => Fmt(directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * (1 + leyes)))).ToArray(),
                TotalFinalStr = Fmt(totalDir),
            },
            new()
            {
                Label = "MO Indirecta (base + leyes)",
                Tag = "ind",
                BaseStr = Fmt(basePorMesInd),
                LeyesStr = Fmt(basePorMesInd * leyes),
                TotalStr = Fmt(basePorMesInd * (1 + leyes)),
                CostosStr = meses.Select((_, i) => Fmt(indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra * (1 + leyes)))).ToArray(),
                TotalFinalStr = Fmt(totalInd),
            },
            new()
            {
                IsSubtotal = true,
                Label = "Sueldos base",
                Tag = "base",
                BaseStr = Fmt(basePorMesDir + basePorMesInd),
                LeyesStr = "—",
                TotalStr = Fmt(basePorMesDir + basePorMesInd),
                CostosStr = meses.Select((_, i) =>
                {
                    var b = directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase)
                          + indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra);
                    return Fmt(b);
                }).ToArray(),
                TotalFinalStr = Fmt(totalBase),
            },
            new()
            {
                IsSubtotal = true,
                Label = $"Leyes sociales ({Math.Round(leyesSocialesPct)}%)",
                Tag = "leyes",
                BaseStr = "—",
                LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                TotalStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                CostosStr = meses.Select((_, i) =>
                {
                    var b = directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase)
                          + indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra);
                    return Fmt(b * leyes);
                }).ToArray(),
                TotalFinalStr = Fmt(totalLeyes),
            },
            new()
            {
                IsGrandTotal = true,
                Label = "Total mes",
                BaseStr = Fmt(basePorMesDir + basePorMesInd),
                LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                TotalStr = Fmt((basePorMesDir + basePorMesInd) * (1 + leyes)),
                CostosStr = meses.Select(m => m.CostoStr).ToArray(),
                TotalFinalStr = Fmt(totalMo),
            },
        };

        CostRows = costRows.ToArray();

        var pctBase = totalMo > 0 ? totalBase / totalMo * 100 : 0;
        var pctLeyes = totalMo > 0 ? totalLeyes / totalMo * 100 : 0;
        var pctDir = totalMo > 0 ? totalDir / totalMo * 100 : 0;
        var pctInd = totalMo > 0 ? totalInd / totalMo * 100 : 0;
        var mesProm = n > 0 ? totalMo / n : 0;
        var promSueldoDir = directa.Length > 0 ? directa.Average(c => c.SueldoBase) : 0;
        var promSueldoInd = indirecta.Length > 0 ? indirecta.Average(c => c.SueldoBase * c.FraccionObra) : 0;
        var picoDotDir = subDirDots.Length > 0 ? subDirDots.Max() : 0;

        KpiTotalMo = Fmt(totalMo);
        KpiTotalMoSub = $"{n} meses · pico {peakHeadcount} pers.";
        KpiBase = Fmt(totalBase);
        KpiBasePct = $"{pctBase:N1}% del total MO";
        KpiLeyes = Fmt(totalLeyes);
        KpiLeyesPct = $"{pctLeyes:N1}% del total MO";
        KpiDirecta = Fmt(totalDir);
        KpiDirectaSub = $"{directa.Length} cargos · variable";
        KpiDirectaPct = $"{pctDir:N1}% del total";
        KpiIndirecta = Fmt(totalInd);
        KpiIndirectaSub = $"{indirecta.Length} roles · fijo";
        KpiIndirectaPct = $"{pctInd:N1}% del total";
        KpiBaseTrend = "Base imponible";
        KpiLeyesTrend = $"{Math.Round(leyesSocialesPct)}% leyes";

        ExDirPico = $"{picoDotDir} pers.";
        ExDirSueldoProm = FmtClp(promSueldoDir);
        ExDirLeyVal = FmtClp(promSueldoDir * leyes);
        ExIndDur = $"{n} meses";
        ExIndRoles = $"{indirecta.Length}";
        ExIndSueldoProm = FmtClp(promSueldoInd);

        PeakLabel = $"M{peakInicio} – M{peakFin}";
        ChipMeses = $"{n} meses";
        ChipUf = FmtClp(vUf);
        CostHint = (viewInUf ? "UF" : "CLP") + $" · incluye {Math.Round(leyesSocialesPct)}% leyes";

        TotalBarLabel = $"Costo total mano de obra · {n} meses";
        TotalBarSub = $"Directa {pctDir:N1}% · Indirecta {pctInd:N1}% · Curva {(curvePreset == "flat" ? "plana" : "campana")} · Pico M{peakInicio}–M{peakFin}";
        TotalBarValue = Fmt(totalMo);
        TotalBarUf = viewInUf ? FmtClp(totalMo) : (MuestraUf ? FmtUf(totalMo, vUf) : "");

        SumBase = Fmt(totalBase);
        SumLeyes = Fmt(totalLeyes);
        SumDirecta = Fmt(totalDir);
        SumIndirecta = Fmt(totalInd);
        SumPromMes = Fmt(mesProm);
        SumRight = viewInUf
            ? $"{FmtUf(totalMo, vUf)}  ·  {FmtClp(mesProm)}/mes"
            : $"{FmtClp(totalMo)}  ·  {FmtClp(mesProm)}/mes";

        FootDir = $"{FmtClp(promSueldoDir)} · {FmtClp(promSueldoDir * (1 + leyes))}";
        FootInd = $"{FmtClp(promSueldoInd)} · {FmtClp(promSueldoInd * (1 + leyes))}";

        foreach (var ed in DirectaEditors.Concat(IndirectaEditors))
            ed.UpdateConLeyes(leyes);

        RebuildChart(meses);

        OnPropertyChanged(nameof(CargosDef));
        OnPropertyChanged(nameof(Meses));
        OnPropertyChanged(nameof(DotacionRows));
        OnPropertyChanged(nameof(CostRows));
    }

    private void RebuildChart(MoMesInfo[] meses)
    {
        ChartBars.Clear();
        if (meses.Length == 0)
            return;

        var maxDot = Math.Max(1, meses.Max(m => m.DotTotal));
        foreach (var m in meses)
        {
            ChartBars.Add(new MoChartBarVm
            {
                Label = $"M{m.Numero}",
                DirPct = 100.0 * m.DotDir / maxDot,
                IndPct = 100.0 * m.DotInd / maxDot,
                Tip = $"Mes {m.Numero} · {m.DotTotal} pers. · {Fmt(m.CostoTotal)}",
            });
        }
    }
}
