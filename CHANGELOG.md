# Changelog

Las versiones siguen el formato `MAJOR.MINOR.PATCH` y corresponden a `Directory.Build.props` + `VersionSuffix`.

## No publicado

## 1.1.7

### En desarrollo (2026-10-31)
- Apertura del ciclo de desarrollo v1.1.7.

## 1.1.6

### Estados de Pago, TipologÃ­as, Adicionales de Obra y Undo Transaccional (2026-10-31)
- **MÃ³dulo de Estados de Pago Profesional**:
  - Motor de cÃ¡lculo desacoplado (`EstadoPagoCalculator`) y calendario multi-aÃ±o continuo con periodos generables.
  - NavegaciÃ³n de tabla estilo Excel: navegaciÃ³n vertical continua manteniÃ©ndose en la misma columna (`Cant. Mes` y `% Mes`).
  - Deshacer/Rehacer (Ctrl+Z / Ctrl+Y) transaccional instantÃ¡neo en 1 sola pulsaciÃ³n sin recargas zombi ni re-commits por pÃ©rdida de foco.
  - Deducciones automÃ¡ticas: anticipo contractual, retenciÃ³n de garantÃ­a e IVA calculados en vivo.
  - Reportes certificados listos para firma: exportaciÃ³n a QuestPDF y Excel con desglose por partidas y acumulados.
- **TipologÃ­as de CanalizaciÃ³n y Conductores en CubicaciÃ³n**:
  - CatÃ¡logo y selector de tipologÃ­as elÃ©ctricas integradas directamente en el flujo de cubicaciÃ³n.
- **GestiÃ³n Integral de Adicionales de Obra (Change Orders)**:
  - RediseÃ±o ergonÃ³mico con trazabilidad de estados y exportadores certificados en PDF y Excel.
- **Control de Materiales, EPP y Solicitudes de Faena**:
  - AgrupaciÃ³n por categorÃ­as EPP, trazabilidad de solicitudes de obra y control de consumos.
- **Instalador Oficial**:
  - Asistente de instalaciÃ³n nativo WPF con blueprint CAD y sincronizaciÃ³n dinÃ¡mica de versiones.

## 1.1.5

### Panel Lateral de Cambio Masivo de LÃ­nea, Doble TuberÃ­a y Mejoras en CubicaciÃ³n SC (2026-10-27)
- **Panel lateral acoplado para Cambio Masivo de LÃ­nea / Familias**:
  - Panel derecho ultra-limpio y minimalista accesible con `Ctrl+Shift+H` o desde la barra superior de CubicaciÃ³n SC.
  - Enfoque directo sin datos monetarios: sustituciÃ³n de lÃ­neas completas de productos (`PVC âž” PVCLH`, `Magic âž” Matix`, `EVA âž” H07Z1-K`, o personalizado).
  - DetecciÃ³n en tiempo real de ocurrencias en el proyecto y actualizaciÃ³n instantÃ¡nea en canalizaciones, circuitos, artefactos, partidas y APUs, compatible con `Ctrl+Z` (deshacer).
- **Soporte de Doble TuberÃ­a en Medidas de Entrada de CubicaciÃ³n SC**:
  - IncorporaciÃ³n de columna para segunda tuberÃ­a (`L. TUB 2` / `Tipo Tub. 2`) en circuitos de servicios comunes.
  - CÃ¡lculo automÃ¡tico combinado de conductores: `(TuberÃ­a 1 + TuberÃ­a 2) Ã— NÂ° conductores`.
  - Reordenamiento visual ergonÃ³mico de columnas: `L.BAND` primero y luego `L.PVC`.
  - Encabezados de tabla optimizados en 2 lÃ­neas para lectura limpia sin desbordes.
- **Paneles Laterales Acoplados (Drawers) en CubicaciÃ³n SC**:
  - TransiciÃ³n completa de diÃ¡logos modales flotantes a paneles acoplados a la derecha para CatÃ¡logo y Artefactos, evitando bloqueos y mejorando el flujo de trabajo continuo.
- **AutoFill / Arrastre inteligente tipo Excel**:
  - Soporte de series y autorrelleno mediante arrastre del tirador inferior derecho en celdas de las grillas de cubicaciÃ³n e itemizado.
- **OptimizaciÃ³n y Estabilidad**:
  - PreservaciÃ³n estricta de presupuestos hermÃ©ticos, recalibraciÃ³n de mano de obra y correcciones en la sincronizaciÃ³n de cajas y accesorios.

## 1.1.4

### Modo Zona Libre y Matriz de CubicaciÃ³n de 400 Casillas Continuas (2026-10-25)
- **Capacidad ampliada a 400 columnas de cubicaciÃ³n**:
  - El motor de fÃ³rmulas (CubicacionTakeoffQueryService), composiciÃ³n (CubicacionCompositionService), persistencia de mediciones y panel de distribuciÃ³n admiten ahora hasta 400 casillas (C1 a C400).
- **Modo Zona Libre con auto-expansiÃ³n continua tipo Excel**:
  - DiseÃ±ado para usuarios que no requieren desglose por pisos o zonas preconfiguradas.
  - Carga liviana inicial en 25 casillas y auto-expansiÃ³n dinÃ¡mica en bloques de 15 columnas al avanzar con el teclado o pegar datos, manteniendo siempre estable la posiciÃ³n del cursor y el foco.
- **Herramienta de Modo de CubicaciÃ³n persistente**:
  - Selector [ ðŸŒ Zona Libre | ðŸ¢ Por Zonas ] independiente del flujo de trabajo diario.
  - Guarda la preferencia en el equipo (DefaultCubicacionMode) para abrir futuros presupuestos directamente en el modo preferido.
- **Selector de vista principal clÃ¡sico restaurado**:
  - Conmutador [ CubicaciÃ³n | Detalle ] restaurado a su funcionamiento original de 2 estados.
  - Atajos Alt+A (CubicaciÃ³n) y Alt+D (Detalle) operan de forma limpia y exclusiva sobre la vista.
