using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical;

public static class BaselineHasher
{
    public static string ComputeHash(ScheduleBaselineSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        // Create a deterministic representation of the snapshot
        var sb = new StringBuilder();

        // 1. Schedule details (ignoring RowVersion/ModifiedAt/timestamps)
        var sched = snapshot.Schedule;
        if (sched != null)
        {
            sb.Append($"SCH:{sched.Id.Value}|{sched.Name}|{sched.TimeZoneId}|{sched.DefaultCalendarId.Value}|");
            sb.Append($"{sched.ProjectStartDate?.ToString("yyyyMMddHHmmss")}|{sched.TargetEndDate?.ToString("yyyyMMddHHmmss")};");
        }

        // 2. Activities sorted by ID to be deterministic
        foreach (var act in snapshot.Activities.OrderBy(a => a.Id.Value))
        {
            sb.Append($"ACT:{act.Id.Value}|{act.Code}|{act.Name}|{act.Type}|{act.Status}|");
            sb.Append($"{act.PlannedDuration.Value}|{act.PlannedDuration.Unit}|");
            sb.Append($"{act.EarlyStart?.ToString("yyyyMMddHHmmss")}|{act.EarlyFinish?.ToString("yyyyMMddHHmmss")}|");
            sb.Append($"{act.TotalFloat}|{act.IsCritical}|{act.ConstraintType}|{act.ConstraintDate?.ToString("yyyyMMdd")};");
        }

        // 3. Dependencies sorted by ID
        foreach (var dep in snapshot.Dependencies.OrderBy(d => d.Id))
        {
            sb.Append($"DEP:{dep.Id}|{dep.PredecessorActivityId.Value}|{dep.SuccessorActivityId.Value}|{dep.Type}|{dep.Lag.Value}|{dep.Lag.Unit}|{dep.IsLead};");
        }

        // 4. Calendars sorted by ID
        foreach (var cal in snapshot.Calendars.OrderBy(c => c.Id.Value))
        {
            sb.Append($"CAL:{cal.Id.Value}|{cal.Name}|{cal.CalendarType};");
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] hashBytes = SHA256.HashData(inputBytes);
        
        var hashSb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            hashSb.Append(b.ToString("x2"));
        }
        return hashSb.ToString();
    }
}
