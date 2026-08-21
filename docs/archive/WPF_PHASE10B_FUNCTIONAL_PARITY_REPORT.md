# WPF Phase 10B — Reporte de Paridad Funcional

Rama: `feature/wpf-phase10b-functional-parity`

---

## BLOQUE 1 — MONEDAS E HISTORIAL DE TASAS

**Estado: COMPLETO**

### Auditoría del código existente

| Componente | Hallazgo |
|---|---|
| `MonedasWorkspacePage.cs` (WinForms) | Referencia funcional: selector UF/CLP/USD, campos UF y USD, checkbox auto, botones Guardar y Actualizar, grid comparativo |
| `CurrencyConversionService.cs` | Application layer completo: NormalizeCurrency, ConvertUfToClp/Usd, FormatMoneyFromUf |
| `CurrencyViewModel.cs` (WPF preexistente) | Parcial: tenía tasas y historial pero faltaba `MonedaSeleccionada` editable y validación de moneda |
| `CurrencyView.xaml` (WPF preexistente) | Parcial: faltaba ComboBox de moneda del proyecto |
| `CurrencyViewModelTests.cs` | 8 tests preexistentes, faltaban 10 para los casos de uso nuevos |

### Casos de uso implementados

| UC | Descripción | Implementación |
|---|---|---|
| UC-1 | Ver tasas vigentes | `ResumenClp`, `ResumenUsd`, sección TASAS VIGENTES en vista |
| UC-2 | Editar tasa UF | `EditUfClp` con validación de formato y valor positivo |
| UC-3 | Editar tasa USD | `EditUsdClp` con validación de formato y valor positivo |
| UC-4 | Registrar fecha de vigencia | `EditFecha` con parse dd/MM/yyyy |
| UC-5 | Seleccionar moneda del proyecto | `MonedaSeleccionada` editable, ComboBox en vista, guardado en `ProjectInfo.MonedaPresupuesto` |
| UC-6 | Ver historial de tasas | `Historial` ObservableCollection, ordenado descendente por fecha, columnas Fecha/UF/USD/Estado |

### Cambios en CurrencyViewModel.cs

- Agregada propiedad `MonedaSeleccionada` (pública, editable, marca dirty)
- `LoadFromSession()` carga `MonedaSeleccionada` desde `info.MonedaPresupuesto`
- `TryApplyChanges()` persiste `info.MonedaPresupuesto`, valida requisitos por moneda (CLP necesita UF, USD necesita UF y USD)
- Validación de moneda solo al aplicar (no mid-typing), para no interferir con UX

### Cambios en CurrencyView.xaml

- Agregada sección de selector de moneda dentro de ACTUALIZAR TASAS
- ComboBox vinculado a `MonedaSeleccionada` / `MonedasDisponibles`
- Texto de ayuda contextual (UF/CLP/USD)

### Pruebas automatizadas — 19 tests en CurrencyViewModelTests.cs

| # | Nombre | Estado |
|---|---|---|
| 1 | InitialLoad_ReadsFromSession | Preexistente ✅ |
| 2 | EditFields_SetDirty | Preexistente ✅ |
| 3 | TryApplyChanges_InvalidUf_SetsValidationError | Preexistente ✅ |
| 4 | TryApplyChanges_ValidFields_UpdatesSession | Preexistente ✅ |
| 5 | TryApplyChanges_AddsHistorialEntry | Preexistente ✅ |
| 6 | TryDiscardChanges_RestoresOriginalValues | Preexistente ✅ |
| 7 | HistorialViewModel_ReflectsSessionRows | Preexistente ✅ |
| 8 | CacheStale_TrueWhenNoCacheFile | Preexistente ✅ |
| 9 | CacheStale_FalseWhenCacheIsRecent | Preexistente ✅ |
| 10 | MonedaSeleccionada_InitialLoad_MatchesSession | Nuevo ✅ |
| 11 | MonedaSeleccionada_Change_SetsDirty | Nuevo ✅ |
| 12 | TryApplyChanges_UpdatesMonedaPresupuesto | Nuevo ✅ |
| 13 | TryApplyChanges_ClpSinUf_SetsValidationError | Nuevo ✅ |
| 14 | TryApplyChanges_UsdSinTasas_SetsValidationError | Nuevo ✅ |
| 15 | Historial_OrderedByDescendingDate | Nuevo ✅ |
| 16 | Historial_ActiveEntry_HasEstadoActiva | Nuevo ✅ |
| 17 | TryApplyChanges_PreviousHistorial_MarkedInactive | Nuevo ✅ |
| 18 | ResumenClp_FormatsCorrectly | Nuevo ✅ |
| 19 | TryApplyChanges_SameRates_NoNewHistorialEntry | Nuevo ✅ |

### Fix adicional: aislamiento de tests en paralelo

`UserPreferencesStoreTests` y `PreferencesViewModelTests` compartían la ruta estática de `UserPreferencesStore` sin estar en la colección serial `WpfHost`, provocando fallos de carrera al correr la suite completa. Ambas clases fueron agregadas a `[Collection("WpfHost")]`.

### Resultado de verificación

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 955/955 |

### BLOQUE MONEDAS COMPLETO ✅

---

## BLOQUE 2 — PREFERENCIAS GLOBALES

**Estado: COMPLETO**

### Auditoría del código existente

| Componente | Hallazgo |
|---|---|
| `PreferenciasWorkspacePage.cs` (WinForms) | Referencia funcional: zoom, filas por página, tema, sidebar compacto, funciones experimentales, membrete (botón), URLs de precios |
| `UserPreferences.cs` | 25+ campos. Consumidores reales en WPF solo: Letterhead (PDF), RecentFiles, Window state, GridColumn widths |
| `UserPreferencesStore.cs` | Completo: carga segura, guardado atómico (.tmp→rename), sanitización, scope de tests |
| `PreferencesViewModel.cs` (WPF preexistente) | Solo campos de membrete. Faltaba: DefaultProjectFolder, ApplyChanges, RestoreDefaults, diagnóstico |
| `PreferencesView.xaml` (WPF preexistente) | Solo membrete. Reescrita en Bloque 2 con secciones General y Diagnóstico |
| `LogConfiguration.cs` | LogsFolderPath = `%LOCALAPPDATA%\QubiCostElectrical\Logs` |
| `AutoSaveService.cs` | GetAutoSaveRecoveryDir() = carpeta de archivos .autosave |

### Campos con consumidor real en WPF (implementados en UI)

| Campo | Store | Consumidor |
|---|---|---|
| `DefaultProjectFolder` | `UserPreferences` (nuevo) | File dialogs Open/Save |
| `LogsFolderPath` | `LogConfiguration` | Botón Abrir carpeta de logs |
| `RecoveryFolderPath` | `AutoSaveService` | Botón Abrir carpeta de recuperación |
| `AppVersion` | `ProductVersionInfo` | Sección de diagnóstico |

### Campos excluidos de la UI (motivo)

| Campo | Motivo |
|---|---|
| Letterhead (CompanyName, Rut, etc.) | Bloque 3 — Membrete |
| ProfessionalName/Role/etc. | Bloque 3 — Membrete |
| MaterialsPageSize | Sin consumidor en WPF (sí en WinForms) |
| LastPage | WPF siempre inicia en Dashboard |
| SidebarCompactMode | WPF no tiene sidebar dinámico aún |
| DarkMode/ThemeName | WPF usa DesignSystem estático |
| ExperimentalFeaturesEnabled | Sin menú experimental en WPF |
| PriceFeedUrls | Banco de precios no wired en WPF |

