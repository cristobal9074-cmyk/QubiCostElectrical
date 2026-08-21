# Fase 10C — Independencia Operativa WPF

**Fecha:** 2026-06-23
**Rama:** feature/wpf-phase10b-functional-parity
**Estado base:** 1109/1109 tests, 0 errores Debug/Release

---

## 1. Matriz de Dependencias

| Dependencia | Consumidor | Tipo | Riesgo | Acción | Estado |
|---|---|---|---|---|---|
| `PresupuestoElectrico.WinForms` (ProjectRef en csproj) | `PresupuestoElectrico.Tests.csproj` | Test build | Bajo | Separar tests legacy en proyecto propio al retirar WinForms | **Pendiente** |
| `System.Windows.Forms` (tipos directos) | `WorkspaceNavigationCoordinatorTests.cs` | Test exclusivo WinForms | Bajo | Permanece como cobertura legacy | **Legacy** |
| Tipos WinForms via assembly reflection | 13 archivos en `tests/WinForms/` | Tests WinForms | Bajo | Cobertura legacy de arquitectura WinForms | **Legacy** |
| `app.manifest` de WinForms | `PresupuestoElectrico.WinForms` | Manifiesto WinForms-only | Ninguno | WPF no requiere este archivo | **Irrelevante** |
| Icono `.ico` | Ninguno en WPF | Recurso | Bajo | WPF usa `.png` en Assets (sin `ApplicationIcon` en csproj) | **Gap menor** |

### Hallazgo principal

`QubiCostElectrical.Wpf.exe` NO tiene ninguna referencia a `System.Windows.Forms` ni a `PresupuestoElectrico.WinForms` a nivel de:
- ProjectReference (csproj) ✓
- `using` / namespace en código fuente ✓
- Referencia de assembly en runtime ✓

El **único acoplamiento restante** es que `PresupuestoElectrico.Tests.csproj` tiene `<ProjectReference>` a WinForms para poder compilar los 14 archivos de test en `tests/WinForms/`.

---

## 2. Solución WPF Independiente

**Archivo:** `QubiCostElectrical.Wpf.sln`

### Proyectos incluidos

| Proyecto | Rol | Razón de inclusión |
|---|---|---|
| `PresupuestoElectrico.Domain` | Modelos y lógica de negocio pura | Dependencia directa de WPF |
| `PresupuestoElectrico.Application` | Servicios de aplicación | Dependencia directa de WPF |
| `PresupuestoElectrico.Infrastructure` | SQLite, Excel, PDF, Word | Dependencia directa de WPF |
| `PresupuestoElectrico.Composition` | Wiring de servicios | Dependencia directa de WPF |
| `QubiCostElectrical.Wpf` | Ejecutable WPF principal | **Producto** |
| `PresupuestoElectrico.WinForms` | Ejecutable WinForms legacy | Dependencia transitiva de Tests |
| `PresupuestoElectrico.Tests` | Suite de pruebas | Cobertura de todas las capas |

### Proyectos excluidos respecto a PresupuestoElectrico.sln

Ninguno — la solución WPF es idéntica en contenido a la solución completa. La diferencia está en el **propósito**: `QubiCostElectrical.Wpf.sln` declara que WPF es el producto principal.

---

## 3. Clasificación de Tests

### Tests de reglas compartidas (Domain / Application / Infrastructure)

Ubicación: `tests/Application/`, `tests/Infrastructure/`, raíz de `tests/`  
**~771 tests** — No dependen de WinForms. Compatibles con solución WPF.

Ejemplos: `BudgetEngineTests`, `ApuCalculationTests`, `SQLiteProjectRoundTripTests`, `ExcelExportTests`.

### Tests específicos WPF (Fase 10A + 10B)

Ubicación: `tests/Wpf/`  
**292 tests** — Usan `WpfAppHost`, no tienen dependencia en `System.Windows.Forms`.

| Archivo | Tests | Flujo cubierto |
|---|---|---|
| `ProjectLifecycleTests.cs` | 9 | Crear, abrir, cerrar, guardar |
| `ProjectPickerTests.cs` | 26 | Selector de proyecto, recientes |
| `ShellNavigationTests.cs` | 5 | Navegación entre vistas |
| `SingleInstanceTests.cs` | 7 | Mutex, handoff .pepro |
| `WpfUndoServiceTests.cs` | 37 | Undo/Redo completo |
| `CurrencyViewModelTests.cs` | 19 | Monedas e indicadores |
| `PreferencesViewModelTests.cs` | 30 | Preferencias globales |
| `ChangeOrdersViewModelTests.cs` | 55 | Órdenes de cambio |
| `LetterheadTests.cs` | 46 | Membrete / exportación |
| `RecentProjectsServiceTests.cs` | 19 | Recientes |
| `ShellReviewAndVersionsTests.cs` | 19 | Revisión y versiones |
| `UserPreferencesStoreTests.cs` | 1 | Persistencia JSON |

### Tests exclusivamente WinForms (cobertura legacy)

Ubicación: `tests/WinForms/`  
**46 tests** — Verifican arquitectura interna de WinForms y componentes de navegación WinForms.

