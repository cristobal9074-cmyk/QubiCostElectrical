# Earned Value Specification — Fase 7C-C

## Objetivo

Motor EVM determinista alineado a PMI/PMBOK. Consume fecha de estado, baseline (existencia), avance y costos **sin** mutar plan, CPM ni forecast.

## Indicadores

PV, EV, AC, BAC, EAC, ETC, VAC, SV, CV, SPI, CPI, TCPI, % planificado/ganado/actual, Forecast Cost, Forecast Finish Variance.

## Componentes Domain

- `EarnedValueInput` / `EarnedValueResult` / `EarnedValueIndicators`
- `EarnedValueEngine`, `EarnedValueValidation`, `EarnedValueInterpreter`
- `EarnedValueInputBuilder`, `EarnedValueSnapshotHasher`, `EarnedValueComparisonEngine`

## Application

`IEarnedValueService` — Calculate, Snapshot, Compare, History, Verify.

## Relación con Fase 7C-D

El dashboard ejecutivo consume `IEarnedValueService` (último resultado + historial) para KPIs y Curva S **sin** recalcular el motor EVM.