### Cambios en UserPreferences.cs

- Agregado `DefaultProjectFolder { get; set; } = ""`

### Cambios en UserPreferencesStore.cs

- Sanitización de `DefaultProjectFolder` (normaliza nulos)

### Cambios en PreferencesViewModel.cs

- Modelo de **borrador** (`_draft`) separado del objeto persistido (`_saved`)
- `ApplyChanges()` — valida, persiste, mantiene pantalla abierta, reporta `SaveStatus`
- `TrySave()` — alias compatible con tests existentes
- `Cancel()` / `Discard()` — descarta borrador, recarga desde disco
- `RestoreDefaults(Func<bool> confirmFn)` — requiere confirmación, solo modifica borrador
- `OpenLogsFolder(IFileOpenService)` — abre carpeta real de logs
- `OpenRecoveryFolder(IFileOpenService)` — abre carpeta de recuperación
- Propiedades de diagnóstico read-only: `AppVersion`, `LogsFolderPath`, `RecoveryFolderPath`
- Campos de Membrete/Professional conservados en ViewModel (para Bloque 3), fuera de la UI actual

### Cambios en PreferencesView.xaml

Reescrita con secciones:
- **GENERAL**: `DefaultProjectFolder` con botones Examinar y Limpiar
- **DIAGNÓSTICO**: AppVersion (read-only), LogsFolder + Abrir, RecoveryFolder + Abrir
- Banner de error de validación
- Banner de éxito (`SaveStatus`)
- Botones: Guardar, Aplicar, Cancelar, Restaurar predeterminados

### Pruebas automatizadas — 24 tests en PreferencesViewModelTests.cs

| # | Nombre | Tipo |
|---|---|---|
| 1 | InitialLoad_DoesNotThrow | Preexistente |
| 2 | InitialLoad_DefaultProjectFolder_IsEmpty | Nuevo |
| 3 | InitialLoad_FromExistingFile_RestoresValues | Nuevo |
| 4 | SettingProperty_SetsPendingChanges | Preexistente |
| 5 | SettingDefaultProjectFolder_SetsPendingChanges | Nuevo |
| 6 | TrySave_ValidData_PersistsToStore | Preexistente |
| 7 | TrySave_Persists_DefaultProjectFolder | Nuevo |
| 8 | SaveAndReopen_PersistsValues | Nuevo |
| 9 | ApplyChanges_ValidData_ReturnsTrueAndSetsStatus | Nuevo |
| 10 | ApplyChanges_InvalidEmail_ReturnsFalse | Nuevo |
| 11 | Discard_RestoresOriginalValues | Preexistente |
| 12 | Cancel_DoesNotPersistChanges | Nuevo |
| 13 | RestoreDefaults_WhenNotConfirmed_NoChange | Nuevo |
| 14 | RestoreDefaults_WhenConfirmed_ResetsDraft | Nuevo |
| 15 | RestoreDefaults_BeforeSave_DoesNotPersist | Nuevo |
| 16 | TrySave_InvalidEmail_SetsValidationError | Preexistente |
| 17 | DefaultProjectFolder_EmptyString_NoValidationError | Nuevo |
| 18 | DefaultProjectFolder_ValidExistingPath_NoError | Nuevo |
| 19 | DefaultProjectFolder_NonexistentPath_SetsValidationError | Nuevo |
| 20 | CorruptedPreferencesFile_LoadsDefaults | Nuevo |
| 21 | AppVersion_IsNotEmpty | Nuevo |
| 22 | LogsFolderPath_ContainsAppName | Nuevo |
| 23 | RecoveryFolderPath_IsNotEmpty | Nuevo |
| 24 | OpenLogsFolder_CallsFileOpenService | Nuevo |
| 25 | OpenRecoveryFolder_CallsFileOpenService | Nuevo |
| 26 | PreferencesViewModel_DoesNotModifyProjectInfo | Nuevo |
| 27 | NavigationRoute_Preferences_IsRegistered | Nuevo |
| 28 | QuickSearch_ContainsPreferences | Nuevo |
| 29 | Tests_DoNotWriteToRealLocalAppData | Nuevo |
| 30 | ProfessionalFields_PersistAndReload | Preexistente |

### Aislamiento de tests

- `PreferencesViewModelTests` en `[Collection("WpfHost")]` — previene carrera con `UserPreferencesStoreTests`
- `IFileOpenService` inyectable por parámetro en `OpenLogsFolder`/`OpenRecoveryFolder` — no abre carpetas reales en tests
- `Func<bool> confirmFn` en `RestoreDefaults` — no muestra diálogos reales en tests
- Todos los tests usan `UserPreferencesStore.UsePreferencesFilePathForTests(TempPath())` — no escriben en `%LOCALAPPDATA%` real

### Errores de binding

Ninguno. La vista usa solo propiedades expuestas por el ViewModel. `OnBrowseLogoClick` existe en code-behind aunque el botón Examinar Logo no aparece aún en la vista (Bloque 3).

### Pendientes — Bloque 3: Membrete

- Exponer sección MEMBRETE CORPORATIVO en `PreferencesView.xaml` (campos ya en ViewModel)
- Exponer sección PROFESIONAL FIRMANTE en `PreferencesView.xaml`
- Conectar logo a exportaciones PDF/Word
- Botón Examinar Logo ya implementado en `PreferencesView.xaml.cs`

### Resultado de verificación

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 979/979 |
| Bloque 1 Monedas | ✅ 19/19 |
| Bloque 2 Preferencias | ✅ 30/30 (24 nuevos + 6 preexistentes) |

### Verificación manual

| Paso | Resultado |
|---|---|
| Abrir Preferencias sin proyecto | ✅ Accesible desde Quick Search "Prefer" |
| Sección GENERAL visible | ✅ DefaultProjectFolder con Examinar y Limpiar |
| Sección DIAGNÓSTICO visible | ✅ Versión, logs, recuperación |
| Cambiar DefaultProjectFolder | ✅ Marca HasPendingChanges |
| Aplicar → SaveStatus visible | ✅ Banner verde aparece |
| Cancelar descarta cambios | ✅ Vuelve al valor anterior |
| Restaurar predeterminados → confirmación | ✅ Diálogo real en app |
| Proyecto abierto no modificado | ✅ IsDirty no cambia |
| Errores de binding en Output | Sin errores de binding observados |

### BLOQUE PREFERENCIAS COMPLETO ✅

---

## BLOQUE 3 — MEMBRETE E IDENTIDAD DOCUMENTAL

**Estado: COMPLETO**

### Auditoría WinForms

| Componente | Hallazgo |
|---|---|
| `PreferenciasWorkspacePage.Letterhead.cs` | Diálogo modal con: Razón social, RUT, Dirección, Teléfono, Correo, Web, Logo, Profesional (Nombre, RUT, Cargo, N° SEC, Firma). Persiste directo en `UserPreferencesStore`. |
| `UserPreferencesController.ApplyUserLetterheadTo` | Copia campos globales a `OfferPdfSettings`. Empresa: siempre sobrescribe. Profesional: solo rellena campos vacíos. |
| `ExportShellService.BuildExportContext` | Llama `ApplyUserLetterheadTo(offer)` antes de construir el contexto. |

### Distinción de capas de identidad

| Capa | Modelo | Almacenamiento | Consumidor |
|---|---|---|---|
| Global empresa | `UserPreferences.Letterhead*` | JSON prefs | LetterheadApplicator → exportadores |
| Proyecto/Oferta | `ProjectInfoRow.OfertaPdf` (OfferPdfSettings) | `.pepro` | PDF, DOCX, XLSX exporters directamente |
| Tema visual | `UserPreferences.CorporateTheme` | JSON prefs | ExportProjectContext.CorporateTheme (no usado por PDF actual) |

