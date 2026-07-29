# Schedule Baseline Comparison Specification

## Overview

The baseline comparison engine computes a structured diff between two schedule states. It identifies additions, deletions, and modifications across all schedule entities.

---

## Comparison Modes

### Mode 1: Baseline vs. Baseline
```
CompareBaselinesAsync(baselineIdA, baselineIdB)
```
Compares two historical snapshots. Both must exist.

### Mode 2: Baseline vs. Current Schedule
```
CompareWithCurrentAsync(scheduleId, baselineId)
```
Compares a saved baseline against the live schedule state. The current state is assembled on-the-fly from the active repositories.

---

## Comparison Scope

### Activities (Primary)

For each activity in snapshot B (comparison target), the engine checks whether it existed in snapshot A (reference):

| Case | Diff Type | Counted in |
|------|-----------|-----------|
| In B but not in A | `Added` | `AddedActivitiesCount` |
| In A but not in B | `Deleted` | `DeletedActivitiesCount` |
| In both but semantically different | `Modified` | `ModifiedActivitiesCount` |

**Fields compared for modification detection:**

| Field | Diff Property |
|-------|--------------|
| `PlannedDuration.Value` | `DurationDiff` (delta value) |
| `EarlyStart` | `StartOffsetDaysDiff` (delta in calendar days) |
| `EarlyFinish` | `FinishOffsetDaysDiff` (delta in calendar days) |
| `IsCritical` | `CriticalDiff` ("Became Critical" / "No Longer Critical") |
| `ConstraintType` / `ConstraintDate` | `ConstraintDiff` (descriptive string) |

### Dependencies (Secondary)

`DependencyDifferences` list contains `DependencyDiff` entries with:
- `DependencyId`: Identifier
- `DiffType`: `"Added"`, `"Deleted"`, or `"Modified"`
- `PredecessorCodeName` / `SuccessorCodeName`: Activity codes for readability
- `Detail`: Human-readable description of the change

### Calendars, Constraints, Deadlines

These are included in the activity diff when the corresponding activity field changes:
- **Deadline** changes appear as a change in `Activity.Deadline`
- **Calendar** reassignment appears as modified activity (if CalendarId changed)
- **Float/Slack** changes appear when `TotalFloat` differs

---

## Output: `ScheduleComparisonResult`

```csharp
public sealed class ScheduleComparisonResult
{
    public List<ActivityDiff> ActivityDifferences { get; set; } = new();
    public List<DependencyDiff> DependencyDifferences { get; set; } = new();
    public int AddedActivitiesCount { get; set; }
    public int DeletedActivitiesCount { get; set; }
    public int ModifiedActivitiesCount { get; set; }
}
```

### `ActivityDiff`

```csharp
public sealed class ActivityDiff
{
    public ActivityId ActivityId { get; set; }
    public string Code { get; set; }           // Activity code
    public string Name { get; set; }           // Activity name
    public string DiffType { get; set; }       // "Added", "Deleted", "Modified"
    public double? DurationDiff { get; set; }  // Duration delta (positive = longer)
    public double? StartOffsetDaysDiff { get; set; }   // Start offset in calendar days
    public double? FinishOffsetDaysDiff { get; set; }  // Finish offset in calendar days
    public string CriticalDiff { get; set; }   // "No Change", "Became Critical", "No Longer Critical"
    public string ConstraintDiff { get; set; } // Constraint change description
    public string DeadlineDiff { get; set; }   // Deadline change description
}
```

---

## WPF Visualization

The `BaselineManagerView` presents comparison results with color-coded rows:

| Diff Type | Color | Meaning |
|-----------|-------|---------|
| `Added` | 🟢 Green (#4CAF50) | Activity exists only in target |
| `Deleted` | 🔴 Red (#F44336) | Activity no longer exists |
| `Modified` | 🟡 Amber (#FF9800) | Activity changed since baseline |

---

## Performance Characteristics

- Comparison runs in O(n) where n = max(activities in A, activities in B)
- Uses dictionary lookups on `ActivityId` for O(1) membership tests
- Fully in-memory (snapshots deserialized from SQLite JSON)
- No CPM recalculation required during comparison

---

## Determinism

Comparison results are deterministic: given the same two snapshots, the same result is produced regardless of order of iteration, platform, or time.