- **Correcciones de usabilidad y visualizaciÃ³n**:
  - Resuelto el corte o compresiÃ³n horizontal de la columna C1 al volver desde otras vistas.
  - Eliminado el recuadro azul no deseado en la columna UD al ingresar fÃ³rmulas de sector con referencias de celdas.
## 1.1.3

### VerificaciÃ³n y ValidaciÃ³n de Auto-Actualizaciones Velopack (2026-10-24)
- **ValidaciÃ³n del canal oficial de auto-actualizaciones in-app**:
  - VerificaciÃ³n del ciclo completo de detecciÃ³n, descarga y aplicaciÃ³n de paquetes delta vÃ­a GitHub Releases.
  - SincronizaciÃ³n continua de versiones e integridad del repositorio de releases.

## 1.1.2

### CalibraciÃ³n definitiva Motor Costos/FA y Familias CanÃ³nicas de PresentaciÃ³n (2026-10-24)
- **CalibraciÃ³n del motor de Mano de Obra y Factor de Ajuste (FA)**:
  - CorrecciÃ³n de la fÃ³rmula en `ExcelCostosBridge` y `FactorAjusteMoService`: la Mano de Obra se calcula como `MoDirecta = Sum(Material x FactorM.O. x FA)`, igualando la base y el margen del Excel de referencia (`$159.746.897` vs `$159.772.500`, discrepancia de solo -0,02%).
  - Mantenimiento bidireccional y dinÃ¡mico de factores: el FA varÃ­a orgÃ¡nicamente por proyecto sin alterar factores fijos.
- **Familias CanÃ³nicas de PresentaciÃ³n ordenadas segÃºn Excel de referencia**:
  - ReestructuraciÃ³n de `PresentacionFamiliasInforme.FamiliasCanonicas` en las 9 familias exactas: `MATERIALES` (1.720), `EQUIPOS` (1.430), `TABLEROS` (1.440), `LUMINARIAS` (1.450), `PVC/EPC` (1.720), `VARIOS` (1.360), `ARTEFACTOS` (1.450), `MOVIMIENTOS` (1.450), `COMISIONAMIENTO` (1.450).
  - ClasificaciÃ³n de recursos ajustada: `Protecciones` clasificado como material (no equipo) con markup de Tableros. `Puesta a tierra` mapeado a `CONDUCTORES` (`MATERIALES`).
  - IncorporaciÃ³n del botÃ³n `+ Agregar familia` en la vista de Costo (`AgregarFamiliaPresentacionDialog`) para registrar nuevas familias de presentaciÃ³n dinÃ¡micas.
- **Infraestructura de Auto-ActualizaciÃ³n Velopack**:
  - CorrecciÃ³n de `ProductVersionInfo.GitHubReleasesUrl` apuntando al repositorio canÃ³nico `https://github.com/cristobal9074-cmyk/QubiCostElectrical`.
  - Empaquetado oficial con Velopack 1.2 (`vpk pack`) generando deltas incrementales y binario de instalaciÃ³n para distribuciÃ³n transparente.

### Hermeticidad de la suite de tests y recuperaciÃ³n de presupuestos de obra (2026-10-21)
- **Incidente corregido**: `FactorAjusteMoServiceTests.SyncAndVerifyPel039A2026Prueba2` era en
  realidad un script de calibraciÃ³n de un presupuesto real, marcado como `[Fact]`. En cada
  `dotnet test` reescribÃ­a `PEL039A2026 ABIERTO.qce` y `PEL039A2026 PRUEBA NUMERO 2.qce` con
  `SaveSnapshot` completo: borraba 6 gastos operacionales, forzaba `MoTotalClp`,
  `MoDirectaClp` e `IndiceMoFa`, y recalculaba el precio de M.O. de los 82 materiales.
  En CI el test pasaba en verde por accidente (la ruta no existe en el runner, asÃ­ que hacÃ­a
  early return), lo que ocultaba el problema justo donde debÃ­a detectarse.
- **Suite hermÃ©tica**: el test destructivo saliÃ³ de la suite y su lÃ³gica vive ahora en
  `tools/LiveProjectCalibration`, herramienta manual *dry-run* por defecto, con `--apply`
  explÃ­cito y copia `.precalibration` del destino.
- **Compuerta de acceso a obra real** (`tests/.../Fixtures/LiveProjectGate.cs`): cerrada por
  defecto, se abre con `QUBI_LIVE_PROJECTS=1` (+ `QUBI_LIVE_PROJECTS_ROOT` y
  `QUBI_LIVE_EXCEL_ROOT`). Resuelve rutas con `Environment.SpecialFolder`, sin literales de
  usuario. Los tests que necesitan un `.qce` de obra trabajan sobre copia en temporal.
- **Guard de build** (`tests/ArchitectureTests/NoLivePathGuardTest.cs`): falla la compilaciÃ³n
  si reaparece una ruta absoluta de mÃ¡quina del desarrollador dentro de `tests/`.
- **RecuperaciÃ³n de datos**: `PEL039A2026 ABIERTO.qce` recuperÃ³ sus 8 gastos operacionales
  (tabla copiada desde el respaldo de la app, conservando 1424 entradas de auditorÃ­a) y
  `PEL039A2026 PRUEBA NUMERO 2.qce` se restaurÃ³ Ã­ntegro desde el respaldo del 19-10 18:13
  (recupera el material `DT-001 PLANOS ASBUILR`, `MoTotalClp = 260.212.500`,
  `IndiceMoFa = 63,04` y 1956 entradas de auditorÃ­a). Consecuencia: la paridad de margen
  contra el Excel de referencia volviÃ³ al rango esperado (12%â€“14%; el Excel da 13,05%),
  tras registrar 9,47% / 9,91% con el estado alterado.