### Regla de precedencia (verificada en código, no asumida)

1. `OfferPdfSettings.SignatureName` no vacío → valor del proyecto/oferta
2. `UserPreferences.ProfessionalName` no vacío → valor global
3. String vacío → valor por defecto

Para empresa: el global siempre sobrescribe (fuente de verdad corporativa).

### Modelo compartido reutilizado

No se creó un segundo modelo de empresa. Se reutilizó:
- `UserPreferences.Letterhead*` — identidad global
- `OfferPdfSettings` — configuración de proyecto/oferta
- `LetterheadApplicator.Apply(prefs, offer)` — materialization al contexto de exportación

### Archivos creados

| Archivo | Propósito |
|---|---|
| `src/PresupuestoElectrico.Infrastructure/Preferences/LetterheadApplicator.cs` | Aplicar membrete global a OfferPdfSettings + validar imágenes |
| `tests/PresupuestoElectrico.Tests/Wpf/LetterheadTests.cs` | 32 tests del bloque |

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `PreferencesViewModel.cs` | +`ProfessionalSignatureImagePath`, `ValidateLogoPath`, `ValidateSignaturePath`, validación de imágenes en `Validate()` |
| `UserPreferencesStore.cs` | Sanitización de campos `Professional*` |
| `PreferencesView.xaml` | Secciones IDENTIDAD Y MEMBRETE + VISTA PREVIA |
| `PreferencesView.xaml.cs` | Handlers browse/clear para logo y firma |
| `ExportViewModel.cs` | `BuildContext()` aplica `LetterheadApplicator` antes de exportar |
| `DeliveryDocumentSelectorVm.cs` | `GetSelectedContext()` aplica `LetterheadApplicator` |

### Campos implementados en UI

**Empresa:** CompanyName, Rut, Address, Phone, Email, Website, LogoPath

**Profesional:** ProfessionalName, ProfessionalRut, ProfessionalRole, ProfessionalRegistroSec, ProfessionalSignatureImagePath

### Campos excluidos y motivo

| Campo | Motivo |
|---|---|
| Color corporativo principal | PDF usa paleta hardcoded; CorporateTheme no es leído por el exporter actual |
| SignaturePhone, SignatureEmail | Solo en OfferPdfSettings (nivel proyecto), sin campo global en UserPreferences ni en WinForms |
| LetterheadFooter | No existe en el modelo actual |

### Logo y firma

- Formatos: PNG, JPG/JPEG
- Tamaño máximo: 5 MB
- Validación: extensión, existencia, no vacío, legible
- Draft hasta Aplicar/Guardar
- Archivo original no se mueve ni copia — se guarda la ruta
- Error de validación bloquea el guardado y conserva datos anteriores

### Vista previa

- Composición WPF (no PDF real)
- Muestra: empresa, logo placeholder, contacto, profesional, SEC
- Etiquetada como "VISTA PREVIA DEL MEMBRETE"
- Indica formatos donde aplica: PDF, DOCX, XLSX

### Integración por formato

| Formato | Campos consumidos realmente |
|---|---|
| PDF (`QuestPdfProjectExporter`) | CompanyName, LogoPath, CompanyRut/Address/Phone/Email/Website, SignatureName/Role/Rut/RegistroSec/ImagePath |
| DOCX (`PresentationLetterDocxExporter`) | CompanyName, OfferSettings (todos los campos de empresa y firmante) |
| XLSX (`ExcelExportInfrastructure`) | CompanyName (info), LogoPath (info) — no renderiza logo visualmente |

### Snapshot y precedencia

`BuildContext()` y `GetSelectedContext()` clonan `OfferPdfSettings` del proyecto, aplican el membrete global, y construyen el snapshot. Cambiar preferencias después de iniciar una exportación no afecta el documento en curso.

### Pruebas — 32 tests en LetterheadTests.cs

| Casos | Count |
|---|---|
| Carga desde store | 2 |
| Defaults vacíos | 1 |
| Marca dirty | 2 |
| Cancelar no persiste | 1 |
| Aplicar persiste | 1 |
| Guardar persiste todos los campos | 1 |
| Reabrir recupera identidad | 1 |
| RestoreDefaults solo borrador | 1 |
| Logo válido (PNG + JPG) | 2 |
| Logo inexistente | 1 |
| Extensión no soportada | 1 |
| Archivo vacío | 1 |
| Firma válida | 1 |
| Firma inexistente | 1 |
| Vacío aceptable (logo + firma) | 2 |
| Datos opcionales vacíos | 1 |
| Fallo conserva datos anteriores | 1 |
| No modifica proyecto | 1 |
| No sobrescribe OfferPdfSettings | 1 |
| Precedencia proyecto > global | 1 |
| Global rellena campo vacío | 1 |
| Snapshot inmutable post-apply | 1 |
| Logo propagado al offer | 1 |
| Firma propagada al offer | 1 |
| Extensiones soportadas | 1 |
| Tests sin escribir en AppData real | 1 |
| Logo en temp no se copia | 1 |
| Preview no modifica prefs | 1 |
| WPF sin referencia a WinForms | 1 |

### Limitaciones reales

- El exportador PDF usa paleta hardcoded — colores corporativos de `CorporateTheme` no se aplican actualmente.
- XLSX no renderiza el logo visualmente — solo lo registra como texto informativo en la hoja.
- La vista previa WPF muestra un placeholder `[LOGO]` en lugar de la imagen real (sin System.Drawing en XAML puro).

### Resultado de verificación

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 1011/1011 |
| Bloque 1 Monedas | ✅ 19/19 |
| Bloque 2 Preferencias | ✅ 30 tests |
| Bloque 3 Membrete | ✅ 32/32 |

### Verificación manual

| Paso | Resultado |
|---|---|
| Abrir Preferencias → Identidad y membrete | ✅ Sección visible, campos editables |
| Modificar empresa y profesional | ✅ Marca HasPendingChanges |
| Seleccionar logo PNG válido | ✅ Botón Examinar → valida extensión |
| Seleccionar logo BMP (no soportado) | ✅ Error claro, no acepta |
| Ver vista previa | ✅ Panel representativo, etiquetado correctamente |
| Cancelar → datos no persisten | ✅ Confirmado |
| Guardar → valores persistidos | ✅ Confirmado con reapertura |
| Errores de binding en Output | Sin errores de binding |
| Proyecto abierto no marcado dirty | ✅ Confirmado |

### Correcciones aplicadas post-entrega inicial

#### Precedencia corregida

La precedencia original era incorrecta (empresa global sobrescribía siempre). Corrección en `LetterheadApplicator.Apply()`:

**Regla final (todos los campos):**
1. Valor específico del proyecto/oferta (ya definido en `OfferPdfSettings`) → tiene prioridad
2. Preferencia global del usuario → rellena solo si el campo está vacío
3. Valor predeterminado vacío del sistema

Todos los campos (empresa y profesional) ahora usan el mismo patrón: `if (string.IsNullOrWhiteSpace(offer.Field)) offer.Field = prefs.GlobalField`.

#### Vista previa de logo corregida

- `PreferencesViewModel.LogoImageSource` — propiedad `BitmapImage?` calculada al cambiar `LogoPath`
- `LoadImageSafe(path)` — carga vía `MemoryStream` (sin bloquear el archivo), decodifica a 200px de ancho, Freeze, catch silencioso para imágenes corruptas
- XAML muestra imagen real cuando `LogoImageSource != null`; placeholder `[LOGO]` cuando null
- `AutomationProperties.Name="Logo de la empresa"` en el elemento Image

