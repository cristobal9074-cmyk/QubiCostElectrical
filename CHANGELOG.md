# Changelog

Las versiones siguen el formato `MAJOR.MINOR.PATCH` y corresponden a `Directory.Build.props` + `VersionSuffix`.

## 1.1.1

### Cierre Comercial GA y Estabilización UI (2026-09-08)
- **Smoke Test Humano Semana A (PASS)**: Validación y firma formal en `docs/SMOKE_TEST.md` de los 5 bloques críticos (Cubicación completa, Cubicación SC, roles y permisos, persistencia con flush al salir y overlay en oferta/exportación). Artefacto SHA-256 verificado y archivado.
- **Endurecimiento del Rol Visor**: Blindaje completo de solo lectura en los 6 módulos principales y cabecera de Cubicación (`UnidadHeaderBox`, `CantHeaderBox`, `ObservacionHeaderBox` enlazados a `IsReadOnly`, setters con guard clauses ante `session.IsReadOnly`). Acciones de exportación estrictamente bloqueadas.
- **Persistencia y Resistencia a Fallos**: Implementación de guardado atómico con flush inmediato en segundo plano ante cierre abrupto con la «X» (<2,5 s) sin pérdida de datos en `.qce`.
- **Tokenización Tipográfica 100%**: Migración completa de tamaños de fuente literales a tokens del sistema de diseño semántico (`DesignSystem/Typography.xaml`) en vistas principales, Cubicación SC, diálogos secundarios (`QuickJumpPartidaDialog`, `ConjuntoPickerDialog`, `MaterialBatchReplaceDialog`, `SolicitudExcelPreviewDialog`, `SolicitudPdfPreviewDialog`, etc.) y controles de usuario.
- **Arquitectura y Sanidad de Código**: Desvinculación de stubs experimentales (`ScenarioWorkspace`) de rutas de navegación visibles; suite completa de 1.821 pruebas automatizadas y guardias de arquitectura en verde (0 fallos, 0 advertencias).

### Motor de cálculo v5 — la política de cierre comercial manda sobre el precio de venta (2026-09-01)
- **Problema**: `ClosingPolicySelection` (6 políticas: Costo+Markup, Precio Objetivo, Suma
  Alzada/EPC, Costo+Fee, Precio Unitario Puro, Gainshare/Painshare) se persistía por proyecto
  pero solo alimentaba un panel informativo en la pantalla Costo. Export, dashboard, oferta y
  Revisión Final leían siempre `TotalVentaUf` (= Costo+Markup).
- **Punto de integración único**: `BudgetTotalsCalculator.Build`. Nuevo paso final común
  `FinalizeVentaYCierre`: se calcula la venta base neta del motor (post-factores) y luego
  `ClosingPolicyEngine.ApplyNeto` la transforma en el **neto de la política** (pre-descuento,
  pre-IVA); descuento e IVA se aplican después, uniformes para las 6 políticas.
- **`ClosingPolicyEngine` — contrato NETO**: `Apply(CostBreakdown, …)` →
  `ApplyNeto(ClosingPolicyInput, …)`. `ClosingPolicyInput` (record) lleva `VentaBaseNetaClp`,
  `CostoEjecucionClp`, `PisoComercialClp`, `RiesgoImplicitoPct` y `Origenes`.
  `ClosingPolicyResult.VentaFinalClp` es ahora el neto pre-descuento y pre-IVA (antes traía todo).
  Se conserva `Apply(CostBreakdown, …)` como adaptador de compatibilidad para UI/tests.
- **`CostBreakdown`**: nuevos `ClosingPolicyResult?` y `ClosingPolicy` (tipo aplicado).
- **`BudgetSettings.ClosingPolicy`**: la política viaja desde `ProjectFileData` vía
  `ExcelCostosBridge.EnrichFromProjectData` (punto único); call-sites de Revisión Final /
  export / dashboard que no pasaban por ahí ahora también la enriquecen.