- Sin cambios en SQLite, migraciones, cÃ¡lculos ni en las fÃ³rmulas de cierre.

### RediseÃ±o del diÃ¡logo "Nuevo proyecto" (2026-10-20)
- **Interfaz nueva en dos columnas**: ventana de 520Ã—680 â†’ 980Ã—720 con hero, pasos visibles (1 Â· Identidad, 2 Â· Plantilla, 3 Â· Archivo) y panel lateral de resumen en vivo (nombre, cliente, ubicaciÃ³n, moneda, plantilla, norma, carpeta y archivo final).
- **CÃ³digo de proyecto autogenerado**: `PEL###A{aÃ±o}` secuencial calculado sobre los `.qce` de la carpeta destino (`NewProjectDialog.GenerateNextProjectCode`), editable, con botones Regenerar/Copiar y aviso Ã¡mbar cuando el archivo ya existe.
- **Nombre de archivo derivado del cÃ³digo**: el archivo se llama `<CÃ³digo>.qce`; se eliminÃ³ el campo "Nombre del archivo" manual para evitar cÃ³digos inconsistentes entre carpeta y proyecto.
- **Plantillas como tarjetas**: selector visual (5 plantillas SEC/RIC) con icono, nombre, subtÃ­tulo y nÃºmero de partidas base, en vez de combo de texto.
- **ValidaciÃ³n viva en el pie**: chips de estado (Nombre / CÃ³digo Ãºnico / Carpeta destino) y CTA "Crear y abrir" deshabilitado mientras falte lo esencial; atajo `Ctrl + Enter`.
- **Valor UF del dÃ­a**: campo editable que se precarga con `PriceConversion.DefaultValorUfClp` y se actualiza con `mindicador.cl` al abrir; el valor se persiste en `session.ValorUfClp` al crear el proyecto (conversiÃ³n UF â†” CLP correcta desde el primer dÃ­a).
- **Modelo comercial pasa a secciÃ³n avanzada colapsable** (6 polÃ­ticas + parÃ¡metro Ãºnico), con etiqueta de resumen en la cabecera del expander.
- Sin cambios en SQLite, migraciones, cÃ¡lculos ni en las fÃ³rmulas de cierre.

## 1.1.1

### Cierre Comercial GA y EstabilizaciÃ³n UI (2026-09-08)
- **Smoke Test Humano Semana A (PASS)**: ValidaciÃ³n y firma formal en `docs/SMOKE_TEST.md` de los 5 bloques crÃ­ticos (CubicaciÃ³n completa, CubicaciÃ³n SC, roles y permisos, persistencia con flush al salir y overlay en oferta/exportaciÃ³n). Artefacto SHA-256 verificado y archivado.
- **Endurecimiento del Rol Visor**: Blindaje completo de solo lectura en los 6 mÃ³dulos principales y cabecera de CubicaciÃ³n (`UnidadHeaderBox`, `CantHeaderBox`, `ObservacionHeaderBox` enlazados a `IsReadOnly`, setters con guard clauses ante `session.IsReadOnly`). Acciones de exportaciÃ³n estrictamente bloqueadas.
- **Persistencia y Resistencia a Fallos**: ImplementaciÃ³n de guardado atÃ³mico con flush inmediato en segundo plano ante cierre abrupto con la Â«XÂ» (<2,5 s) sin pÃ©rdida de datos en `.qce`.
- **TokenizaciÃ³n TipogrÃ¡fica 100%**: MigraciÃ³n completa de tamaÃ±os de fuente literales a tokens del sistema de diseÃ±o semÃ¡ntico (`DesignSystem/Typography.xaml`) en vistas principales, CubicaciÃ³n SC, diÃ¡logos secundarios (`QuickJumpPartidaDialog`, `ConjuntoPickerDialog`, `MaterialBatchReplaceDialog`, `SolicitudExcelPreviewDialog`, `SolicitudPdfPreviewDialog`, etc.) y controles de usuario.
- **Arquitectura y Sanidad de CÃ³digo**: DesvinculaciÃ³n de stubs experimentales (`ScenarioWorkspace`) de rutas de navegaciÃ³n visibles; suite completa de 1.821 pruebas automatizadas y guardias de arquitectura en verde (0 fallos, 0 advertencias).

### Motor de cÃ¡lculo v5 â€” la polÃ­tica de cierre comercial manda sobre el precio de venta (2026-09-01)
- **Problema**: `ClosingPolicySelection` (6 polÃ­ticas: Costo+Markup, Precio Objetivo, Suma
  Alzada/EPC, Costo+Fee, Precio Unitario Puro, Gainshare/Painshare) se persistÃ­a por proyecto
  pero solo alimentaba un panel informativo en la pantalla Costo. Export, dashboard, oferta y
  RevisiÃ³n Final leÃ­an siempre `TotalVentaUf` (= Costo+Markup).
- **Punto de integraciÃ³n Ãºnico**: `BudgetTotalsCalculator.Build`. Nuevo paso final comÃºn
  `FinalizeVentaYCierre`: se calcula la venta base neta del motor (post-factores) y luego
  `ClosingPolicyEngine.ApplyNeto` la transforma en el **neto de la polÃ­tica** (pre-descuento,
  pre-IVA); descuento e IVA se aplican despuÃ©s, uniformes para las 6 polÃ­ticas.
- **`ClosingPolicyEngine` â€” contrato NETO**: `Apply(CostBreakdown, â€¦)` â†’
  `ApplyNeto(ClosingPolicyInput, â€¦)`. `ClosingPolicyInput` (record) lleva `VentaBaseNetaClp`,
  `CostoEjecucionClp`, `PisoComercialClp`, `RiesgoImplicitoPct` y `Origenes`.
  `ClosingPolicyResult.VentaFinalClp` es ahora el neto pre-descuento y pre-IVA (antes traÃ­a todo).
  Se conserva `Apply(CostBreakdown, â€¦)` como adaptador de compatibilidad para UI/tests.
