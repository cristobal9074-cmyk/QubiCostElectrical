# Progress Tracking Specification — Fase 7C-B

## Objetivo

Seguimiento profesional del cronograma con **fecha de estado explícita**, separando Plan · Baseline · Actual · Forecast.

## Principios

- Baselines inmutables (Fase 7C-A).
- No sobrescribir campos planificados con datos reales.
- No usar `DateTime.Now` como fuente de verdad de avance.
- `decimal` para porcentajes y costos.
- Fórmulas en Domain/Application; WPF solo captura y presenta.

## Entidades

| Entidad | Rol |
|----------|-----|
| `ScheduleStatusDate` | Una fecha de estado por cronograma |
| `ActivityProgressUpdate` | Avance real por actividad |
| `ProgressInterval` | Pausas/reanudaciones (split) |
| `DelayReasonEntry` | Causas tipadas de atraso |
| `ProgressEvidence` | Referencias a evidencias |
| `ProgressSnapshot` | Congelación de seguimiento + SHA-256 |

## Estados de seguimiento

`NotStarted`, `Started`, `InProgress`, `Suspended`, `Completed`, `Cancelled`, `Blocked`, `Delayed`, `Ahead`, `OnTrack`, `NotApplicable`

Derivados por `ProgressStatusDeriver` a partir de datos reales y fecha de estado.

## Validaciones clave

Ver `ProgressValidation`: rangos 0–100, término ≥ inicio, sin término sin inicio, 100% al completar, cantidad/unidad/sobreavance, costos no negativos, disminución de % con comentario.

## Motores

- `ProgressCalculationEngine` — % / físico / financiero / productividad
- `RemainingDurationCalculator` — restante y EAC de duración
- `OutOfSequenceDetector` — FS/SS/FF/restricción/calendario
- `ProgressRescheduleEngine` — reprogramación **explícita** del restante
- `ProgressForecastEngine` — forecast básico de fechas
- `ScheduleProgressSummaryCalculator` — resumen ponderado
- `ProgressSnapshotHasher` / `ProgressComparisonEngine`

## Schema SQLite

Migraciones **36–38** (`planning_status_dates`, `planning_activity_progress`, `planning_progress_intervals`, snapshots, delay/evidence).
