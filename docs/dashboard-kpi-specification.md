# Dashboard KPI Specification — Fase 7C-D

## Categorías

Plazo, Avance, EVM, Costos.

## Semáforos (`DashboardTrafficLightPolicy`)

| Índice | Verde | Ámbar | Rojo |
|--------|-------|-------|------|
| SPI | ≥ 1.00 | ≥ 0.90 | < 0.90 |
| CPI | ≥ 1.00 | ≥ 0.90 | < 0.90 |

Umbrales configurables vía `DashboardTrafficLightThresholds` / preferencias.

## Estados

Good, Warning, Critical, Neutral, NoData, NotApplicable.

## Calidad

Complete, Partial, Outdated, Inconsistent, NoBaseline, NoCosts, NoProgress, InvalidIntegrity.