- **Bloqueante `UtilidadNegativa`**: se emite cuando el neto de la política no cubre el umbral
  que corresponde a esa política (ver "Revisión — policy-aware" más abajo). Revisión Final
  además convierte las advertencias de fondo de la política ("margen negativo" / "no cubre el
  costo de ejecución") en inconsistencia bloqueante y el resto en advertencia.
- **`CostoViewModel`**: la sección "Política de cierre" lee `cost.ClosingPolicyResult` del
  breakdown en vez de recalcular aparte.
- **Revisión — el bloqueo `UtilidadNegativa` ahora es policy-aware** (`FinalizeVentaYCierre`):
  - `CostPlusMarkup` / `UnitPricePure`: bloquea si el neto no cubre el **piso comercial**
    (los factores / precios unitarios deben cubrir GG + utilidad — regla C-1 original).
  - `TargetPrice` / `LumpSumEpc` / `GainsharePainshare`: bloquea solo si hay **pérdida de caja
    real** (`neto < costo de ejecución`). Entre el costo y el piso ⇒ advertencia NO bloqueante
    ("… bajo el costo del proyecto con utilidad …; margen reducido", generada en `ApplyNeto`),
    que Revisión Final muestra como advertencia. Antes toda oferta bajo el piso quedaba
    bloqueada, incluido el caso que la política existe para evaluar.
  - `CostPlusFee`: bloquea solo si el fee es ≤ 0 (`neto ≤ costo de ejecución`). Antes un
    Costo+Fee con fee normal (8–12 %) SIEMPRE quedaba bloqueado porque el piso apila GG +
    utilidad que este modelo, por diseño, no carga.
  - Se elimina el bloque duplicado de `BuildExcelCostos` que comparaba `neto0` (contaminado por
    la política) contra el costo del proyecto; la comprobación C-1 de la ruta por factores usa
    la venta PRE-política (`bruto`) y solo aplica con `CostPlusMarkup`.
  - `DiferenciaFactoresUf` (la "diferencia por factores" de la pantalla Costo) se fija en 0 para
    `CostPlusFee` / `TargetPrice` / `LumpSumEpc` / `GainsharePainshare`: el concepto solo aplica
    a las políticas que venden por factores.
- **`MargenRealPct`** se calcula contra el **neto** (post-descuento, pre-IVA) y ya no contra la
  venta con IVA. Antes, con `IncluirIvaEnTotal`, el IVA inflaba/deformaba el margen mostrado.
  Snapshot dorado: solo se movió ese diagnóstico (38 % → 26 % en los 3 modos); ningún monto
  cambió.
- **`BudgetEngine.AddCubicacionRow` — familia con factor 1.0**: el mapa de factores por familia
  (`BudgetSettings.PresentationFactorsByFamily`, poblado por `FromProject` desde `Variables`) es
  la fuente de verdad; el factor suelto de la fila (`GgUtFactor`) solo se usa como respaldo si
  la categoría no está mapeada. Antes una familia fijada a 1.0 caía al respaldo.
- `CalculationEngineVersion` 4 → 5. **Snapshot dorado sin cambios**: con la política por
  defecto `CostPlusMarkup` el paso es pass-through (`neto0 == venta base`) y el pipeline es idéntico.
- **Selector al crear el proyecto**: `NewProjectDialog` incorpora la sección "Modelo comercial"
  con 6 tarjetas (una por política, "Úsala cuando…") y un único campo de parámetro según la
  elegida (precio objetivo / contingencia mínima % / fee / costo objetivo). `NewProjectAsync`
  persiste la selección (`session.Data.ClosingPolicy` + `MarkDirty(ClosingPolicy)`) antes del
  primer guardado; viaja en el `.qce` y en los snapshots.
- **Salvaguardas**: `ClosingPolicySelection` sella `ParamsStampedAt` +
  `ParamsStampedCostoEjecucionClp` al fijar un monto absoluto (precio objetivo, fee fijo, costo
  objetivo, tope); la pantalla Costo avisa si el costo de ejecución se movió > 5 % desde
  entonces. Aviso permanente para `CostPlusFee` ("ignora factores por familia y GG/utilidad") y
  aviso menor para `TargetPrice` / `LumpSumEpc`. El cambio de tipo de política queda en la
  auditoría del proyecto (`ProjectAuditRecorder`).
- `BudgetVersionComparisonService` compara por línea de itemizado, no campo a campo: no se
  agregó fila "Política de cierre" al diff (la política ya viaja en el snapshot).
- **Consumidores que faltaba enriquecer (hallazgos de revisión)**:
  - `BudgetKpiService.GetMetricValue` (KPIs venta/margen de versiones guardadas) enriquecía el
    `BudgetSettings` solo con `FromProject`, así que la venta de toda versión se calculaba como
    Costo+Markup y comparar dos versiones con políticas distintas daba números erróneos. Ahora
    proyecta el `BudgetVersionData` (que ya sella `ClosingPolicy`, gastos operacionales, plan de
    dotación, descuentos y MO) a `ProjectFileData` y pasa por
    `ExcelCostosBridge.EnrichFromProjectData`. El snapshot ya persistía la política vía JSON de
    `BudgetVersionRow`; sin cambios de esquema SQLite.
  - `CubicacionViewModelService.Build` / `BuildSummary` aceptan un `BudgetSettings?` opcional ya
    enriquecido; si es `null` mantienen el `FromValues` mínimo (venta Costo+Markup), ahora
    documentado en la interfaz como preliminar. El panel de totales vivo de la pantalla
    Cubicación (`CubicacionPresenter.CalculatePartidaTotals`) no expone venta, así que no había
    número engañoso visible; el cambio deja alineable el resto de constructores de VM.
- **Fuga de conexiones — `ProjectDbContext.Rewire()`**: antes de reasignar `_contracts` en cada
  cambio de ruta de proyecto ahora se hace `(_contracts as IDisposable)?.Dispose()` (y también
  en `Dispose()`). Los `SqliteContract*Repository` son sin estado (una conexión por operación,
  cerrada con `using`) y `ContractService` hoy no es `IDisposable`: el descarte es defensivo y
  queda comentado.

### Motor de cálculo v4 — C-1 cerrado: rutas de markup unificadas (2026-08-30)
- **C-1 (raíz)**: un presupuesto por ruta de factores por familia podía calcular una pérdida
  en silencio. En `HasRowLevelMarkups` el motor hacía `bruto = Σ origin.VentaClp`, pero solo
  las filas de cubicación MAT llevaban factor: `AddApuRow`, `AddApprovedChangeOrders` y
  `AddGlobalPercentages` aportaban a la venta el costo puro (o 0), y `CostoProyectoUf` escondía
  el signo con `Math.Max(0, utilidad)`.
- **Unificación**: `venta = Σ (costo_origen × factor_presentación_familia)` para TODA línea.
  Los orígenes APU (por área de la partida), OC aprobadas y cargos globales de
  herramientas/mermas ahora llevan el factor de presentación; lo no clasificado usa el
  **factor por defecto del proyecto** (familia general, ≈ factor de MO, leído de
  `ProjectInfo.Variables` al cierre). El flag `FactoresPresentacionPorFamilia` pasa a
  significar "el proyecto usa factores por familia" (todas las líneas, no solo las de factor ≠ 1).
- **`BudgetSettings`**: nuevos `PresentationFactorsByFamily` / `DefaultPresentationFactor` /
  `ResolvePresentationFactor(family)`, poblados en `FromProject` desde
  `Variables["MarkupFactor_*"] × (1 − MarkupDiscount_*/100)`. Factores 100% por proyecto,
  nunca fijos. Regla de familia (normalización + tabla de defaults) unificada en
  `FamilyPresentationDefaults` (Domain); Application delega.
- **Inconsistencia BLOQUEANTE `UtilidadNegativa`**: si la venta por factores cae bajo el piso
  GG+utilidad+VA del costo del proyecto, se emite en el cálculo y Revisión Final impide
  exportar ("Generar oferta").
- `CalculationEngineVersion` 3 → 4. Sin cambios para proyectos sin factores por familia
  (factor 1, ruta aditiva idéntica).

### Composición: contenedor DI real (2026-08-28)
- Migración de la composición manual (`AppServicesBuilder` 374 líneas + `AppServices` con dos
  ctores y setters mutables + `WpfAppHostBuilder` + `WpfAppHost.Current` estático) a
  **Microsoft.Extensions.DependencyInjection** 10.0.11.
- 4 módulos de registro por capa: `AddDomainServices` / `AddApplicationServices` /
  `AddInfrastructureServices` / `AddWpfServices`. Registro explícito por interfaz, lifetime
  singleton, grafo validado (`ValidateScopes`).
- **Raíz única**: `CompositionRoot.Build()` en el arranque. **`AppServicesBuilder` y
  `WpfAppHostBuilder` eliminados** como código de producción (`AppServicesBuilder` queda como
  helper de tests).
- `IProjectDbContext` reemplaza la closure `RewireContractsIfProjectChanged`: una sola
  suscripción a `Session.Changed` reconstruye los repos de contratos contra la BD del `.qce`.
- `StartupSeeder.SeedPriceBank` saca el I/O de disco de la construcción del grafo.
- `Func<BudgetEngine>` inyectado en `BudgetCalculationService`, `FinalReviewService`,
  `ProjectUseCases` (adiós a los `new BudgetEngine()` sueltos).
- `ShellPageFactory` resuelve los 27 ViewModels de página del contenedor
  (`ActivatorUtilities.CreateInstance` con el `IShellNavigationHost` vivo como arg runtime).
- Fitness function `ServiceProviderValidationTests` (build+validate del grafo y resolución de
  los 28 ViewModels de página vía `ActivatorUtilities`).
- **Los 28 ViewModels migrados a inyección por constructor**: cada ctor pasa de
  `(IShellNavigationHost, WpfAppHost)` a dependencias concretas por interfaz; se eliminan los
  64 `host.Services.X` (service locator). Los sub-servicios de Cubicación
  (`CubicacionPersistenceService`, `CubicacionSectorService`, `CubicacionRowMutationService`,
  `CubicacionClipboardCoordinator`), Export (`DeliveryPreviewService`,
  `DeliveryDocumentSelectorVm`), Shell (`AutoSaveService`, `ShellNavigationCoordinator`,
  `ShellProjectLifecycle`) y `MaterialBatchReplaceDialog` también reciben deps concretas.
  Cada VM conserva un ctor `(…, WpfAppHost)` de compatibilidad que delega en el primario.
- `IAuthService` registrado en el contenedor (`→ AuthService`).
- Pendiente (volumen, no riesgo; el grafo ya está 100% en MEDI): borrar las fachadas
  `AppServices`/`WpfAppHost` y los ctores de compatibilidad; estáticos
  (`AppClock`→`TimeProvider`, `ProductPaths`→`IProductPaths`, `UserPreferencesStore`,
  `SessionManager`).

### Reproducibilidad y correctitud del motor de presupuesto (2026-08-27)
- Red de reproducibilidad: `BudgetEngineReproducibilityTests` (determinismo, independencia de
  orden, incremental == completo, suma de oferta, snapshot dorado al peso) — 3 modos × con/sin
  factores por familia.
- **C4**: la venta por línea se calcula directo en CLP (`precio × factor × cantidad`); se
  elimina el viaje CLP→UF→redondeo(3 dec)→CLP y el valor UF de respaldo `39000` hardcodeado.
  `AddApuRow` ya no pasa `apu.PrecioUnitUf` (CLP) por `UfToClp` — eso multiplicaba CLP × valor
  UF y disparaba la venta ~2400× cuando había factores por familia + cualquier APU.
- **C3**: `MarkupPolicy.FactoresPresentacionPorFamilia` (opt-in, `ProjectInfoRow
  .UsarFactoresPresentacionFamilia`). Un `GgUtFactor ≠ 1` suelto ya no cambia el método de
  markup de todo el presupuesto; solo lo hace si el proyecto lo activó (al configurar markups
  por familia).
- **C1**: en modo complementario el APU solo aporta equipos, subcontratos y recursos HERR/TR
  explícitos + imprevistos de alza sobre esa base. Antes cargaba herramientas/transporte/merma
  calculadas sobre material y MO del APU que la cubicación ya cubre.
- **C2**: la merma de nivel APU (`ApuRow.MermaPct`) no se compone con la merma propia del
  recurso — solo aplica a los materiales sin desperdicio declarado. Inconsistencia
  `MermaDobleNivel` cuando coexisten.
- **C5**: la oferta al cliente se prorratea por la venta real por partida (`origin.VentaClp`,
  que ya incluye el factor de presentación), no por el costo directo; sin piso artificial; la
  última partida absorbe el redondeo para que la suma cuadre exacto con la venta neta.
- **A1**: helper único `Money.Round` (peso entero) para totales de presentación; el cálculo
  intermedio queda a precisión plena `decimal` sin redondeos intermedios.
- Revisión: la venta de línea usa `PrecioUnitClpConFallback` (misma fuente que el costo) para
  no quedar en 0 en proyectos legacy sin `MatClp`; migración en `ProjectSnapshotIntegrity` que
  enciende `UsarFactoresPresentacionFamilia` si el proyecto ya trae `GgUtFactor ≠ 1` (preserva
  el total al reabrir); `FinalReviewService` respeta overrides comerciales y `MermaDobleNivel`
  solo se reporta en modo APU completo; bordes de la oferta (venta ≤ 0, ajuste de redondeo
  negativo, itemizado solo con títulos).
- **Abierto (C-1)**: en la ruta por factores por familia, los orígenes APU/OC aportan a la venta
  sin markup de presentación → un proyecto APU-dominante puede quedar con utilidad negativa.
  Test `FamilyMarkupPath_ApuHeavy_KeepsPositiveMargin` (skip) documenta el caso. Pendiente
  decidir el modelo de composición row-level vs markup global en `BudgetTotalsCalculator`.

### Endurecimiento arquitectónico (Ola 1–4)
- Plan: `docs/ARCHITECTURE_HARDENING_PLAN.md`
- `ProjectDirtyScope` tipado + hot-path `MarkDirty` migrado (scopes GO/descuentos/staffing)
- `ProjectDirtyTracker` / `ProjectAccessGate` internos en sesión
- Lock SMB: `OwnsActiveLock` + RO al `LockLost` + cancel debounce; `SaveAsync` exige lease
- Ola 4: lease mid-write (`TryBeginWriteLease`), `RequiresExclusiveLease` / `ProjectLockLostException`, sidecar en LockLostPending, exit sin `.qce`
- Costo testeable sin WPF: `CostoBreakdownLineBuilder`, `CostoMoLaborProjectionBuilder`, `CostoGoComposeProjectionBuilder`
- Cub: `PartidaCantidadVariablesBuilder` (R1..Rn) + `CubicacionSectorExpressionCompactor` + `CubicacionSectorNavigation`
- Export: `OfferPackagePresetCatalog` + `ExportEligibilityEvaluator`; GO: `GastoOperacionalCategoryMaintenance`
- Gate de líneas por tipo parcial (no escape `*.Methods.cs`)
- Shell: `ShellPageFactory` + `ShellPageCache` + `ShellNavigationCoordinator` + `ShellProjectLifecycle`
- `WpfAppHostBuilder`; coalesce `Session.Changed` (75 ms, batch por Kind) + `ISessionChangeAware`
- Dual-path eliminado: LIVE → `Features/`; stubs DEAD purgados

### Contrato UI/UX sistema (2026-08-25)
- Plan: `docs/UI_SCREEN_CONTRACT.md`
- `WorkspacePageViewModelBase`, `WorkspacePageChrome`, `ModuleFeedback`
- Tokens densos: `Font.Size.kpi/price/hero/micro`, `Text.SectionTitle`, `Brush.HeroPanel`
- Excel-nav declarativo: ~30 grillas (`Enable`) — editables + previews RO (pickers, paste, catálogo, viáticos resumen, SolicitudExcel…)
- Costo Fase A+B: tokens `Font.Size.*` + estilos `Text.SectionTitle` / `Helper` / `KpiInline`
- Tokens densos APU / EPP / MO: 0 `FontSize` literales
- Contrato empty+skeleton: ApuCatalog, ApuMerge, Contracts (3), ScenarioWorkspace (WIP sin menú)
- Chrome compacto 720p: stepper + banner RO; guardar sin toast duplicado
- Gate: `WorkspacePageContractTests` (15 vistas core con empty + skeleton)

### Confianza al salir / export (2026-08-25)
- Flush silencioso al cerrar con X / Alt+F4 / Shutdown (`PrepareForAppExitAsync` + `AutoSaveService.FlushPersistAsync`)
- Primary «Generar oferta →» usa `DeliverPackageCommand` + `IsBusy` / `InverseBool`
- Cubicar multi-selección con cola FIFO + toast «Siguiente»
- Tomar SC confirma sobrescritura; Visor RO en Documents/Plantillas; skeletons Banco/Proyecto/Docs/Cotizaciones
- SC modo entrada/avanzado oculta columnas técnicas; atajos shell/itemizado en Preferencias
- Excel-nav en Banco y Plantillas; stepper visible con proyecto abierto

### Fluidez de guardado (profesional)
- Cambiar de pestaña ya no pide Guardar/Descartar: aplica el draft y autoguarda el `.qce`
- Autoguardado al disco con debounce 2,5 s + sidecar de recuperación cada 15 s
- Cerrar proyecto guarda en silencio; Ctrl+S sigue disponible

### UI premium
- Dashboard renovado con tarjetas premium, KPIs y flujo de producción integrado
- Tokens de diseño unificados (`Badge.*`, `Dashboard.PremiumCard`, tipografía y paleta corporativa)
- Empty states consistentes en módulos core (itemizado, banco de precios, plantillas)
- Preferencias con barra lateral de navegación por secciones
- Toasts no modales en el shell (`ToastHost` + `ShellNotificationService`)
- Skeleton de carga reutilizable para pantallas con datos asíncronos
- Toasts con acción para deshacer y abrir carpeta
- Tokens MO/EPP/SC sin hex sueltos en la UI objetivo
- Empty state de Versiones alineado al design system
- Documentación comercial y de humo actualizada (`docs/SMOKE_TEST.md`, `docs/DEMO_SCRIPT_15MIN.md`, `docs/FIRST_RUN_TEAM_ONBOARDING.md`)

### UX polish P0–P2 (2026-08-25)
- Copy sober: Dashboard, Export, EPP, Mano de obra y Documentos (sin tono SaaS / anglicismos)
- Anti-slop en Costo/EPP: chips con `Radius.Chip`, CTA viáticos con `Button.Primary` (sin gradiente ni DropShadow)
- Márgenes de página Preferencias/Itemizado con token `Padding.Page`
- Fade de cambio de ruta en shell alineado a `Anim.FadeIn`

### Distribución local unificada
- Launcher canónico: `publish\win-x64\QubiCostElectrical.exe` (mismo exe para acceso directo del escritorio y doble clic en `.qce`)
- `Update-DesktopShortcut.ps1` apunta por defecto al publish; `-RegisterAssociation` alinea el ProgId HKCU
- Evita abrir builds distintas (bin de desarrollo vs publish portable)

## 1.1.0

### Contratos — ciclo de vida completo
- Nuevo módulo Contratos: creación, envío a revisión, aprobación/rechazo, suspensión/reactivación, cierre, término y rescisión
- Segregación de funciones (SoD): quien crea/edita un contrato no puede aprobarlo ni cerrarlo
- Esquema SQLite por proyecto (`Migration032`): `contracts`, `contract_parties`, `contract_milestones`, `contract_scope_links`, `contract_documents`, `contract_versions`, `contract_audit`
- Versionado de contratos con hash de integridad (`ContractVersion.ComputeHash`) y verificación de manipulación
- UI: espacio de trabajo de contratos, editor y comparador de versiones, integrados en la navegación (grupo «Cierre»)
- 7 tests de integración contra SQLite real cubriendo ciclo de vida, SoD, integridad y concurrencia optimista

## 1.0.0

### General Availability — presupuesto y oferta eléctrica
- Gate de exportación sin precios congelados (congelar / continuar con riesgo / cancelar) + auditoría
- **Fase 2 (semanas 5–8):** stepper «Línea de producción» en shell (Datos → Exportar + C.SC)
- Design system en flujo core: `Form.ComboBox`, `Form.TabControl`, `Form.ProgressBar`, `DataGrid.Workspace`
- Comparación versiones **A vs B** en módulo Versiones (UI + tests VM + puerta `WpfPhase2GateTests`)
- Golden-file Excel interno (`ExcelExportGoldenFileTests`) — 11 hojas, totales alineados al motor
- Banco corporativo: `ProductPaths` UNC + tests carpeta compartida (`CorporatePriceBankTests`)
- Cubicación SC: match de materiales por código además de descripción
- UI: `AppMessageDialog`, sin títulos duplicados, Delivery legacy retirado
- PDF oferta: corrección de encoding UTF-8 en `QuestPdfProjectExporter`
- Tests: Itemizado/Cubicacion VM, PriceBankStore, Export freeze, Shell workflow, Versiones A/B
- BC3 explícitamente fuera de alcance (`docs/BC3_SCOPE.md`)
- Informe cierre Fase 2: `docs/PHASE2_WEEKS_5_8_REPORT.md`

## 1.0.0-rc.4

### Arquitectura y producto
- Identidad unificada: `ProductIdentity`, `ProductPaths`, datos en `%LOCALAPPDATA%\QubiCostElectrical`
- `docs/PRODUCT.md` como fuente operativa; `CLAUDE.md` sincronizado
- Presenters: `CubicacionPresenter`, `ExportPresenter`; `ExportScopeService` en Application
- Cubicación: mediciones conectadas a `CubicacionCompositionService` en edición
- UI: `ItemizadoImportPreviewDialog` XAML + design system; eliminados placeholders y mapping dialog huérfano
- Design system: `Dialogs.xaml` para ventanas modales

## 1.0.0-rc.3

### Confianza y distribución
- Pipeline Authenticode: `deploy/Sign-Authenticode.ps1`, CI con secretos, `-Sign` en builds
- Identidad Qubi Cost congelada: `Verify-ProductIdentity.ps1`, splash y PDF sin marca legacy
- QuestPDF: licencia declarada en `QuestPdfBootstrap` + `THIRD_PARTY_NOTICES.txt`
- Docs operativos actualizados: `AGENTS.md`, `RELEASE_PROCESS.md`, `SMOKE_TEST.md`, `WPF_COMPATIBILITY_REPORT.md`
- `Validate-Release.ps1` usa `QubiCostElectrical.Wpf.sln`

## 3.140.2

### Entrega reproducible
- NavegaciÃ³n pÃºblica limpia: `OrdenesCambio` deja de registrarse como ruta activa sin borrar datos legacy.
- Tests de indicadores econÃ³micos aislados del cache real de usuario.
- Controles de importaciÃ³n de cotizaciones usan tokens de tema en vez de `Color.White` directo.
- Suite Release validada localmente: 749 pruebas correctas.

## 3.140.1

### Calidad técnica
- Catches silenciosos reemplazados por `AppLog.Warning/Error` en todos los módulos
- 10 métodos `async void` fuera de eventos corregidos a `async Task`
- `Application.DoEvents()` eliminado de `LoadingForm`
- `HttpClient` directo reemplazado por instancia `static readonly`
- `DateTime.Now` centralizado en `AppClock` (reemplazable en tests) en 63 archivos
- `MessageBox.Show` en servicios enrutado a través de `ShellPresentationAccessor.Confirm/Alert`
- Columnas de grilla inicializadas una vez (no por cada recarga) en 6 pantallas
- Paralelismo de tests activado (`xunit.runner.json`)

### Arquitectura
- `SQLiteSchemaMigrator` dividido en `SqliteMigrationSteps`, `SqliteMigrationLedger`, `SqliteMigratorHelpers`
- UI de Versiones de presupuesto: crear, activar, renombrar, eliminar snapshots
- Versionado centralizado en `Directory.Build.props` como única fuente de verdad
- `ProductVersionInfo` lee esquema, formato y motor desde `AssemblyMetadata`
- Esquema SQLite en versión 20 (migración `CubicacionComposicion`)
- Navegación reorganizada en 5 etapas del flujo de presupuesto

### Infraestructura
- CI lee versión desde `Directory.Build.props` y genera artefactos con SHA-256
- Instalador Inno Setup compilado en CI (`installer/PresupuestoElectricoPRO.iss`)
- Paquete fuente con validación de rutas y rechazo si repositorio sucio
- `.claude/`, `.cursor/` y `worktrees` excluidos del paquete fuente y `.gitignore`

### Base inicial (desarrollo previo al release 3.140.1)
- Versionado del producto, base de datos, formato de proyecto y motor de cálculo
- Clean Architecture: Domain / Application / Infrastructure / WinForms
- Exportación PDF oferta, cubicación, DOCX carta de presentación
- 742 pruebas unitarias cubriendo motor, servicios, SQLite y UI de navegación
- Cifrado DPAPI para datos sensibles de clientes
- Autoguardado, backups rotativos SQLite, recuperación ante fallo
- Banco de precios, trazabilidad de precios, congelado de precios
- APU por partida, conjuntos eléctricos, plantillas de insumos
- Revisión final con checklist, alertas y acciones directas
- Dashboard KPI, workflow de estados, historial de cambios
