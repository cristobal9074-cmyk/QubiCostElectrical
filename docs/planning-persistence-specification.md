# Planning Persistence Specification

This document details the SQLite database schemas, migrations, indices, and transactional controls for the planning engine.

## Database Tables

- **planning_schedules**: Stores root cronogramas.
- **planning_wbs_nodes**: Stores hierarchy tree.
- **planning_activities**: Stores task details, constraints, and links.
- **planning_calendars**: Stores calendar configurations.
- **planning_audit**: Persistent log of modifications.

## Schema Migrations

The planning storage uses transactional migrations (`SQLiteSchemaMigrator`). It maintains a schema integrity history and is versioned up to the latest revision.

## Concurrency Handling

All updates are governed by `RowVersion` (UUID-based concurrency tokens) to prevent writing over stale data (optimistic concurrency).
All mutations are carried out inside transactional limits ensuring complete atomicity.
