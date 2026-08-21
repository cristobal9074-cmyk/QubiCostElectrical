# WPF Migration Plan

## Fase 1 (completada)

Shell WPF navegable alineado al prototipo HTML, sin lógica de negocio completa.

### Checklist Fase 1

- [x] Alinear `QubiCostElectrical.Wpf` a `net8.0-windows`
- [x] `DesignSystem/*.xaml` con tokens del prototipo
- [x] `ShellWindow` — 3 barras + contenido ancho completo
- [x] Navegación horizontal con menús desplegables
- [x] 13 páginas placeholder navegables

## Fase 2 (completada — revisión pendiente)

### Alcance implementado

- [x] Proyecto compartido `PresupuestoElectrico.Composition` (`AppServices`, `AppServicesBuilder`)
- [x] Servicios de workspace en Application (`ProjectWorkspaceService`, `DocumentsWorkspaceService`, `ProjectEditWorkspaceService`)
- [x] Dashboard WPF con KPIs, flujo, alertas, costos y top partidas desde proyecto real
- [x] Datos del proyecto: carga, edición, validación, guardado en sesión, confirmación al navegar
- [x] Antecedentes y planos: tabla, búsqueda, filtros, fecha de corte, operaciones soportadas por `AntecedentesTecnicosService`
- [x] Navegación conectada (menú, contexto, alertas del dashboard)
- [x] Abrir / Guardar / Guardar como `.pepro` sin WinForms embebido

### Fuera de alcance Fase 2

Itemizado, cubicación, APU, precios completos, login, WebView2, HTML embebido, eliminación de WinForms.

### Verificación

```powershell
dotnet build PresupuestoElectrico.sln -c Debug
dotnet build PresupuestoElectrico.sln -c Release
dotnet test PresupuestoElectrico.sln -c Debug
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
```

### Próxima fase

Fase 3 (completada — revisión pendiente): Itemizado WPF con datos reales.

Fase 4: cubicación WPF completa.

## Fase 3 (completada — revisión pendiente)

### Alcance implementado

- [x] `ItemizadoPresenter` y orquestación en Application
- [x] `ItemizadoWorkspaceService` (filas de presentación, filtros, resumen)
- [x] `Features/Itemizado/ItemizadoView` + ViewModel MVVM
- [x] Importar Excel y pegar (use cases existentes + diálogos WPF)
- [x] Filtros: Todos, Incompletos, Completos, Sin cubicar, Sin precio
- [x] Navegación a Cubicación provisional con `TakeoffNavigationContext`
- [x] Duplicar partida (`DuplicateAt`)
- [x] Virtualización DataGrid, 109 tests Itemizado

### Fuera de alcance Fase 3

Cubicación completa, APU, precios, revisión, exportación, login, WebView2, HTML embebido.

### Verificación Fase 3

```powershell
dotnet build PresupuestoElectrico.sln -c Debug
dotnet test PresupuestoElectrico.sln -c Debug --filter "FullyQualifiedName~Itemizado"
dotnet run --project src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj
```

Ver `docs/WPF_PHASE3_ITEMIZADO_REPORT.md`.

## Fase 4 (completada — revisión pendiente)

### Alcance implementado

- [x] `CubicacionTakeoffQueryService` — servicio puro de evaluación de fórmulas por zonas, sin duplicar `QuantityFormulaService`
- [x] `CubicacionClipboardService` — parsing de texto TSV de Excel con validación de cantidad
- [x] `PartidaListItemVm`, `CubicacionRowVm`, `CubicacionRightPanelVm` — view models MVVM
- [x] `CubicacionViewModel` — orquestación completa: carga, selección, agregar/duplicar/eliminar/reordenar, fórmulas, autocompletado, pegado, guardado, validación
- [x] `ExcelPastePreviewDialog` — diálogo de previsualización antes de importar
- [x] `CubicacionView` — layout tres paneles (izquierdo colapsable, centro con barra de fórmula + grilla + totales, derecho colapsable con TabControl)
- [x] Panel izquierdo: lista de partidas con filtro de búsqueda y badge de conteo de recursos
- [x] Panel central: barra de fórmula con evaluación en tiempo real, grilla virtualizada con indicador de error, strip de totales (Materiales / MO / Costo directo / Precio unitario / Total partida)
- [x] Panel derecho: TabControl con pestañas Recurso (zona, tipo, redondeo, merma), Proveedor (nombre, fecha, alerta vencimiento), Nota, Alertas (con badge contador)
- [x] Barra de fórmula: soporte de expresiones con variables de proyecto, evaluación live, commit con Enter/Escape
- [x] Autocompletado de materiales: búsqueda async con debounce, navegación teclado (↑↓Enter Tab), selección automática del primer resultado
- [x] Validación de filas: código no encontrado en catálogo, cantidad cero, precio cero; indicador `!` rojo en grilla con tooltip
- [x] Atajos de teclado: Insert, Delete, F2, Ctrl+S, Ctrl+D, Ctrl+V, Alt+↑↓, Ctrl+↑↓
- [x] Recepción de `TakeoffNavigationContext` desde Itemizado con auto-selección de partida
- [x] Converters nuevos: `BoolToFontWeightConverter`, `NullToVisibilityConverter`, `RowIndexConverter`
- [x] Tests de rendimiento: `RecalculateAll` 3000 filas < 2 s, validación 3000 filas < 1 s

### Fuera de alcance Fase 4

APU completo, banco de precios completo, cotizaciones, revisión, exportación, login, WebView2, HTML embebido.

### Verificación Fase 4

```powershell
dotnet build PresupuestoElectrico.sln -c Debug
dotnet build PresupuestoElectrico.sln -c Release
dotnet test PresupuestoElectrico.sln -c Debug
dotnet test PresupuestoElectrico.sln --filter "CubicacionPerformance"
dotnet run --project src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj
```

Ver `docs/WPF_PHASE4_CUBICACION_REPORT.md`.
