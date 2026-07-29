using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public sealed class CpmCalculationEngine
{

    public CpmResult Calculate(
        DateTime projectStartDate,
        DateTime? targetEndDate,
        List<Activity> activities,
        List<ActivityDependency> dependencies,
        ICpmCalendarCalculator calculator,
        CancellationToken ct = default)
    {
        var result = new CpmResult();
        if (activities == null || activities.Count == 0)
        {
            result.Success = true;
            return result;
        }

        ct.ThrowIfCancellationRequested();

        // 1. Detección de ciclos y ordenación topológica
        var adj = new Dictionary<ActivityId, List<ActivityDependency>>();
        var inDegree = new Dictionary<ActivityId, int>();

        foreach (var act in activities)
        {
            adj[act.Id] = new List<ActivityDependency>();
            inDegree[act.Id] = 0;
        }

        foreach (var dep in dependencies)
        {
            if (adj.ContainsKey(dep.PredecessorActivityId) && adj.ContainsKey(dep.SuccessorActivityId))
            {
                adj[dep.PredecessorActivityId].Add(dep);
                inDegree[dep.SuccessorActivityId]++;
            }
        }

        // Cycle detection using DFS (visited states: 0 = unvisited, 1 = visiting, 2 = visited)
        var visited = new Dictionary<ActivityId, int>();
        var pathStack = new List<ActivityId>();
        bool hasCycle = false;
        List<ActivityId> cycleNodes = null!;

        foreach (var act in activities)
        {
            visited[act.Id] = 0;
        }

        bool Dfs(ActivityId u)
        {
            visited[u] = 1;
            pathStack.Add(u);

            foreach (var dep in adj[u])
            {
                var v = dep.SuccessorActivityId;
                if (visited[v] == 1)
                {
                    hasCycle = true;
                    cycleNodes = pathStack.SkipWhile(n => !n.Equals(v)).ToList();
                    cycleNodes.Add(v); // Close cycle
                    return true;
                }
                else if (visited[v] == 0)
                {
                    if (Dfs(v)) return true;
                }
            }

            pathStack.RemoveAt(pathStack.Count - 1);
            visited[u] = 2;
            return false;
        }

        foreach (var act in activities)
        {
            if (visited[act.Id] == 0)
            {
                if (Dfs(act.Id)) break;
            }
        }

        if (hasCycle)
        {
            result.Success = false;
            result.ErrorMessage = "Ciclo detectado en el cronograma.";
            result.CyclePath = cycleNodes.Select(n => n.ToString()).ToList();
            return result;
        }

        ct.ThrowIfCancellationRequested();

        // Topological Sort using Kahn's algorithm (ensures determinism)
        var q = new List<Activity>();
        foreach (var act in activities.OrderBy(a => a.Code).ThenBy(a => a.Id.Value))
        {
            if (inDegree[act.Id] == 0)
            {
                q.Add(act);
            }
        }

        var topologicalOrder = new List<Activity>();
        while (q.Count > 0)
        {
            // Deterministic selection
            q = q.OrderBy(a => a.Code).ThenBy(a => a.Id.Value).ToList();
            var u = q[0];
            q.RemoveAt(0);
            topologicalOrder.Add(u);

            foreach (var dep in adj[u.Id])
            {
                var vId = dep.SuccessorActivityId;
                inDegree[vId]--;
                if (inDegree[vId] == 0)
                {
                    var v = activities.First(a => a.Id.Equals(vId));
                    q.Add(v);
                }
            }
        }

        // Initialize ES and EF
        var es = new Dictionary<ActivityId, DateTime>();
        var ef = new Dictionary<ActivityId, DateTime>();

        foreach (var act in activities)
        {
            es[act.Id] = projectStartDate;
            ef[act.Id] = act.Type == ActivityType.Milestone 
                ? projectStartDate 
                : calculator.AddDuration(projectStartDate, act.PlannedDuration, act.CalendarId);
        }

        ct.ThrowIfCancellationRequested();

        // 2. Forward Pass
        var incomingDeps = new Dictionary<ActivityId, List<ActivityDependency>>();
        foreach (var act in activities) incomingDeps[act.Id] = new List<ActivityDependency>();
        foreach (var dep in dependencies)
        {
            if (incomingDeps.ContainsKey(dep.SuccessorActivityId))
                incomingDeps[dep.SuccessorActivityId].Add(dep);
        }

        foreach (var act in topologicalOrder)
        {
            var myEs = projectStartDate;
            var deps = incomingDeps[act.Id];

            if (deps.Count > 0)
            {
                foreach (var dep in deps)
                {
                    var predId = dep.PredecessorActivityId;
                    var predEs = es[predId];
                    var predEf = ef[predId];

                    DateTime calculated = projectStartDate;
                    if (dep.Type == DependencyType.FinishToStart)
                    {
                        calculated = dep.IsLead 
                            ? calculator.SubtractDuration(predEf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                            : calculator.AddDuration(predEf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    }
                    else if (dep.Type == DependencyType.StartToStart)
                    {
                        calculated = dep.IsLead 
                            ? calculator.SubtractDuration(predEs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                            : calculator.AddDuration(predEs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    }

                    if (calculated > myEs) myEs = calculated;
                }
            }

            es[act.Id] = myEs;

            // Apply FF and SF logic (they directly determine EF)
            var myEf = act.Type == ActivityType.Milestone ? myEs : calculator.AddDuration(myEs, act.PlannedDuration, act.CalendarId);

            foreach (var dep in deps)
            {
                var predId = dep.PredecessorActivityId;
                var predEs = es[predId];
                var predEf = ef[predId];

                if (dep.Type == DependencyType.FinishToFinish)
                {
                    var calculated = dep.IsLead 
                        ? calculator.SubtractDuration(predEf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                        : calculator.AddDuration(predEf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    if (calculated > myEf) myEf = calculated;
                }
                else if (dep.Type == DependencyType.StartToFinish)
                {
                    var calculated = dep.IsLead 
                        ? calculator.SubtractDuration(predEs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                        : calculator.AddDuration(predEs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    if (calculated > myEf) myEf = calculated;
                }
            }

            // Apply forward constraints
            if (act.ConstraintType == ConstraintType.StartNoEarlierThan && act.ConstraintDate.HasValue)
            {
                if (act.ConstraintDate.Value > myEs) myEs = act.ConstraintDate.Value;
                myEf = act.Type == ActivityType.Milestone ? myEs : calculator.AddDuration(myEs, act.PlannedDuration, act.CalendarId);
            }
            if (act.ConstraintType == ConstraintType.FinishNoEarlierThan && act.ConstraintDate.HasValue)
            {
                if (act.ConstraintDate.Value > myEf)
                {
                    myEf = act.ConstraintDate.Value;
                    if (act.Type != ActivityType.Milestone)
                        myEs = calculator.SubtractDuration(myEf, act.PlannedDuration, act.CalendarId);
                }
            }
            if (act.ConstraintType == ConstraintType.MustStartOn && act.ConstraintDate.HasValue)
            {
                myEs = act.ConstraintDate.Value;
                myEf = act.Type == ActivityType.Milestone ? myEs : calculator.AddDuration(myEs, act.PlannedDuration, act.CalendarId);
            }
            if (act.ConstraintType == ConstraintType.MustFinishOn && act.ConstraintDate.HasValue)
            {
                myEf = act.ConstraintDate.Value;
                if (act.Type != ActivityType.Milestone)
                    myEs = calculator.SubtractDuration(myEf, act.PlannedDuration, act.CalendarId);
                else
                    myEs = myEf;
            }

            es[act.Id] = myEs;
            ef[act.Id] = myEf;
        }

        ct.ThrowIfCancellationRequested();

        // 3. Backward Pass
        var ls = new Dictionary<ActivityId, DateTime>();
        var lf = new Dictionary<ActivityId, DateTime>();

        var maxEf = ef.Values.Max();
        var defaultLf = targetEndDate ?? maxEf;

        foreach (var act in activities)
        {
            lf[act.Id] = defaultLf;
            ls[act.Id] = act.Type == ActivityType.Milestone 
                ? defaultLf 
                : calculator.SubtractDuration(defaultLf, act.PlannedDuration, act.CalendarId);
        }

        var successorDeps = new Dictionary<ActivityId, List<ActivityDependency>>();
        foreach (var act in activities) successorDeps[act.Id] = new List<ActivityDependency>();
        foreach (var dep in dependencies)
        {
            if (successorDeps.ContainsKey(dep.PredecessorActivityId))
                successorDeps[dep.PredecessorActivityId].Add(dep);
        }

        for (int i = topologicalOrder.Count - 1; i >= 0; i--)
        {
            var act = topologicalOrder[i];
            var myLf = defaultLf;
            var deps = successorDeps[act.Id];

            if (deps.Count > 0)
            {
                foreach (var dep in deps)
                {
                    var succId = dep.SuccessorActivityId;
                    var succLs = ls[succId];
                    var succLf = lf[succId];

                    DateTime calculated = defaultLf;
                    if (dep.Type == DependencyType.FinishToStart)
                    {
                        calculated = dep.IsLead 
                            ? calculator.AddDuration(succLs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                            : calculator.SubtractDuration(succLs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    }
                    else if (dep.Type == DependencyType.FinishToFinish)
                    {
                        calculated = dep.IsLead 
                            ? calculator.AddDuration(succLf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                            : calculator.SubtractDuration(succLf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    }

                    if (calculated < myLf) myLf = calculated;
                }
            }

            lf[act.Id] = myLf;
            var myLs = act.Type == ActivityType.Milestone ? myLf : calculator.SubtractDuration(myLf, act.PlannedDuration, act.CalendarId);

            // Apply StartToStart and StartToFinish restrictions in backward pass
            foreach (var dep in deps)
            {
                var succId = dep.SuccessorActivityId;
                var succLs = ls[succId];
                var succLf = lf[succId];

                if (dep.Type == DependencyType.StartToStart)
                {
                    var calculated = dep.IsLead 
                        ? calculator.AddDuration(succLs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                        : calculator.SubtractDuration(succLs, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    if (calculated < myLs) myLs = calculated;
                }
                else if (dep.Type == DependencyType.StartToFinish)
                {
                    var calculated = dep.IsLead 
                        ? calculator.AddDuration(succLf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId)) 
                        : calculator.SubtractDuration(succLf, dep.Lag, dep.LagCalendarId == string.Empty ? act.CalendarId : new CalendarId(dep.LagCalendarId));
                    if (calculated < myLs) myLs = calculated;
                }
            }

            // Apply backward constraints
            if (act.ConstraintType == ConstraintType.StartNoLaterThan && act.ConstraintDate.HasValue)
            {
                if (act.ConstraintDate.Value < myLs)
                {
                    myLs = act.ConstraintDate.Value;
                    if (act.Type != ActivityType.Milestone)
                        myLf = calculator.AddDuration(myLs, act.PlannedDuration, act.CalendarId);
                }
            }
            if (act.ConstraintType == ConstraintType.FinishNoLaterThan && act.ConstraintDate.HasValue)
            {
                if (act.ConstraintDate.Value < myLf) myLf = act.ConstraintDate.Value;
                myLs = act.Type == ActivityType.Milestone ? myLf : calculator.SubtractDuration(myLf, act.PlannedDuration, act.CalendarId);
            }
            if (act.ConstraintType == ConstraintType.MustStartOn && act.ConstraintDate.HasValue)
            {
                myLs = act.ConstraintDate.Value;
                myLf = act.Type == ActivityType.Milestone ? myLs : calculator.AddDuration(myLs, act.PlannedDuration, act.CalendarId);
            }
            if (act.ConstraintType == ConstraintType.MustFinishOn && act.ConstraintDate.HasValue)
            {
                myLf = act.ConstraintDate.Value;
                if (act.Type != ActivityType.Milestone)
                    myLs = calculator.SubtractDuration(myLf, act.PlannedDuration, act.CalendarId);
                else
                    myLs = myLf;
            }

            ls[act.Id] = myLs;
            lf[act.Id] = myLf;
        }

        ct.ThrowIfCancellationRequested();
        foreach (var act in activities)
        {
            var myEs = es[act.Id];
            var myEf = ef[act.Id];
            var myLs = ls[act.Id];
            var myLf = lf[act.Id];
            var tf = calculator.SubtractDates(myEs, myLs, act.CalendarId).Value;
            double ff = 0;
            var succs = successorDeps[act.Id];
            if (succs.Count > 0)
            {
                var minSuccEs = succs.Select(d => es[d.SuccessorActivityId]).Min();
                ff = calculator.SubtractDates(myEf, minSuccEs, act.CalendarId).Value;
                if (ff < 0) ff = 0;
            }
            result.CalculatedDates[act.Id] = new ActivityCalculatedDates
            {
                ActivityId = act.Id,
                EarlyStart = myEs,
                EarlyFinish = myEf,
                LateStart = myLs,
                LateFinish = myLf,
                TotalFloat = tf,
                FreeFloat = ff,
                IsCritical = tf <= 0.001
            };
        }
        result.Success = true;
        return result;
    }
}
