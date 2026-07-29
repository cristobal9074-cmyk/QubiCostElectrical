# Plan: Arquitectura 8.0 → 10.0

> **Proyecto:** Qubi Cost Electrical
> **Objetivo:** Cero god objects, DI container, CI/CD, test coverage, documentación de arquitectura
> **Duración estimada:** 12 semanas

## Criterio 10/10

| # | Regla | Cómo se mide |
|---|-------|-------------|
| 1 | Cero archivos > 500 líneas | Test de arquitectura + CI gate |
| 2 | Cero código huérfano | Analizador de referencias cruzadas |
| 3 | Single Responsibility en cada clase pública | Cada clase tiene una razón de cambio |
| 4 | Flujos de datos documentados | Diagrama C4 (Contexto + Contenedores + Componentes + Código) |
| 5 | DI container real | No más service locator (`AppServices`); Microsoft DI o DryIoc |
| 6 | CI/CD pipeline | GitHub Actions: build + test + lint + size gate |
| 7 | Servicios testables | Todas las dependencias vía interfaz, inyectadas |
| 8 | Cero fugas de recursos | Conexiones SQLite con `using`, `IAsyncDisposable` |
| 9 | Cero O(n·m) en memoria | Índices, lookups, cachés donde corresponda |
| 10 | Diagrama de arquitectura actualizado | En `docs/ARCHITECTURE.md` con C4 |

---

## Fase 0: Fundación (Semana 1)

Poner los rieles antes de tocar código.

| Tarea | Archivos | Esfuerzo |
|-------|----------|----------|
| 0.1 | GitHub Actions: build + test + size gate (`max-line-count=500`) | `.github/workflows/ci.yml` | 1d |
| 0.2 | Roslyn analyzers + reglas CA en `Directory.Build.props` | `Directory.Build.props` | 0.5d |
| 0.3 | Script de inventario de líneas por archivo (>300) | `tools/Measure-FileSizes.ps1` | 0.5d |
| 0.4 | Test de arquitectura que falle si archivo > 500 líneas | `tests/Architecture/FileSizeTests.cs` | 0.5d |
| 0.5 | Identificar código muerto (referencias cruzadas) | — | 1d |

**Entregable:** CI/CD pipeline verde. Test de arquitectura fallando en ~16 archivos.

---

## Fase 1: Quick Wins (Semana 2)

Eliminar problemas triviales sin riesgo alto.

| Tarea | Archivo | Problema | Fix |
|-------|---------|----------|-----|
| 1.1 | `FormulaBar.cs` (0 bindings XAML) | Código huérfano | Eliminar + limpiar referencias |
| 1.2 | `ApuTemplateStore.Open()` | Conexión SQLite no descartada | Envolver en `using` |
| 1.3 | `PriceBankStore.Search` fallback | O(n·m) carga completa en memoria | Agregar índice `Dictionary<string, PriceEntry>` |
| 1.4 | `SectorPickOffsetEqualsConverter` anidado en `CubicacionView.xaml.cs` | Inner class infla code-behind | Extraer a `Converters/` |
| 1.5 | `DisplayBindingTextColumn` anidado en `CubicacionView.xaml.cs` | Idem | Extraer a `Controls/` |

**Entregable:** +0.3 score. CI/CD verde.

---

## Fase 2: Deconstrucción de God Objects — Views & ViewModels (Semanas 3-5)

### 2.1 `CubicacionView.xaml.cs` (1.682 → <300 líneas de lógica)

| Sub-fase | Responsabilidad extraída | Nuevo archivo |
|----------|------------------------|--------------|
| 2.1a | Navegación Alt+Up/Down + teclas globales | `CubicacionKeyboardBehavior.cs` |
| 2.1b | Autocomplete hover + popup tracking | `CubicacionAutocompleteBehavior.cs` |
| 2.1c | Sector reference highlighting + formula pick | `CubicacionFormulaPickBehavior.cs` |
| 2.1d | Cell width binding (12 columnas × 2 modos) | `CubicacionCellWidthManager.cs` |
| 2.1e | DataGridExcelNavigation + grid mode | `CubicacionGridModeManager.cs` |

### 2.2 `CubicacionScViewModel.cs` (1.378 → <500)

| Sub-fase | Acción |
|----------|--------|
| 2.2a | Extraer `ArtefactoScRowVm`, `CircuitoScRowVm`, `ZonaPisoQuickItem` a archivos propios |
| 2.2b | Split en partials: `.Loading.cs`, `.Persistence.cs`, `.Sectors.cs` |
| 2.2c | Crear `CubicacionScLoadingService`, `CubicacionScPersistenceService` |

### 2.3 `CostoViewModel.cs` (1.700 → <500)

| Sub-fase | Acción |
|----------|--------|
| 2.3a | Extraer `CostoRowVm`, `CoefFactorVm` a archivos propios |
| 2.3b | Split en partials: `.Loading.cs`, `.Persistence.cs`, `.Calculations.cs` |
| 2.3c | Migrar lógica de descuentos/factores a Domain |

---

## Fase 3: Deconstrucción de God Objects — Infrastructure & Shell (Semanas 6-8)

### 3.1 `DataGridExcelNavigation.cs` (1.148 → <500)