| Archivo | Tests | Tipo |
|---|---|---|
| `AppNavigationServiceTests.cs` | 5 | Servicio navegación WinForms |
| `AppThemeDesignSystemTests.cs` | 9 | Tema visual WinForms |
| `ExportPlantillasThemeTests.cs` | 3 | Tema exportación WinForms |
| `LastSaveStatusFormatterTests.cs` | 3 | Formato estado guardado |
| `MainFormArchitectureGuardTests.cs` | 6 | Guard arquitectura MainForm |
| `MainFormShellLeanTests.cs` | 1 | MainForm no tiene lógica |
| `ProjectStoragePathsTests.cs` | 5 | Rutas de almacenamiento |
| `ShellLayoutMetricsTests.cs` | 3 | Métricas de layout WinForms |
| `UiErrorPolicyTests.cs` | 4 | Política errores UI |
| `WinFormsLegacyCleanupTests.cs` | 3 | Guard archivos legacy eliminados |
| `WinFormsThemeCompletenessTests.cs` | 1 | Completitud del tema |
| `WorkspaceNavigationCoordinatorTests.cs` | 1 | Coordinador nav WinForms (usa `System.Windows.Forms`) |
| `WorkspaceNavigationRegistrationTests.cs` | 2 | Registro de páginas WinForms |

**Nota:** estos tests deben eliminarse o migrarse al retirar WinForms. Mientras WinForms exista, proveen cobertura de la arquitectura legacy.

---

## 4. Recursos

### Recursos en WPF (self-contained)

| Recurso | Ubicación | Uso |
|---|---|---|
| `qubi_cost_electrical_refined_logo.png` | `src/QubiCostElectrical.Wpf/Assets/` | Logo en shell WPF |
| `isolate_the_isometric_cube_logo_mark_from_data_image...png` | `src/QubiCostElectrical.Wpf/Assets/` | Logo alternativo |

### Recursos en WinForms (no compartidos con WPF)

| Recurso | Ubicación | Impacto en WPF |
|---|---|---|
| `app.manifest` | `src/PresupuestoElectrico.WinForms/` | Ninguno — WPF no lo usa |

### Gap identificado

WPF no tiene un archivo `.ico` ni `ApplicationIcon` en su csproj. Impacto: el ejecutable WPF aparece sin ícono personalizado en el taskbar de Windows. Severidad: **baja** (cosmética).

---

## 5. Entry Point WPF

| Atributo | Valor |
|---|---|
| AssemblyName | `QubiCostElectrical.Wpf` |
| OutputType | `WinExe` |
| TargetFramework | `net8.0-windows` |
| UseWPF | `true` |
| ApplicationHighDpiMode | `PerMonitorV2` |
| Single-instance | Mutex `QubiCostElectrical_SingleInstance` ✓ |
| Handoff `.pepro` | `SingleInstanceHandoffService.TryExtractOpenProjectPath` ✓ |
| Autosave | `WpfAppHost` → `AutoSaveService` ✓ |
| Backup | `WpfAppHost` → `BackupService` ✓ |
| Recovery | `WpfAppHost` → `RecoveryService` ✓ |
| Errores no controlados | `GlobalErrorHandler.Initialize()` ✓ |
| Logging | `WpfAppHost.Logger` (FileLogger) ✓ |
| Icono `.ico` | **No configurado** (gap cosmético) |
| `ApplicationIcon` en csproj | **No configurado** (gap cosmético) |
| `app.manifest` | **No tiene** (WinForms sí tiene, pero es minimal y no aporta nada crítico) |
| Asociación `.pepro` | Vía instalador Velopack (fuera de scope de esta fase) |
| AppUserModelID | No configurado explícitamente (usa default de Windows) |

---

## 6. Flujos Críticos Verificados

| Flujo | Evidencia | Estado |
|---|---|---|
| Crear proyecto | `ProjectLifecycleTests` (9 tests) | ✓ |
| Abrir `.pepro` | `ProjectLifecycleTests`, `ProjectPickerTests` | ✓ |
| Guardar | `ProjectLifecycleTests` | ✓ |
| Guardar como | `ProjectLifecycleTests` | ✓ |
| Cerrar proyecto | `ProjectLifecycleTests` | ✓ |
| Recientes | `RecentProjectsServiceTests` (19 tests) | ✓ |
| Single-instance | `SingleInstanceTests` (7 tests) | ✓ |
| Handoff `.pepro` | `SingleInstanceTests` | ✓ |
| Autosave | `AutoSaveServiceTests` (via Infrastructure) | ✓ |
| Recuperación | `SafeSaveServiceTests`, `ProjectLockServiceTests` | ✓ |
| Backups | `SafeSaveServiceTests` | ✓ |
| Monedas | `CurrencyViewModelTests` (19 tests) | ✓ |
| Preferencias | `PreferencesViewModelTests` (30 tests) | ✓ |
| Membrete | `LetterheadTests` (46 tests) | ✓ |
| Órdenes de cambio | `ChangeOrdersViewModelTests` (55 tests) | ✓ |
| Undo/Redo | `WpfUndoServiceTests` (37 tests) | ✓ |
| PDF | `QuestPdfProjectExporter` (via Infrastructure) | ✓ |
| DOCX | `PresentationLetterDocxExporter` (via Infrastructure) | ✓ |
| XLSX | `ExcelExportTests` (via Infrastructure) | ✓ |
| Exportación comercial | `DeliveryViewModelTests`, `ExportProjectContextTests` | ✓ |
| Compatibilidad `.pepro` | `SQLiteProjectRoundTripTests`, `ItemizadoCompatibilityTests` | ✓ |
| Revisión y versiones | `ShellReviewAndVersionsTests` (19 tests) | ✓ |
| Navegación shell | `ShellNavigationTests` (5 tests) | ✓ |

