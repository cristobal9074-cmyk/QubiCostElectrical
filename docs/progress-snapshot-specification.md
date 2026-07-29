# Progress Snapshot Specification — Fase 7C-B

## Contenido congelado

Fecha de estado, avances, fechas reales, restante, costos/cantidades informados, forecast, camino crítico, resumen.

## Metadatos

`SnapshotId`, `Version`, `CreatedBy`, `CreatedUtc`, `Comment`, `SHA256`.

## Integridad

`ProgressSnapshotHasher` (orden determinista, SHA-256 hex). `VerifySnapshotIntegrityAsync` recalcula y compara.

## Comparación

`CompareSnapshotsAsync` → `ProgressComparisonResult` (iniciadas, completadas, atrasadas, adelantadas, deltas, camino crítico).

Snapshots **no** se sobrescriben; cada creación incrementa `version`.