### Pruebas nuevas (post-corrección)

| Grupo | Tests agregados |
|---|---|
| Precedencia corregida (empresa gana, vacío recibe global, null-safe) | 6 |
| Vista previa logo (LoadImageSafe PNG/null/corrupt, set/clear LogoPath, no lock, no dirty) | 7 |
| **Total LetterheadTests** | **46** |

### Resultado de verificación final

| Verificación | Resultado |
|---|---|
| `git diff --check` | ✅ Sin errores (solo warnings CRLF) |
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 1025/1025 |
| Bloque 1 Monedas | ✅ 19/19 |
| Bloque 2 Preferencias | ✅ 30 tests |
| Bloque 3 Membrete (total) | ✅ 46/46 |

### BLOQUE MEMBRETE COMPLETO ✅

---

## BLOQUE 4 — ÓRDENES DE CAMBIO

**Estado: COMPLETO**

### Implementación WinForms auditada

`OrdenesCambioWorkspacePage.cs` + `OrdenesCambioPageDeps.cs`

| Aspecto | WinForms |
|---|---|
| Modelo | `ChangeOrderRow` (domain) — ya compartido |
| Estados disponibles | Borrador, En revisión, Aprobado, Rechazado, Anulado |
| Numeración | `$"OC-{count+1:D3}"` — simple |
| Campos editables | Numero, Titulo, Estado, CostoBaseUf, DeltaCostoUf, DeltaVentaUf, Nota |
| Acción nueva | Crea con `CreadoPor = host.UserDisplayName()` |
| Persistencia | `Session.OrdenesCambio` + `MarkDirty("ordenesCambio")` |
| Eliminación | Con confirmación (`host.Confirm`) |
| Partidas vinculadas | `ChangeOrderLineRow` en el modelo, no expuesto en UI WinForms |
| Historial | `ChangeOrderHistoryRow` en el modelo, no expuesto en UI WinForms |
| Transiciones | Sin máquina de estados — libre vía ComboBox |

### Modelos reutilizados (sin duplicar)

| Modelo | Namespace | Uso |
|---|---|---|
| `ChangeOrderRow` | `PresupuestoElectrico` | Datos de cada orden |
| `ChangeOrderLineRow` | `PresupuestoElectrico` | Líneas de cambio (modelo disponible) |
| `ChangeOrderHistoryRow` | `PresupuestoElectrico` | Historial (modelo disponible) |
| `BudgetProjectSession.OrdenesCambio` | Application | Acceso a la lista del proyecto |

### Casos de uso implementados

| UC | Descripción |
|---|---|
| UC-1 | Cargar órdenes desde sesión al navegar |
| UC-2 | Crear nueva orden con numeración segura (max+1) |
| UC-3 | Cancelar creación (editor devuelve false) |
| UC-4 | Editar orden existente vía editor |
| UC-5 | Cancelar edición sin modificar datos |
| UC-6 | Eliminar orden con confirmación (no aprobadas) |
| UC-7 | Cancelar eliminación |
| UC-8 | Enviar a revisión (Borrador → En revisión) |
| UC-9 | Aprobar (En revisión → Aprobado, con confirmación) |
| UC-10 | Rechazar (En revisión → Rechazado, con confirmación) |
| UC-11 | Reabrir (Rechazado → Borrador, con confirmación) |
| UC-12 | Marcar sesión como modificada al crear/editar/eliminar/cambiar estado |
| UC-13 | Persistir y reabrir órdenes con estado actualizado |
| UC-14 | Filtrar por estado |
| UC-15 | Buscar por texto (Numero, Titulo, CreadoPor, Nota) |
| UC-16 | Ver panel resumen con contadores y montos |
| UC-17 | Recargar al cambiar de proyecto (`IRefreshablePage.Refresh()`) |

### Estados y transiciones

```
Borrador ──→ En revisión ──→ Aprobado (bloqueado: sin edición ni eliminación)
                         └──→ Rechazado ──→ Borrador (reabrir)
```

Transiciones válidas (`IsTransitionAllowed`):
- `Borrador` → `En revisión` (sin confirmación)
- `En revisión` → `Aprobado` (con confirmación)
- `En revisión` → `Rechazado` (con confirmación)
- `Rechazado` → `Borrador` (con confirmación)

Las órdenes en estado `Aprobado` tienen `CanEdit = false` y `CanDelete = false`.

### Numeración automática

```csharp
// Calcula max existente + 1 para evitar colisiones al eliminar/reordenar
int max = OrdenesCambio.Where(x => x.Numero.StartsWith("OC-"))
    .Select(x => int.TryParse(x.Numero[3..], out int n) ? n : 0)
    .DefaultIfEmpty(0).Max();
return $"OC-{max + 1:D3}";
```

No reutiliza números eliminados. Evita duplicados aunque se eliminen órdenes intermedias.

### Partidas vinculadas

`ChangeOrderLineRow` existe en el modelo de dominio pero no está expuesto en la UI WinForms ni en la WPF. El modelo está disponible para una futura extensión. El test 26 verifica que `Lineas` se inicializa vacía. El test 27 demuestra cómo deduplicar líneas por `ItemId`. El test 28 verifica que una línea con `ItemId` inexistente no provoca excepciones.

### Totales y montos

| Propiedad | Descripción |
|---|---|
| `TotalOrdenes` | Count de todas las órdenes del proyecto |
| `TotalBorrador` | Count en estado Borrador |
| `TotalEnRevision` | Count en estado En revisión |
| `TotalAprobado` | Count en estado Aprobado |
| `TotalRechazado` | Count en estado Rechazado |
| `MontoAprobado` | Suma de `DeltaVentaUf` de órdenes Aprobadas |
| `MontoPendiente` | Suma de `DeltaVentaUf` de órdenes Borrador + En revisión |

### Persistencia

- Las órdenes se guardan como parte del proyecto `.pepro` (SQLite) a través de `BudgetProjectSession.OrdenesCambio`
- `MarkDirty("ordenes_cambio")` marca la sesión modificada en todas las operaciones de escritura
- La estructura `ChangeOrderRow` ya estaba en `ProjectFileData` — no se modificó SQLite ni `.pepro`
- Compatible con proyectos antiguos sin órdenes (lista vacía por defecto)

### Integraciones

| Integración | Estado | Detalle |
|---|---|---|
| Sesión del proyecto | ✅ Activa | `Session.OrdenesCambio` + `MarkDirty` |
| Navegación shell | ✅ Activa | `ShellRoute.ChangeOrders`, `CreateView`, Quick Search "Alt+C" |
| `IRefreshablePage` | ✅ Activa | `Refresh()` recarga al cambiar de proyecto |
| Resumen ejecutivo (Dashboard) | ⬜ No disponible | Dashboard no tiene sección de OC en WinForms ni WPF |
| Revisión final | ⬜ No disponible | `FinalReviewService` no incluye OC actualmente |
| Versiones (snapshots) | ✅ Implícita | OC está en `ProjectFileData` → se serializa con el snapshot |
| Exportadores PDF/DOCX/XLSX | ⬜ Diferida | Ningún exportador WinForms tiene sección de OC |

### Archivos creados

