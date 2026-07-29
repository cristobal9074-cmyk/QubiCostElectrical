# Executive Dashboard Specification — Fase 7C-D

## Objetivo

Consolidar planificación, seguimiento y EVM en un dashboard ejecutivo sin recalcular motores.

## Fuentes (solo lectura)

- Baseline oficial (`IScheduleBaselineRepository`)
- Fecha de estado / avance (`IStatusDateRepository`, `IActivityProgressRepository`, `IProgressTrackingService`)
- EVM (`IEarnedValueService.GetLatestAsync` / historial)
- Actividades / WBS / CPM ya persistidos

## Salida

`ProjectExecutiveDashboard`: KPIs, series Curva S, alertas, desviaciones, WBS, responsables, filas operativas, calidad de dato, hashes fuente.

## UI

- Planificación → **Dashboard ejecutivo** (`ShellRoute.PlanningExecutiveDashboard`)
- Planificación → **Dashboard operativo** (`ShellRoute.PlanningOperationalDashboard`)
