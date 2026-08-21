# Phase 3 — WinForms Reduction Baseline

Fecha de corte: 2026-06-19  
Commit base: 828ebed (Fase 2 corregida - navegación única y tipada)  
Pruebas al inicio: 668 passing, 0 failing

---

## 1. Líneas por proyecto

| Proyecto | LOC |
|---|---:|
| PresupuestoElectrico.Domain | 3 368 |
| PresupuestoElectrico.Application | 5 990 |
| PresupuestoElectrico.Infrastructure | 6 538 |
| PresupuestoElectrico.WinForms | 45 325 |
| tests | 9 084 |
| **TOTAL** | **70 305** |

---

## 2. Archivos prioritarios — estado inicial

| Archivo | LOC | Proyecto |
|---|---:|---|
| CubicacionSCWorkspacePage.cs | 2 497 | WinForms |
| ExportOrchestrator.cs | 1 768 | WinForms |
| ModernControls.cs | 1 678 | WinForms |
| PreciosWorkspacePage.cs | 1 687 | WinForms |
| AppTheme.cs | 1 042 | WinForms |
| SQLiteProjectStore.cs | 1 365 | Infrastructure |
| ApuWorkspacePage.cs | 1 164 | WinForms |
| PlantillasWorkspacePage.cs | 1 018 | WinForms |
| BudgetEngine.cs | 921 | Domain |

---

## 3. Análisis de responsabilidades — CubicacionSCWorkspacePage.cs (2 497 líneas)

### Dependencias directas

| Dependencia | Tipo | Capa correcta |
|---|---|---|
| `CubicacionSCPageDeps._deps` | DI record | WinForms |
| `BudgetProjectSession` | acceso a datos del proyecto | Application |
| `CubicacionSCPageServices` | servicios inyectados | Application |
| `CubicacionSCEngine.Aggregate()` | motor de cálculo | Domain ✓ |
| `CubicacionSCEngine.HasFlexiblesWithoutConfig()` | validación | Domain ✓ |
| `ICurrencyConversionService` | servicio | Application ✓ |
| `ICubicacionSCExcelExporter` | exportador | Infrastructure ✓ (via servicio) |

### Clasificación de métodos

| Método | Líneas | Clasificación | Destino propuesto |
|---|---:|---|---|
| `BuildCubicacionSCCard()` | 260 | UI — layout principal | WinForms (OK) |
| `BuildTableroHeaderSC()` | 71 | UI — encabezado tablero | WinForms (OK) |
| `BuildEmptyStateSC()` | 47 | UI — estado vacío | WinForms (OK) |
| `BuildArtefactosDetailSC()` | 229 | UI — panel artefactos | → CubicacionSCArtefactosEditor.cs |
| `BuildCircuitosSCColumns()` | 88 | presentación — columnas grilla | → CubicacionSCGridAdapter.cs |
| `DrawGroupBandsSC()` | 33 | UI — pintura bandas | → CubicacionSCGridAdapter.cs |
| `GridCircuitosSC_CellPainting()` | 77 | UI — pintura celdas | → CubicacionSCGridAdapter.cs |
| `GridCircuitosSC_CellValueChanged()` | 51 | presentación — sync grid↔modelo | → CubicacionSCGridAdapter.cs |
| `ApplyCircuitoSCRowStyle()` | 6 | presentación — estilo fila | → CubicacionSCGridAdapter.cs |
| `UpdateTableroSCHeaderTexts()` | 65 | UI — chips de estado | WinForms (OK) |
| `DrawTableroSCItem()` | 27 | UI — pintura custom | WinForms (OK) |
| `ShowCubicacionScParamsDialog()` | 318 | UI — diálogo config | → CubicacionSCParamsDialog.cs |
| `ShowBatchCircuitosSCDialog()` | 154 | UI — diálogo lote | → CubicacionSCBatchDialog.cs |
| `ShowTableroSCDialog()` | 87 | UI — diálogo tablero | → CubicacionSCTableroDialog.cs |
| `AggregateCubicacionSC()` | 2 | coordinación | WinForms OK (1 línea delega a Engine) |
| `ScCategoriaMaterial()` | 9 | presentación — color/ícono por cat. | WinForms (asociado a UI) |
| `BuildScResumenHash()` | 7 | transformación | → CubicacionSCBudgetService |
| `UpdateEnviarBadge()` | 15 | UI — estado botón | WinForms (OK) |
| `FormatMoneyFromUf()` | 5 | presentación | WinForms (OK, delega) |
| `RefreshResumenSC()` | 64 | UI — panel materiales | → CubicacionSCResumenPanel.cs |
| `RefreshResumenKpiSC()` | 24 | UI — KPIs | → CubicacionSCResumenPanel.cs |
| `BuildResumenKpiSC()` | 21 | UI — factory KPI | → CubicacionSCResumenPanel.cs |
| `BuildMaterialCardSC()` | 55 | UI — card material | → CubicacionSCResumenPanel.cs |
| `SendCubicacionSCToBudget()` | 67 | **CASO DE USO** — crea entidades | → Application/CubicacionSCBudgetService |
| `AutoSyncCubicacionSCToBudget()` | 50 | **CASO DE USO** — auto-sync | → Application/CubicacionSCBudgetService |
| `ApplySistemaToCircuito()` | 9 | **REGLA DE NEGOCIO** | → CubicacionSCEngine (Domain) |
| `ExportarResumenSCExcel()` | 39 | coordinación — exportación | WinForms (OK, llama a servicio) |
| `AddTableroSC/EditTableroSC/RemoveTableroSC` | 20 | coordinación UI | WinForms (OK) |
| `AddCircuitoSC/RemoveCircuitoSC` | 28 | coordinación UI | WinForms (OK) |
| `PushScUndoSnapshot/UndoSC` | 30 | coordinación | WinForms (OK) |
| `ScheduleScAutoSync/SetScSyncStatus` | 24 | UI | WinForms (OK) |
| `ReloadTablerosSCList/ReloadCircuitosSCGrid` | 70 | presentación | WinForms (OK) |
| `SaveArtefactosListSC/LoadArtefactosDetailSC` | 56 | presentación datos | → CubicacionSCArtefactosEditor |
| `SelectedCircuitoSC/ScArtefactosResumen` | 20 | presentación | → CubicacionSCArtefactosEditor |
| Helpers estáticos (ActionButton, MakeGrid, etc.) | 80 | UI | WinForms (OK) |