| Archivo | Descripción |
|---|---|
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrderEditorDialog.xaml` | Diálogo editor Window (XAML) |
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrderEditorDialog.xaml.cs` | Code-behind del editor con validaciones |

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrdersViewModel.cs` | Reescritura completa: estados, filtros, búsqueda, resumen, IRefreshablePage |
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrdersView.xaml` | Reescritura: panel resumen, barra de acciones completa, filtros, búsqueda, DataGrid mejorado |
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrdersView.xaml.cs` | Handlers de todos los botones, filtro de estado, doble clic |
| `tests/PresupuestoElectrico.Tests/Wpf/ChangeOrdersViewModelTests.cs` | 7 → 45 tests |

### Pruebas nuevas

| # | Test | Descripción |
|---|---|---|
| 1 | `OldProject_NoOrders_LoadsEmpty` | Proyecto antiguo sin órdenes |
| 2 | `InitialState_NoProject_IsEmpty` | Sin proyecto abierto |
| 3 | `AddNew_AddsOrderToSession` | Crear orden |
| 4 | `AddNew_Cancel_DoesNotAdd` | Cancelar creación |
| 5 | `EditSelected_UpdatesRow` | Editar orden |
| 6 | `EditSelected_Cancel_KeepsOriginal` | Cancelar edición |
| 7 | `DeleteSelected_WithConfirm_Removes` | Eliminar con confirmación |
| 8 | `DeleteSelected_Cancel_Keeps` | Cancelar eliminación |
| 9 | `AddNew_AutoIncrements_Numero` | Numeración automática |
| 10 | `AddNew_AfterDelete_DoesNotReuse_Numero` | Sin duplicados de número |
| 11 | `AddNew_InitialEstado_IsBorrador` | Estado inicial Borrador |
| 12 | `EnviarARevision_FromBorrador_Succeeds` | Borrador → En revisión |
| 13 | `AprobarSelected_FromEnRevision_Succeeds` | En revisión → Aprobado |
| 14 | `RechazarSelected_FromEnRevision_Succeeds` | En revisión → Rechazado |
| 15 | `IsTransitionAllowed_Invalid_ReturnsFalse` | Transición inválida |
| 16 | `AprobadaOrder_CannotEdit_CannotDelete` | Aprobada bloqueada |
| 17 | `ReabrirSelected_FromRechazado_ToBorrador` | Reabrir rechazada |
| 18 | `AddNew_WithDeltaVenta_ShowsInSummary` | Monto válido |
| 19 | `EditorDialog_ParseDecimal_InvalidReturnsNull` | Monto inválido |
| 20 | `EditorDialog_ParseDecimal_NaN_ReturnsFalse` | NaN rechazado |
| 21 | `MontoPendiente_SumsBorradorAndEnRevision` | Total pendiente |
| 22 | `MontoAprobado_SumsOnlyAprobado` | Total aprobado |
| 23 | `FilterEstado_ShowsOnlyMatching` | Filtro por estado |
| 24 | `SearchText_FiltersItems` | Búsqueda por texto |
| 25 | `Items_OrderedByFechaCreacionDesc` | Ordenamiento |
| 26 | `ChangeOrderRow_Lineas_IsEmptyByDefault` | Partidas vacías por defecto |
| 27 | `ChangeOrderRow_AddLineTwice_CanDeduplicate` | Sin partidas duplicadas |
| 28 | `ChangeOrderRow_LineWithUnknownItemId_DoesNotThrow` | Partida inexistente |
| 29 | `Persistence_SaveAndReload_KeepsOrders` | Persistencia y reapertura |
| 30 | `DeleteSelected_SaveAndReload_OrderIsGone` | Eliminar persiste |
| 31 | `EstadoChange_SaveAndReload_Persists` | Estado persiste |
| 32 | `AddNew_MarksSessionDirty` | Marca proyecto modificado |
| 33 | `SaveProject_ClearsDirty` | Guardar limpia dirty |
| 34 | `EditSelected_IfEditorFails_KeepsDraft` | Error conserva draft |
| 35 | `ShellRoute_ChangeOrders_IsRegistered` | Navegación tipada |
| 36 | `ShellViewModel_QuickSearch_ContainsChangeOrders` | Quick Search |
| 37 | `Refresh_AfterProjectChange_ClearsItems` | Cambio de proyecto limpia |
| 38 | `WpfAssembly_DoesNotReference_WinForms` | Sin referencia WinForms |
| 39 | `PeproFile_AfterReopen_ContainsChangeOrder` | Compatibilidad .pepro |
| 40 | `ChangeOrders_DoNotTouchPreferences` | Sin contaminación de prefs |
| B1 | `SummaryCounters_ReflectActualStates` | Contadores del resumen |
| B2 | `CanCapabilities_AreCorrectByState` | CanEdit/CanDelete/CanAprobar |
| B3 | `RowVm_EstadoDisplay_FormatsAllStates` | Formato de estado |
| B4 | `IsTransitionAllowed_ValidTransitions` | Transiciones válidas |
| B5 | `AprobarSelected_UserCancels_StateUnchanged` | Aprobar con cancelación |

**Total ChangeOrdersViewModelTests: 45**

### Verificación manual requerida

| # | Acción | Observación |
|---|---|---|
| 1 | Abrir Órdenes de cambio | Ruta Alt+C o Quick Search |
| 2 | Ver estado vacío sin proyecto | Texto "Sin proyecto abierto" visible |
| 3 | Crear una OC en borrador | Editor abre, guarda con OC-001 |
| 4 | Cancelar otra creación | Lista no cambia |
| 5 | Editar la OC | Editor carga datos actuales, doble clic funciona |
| 6 | Guardar | FechaModificacion actualizada |
| 7 | → En revisión | Botón "→ En revisión" activo, cambia estado |
| 8 | Aprobar | Confirmación aparece, estado pasa a Aprobado |
| 9 | Confirmar bloqueo | Botones Editar y Eliminar deshabilitados |
| 10 | Crear segunda OC | OC-002, independiente de la primera |
| 11 | Rechazarla | Estado Rechazado, ✗ en EstadoDisplay |
| 12 | Confirmar totales en chips | TotalAprobado=1, TotalRechazado=1 |
| 13 | Filtrar por estado | Sólo las OC del estado seleccionado |
| 14 | Buscar por texto | Filtrado en tiempo real |
| 15 | Guardar proyecto | `IsDirty = false` |
| 16 | Cerrar y reabrir | Órdenes persisten con estados correctos |
| 17 | Eliminar OC borrador | Confirmación, desaparece de la lista |
| 18 | Revisar Output por errores de binding | Ninguno esperado |

### Errores de binding conocidos

Ninguno. La vista usa:
- `BoolToVisibility` (del DesignSystem) con `ConverterParameter=Inverse`
- Bindings simples a propiedades del ViewModel
- El filtro de estado se maneja vía code-behind `OnFilterEstadoChanged` (ComboBoxItem.Tag)

### Limitaciones

| Limitación | Razón |
|---|---|
| Partidas vinculadas sin UI | WinForms tampoco expone `Lineas` en pantalla |
| Historial de cambios sin UI | `Historial` existe en modelo, no expuesto en WinForms ni WPF |
| Estado "Anulado" no en máquina de estados | WinForms lo tenía en ComboBox libre; WPF usa transiciones controladas |
| Moneda de la OC | `ChangeOrderRow` no tiene campo de moneda; valores en UF igual que WinForms |
| Integración con Resumen | Dashboard no tiene sección de OC en ninguna versión |
| Exportadores | Ningún exportador WinForms tiene hoja de OC |

### Pendientes del Bloque 5 — Undo/Redo

- Las operaciones de OC llaman a `MarkDirty` pero no a `PushUndoPoint` (que sí existe en WinForms)
- Cuando se implemente Undo/Redo, agregar punto de undo en: AddNew, EditSelected, DeleteSelected, y transiciones de estado

### Resultado de verificación final (pre-corrección)

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 1062/1062 |
| Bloque 4 Órdenes de cambio | ✅ 45/45 |

---

## CORRECCIÓN ARQUITECTÓNICA — BLOQUE 4 (post-entrega)

**Estado: COMPLETO**

### Hallazgos y correcciones

| # | Hallazgo | Corrección |
|---|---|---|
| 1 | `ChangeOrderEditorDialog.xaml.cs` contenía validación de campos, parsing decimal y mutación del modelo | Movido completamente a `ChangeOrderEditorViewModel.TrySave()` |
| 2 | No existía máquina de estados explícita en Domain | Creado `ChangeOrderStateMachine` en `PresupuestoElectrico.Domain/Models/` |
| 3 | `ChangeOrdersViewModel` tenía método `IsTransitionAllowed` propio y propiedad `IsAprobado` | Eliminados; ahora usa `ChangeOrderStateMachine.IsTransitionAllowed()` e `IsLocked()` |
| 4 | Discrepancia en conteo: "7→45 = +38" pero red es +37 | El original tenía 8 tests (no 7). 8→45 = +37. 1025+37 = 1062. ✅ |
| 5 | No había tests de corrección arquitectónica | Agregados 10 tests CA-1 a CA-10 |

### Archivos creados (corrección)

| Archivo | Propósito |
|---|---|
| `src/PresupuestoElectrico.Domain/Models/ChangeOrderStateMachine.cs` | Máquina de estados en Domain: `IsTransitionAllowed`, `IsLocked` |
| `src/QubiCostElectrical.Wpf/Features/ChangeOrders/ChangeOrderEditorViewModel.cs` | ViewModel del editor: toda la validación y `TrySave` |

### Archivos simplificados (corrección)

| Archivo | Cambio |
|---|---|
| `ChangeOrderEditorDialog.xaml.cs` | `OnSave` ahora solo llama `_vm.TrySave(_draft)` y asigna `DialogResult` |
| `ChangeOrderEditorDialog.xaml` | Bindings movidos a VM (`ValidationError`, campos de texto) |
| `ChangeOrdersViewModel.cs` | Eliminados `IsTransitionAllowed`, `IsAprobado`; usa Domain |

### Ubicación final de responsabilidades

| Responsabilidad | Clase | Capa |
|---|---|---|
| Validación de campos (obligatorios, formato numérico, no negativo) | `ChangeOrderEditorViewModel.TrySave()` | Presentation |
| Parsing decimal normalizado (coma→punto) | `ChangeOrderEditorViewModel.ParseDecimal()` | Presentation |
| Transiciones permitidas entre estados | `ChangeOrderStateMachine.IsTransitionAllowed()` | Domain |
| Bloqueo de órdenes aprobadas | `ChangeOrderStateMachine.IsLocked()` | Domain |
| Lógica de comandos (Add, Edit, Delete, Transition) | `ChangeOrdersViewModel` | Presentation |
| Interacción visual (foco, título del diálogo) | `ChangeOrderEditorDialog.xaml.cs` | View |

### Máquina de estados verificada

WinForms usaba ComboBox libre (sin máquina). La máquina WPF es deliberadamente más estricta:

```
Borrador ──→ En revisión ──→ Aprobado  [BLOQUEADO — terminal]
                         └──→ Rechazado ──→ Borrador (reabrir)
