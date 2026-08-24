# TODOS — Qubi Cost Electrical

Plan operativo CEO (90 días → **1.0.0 GA**). Objetivo: producto vendible sin vergüenza a instaladoras 3–8 estimadores en Chile.

**Posicionamiento:** presupuesto y oferta eléctrica. No obra/estados de pago (post-GA).

**Versión actual:** `1.1.0`  
**Siguiente hito:** Semana 12 — GA comercial (tag, instalador firmado)

---

## Puerta Fase 1 ✅ · Fase 2 ✅

Ver `docs/PRE_WEEK5_GATE.md`, `docs/PHASE2_WEEKS_5_8_REPORT.md`.

- [x] Smoke automatizado (`docs/SMOKE_TEST.md` + `scripts/Run-SmokeAutomated.ps1`)
- [ ] Smoke humano GUI (instalador, flujo visual, DPI)

---

## Fase 3 — Diferenciación (semanas 9–12) ⏳ EN CURSO

### S9 — Export impecable ✅
- [x] Encoding QuestPDF (adelantado)
- [x] Golden-file Excel (`ExcelExportGoldenFileTests`)

### S10 — Cubicación SC ⏸️ OMITIDA
- [x] Match por código (adelantado)
- [ ] Smoke manual flujo SC — **pospuesto**

### S11 — Banco corporativo ✅
- [x] Ruta UNC Preferencias + `ProductPaths` helpers
- [x] Tests `CorporatePriceBankTests` + `ProductPathsTests`
- [x] `docs/PRICE_BANK_SHARED_FOLDER.md`

### S12 — GA comercial
- [x] Versión `1.0.0`, BC3 documentado
- [ ] Tag `v1.0.0`, instalador firmado, smoke completo

---

## Deuda de arquitectura — CubicacionViewModel ✅ RESUELTA

Auditoría CEO + refactor en `feature/rediseno-ejecutivo` (ver plan completo en
`~/.claude/plans/glittery-stargazing-hippo.md`). Los 7 archivos parciales que mezclaban
estado y lógica de negocio (`Totals.cs`, `Autocomplete.cs`, `Session.cs`, `Persistence.cs`,
`Loading.cs`, `Sectors.cs`, `RowMutations.cs`, `Clipboard.cs`) quedaron descompuestos en
clases colaboradoras reales e inyectadas (`CubicacionTotalsCalculator`,
`CubicacionAutocompleteService`, `CubicacionSessionCoordinator`,
`CubicacionPersistenceService`, `CubicacionLoadingService`, `CubicacionSectorService`,
`CubicacionRowMutationService`, `CubicacionClipboardCoordinator`), mismo patrón `parent:
CubicacionViewModel` en todas. `CubicacionViewModel.cs` y los 8 archivos `.X.cs` restantes
son ahora fachadas delgadas (wrappers de una línea) que preservan cada firma pública/internal
que ya conocían `CubicacionView.xaml.cs`, `CubicacionRightPanelVm` y `CubicacionMedicionesVm`.
Cero cambio de comportamiento observable — build limpio + 1696/1696 tests verdes verificado
después de cada uno de los 7 colaboradores, un commit por colaborador.

- [x] **`CubicacionViewModel.FormulaBar.cs` código huérfano — RESUELTO.** El archivo y
  sus 2 tests (`CubicacionViewModelTests.cs:94,534`) ya no existen en el repo (verificado
  2026-08-24: `find`/`grep` no encuentran ni el archivo ni las referencias). Se eliminó en
  algún punto posterior a esta nota; queda solo como registro histórico.
- [ ] **Smoke test manual pendiente** (no automatizable: `Application.Current` es `null`
  en el entorno headless de xunit). Antes de dar el refactor por completamente cerrado,
  probar en vivo cada flujo de la tabla de verificación del plan: autocompletado de
  código, cerrar/reabrir proyecto, guardar (Ctrl+S), cambiar de partida, Hoja de
  Cubicación/Zonas, agregar/duplicar/mover/eliminar filas, pegar desde Excel y "Agregar
  material rápido".
- [ ] **Nota para quien retome trabajo futuro en esta zona:** dos lecciones de este
  refactor. (1) La premisa de "3 secuencias post-edición idénticas para consolidar" en
  `RowMutations.cs`/`Clipboard.cs`/`Sectors.cs` resultó **falsa** al verificar con grep
  directo (7+ secuencias con orden y contenido distintos) — no proponer consolidarlas sin
  releer el código real primero. (2) Antes de nombrar una clase nueva, grepear el nombre
  en todo `src/` — `CubicacionClipboardService` ya existía como utilidad estática de
  parseo de Excel en `QubiCostElectrical.Application.Services`, lo que habría causado una
  colisión de nombres silenciosa; el colaborador de Clipboard se llama
  `CubicacionClipboardCoordinator` por eso.

