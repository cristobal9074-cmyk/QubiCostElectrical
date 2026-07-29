# Phase 7C-C Report — Earned Value Management

**Date:** 2026-07-25  
**Status:** COMPLETADA

## Deliverables

| Área | Entrega |
|------|---------|
| Domain | `EarnedValueEngine`, validación, interpretación, builder, comparación, snapshots SHA-256 |
| Application | `IEarnedValueService` / `EarnedValueService`, permisos `Planning.EarnedValue*` |
| SQLite | Schema **v39** — `planning_earned_value`, `_snapshots`, `_history` |
| WPF | `EarnedValueWorkspaceView` + export CSV/Excel/PDF |
| Docs | `docs/earned-value-*.md`, este informe |

## Indicadores PMI

PV, EV, AC, BAC, EAC, ETC, VAC, SV, CV, SPI, CPI, TCPI, % planificado/ganado/actual, Forecast Cost, Forecast Finish Variance.

## Reglas respetadas

- No recalcula CPM; no muta baselines, snapshots de plan, forecast ni cronograma.
- Cálculo solo en Domain; ViewModels solo orquestan.
- No inicia 7C-D (dashboards, curva S gráfica, compras, BIM, CAD, ERP, IA).

## Validación automática

```
dotnet restore .\QubiCostElectrical.Wpf.sln
dotnet build .\QubiCostElectrical.Wpf.sln -c Debug --warnaserror
dotnet build .\QubiCostElectrical.Wpf.sln -c Release --warnaserror
dotnet test .\QubiCostElectrical.Wpf.sln -c Release --no-build
dotnet test .\tests\ArchitectureTests\ArchitectureTests.csproj -c Release
```

Resultados sesión:

| Check | Resultado |
|-------|-----------|
| Debug `--warnaserror` | 0 / 0 |
| Release `--warnaserror` | 0 / 0 |
| Tests Release | **2482** superados, 0 fallas |
| ArchitectureTests | **8/8** |
| Schema | **v39** |
| Tests EVM nuevos | ≥120 (suite 2359 → 2482) |
