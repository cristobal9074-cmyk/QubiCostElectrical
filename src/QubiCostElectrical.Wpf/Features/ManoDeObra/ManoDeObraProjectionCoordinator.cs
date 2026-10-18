using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

/// <summary>
/// Coordina la lógica de tareas de proyección de mano de obra (curvas, distribución
/// por peso/paridad a cargos directos, promedios mensuales y persistencia).
/// </summary>
internal sealed class ManoDeObraProjectionCoordinator
{
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

    private readonly ManoDeObraViewModel _vm;
    private readonly List<MoProjectionTaskVm> _projectionTasks = new();
    private readonly HashSet<int> _pendingProjectionMonths = new();
    private bool _projectionPersistPending;

    public ManoDeObraProjectionCoordinator(ManoDeObraViewModel vm)
    {
        _vm = vm;
    }

    public bool IsApplyingProjection { get; private set; }

    public MoProjectionTaskVm[] ProjectionTasks
    {
        get
        {
            FitProjectionMonths();
            return _projectionTasks.ToArray();
        }
    }

    public void LoadProjectionTasks()
    {
        _projectionTasks.Clear();
        var saved = _vm.Session.Data.MoProjectionTasks;
        if (saved != null)
        {
            foreach (var row in saved)
            {
                var cantidades = row.CantidadesMensuales
                    .Take(_vm.TotalMeses)
                    .Concat(Enumerable.Repeat(0, _vm.TotalMeses))
                    .Take(_vm.TotalMeses)
                    .Select(v => Math.Max(0, v))
                    .ToArray();
                _projectionTasks.Add(new MoProjectionTaskVm
                {
                    Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id,
                    Nombre = string.IsNullOrWhiteSpace(row.Nombre) ? "Nueva tarea" : row.Nombre.Trim(),
                    Cantidades = cantidades
                });
            }
        }

        if (_projectionTasks.Count == 0 || IsPlaceholderProjection(_projectionTasks))
        {
            _projectionTasks.Clear();
            foreach (var name in DefaultProjectionPartidas)
                AddProjectionTaskCore(name);
            PersistProjectionTasks();
        }

        _vm.TriggerPropertyChanged(nameof(_vm.ProjectionTasks));
    }

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