## Auditoría de mejora — rendimiento, código muerto, duplicados ✅ Fases 1–3 · ⏳ Fase 4 pendiente

Plan completo en `~/.claude/plans/glittery-stargazing-hippo.md`, `feature/rediseno-ejecutivo`.
Investigado con 2 agentes Explore en paralelo + verificación personal + voz externa
independiente (sin `isolation: "worktree"`, que en sesiones anteriores dio falsos negativos).

**Fase 1 — código muerto (hecho):** 10 clases sin callers de producción eliminadas
(`TechnicalRuleValidationService`, `UiErrorPolicy`, `BudgetWorkflowStatusService`,
`MaterialCurrencyService`, `CubicacionKeyboardShortcutPolicy`, `PricingRuleContext`,
`WorkflowStepStatusRow` + `EstadoWorkflowStep` huérfano, `ProactiveAlertDefaults`,
`AppTextValidator`/`TextValidationResult`). `TechnicalRuleValidationService` y `UiErrorPolicy`
eran features completas (lógica + persistencia) sin ninguna UI que las conectara — decisión
explícita del usuario fue borrar, no conectar.

**Fase 2 — rendimiento alto impacto (hecho):** `BudgetEngine.Calculate` pasó de O(N partidas
con APU × M filas/ítems) a índice precomputado (`BuildItemizadoIndex`/`BuildAreaIndex`), mismo
patrón que ya tenía `FinalReviewService`. `QuotesViewModel.FilterProvider` pasó de escanear
todo el banco de precios por proveedor visible (cuando hay categoría activa) a un `HashSet`
precomputado.

**Fase 3 — rendimiento medio/menor (hecho):** arranque ya no bloquea el hilo de UI
(`App.xaml.cs`, `TryRestoreSessionAsync` ahora `await`); `ApuTemplateStore` ya no re-ejecuta
`CREATE TABLE`/`CREATE INDEX` en cada tecla del filtro de Plantillas; `CubicacionScViewModel`
cachea las 8 colecciones de catálogo en vez de reconstruirlas por cada fila virtualizada del
`DataGrid`; `PriceBankViewModel.UpdateSummary` cachea conteos de proveedores/categorías en vez
de recalcularlos en cada refiltrado.

- [ ] **`ApuWorkspaceVm.ApplyFilter`** (`Clear()` + `foreach Add()` completo por tecla) —
  volumen bajo hoy (cientos de partidas, no miles), quedó fuera de esta ronda a propósito.
  Vigilar si crece.
- [x] **Fase 4 — descomposición de archivos grandes — RESUELTA.** Verificado 2026-08-24: los
  19 archivos listados aquí (incluidos los dos "más urgentes") ya no superan 260 líneas —
  p. ej. `CubicacionView.xaml.cs` 1682→45, `DataGridExcelNavigation.cs` 1094→41,
  `ShellViewModel.cs` 789→37, `QuestPdfProjectExporter.cs` 882→38, `SqliteMigrationSteps.cs`
  548→9 — evidencia de que ya fueron divididos en archivos parciales/colaboradores en algún
  punto posterior a esta nota.

## Bugs preexistentes — búsqueda y fix ✅ RESUELTO

A pedido explícito del usuario tras la auditoría de mejora: buscar y corregir bugs
preexistentes en toda la app (no solo rendimiento/limpieza). Investigado con `/investigate`
+ 2 agentes Explore en paralelo (Infrastructure/SQL, capa WPF/ViewModels), cada hallazgo
verificado leyendo el código real antes de tocar nada, siguiendo la Ley de Hierro (causa
raíz confirmada antes de escribir cualquier fix).

- [x] **CRÍTICO — `CubicacionLoadingService.LoadCubicacionRows` async roto.** Una reescritura
  sin commitear (`Task.Run(...).ContinueWith(...)` + `Application.Current?.Dispatcher.
  InvokeAsync(...)`) rompía el contrato síncrono del método; con `Application.Current == null`
  (tests, posible shutdown) la continuación completa nunca corría — filas vacías para siempre.
  Revertido a la versión síncrona con `Dispatcher.Invoke(Render)` ya commiteada, que ya
  resolvía el objetivo (percepción de UI no congelada) sin ese riesgo. 34 tests que fallaban
  ahora pasan.