- **`CostBreakdown`**: nuevos `ClosingPolicyResult?` y `ClosingPolicy` (tipo aplicado).
- **`BudgetSettings.ClosingPolicy`**: la polÃ­tica viaja desde `ProjectFileData` vÃ­a
  `ExcelCostosBridge.EnrichFromProjectData` (punto Ãºnico); call-sites de RevisiÃ³n Final /
  export / dashboard que no pasaban por ahÃ­ ahora tambiÃ©n la enriquecen.
- **Bloqueante `UtilidadNegativa`**: se emite cuando el neto de la polÃ­tica no cubre el umbral
  que corresponde a esa polÃ­tica (ver "RevisiÃ³n â€” policy-aware" mÃ¡s abajo). RevisiÃ³n Final
  ademÃ¡s convierte las advertencias de fondo de la polÃ­tica ("margen negativo" / "no cubre el
  costo de ejecuciÃ³n") en inconsistencia bloqueante y el resto en advertencia.
- **`CostoViewModel`**: la secciÃ³n "PolÃ­tica de cierre" lee `cost.ClosingPolicyResult` del
  breakdown en vez de recalcular aparte.
- **RevisiÃ³n â€” el bloqueo `UtilidadNegativa` ahora es policy-aware** (`FinalizeVentaYCierre`):
  - `CostPlusMarkup` / `UnitPricePure`: bloquea si el neto no cubre el **piso comercial**
    (los factores / precios unitarios deben cubrir GG + utilidad â€” regla C-1 original).
  - `TargetPrice` / `LumpSumEpc` / `GainsharePainshare`: bloquea solo si hay **pÃ©rdida de caja
    real** (`neto < costo de ejecuciÃ³n`). Entre el costo y el piso â‡’ advertencia NO bloqueante
    ("â€¦ bajo el costo del proyecto con utilidad â€¦; margen reducido", generada en `ApplyNeto`),
    que RevisiÃ³n Final muestra como advertencia. Antes toda oferta bajo el piso quedaba
    bloqueada, incluido el caso que la polÃ­tica existe para evaluar.
  - `CostPlusFee`: bloquea solo si el fee es â‰¤ 0 (`neto â‰¤ costo de ejecuciÃ³n`). Antes un
    Costo+Fee con fee normal (8â€“12 %) SIEMPRE quedaba bloqueado porque el piso apila GG +
    utilidad que este modelo, por diseÃ±o, no carga.
  - Se elimina el bloque duplicado de `BuildExcelCostos` que comparaba `neto0` (contaminado por
    la polÃ­tica) contra el costo del proyecto; la comprobaciÃ³n C-1 de la ruta por factores usa
    la venta PRE-polÃ­tica (`bruto`) y solo aplica con `CostPlusMarkup`.
  - `DiferenciaFactoresUf` (la "diferencia por factores" de la pantalla Costo) se fija en 0 para
    `CostPlusFee` / `TargetPrice` / `LumpSumEpc` / `GainsharePainshare`: el concepto solo aplica
    a las polÃ­ticas que venden por factores.
- **`MargenRealPct`** se calcula contra el **neto** (post-descuento, pre-IVA) y ya no contra la
  venta con IVA. Antes, con `IncluirIvaEnTotal`, el IVA inflaba/deformaba el margen mostrado.
  Snapshot dorado: solo se moviÃ³ ese diagnÃ³stico (38 % â†’ 26 % en los 3 modos); ningÃºn monto
  cambiÃ³.
- **`BudgetEngine.AddCubicacionRow` â€” familia con factor 1.0**: el mapa de factores por familia
  (`BudgetSettings.PresentationFactorsByFamily`, poblado por `FromProject` desde `Variables`) es
  la fuente de verdad; el factor suelto de la fila (`GgUtFactor`) solo se usa como respaldo si
  la categorÃ­a no estÃ¡ mapeada. Antes una familia fijada a 1.0 caÃ­a al respaldo.
- `CalculationEngineVersion` 4 â†’ 5. **Snapshot dorado sin cambios**: con la polÃ­tica por
  defecto `CostPlusMarkup` el paso es pass-through (`neto0 == venta base`) y el pipeline es idÃ©ntico.
- **Selector al crear el proyecto**: `NewProjectDialog` incorpora la secciÃ³n "Modelo comercial"
  con 6 tarjetas (una por polÃ­tica, "Ãšsala cuandoâ€¦") y un Ãºnico campo de parÃ¡metro segÃºn la
  elegida (precio objetivo / contingencia mÃ­nima % / fee / costo objetivo). `NewProjectAsync`
  persiste la selecciÃ³n (`session.Data.ClosingPolicy` + `MarkDirty(ClosingPolicy)`) antes del
  primer guardado; viaja en el `.qce` y en los snapshots.
- **Salvaguardas**: `ClosingPolicySelection` sella `ParamsStampedAt` +
  `ParamsStampedCostoEjecucionClp` al fijar un monto absoluto (precio objetivo, fee fijo, costo
  objetivo, tope); la pantalla Costo avisa si el costo de ejecuciÃ³n se moviÃ³ > 5 % desde
  entonces. Aviso permanente para `CostPlusFee` ("ignora factores por familia y GG/utilidad") y
  aviso menor para `TargetPrice` / `LumpSumEpc`. El cambio de tipo de polÃ­tica queda en la
  auditorÃ­a del proyecto (`ProjectAuditRecorder`).
- `BudgetVersionComparisonService` compara por lÃ­nea de itemizado, no campo a campo: no se
  agregÃ³ fila "PolÃ­tica de cierre" al diff (la polÃ­tica ya viaja en el snapshot).
- **Consumidores que faltaba enriquecer (hallazgos de revisiÃ³n)**:
  - `BudgetKpiService.GetMetricValue` (KPIs venta/margen de versiones guardadas) enriquecÃ­a el
    `BudgetSettings` solo con `FromProject`, asÃ­ que la venta de toda versiÃ³n se calculaba como
    Costo+Markup y comparar dos versiones con polÃ­ticas distintas daba nÃºmeros errÃ³neos. Ahora
    proyecta el `BudgetVersionData` (que ya sella `ClosingPolicy`, gastos operacionales, plan de
    dotaciÃ³n, descuentos y MO) a `ProjectFileData` y pasa por
    `ExcelCostosBridge.EnrichFromProjectData`. El snapshot ya persistÃ­a la polÃ­tica vÃ­a JSON de
    `BudgetVersionRow`; sin cambios de esquema SQLite.
  - `CubicacionViewModelService.Build` / `BuildSummary` aceptan un `BudgetSettings?` opcional ya
    enriquecido; si es `null` mantienen el `FromValues` mÃ­nimo (venta Costo+Markup), ahora
    documentado en la interfaz como preliminar. El panel de totales vivo de la pantalla
    CubicaciÃ³n (`CubicacionPresenter.CalculatePartidaTotals`) no expone venta, asÃ­ que no habÃ­a
    nÃºmero engaÃ±oso visible; el cambio deja alineable el resto de constructores de VM.
- **Fuga de conexiones â€” `ProjectDbContext.Rewire()`**: antes de reasignar `_contracts` en cada
  cambio de ruta de proyecto ahora se hace `(_contracts as IDisposable)?.Dispose()` (y tambiÃ©n
  en `Dispose()`). Los `SqliteContract*Repository` son sin estado (una conexiÃ³n por operaciÃ³n,
  cerrada con `using`) y `ContractService` hoy no es `IDisposable`: el descarte es defensivo y
  queda comentado.

### Motor de cÃ¡lculo v4 â€” C-1 cerrado: rutas de markup unificadas (2026-08-30)
- **C-1 (raÃ­z)**: un presupuesto por ruta de factores por familia podÃ­a calcular una pÃ©rdida
  en silencio. En `HasRowLevelMarkups` el motor hacÃ­a `bruto = Î£ origin.VentaClp`, pero solo
  las filas de cubicaciÃ³n MAT llevaban factor: `AddApuRow`, `AddApprovedChangeOrders` y
  `AddGlobalPercentages` aportaban a la venta el costo puro (o 0), y `CostoProyectoUf` escondÃ­a
  el signo con `Math.Max(0, utilidad)`.
- **UnificaciÃ³n**: `venta = Î£ (costo_origen Ã— factor_presentaciÃ³n_familia)` para TODA lÃ­nea.
  Los orÃ­genes APU (por Ã¡rea de la partida), OC aprobadas y cargos globales de
  herramientas/mermas ahora llevan el factor de presentaciÃ³n; lo no clasificado usa el
  **factor por defecto del proyecto** (familia general, â‰ˆ factor de MO, leÃ­do de
  `ProjectInfo.Variables` al cierre). El flag `FactoresPresentacionPorFamilia` pasa a
  significar "el proyecto usa factores por familia" (todas las lÃ­neas, no solo las de factor â‰  1).
- **`BudgetSettings`**: nuevos `PresentationFactorsByFamily` / `DefaultPresentationFactor` /
  `ResolvePresentationFactor(family)`, poblados en `FromProject` desde
  `Variables["MarkupFactor_*"] Ã— (1 âˆ’ MarkupDiscount_*/100)`. Factores 100% por proyecto,
  nunca fijos. Regla de familia (normalizaciÃ³n + tabla de defaults) unificada en
  `FamilyPresentationDefaults` (Domain); Application delega.
- **Inconsistencia BLOQUEANTE `UtilidadNegativa`**: si la venta por factores cae bajo el piso
  GG+utilidad+VA del costo del proyecto, se emite en el cÃ¡lculo y RevisiÃ³n Final impide
  exportar ("Generar oferta").
- `CalculationEngineVersion` 3 â†’ 4. Sin cambios para proyectos sin factores por familia
  (factor 1, ruta aditiva idÃ©ntica).

### ComposiciÃ³n: contenedor DI real (2026-08-28)
- MigraciÃ³n de la composiciÃ³n manual (`AppServicesBuilder` 374 lÃ­neas + `AppServices` con dos
  ctores y setters mutables + `WpfAppHostBuilder` + `WpfAppHost.Current` estÃ¡tico) a
  **Microsoft.Extensions.DependencyInjection** 10.0.11.
- 4 mÃ³dulos de registro por capa: `AddDomainServices` / `AddApplicationServices` /
  `AddInfrastructureServices` / `AddWpfServices`. Registro explÃ­cito por interfaz, lifetime
  singleton, grafo validado (`ValidateScopes`).
- **RaÃ­z Ãºnica**: `CompositionRoot.Build()` en el arranque. **`AppServicesBuilder` y
  `WpfAppHostBuilder` eliminados** como cÃ³digo de producciÃ³n (`AppServicesBuilder` queda como
  helper de tests).
- `IProjectDbContext` reemplaza la closure `RewireContractsIfProjectChanged`: una sola
  suscripciÃ³n a `Session.Changed` reconstruye los repos de contratos contra la BD del `.qce`.
- `StartupSeeder.SeedPriceBank` saca el I/O de disco de la construcciÃ³n del grafo.
- `Func<BudgetEngine>` inyectado en `BudgetCalculationService`, `FinalReviewService`,
  `ProjectUseCases` (adiÃ³s a los `new BudgetEngine()` sueltos).
- `ShellPageFactory` resuelve los 27 ViewModels de pÃ¡gina del contenedor
  (`ActivatorUtilities.CreateInstance` con el `IShellNavigationHost` vivo como arg runtime).
- Fitness function `ServiceProviderValidationTests` (build+validate del grafo y resoluciÃ³n de
  los 28 ViewModels de pÃ¡gina vÃ­a `ActivatorUtilities`).
- **Los 28 ViewModels migrados a inyecciÃ³n por constructor**: cada ctor pasa de
  `(IShellNavigationHost, WpfAppHost)` a dependencias concretas por interfaz; se eliminan los
  64 `host.Services.X` (service locator). Los sub-servicios de CubicaciÃ³n
  (`CubicacionPersistenceService`, `CubicacionSectorService`, `CubicacionRowMutationService`,
  `CubicacionClipboardCoordinator`), Export (`DeliveryPreviewService`,
  `DeliveryDocumentSelectorVm`), Shell (`AutoSaveService`, `ShellNavigationCoordinator`,
  `ShellProjectLifecycle`) y `MaterialBatchReplaceDialog` tambiÃ©n reciben deps concretas.
  Cada VM conserva un ctor `(â€¦, WpfAppHost)` de compatibilidad que delega en el primario.
- `IAuthService` registrado en el contenedor (`â†’ AuthService`).
- Pendiente (volumen, no riesgo; el grafo ya estÃ¡ 100% en MEDI): borrar las fachadas
  `AppServices`/`WpfAppHost` y los ctores de compatibilidad; estÃ¡ticos
  (`AppClock`â†’`TimeProvider`, `ProductPaths`â†’`IProductPaths`, `UserPreferencesStore`,
  `SessionManager`).

### Reproducibilidad y correctitud del motor de presupuesto (2026-08-27)
- Red de reproducibilidad: `BudgetEngineReproducibilityTests` (determinismo, independencia de
  orden, incremental == completo, suma de oferta, snapshot dorado al peso) â€” 3 modos Ã— con/sin
  factores por familia.
- **C4**: la venta por lÃ­nea se calcula directo en CLP (`precio Ã— factor Ã— cantidad`); se
  elimina el viaje CLPâ†’UFâ†’redondeo(3 dec)â†’CLP y el valor UF de respaldo `39000` hardcodeado.
  `AddApuRow` ya no pasa `apu.PrecioUnitUf` (CLP) por `UfToClp` â€” eso multiplicaba CLP Ã— valor
  UF y disparaba la venta ~2400Ã— cuando habÃ­a factores por familia + cualquier APU.
- **C3**: `MarkupPolicy.FactoresPresentacionPorFamilia` (opt-in, `ProjectInfoRow
  .UsarFactoresPresentacionFamilia`). Un `GgUtFactor â‰  1` suelto ya no cambia el mÃ©todo de
  markup de todo el presupuesto; solo lo hace si el proyecto lo activÃ³ (al configurar markups
  por familia).
- **C1**: en modo complementario el APU solo aporta equipos, subcontratos y recursos HERR/TR
  explÃ­citos + imprevistos de alza sobre esa base. Antes cargaba herramientas/transporte/merma
  calculadas sobre material y MO del APU que la cubicaciÃ³n ya cubre.
- **C2**: la merma de nivel APU (`ApuRow.MermaPct`) no se compone con la merma propia del
  recurso â€” solo aplica a los materiales sin desperdicio declarado. Inconsistencia
  `MermaDobleNivel` cuando coexisten.
- **C5**: la oferta al cliente se prorratea por la venta real por partida (`origin.VentaClp`,
  que ya incluye el factor de presentaciÃ³n), no por el costo directo; sin piso artificial; la
  Ãºltima partida absorbe el redondeo para que la suma cuadre exacto con la venta neta.
- **A1**: helper Ãºnico `Money.Round` (peso entero) para totales de presentaciÃ³n; el cÃ¡lculo
  intermedio queda a precisiÃ³n plena `decimal` sin redondeos intermedios.
- RevisiÃ³n: la venta de lÃ­nea usa `PrecioUnitClpConFallback` (misma fuente que el costo) para
  no quedar en 0 en proyectos legacy sin `MatClp`; migraciÃ³n en `ProjectSnapshotIntegrity` que
  enciende `UsarFactoresPresentacionFamilia` si el proyecto ya trae `GgUtFactor â‰  1` (preserva
  el total al reabrir); `FinalReviewService` respeta overrides comerciales y `MermaDobleNivel`
  solo se reporta en modo APU completo; bordes de la oferta (venta â‰¤ 0, ajuste de redondeo
  negativo, itemizado solo con tÃ­tulos).
- **Abierto (C-1)**: en la ruta por factores por familia, los orÃ­genes APU/OC aportan a la venta
  sin markup de presentaciÃ³n â†’ un proyecto APU-dominante puede quedar con utilidad negativa.
  Test `FamilyMarkupPath_ApuHeavy_KeepsPositiveMargin` (skip) documenta el caso. Pendiente
  decidir el modelo de composiciÃ³n row-level vs markup global en `BudgetTotalsCalculator`.

### Endurecimiento arquitectÃ³nico (Ola 1â€“4)
- Plan: `docs/ARCHITECTURE_HARDENING_PLAN.md`
- `ProjectDirtyScope` tipado + hot-path `MarkDirty` migrado (scopes GO/descuentos/staffing)
- `ProjectDirtyTracker` / `ProjectAccessGate` internos en sesiÃ³n
- Lock SMB: `OwnsActiveLock` + RO al `LockLost` + cancel debounce; `SaveAsync` exige lease
- Ola 4: lease mid-write (`TryBeginWriteLease`), `RequiresExclusiveLease` / `ProjectLockLostException`, sidecar en LockLostPending, exit sin `.qce`
- Costo testeable sin WPF: `CostoBreakdownLineBuilder`, `CostoMoLaborProjectionBuilder`, `CostoGoComposeProjectionBuilder`
- Cub: `PartidaCantidadVariablesBuilder` (R1..Rn) + `CubicacionSectorExpressionCompactor` + `CubicacionSectorNavigation`
- Export: `OfferPackagePresetCatalog` + `ExportEligibilityEvaluator`; GO: `GastoOperacionalCategoryMaintenance`
- Gate de lÃ­neas por tipo parcial (no escape `*.Methods.cs`)
- Shell: `ShellPageFactory` + `ShellPageCache` + `ShellNavigationCoordinator` + `ShellProjectLifecycle`
- `WpfAppHostBuilder`; coalesce `Session.Changed` (75 ms, batch por Kind) + `ISessionChangeAware`
- Dual-path eliminado: LIVE â†’ `Features/`; stubs DEAD purgados

### Contrato UI/UX sistema (2026-08-25)
- Plan: `docs/UI_SCREEN_CONTRACT.md`
- `WorkspacePageViewModelBase`, `WorkspacePageChrome`, `ModuleFeedback`
- Tokens densos: `Font.Size.kpi/price/hero/micro`, `Text.SectionTitle`, `Brush.HeroPanel`
- Excel-nav declarativo: ~30 grillas (`Enable`) â€” editables + previews RO (pickers, paste, catÃ¡logo, viÃ¡ticos resumen, SolicitudExcelâ€¦)
- Costo Fase A+B: tokens `Font.Size.*` + estilos `Text.SectionTitle` / `Helper` / `KpiInline`
- Tokens densos APU / EPP / MO: 0 `FontSize` literales
- Contrato empty+skeleton: ApuCatalog, ApuMerge, Contracts (3), ScenarioWorkspace (WIP sin menÃº)
- Chrome compacto 720p: stepper + banner RO; guardar sin toast duplicado
- Gate: `WorkspacePageContractTests` (15 vistas core con empty + skeleton)

### Confianza al salir / export (2026-08-25)
- Flush silencioso al cerrar con X / Alt+F4 / Shutdown (`PrepareForAppExitAsync` + `AutoSaveService.FlushPersistAsync`)
- Primary Â«Generar oferta â†’Â» usa `DeliverPackageCommand` + `IsBusy` / `InverseBool`
- Cubicar multi-selecciÃ³n con cola FIFO + toast Â«SiguienteÂ»
- Tomar SC confirma sobrescritura; Visor RO en Documents/Plantillas; skeletons Banco/Proyecto/Docs/Cotizaciones
- SC modo entrada/avanzado oculta columnas tÃ©cnicas; atajos shell/itemizado en Preferencias
- Excel-nav en Banco y Plantillas; stepper visible con proyecto abierto

### Fluidez de guardado (profesional)
- Cambiar de pestaÃ±a ya no pide Guardar/Descartar: aplica el draft y autoguarda el `.qce`
- Autoguardado al disco con debounce 2,5 s + sidecar de recuperaciÃ³n cada 15 s
- Cerrar proyecto guarda en silencio; Ctrl+S sigue disponible

### UI premium
- Dashboard renovado con tarjetas premium, KPIs y flujo de producciÃ³n integrado
- Tokens de diseÃ±o unificados (`Badge.*`, `Dashboard.PremiumCard`, tipografÃ­a y paleta corporativa)
- Empty states consistentes en mÃ³dulos core (itemizado, banco de precios, plantillas)
- Preferencias con barra lateral de navegaciÃ³n por secciones
- Toasts no modales en el shell (`ToastHost` + `ShellNotificationService`)
- Skeleton de carga reutilizable para pantallas con datos asÃ­ncronos
- Toasts con acciÃ³n para deshacer y abrir carpeta
- Tokens MO/EPP/SC sin hex sueltos en la UI objetivo
- Empty state de Versiones alineado al design system
- DocumentaciÃ³n comercial y de humo actualizada (`docs/SMOKE_TEST.md`, `docs/DEMO_SCRIPT_15MIN.md`, `docs/FIRST_RUN_TEAM_ONBOARDING.md`)

### UX polish P0â€“P2 (2026-08-25)
- Copy sober: Dashboard, Export, EPP, Mano de obra y Documentos (sin tono SaaS / anglicismos)
- Anti-slop en Costo/EPP: chips con `Radius.Chip`, CTA viÃ¡ticos con `Button.Primary` (sin gradiente ni DropShadow)
- MÃ¡rgenes de pÃ¡gina Preferencias/Itemizado con token `Padding.Page`
- Fade de cambio de ruta en shell alineado a `Anim.FadeIn`

### DistribuciÃ³n local unificada
- Launcher canÃ³nico: `publish\win-x64\QubiCostElectrical.exe` (mismo exe para acceso directo del escritorio y doble clic en `.qce`)
- `Update-DesktopShortcut.ps1` apunta por defecto al publish; `-RegisterAssociation` alinea el ProgId HKCU
- Evita abrir builds distintas (bin de desarrollo vs publish portable)

## 1.1.0

### Contratos â€” ciclo de vida completo
- Nuevo mÃ³dulo Contratos: creaciÃ³n, envÃ­o a revisiÃ³n, aprobaciÃ³n/rechazo, suspensiÃ³n/reactivaciÃ³n, cierre, tÃ©rmino y rescisiÃ³n
- SegregaciÃ³n de funciones (SoD): quien crea/edita un contrato no puede aprobarlo ni cerrarlo
- Esquema SQLite por proyecto (`Migration032`): `contracts`, `contract_parties`, `contract_milestones`, `contract_scope_links`, `contract_documents`, `contract_versions`, `contract_audit`
- Versionado de contratos con hash de integridad (`ContractVersion.ComputeHash`) y verificaciÃ³n de manipulaciÃ³n
- UI: espacio de trabajo de contratos, editor y comparador de versiones, integrados en la navegaciÃ³n (grupo Â«CierreÂ»)
- 7 tests de integraciÃ³n contra SQLite real cubriendo ciclo de vida, SoD, integridad y concurrencia optimista

## 1.0.0

### General Availability â€” presupuesto y oferta elÃ©ctrica
- Gate de exportaciÃ³n sin precios congelados (congelar / continuar con riesgo / cancelar) + auditorÃ­a
- **Fase 2 (semanas 5â€“8):** stepper Â«LÃ­nea de producciÃ³nÂ» en shell (Datos â†’ Exportar + C.SC)
- Design system en flujo core: `Form.ComboBox`, `Form.TabControl`, `Form.ProgressBar`, `DataGrid.Workspace`
- ComparaciÃ³n versiones **A vs B** en mÃ³dulo Versiones (UI + tests VM + puerta `WpfPhase2GateTests`)
- Golden-file Excel interno (`ExcelExportGoldenFileTests`) â€” 11 hojas, totales alineados al motor
- Banco corporativo: `ProductPaths` UNC + tests carpeta compartida (`CorporatePriceBankTests`)
- CubicaciÃ³n SC: match de materiales por cÃ³digo ademÃ¡s de descripciÃ³n
- UI: `AppMessageDialog`, sin tÃ­tulos duplicados, Delivery legacy retirado
- PDF oferta: correcciÃ³n de encoding UTF-8 en `QuestPdfProjectExporter`
- Tests: Itemizado/Cubicacion VM, PriceBankStore, Export freeze, Shell workflow, Versiones A/B
- BC3 explÃ­citamente fuera de alcance (`docs/BC3_SCOPE.md`)
- Informe cierre Fase 2: `docs/PHASE2_WEEKS_5_8_REPORT.md`

## 1.0.0-rc.4

### Arquitectura y producto
- Identidad unificada: `ProductIdentity`, `ProductPaths`, datos en `%LOCALAPPDATA%\QubiCostElectrical`
- `docs/PRODUCT.md` como fuente operativa; `CLAUDE.md` sincronizado
- Presenters: `CubicacionPresenter`, `ExportPresenter`; `ExportScopeService` en Application
- CubicaciÃ³n: mediciones conectadas a `CubicacionCompositionService` en ediciÃ³n
- UI: `ItemizadoImportPreviewDialog` XAML + design system; eliminados placeholders y mapping dialog huÃ©rfano
- Design system: `Dialogs.xaml` para ventanas modales

## 1.0.0-rc.3

### Confianza y distribuciÃ³n
- Pipeline Authenticode: `deploy/Sign-Authenticode.ps1`, CI con secretos, `-Sign` en builds
- Identidad Qubi Cost congelada: `Verify-ProductIdentity.ps1`, splash y PDF sin marca legacy
- QuestPDF: licencia declarada en `QuestPdfBootstrap` + `THIRD_PARTY_NOTICES.txt`
- Docs operativos actualizados: `AGENTS.md`, `RELEASE_PROCESS.md`, `SMOKE_TEST.md`, `WPF_COMPATIBILITY_REPORT.md`
- `Validate-Release.ps1` usa `QubiCostElectrical.Wpf.sln`

## 3.140.2

### Entrega reproducible
- NavegaciÃƒÂ³n pÃƒÂºblica limpia: `OrdenesCambio` deja de registrarse como ruta activa sin borrar datos legacy.
- Tests de indicadores econÃƒÂ³micos aislados del cache real de usuario.
- Controles de importaciÃƒÂ³n de cotizaciones usan tokens de tema en vez de `Color.White` directo.
- Suite Release validada localmente: 749 pruebas correctas.

## 3.140.1

### Calidad tÃ©cnica
- Catches silenciosos reemplazados por `AppLog.Warning/Error` en todos los mÃ³dulos
- 10 mÃ©todos `async void` fuera de eventos corregidos a `async Task`
- `Application.DoEvents()` eliminado de `LoadingForm`
- `HttpClient` directo reemplazado por instancia `static readonly`
- `DateTime.Now` centralizado en `AppClock` (reemplazable en tests) en 63 archivos
- `MessageBox.Show` en servicios enrutado a travÃ©s de `ShellPresentationAccessor.Confirm/Alert`
- Columnas de grilla inicializadas una vez (no por cada recarga) en 6 pantallas
- Paralelismo de tests activado (`xunit.runner.json`)

### Arquitectura
- `SQLiteSchemaMigrator` dividido en `SqliteMigrationSteps`, `SqliteMigrationLedger`, `SqliteMigratorHelpers`
- UI de Versiones de presupuesto: crear, activar, renombrar, eliminar snapshots
- Versionado centralizado en `Directory.Build.props` como Ãºnica fuente de verdad
- `ProductVersionInfo` lee esquema, formato y motor desde `AssemblyMetadata`
- Esquema SQLite en versiÃ³n 20 (migraciÃ³n `CubicacionComposicion`)
- NavegaciÃ³n reorganizada en 5 etapas del flujo de presupuesto

### Infraestructura
- CI lee versiÃ³n desde `Directory.Build.props` y genera artefactos con SHA-256
- Instalador Inno Setup compilado en CI (`installer/PresupuestoElectricoPRO.iss`)
- Paquete fuente con validaciÃ³n de rutas y rechazo si repositorio sucio
- `.claude/`, `.cursor/` y `worktrees` excluidos del paquete fuente y `.gitignore`

### Base inicial (desarrollo previo al release 3.140.1)
- Versionado del producto, base de datos, formato de proyecto y motor de cÃ¡lculo
- Clean Architecture: Domain / Application / Infrastructure / WinForms
- ExportaciÃ³n PDF oferta, cubicaciÃ³n, DOCX carta de presentaciÃ³n
- 742 pruebas unitarias cubriendo motor, servicios, SQLite y UI de navegaciÃ³n
- Cifrado DPAPI para datos sensibles de clientes
- Autoguardado, backups rotativos SQLite, recuperaciÃ³n ante fallo
- Banco de precios, trazabilidad de precios, congelado de precios
- APU por partida, conjuntos elÃ©ctricos, plantillas de insumos
- RevisiÃ³n final con checklist, alertas y acciones directas
- Dashboard KPI, workflow de estados, historial de cambios