---

## 7. Compatibilidad `.pepro`

El formato `.pepro` (SQLite con schema versión 20) es gestionado completamente por `PresupuestoElectrico.Infrastructure`, que no tiene dependencias en WinForms. Las migraciones automáticas desde formatos anteriores siguen funcionando. Tests de round-trip confirman compatibilidad.

---

## 8. Resultados de Build

### `QubiCostElectrical.Wpf.sln` (nueva)

| Operación | Resultado |
|---|---|
| `dotnet restore` | ✓ Sin errores |
| `dotnet build -c Debug` | ✓ 0 errores |
| `dotnet build -c Release` | ✓ 0 errores |
| `dotnet test --no-build` | ✓ 1109/1109 passed |

### `PresupuestoElectrico.sln` (completa, sin modificar)

| Operación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✓ 0 errores |
| `dotnet build -c Release` | ✓ 0 errores |
| `dotnet test --no-build` | ✓ 1109/1109 passed |

---

## 9. Total Exacto de Pruebas

| Solución | Tests totales | WinForms-específicos | WPF-específicos | Compartidos |
|---|---|---|---|---|
| `QubiCostElectrical.Wpf.sln` | 1109 | 46 | 292 | 771 |
| `PresupuestoElectrico.sln` | 1109 | 46 | 292 | 771 |

Los 46 tests WinForms-específicos requieren que WinForms esté en la solución. Cuando se retire WinForms, estos tests deberán eliminarse o reemplazarse.

---

## 10. Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| `PresupuestoElectrico.Tests` acoplado a WinForms | Alta (ya existe) | Bajo (solo build) | Separar tests WinForms en proyecto propio antes del retiro |
| WPF sin `.ico` en ejecutable | Alta (ya existe) | Muy bajo (cosmético) | Agregar `ApplicationIcon` al csproj de WPF |
| WPF sin `app.manifest` | Alta (ya existe) | Muy bajo | El manifest de WinForms es minimal; WPF hereda defaults |
| Asociación `.pepro` depende de Velopack | Alta | Medio | Se resuelve en fase de empaquetado, fuera de scope |
| 14 tests legacy WinForms sin equivalente WPF | Media | Bajo | Los flujos son cubiertos por tests WPF nuevos |

---

## 11. Recomendación de Retiro

### Criterios evaluados

| Criterio | Estado |
|---|---|
| WPF no referencia WinForms (producción) | ✅ CUMPLE |
| La solución independiente compila | ✅ CUMPLE |
| Funciones de producción existen en WPF | ✅ CUMPLE (Monedas, Preferencias, Membrete, OC, Undo/Redo) |
| No hay recursos requeridos dentro de WinForms | ✅ CUMPLE |
| Formatos `.pepro` siguen compatibles | ✅ CUMPLE |
| Tests compartidos no dependen de WinForms | ❌ PENDIENTE — Tests.csproj referencia WinForms |
| Flujos manuales críticos pasan | ⚠️ VERIFICADO via tests; prueba manual con teclado requiere usuario |

### Veredicto

## RETIRO PARCIAL RECOMENDADO

**Fundamento:**

El ejecutable `QubiCostElectrical.Wpf.exe` es operativamente independiente de WinForms. No hay ninguna dependencia de producción entre WPF y WinForms. La app WPF puede lanzarse, cargar proyectos, operar y exportar sin que WinForms esté en el sistema.

El único acoplamiento restante es **en el proyecto de tests**: `PresupuestoElectrico.Tests.csproj` referencia `PresupuestoElectrico.WinForms` para compilar 14 archivos de test en la carpeta `tests/WinForms/`. Estos tests verifican la arquitectura interna de WinForms (PageNavigator, AppTheme, MainForm guards) y no tienen equivalente WPF porque protegen funcionalidad que WPF no necesita.

**Para llegar a LISTO PARA RETIRAR** se requiere:
1. Mover los 14 archivos de `tests/WinForms/` a un proyecto separado `PresupuestoElectrico.WinForms.Tests` (no incluido en la solución WPF).
2. Eliminar la referencia a `PresupuestoElectrico.WinForms` de `PresupuestoElectrico.Tests.csproj`.
3. Autorización explícita del usuario para eliminar WinForms.

**WinForms permanece disponible como respaldo** sin ninguna penalidad para WPF.