- [x] **CRÍTICO — "Cerrar sesión" cerraba TODA la aplicación en vez de volver al login.**
  `ShellWindow.Closing` llama `Application.Current.Shutdown()` incondicionalmente
  (`ShutdownMode="OnExplicitShutdown"` en `App.xaml`); `ShellViewModel.Logout()` reutilizaba
  `Window.Close()` sobre esa misma ventana para destruirla antes de mostrar el login, disparando
  ese shutdown antes de que `login.ShowDialog()` corriera. Fix: `ShellWindow.
  SuppressShutdownOnClose` (bandera que solo `Logout()` activa). De paso: `ShellViewModel` no
  implementaba `IDisposable` pese a 5 suscripciones de larga vida + un `AutoSaveService` propio
  con `Timer` interno nunca liberado — mismo patrón de leak ya corregido en otras 8 pantallas
  (`SessionChangedSubscriptionLeakTests.cs`). Cada ciclo login/logout habría acumulado un
  `ShellViewModel`+`Timer` fantasma más. 2 tests de regresión nuevos.
- [x] **Búsqueda no encontraba texto con acentos.** `ApuTemplateStore.Search` y el fallback de
  `PriceBankStore.Search` (cuando FTS5 lanza excepción) usaban `UPPER()` de SQLite — ASCII-only
  sin extensión ICU, "tuberias" no encontraba "Tuberías". Fix: `SearchTextNormalizer` (nueva,
  compartida) filtra en memoria con la misma técnica de normalización Unicode ya usada en
  `DataGridExcelNavigation.NormalizePasteHeader`.
- [x] **Fechas de autenticación se corrían por zona horaria al leer.** `SqliteAuthStore` guarda
  fechas con `ToString("O")` (round-trip UTC) pero las leía con `DateTime.Parse(string)` sin
  `DateTimeStyles.RoundtripKind` — el "Z" (UTC) se convertía a hora local del equipo en cada
  lectura, corriendo `CreatedAt`/`LastLoginAt`/`LockedUntil`. Más grave de lo que parecía a
  primera vista (no solo un edge case de calendario no gregoriano). No existía ninguna
  cobertura de test para esta clase; se agregó `SqliteAuthStoreTests.cs`.
- [x] **Catch silencioso en export PDF.** Si el logo/firma de la oferta existía pero fallaba al
  leerse (bloqueado, corrupto, permisos), el error se tragaba sin rastro. Ahora se loggea con
  `AppLog.Warning` antes de continuar sin la imagen (mismo comportamiento de fallback, solo
  agrega diagnóstico).
- [x] **Reportado por el usuario — un código aplicado por autocompletado desaparecía al volver
  a la partida.** Investigado con `/investigate`. Causa raíz: `session.Cubicacion` (lista
  "legacy", lo que edita la grilla) y `CubicacionRecursos`/`Valorizaciones`/`ZonaCantidades`
  (composición "normalizada", usada por Zonas/Mediciones) son dos representaciones separadas
  del mismo dato; `session.CreateSnapshot()` (disparado por CADA punto de Undo y por Guardar)
  reconstruye la lista legacy desde la normalizada — si una edición no sincroniza hacia la
  normalizada primero, ese `CreateSnapshot()` la revierte en silencio. Las casillas de sector
  (`CubicacionSectorService`) y la edición de celda normal (`CubicacionPersistenceService.
  OnRowEdited` vía `CubicacionPresenter.AfterRowEdited`) ya sincronizaban.
  `CubicacionRowMutationService.ApplyMaterial` (aplicar una sugerencia del autocompletado — la
  forma más común de "agregar un código") NO lo hacía: al aplicar un SEGUNDO material sobre una
  fila que ya tenía un recurso normalizado, el recurso quedaba obsoleto y el próximo snapshot
  revertía Código/Descripción al valor viejo (la Cantidad, escrita en una casilla, sí
  sobrevivía — de ahí la confusión del reporte). Mismo hueco encontrado y corregido en
  `RefreshPricesForSelectedRows` (botón "Actualizar precio") y `CubicacionClipboardCoordinator.
  ApplyClipboardRows` (pegado con vista previa desde Excel). 2 tests de regresión nuevos
  (`ApplyMaterialCommand_ThenCreateSnapshot_PersistsCodigoAndDescripcion`,
  `RefreshPricesCommand_ThenCreateSnapshot_PersistsUpdatedPrice`), cada uno confirmado fallando
  sin el fix y pasando con el fix restaurado.

## Completados (histórico)

Fases 1–2 ✅ · S9 + S11 ✅ (2026-06) · FTS5 PriceBankStore ✅ · QuestPDF licencia ✅ ·
Contratos — ciclo de vida completo ✅ (`1.1.0`, 2026-08-24, ver `CHANGELOG.md`) ·
Limpieza de repo — bin/obj, `_cleanup_2026-08-21/`, PDF suelto en raíz, script de scratch y
plantilla `QubiSpatialElectricalTwin` sin relación al proyecto activo, eliminados ✅
(2026-08-24)
