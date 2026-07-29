# Status Date Specification — Fase 7C-B

## Modelo

`ScheduleStatusDate`: `Id`, `ScheduleId`, `StatusDate`, `TimeZoneId`, `UpdatedUtc`, `UpdatedBy`, `Comment`, `RowVersion`.

## Reglas

1. Una fecha de estado activa por cronograma (UNIQUE `schedule_id`).
2. Puede avanzar o retroceder con permiso `Planning.StatusDateManage`.
3. Cambiarla no modifica baselines.
4. Auditoría UTC (`SetStatusDate`).
5. Zona horaria del proyecto en `TimeZoneId`; la fecha de estado es el ancla de cálculo, no el reloj del sistema.

## Servicio

`IProgressTrackingService.SetStatusDateAsync` / `GetStatusDateAsync`.

Tras establecer fecha de estado, los indicadores (resumen, OOS, forecast, restante) se recalculan al consultarlos.