    public void FitProjectionMonths()
    {
        var totalMeses = _vm.TotalMeses;
        if (totalMeses <= 0)
            return;
        var changed = false;
        foreach (var task in _projectionTasks)
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
            _vm.TriggerPropertyChanged(nameof(_vm.ProjectionTasks));
            PersistProjectionTasks();
        }
    }

    public void AddProjectionTask(string? name = null)
    {
        if (!_vm.CanEdit)
            return;
        var label = string.IsNullOrWhiteSpace(name)
            ? $"Partida {_projectionTasks.Count + 1}"
            : name.Trim();
        AddProjectionTaskCore(label);
        PersistProjectionTasks();
    }

    public void RemoveProjectionTask(string id)
    {
        if (!_vm.CanEdit || string.IsNullOrWhiteSpace(id))
            return;
        FlushProjectionEdits();
        var before = MonthlyProjectionTotals();
        if (_projectionTasks.RemoveAll(t => t.Id == id) == 0)
            return;
        _vm.TriggerPropertyChanged(nameof(_vm.ProjectionTasks));
        PersistProjectionTasks();
        PushChangedProjectionMonths(before);
    }

    public void RenameProjectionTask(string id, string name)
    {
        if (!_vm.CanEdit)
            return;
        var task = _projectionTasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
            return;
        var label = string.IsNullOrWhiteSpace(name) ? "Partida" : name.Trim();
        if (task.Nombre == label)
            return;
        task.Nombre = label;
        _vm.TriggerPropertyChanged(nameof(_vm.ProjectionTasks));
        PersistProjectionTasks();
    }

    public bool SetProjectionQuantity(string id, int monthIndex, int value)
    {
        if (!_vm.CanEdit)
            return false;
        var task = _projectionTasks.FirstOrDefault(t => t.Id == id);
        if (task == null || monthIndex < 0)
            return false;

        var totalMeses = _vm.TotalMeses;
        if (monthIndex >= task.Cantidades.Length)
        {
            var targetLen = Math.Max(monthIndex + 1, totalMeses);
            var next = new int[targetLen];
            Array.Copy(task.Cantidades, next, task.Cantidades.Length);
            task.Cantidades = next;
        }

        value = Math.Max(0, value);
        if (task.Cantidades[monthIndex] == value)
            return false;
        task.Cantidades[monthIndex] = value;
        _pendingProjectionMonths.Add(monthIndex);
        _projectionPersistPending = true;
        return true;
    }

    public void FlushProjectionEdits()
    {
        if (!_projectionPersistPending && _pendingProjectionMonths.Count == 0)
            return;

        var months = _pendingProjectionMonths.Count == 0
            ? Array.Empty<int>()
            : new int[_pendingProjectionMonths.Count];
        if (months.Length > 0)
            _pendingProjectionMonths.CopyTo(months);
        _pendingProjectionMonths.Clear();
        var persistTasks = _projectionPersistPending;
        _projectionPersistPending = false;

        if (persistTasks)
            PersistProjectionTasks();
        if (months.Length > 0)
            PushProjectionMonths(months);
    }

    public int[] MonthlyProjectionTotals()
    {
        var totalMeses = _vm.TotalMeses;
        var totals = new int[totalMeses];
        for (var i = 0; i < totalMeses; i++)
            totals[i] = ProjectionMonthTotal(i);
        return totals;
    }

    public bool ApplySavedProjectionToDirecta()
    {
        var totalMeses = _vm.TotalMeses;
        if (totalMeses <= 0)
            return false;
        var months = Enumerable.Range(0, totalMeses).ToList();
        return WriteProjectionMonths(months);
    }

    public void PushChangedProjectionMonths(int[] before)
    {
        var totalMeses = _vm.TotalMeses;
        var months = new List<int>();
        for (var i = 0; i < totalMeses && i < before.Length; i++)
        {
            if (ProjectionMonthTotal(i) != before[i])
                months.Add(i);
        }
        PushProjectionMonths(months);
    }

    public void PushProjectionMonths(IReadOnlyList<int> months)
    {
        if (months.Count == 0 || !WriteProjectionMonths(months))
            return;
        IsApplyingProjection = true;
        try
        {
            _vm.TriggerRecalcular();
            _vm.TriggerPersistToSession();
        }
        finally
        {
            IsApplyingProjection = false;
        }
    }

    public bool WriteProjectionMonths(IReadOnlyList<int> months)
    {
        var directa = _vm.CargosDef.Where(c => c.EsDirecta).ToArray();
        if (directa.Length == 0)
            return false;

        var totalMeses = _vm.TotalMeses;
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
                if (_vm.CellOverrides.TryGetValue(key, out var current) && current == shares[i])
                    continue;
                _vm.CellOverrides[key] = shares[i];
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
        => _projectionTasks.Sum(t => monthIndex >= 0 && monthIndex < t.Cantidades.Length ? t.Cantidades[monthIndex] : 0);

    public double ProjectionMonthlyAverage()
    {
        var totalMeses = _vm.TotalMeses;
        if (totalMeses <= 0)
            return 0;
        double sum = 0;
        for (var i = 0; i < totalMeses; i++)
            sum += ProjectionMonthTotal(i);
        return sum / totalMeses;
    }

    public int ProjectionMonthlyAverageRounded()
    {
        var avg = ProjectionMonthlyAverage();
        return avg <= 0 ? 0 : (int)Math.Round(avg, MidpointRounding.AwayFromZero);
    }

    public bool ProjectionMonthIsPeak(int monthIndex)
        => monthIndex >= 0 && monthIndex < _vm.Meses.Length && _vm.Meses[monthIndex].EsPeak;

    public void AddProjectionTaskCore(string name)
    {
        _projectionTasks.Add(new MoProjectionTaskVm
        {
            Id = Guid.NewGuid().ToString("N"),
            Nombre = name,
            Cantidades = new int[_vm.TotalMeses]
        });
        _vm.TriggerPropertyChanged(nameof(_vm.ProjectionTasks));
    }

    public void PersistProjectionTasks()
    {
        if (!_vm.Workspace.HasOpenProject || _vm.Session.IsReadOnly)
            return;

        var data = _vm.Session.Data;
        data.MoProjectionTasks = _projectionTasks.Select(t => new MoProjectionTaskRow
        {
            Id = t.Id,
            Nombre = t.Nombre,
            CantidadesMensuales = t.Cantidades
        }).ToList();
        _vm.Session.MarkDirty("mo_projection_tasks");
    }

    public void ExecuteTakeProjection()
    {
        if (!_vm.CanEdit)
            return;
        FlushProjectionEdits();
        if (!_projectionTasks.Any(t => t.Cantidades.Any(c => c > 0)))
        {
            ShellNotificationService.Instance.ShowWarning(
                "Proyección MO",
                "La proyección no tiene trabajadores asignados: carga los meses en el botón Proyección MO.");
            return;
        }

        ApplySavedProjectionToDirecta();
        _vm.TriggerRecalcular();
        _vm.TriggerPersistToSession();
        ShellNotificationService.Instance.ShowSuccess(
            "Proyección MO",
            "Dotación directa actualizada con los totales de la proyección.");
    }

    public void ExecuteApplyAverageProjection()
    {
        if (!_vm.CanEdit)
            return;
        FlushProjectionEdits();
        if (!_projectionTasks.Any(t => t.Cantidades.Any(c => c > 0)))
        {
            ShellNotificationService.Instance.ShowWarning(
                "Proyección MO",
                "La proyección no tiene trabajadores asignados: carga los meses en el botón Proyección MO.");
            return;
        }

        var avgRounded = ProjectionMonthlyAverageRounded();
        if (avgRounded <= 0)
            return;

        var directa = _vm.CargosDef.Where(c => c.EsDirecta).ToArray();
        if (directa.Length == 0)
            return;

        var weights = directa.Select(c => Math.Max(0, c.DotPico)).ToArray();
        if (weights.All(w => w <= 0))
            weights = directa.Select(_ => 1).ToArray();

        var totalMeses = _vm.TotalMeses;
        for (var monthIndex = 0; monthIndex < totalMeses; monthIndex++)
        {
            var shares = SplitByWeightWithParity(avgRounded, weights, monthIndex);
            for (var i = 0; i < directa.Length; i++)
            {
                var key = $"{directa[i].Id}_{monthIndex}";
                _vm.CellOverrides[key] = shares[i];
            }
        }

        _vm.InternalPeakHeadcount = avgRounded;
        _vm.TriggerPropertyChanged(nameof(_vm.PeakHeadcount));
        _vm.TriggerRecalcular();
        _vm.TriggerPersistToSession();
        ShellNotificationService.Instance.ShowSuccess(
            "Proyección MO",
            $"Promedio de proyección ({avgRounded} pers./mes) aplicado a la dotación directa.");
    }

    private static int[] SplitByWeightWithParity(int total, int[] weights, int parityOffset)
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
            {
                var idx = (i + parityOffset) % count;
                result[idx] = each + (i < extra ? 1 : 0);
            }
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
            .ThenBy(i => (i + parityOffset) % count)
            .ToArray();
        for (var k = 0; k < left; k++)
            result[order[k]]++;
        return result;
    }
}
