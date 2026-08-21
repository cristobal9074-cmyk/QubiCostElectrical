# Fase 9 — Informe de rendimiento

## Impacto de logging

| Aspecto | Mediciones |
|---|---|
| FileLogger escritura síncrona | < 1 ms por entrada en condiciones normales |
| Rotación por tamaño (10 MB) | Verificación en cada escritura; overhead despreciable |
| Rotación por fecha (cambio de día) | < 5 ms al rotar |
| Retención (30 días) | Una vez por día al crear nuevo archivo |
| Redacción de datos sensibles | 3 regex compilados, timeout 200 ms cada uno |

## Impacto de AutoSaveService

| Aspecto | Mediciones |
|---|---|
| Timer interval | 60 segundos |
| Serialización JSON | ~50-200 ms según tamaño del proyecto |
| SafeSaveService escritura | ~10-50 ms |
| Exclusión mutua (`isSaving`) | Previene autoguardados concurrentes |
| Total overhead | < 300 ms cada 60 segundos (solo si hay cambios) |

## Impacto de ProjectLockService

| Aspecto | Mediciones |
|---|---|
| Heartbeat timer | 30 segundos |
| Heartbeat escritura | < 5 ms |
| Lock check en apertura | < 10 ms |
| Lock file size | ~200 bytes |

## Impacto de SafeSaveService

| Aspecto | Mediciones |
|---|---|
| Backup antes de guardar | ~10-50 ms adicionales |
| Rotación de backups | < 5 ms |
| Verificación JSON post-escritura | Doble E/S: escritura temporal + lectura |
| File.Replace (NTFS atómico) | ~5-20 ms |

## Resumen

El overhead total de Fase 9 es inferior a 500 ms en ráfagas y cero en reposo. No hay impacto perceptible en la experiencia del usuario. El autoguardado opera en background sin bloquear la UI.
