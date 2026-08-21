# Fase 3 — Itemizado WPF (informe)

**Fecha:** 2026-06-21  
**Ruta:** `C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1`  
**Rama:** `master` (9 commits por delante de `origin/master`, sin commit de Fase 3)

## Resumen

Se reemplazó `ItemizedPlaceholderView` por `Features/Itemizado/ItemizadoView` conectada a datos reales vía `ItemizadoPresenter` (Application), `ItemizadoWorkspaceService` y sesión `BudgetProjectSession`. WinForms no fue eliminado ni referenciado desde WPF.

## Skill ui-ux-pro-max

| Campo | Valor |
|-------|-------|
| Ubicación | `C:\Users\cristobal1\.cursor\skills\ui-ux-pro-max\SKILL.md` |
| Script | `scripts/search.py` |

### Consultas ejecutadas

1. `enterprise data tables construction estimating` (design-system)
2. `keyboard-first editable grid excel-like hierarchical` (ux)
3. `large data grid virtualization dense interface accessibility` (web)
4. `data table filter search toolbar destructive actions` (ux)
5. `status badge progress bar dense dashboard` (style)

### Aplicado en WPF

- Grilla densa `DataGrid.Workspace`, filas 36px, virtualización/reciclaje
- Badges de estado por semántica (ok/warn/info/bad/neutral)
- Barra de progreso de cubicación por partida
- Confirmación en eliminar/vaciar/importar reemplazo
- Focus/teclado: Tab, Enter, Escape, flechas (DataGrid), Ctrl+V, Delete
- Empty state con acciones reales (importar, pegar, agregar)
- Acciones destructivas solo en menú «Más acciones»

### Descartado (web/móvil)

- Mobile-first, skip links, cursor:pointer, autocomplete remoto, Fira Code como fuente UI

## Archivos creados

### Application / Composition
- `Application/Presenters/ItemizadoPresenter.cs`
- `Application/Presenters/ItemizadoPresenterServices.cs`
- `Application/Navigation/WorkspaceNavigationContext.cs`
- `Application/Workspace/ItemizadoModels.cs`
- `Application/Workspace/ItemizadoWorkspaceService.cs`
- `Application/Workspace/ItemizadoImportPreviewPolicy.cs`
- `Composition/ItemizadoComposition.cs`

### WPF
- `QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoView.xaml(.cs)`
- `QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoViewModel.cs`
- `QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoRowVm.cs`
- `QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoImportDialog.cs`
- `QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoPasteDialog.cs`

### Tests
- `tests/.../Application/ItemizadoPresenterTests.cs` (movido desde WinForms)
- `tests/.../Application/ItemizadoWorkspaceServiceTests.cs`
- `tests/.../Application/ItemizadoServiceDuplicateTests.cs`
- `tests/.../Application/ItemizadoPerformanceTests.cs`
- `tests/.../Application/ItemizadoCompatibilityTests.cs`

## Archivos modificados (principales)

- `BudgetProjectSession.cs` — `PendingTakeoffNavigation`
- `ItemizadoService.cs` / `Interfaces.cs` — `DuplicateAt`
- `WpfAppHost.cs`, `ShellViewModel.cs`, `IShellNavigationHost.cs`
- `TakeoffPlaceholderView.cs` — muestra partida recibida
- `WpfDialogService.cs` — `PromptOpenExcel`
- WinForms: `ItemizadoPageDeps`, `ItemizadoWorkspacePage`, `MainForm.WorkspaceNavigation`
- Eliminados de WinForms: `Presenters/ItemizadoPresenter.cs`, `Features/Itemizado/ItemizadoPageServices.cs`

## Servicios reutilizados

- `ItemizadoService`, `ImportarItemizadoUseCase`, `PegarItemizadoUseCase`, `ItemizadoImportParser`
- `IItemizadoSpreadsheetReader` / `ItemizadoSpreadsheetReader`
- `IPartidaCompletionService`, `IBudgetCalculationService`
- `IProjectWorkspaceService` (guardado global)

## Servicios nuevos

- `ItemizadoPresenter` (movido a Application)
- `ItemizadoWorkspaceService` / `IItemizadoWorkspaceService`
- `ItemizadoPresenterFactory` (Composition)
- `TakeoffNavigationContext`

## Lógica extraída desde WinForms

- Orquestación del presenter y DTOs (`ItemizadoGridState`, `ItemizadoImportPlan`, …)
- Filtros, resumen, badges y totales por partida (antes en `LoadItemizadoGrid` / `UpdateSummaryStrip`)
- Política de vista previa de pegado/importación

## Acciones implementadas

| Acción | Estado |
|--------|--------|
| Cargar itemizado | OK |
| Agregar partida / título | OK |
| Editar ítem, descripción, unidad, cantidad | OK |
| Duplicar partida | OK (nuevo `DuplicateAt`) |
| Subir / bajar | OK |
| Eliminar / vaciar (confirmación) | OK |
| Importar Excel | OK (mismo use case) |
| Pegar desde Excel | OK (mismo use case + preview) |
| Buscar / filtrar | OK (`ICollectionView`) |
| Guardar (shell) | OK |
| Cubicar → placeholder | OK (`PendingTakeoffNavigation`) |
| Convertir en título | No existe en dominio — omitido |
| Reordenar modal / drag | No — solo subir/bajar |
| Undo/Redo | No hay soporte en sesión |

## Compilación y pruebas

| Métrica | Debug | Release |
|---------|-------|---------|
| Build | 0 errores | 0 errores |
| Warnings | ~78 | ~76 |
| Tests totales | 767 | — |
| Tests OK | 763 | — |
| Tests fallidos (preexistentes) | 3 | — |
| Tests Itemizado | 109 OK | — |

### Fallos preexistentes (no introducidos por Fase 3)

1. `NavigationPageCatalogTests.TryFromKey_RemovedExperimentalKeys_AreNotRegistered`
2. `WorkspaceNavigationRegistrationTests.Workspace_factory_count_matches_primary_routes`
3. `WinFormsThemeCompletenessTests.WinFormsSources_DoNotUseHardcodedWhite_OutsideAllowList`

## Prueba manual `.pepro`

No hay archivos `.pepro` en el repositorio. Se validó roundtrip programático en `ItemizadoCompatibilityTests` (guardar `.pepro` temporal y reabrir).

**Pendiente en su entorno:** abrir un `.pepro` real, editar, guardar copia, abrir en WinForms y reabrir en WPF (checklist del plan).

## Rendimiento 3.000 filas

`ItemizadoPerformanceTests.BuildPageState_handles_three_thousand_rows_under_two_seconds` — OK (< 2 s en build de estado).

## Diferencias vs prototipo HTML

- Sin filtro «En revisión» (no hay estado persistido por fila)
- Sin botón «Reordenar» modal; subir/bajar en menú
- Sin columna menú ⋮ por fila (acciones en barra y menú grupal)
- Tipografía Segoe UI del design system (no Fira)
- Total/P.U. calculados por motor, no editables

## Capturas

Las capturas de pantalla deben tomarse al ejecutar:

```powershell
dotnet run --project src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj
```

Escenas: itemizado con datos, vacío, diálogo importación, pegado, menú «Más acciones». (No automatizadas en este entorno headless.)

## Funciones pendientes (Fase 4+)

- Cubicación completa WPF
- APU, precios, revisión, exportación
- Drag-and-drop reordenar
- Convertir en título
- Undo/Redo compartido
- Partida asistida con `MaterialSearchService` (diálogo ProFeatures WinForms)
