namespace QubiCostElectrical;

public sealed class ProjectAuditRecorder
{
    public const int MaxEntries = 3000;

    private readonly BudgetProjectSession session;
    private CollaborationUserContext user = CollaborationUserContext.LocalFallback();
    private bool enabled;

    public ProjectAuditRecorder(BudgetProjectSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        session.Changed += OnSessionChanged;
        // "Guardado" se registra en Saving (antes del snapshot), no en Saved (después
        // de escribir): grabarla en Saved dejaba la entrada solo en memoria y nunca
        // llegaba al .qce.
        session.Saving += OnSessionSaving;
    }

    public void SetUser(CollaborationUserContext context)
        => user = context ?? CollaborationUserContext.LocalFallback();

    public void Enable() => enabled = true;

    public void Disable() => enabled = false;

    public void RecordLifecycle(string action, string detail = "")
    {
        if (!enabled)
            return;

        Append(new ProjectAuditEntryRow
        {
            OccurredAt = AppClock.Now,
            UserId = user.UserId,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            MachineName = user.MachineName,
            Action = action,
            Detail = detail
        });
    }

    public void RecordPartidaChange(string itemId, string field, string oldValue, string newValue, string origin = "Presupuesto")
    {
        if (!enabled || string.Equals(oldValue, newValue, StringComparison.Ordinal))
            return;

        var timeline = new PartidaChangeTimelineRow
        {
            ItemId = itemId,
            Fecha = AppClock.Now,
            Usuario = user.DisplayName,
            Campo = field,
            ValorAnterior = oldValue,
            ValorNuevo = newValue,
            Origen = origin
        };
        session.PartidaTimeline.Add(timeline);

        Append(new ProjectAuditEntryRow
        {
            OccurredAt = timeline.Fecha,
            UserId = user.UserId,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            MachineName = user.MachineName,
            Action = "CambioPartida",
            Entity = "Itemizado",
            EntityId = itemId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Detail = origin
        });
    }

    public IReadOnlyList<ProjectAuditEntryRow> GetRecentEntries(int count = 200)
    {
        var log = session.ProjectInfo.AuditLog;
        if (log.Count == 0)
            return Array.Empty<ProjectAuditEntryRow>();

        int take = Math.Min(count, log.Count);
        return log
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToArray();
    }

    private void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (!enabled || e.Kind == ProjectSessionChangeKind.Saved || e.Kind == ProjectSessionChangeKind.Reset)
            return;

        Append(new ProjectAuditEntryRow
        {
            OccurredAt = e.TimestampUtc.ToLocalTime(),
            UserId = user.UserId,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            MachineName = user.MachineName,
            Action = "Edicion",
            Entity = e.Kind.ToString(),
            EntityId = e.Scope,
            Detail = e.Reason
        });
    }

    private void OnSessionSaving(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (!enabled)
            return;

        RecordLifecycle("Guardado", $"Proyecto persistido · {session.CurrentProjectPath}");
    }

    private void Append(ProjectAuditEntryRow entry)
    {
        var log = session.ProjectInfo.AuditLog;
        log.Add(entry);
        if (log.Count > MaxEntries)
        {
            int remove = log.Count - MaxEntries;
            log.RemoveRange(0, remove);
        }
    }
}
