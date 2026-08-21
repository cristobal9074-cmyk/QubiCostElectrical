# Changelog

Las versiones siguen el formato `MAJOR.MINOR.PATCH` y corresponden a `Directory.Build.props` + `VersionSuffix`.

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
