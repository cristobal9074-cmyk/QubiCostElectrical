# Fase 4 — Cubicación WPF (informe)

**Fecha:** 2026-06-21
**Rama:** `master`

## Resumen

Se reemplazó `TakeoffPlaceholderView` por `Features/Cubicacion/CubicacionView` conectada a datos reales. La vista implementa el flujo completo de cubicación: selección de partida, edición de recursos, barra de fórmula con evaluación live, autocompletado de materiales, pegado desde Excel, validación automática y panel contextual con pestañas. WinForms no fue eliminado ni referenciado desde WPF.

## Skill ui-ux-pro-max

| Campo | Valor |
|-------|-------|
| Ubicación | `C:\Users\cristobal1\.claude\skills\ui-ux-pro-max` |
| Script | `scripts/search.py` |

### Consultas ejecutadas

1. `electrical estimating takeoff workspace three-panel desktop layout dense` (design-system)
2. `editable WPF DataGrid keyboard-first estimating software` (ux / web)
3. `formula editing interfaces autocomplete material catalogs` (ux)
4. `progressive disclosure collapsible contextual panels` (ux)
5. `construction estimating workspace data-dense dashboard` (style)

### Aplicado en WPF

- Layout tres paneles con `GridSplitter` redimensionable
- `DataGrid.Workspace` con virtualización/reciclaje, `RowHeight=28`
- Barra de fórmula estilo Excel (`fx` + TextBox consolas + resultado coloreado)
- Popup de autocompletado navegable con teclado sin perder foco del editor
- Panel derecho colapsable con `TabControl` (4 pestañas)
- Indicador `!` rojo en columna `#` para filas con error
- Transiciones de estado 150-300 ms (triggers WPF DataTrigger)

## Arquitectura

### Nuevos archivos en Application

| Archivo | Rol |
|---------|-----|
| `CubicacionTakeoffQueryService.cs` | Evaluación de fórmulas por zonas usando `QuantityFormulaService`. Variable building, recalculación total, redondeo. Nunca duplica el evaluador. |
| `CubicacionClipboardService.cs` | Parsing TSV de Excel: valida cantidades, detecta fórmulas, genera warnings. |

### Nuevos archivos en WPF

| Archivo | Rol |
|---------|-----|
| `PartidaListItemVm.cs` | VM de ítem en lista izquierda. Badge de conteo, `DisplayLabel`, `IsActiva`. |
| `CubicacionRowVm.cs` | VM de fila de recurso. Escribe a través de `CubicacionRow.Source`. `HasError`, `HasFormula`, `IsSelected`, displays formateados. |
| `CubicacionRightPanelVm.cs` | VM del panel derecho. Carga/limpia al cambiar fila. Lee comentarios, proveedor, redondeo, alertas desde sesión. |
| `CubicacionViewModel.cs` | VM principal. `IRefreshablePage`, `IPageWithPendingChanges`. Orquesta partidas, filas, fórmulas, autocompletado, pegado, validación, guardado. |
| `CubicacionView.xaml` | Vista tres paneles. Barra de fórmula, grilla con indicadores, strip de totales, TabControl derecho, popup autocompletado. |
| `CubicacionView.xaml.cs` | Code-behind: atajos teclado, navegación popup, autocompletado al editar Código, pegado Excel, selección automática primer resultado. |
| `ExcelPastePreviewDialog.xaml/.cs` | Diálogo de previsualización TSV con estado por fila (✓/⚠/✗). |
| `Infrastructure/CubicacionConverters.cs` | `BoolToFontWeightConverter`, `NullToVisibilityConverter`, `RowIndexConverter`. |

### Modificaciones

| Archivo | Cambio |
|---------|--------|
| `ShellViewModel.cs` | `case ShellRoute.Takeoff` → instancia `CubicacionViewModel` y `CubicacionView`. |
| `DesignSystem/Converters.xaml` | Registro de los 3 converters nuevos. |

## Subfases

### 4A — Núcleo
Panel izquierdo con lista de partidas y filtro. Grilla central virtualizada. Agregar/duplicar/eliminar/reordenar. Estado vacío. Recepción de `TakeoffNavigationContext` desde Itemizado. Guardado y reapertura.

### 4B — Fórmulas y validación
Barra de fórmula con evaluación live vía `QuantityFormulaService`. `ValidateRows()` al cargar partida, al editar y al aplicar material. Indicador visual `!` por fila. Autocompletado disparado por `TextChanged` en la celda de Código.

### 4C — Productividad y teclado
Navegación del popup con ↑↓ sin perder foco del editor. Enter/Tab para aplicar material. Insert para agregar fila. Delete para eliminar. F2 para barra de fórmula. Ctrl+D duplicar. Ctrl+S guardar. Ctrl+↑↓ cambiar partida. Alt+↑↓ reordenar. Selección automática del primer resultado al abrir popup.

### 4D — Panel derecho y rendimiento
`TabControl` con 4 pestañas: Recurso, Proveedor, Nota, Alertas (con badge contador). `EnableColumnVirtualization`. Tests de rendimiento: 3000 filas con fórmulas < 2 s; validación 3000 filas < 1 s.

## Resultados de build y tests

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | 0 errores |
| `dotnet build -c Release` | 0 errores |
| Suite completa (769 tests) | 766 pasan / 3 fallan (pre-existentes, sin relación con Fase 4) |
| `CubicacionPerformanceTests` (2 tests) | Pasan en ~800 ms total |

### Tests pre-existentes que fallan (sin cambio)

- `WinFormsSources_DoNotUseHardcodedWhite_OutsideAllowList` — `ProyectoWorkspacePage.cs` usa `Color.White` directamente
- `TryFromKey_RemovedExperimentalKeys_AreNotRegistered` (×2) — claves de navegación experimental aún registradas

## Restricciones respetadas

- WinForms no fue eliminado ni referenciado desde WPF.
- SQLite y migraciones sin cambios.
- `QuantityFormulaService` es el único evaluador; no hay duplicación.
- Sin APU completo, banco de precios, cotizaciones, revisión, exportación ni login real.
- Sin WebView2, sin HTML embebido, sin sidebar principal nuevo.
- Sin recursos demostrativos — todos los datos vienen de `BudgetProjectSession`.
- Sin botones sin función.
