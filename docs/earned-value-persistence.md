# Earned Value Persistence — Fase 7C-C

## Schema v39

| Tabla | Uso |
|-------|-----|
| `planning_earned_value` | Último resultado por `schedule_id` |
| `planning_earned_value_snapshots` | Snapshots versionados + SHA-256 |
| `planning_earned_value_history` | Historial append-only de cálculos |

Migración: `Migration039_PlanningEarnedValue`.

Repositorios SQLite: `SqliteEarnedValueRepository`, `SqliteEarnedValueSnapshotRepository`, `SqliteEarnedValueHistoryRepository`.