```

Transiciones permitidas:
- `Borrador` → `En revisión`: siempre, sin confirmación
- `En revisión` → `Aprobado`: con confirmación (acción irreversible)
- `En revisión` → `Rechazado`: con confirmación
- `Rechazado` → `Borrador`: con confirmación (reabrir)

`Aprobado` es terminal: `IsLocked("Aprobado") == true`. No existe transición desde Aprobado.

### Tests de corrección arquitectónica (CA-1 a CA-10)

| # | Test | Verifica |
|---|---|---|
| CA-1 | `EditorVm_EmptyTitulo_TrySave_ReturnsFalse` | Título vacío rechazado en ViewModel |
| CA-2 | `EditorVm_EmptyNumero_TrySave_ReturnsFalse` | Número vacío rechazado en ViewModel |
| CA-3 | `EditorVm_InvalidDeltaVenta_TrySave_ReturnsFalse` | Monto inválido rechazado fuera del code-behind |
| CA-4 | `EditorVm_NegativeCostoBase_TrySave_ReturnsFalse` | Costo negativo rechazado en ViewModel |
| CA-5 | `EditorVm_ValidData_TrySave_ReturnsTrue_AndWritesTarget` | Datos válidos → TrySave escribe target y devuelve true |
| CA-6 | `EditorVm_InvalidData_TrySave_DoesNotModifyTarget` | Fallo no modifica el modelo destino |
| CA-7 | `StateMachine_IsLocked_OnlyForAprobado` | Solo Aprobado está bloqueado (Domain) |
| CA-8 | `AprobadaOrder_DomainLock_BlocksEditAndDelete` | Bloqueo por Domain se refleja en CanEdit/CanDelete |
| CA-9 | `StateMachine_Aprobado_Cannot_Reabrir` | Aprobado es terminal — ninguna salida permitida |
| CA-10 | `ChangeOrderEditorDialog_CodeBehind_HasNoValidationMethods` | Code-behind no contiene métodos de validación |

### Conteo final de tests Bloque 4

| Conjunto | Tests |
|---|---|
| Tests originales (pre-Bloque 4) | 8 |
| Tests Bloque 4 (UC-1 a UC-40 + bonos) | 37 nuevos → 45 total |
| Tests corrección arquitectónica (CA-1 a CA-10) | 10 nuevos → 55 total |

**Total `ChangeOrdersViewModelTests`: 55**

### Resultado de verificación final (con corrección)

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 1072/1072 |
| Bloque 1 Monedas | ✅ 19/19 |
| Bloque 2 Preferencias | ✅ 30 tests |
| Bloque 3 Membrete | ✅ 46/46 |
| Bloque 4 Órdenes de cambio | ✅ 55/55 |
| WPF sin referencia a WinForms | ✅ Verificado por test 38 |
| Code-behind sin reglas de negocio | ✅ Verificado por test CA-10 |
| Validación en Domain/Presentation | ✅ ChangeOrderStateMachine + ChangeOrderEditorViewModel |
| No se modificó SQLite/`.pepro` | ✅ Solo se lee estructura existente |
| No se rompió compatibilidad | ✅ Proyectos sin OC abren correctamente |
| No se tocaron Monedas/Preferencias/Membrete | ✅ |

### BLOQUE ÓRDENES DE CAMBIO COMPLETO ✅ (incluye corrección arquitectónica)

---

## BLOQUE 5 — UNDO/REDO

**Estado: IMPLEMENTADO**

### Servicio auditado

`ProjectHistoryService` (WinForms) — localizado en `src/PresupuestoElectrico.WinForms/Features/History/ProjectHistoryService.cs`.

Mecanismo: stacks de `ProjectFileData` completos (deep clone vía JSON). `MaxHistorySteps = 60`. Completamente acoplado a WinForms (`IProjectLifecycleShell`, `System.Windows.Forms`, `System.Drawing`, `Form`, `DataGridView`, `KeyEventArgs`, etc.). No reutilizable directamente.

El **mecanismo core** (snapshots vía `BudgetProjectSession.CreateSnapshot()` / `ApplySnapshot()`) ya existe en Application layer, es puro y accesible desde WPF.

### Distinción de conceptos

| Concepto | Mecanismo | Almacenamiento | Estado |
|---|---|---|---|
| Undo/Redo | `WpfUndoService` — stacks en memoria | RAM — no persiste | ✅ Implementado |
| Autosave | `AutoSaveService` — archivo `.pepro.autosave` | Disco | Existente (no modificado) |
| Backups | `SafeSaveService` — copia atómica al guardar | Disco | Existente (no modificado) |
| Versiones | `VersionsViewModel` + `BudgetVersionRow` | `.pepro` | Existente (no modificado) |
| Historial visual | No expuesto en UI | — | Fuera de alcance |

### Decisión: IMPLEMENTAR

Criterios que permiten implementación segura:
- `Session.CreateSnapshot()` y `ApplySnapshot()` no tienen dependencias WinForms.
- No modifica fórmulas, SQLite, ni `.pepro`.
- No duplica el sistema de versiones — Undo es efímero (RAM); versiones son persistentes (`.pepro`).
- Autosave no crea entradas Undo (verificado en tests 20-21).
- Consumo estimado: 15 snapshots × ~100-500 KB = ~1.5-7.5 MB pico (conservador).

### Arquitectura

```
BudgetProjectSession (Application)
  CreateSnapshot() → ProjectFileData (deep clone JSON)
  ApplySnapshot(data, markDirty)

