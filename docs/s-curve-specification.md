# S-Curve Specification — Fase 7C-D

## Series

PV, EV, AC, comprometido, forecast (EAC), baseline BAC, % físico, % plan.

## Construcción

`DashboardSeriesBuilder` agrupa puntos ya calculados (historial EVM u otros) por día/semana/mes.

- **No interpola** huecos: solo periodos con dato (`HasData=true`).
- Modo acumulado: suma secuencial de periodos con dato.
- Modo periodo: valor del bucket.
- Semana inicia en **lunes** (determinista).

## UI

Barras apiladas horizontales (patrón Mano de Obra / Canvas), sin librería 3D.
