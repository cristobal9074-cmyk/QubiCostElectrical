using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Gestión de baseline y snapshot para IPageWithPendingChanges e IPageWithCommitChanges en Cubicación.
/// </summary>
internal sealed class CubicacionBaselineCoordinator
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;

    private List<CubicacionRow>? baselineCubicacion;
    private List<CubicacionRecursoRow>? baselineRecursos;
    private List<CubicacionZonaCantidadRow>? baselineZonas;
    private List<CubicacionValorizacionRow>? baselineValorizaciones;

    public CubicacionBaselineCoordinator(CubicacionViewModel parent, BudgetProjectSession session)
    {
        this.parent = parent;
        this.session = session;
    }

    public ProjectFileData? CaptureSnapshot() => session.IsReadOnly ? null : session.CreateSnapshot();

    public bool TryDiscardChanges()
    {
        if (!parent.HasPendingChanges)
            return true;

        RestoreBaseline();
        parent.HasPendingChanges = false;
        parent.SaveStatusText = "Cambios descartados";
        parent.Refresh();
        return true;
    }

    public bool TryApplyChanges()
    {
        if (session.IsReadOnly)
            return !parent.HasPendingChanges;

        if (!session.IsDirty)
        {
            using (parent.SuppressSessionReload())
                session.MarkDirty(ProjectDirtyScope.Cubicacion);
        }

        CaptureBaseline();
        parent.MarkClean();
        return true;
    }

    public void ScheduleCaptureBaseline()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            if (session.Cubicacion.Count <= 2_000)
                CaptureBaseline();
            return;
        }

        _ = dispatcher.InvokeAsync(() =>
        {
            if (!parent.HasPendingChanges)
                CaptureBaseline();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    public void CaptureBaseline()
    {
        baselineCubicacion = CloneJson(session.Cubicacion.ToList());
        baselineRecursos = CloneJson(session.CubicacionRecursos.ToList());
        baselineZonas = CloneJson(session.CubicacionZonaCantidades.ToList());
        baselineValorizaciones = CloneJson(session.CubicacionValorizaciones.ToList());
    }

    private void RestoreBaseline()
    {
        if (baselineCubicacion == null)
            return;

        ReplaceList(session.Cubicacion, CloneJson(baselineCubicacion));
        if (baselineRecursos != null)
            ReplaceList(session.CubicacionRecursos, CloneJson(baselineRecursos));
        if (baselineZonas != null)
            ReplaceList(session.CubicacionZonaCantidades, CloneJson(baselineZonas));
        if (baselineValorizaciones != null)
            ReplaceList(session.CubicacionValorizaciones, CloneJson(baselineValorizaciones));
    }

    private static void ReplaceList<T>(List<T> target, List<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private static T CloneJson<T>(T source)
    {
        var json = JsonSerializer.Serialize(source, BaselineJsonOptions);
        return JsonSerializer.Deserialize<T>(json, BaselineJsonOptions)
               ?? throw new InvalidOperationException($"No se pudo clonar {typeof(T).Name} para baseline Cubicación.");
    }
}