| Clase actual | Nuevo archivo destino |
|-------------|----------------------|
| Anchor/selection tracking | `ExcelSelectionEngine.cs` |
| Copy marker adorners | `ExcelCopyMarkerHandler.cs` |
| Column scope restriction | `ExcelColumnScopeManager.cs` |
| Entry point | `DataGridExcelNavigation.cs` (~150 líneas) |
| Paste handling | `ExcelPasteHandler.cs` |

### 3.2 `QuestPdfProjectExporter.cs` (882 → <500)

| Sub-fase | Acción |
|----------|--------|
| 3.2a | Crear `IPdfExportStrategy` |
| 3.2b | Implementaciones: `ProjectLetterPdfStrategy`, `CubicacionReportPdfStrategy`, `CubicacionSCReportPdfStrategy` |
| 3.2c | `QuestPdfProjectExporter` como facade |

### 3.3 `ShellViewModel.cs` (823 → <500)

| Sub-fase | Acción |
|----------|--------|
| 3.3a | Extraer navegación a `ShellNavigationController.cs` |
| 3.3b | Extraer sesión/proyecto a `ShellSessionManager.cs` |
| 3.3c | Extraer visibilidad por roles a `ShellRoleVisibilityService.cs` |

### 3.4 God objects restantes

| Archivo | Líneas | Estrategia |
|---------|--------|-----------|
| `ExportViewModel.cs` | 778 | Split: `ExportPdfVm`, `ExportExcelVm`, `ExportDocxVm` + base |
| `ManoDeObraViewModel.cs` | ~750 | Split partials + servicios |
| `DashboardViewModel.cs` | ~700 | Split: KPIs, gráficos, alertas |
| `PlantillasVm.cs` | ~650 | Split partials |
| `ApuWorkspaceVm.cs` | ~600 | Split partials |
| `BudgetProjectSessionService.cs` | ~600 | Extraer event handlers |

---

## Fase 4: Migración a DI Container (Semanas 9-10)

| Paso | Acción | Archivos tocados |
|------|--------|-----------------|
| 4.1 | Agregar DryIoc NuGet | `QubiCostElectrical.Composition`, `QubiCostElectrical.Wpf` |
| 4.2 | Crear `CompositionRoot.cs` con registros | Nuevo archivo |
| 4.3 | Migrar `WpfAppHost.Initialize()`: container primero | `WpfAppHost.cs` |
| 4.4 | Reemplazar `WpfAppHost.Current.Servicio` por inyección en VMs | Todos los ViewModels |
| 4.5 | Eliminar `AppServices` + `AppServicesBuilder` | Eliminar ambos |
| 4.6 | Migrar presenters y diálogos a resolución por container | `PresenterFactory.cs`, ~12 diálogos |

---

## Fase 5: Test Infrastructure & Coverage (Semana 11)

| Tarea | Cobertura esperada |
|-------|-------------------|
| 5.1 | Tests unitarios para servicios extraídos en F2-F3 | >80% |
| 5.2 | Tests para `DataGridExcelNavigation` descompuesto | >70% |
| 5.3 | Tests para estrategias de exportación (`IPdfExportStrategy`) | >80% |
| 5.4 | Tests para `CompositionRoot.Validate()` | Todos los grafos de dependencias |
| 5.5 | Tests de regresión: reportes PDF generados correctamente | Snapshot |
| 5.6 | Meta: cobertura general del proyecto | ~15% → ~35% |

---

## Fase 6: Documentación & Verificación Final (Semana 12)

| Tarea | Detalle |
|-------|---------|
| 6.1 | Diagrama C4 Nivel 1-2-3 (Contexto, Contenedores, Componentes) | `docs/diagrams/*.puml` |
| 6.2 | Mapa de flujo de datos por módulo | `docs/data-flows/{cubicacion,apu,export}.md` |
| 6.3 | Auditoría final: 0 archivos > 500 líneas, CI/CD verde | Test de arquitectura |
| 6.4 | Score audit: re-evaluar contra criterios 10/10 | Planilla |

---

## Resumen de puntuación esperada

| Fase | Score parcial | Score acumulado |
|------|-------------|-----------------|
| Hoy | 8.0 | 8.0 |
| F0: Fundación | +0.2 | 8.2 |
| F1: Quick Wins | +0.3 | 8.5 |
| F2: God Objects (Views) | +1.0 | 9.5 |
| F3: God Objects (Infra) | +0.3 | 9.8 |
| F4: DI Container | +0.1 | 9.9 |
| F5: Tests | +0.05 | 9.95 |
| F6: Documentación | +0.05 | **10.0** |

---

## Mapa de riesgos

| Riesgo | Impacto | Mitigación |
|--------|---------|-----------|
| Regresión en exportación PDF (F3.2) | Alto | Tests de snapshot antes de refactor |
| Rotura de CubicaciónView.xaml.cs (F2.1) | Alto | Behaviors sin cambio de comportamiento; tests E2E |
| DI container rompe singletons (F4) | Medio | `CompositionRoot` con lifetime scopes; validación al arranque |
| CostoViewModel cascada a otros módulos (F2.3) | Medio | Mapear todas las referencias antes de mover |
| Desvío en estimación de tiempo | Bajo | Fases con 1-2 semanas de buffer |