### Violaciones arquitectónicas identificadas

1. **`SendCubicacionSCToBudget()`** — Crea `ItemizadoRow` y `CubicacionRow` directamente en la página. Este es un caso de uso de Application.
2. **`AutoSyncCubicacionSCToBudget()`** — Duplica la misma lógica de forma silenciosa. También es Application.
3. **`ApplySistemaToCircuito()`** — Regla pura de dominio (aplica sistema al circuito). Pertenece a `CubicacionSCEngine`.

---

## 4. Tabla completa de archivos prioritarios

| Archivo | Líneas | Responsabilidades mezcladas | Dependencias críticas | Destino propuesto |
|---|---:|---|---|---|
| CubicacionSCWorkspacePage.cs | 2 497 | UI, diálogos, casos de uso, regla dominio | BudgetProjectSession, CubicacionSCEngine, exporters | WinForms → extraer casos uso a App, diálogos a clases propias |
| ExportOrchestrator.cs | 1 768 | todos los formatos, parsing XML/Excel, diálogos | ExcelExporter, PdfExporter, Bc3Exchange | Dividir por formato en Infrastructure; fachada <300L en WinForms |
| ModernControls.cs | 1 678 | 11 clases en 1 archivo | AppTheme | 1 clase por archivo en WinForms/Controls/ |
| PreciosWorkspacePage.cs | 1 687 | UI, búsqueda, conversión moneda, import Excel | MaterialSearchService, PriceBankRepository | Extraer búsqueda/import a Application |
| AppTheme.cs | 1 042 | colores, tipografía, espacio, métricas, íconos | GDI (Pens, Brushes) | Dividir en ThemeColors, ThemeTypography, etc. |
| SQLiteProjectStore.cs | 1 365 | todas las tablas en 1 clase estática | Microsoft.Data.Sqlite | Dividir por repositorio en Infrastructure |
| ApuWorkspacePage.cs | 1 164 | UI, cálculos APU, sincronización | BudgetEngine, ApuLaborEngine | Extraer cálculos a Application |
| PlantillasWorkspacePage.cs | 1 018 | UI, CRUD, expansión recursiva, detección ciclos | TemplateService | Extraer lógica a Application |
| BudgetEngine.cs | 921 | motor de cálculo monolítico | — | No tocar sin pruebas de caracterización |

---

## 5. Plan de extracción Fase 3B (CubicacionSC)

### Archivos nuevos a crear

**Application:**
- `Application/Features/CubicacionSC/CubicacionSCBudgetService.cs` — casos de uso SendToBudget y AutoSync

**Domain:**
- Agregar `ApplySistema()` a `CubicacionSCEngine.cs`

**WinForms:**
- `WinForms/Features/CubicacionSC/CubicacionSCParamsDialog.cs` — Form de parámetros (~320L)
- `WinForms/Features/CubicacionSC/CubicacionSCBatchDialog.cs` — Form de lote (~154L)
- `WinForms/Features/CubicacionSC/CubicacionSCTableroDialog.cs` — Form de tablero (~87L)
- `WinForms/Features/CubicacionSC/CubicacionSCGridAdapter.cs` — adapter grilla (~255L)
- `WinForms/Features/CubicacionSC/CubicacionSCArtefactosEditor.cs` — panel artefactos (~280L)
- `WinForms/Features/CubicacionSC/CubicacionSCResumenPanel.cs` — panel resumen (~164L)

### Reducción estimada

| Extracción | Líneas removidas de página |
|---|---:|
| 3 diálogos → clases propias | ~559 |
| Grid adapter | ~255 |
| Artefactos editor | ~280 |
| Resumen panel | ~164 |
| Casos de uso → Application | ~97 |
| **Total estimado removido** | **~1 355** |
| **Estimado final CubicacionSCWorkspacePage** | **~1 150** |

> El objetivo orientativo de 800 líneas requeriría además extraer los builders de layout principal (`BuildCubicacionSCCard`, `BuildArtefactosDetailSC`) que están profundamente acoplados a los campos de instancia. Se deja para evaluación posterior si el resultado a ~1150 líneas ya cumple el criterio de "principalmente visual, sin casos de uso ni reglas de negocio".
