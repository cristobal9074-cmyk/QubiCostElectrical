# FASE 11C.2 — SUITE GLOBAL RESTAURADA A CERO FALLOS

## Resumen

Los cuatro tests globales fallidos detectados tras la Fase 11C.1 fueron identificados, clasificados y corregidos. La suite completa (3232 tests) pasa con 0 errores, 0 omitidos.

## Tabla de fallos

| Test | Causa raíz | Corrección | Resultado aislado | Resultado global |
|---|---|---|---|---|
| `Phase9ECommitmentDashboardTests.Schema_Is_51` → `Schema_Is_55` | Regresión funcional: `DatabaseSchemaVersion` se actualizó a 55 en `Directory.Build.props` pero el test quedó esperando 51 | Actualizado valor esperado de 51 a 55 y nombre del test | ✅ Correcto | ✅ 0 errores |
| `Phase9CdePersistenceTests.Schema_Is_51` → `Schema_Is_55` | Regresión funcional: misma causa — el schema del migrador de SQLite ahora reporta 55 | Actualizado valor esperado de 51 a 55 en ambos asserts y nombre del test | ✅ Correcto | ✅ 0 errores |
| `ValuationEngineTests.Calculate_UsesBankerRounding_ForLineAmounts` | Error de expectativa: `PrecisionPolicy.RoundImporte` usa 2 decimales (`ImporteDecimals=2`). 100.5 con 2 decimales y `ToEven` = 100.50, no 100 | Corregido valor esperado de 100 a 100.50 | ✅ Correcto | ✅ 0 errores |
| `ValuationEngineTests.Calculate_WithCompleteScenario_ProducesCorrectFinalTotals` | Error aritmético en expectativa: 43,500 + 8,265 - 3,500 - 1,750 - 2,000 = 44,515 (no 43,515) | Corregido valor esperado de 43,515 a 44,515 | ✅ Correcto | ✅ 0 errores |

## Clasificación de causas

| Test | Categoría |
|---|---|
| `Schema_Is_51` (Application) | Regresión funcional — versión de schema desactualizada en test |
| `Schema_Is_51` (Infrastructure) | Regresión funcional — versión de schema desactualizada en test |
| `Calculate_UsesBankerRounding_ForLineAmounts` | Error de expectativa — no consideraba decimales de `PrecisionPolicy` |
| `Calculate_WithCompleteScenario_ProducesCorrectFinalTotals` | Error de expectativa — error aritmético manual en cálculo esperado |

## Determinismo

Los cuatro fallos son **deterministas** y no dependen del entorno.

## Validación final

| Verificación | Resultado |
|---|---|
| Debug build — 0 errores, 0 advertencias | ✅ |
| Release build — 0 errores, 0 advertencias | ✅ |
| Suite completa (3232 tests) — 0 fallos, 0 omitidos | ✅ |
| Tests de arquitectura (9 tests) — 0 fallos, 0 omitidos | ✅ |
| 3 repeticiones consecutivas de los 4 tests corregidos | ✅ |

## Archivos modificados

- `tests/QubiCostElectrical.Tests/Application/Procurement/Phase9CdeTests.cs` — Schema_Is_55
- `tests/QubiCostElectrical.Tests/Infrastructure/Procurement/Phase9CdePersistenceTests.cs` — Schema_Is_55
- `tests/QubiCostElectrical.Tests/Domain/ValuationEngineTests.cs` — valores esperados corregidos

## Criterio de cierre

**FASE 11C.2 — SUITE GLOBAL RESTAURADA A CERO FALLOS**
