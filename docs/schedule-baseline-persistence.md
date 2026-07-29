# Schedule Baseline Persistence

## Database Schema (Migration 35)

Baseline data is persisted in two SQLite tables added by Migration 35.

---

## Tables

### `planning_baselines`

Stores baseline metadata.

```sql
CREATE TABLE IF NOT EXISTS planning_baselines (
    baseline_id     TEXT NOT NULL PRIMARY KEY,
    schedule_id     TEXT NOT NULL,
    version         INTEGER NOT NULL DEFAULT 0,
    name            TEXT NOT NULL DEFAULT '',
    description     TEXT NOT NULL DEFAULT '',
    created_utc     TEXT NOT NULL,
    created_by      TEXT NOT NULL DEFAULT '',
    status          TEXT NOT NULL DEFAULT 'Draft',
    sha256          TEXT NOT NULL DEFAULT '',
    snapshot_date   TEXT NOT NULL,
    is_official     INTEGER NOT NULL DEFAULT 0,
    notes           TEXT NOT NULL DEFAULT '',
    metadata        TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_planning_baselines_schedule
    ON planning_baselines(schedule_id);
```

### `planning_baseline_snapshots`

Stores serialized snapshot content as JSON.

```sql
CREATE TABLE IF NOT EXISTS planning_baseline_snapshots (
    baseline_id       TEXT NOT NULL PRIMARY KEY
        REFERENCES planning_baselines(baseline_id) ON DELETE CASCADE,
    snapshot_json     TEXT NOT NULL,
    created_utc       TEXT NOT NULL,
    cpm_engine_version TEXT NOT NULL DEFAULT '1.0.0'
);
```

---

## Migration

Migration 35 is executed atomically by `SQLiteSchemaMigrator.MigrateToLatest()`. It is incremental — the migrator applies only migrations that have not yet been applied to the target database.

The `DatabaseSchemaVersion` constant in `Directory.Build.props` is set to `35`.

---

## Repositories

### `SqliteScheduleBaselineRepository`

Implements `IScheduleBaselineRepository` with:

| Method | SQL |
|--------|-----|
| `SaveAsync` | `INSERT OR REPLACE INTO planning_baselines` |
| `GetByIdAsync` | `SELECT ... WHERE baseline_id = ?` |
| `GetByScheduleAsync` | `SELECT ... WHERE schedule_id = ?` |
| `DeleteAsync` | `DELETE FROM planning_baselines WHERE baseline_id = ?` |
| `ExistsAsync` | `SELECT 1 FROM planning_baselines WHERE baseline_id = ?` |
| `GetNextVersionAsync` | `SELECT MAX(version) FROM planning_baselines WHERE schedule_id = ?` |

### `SqliteScheduleBaselineSnapshotRepository`

Implements `IScheduleBaselineSnapshotRepository` with:

| Method | SQL |
|--------|-----|
| `SaveAsync` | `INSERT OR REPLACE INTO planning_baseline_snapshots` |
| `GetByBaselineIdAsync` | `SELECT snapshot_json WHERE baseline_id = ?` → JSON deserialized |
| `DeleteAsync` | `DELETE FROM planning_baseline_snapshots WHERE baseline_id = ?` |

---

## Serialization

Snapshots are serialized to JSON using `System.Text.Json` with:
- `Schedule`, `Activities`, `Dependencies`, `Calendars` all included
- Volatile fields (`RowVersion`, `ModifiedAt`) preserved in JSON but excluded from hash computation
- JSON stored as TEXT in `snapshot_json` column

---

## Referential Integrity

The snapshot table has a `FOREIGN KEY` constraint referencing `planning_baselines(baseline_id) ON DELETE CASCADE`. This ensures:
- A snapshot cannot exist without its parent baseline
- Deleting a baseline automatically removes its snapshot

---

## Reopening

When a `.qce` project file is reopened:
1. `SQLiteSchemaMigrator.MigrateToLatest()` runs — no-op if already at version 35
2. `SqliteScheduleBaselineRepository` and `SqliteScheduleBaselineSnapshotRepository` are instantiated with the project path
3. All baselines for the project's schedules are available immediately

---

## Concurrency

- Each `*Repository` opens a new SQLite connection per operation
- SQLite WAL mode is enabled for the project file
- Simultaneous reads are safe; writes serialize naturally via SQLite locking
- No optimistic concurrency conflicts on baseline data (baselines are append-only in practice)