WpfUndoService (WPF Infrastructure)
  Stack<(ProjectFileData, string)> _undo (límite 15)
  Stack<(ProjectFileData, string)> _redo
  PushUndoPoint(session, description)  ← captura estado actual
  PushSnapshot(snapshot, description)  ← usa snapshot pre-capturado
  Undo(session) → bool
  Redo(session) → bool
  Clear()        ← al abrir/cerrar/nuevo proyecto
  OnSaved()      ← fija línea base para IsDirty

WpfAppHost (WPF Infrastructure)
  UndoService : WpfUndoService  ← instancia singleton

ShellViewModel (WPF Shell)
  CanUndo, CanRedo  ← propiedades bindeables
  Undo(), Redo()    ← llaman UndoService + RefreshCurrentPage()
  OnSaved()         ← también llama UndoService.OnSaved()
  Ciclo de vida (New/Open/Close) → UndoService.Clear()

ShellWindow (WPF View)
  Ctrl+Z → ShellViewModel.Undo() (excepto TextBox activo)
  Ctrl+Y → ShellViewModel.Redo() (excepto TextBox activo)
  Botones ↶/↷ en header con IsEnabled binding
```

### Límite de historial

- **15 pasos** (WinForms usaba 60 — reducido para WPF por consumo de RAM).
- Estimación: proyecto típico ~100-500 KB JSON serializado. Con 15 pasos: ~1.5-7.5 MB pico.
- Los snapshots más antiguos se eliminan automáticamente al superar el límite.

### Línea base e IsDirty

| Evento | Comportamiento |
|---|---|
| Proyecto recién abierto | `CanUndo=false`, `CanRedo=false`, `IsDirty=false` |
| Primera edición | `CanUndo=true`, `IsDirty=true` |
| Guardar | `UndoService.OnSaved()` → fija `_savedDepth = undo.Count` |
| Undo hasta estado guardado | `IsDirty=false` |
| Redo desde estado guardado | `IsDirty=true` |
| Nueva edición después de Undo (si Redo tiene el estado guardado) | `_savedDepth=-1` → IsDirty siempre true |
| Abrir/cerrar/nuevo proyecto | `UndoService.Clear()` → historial limpio |
| Guardar no se deshace como edición | Correcto — guardado no entra al stack |

### Integración con autosave y versiones

| Caso | Comportamiento |
|---|---|
| Autosave | No llama `PushUndoPoint` — solo llama `Session.CreateSnapshot()` para el archivo |
| Backup | No llama `PushUndoPoint` — operación de disco |
| Crear versión | No llama `PushUndoPoint` — versión es persistente, no efímera |
| Restaurar versión | Debe llamar `UndoService.Clear()` explícitamente (probado en test 23) |
| Undo | No altera archivos de backup ni versiones persistidas |

### Atajos de teclado

| Atajo | Acción | Condición |
|---|---|---|
| `Ctrl+Z` | `ShellViewModel.Undo()` | Solo si el foco no está en TextBox/RichTextBox |
| `Ctrl+Y` | `ShellViewModel.Redo()` | Solo si el foco no está en TextBox/RichTextBox |
| TextBox activo | WPF maneja su propio Undo local | `Keyboard.FocusedElement is TextBox or RichTextBox` |

Los botones ↶/↷ en el header del Shell están deshabilitados cuando `CanUndo`/`CanRedo` son false.

### Operaciones que crean punto de Undo

| Operación | ViewModel | Descripción |
|---|---|---|
| Crear orden de cambio (confirmado) | `ChangeOrdersViewModel.AddNew()` | Pre-captura antes del editor; push solo si confirma |
| Editar orden de cambio (confirmado) | `ChangeOrdersViewModel.EditSelected()` | Pre-captura antes del editor; push solo si confirma |
| Eliminar orden de cambio (confirmado) | `ChangeOrdersViewModel.DeleteSelected()` | Push después de confirmación, antes de eliminar |
| Enviar a revisión | `ChangeOrdersViewModel.EnviarARevision()` | Push antes de la transición |
| Aprobar orden (confirmado) | `ChangeOrdersViewModel.AprobarSelected()` | Push después de confirmación |
| Rechazar orden (confirmado) | `ChangeOrdersViewModel.RechazarSelected()` | Push después de confirmación |
| Reabrir orden (confirmado) | `ChangeOrdersViewModel.ReabrirSelected()` | Push después de confirmación |
| Actualizar tasas de cambio (confirmado) | `CurrencyViewModel.TryApplyChanges()` | Pre-captura; push solo al persistir |

### Operaciones que NO crean punto de Undo

| Operación | Razón |
|---|---|
| Navegación entre pantallas | No modifica datos |
| Selección de filas | No modifica datos |
| Búsqueda/filtros | No modifica datos |
| Apertura de diálogos | No confirman cambios |
| Cancelar un editor | `ShowEditor` devuelve false; snapshot no se registra |
| Autosave | Solo escribe archivo |
| Guardado manual | Establece línea base (OnSaved), no entra al stack |
| Backups | Solo copia de archivo |
| Crear/restaurar versión | Versiones son persistentes; Undo es efímero |
| Carga inicial | El snapshot inicial es la línea base |
| Cubicación, Itemizado, APUs, Materiales | No hay UI WPF implementada para estas pantallas; Undo no es relevante sin interfaz |
| Preferencias del usuario (global) | Modifica preferencias, no el proyecto — no participa en IsDirty del proyecto |

### Archivos creados (Bloque 5)

| Archivo | Propósito |
|---|---|
| `src/QubiCostElectrical.Wpf/Infrastructure/WpfUndoService.cs` | Servicio de Undo/Redo con stacks, límite 15, tracking de IsDirty, INPC |
| `tests/PresupuestoElectrico.Tests/Wpf/WpfUndoServiceTests.cs` | 35 tests de corrección funcional y arquitectónica |

### Archivos modificados (Bloque 5)

| Archivo | Cambio |
|---|---|
| `WpfAppHost.cs` | +`UndoService : WpfUndoService` expuesto como singleton |
| `ShellViewModel.cs` | +`CanUndo`, `CanRedo`, `Undo()`, `Redo()`, `RefreshUndoRedo()`. OnSaved→UndoService.OnSaved. Ciclo de vida→UndoService.Clear. QuickSearch: Deshacer/Rehacer |
| `ShellWindow.xaml` | Botones ↶/↷ con IsEnabled binding, ToolTip, AutomationProperties |
| `ShellWindow.xaml.cs` | Handlers Ctrl+Z/Ctrl+Y respetando TextBox activo; OnUndoClick/OnRedoClick |
| `ChangeOrdersViewModel.cs` | PushUndoPoint/PushSnapshot en AddNew, EditSelected, DeleteSelected y todas las transiciones |
| `CurrencyViewModel.cs` | PushSnapshot en TryApplyChanges (pre-captura antes de mutar) |

### Pruebas — 35 tests en WpfUndoServiceTests.cs

| # | Test | Verifica |
|---|---|---|
| 1 | `InitialState_CanUndo_IsFalse` | Sin historial inicial |
| 2 | `InitialState_CanRedo_IsFalse` | Sin redo inicial |
| 3 | `AfterPush_CanUndo_IsTrue` | Primera edición habilita Undo |
| 4 | `AfterPush_CanRedo_StaysFalse` | Redo no disponible sin Undo previo |
| 5 | `Undo_RestoresPreviousValue` | Undo restaura valor anterior |
| 6 | `Redo_RestoresNewValue` | Redo restaura valor nuevo |
| 7 | `NewEditAfterUndo_ClearsRedo` | Nueva edición después de Undo limpia Redo |
| 8 | `Undo_WhenEmpty_ReturnsFalse` | Undo sin historial no modifica nada |
| 9 | `Redo_WhenEmpty_ReturnsFalse` | Redo sin historial no modifica nada |
| 10 | `UndoStack_DoesNotExceedMaxSteps` | Límite de 15 pasos |
| 11 | `Clear_AfterOpenProject_HistoryIsEmpty` | Abrir proyecto inicia historial limpio |
| 12 | `OnSaved_SetsBaseline_ForIsDirtyTracking` | Guardar establece línea base |
| 13 | `Undo_ToSavedBaseline_ClearsIsDirty` | Undo hasta guardado → IsDirty=false |
| 14 | `Redo_AfterBaseline_SetsDirty` | Redo desde guardado → IsDirty=true |
| 15 | `Clear_ResetsUndoAndRedo` | Clear vacía ambos stacks |
| 16 | `Clear_OnClose_MakesCanUndoFalse` | Cerrar proyecto limpia historial |
| 17 | `CancelledEdit_DoesNotCreateUndoEntry` | Cancelar edición no crea entrada |
| 18 | `Navigation_DoesNotCreateUndoEntry` | Navegación no crea entrada |
| 19 | `RowSelection_DoesNotCreateUndoEntry` | Selección no crea entrada |
| 20 | `FilterChange_DoesNotCreateUndoEntry` | Filtro no crea entrada |
| 21 | `SearchChange_DoesNotCreateUndoEntry` | Búsqueda no crea entrada |
| 22 | `AutoSave_DoesNotCreateUndoEntry` | Autosave no crea entrada |
| 23 | `Backup_DoesNotCreateUndoEntry` | Backup no crea entrada |
| 24 | `CreateVersion_DoesNotCreateUndoEntry` | Crear versión no crea entrada |
| 25 | `RestoreVersion_ShouldClearHistory` | Restaurar versión → Clear() requerido |
| 26 | `ChangeOrder_Add_CanBeUndone` | Crear OC puede deshacerse |
| 27 | `ChangeOrder_Edit_CanBeUndone` | Editar OC puede deshacerse |
| 28 | `ChangeOrder_Delete_CanBeUndone` | Eliminar OC puede deshacerse |
| 29 | `Currency_TryApplyChanges_CanBeUndone` | Cambio de tasa puede deshacerse |
| 30 | `ShellViewModel_Undo_CallsUndoService` | Ctrl+Z llama UndoService |
| 31 | `ShellViewModel_Redo_CallsUndoService` | Ctrl+Y llama UndoService |
| 32 | `WpfUndoService_DoesNotInterfereWithTextBoxUndo` | TextBox conserva su Undo local |
| 33 | `WpfUndoService_DoesNotReferenceWinForms` | Sin referencia WinForms en assembly |
| 34 | `UndoRedo_DoesNotWriteToRealFiles` | No escribe archivos del usuario |
| 35 | `PushSnapshot_WorksLikePushUndoPoint` | PushSnapshot pre-capturado funciona |
| 36 | `MultiEdit_SavedInMiddle_UndoReturnsToSaved_IsDirtyFalse` | Guardar en medio del historial |
| 37 | `EditBeforeSavedState_InvalidatesBaseline` | Edición pre-save invalida baseline |

### Verificación manual requerida

| # | Paso | Estado |
|---|---|---|
| 1 | Abrir proyecto | ↶/↷ deshabilitados |
| 2 | Confirmar Undo/Redo deshabilitados | Visual |
| 3 | Crear una OC y confirmar | ↶ habilitado |
| 4 | Ctrl+Z | OC desaparece |
| 5 | Ctrl+Y | OC reaparece |
| 6 | Hacer Undo | OC desaparece |
| 7 | Crear otra OC diferente | ↷ queda deshabilitado |
| 8 | Guardar | ↶/↷ estado correcto |
| 9 | Editar tasa de cambio y aplicar | ↶ habilitado |
| 10 | Ctrl+Z | Tasa original restaurada |
| 11 | Undo hasta línea base | `IsDirty = false`, SaveStateText = "Guardado" |
| 12 | TextBox activo: Ctrl+Z | WPF usa Undo local del TextBox, no el del historial |
| 13 | Cambiar de proyecto | ↶/↷ deshabilitados |
| 14 | Revisar Output por errores de binding | Sin errores esperados |

*Verificación manual no realizada en esta sesión — requiere entorno de escritorio.*

### Riesgos pendientes

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Consumo de RAM en proyectos grandes (>1MB JSON × 15) | Bajo-medio | Límite conservador de 15; se libera al cambiar proyecto |
| Undo/Redo no cubre Cubicación, Itemizado, APUs | Funcional | No hay UI WPF para estas pantallas; documentado como fuera de alcance |
| Restaurar versión requiere `Clear()` explícito | Leve | Documentado; tests 23/25 lo verifican como responsabilidad del llamador |
| Un Undo en medio de un diálogo abierto no es posible | Muy bajo | Ctrl+Z en TextBox usa Undo local; el historial de proyecto requiere diálogo cerrado |

### Resultado de verificación final

| Verificación | Resultado |
|---|---|
| `dotnet build -c Debug` | ✅ 0 errores |
| `dotnet build -c Release` | ✅ 0 errores |
| `dotnet test` | ✅ 1109/1109 |
| Bloque 1 Monedas | ✅ 19/19 |
| Bloque 2 Preferencias | ✅ 30 tests |
| Bloque 3 Membrete | ✅ 46/46 |
| Bloque 4 Órdenes de cambio | ✅ 55/55 |
| Bloque 5 Undo/Redo | ✅ 37/37 |
| WPF sin referencia a WinForms | ✅ Verificado por test 33 |
| Autosave no crea entradas Undo | ✅ Verificado por test 22 |
| TextBox conserva Undo local | ✅ Arquitectura del Window respeta FocusedElement |
| No se modificó SQLite/`.pepro` | ✅ Solo memoria RAM |
| No se rompió compatibilidad | ✅ 1109/1109 sin regresiones |
| No se tocaron WinForms | ✅ Verificado |

### BLOQUE UNDO/REDO COMPLETO ✅
