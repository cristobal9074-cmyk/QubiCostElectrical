# Progress Rescheduling Specification — Fase 7C-B

## Acción explícita

`RescheduleFromStatusDateAsync` — **nunca** silenciosa.

Permiso: `Planning.ScheduleReschedule`.

## Comportamiento

| Caso | Acción |
|------|--------|
| Completada | Conserva fechas reales; no mueve |
| No iniciada | Mueve inicio planificado a fecha de estado si está en el pasado; recalcula finish forecast |
| En progreso | Conserva `ActualStart`; recalcula `RemainingDuration` y `ForecastFinish` |
| Baseline | **Intacta** |

## Postcondiciones

- Persiste actividades/avances actualizados.
- Invoca CPM (`CalculateScheduleAsync`) si está disponible.
- Audita `Reschedule` con conteo de actividades afectadas.

Motor: `ProgressRescheduleEngine`.
