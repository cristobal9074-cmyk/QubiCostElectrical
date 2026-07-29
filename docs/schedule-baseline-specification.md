# Schedule Baseline Specification

## Overview

A **Schedule Baseline** is an immutable, stamped snapshot of the full CPM schedule at a point in time. It serves as the official reference plan against which all future progress and deviations are measured. The Qubi Cost Electrical baseline engine is equivalent in scope to the baseline management found in Microsoft Project and Oracle Primavera P6.

---

## Core Concepts

### Baseline
A baseline is metadata that describes the snapshot:

| Property | Type | Description |
|----------|------|-------------|
| `BaselineId` | `string` (UUID) | Unique identifier |
| `ScheduleId` | `ScheduleId` | Parent schedule |
| `Version` | `int` | Sequential version number (0-based) |
| `Name` | `string` | User-provided name |
| `Description` | `string` | Optional description |
| `CreatedUtc` | `DateTime` | UTC timestamp of creation |
| `CreatedBy` | `string` | User who created the baseline |
| `Status` | `"Draft"` \| `"Official"` | Lifecycle status |
| `IsOfficial` | `bool` | True when promoted to official |
| `SHA256` | `string` | SHA-256 hash of snapshot content (64 hex chars) |
| `SnapshotDate` | `DateTime` | Date of the schedule state captured |
| `Notes` | `string` | Optional notes |
| `Metadata` | `string` | JSON-encoded auxiliary metadata |

### Snapshot
A snapshot is the serialized state of the schedule at the baseline moment:

| Property | Type | Description |
|----------|------|-------------|
| `Schedule` | `Schedule` | Full schedule entity |
| `Activities` | `List<Activity>` | All activities including CPM results |
| `Dependencies` | `List<ActivityDependency>` | All FS/SS/FF/SF links with lag/lead |
| `Calendars` | `List<Calendar>` | All calendar definitions |
| `CpmEngineVersion` | `string` | Version of CPM engine used |

---

## Immutability Rules

1. **Snapshots are write-once.** Once saved, the raw content is never overwritten.
2. **Official baselines cannot be deleted.** Only `Draft` baselines can be removed.
3. **Restoration does NOT modify the baseline.** The baseline SHA256, version, and metadata remain unchanged after a restore.
4. **The SHA256 hash is computed deterministically** at creation time and stored. Any subsequent verification re-computes and compares.

---

## Version Numbering

- Version numbers are sequential integers starting at `0`.
- `GetNextVersionAsync()` queries the maximum existing version for the schedule and returns `max + 1` (or `0` if no baselines exist).
- Versions are unique per schedule, not globally.

---

## Lifecycle

```
[Created] → Draft
    ↓
[MarkOfficial] → Official (irreversible)
```

- **Draft:** Can be deleted, restored, and compared.
- **Official:** Cannot be deleted. Can be restored and compared.

---

## SHA-256 Integrity Hash

The `BaselineHasher` computes a deterministic SHA-256 hash over the snapshot content:

1. **Schedule** fields: ID, Name, TimeZoneId, DefaultCalendarId, ProjectStartDate, TargetEndDate
2. **Activities** sorted by `Id.Value`: ID, Code, Name, Type, Status, Duration, EarlyStart, EarlyFinish, TotalFloat, IsCritical, ConstraintType, ConstraintDate
3. **Dependencies** sorted by `Id`: ID, Predecessor, Successor, Type, Lag Value, Lag Unit, IsLead
4. **Calendars** sorted by `Id.Value`: ID, Name, CalendarType

**Volatile fields excluded:** `ModifiedAt`, `RowVersion`, `LastEditor`, `RowVersion`

This guarantees:
- Same input → same hash (deterministic)
- Reversed list order → same hash (sorted before hashing)
- Any semantic change → different hash

---

## Permissions

Baseline operations are gated by the Application permission system through `PlanningPermissions`:

| Operation | Required Permission |
|-----------|---------------------|
| View baselines | `Planning.ScheduleView` |
| Create baseline | `Planning.ScheduleEdit` |
| Mark official | `Planning.ScheduleEdit` |
| Delete draft | `Planning.ScheduleEdit` |
| Restore | `Planning.ScheduleEdit` |
| Verify integrity | `Planning.ScheduleView` |
| Compare | `Planning.ScheduleView` |

---

## Audit Trail

Every baseline operation creates a `PlanningAuditEvent`:

| Operation | Action Code |
|-----------|------------|
| Create baseline | `CreateBaseline` |
| Mark official | `MarkOfficial` |
| Delete draft | `DeleteBaseline` |
| Restore (success) | `RestoreBaseline` |
| Restore (failed integrity) | `RestoreFailed` |
| Integrity mismatch detected | `IntegrityError` |
