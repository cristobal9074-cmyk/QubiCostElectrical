# Fase 3 parcial — Semanas 9 y 11

> Producto: **Qubi Cost Electrical 1.0.0** · Semana 10 (smoke SC) omitida por decisión de producto.

## S9 — Export impecable ✅

### Golden-file Excel interno

Tests en `ExcelExportGoldenFileTests` + fixture `ExcelExportGoldenFixture`:

- 11 hojas en orden fijo (`00_CONTROL` … `11_FORMATO_COMPRA`)
- Metadatos de proyecto/cliente en control y datos
- Itemizado con título + partida valorizada
- Totales `06_HOJA_COSTO` y `09_OFERTA_CLIENTE` alineados con `BudgetCalculationService`
- Estructura mínima por hoja (filas usadas)

Ejecutar:

```powershell
dotnet test QubiCostElectrical.Wpf.sln -c Release --filter "FullyQualifiedName~ExcelExportGoldenFileTests"
```

## S11 — Banco corporativo ✅

### Ruta UNC / carpeta compartida

- `ProductPaths.IsUncPath` / `IsAbsolutePriceBankPath` / `ResolvePriceBankDatabaseFile`
- Validación en `PreferencesViewModel` (ruta absoluta; carpeta local verificada; UNC sin bloqueo por red inaccesible en guardado)
- UI Preferencias: tooltip y ayuda UNC
- Tests: `ProductPathsTests`, `CorporatePriceBankTests` (dos instancias sobre mismo `.db`)

Documentación operativa: `docs/PRICE_BANK_SHARED_FOLDER.md`.

## Puerta automatizada

`WpfPhase3GateTests` — golden Excel + helpers UNC + doc corporativo.

## Pendiente (S10 omitida, S12 GA)

- Smoke manual flujo SC (semana 10 — no ejecutada)
- Tag `v1.0.0`, instalador firmado, smoke GA completo
