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

public sealed partial class ManoDeObraViewModel : ViewModelBase, IRefreshablePage
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
    private bool showEmptyState = true;
    private bool canEdit;
    private bool isLoading;
    private bool hasLoaded;
    private bool suppressSalaryLoop;
    private int totalMeses = 12;
    private decimal lastTotalMoClp;
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
            return (n / valorUf).ToString("N2", CultureInfo.GetCultureInfo("es-CL")) + " UF";
        return "$ " + Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));
    }

    private static string FmtClp(decimal n)
        => "$ " + Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));

    private string FmtUf(decimal n, decimal vUf)
        => (n / vUf).ToString("N2", CultureInfo.GetCultureInfo("es-CL")) + " UF";

    private readonly List<MoProjectionTaskVm> projectionTasks = new();

    public MoProjectionTaskVm[] ProjectionTasks => projectionTasks.ToArray();

    public MoCargoDef[] CargosDef { get; private set; } = Array.Empty<MoCargoDef>();
    public MoMesInfo[] Meses { get; private set; } = Array.Empty<MoMesInfo>();
    public MoTableRow[] DotacionRows { get; private set; } = Array.Empty<MoTableRow>();
    public MoCostRow[] CostRows { get; private set; } = Array.Empty<MoCostRow>();
    public ObservableCollection<MoCargoEditorVm> DirectaEditors { get; } = new();
    public ObservableCollection<MoCargoEditorVm> IndirectaEditors { get; } = new();
    public ObservableCollection<MoChartBarVm> ChartBars { get; } = new();
    public int TotalMonths => totalMeses;

    public ManoDeObraViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace) { }

    public ManoDeObraViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ApplyCommand = new RelayCommand(ExecuteApply, () => CanEdit);
        ResetParamsCommand = new RelayCommand(ExecuteResetParams, () => CanEdit);
        ResetSueldosCommand = new RelayCommand(ExecuteResetSueldos, () => CanEdit);
        TakeProjectionCommand = new RelayCommand(ExecuteTakeProjection, () => CanEdit);
        SetCurrencyClpCommand = new RelayCommand(() => SetCurrency(false));
        SetCurrencyUfCommand = new RelayCommand(() => SetCurrency(true));
    }

    public ICommand OpenProjectCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ResetParamsCommand { get; }
    public ICommand ResetSueldosCommand { get; }
    public ICommand TakeProjectionCommand { get; }
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

    private void LoadProjectionTasks()
    {
        projectionTasks.Clear();
        var saved = session.Data.MoProjectionTasks;
        if (saved != null)
        {
            foreach (var row in saved)
            {
                var cantidades = row.CantidadesMensuales
                    .Take(totalMeses)
                    .Concat(Enumerable.Repeat(0, totalMeses))
                    .Take(totalMeses)
                    .Select(v => Math.Max(0, v))
                    .ToArray();
                projectionTasks.Add(new MoProjectionTaskVm
                {
                    Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id,
                    Nombre = string.IsNullOrWhiteSpace(row.Nombre) ? "Nueva tarea" : row.Nombre.Trim(),
                    Cantidades = cantidades
                });
            }
        }

        if (projectionTasks.Count == 0 || IsPlaceholderProjection(projectionTasks))
        {
            projectionTasks.Clear();
            foreach (var name in DefaultProjectionPartidas)
                AddProjectionTaskCore(name);
            PersistProjectionTasks();
        }

        OnPropertyChanged(nameof(ProjectionTasks));
    }

    private static readonly string[] DefaultProjectionPartidas =
    [
        "Loseros",
        "Destapadores",
        "Enlauchadores",
        "Tablereros",
        "Cableado",
        "Cabl C. Escala",
        "Artefactos",
        "Fierros",
        "Revisión Final",
    ];

    private static bool IsPlaceholderProjection(List<MoProjectionTaskVm> tasks)
    {
        if (tasks.Count != 1)
            return false;
        var only = tasks[0];
        var name = only.Nombre.Trim();
        var placeholder = name.Equals("Losero", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Nueva tarea", StringComparison.OrdinalIgnoreCase);
        return placeholder && only.Cantidades.All(v => v == 0);
    }

    private void FitProjectionMonths()
    {
        var changed = false;
        foreach (var task in projectionTasks)
        {
            if (task.Cantidades.Length == totalMeses)
                continue;
            var next = new int[totalMeses];
            Array.Copy(task.Cantidades, next, Math.Min(task.Cantidades.Length, totalMeses));
            task.Cantidades = next;
            changed = true;
        }

        if (changed)
        {
            OnPropertyChanged(nameof(ProjectionTasks));
            PersistProjectionTasks();
        }
    }

    public void AddProjectionTask(string? name = null)
    {
        if (!CanEdit)
            return;
        var label = string.IsNullOrWhiteSpace(name)
            ? $"Partida {projectionTasks.Count + 1}"
            : name.Trim();
        AddProjectionTaskCore(label);
        PersistProjectionTasks();
    }

    public void RemoveProjectionTask(string id)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(id))
            return;
        FlushProjectionEdits();
        var before = MonthlyProjectionTotals();
        if (projectionTasks.RemoveAll(t => t.Id == id) == 0)
            return;
        OnPropertyChanged(nameof(ProjectionTasks));
        PersistProjectionTasks();
        PushChangedProjectionMonths(before);
    }

    public void RenameProjectionTask(string id, string name)
    {
        if (!CanEdit)
            return;
        var task = projectionTasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
            return;
        var label = string.IsNullOrWhiteSpace(name) ? "Partida" : name.Trim();
        if (task.Nombre == label)
            return;
        task.Nombre = label;
        OnPropertyChanged(nameof(ProjectionTasks));
        PersistProjectionTasks();
    }

    public bool SetProjectionQuantity(string id, int monthIndex, int value)
    {
        if (!CanEdit)
            return false;
        var task = projectionTasks.FirstOrDefault(t => t.Id == id);
        if (task == null || monthIndex < 0 || monthIndex >= task.Cantidades.Length)
            return false;
        value = Math.Max(0, value);
        if (task.Cantidades[monthIndex] == value)
            return false;
        task.Cantidades[monthIndex] = value;
        pendingProjectionMonths.Add(monthIndex);
        projectionPersistPending = true;
        return true;
    }

    private readonly HashSet<int> pendingProjectionMonths = new();
    private bool projectionPersistPending;

    /// <summary>
    /// Escribe en la dotación y guarda una sola vez, cuando ya se dejó de escribir.
    /// </summary>
    public void FlushProjectionEdits()
    {
        if (!projectionPersistPending && pendingProjectionMonths.Count == 0)
            return;

        var months = pendingProjectionMonths.Count == 0
            ? Array.Empty<int>()
            : new int[pendingProjectionMonths.Count];
        if (months.Length > 0)
            pendingProjectionMonths.CopyTo(months);
        pendingProjectionMonths.Clear();
        var persistTasks = projectionPersistPending;
        projectionPersistPending = false;

        if (persistTasks)
            PersistProjectionTasks();
        if (months.Length > 0)
            PushProjectionMonths(months);
    }

    /// <summary>True mientras el total de la proyección se escribe en los meses de MO directa.</summary>
    public bool IsApplyingProjection { get; private set; }

    private int[] MonthlyProjectionTotals()
    {
        var totals = new int[totalMeses];
        for (var i = 0; i < totalMeses; i++)
            totals[i] = ProjectionMonthTotal(i);
        return totals;
    }

    private bool ApplySavedProjectionToDirecta()
    {
        var months = new List<int>();
        for (var i = 0; i < totalMeses; i++)
        {
            if (ProjectionMonthTotal(i) > 0)
                months.Add(i);
        }
        return months.Count > 0 && WriteProjectionMonths(months);
    }

    private void PushChangedProjectionMonths(int[] before)
    {
        var months = new List<int>();
        for (var i = 0; i < totalMeses && i < before.Length; i++)
        {
            if (ProjectionMonthTotal(i) != before[i])
                months.Add(i);
        }
        PushProjectionMonths(months);
    }

    private void PushProjectionMonths(IReadOnlyList<int> months)
    {
        if (months.Count == 0 || !WriteProjectionMonths(months))
            return;
        IsApplyingProjection = true;
        try
        {
            Recalcular();
            PersistToSession();
        }
        finally
        {
            IsApplyingProjection = false;
        }
    }

    /// <summary>
    /// El total de trabajadores del mes se reparte entre los cargos de MO directa
    /// según su dotación pico, y queda como edición de esa columna.
    /// </summary>
    private bool WriteProjectionMonths(IReadOnlyList<int> months)
    {
        var directa = CargosDef.Where(c => c.EsDirecta).ToArray();
        if (directa.Length == 0)
            return false;

        var weights = directa.Select(c => Math.Max(0, c.DotPico)).ToArray();
        var changed = false;
        foreach (var monthIndex in months)
        {
            if (monthIndex < 0 || monthIndex >= totalMeses)
                continue;
            var shares = SplitByWeight(ProjectionMonthTotal(monthIndex), weights);
            for (var i = 0; i < directa.Length; i++)
            {
                var key = $"{directa[i].Id}_{monthIndex}";
                if (cellOverrides.TryGetValue(key, out var current) && current == shares[i])
                    continue;
                cellOverrides[key] = shares[i];
                changed = true;
            }
        }
        return changed;
    }

    private static int[] SplitByWeight(int total, int[] weights)
    {
        var count = weights.Length;
        var result = new int[count];
        if (count == 0 || total <= 0)
            return result;

        var weightSum = weights.Sum();
        if (weightSum <= 0)
        {
            var each = total / count;
            var extra = total % count;
            for (var i = 0; i < count; i++)
                result[i] = each + (i < extra ? 1 : 0);
            return result;
        }

        var exact = new double[count];
        var assigned = 0;
        for (var i = 0; i < count; i++)
        {
            exact[i] = total * (weights[i] / (double)weightSum);
            result[i] = (int)Math.Floor(exact[i]);
            assigned += result[i];
        }

        var left = total - assigned;
        var order = Enumerable.Range(0, count)
            .OrderByDescending(i => exact[i] - result[i])
            .ToArray();
        for (var k = 0; k < left; k++)
            result[order[k]]++;
        return result;
    }

    public int ProjectionMonthTotal(int monthIndex)
        => projectionTasks.Sum(t => monthIndex >= 0 && monthIndex < t.Cantidades.Length ? t.Cantidades[monthIndex] : 0);

    public double ProjectionMonthlyAverage()
    {
        if (totalMeses <= 0)
            return 0;
        double sum = 0;
        for (var i = 0; i < totalMeses; i++)
            sum += ProjectionMonthTotal(i);
        return sum / totalMeses;
    }

    public bool ProjectionMonthIsPeak(int monthIndex)
        => monthIndex >= 0 && monthIndex < Meses.Length && Meses[monthIndex].EsPeak;

    private void AddProjectionTaskCore(string name)
    {
        projectionTasks.Add(new MoProjectionTaskVm
        {
            Id = Guid.NewGuid().ToString("N"),
            Nombre = name,
            Cantidades = new int[totalMeses]
        });
        OnPropertyChanged(nameof(ProjectionTasks));
    }

    private void PersistProjectionTasks()
    {
        if (!workspace.HasOpenProject || session.IsReadOnly)
            return;

        var data = session.Data;
        data.MoProjectionTasks = projectionTasks.Select(t => new MoProjectionTaskRow
        {
            Id = t.Id,
            Nombre = t.Nombre,
            CantidadesMensuales = t.Cantidades
        }).ToList();
        session.MarkDirty("mo_projection_tasks");
    }

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

    private void ExecuteTakeProjection()
    {
        if (!CanEdit)
            return;
        FlushProjectionEdits();
        if (!projectionTasks.Any(t => t.Cantidades.Any(c => c > 0)))
        {
            ShellNotificationService.Instance.ShowWarning(
                "Proyección MO",
                "La proyección no tiene trabajadores asignados: carga los meses en el botón Proyección MO.");
            return;
        }

        ApplySavedProjectionToDirecta();
        Recalcular();
        PersistToSession();
        ShellNotificationService.Instance.ShowSuccess(
            "Proyección MO",
            "Dotación directa actualizada con los totales de la proyección.");
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
            Dotacion = c.DotPico,
            SueldoMensualClp = c.SueldoBase,
            DotacionMensual = BuildMensualList(c.Id, n),
        }).ToList();

        data.MoIndirecta = CargosDef.Where(c => !c.EsDirecta).Select(c => new MoIndirectaRow
        {
            Id = c.Id,
            Cargo = c.Nombre,
            Dotacion = c.DotPico,
            SueldoMensualClp = c.SueldoBase,
            FraccionObra = ClampFraccion(c.FraccionObra),
            DotacionMensual = BuildMensualList(c.Id, n),
        }).ToList();

        var project = data.Proyecto ?? session.ProjectInfo;
        project.MoDuracionMeses = totalMeses;
        project.LeyesSocialesMoPct = leyesSocialesPct;
        // Solo sobrescribir el total si ya recalculamos (evita borrar MoTotalClp en SeedCargos).
        if (lastTotalMoClp > 0m)
            project.MoTotalClp = lastTotalMoClp;
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
                var tag = GuessTag(r.Cargo, true, i);
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
                var tag = GuessTag(r.Cargo, false, i++);
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
            var ed = new MoCargoEditorVm(c, "Dotación variable según etapa", OnEditorChanged);
            ed.UpdateConLeyes(leyes);
            DirectaEditors.Add(ed);
        }

        foreach (var c in CargosDef.Where(x => !x.EsDirecta))
        {
            var ed = new MoCargoEditorVm(c, "Dotación fija todos los meses", OnEditorChanged);
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
            if (c.Tag is "M1" or "AA" || c.Id is "M1" or "AA")
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
                    var isMaestro = c.Id == "M1" || c.Tag == "M1";
                    var isAyudante = c.Id == "AA" || c.Tag == "AA";
                    dotacionPorCargo[c.Id][i] = isMaestro || isAyudante ? head : c.DotPico;
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
                Tag = c.Id,
                Nombre = c.Nombre,
                EsDirecta = true,
                DotPico = c.DotPico,
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

            dotRows.Add(new MoTableRow
            {
                Tag = c.Id,
                Nombre = c.Nombre,
                EsDirecta = false,
                DotPico = c.DotPico,
                SueldoBaseStr = Fmt(baseCobrada),
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
            SueldoBaseStr = Fmt(basePorMesDir + basePorMesInd),
            FraccionStr = "—",
            LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
            TotalMensualStr = Fmt((basePorMesDir + basePorMesInd) * (1 + leyes)),
            Dotaciones = grandDots,
            TotalStr = Fmt(totalMo),
        });

        DotacionRows = dotRows.ToArray();
        lastTotalMoClp = totalMo;

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
        TotalBarUf = viewInUf ? FmtClp(totalMo) : FmtUf(totalMo, vUf);

        SumBase = Fmt(totalBase);
        SumLeyes = Fmt(totalLeyes);
        SumDirecta = Fmt(totalDir);
        SumIndirecta = Fmt(totalInd);
        SumPromMes = Fmt(mesProm);
        SumRight = $"{FmtUf(totalMo, vUf)}  ·  {FmtClp(mesProm)}/mes";

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
