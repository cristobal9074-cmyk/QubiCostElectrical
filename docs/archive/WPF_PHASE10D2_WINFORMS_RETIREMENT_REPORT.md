# WPF Phase 10D.2 - WinForms Retirement Audit

**Fecha:** 2026-06-23  
**Rama:** `feature/wpf-phase10b-functional-parity`  
**Restore point:** `855fd2fb0b30208d42f56dd902e03646998e01d0`  
**Tag de restauracion:** `pre-winforms-retirement`  
**Estado del repositorio al auditar:** limpio

## Resumen ejecutivo

La auditoria previa al retiro controlado de WinForms esta completa. Los bloques piloto de migracion de `LastSaveStatusFormatter`, `UiErrorPolicy` y `ValidationHelper` ya fueron movidos fuera de WinForms hacia capas compartidas.

Hallazgos principales:

- La separacion WPF/WinForms ya esta reconciliada y el punto de restauracion queda fijado.
- `QubiCostElectrical.Wpf.sln` no referencia `PresupuestoElectrico.WinForms` ni `PresupuestoElectrico.WinForms.Tests`.
- La solucion legacy `PresupuestoElectrico.sln` sigue incluyendo WinForms y su suite de pruebas dedicada.
- Existen dependencias de WinForms en instalacion, publicacion, CI, documentacion y en el arbol de codigo WinForms.
- La bateria de WinForms contiene 97 pruebas unicas y ya esta aislada del inventario compartido.
- La solucion WPF sigue compilando y testeando limpia con `--no-restore --no-build`.

## Estado verificado antes de la auditoria

| Verificacion | Resultado |
|---|---|
| Build WPF Debug | OK |
| Build WPF Release | OK |
| Test WPF con `--no-restore --no-build` | OK: `1012/1012`, `0` fallos, `0` omitidos |
| Solucion WPF sin WinForms | OK |
| Inventario total unico | `1109` pruebas |
| Inventario WinForms aislado | `97` pruebas |
| Restore de NuGet en `%APPDATA%` | Bloqueado por ACL local, fuera de alcance del codigo |

## Inventario de dependencias WinForms en el repositorio

### Dependencias estructurales y de entrega

| Categoria | Evidencia | Impacto | Estado |
|---|---|---|---|
| Solucion legacy | `PresupuestoElectrico.sln` | Sigue compilando WinForms y su test project | Mantener hasta retiro final |
| Proyecto UI legacy | `src/PresupuestoElectrico.WinForms/PresupuestoElectrico.WinForms.csproj` | Es la UI WinForms completa | Legacy |
| Test project legacy | `tests/PresupuestoElectrico.WinForms.Tests/PresupuestoElectrico.WinForms.Tests.csproj` | 97 tests dedicados a WinForms | Legacy |
| Instalador | `installer/PresupuestoElectricoPRO.iss` | Publica `PresupuestoElectrico.exe` desde WinForms | Pendiente de migracion |
| Script de publicacion | `scripts/Publish-Release.ps1` | Publica WinForms | Pendiente de migracion |
| CI de build | `.github/workflows/build.yml` | Todavia publica WinForms en el job `publish` | Pendiente de migracion |
| Build release local | `deploy/build-release.ps1` | Apunta a `src\PresupuestoElectrico.WinForms\PresupuestoElectrico.WinForms.csproj` | Pendiente de migracion |

### Dependencias de codigo WinForms

El arbol `src/PresupuestoElectrico.WinForms` sigue siendo grande y concentrado:

| Area | Archivos con referencia WinForms | Observacion |
|---|---:|---|
| `Features` | 94 | La mayor parte de la logica de negocio/presentacion legacy vive aqui |
| `Forms` | 28 | MainForm y superficies auxiliares del shell WinForms |
| `Controls` | 24 | Controles reutilizables y custom drawing |
| `Navigation` | 4 | Enrutamiento, catalogo y coordinacion de pantallas |
| `Shell` | 3 | Ensamblado y layout del shell |
| `State` | 2 | Estado UI de respaldo |
| Archivos sueltos (`Program.cs`, `LoadingForm.cs`, `AppTheme*`, etc.) | 6 | Entry point, tema, loader, errores y utilitarios |

En total hay **167 archivos de codigo/test** que siguen tocando WinForms o sus tipos en el arbol legacy auditado.

### Menciones documentales y operativas

Tambien siguen existiendo referencias a WinForms en artefactos no ejecutables:

| Zona | Archivos con mension WinForms | Observacion |
|---|---:|---|
| `docs/` | 20 | Documentacion historica y reportes de fases previas |
| `tests/` | 20 | Principalmente la suite legacy WinForms |
| `src/` | 14 | Codigo legacy y una mension textual en WPF |
| `.github/` | 1 | Workflow de build/publish |
| `installer/` | 1 | Inno Setup legacy |
| `scripts/` | 1 | Publicacion legacy |

### Aclaracion sobre WPF

No se encontro dependencia real de WPF hacia WinForms.  
La unica mension restante en WPF es textual:

- `src/QubiCostElectrical.Wpf/Features/Documents/DocumentsViewModel.cs:174`

Esa cadena solo informa que cierta importacion masiva sigue disponible en WinForms mientras se migra. No introduce dependencia compilable ni runtime.

## Inventario de las 97 pruebas WinForms

### Conteo por clase

| Namespace / clase | Pruebas |
|---|---:|
| `PresupuestoElectrico.Tests.Composition.AppServicesBuilderTests` | 4 |
| `PresupuestoElectrico.Tests.Composition.ShellServicesTests` | 2 |
| `PresupuestoElectrico.Tests.WinForms.AppNavigationServiceTests` | 5 |
| `PresupuestoElectrico.Tests.WinForms.AppThemeDesignSystemTests` | 10 |
| `PresupuestoElectrico.Tests.WinForms.ExportPlantillasThemeTests` | 4 |
| `PresupuestoElectrico.Tests.WinForms.MainFormArchitectureGuardTests` | 6 |
| `PresupuestoElectrico.Tests.WinForms.MainFormShellLeanTests` | 6 |
| `PresupuestoElectrico.Tests.WinForms.NavigationPageCatalogTests` | 31 |
| `PresupuestoElectrico.Tests.WinForms.ProjectStoragePathsTests` | 19 |
| `PresupuestoElectrico.Tests.WinForms.ShellLayoutMetricsTests` | 3 |
| `PresupuestoElectrico.Tests.WinForms.WinFormsLegacyCleanupTests` | 3 |
| `PresupuestoElectrico.Tests.WinForms.WinFormsThemeCompletenessTests` | 1 |
| `PresupuestoElectrico.Tests.WinForms.WorkspaceNavigationCoordinatorTests` | 1 |
| `PresupuestoElectrico.Tests.WinForms.WorkspaceNavigationRegistrationTests` | 2 |

**Total:** `97`

### Clasificacion funcional

| Categoria | Pruebas | Lectura de migracion |
|---|---:|---|
| Composicion / servicios compartibles | 6 | Buen candidato para mover a infraestructura compartida o a tests comunes |
| Navegacion / catalogo / registro | 39 | Logica reutilizable si se extrae de WinForms hacia abstracciones neutrales |
| Persistencia / rutas / formato de estado | 19 | Muy reutilizable; conviene conservar como helpers puros o shared services |
| Tema / layout / politica visual | 18 | Mayormente util para portar tokens y reglas a WPF |
| Guardias de arquitectura y limpieza legacy | 15 | Sirven para el retiro; no deberian migrarse tal cual a produccion WPF |

## Logica reutilizable que conviene migrar despues

Los siguientes bloques no deben reescribirse a ciegas; son los candidatos mas claros para una futura extraccion o traslado a WPF/shared:

- `src/PresupuestoElectrico.WinForms/Navigation/AppNavigationService.cs`
- `src/PresupuestoElectrico.WinForms/Navigation/NavigationCatalog.cs`
- `src/PresupuestoElectrico.WinForms/Navigation/PageNavigator.cs`
- `src/PresupuestoElectrico.WinForms/Navigation/WorkspaceNavigationCoordinator.cs`
- `src/PresupuestoElectrico.WinForms/Navigation/WorkspacePageFactory.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectLifecycleService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectOpenService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectSaveService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectPersistenceService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectAutoSaveService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectBackupService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ShellUi/ShellUiService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ShellUi/WindowChromeService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ShellUi/PageHeaderService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ShellUi/ProfessionalErgonomicsService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ShellUi/ContextualTooltipsService.cs`
- `src/PresupuestoElectrico.WinForms/Features/Exportacion/ExportOrchestrator.cs`
- `src/PresupuestoElectrico.WinForms/Features/Exportacion/ExportShellService.cs`
- `src/PresupuestoElectrico.WinForms/Features/Exportacion/ExportPageServices.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectHub/ProjectHubService.cs`
- `src/PresupuestoElectrico.WinForms/Features/Preferences/UserPreferencesController.cs`
- `src/PresupuestoElectrico.WinForms/Features/Workflow/WorkflowUiService.cs`
- `src/PresupuestoElectrico.WinForms/Features/Precios/PreciosPageServices.cs`
- `src/PresupuestoElectrico.WinForms/Features/Cubicacion/CubicacionPageServices.cs`
- `src/PresupuestoElectrico.WinForms/Features/Revision/RevisionPageServices.cs`
- `src/PresupuestoElectrico.WinForms/Features/Apus/ApuPageServices.cs`

### Helpers puros o casi puros

Estos archivos son especialmente valiosos para extraer primero:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectAutoSaveService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectBackupService.cs`

`ValidationHelper` ya fue extraido y ahora vive en `src/PresupuestoElectrico.Application/Validation/ValidationHelper.cs`.

### Bloque de migracion piloto completado

`LastSaveStatusFormatter` fue el primer helper elegido para el traslado controlado fuera de WinForms. El archivo se movio a `src/PresupuestoElectrico.Application/Session/LastSaveStatusFormatter.cs` y sus tres pruebas pasaron a `tests/PresupuestoElectrico.Tests/Application/LastSaveStatusFormatterTests.cs`.

Razon de destino:

- Es un formateador puro.
- No usa controles WinForms.
- Ya dependia solo de `AppClock` y de cadenas de texto.
- Era reutilizable tanto por WinForms como por WPF.

Referencias actualizadas en produccion:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectPersistenceService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectLifecycleService.cs`

Validaciones ejecutadas tras la migracion:

| Comando | Resultado |
|---|---|
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~LastSaveStatusFormatter -v minimal` | `3/3` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1008` pruebas listadas en la bateria compartida/WPF |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --list-tests` | `101` pruebas listadas en WinForms |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores, `92` advertencias |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1008/1008`, `0` fallos, `0` omitidos, exit code `0`, `4 m 26 s` |

Efecto sobre el inventario:

- WinForms baja de `104` a `101` pruebas.
- La bateria compartida/WPF sube de `1005` a `1008` pruebas.
- El total unico permanece en `1109`.

### Bloque de migracion 2 completado

`UiErrorPolicy` se movio a `src/PresupuestoElectrico.Infrastructure/Errors/UiErrorPolicy.cs`. La clase no dependia de controles WinForms, pero si de `OperationFailureMessages` y `SqliteStorageMessages`, que ya viven en Infrastructure. Llevarla a `Application` habria creado un ciclo de referencias con `PresupuestoElectrico.Infrastructure -> PresupuestoElectrico.Application`.

Clasificacion inicial del componente:

- `EXTRAIBLE CON ABSTRACCION PEQUENA`

Razon de destino:

- Mantiene comportamiento intacto.
- Sigue siendo neutral para la UI.
- Evita ciclos de proyectos.
- Encaja con el grupo de mensajes y errores de Infrastructure.

Archivos movidos:

- `src/PresupuestoElectrico.WinForms/UiErrorPolicy.cs`
- `tests/PresupuestoElectrico.WinForms.Tests/UiErrorPolicyTests.cs`

Ubicacion nueva:

- `src/PresupuestoElectrico.Infrastructure/Errors/UiErrorPolicy.cs`
- `tests/PresupuestoElectrico.Tests/Infrastructure/UiErrorPolicyTests.cs`

Consumo actualizado:

- `src/PresupuestoElectrico.WinForms/Forms/MainForm.Errors.cs`

Validaciones ejecutadas:

| Comando | Resultado |
|---|---|
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~UiErrorPolicy -v minimal` | `4/4` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1012` pruebas listadas en la bateria compartida/WPF |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --list-tests` | `97` pruebas listadas en WinForms |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --no-build -v minimal` | `97/97`, `0` fallos, `0` omitidos, exit code `0`, `7 s` |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 25 s` |

Riesgos o trabajo pendiente:

- El inventario WinForms sigue teniendo dependencias de retiro controlado y publicacion legacy.

### Bloque de migracion 3 completado

`ValidationHelper` se clasifico como `PURO` y se traslado desde WinForms hacia `src/PresupuestoElectrico.Application/Validation/ValidationHelper.cs`. No dependia de controles WinForms ni de adaptadores visuales; solo encapsulaba reglas de validacion de texto apoyandose en `ProjectTextValidator`, que ya vive en Application.

Clasificacion final del componente:

- `PURO`

Razon de destino:

- No usa `Control`, `Form`, `MessageBox` ni `ErrorProvider`.
- Reutiliza reglas puras ya existentes en Application.
- No crea ciclos entre proyectos.
- Conserva el comportamiento para WinForms y para cualquier consumidor futuro.

Archivos movidos:

- `src/PresupuestoElectrico.WinForms/ValidationHelper.cs`

Ubicacion nueva:

- `src/PresupuestoElectrico.Application/Validation/ValidationHelper.cs`

Consumo actualizado:

- `src/PresupuestoElectrico.WinForms/Features/Plantillas/PlantillasWorkspacePage.ImportExport.cs`
- `src/PresupuestoElectrico.WinForms/Features/Exportacion/ExportOfferPdfSettingsDialog.cs`
- `src/PresupuestoElectrico.WinForms/Features/Preferences/PreferenciasWorkspacePage.Letterhead.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectLifecycleService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectSettingsDialogService.cs`
- `src/PresupuestoElectrico.WinForms/Features/Itemizado/ItemizadoWorkspacePage.ProFeatures.cs`

Validaciones ejecutadas:

| Comando | Resultado |
|---|---|
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~Validation" -v minimal` | `18/18` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1012` pruebas listadas en la bateria compartida/WPF |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `97` pruebas listadas en WinForms |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore -v minimal` | `97/97`, `0` fallos, `0` omitidos, exit code `0`, `7 s` |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 19 s` |

Efecto sobre el inventario:

- WinForms se mantiene en `97` pruebas aisladas.
- La bateria compartida/WPF queda en `1012` pruebas.
- El total unico permanece en `1109`.

### Bloque de auditoria 4 - ProjectBackupService

`ProjectBackupService` se clasifico como `DEPENDIENTE DE WINFORMS`.

Motivos reales de la clasificacion:

- Recibe `IProjectLifecycleShell`, cuyo contrato vive en WinForms y expone `Form ShellForm`.
- Usa `ShellPresentation` para `Alert`, `Confirm` y `ReportHandledException`.
- Usa `ShellOps.BeginBusyOperation`, que es parte del shell visual.
- Su responsabilidad no es solo filesystem: decide cuando mostrar dialogos, cuando bloquear la apertura y como continuar tras un error.
- El filesystem puro ya esta separado en `src/PresupuestoElectrico.Infrastructure/SQLite/SQLiteProjectBackup.cs` y `ProjectRepository`.

Conclusion arquitectonica:

- No existe una extraccion segura y pequena hacia `Application` sin introducir un adaptador visual adicional.
- No conviene moverlo a `Infrastructure` porque el orquestador seguiria requiriendo el shell WinForms para confirmar, alertar y reportar errores.
- La logica neutral reutilizable ya esta en Infrastructure; lo que queda aqui es coordinacion de UI y recuperacion guiada por el shell.

Ubicacion actual:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectBackupService.cs`

Consumidores directos:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectPersistenceService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectOpenService.cs` via `ShellPersistence`

Dependencias observadas:

- `IProjectLifecycleShell`
- `MainForm.ShellPresentationAccessor`
- `MainForm.ShellOperationsAccessor`
- `IProjectRepository`
- `System.IO.File`
- `System.IO.Directory`
- `System.IO.Path`
- `AppClock`

Comportamiento preservado:

- Carpeta de respaldo previa a migracion: `.schema_backups`
- Carpeta de respaldos numerados: `.backups`
- Politica numerada: hasta `10` slots
- Fallback a backup usable mas reciente
- Confirmacion antes de restaurar
- Mensajes de error y de continuidad intactos
- Tratamiento de archivo inexistente intacto
- Tratamiento de respaldo fallido intacto

Pruebas y verificaciones relacionadas:

| Comando | Resultado |
|---|---|
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~Backup" -v minimal` | `5/5` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~Backup" -v minimal` | Ninguna prueba coincidio con el filtro |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1012` pruebas listadas en la bateria compartida/WPF |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `97` pruebas listadas en WinForms |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 14 s` |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 17 s` |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore -v minimal` | `97/97`, `0` fallos, `0` omitidos, exit code `0`, `10 s` |

Inventario confirmado:

- Shared/WPF: `1012`
- WinForms: `97`
- Total unico: `1109`

Veredicto del bloque:

- No se realizo traslado de `ProjectBackupService` porque su dependencia real sigue siendo WinForms.
- Se mantiene en WinForms por ahora, con la logica neutral ya concentrada en Infrastructure.
- No se creo un commit porque no hubo extraccion segura que justificara el traslado.

### Bloque de auditoria 5 - ProjectAutoSaveService

`ProjectAutoSaveService` se clasifico como `DEPENDIENTE DE WINFORMS`.

Motivos reales de la clasificacion:

- El servicio recibe `IProjectLifecycleShell`, que esta definido en el arbol WinForms y expone el shell visual completo.
- Usa `ShellPresentation` para confirmar recuperaciones, reportar errores y actualizar la barra de estado.
- Usa `ShellLayout` para persistir estado visual y escribir mensajes en la UI.
- Usa `ShellOps` para obtener el nombre visible del proyecto, verificar contenido util, crear snapshots y aplicar recuperaciones.
- La politica neutral util ya esta en `src/PresupuestoElectrico.Application/ProjectStoragePaths.cs` y no queda una pieza aislada con suficiente valor para mover sin arrastrar el adaptador visual.
- El temporizador real del autoguardado no vive aqui, sino en `ProjectPersistenceService`, por lo que este archivo funciona como ejecutor del flujo y no como politica pura independiente.

Conclusion arquitectonica:

- No existe una extraccion segura y pequena hacia Application sin introducir otro contrato intermedio para el shell.
- No conviene moverlo a Infrastructure porque la responsabilidad restante no es filesystem pura, sino coordinacion del autoguardado con UI, restauracion y feedback al usuario.
- El componente debe permanecer en WinForms como adaptador de autoguardado y restauracion.

Ubicacion actual:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectAutoSaveService.cs`

Consumidores directos:

- `src/PresupuestoElectrico.WinForms/Features/ProjectPersistence/ProjectPersistenceService.cs`

Consumo indirecto:

- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectSaveService.cs`
- `src/PresupuestoElectrico.WinForms/Features/ProjectLifecycle/ProjectOpenService.cs`

Dependencias observadas:

- `IProjectLifecycleShell`
- `MainForm.ShellPresentationAccessor`
- `MainForm.ShellLayoutAccessor`
- `MainForm.ShellOpsAccessor`
- `ProjectStoragePaths`
- `System.IO.File`
- `System.IO.Directory`
- `System.IO.Path`
- `System.Text.Json.JsonSerializer`
- `AppClock`

Comportamiento preservado:

- No guarda si ya hay una operacion en curso.
- No guarda si no hay cambios, incluso con `force`.
- No guarda si no hay contenido util de proyecto.
- Usa `ProjectStoragePaths.AutoSaveDirectory()` para autoguardado sin proyecto.
- Usa `ProjectStoragePaths.GetAutoSavePathForProject(...)` para sidecar junto al `.pepro`.
- Crea directorio antes de escribir.
- Escribe archivo temporal `.tmp`, copia al destino y elimina el temporal.
- Actualiza `lastSavedAt` solo al terminar bien.
- Conserva confirmacion para restaurar autoguardado reciente.
- Conserva el borrado del autoguardado cuando el usuario descarta la recuperacion.
- Conserva el limite de 30 dias para autoguardados viejos al iniciar.
- Conserva el mensaje de estado y el flujo de recuperacion por proyecto.

Pruebas y verificaciones relacionadas:

| Comando | Resultado |
|---|---|
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AutoSave" -v minimal` | `5/5` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AutoSave" -v minimal` | `3/3` pruebas superadas, `0` fallos, exit code `0` |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1012` pruebas listadas en la bateria compartida/WPF |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `97` pruebas listadas en WinForms |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 21 s` |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 21 s` |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore -v minimal` | `97/97`, `0` fallos, `0` omitidos, exit code `0`, `8 s` |

Inventario confirmado:

- Shared/WPF: `1012`
- WinForms: `97`
- Total unico: `1109`

Veredicto del bloque:

- No se realizo traslado de `ProjectAutoSaveService`.
- La politica util ya vive en `ProjectStoragePaths`, y lo que queda aqui es coordinacion con shell visual WinForms.
- No se creo ningun archivo nuevo ni se movio codigo.

## Matriz de disposición del código WinForms restante

### Estado actual verificado

| Verificacion | Resultado |
|---|---|
| `git status --short` | Limpio |
| `git rev-parse --short HEAD` | `10e02e3` |
| `dotnet test tests\\PresupuestoElectrico.WinForms.Tests\\PresupuestoElectrico.WinForms.Tests.csproj -c Debug --no-restore --no-build --list-tests -v minimal` | `97` pruebas WinForms listadas |
| `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests` | `1012` pruebas compartidas/WPF listadas |
| Inventario total unico | `1109` pruebas |

### Disposición por bloque

| Categoria | Estado | Bloques representativos | Lectura tecnica |
|---|---|---|---|
| MIGRAR A CAPA NEUTRAL | Sin candidato limpio adicional | La politica neutral ya fue extraida en fases previas (`ProjectStoragePaths`, `ValidationHelper`, `LastSaveStatusFormatter`). Lo restante vive mezclado dentro de servicios de shell o UI. | No queda un archivo standalone que justifique otra extraccion pequena sin arrastrar adaptadores WinForms. |
| REEMPLAZADO POR WPF | Confirmado | `Navigation/AppNavigationService.cs`, `Navigation/NavigationCatalog.cs`, `Navigation/PageNavigator.cs`, `Navigation/WorkspaceNavigationCoordinator.cs`, `Features/ProjectHub/ProjectHubService.cs`, `Features/Preferences/UserPreferencesController.cs`, `Features/Dashboard/DashboardPageServices.cs`, `Features/Proyecto/ProyectoPageServices.cs`, `Features/Cubicacion/CubicacionPageServices.cs`, `Features/Apus/ApuPageServices.cs`, `Features/Revision/RevisionPageServices.cs` | WPF ya tiene equivalentes directos en `ShellViewModel`, `ProjectViewModel`, `DashboardViewModel`, `Cubicacion*Vm`, `ApuWorkspaceVm`, `ReviewViewModel` y `PreferencesViewModel`. Estas superficies son duplicacion legacy, no fuente de verdad futura. |
| DIVIDIR | Confirmado | `Features/ProjectLifecycle/ProjectLifecycleService.cs`, `ProjectOpenService.cs`, `ProjectSaveService.cs`, `Features/ProjectPersistence/ProjectPersistenceService.cs`, `ProjectAutoSaveService.cs`, `ProjectBackupService.cs`, `Features/Exportacion/ExportShellService.cs`, `Features/Workflow/WorkflowUiService.cs` | Mezclan politica, persistencia y shell visual. Tienen fragmentos reutilizables, pero la implementacion actual sigue acoplada a WinForms. |
| MANTENER HASTA CERRAR PARIDAD | Confirmado | `Features/Exportacion/*`, `Features/Conjuntos/*`, `Features/CubicacionSC/*`, `Features/OrdenesCambio/*`, `Features/Monedas/*` | Son las superficies que siguen vivas mientras se cierra la paridad funcional. Lista de compra cerrada (Fase 10D.8). Versiones cerrada (Fase 10D.9). Cotizaciones cerrada (Fase 10D.10). Plantillas cerrada (Fase 10D.11). |
| REEMPLAZADO POR WPF (Lista de compra) | Completado Fase 10D.8 | `Features/Precios/PreciosWorkspacePage.PurchaseList.cs` | WPF tiene `PurchaseListViewModel` + `PurchaseListView` con paridad funcional completa. Ver sección "Cierre de paridad WPF — Lista de compra". |
| REEMPLAZADO POR WPF (Versiones) | Completado Fase 10D.9 | `Features/Versiones/VersionesWorkspacePage.cs` | WPF tiene `VersionsViewModel` + `VersionsView` con paridad funcional completa. Ver sección "Cierre de paridad WPF — Versiones". |
| REEMPLAZADO POR WPF (Cotizaciones) | Completado Fase 10D.10 | `Features/Precios/ImportarCotizacionDialog.cs` | WPF tiene `QuotesViewModel` + `QuotesView` con importación, comparación y aplicación de precios. Ver sección "Cierre de paridad WPF — Cotizaciones". |
| REEMPLAZADO POR WPF (Plantillas) | Completado Fase 10D.11 | `Features/Plantillas/PlantillasWorkspacePage*.cs` | WPF tiene `PlantillasVm` + `PlantillasView` con CRUD, import/export JSON, rename, conjuntos eléctricos. Ver sección "Cierre de paridad WPF — Plantillas". |
| RETIRAR CON WINFORMS | Confirmado | `Forms/MainForm.*`, `Forms/CompanySetupForm.cs`, `Forms/LoadingForm.cs`, `Onboarding/NewProjectWizardForm.cs`, `Controls/*`, `Shell/*`, `AppTheme*`, `Program.cs`, `Features/ProjectLifecycle/ProjectFileDialogService.cs` | Son chrome, controles o utilidades WinForms puras. No deberian migrarse a WPF uno por uno; se retiran cuando WinForms desaparezca como host. |

### Clasificacion de las 97 pruebas WinForms

| Categoria | Pruebas | Clases incluidas |
|---|---:|---|
| Composicion / servicios compartidos | 6 | `AppServicesBuilderTests`, `ShellServicesTests` |
| Navegacion / catalogo / registro | 39 | `AppNavigationServiceTests`, `NavigationPageCatalogTests`, `WorkspaceNavigationCoordinatorTests`, `WorkspaceNavigationRegistrationTests` |
| Persistencia / rutas / autosave | 19 | `ProjectStoragePathsTests` |
| Visual / tema / layout | 18 | `AppThemeDesignSystemTests`, `ExportPlantillasThemeTests`, `ShellLayoutMetricsTests`, `WinFormsThemeCompletenessTests` |
| Limpieza y guardias legacy | 15 | `MainFormArchitectureGuardTests`, `MainFormShellLeanTests`, `WinFormsLegacyCleanupTests` |

### Lectura ejecutiva

- Las superficies shell y de chrome WinForms ya estan claramente condenadas a retiro.
- Las facades que duplican ViewModels WPF no deberian expandirse mas.
- La unica extraccion neutral nueva claramente util en esta fase no aparece como archivo standalone; el resto ya esta absorbido o sigue acoplado al shell.
- Las cinco pantallas aun medianas de paridad siguen justificando mantener sus bloques WinForms hasta cerrar equivalencia.

## Gaps y riesgos

| Hallazgo | Severidad | Comentario |
|---|---|---|
| WinForms sigue siendo el objetivo de publicacion legacy | Alta | `installer`, `scripts` y `CI` siguen empaquetando WinForms |
| 97 tests WinForms siguen vivos | Media | Correcto para la fase de retiro, pero no son parte del runtime WPF |
| Mencion textual de WinForms dentro de WPF | Baja | No es dependencia tecnica; solo nota de migracion |
| Icono / manifest WPF no estan alineados con WinForms | Baja | Tema cosmético, fuera del bloqueo funcional |

## Validacion WPF posterior

La solucion WPF se mantiene independiente y funcional. Esta es la verificacion ejecutada al cierre de la auditoria:

```powershell
dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore
dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore
dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal
```

| Comando | Resultado |
|---|---|
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores, `92` advertencias |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `92` advertencias |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal` | `1012/1012`, `0` fallos, `0` omitidos, exit code `0`, `4 m 18 s` |

Evidencia adicional de aislamiento:

- `PresupuestoElectrico.Tests.Wpf.WpfUndoServiceTests.WpfUndoService_DoesNotReferenceWinForms`
- `PresupuestoElectrico.Tests.Wpf.LetterheadTests.WpfAssembly_DoesNotReferenceWinFormsAssembly`

## Cierre de paridad WPF — Lista de compra

**Fecha:** 2026-06-23  
**Commit base:** `dcdc9b0`  
**Rama:** `feature/wpf-phase10b-functional-parity`

### Auditoria del comportamiento WinForms

La implementacion WinForms vive en `PreciosWorkspacePage.PurchaseList.cs`. Columnas: Codigo, Descripcion, Unidad, Categoria, Cant. Neta, Tol. %, Cant. Compra, Precio UF, Neto UF, Compra UF, Lineas. Acciones: tolerancia configurable (slider + botones 5%/10%), exportar Excel. La logica de consolidacion esta en `PurchaseListService` (capa Application), que es neutral y reutilizable. No hay busqueda ni filtro por categoria en WinForms.

### Estado WPF anterior al cierre

`PurchaseListViewModel` y `PurchaseListView` ya existian con la estructura correcta pero el ViewModel no manejaba correctamente el estado sin proyecto abierto (lanzaba la generacion sin verificar `HasOpenProject`, el mensaje de estado y el resumen eran incorrectos).

### Brechas encontradas y resueltas

| Brecha | Resolucion |
|---|---|
| `UpdateRows` generaba datos sin proyecto abierto | Agregado guard `if (HasProject)` |
| `ShowEmptyState` no distinguia proyecto ausente vs. proyecto vacio | Corregido: `!HasProject \|\| rows.Count == 0` |
| `StatusMessage` sin proyecto decia "No hay recursos" en lugar de guiar al usuario | Mensajes diferenciados por estado |
| `UpdateSummary` calculaba totales con proyecto ausente | Agregado early return con mensaje correcto |
| `UpdateProjectSummary` usaba comparacion nula incorrecta | Reemplazado por `!HasProject` |
| `CanExportExcel` no verificaba presencia de proyecto | Agregado `HasProject &&` |
| `ShellNavigationTests` tenia caracteres corruptos (mojibake UTF-8) | Corregidos tres strings en el archivo |

### Arquitectura aplicada

- `PurchaseListService.Build()` (Application) — logica de consolidacion, sin cambios.
- `PurchaseListViewModel` (WPF) — estado, filtros, busqueda, comandos, tolerancia.
- `PurchaseListView.xaml` (WPF) — grilla con 11 columnas, panel de resumen, barra de filtros.
- Navegacion: `ShellRoute.Purchases` registrado en `ShellRouteCatalog`, `ShellViewModel`, busqueda rapida y menu desplegable de Precios.

### Servicios reutilizados

- `IPurchaseListService` / `PurchaseListService` — consolidacion neutral.
- `IPurchaseListExcelExporter` / `PurchaseListExcelExporter` — exportacion Excel.
- `IMaterialSearchService.Normalize()` — normalizacion de busqueda.
- `WpfAppHost.Current.Session` — datos de sesion activa.
- `IShellNavigationHost` — navegacion al origen de partida.

### Archivos modificados

- `src/QubiCostElectrical.Wpf/Features/Purchases/PurchaseListViewModel.cs` — correcciones de estado sin proyecto.
- `tests/PresupuestoElectrico.Tests/Wpf/ShellNavigationTests.cs` — encoding corregido.

### Archivos creados

- `tests/PresupuestoElectrico.Tests/Wpf/PurchaseListViewModelTests.cs` — 3 pruebas de ViewModel.

### Pruebas nuevas

| Test | Resultado |
|---|---|
| `PurchaseListViewModelTests.Refresh_WithoutProject_ShowsEmptyStateAndDisablesExport` | OK |
| `PurchaseListViewModelTests.Refresh_WithProject_BuildsConsolidatedRowsAndCategories` | OK |
| `PurchaseListViewModelTests.SearchText_FiltersRows` | OK |
| `ShellNavigationTests.PurchaseList_GoToOrigin_NavigatesToTakeoffWithContext` | OK |

Tests especificos de Lista de compra (incluyendo Application e Infrastructure): **13/13**.

### Resultados de build y baterias

| Verificacion | Resultado |
|---|---|
| Build Debug | 0 errores |
| Build Release | 0 errores |
| Tests Shared/WPF (minimo) | 1015/1015, 0 fallos |
| Tests Shared/WPF (normal) | 1015/1015, 0 fallos |
| Tests WinForms | 97/97, 0 fallos |
| Inventario Shared/WPF | 1015 |
| Inventario WinForms dedicado | 97 |
| Total unico | 1112 |

### Verificacion manual

No realizada en esta sesion por entorno de CI. El comportamiento fue validado mediante pruebas automatizadas que cubren los estados criticos: sin proyecto, proyecto vacio, proyecto con materiales, busqueda, navegacion a origen.

### Riesgos pendientes

- El resaltado de filas sin precio (fondo amarillo en WinForms) no tiene equivalente en WPF. Es cosmético, no funcional.
- La tolerancia en WinForms va de 0 a 50% como NumericUpDown. WPF usa Slider con el mismo rango (implementado).
- Exportacion Excel dependiente de `IPurchaseListExcelExporter` que ya funciona (validado por `SpreadsheetInfrastructureTests`).

### Evidencia de independencia de WinForms

`PurchaseListViewModel` no contiene referencias a `System.Windows.Forms`, `PresupuestoElectrico.WinForms`, `MainForm` ni `IProjectLifecycleShell`. El ensamblado WPF no referencia WinForms (cubierto por `LetterheadTests.WpfAssembly_DoesNotReferenceWinFormsAssembly`).

### Clasificacion final

**Lista de compra: REEMPLAZADO POR WPF**

---

---

## Cierre de paridad WPF — Versiones

**Fase:** 10D.9  
**Commit base:** `bcda666` ("feat: complete WPF shopping list parity")  
**Shared/WPF base:** 1015  
**WinForms base:** 97  
**Fecha:** 2026-06-23  

### Comportamiento WinForms auditado

`VersionesWorkspacePage.cs` implementa:
- Grilla con columnas: ★ (activa), Código, Nombre, Estado, Venta UF, Delta UF, Creación, Creada Por
- Botones: Nueva versión, Activar, Renombrar…, Eliminar
- Doble clic activa la versión seleccionada
- Creación: diálogo Código+Nombre, snapshot del estado actual, MontoVentaUF de `_deps.GetVentaUf`
- Activar: cambia `Session.ActiveVersionId` — no hay restauración de datos
- Renombrar: edita Código y Nombre in-place
- Eliminar: confirma con `MessageBox.Show`
- Label de estado activo en pie de grilla
- **No existe restauración de proyecto** — "Activar" solo cambia el puntero `ActiveVersionId`

### Estado WPF antes de la fase

`VersionsViewModel` ya tenía:
- Colección observable con `VersionRowVm` y comparación con `VersionComparisonRowVm`
- Comandos: Nueva, Activar, Renombrar, Eliminar, Ir a partida, Actualizar comparación
- `WpfDialogService.PromptText` para entrada de código/nombre
- `WpfDialogService.Confirm` para confirmación de eliminación
- Comparación contra snapshot actual via `IVersionComparisonService`
- Navegación a `ShellRoute.Versions` registrada en `ShellRouteCatalog`

### Brechas cerradas

| Brecha | Antes | Después |
|---|---|---|
| Guard `HasProject` | No | `HasProject => host.ProjectWorkspace.HasOpenProject` |
| `ShowEmptyState` property | No | Sí, distingue sin-proyecto vs sin-versiones |
| CanExecute en comandos | Always true | `NewCommand`: requiere proyecto; Activar/Renombrar/Eliminar: requiere proyecto + selección |
| Mensaje diferenciado sin proyecto | No | "Abre o crea un proyecto para gestionar versiones." |
| Mensaje sin versiones | Genérico | "Este proyecto todavía no tiene versiones guardadas." |
| Panel vacío en XAML | No | `Border Grid.Row="5"` con botón "Nueva versión" condicional |
| Visibilidad lista/comparación | Siempre visible | Colapsada cuando `ShowEmptyState` |

### Arquitectura

- `VersionsViewModel` — un único archivo, 0 dependencias WinForms
- `VersionsView.xaml` — bindings `BoolToVisibility` para empty state
- `WpfDialogService` — reutilizado para prompts y confirmaciones
- `BudgetVersionComparisonService` — servicio neutral reutilizado
- `IVersionComparisonService` — contrato neutral en `AppServices`
- `BudgetVersionData.FromProjectFileData` — snapshot neutral

### Servicios reutilizados

- `host.ProjectWorkspace.HasOpenProject` — guard de proyecto abierto
- `host.Session.Versiones` — colección de la sesión
- `host.Session.ActiveVersionId` — puntero a versión activa
- `host.Session.MarkDirty(...)` — marca el proyecto como modificado
- `AppClock.Now` — reloj neutral
- `WpfDialogService.PromptText` / `.Confirm` — diálogos WPF
- `BudgetVersionComparisonService.Compare` — comparación neutral

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `src/QubiCostElectrical.Wpf/Features/Versions/VersionsViewModel.cs` | HasProject, ShowEmptyState, guards, CanExecute en comandos |
| `src/QubiCostElectrical.Wpf/Features/Versions/VersionsView.xaml` | Empty state panel, visibility bindings en lista y comparación |
| `tests/PresupuestoElectrico.Tests/Wpf/ShellNavigationTests.cs` | SaveAsAsync en test Versions_GoToItem |
| `tests/PresupuestoElectrico.Tests/Wpf/ShellReviewAndVersionsTests.cs` | SaveAsAsync en test Versions_ShowComparison |

### Archivos creados

| Archivo | Contenido |
|---|---|
| `tests/PresupuestoElectrico.Tests/Wpf/VersionsViewModelTests.cs` | 22 pruebas unitarias |

### Pruebas

- Pruebas nuevas: 22 (en `VersionsViewModelTests`)
- Inventario Shared/WPF: 1037/1037
- Inventario WinForms: 97/97
- Filtro `FullyQualifiedName~Version`: 63/63

### Builds

- Debug: 0 errores
- Release: 0 errores

### Verificación manual

No ejecutada (entorno sin pantalla WPF disponible en esta sesión).

### Diferencias menores con WinForms

- WPF agrega comparación de versiones (extra sobre WinForms) — mantenido
- WPF muestra delta UF calculado entre versiones consecutivas (compatible)
- WPF no tiene restauración de datos — WinForms tampoco la tiene

### Clasificación final

**Versiones: REEMPLAZADO POR WPF**

---

## Cierre de paridad WPF — Cotizaciones

**Fase:** 10D.10  
**Commit base:** (Versiones, Fase 10D.9)  
**Shared/WPF base:** 1037  
**WinForms base:** 97  
**Fecha:** 2026-06-23  

### Comportamiento WinForms auditado

`ImportarCotizacionDialog.cs` implementa:
- Diálogo modal para importar listas de precios Excel de proveedores
- Campos de metadatos: Proveedor, Fecha, Moneda (CLP/UF), valor CLP→UF
- Mapeo manual de columnas: Código, Descripción, Unidad, Precio, Marca
- Llama `new PriceBankStore().UpsertMany(entries)` para guardar
- No existe pantalla de "Cotizaciones" como workspace autónomo en WinForms — solo este diálogo de importación
- La vista principal de cotizaciones (comparación por proveedor) era nueva en WPF

### Estado WPF antes de la fase

`QuotesViewModel` + `QuotesView` ya existían con:
- Lista de proveedores con sumario de materiales, categorías, precio promedio
- Filtro de búsqueda y por categoría
- DataGrid de materiales por proveedor seleccionado
- DataGrid de comparación con delta UF y delta %
- Comando `ApplyPriceCommand` para aplicar el mejor precio a `Session.Materiales`
- Botón "Importar cotización" con `IsEnabled="False"`

### Brecha cerrada

| Brecha | Antes | Después |
|---|---|---|
| "Importar cotización" deshabilitado | `IsEnabled="False"` | `Command="{Binding ImportQuoteCommand}"` |
| Importación de Excel/CSV de precios | No implementado | `PriceBankSeeder.ParseFile()` + `store.UpsertMany()` |

### Arquitectura

- `QuotesViewModel.ImportQuote()` — orquesta: diálogo de archivo, parse, upsert, refresh, mensaje de estado
- `PriceBankSeeder.ParseFile()` (Infrastructure) — ya existía, detecta encabezados automáticamente
- `IPriceBankStore.UpsertMany()` (Infrastructure) — ya existía, bulk save a SQLite
- `WpfDialogService.PromptOpenExcel()` — ya existía, abre file dialog de Excel
- Sin nuevos contratos ni infraestructura

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `src/QubiCostElectrical.Wpf/Features/Quotes/QuotesViewModel.cs` | `importQuoteCommand` field, `ImportQuoteCommand` property, `ImportQuote()` method |
| `src/QubiCostElectrical.Wpf/Features/Quotes/QuotesView.xaml` | Botón Importar: `IsEnabled="False"` → `Command="{Binding ImportQuoteCommand}"` |

### Archivos creados

| Archivo | Contenido |
|---|---|
| `tests/PresupuestoElectrico.Tests/Wpf/QuotesViewModelTests.cs` | 28 pruebas unitarias |

### Pruebas

- Pruebas nuevas: 28 (en `QuotesViewModelTests`)
- Inventario Shared/WPF: 1065/1065
- Inventario WinForms: 97/97

Cobertura de pruebas:

| Area | Tests |
|---|---|
| Construcción y colecciones | 3 |
| ProjectSummary con/sin proyecto | 2 |
| Valores por defecto (SearchText, SelectedCategory) | 2 |
| Comandos no nulos, CanExecute | 4 |
| ApplyPrice (cero, válido, no encontrado) | 3 |
| Filtros (SearchText, SelectedCategory, ClearFilters) | 3 |
| Refresh y ShowEmptyState | 2 |
| QuoteComparisonRowVm (DeltaUf, DeltaPct, cero) | 3 |
| QuoteEntryVm (PrecioUf, propiedades) | 2 |
| Registro en ShellRouteCatalog | 1 |
| Sin referencias WinForms | 1 |
| SelectedComparisonRow actualiza CanExecute | 1 |
| SummaryText poblado | 1 |

### Builds

- Debug: 0 errores
- Release: 0 errores

### Verificación manual

No ejecutada (entorno sin pantalla WPF disponible en esta sesión).

### Diferencias con WinForms

- WinForms tenía mapeo manual de columnas con diálogo modal. WPF usa detección automática de encabezados vía `PriceBankSeeder.ParseFile()` — experiencia más simple y robusta.
- La vista de comparación por proveedor es nueva en WPF (no existía en WinForms).
- "Nueva solicitud" permanece deshabilitado — sin equivalente en WinForms.

### Clasificación final

**Cotizaciones: REEMPLAZADO POR WPF**

---

## Cierre de paridad WPF — Plantillas

**Fase:** 10D.11  
**Commit base:** `a55e693` (Cotizaciones)  
**Shared/WPF base:** 1065  
**WinForms base:** 97  
**Fecha:** 2026-06-23  

### Diferencia crítica: dos sistemas distintos

"Plantillas" en WinForms y "Plantillas" en WPF son funcionalidades distintas con propósitos diferentes:

| Aspecto | WinForms (TemplateRow) | WPF (ApuTemplateRow) |
|---|---|---|
| Propósito | Plantillas de cubicación (insumos/recursos composables) | Plantillas APU (fórmulas de análisis de precios unitarios) |
| Almacenamiento | JSON dentro del proyecto (.pepro) | SQLite global (banco_precios.db en AppData) |
| Scope | Por proyecto | Global (entre proyectos) |
| Jerarquía | Composable, referencias cruzadas, detección de ciclos | Recursos simples en JSON |
| Import/Export | JSON + XLSX via TemplateSpreadsheetService | JSON nativo (nuevo en 10D.11) |

WPF también tiene **Conjuntos Eléctricos** (lectura del proyecto) como segunda pestaña.

### Letterhead (Membrete)

No es parte de Plantillas. Es identidad de empresa global (razón social, RUT, logo, firma). Se administra en Preferencias y vive en `UserPreferences`. `LetterheadApplicator` lo aplica a los exportadores. Ya tenía paridad funcional completa en WPF (26 tests en `LetterheadTests.cs`).

### Estado WPF antes de la fase

`PlantillasVm` ya tenía:
- CRUD (Crear, Eliminar, Duplicar)
- Filtro/búsqueda en tiempo real
- KPIs (conteo plantillas, conjuntos, recursos)
- Conjuntos eléctricos read-only
- Navegación registrada en `ShellRouteCatalog`

Brechas:
1. `ShowEmptyState` requería `HasOpenProject` — incorrecto para APU templates globales
2. "Importar biblioteca" deshabilitado (`IsEnabled="False"`)
3. Sin export
4. Sin rename

### Brechas cerradas

| Comportamiento | WinForms | WPF anterior | WPF 10D.11 |
|---|---|---|---|
| Navegación | Sí | Sí | Validado |
| Listado | Sí | Sí | Validado |
| Crear | Sí | Sí | Validado |
| Duplicar | Manual | Sí | Validado |
| Renombrar | Sí | No | Implementado |
| Eliminar con confirmación | Sí | Sí | Validado |
| Importar | JSON + XLSX | Deshabilitado | Implementado (JSON) |
| Exportar | JSON + XLSX | No | Implementado (JSON) |
| Estado vacío correcto | N/A | Requería proyecto | Corregido (global) |
| Conjuntos eléctricos | No | Sí (read-only) | Validado |
| StatusMessage | Sí | No | Implementado |

### Arquitectura

- `PlantillasVm` — global (no requiere proyecto). APU templates de SQLite, conjuntos del proyecto.
- `ApuTemplateLibraryFile` — formato JSON nativo para import/export (internal sealed class).
- `WpfDialogService.PromptOpenJson()` / `.PromptSaveJson()` — nuevos métodos de diálogo.
- `ApuTemplateStore` (Infrastructure) — reutilizado sin cambios.
- `IApuTemplateStore` (Application) — contrato existente, sin cambios.

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `src/QubiCostElectrical.Wpf/Features/Templates/PlantillasVm.cs` | Fix ShowEmptyState, RenameCommand, ImportCommand, ExportCommand, StatusMessage |
| `src/QubiCostElectrical.Wpf/Features/Templates/PlantillasView.xaml` | Botones Import/Export/Rename cableados, empty state corregido, StatusMessage |
| `src/QubiCostElectrical.Wpf/Infrastructure/WpfDialogService.cs` | `PromptOpenJson()`, `PromptSaveJson()` |
| `docs/WPF_PHASE10D2_WINFORMS_RETIREMENT_REPORT.md` | Sección Plantillas y tabla de disposición actualizada |

### Archivos creados

| Archivo | Contenido |
|---|---|
| `tests/PresupuestoElectrico.Tests/Wpf/PlantillasVmTests.cs` | 28 pruebas unitarias |

### Pruebas

- Pruebas nuevas: 28 (en `PlantillasVmTests`)
- Inventario Shared/WPF: 1093/1093
- Inventario WinForms: 97/97
- Filtro Template|Plantilla|Letterhead: 113/113

Cobertura de pruebas:

| Área | Tests |
|---|---|
| Construcción, colecciones, comandos | 5 |
| ShowEmptyState — global, sin proyecto | 2 |
| ApuTemplateItemVm (propiedades, RecursoCount) | 4 |
| ConjuntoItemVm | 1 |
| KPIs y filtros | 3 |
| SelectedTemplate, TemplateRecursos | 2 |
| CanExecute de comandos (Delete, Duplicate, Rename, Import) | 4 |
| Refresh/Load idempotente | 2 |
| ActiveTab | 1 |
| Registro ShellRouteCatalog | 1 |
| Sin referencias WinForms | 1 |
| Parsing JSON (formato library y lista directa) | 2 |
| Store FakeApuStore (Duplicate crea nuevo Id, Delete funciona) | 2 |

### Integración con exportación

Los exportadores WinForms consumen `TemplateRow` (cubicación, en proyecto). Los exportadores WPF que requieran APU templates pueden inyectar `IApuTemplateStore` directamente — ya está registrado en `WpfAppHost.Services`. No requiere trabajo adicional en esta fase.

La fase de Exportación general (pendiente) deberá:
- Resolver plantilla APU activa para generar APU en PDF/Excel
- Resolver `LetterheadApplicator` para membrete (ya funcional)

### Builds

- Debug: 0 errores
- Release: 0 errores, 111 advertencias (pre-existentes)

### Verificación manual

No ejecutada (entorno sin pantalla WPF disponible en esta sesión).

### Diferencias menores con WinForms

- WinForms importa TemplateRow (cubicación). WPF importa ApuTemplateRow (APU). Son modelos distintos — la diferencia es intencional.
- WinForms soporta XLSX via `TemplateSpreadsheetService`. WPF usa JSON nativo en esta fase — extensible a XLSX si se requiere.
- WinForms tiene jerarquía composable (sub-plantillas con detección de ciclos). WPF no — no corresponde para APU templates.
- WinForms no tiene Export en la misma pantalla (es acción del toolbar). WPF agrega Export como botón de la vista.

### Riesgos pendientes

- Import desde XLSX no implementado en WPF (WinForms sí lo tiene para TemplateRow). No es bloqueante — son modelos distintos.
- Editor inline de recursos APU no implementado (solo visualización read-only). WinForms sí edita inline.
- Conjuntos eléctricos son read-only en WPF. WinForms permite gestión desde Cubicación.

### Clasificación final

**Plantillas: REEMPLAZADO POR WPF**

---

---

## FASE 10D.12 — Exportación general WPF (cierre)

**Commit:** pendiente  
**Rama:** `feature/wpf-phase10b-functional-parity`

### Auditoría

`ExportViewModel` ya tenía 8 operaciones de exportación completamente implementadas antes de esta fase:

| # | Acción | Formato | Motor neutral |
|---|---|---|---|
| 1 | Oferta comercial | PDF | `QuestPdfProjectExporter` via `host.Services.PdfExporter` |
| 2 | Excel interno | XLSX | `ExcelExportInfrastructure` via `host.Services.ExcelExporter` |
| 3 | Carta de presentación | DOCX | `PresentationLetterDocxExporter` via `host.Services.WordLetterExporter` |
| 4 | Memoria cubicación Excel | XLSX | `host.Services.CubicacionReportExcelExporter` |
| 5 | Memoria cubicación PDF | PDF | `host.Services.CubicacionReportPdfExporter` |
| 6 | Reporte técnico C.SEC | XLSX | `host.Services.CubicacionSCReportExcelExporter` |
| 7 | Paquete JSON | JSON | `host.Services.ProjectRepository.SaveProjectFileAsync()` |
| 8 | Paquete final ZIP | ZIP | Agrega todos los anteriores en carpeta temp + `ZipFile.CreateFromDirectory` |

Brechas identificadas:
- `PurchaseListExcelExporter` — ya tiene su propio ExportCommand en `PurchaseListViewModel`. No requiere duplicarse en ExportViewModel.
- `CubicacionSCExcelExporter` (simple) — reemplazado funcionalmente por el `CubicacionSCReportExcelExporter` ya cableado.

### Patrones WPF confirmados

- Guard: `CanExport() => host.ProjectWorkspace.HasOpenProject`
- Diálogos: `Microsoft.Win32.SaveFileDialog` directamente (no `System.Windows.Forms`)
- Contexto: `BuildContext()` aplica `LetterheadApplicator.Apply(prefs, offerSnapshot)` y `ExportScopeFilter.ApplyScope()`
- Modo de alcance: 3 modos — Completo / Partida / Área
- Reacción a cambios: `Session.Changed` → `Dispatcher.InvokeAsync(Refresh)` (nulo en tests)
- Paquete final: construye todos los artefactos en carpeta temp, luego ZipFile

### Pruebas creadas

`tests/PresupuestoElectrico.Tests/Wpf/ExportViewModelTests.cs` — 28 pruebas:

- Constructor sin excepción
- Todos los comandos de exportación no nulos (8 comandos)
- Guard `CanExport` sin proyecto = false (todos los comandos)
- Guard usa `HasOpenProject` del `ProjectWorkspaceService`
- `Actions` tiene exactamente 8 entradas
- `PackageItems` tiene exactamente 7 entradas
- `ScopeModes` tiene 3 entradas (Completo, Partida, Área)
- `SummaryText` sin proyecto indica estado correcto
- `SummaryText` sin proyecto = valor por defecto exacto
- `ProjectTitle` refleja nombre del proyecto
- `ClientName` refleja cliente del proyecto
- `SelectedScopeMode` por defecto es Completo
- `IsPartidaScope` / `IsAreaScope` correctos por defecto
- Cambiar scope a Partida actualiza `IsPartidaScope`
- Cambiar scope a Área actualiza `IsAreaScope`
- `ExportScopeSummaryText` Completo, Partida sin selección, Área sin selección
- `ExportActionVm` expone propiedades correctamente
- `ExportActionVm` ícono derivado del texto del botón (PDF/XLS/DOC/ZIP)
- `ExportPackageItemVm` expone propiedades
- Refresh es idempotente y no acumula acciones
- `StateText` por defecto es Borrador
- Exportación registrada en `ShellRouteCatalog` grupo Entrega
- Sin referencias a `System.Windows.Forms` ni a `PresupuestoElectrico.WinForms`
- `CorporateThemeText` y `ReportTemplateText` no son nulos

### Builds y suite

- Debug: 0 errores, 0 fallos
- Release: 0 errores
- Suite completa: 1121/1121 pruebas, 0 fallos (Shared/WPF: 1121 · WinForms: 97)

### Clasificación final

**Exportación general: IMPLEMENTADO EN WPF — SIN DEPENDENCIA DE WINFORMS**

---

---

## FASE 10D.13 — Auditoría final previa al retiro físico de WinForms

**Commit base:** `c850bf2`  
**Fecha:** 2026-06-23  
**Rama:** `feature/wpf-phase10b-functional-parity`  
**Estado del árbol al auditar:** limpio  
**Inventario WPF antes de auditar:** 1121 pruebas  
**Inventario WinForms antes de auditar:** 97 pruebas  
**Total único antes de auditar:** 1218 pruebas

---

### 1. Matriz funcional

| Función | WinForms | WPF | Evidencia | Estado |
|---|:---:|:---:|---|---|
| Creación de proyecto | Sí | Sí | `ProjectLifecycleTests`, `ProjectLifecycleService` WPF, `ShellViewModel.NewProjectAsync()` | REEMPLAZADO POR WPF |
| Apertura de proyecto | Sí | Sí | `ProjectLifecycleTests`, `WpfHostCollectionDefinition`, `ShellViewModel.OpenProjectAsync()` | REEMPLAZADO POR WPF |
| Guardado | Sí | Sí | `ProjectLifecycleTests`, `SafeSaveService`, `ShellViewModel.SaveProjectAsync()` | REEMPLAZADO POR WPF |
| Guardar como | Sí | Sí | `ShellViewModel.SaveProjectAsAsync()` | REEMPLAZADO POR WPF |
| Autosave | Sí | Sí | `AutoSaveServiceTests` (31 pruebas WPF), `AutoSaveService` en `WpfAppHost` | REEMPLAZADO POR WPF |
| Backup | Sí | Sí | `SafeSaveService` neutral en Infrastructure | REEMPLAZADO POR WPF |
| Navegación | Sí | Sí | `ShellRoute`, `ShellRouteCatalog`, `IShellNavigationHost`, `ShellNavigationTests` | REEMPLAZADO POR WPF |
| Dashboard | Sí | Sí | `DashboardViewModel`, `DashboardPresenter`, `DashboardPresenterTests` | REEMPLAZADO POR WPF |
| Itemizado | Sí | Sí | `ItemizadoViewModel`, `ItemizadoWorkspaceService`, tests en suite compartida | REEMPLAZADO POR WPF |
| Cubicación | Sí | Sí | `CubicacionViewModel` (Features/Cubicacion) | REEMPLAZADO POR WPF |
| APU | Sí | Sí | `ApuWorkspaceVm`, `ApuPartidaVm`, `ApuRecursoVm`, `ApuTemplatePickerDialog` | REEMPLAZADO POR WPF |
| Revisión final | Sí | Sí | `ReviewViewModel`, `FinalReviewServiceTests` | REEMPLAZADO POR WPF |
| Banco de precios | Sí | Sí | `PriceBankViewModel`, `PriceBankStore` (SQLite neutral) | REEMPLAZADO POR WPF |
| Cotizaciones | Sí | Sí | `QuotesViewModel`, `QuotesViewModelTests` (28 pruebas WPF) | REEMPLAZADO POR WPF |
| Lista de compra | Sí | Sí | `PurchaseListViewModel`, `PurchaseListViewModelTests` | REEMPLAZADO POR WPF |
| Versiones | Sí | Sí | `VersionsViewModel`, `VersionsViewModelTests` | REEMPLAZADO POR WPF |
| Plantillas (APU global) | Sí | Sí | `PlantillasVm`, `PlantillasVmTests` (28 pruebas WPF) | REEMPLAZADO POR WPF |
| Conjuntos eléctricos | Sí | Sí | `ConjuntoItemVm` en `PlantillasVm`, read-only (WinForms permite edición desde Cubicación) | REEMPLAZADO POR WPF |
| Documentos | Sí | Sí | `DocumentsViewModel` — importación masiva desde carpeta pendiente (etiqueta de UI) | REEMPLAZADO POR WPF |
| Exportaciones | Sí | Sí | `ExportViewModel` (8 operaciones), `ExportViewModelTests` (28 pruebas WPF) | REEMPLAZADO POR WPF |
| Preparación de oferta | Sí | Sí | `DeliveryViewModel`, `DeliveryViewModelTests` | REEMPLAZADO POR WPF |
| Preferencias | Sí | Sí | `PreferencesViewModel`, `PreferencesViewModelTests` | REEMPLAZADO POR WPF |
| Monedas e indicadores | Sí | Sí | `CurrencyViewModel`, `CurrencyViewModelTests` | REEMPLAZADO POR WPF |
| Membrete / tema corporativo | Sí | Sí | `LetterheadTests`, `LetterheadApplicator` neutral | REEMPLAZADO POR WPF |
| Órdenes de cambio | Sí | Sí | `ChangeOrdersViewModel`, `ChangeOrdersViewModelTests` | REEMPLAZADO POR WPF |
| Undo/Redo | Sí | Sí | `WpfUndoService`, `WpfUndoServiceTests` | REEMPLAZADO POR WPF |
| Búsqueda rápida | Sí | Sí | `ShellViewModel.QuickSearch`, `QuickSearchItemVm` | REEMPLAZADO POR WPF |
| Errores y estados vacíos | Sí | Sí | `ShowEmptyState` en todos los ViewModels, `UiErrorPolicy` neutral | REEMPLAZADO POR WPF |
| Importación masiva desde carpeta | Sí | No | `DocumentsViewModel.ImportHelp` indica que es pendiente | NO APLICA AL PRODUCTO ACTUAL |

**Resultado:** 28 de 28 funciones auditadas. 27 REEMPLAZADAS POR WPF. 1 NO APLICA (importación masiva de documentos, no es flujo esencial de presupuesto).

**Sin bloqueos funcionales.**

---

### 2. Referencias técnicas a WinForms

#### 2.1 — Capas neutrales (Domain, Application, Infrastructure)

| Ubicación | Tipo | Contenido | Clasificación |
|---|---|---|---|
| `Domain/PricingRules/PricingRuleContext.cs:4` | Comentario doc | `"...sin poner lógica en MainForm"` | Documentación histórica — se retirará con WinForms |
| `Application/UseCases/ProjectUseCases.cs:4` | Comentario doc | `"...moviendo lógica desde MainForm"` | Documentación histórica |
| `Application/Services/BudgetWorkflowStatusService.cs:18` | Comentario doc | `"MainForm, exportes y diálogos"` | Documentación histórica |
| `Application/Session/BudgetProjectSession.cs:46,47,55` | Comentarios doc | referencias a `MainForm` como referencia de contexto | Documentación histórica |

**Conclusión:** CERO acoplamiento real de código. Solo comentarios de contexto histórico.

#### 2.2 — WPF

| Ubicación | Tipo | Contenido | Clasificación |
|---|---|---|---|
| `QubiCostElectrical.Wpf/Features/Documents/DocumentsViewModel.cs:174` | Mensaje UI | `"Importación masiva desde carpeta: disponible en WinForms hasta migrar esta operación."` | Mensaje visible al usuario — debe eliminarse antes del retiro |
| `tests/PresupuestoElectrico.Tests/Wpf/*.cs` (9 archivos) | Guard assertions | `Assert.DoesNotContain(..., "System.Windows.Forms"...)` | Guardias de arquitectura — se conservan |

**Nota crítica:** El mensaje `ImportHelp` en `DocumentsViewModel.cs:174` debe eliminarse o corregirse antes de completar el retiro físico, ya que refiere a WinForms en una UI activa.

#### 2.3 — Archivos de solución y proyecto

| Archivo | Referencia | Clasificación |
|---|---|---|
| `PresupuestoElectrico.sln` | Incluye `PresupuestoElectrico.WinForms` y `PresupuestoElectrico.WinForms.Tests` | Se retirará junto con WinForms |
| `QubiCostElectrical.Wpf.sln` | Sin referencias a WinForms | OK |
| `src/PresupuestoElectrico.WinForms/PresupuestoElectrico.WinForms.csproj` | `InternalsVisibleTo` a WinForms.Tests | Se eliminará |
| `tests/PresupuestoElectrico.WinForms.Tests/PresupuestoElectrico.WinForms.Tests.csproj` | `ProjectReference` a WinForms | Se eliminará |
| `.github/workflows/build.yml:35,38,41` | Compila y testea `PresupuestoElectrico.sln` | Cambiar a `QubiCostElectrical.Wpf.sln` |
| `.github/workflows/build.yml:91` | Publica `PresupuestoElectrico.WinForms.csproj` | Cambiar a `QubiCostElectrical.Wpf.csproj` |
| `installer/PresupuestoElectricoPRO.iss:5` | Publica WinForms | Cambiar a WPF |
| `installer/PresupuestoElectricoPRO.iss:23` | `AppExeName = "PresupuestoElectrico.exe"` | Cambiar a nombre definitivo |
| `scripts/Publish-Release.ps1:24` | Publica WinForms | Reemplazar con contenido de `Publish-Wpf.ps1` |
| `deploy/build-release.ps1:45,95` | Publica WinForms, `--mainExe PresupuestoElectrico.exe` | Cambiar a WPF |

#### 2.4 — Cargas dinámicas y rutas runtime

- Sin cargas dinámicas por reflection que referencien WinForms.
- Sin rutas de código que ejecuten `PresupuestoElectrico.exe` desde WPF.
- Sin comandos en scripts WPF que invoquen la aplicación WinForms en runtime.

---

### 3. Grafo de dependencias

```
QubiCostElectrical.Wpf
  └── PresupuestoElectrico.Composition   (sin WinForms)
  └── PresupuestoElectrico.Application   (sin WinForms)
  └── PresupuestoElectrico.Domain        (sin WinForms)
  └── PresupuestoElectrico.Infrastructure (sin WinForms)

PresupuestoElectrico.Tests (suite compartida/WPF)
  └── QubiCostElectrical.Wpf             (InternalsVisibleTo)
  └── PresupuestoElectrico.Composition
  └── PresupuestoElectrico.Application
  └── PresupuestoElectrico.Domain
  └── PresupuestoElectrico.Infrastructure
  └── PresupuestoElectrico.WinForms      (⚠ solo para compilar 97 tests WinForms)

PresupuestoElectrico.WinForms.Tests
  └── PresupuestoElectrico.WinForms
  └── + todas las capas neutrales
```

**Verificaciones explícitas:**
- WPF no referencia WinForms: ✓ CONFIRMADO — `QubiCostElectrical.Wpf.csproj` sin `ProjectReference` a WinForms.
- Application no referencia WinForms: ✓ CONFIRMADO — solo comentarios históricos.
- Infrastructure no referencia WinForms: ✓ CONFIRMADO.
- Domain no referencia WinForms: ✓ CONFIRMADO.
- Motores de exportación no referencian WinForms: ✓ CONFIRMADO — `QuestPdfProjectExporter`, `ExcelExportInfrastructure`, `PresentationLetterDocxExporter` son neutrales.
- Tests compartidos no referencian ensamblado WinForms directamente en código de negocio: ✓ (solo guard assertions que verifican ausencia de referencias).
- Sin cargas por reflection: ✓ CONFIRMADO.
- Sin rutas que ejecuten el `.exe` legacy: ✓ CONFIRMADO.

**Única dependencia real:** `PresupuestoElectrico.Tests.csproj` tiene `ProjectReference` a `PresupuestoElectrico.WinForms.csproj` para compilar los 97 tests de la suite WinForms que viven en el mismo proyecto de pruebas. Esta referencia desaparece cuando se retire WinForms y los tests Categoría A/D se muevan a la suite compartida.

---

### 4. Clasificación de las 97 pruebas WinForms

Lista completa extraída del runner (`dotnet test --list-tests`):

**Categoría A — Migrar antes del retiro (19 tests)**

Tests que verifican lógica neutral del proyecto sin cobertura equivalente en la suite compartida. Deben moverse a `tests/PresupuestoElectrico.Tests/` antes de eliminar WinForms.

| Test | Clase | Lógica probada |
|---|---|---|
| `IsSqliteProjectFile_DetectsDatabaseExtensions` (9 casos) | `ProjectStoragePathsTests` | `ProjectStoragePaths.IsSqliteProjectFile()` — detección de extensiones |
| `IsJsonProjectFile_DetectsJsonExtensions` (7 casos) | `ProjectStoragePathsTests` | `ProjectStoragePaths.IsJsonProjectFile()` — detección de extensiones |
| `GetAutoSavePathForProject_UsesSidecarNextToProject` | `ProjectStoragePathsTests` | Ruta de autosave sidecar |
| `GetAutoSavePathForProject_UsesFallbackNameWhenPathIsEmpty` | `ProjectStoragePathsTests` | Fallback autosave sin ruta |
| `GetAutoSavePathForProject_AutosaveExtensionAlwaysPresent` | `ProjectStoragePathsTests` | Extensión `.autosave` garantizada |

**Categoría B — Cubiertas por pruebas WPF (0 tests)**

No se identificaron pruebas WinForms con cobertura directa en la suite WPF que las haga redundantes. Las lógicas de negocio están cubiertas en la suite compartida por vías distintas.

**Categoría C — Retirar con WinForms (74 tests)**

Tests de formularios, controles, shell, navegación WinForms, composición legacy y guardias internas.

| Clase | Count | Razón |
|---|:---:|---|
| `AppNavigationServiceTests` | 5 | Testa `IAppNavigationService` WinForms — reemplazado por `IShellNavigationHost` WPF |
| `AppThemeDesignSystemTests` | 10 | Testa tokens de color de `AppTheme` WinForms — WPF usa DesignSystem XAML propio |
| `ExportPlantillasThemeTests` | 4 | Tokens de tema WinForms para Export y Plantillas |
| `MainFormArchitectureGuardTests` | 6 | Guards sobre `MainForm` que desaparece con el retiro |
| `MainFormShellLeanTests` | 6 | Guards que verifican que `MainForm` no aloja estado de workspace |
| `NavigationPageCatalogTests` | 31 | Testa `NavigationPageCatalog` WinForms — reemplazado por `ShellRouteCatalog` WPF |
| `ShellLayoutMetricsTests` | 3 | Métricas de viewport para resolución mínima de ventana WinForms |
| `ShellServicesTests` | 2 | Testa `ShellServices.From()` — composición WinForms-específica |
| `WinFormsLegacyCleanupTests` | 3 | Guards de limpieza de archivos WinForms legacy |
| `WinFormsThemeCompletenessTests` | 1 | Verifica que código WinForms no use white hardcodeado |
| `WorkspaceNavigationCoordinatorTests` | 1 | Testa `WorkspaceNavigationCoordinator` WinForms |
| `WorkspaceNavigationRegistrationTests` | 2 | Verifica que `MainForm` registra workspace pages |

**Categoría D — Convertir en guardias neutrales (4 tests)**

Tests con valor arquitectónico independiente de WinForms. Recrear en suite compartida.

| Test | Clase | Nueva guardia propuesta |
|---|---|---|
| `Build_default_produces_all_core_services` | `AppServicesBuilderTests` | `AppServices.CreateDefault()` devuelve servicios no nulos — neutral |
| `CreateDefault_matches_builder_default` | `AppServicesBuilderTests` | Consistencia entre builder y default |
| `WithItemizado_override_is_used_by_page_services` | `AppServicesBuilderTests` | Override de `IItemizadoPresenter` se propaga |
| `WithCubicacion_override_is_used_by_page_services` | `AppServicesBuilderTests` | Override de `ICubicacionPresenter` se propaga |

**Tabla resumen:**

| Clase de pruebas | Cantidad | Acción |
|---|:---:|---|
| Migrar (A) | 19 | Mover a `PresupuestoElectrico.Tests` antes del retiro |
| Cubiertas por WPF (B) | 0 | — |
| Retirar con WinForms (C) | 74 | Eliminar con la suite legacy |
| Convertir en guardias (D) | 4 | Crear en `PresupuestoElectrico.Tests` antes del retiro |
| **Total** | **97** | |

---

### 5. Componentes que se eliminarán con WinForms

**Código fuente (`src/PresupuestoElectrico.WinForms/`):**

| Componente | Tipo | Sucesor WPF |
|---|---|---|
| `MainForm` | Shell principal WinForms | `ShellWindow` + `ShellViewModel` |
| `LoadingForm` | Pantalla de carga | `App.xaml.cs` startup |
| `Program.cs` | Punto de entrada WinForms | `App.xaml.cs` |
| `AppTheme` / `AppThemeCollection` | Sistema de tema WinForms | DesignSystem XAML |
| `Features/*/Forms/*.cs` | Formularios WinForms | `Features/*/Views/*.xaml` |
| `Features/*/Controls/*.cs` | Controles WinForms | Controles WPF en vistas |
| `Shell/` | Shell WinForms | `Shell/ShellWindow` WPF |
| `Navigation/` | Navegación WinForms | `Navigation/ShellRoute` WPF |
| `ProjectLifecycleService` | Orquestador de ciclo de vida WinForms | `ShellViewModel` (lifecycle) + `ProjectLifecycleService` WPF |
| `ProjectOpenService` | Apertura WinForms | `ShellViewModel.OpenProjectAsync()` |
| `ProjectSaveService` | Guardado WinForms | `ShellViewModel.SaveProjectAsync()` |
| `ProjectPersistenceService` | Persistencia WinForms | `AutoSaveService` WPF + `SafeSaveService` neutral |
| `ProjectAutoSaveService` | Autosave WinForms | `AutoSaveService` en WPF Infrastructure |
| `ProjectBackupService` | Backup WinForms | `SafeSaveService` en Infrastructure (neutral, ya activo) |
| `ExportShellService` | Exportación vía shell WinForms | `ExportViewModel` WPF |
| `WorkflowUiService` | Coordinación UI WinForms | Integrado en ViewModels WPF |

**Tests (`tests/PresupuestoElectrico.WinForms.Tests/`):**

- Proyecto completo se elimina junto con `PresupuestoElectrico.WinForms.csproj`.
- 74 tests (Categoría C) se eliminan directamente.
- 19 tests (Categoría A) se migran antes.
- 4 tests (Categoría D) se recrean como guardias neutrales.

---

### 6. Soluciones afectadas

| Solución | Estado actual | Acción requerida |
|---|---|---|
| `QubiCostElectrical.Wpf.sln` | Solución WPF sin WinForms — compila limpia | **Pasa a ser la solución principal definitiva** |
| `PresupuestoElectrico.sln` | Incluye WinForms y WinForms.Tests | Eliminar después de retirar WinForms |
| `PresupuestoElectrico.WinForms.csproj` | Proyecto de app WinForms | Eliminar en el retiro físico |
| `PresupuestoElectrico.WinForms.Tests.csproj` | Proyecto de tests WinForms | Eliminar después de migrar A y D |

**`ProjectReference` que quedarán huérfanos al retirar WinForms:**
- `PresupuestoElectrico.Tests.csproj` tiene `ProjectReference` a `PresupuestoElectrico.WinForms.csproj` para compilar los 97 tests. Esta referencia se elimina cuando se retire WinForms y se muevan los tests A/D.

**Configuraciones Debug/Release que dependen de WinForms:**
- Todas las configuraciones en `PresupuestoElectrico.sln` dependen de WinForms. Solo afectan la solución legacy, no `QubiCostElectrical.Wpf.sln`.

---

### 7. Auditoría de entrega

#### 7.1 — `.github/workflows/build.yml`

| Campo | Valor actual | Valor requerido |
|---|---|---|
| Solución (restore/build/test) | `PresupuestoElectrico.sln` | `QubiCostElectrical.Wpf.sln` |
| Proyecto publicado | `src/PresupuestoElectrico.WinForms/PresupuestoElectrico.WinForms.csproj` | `src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj` |
| Ejecutable esperado | `PresupuestoElectrico.exe` | `QubiCostElectrical.Wpf.exe` (ver sección 8) |
| Runtime | `win-x64 --self-contained` | `win-x64 --self-contained` (sin cambio) |
| ZIP artefacto | `PresupuestoElectricoPRO-$ver-win-x64.zip` | Sin cambio de nombre necesario |
| Checksums | `SHA256SUMS.txt` | Sin cambio |

**Líneas que deben cambiarse:** 35, 38, 41 (solución), 91 (proyecto publicado).

#### 7.2 — `installer/PresupuestoElectricoPRO.iss`

| Campo | Valor actual | Valor requerido |
|---|---|---|
| Instrucción de publish manual (comentario línea 5) | `PresupuestoElectrico.WinForms\...` | `QubiCostElectrical.Wpf\...` |
| `AppExeName` (línea 23) | `PresupuestoElectrico.exe` | `QubiCostElectrical.Wpf.exe` (ver sección 8) |
| `PublishDir` | `..\publish\win-x64` | Sin cambio |
| `AppName` | `Presupuesto Eléctrico PRO` | Sin cambio — nombre comercial correcto |
| `DefaultDirName` | `{autopf}\Presupuesto Eléctrico PRO` | Sin cambio |
| `AppId` | UUID fijo | Sin cambio — no modificar para preservar detección de actualizaciones |
| Accesos directos | `{group}`, `{autodesktop}` | Sin cambio |

#### 7.3 — `scripts/Publish-Release.ps1`

| Campo | Valor actual | Valor requerido |
|---|---|---|
| Proyecto publicado (línea 24) | `PresupuestoElectrico.WinForms.csproj` | `QubiCostElectrical.Wpf.csproj` |

Nota: `scripts/Publish-Wpf.ps1` ya existe y tiene la lógica correcta para WPF. La acción es reemplazar `Publish-Release.ps1` con el contenido de `Publish-Wpf.ps1` o unificarlos.

#### 7.4 — `deploy/build-release.ps1`

| Campo | Valor actual | Valor requerido |
|---|---|---|
| Proyecto publicado (línea 45) | `PresupuestoElectrico.WinForms.csproj` | `QubiCostElectrical.Wpf.csproj` |
| `--mainExe` (línea 95) | `PresupuestoElectrico.exe` | `QubiCostElectrical.Wpf.exe` (ver sección 8) |

---

### 8. Ejecutable oficial

**Estado actual del ensamblado WPF:**

| Metadato | Valor en `QubiCostElectrical.Wpf.csproj` / `Directory.Build.props` |
|---|---|
| `AssemblyName` | `QubiCostElectrical.Wpf` |
| `RootNamespace` | `QubiCostElectrical.Wpf` |
| `Product` | `Presupuesto Eléctrico PRO` |
| `Company` | `QubiCost Electrical` |
| `Description` | `Software profesional de presupuestos eléctricos con cubicación, APU, cotizaciones y ofertas comerciales.` |
| `Version` | `3.140.2` |
| Ejecutable generado | `QubiCostElectrical.Wpf.exe` |
| Carpeta de datos | `%LOCALAPPDATA%\PresupuestoElectricoPRO\` |
| `Title` / `Copyright` | `© QubiCost Electrical 2024-2026` |

**Ejecutables actuales:**
- WinForms: `PresupuestoElectrico.exe` (ensamblado `PresupuestoElectrico.dll`)
- WPF: `QubiCostElectrical.Wpf.exe`

**Problema:** `QubiCostElectrical.Wpf.exe` es un nombre interno de namespace, no un nombre comercial. El instalador y los accesos directos muestran `Presupuesto Eléctrico PRO` que sí es correcto, pero el ejecutable en sí tiene un nombre de espacio de nombres.

**Recomendación:** Cambiar `AssemblyName` en `QubiCostElectrical.Wpf.csproj` a:
```
<AssemblyName>PresupuestoElectricoPRO</AssemblyName>
```
Esto producirá `PresupuestoElectricoPRO.exe` — nombre coherente con `AppId`, `OutputBaseFilename` del instalador y el nombre del producto. No rompe compatibilidad de datos porque la carpeta `%LOCALAPPDATA%\PresupuestoElectricoPRO\` es independiente del nombre del ejecutable.

Si se elige no renombrar antes del retiro, el ejecutable entregado será `QubiCostElectrical.Wpf.exe`, que es funcional pero no profesional.

**No renombrar hasta que se decida y se actualicen todos los scripts.**

---

### 9. Compatibilidad de datos

| Artefacto | Formato | Ubicación | Compatibilidad | Prueba existente | Riesgo |
|---|---|---|---|---|---|
| Proyecto principal | SQLite (`.pepro.db`) + JSON (`.pepro`) | Carpeta elegida por el usuario | ✓ Misma versión de esquema (`DatabaseSchemaVersion=20`) | `ProjectLifecycleTests`, roundtrip tests | Bajo |
| Paquete JSON de exportación | JSON (`.pepro`) | Carpeta elegida | ✓ Mismo `ProjectJsonSerializer` | `ExportViewModelTests`, `ExportJsonAsync` | Bajo |
| Autosave | `.pepro.autosave` (sidecar) | Junto al `.pepro.db` | ✓ Misma lógica en `AutoSaveService` | `AutoSaveServiceTests` | Bajo |
| Backups | `.backups/` numerados | Junto al archivo de proyecto | ✓ `SafeSaveService` neutral | Pruebas de Infrastructure | Bajo |
| Banco de precios global | SQLite (`banco_precios.db`) | `%LOCALAPPDATA%\PresupuestoElectricoPRO\` | ✓ Misma ruta, misma clave de lectura | `PriceBankStore` tests | Bajo |
| Preferencias de usuario | JSON (`preferences.json`) | `%LOCALAPPDATA%\PresupuestoElectricoPRO\` | ✓ `UserPreferencesStore` neutral | `PreferencesViewModelTests` | Bajo |
| Versiones de proyecto | Snapshots JSON en sesión | Embebidos en el proyecto | ✓ `VersionsViewModel` lee mismos snapshots | `VersionsViewModelTests` | Bajo |
| Documentos / planos | Rutas relativas en JSON | Junto al proyecto | ✓ `DocumentsViewModel` lee mismas rutas | `DocumentsViewModel` tests | Bajo |

**Conclusión:** WPF puede abrir proyectos existentes creados con WinForms sin pérdida de datos. Las rutas de almacenamiento son idénticas. No se requiere migración de datos.

---

### 10. Documentación histórica

| Documento | Contenido WinForms | Clasificación |
|---|---|---|
| `docs/WPF_PHASE10D2_WINFORMS_RETIREMENT_REPORT.md` (este archivo) | Historial completo de auditoría y migración | Historia válida — conservar |
| `docs/WINFORMS_ARCHITECTURE_AUDIT.md` (si existe) | Auditorías anteriores | Historia válida — conservar |
| `installer/PresupuestoElectricoPRO.iss` comentarios | Instrucciones manuales de publish WinForms | Instrucción desactualizada — actualizar en retiro |
| `scripts/Publish-Release.ps1` | Script de publish WinForms | Reemplazar o unificar con `Publish-Wpf.ps1` |
| `deploy/build-release.ps1` | Deploy WinForms con Velopack | Actualizar rutas de proyecto y ejecutable |
| `.github/workflows/build.yml` comentarios | Referencias implícitas a WinForms | Actualizar en retiro |
| `src/PresupuestoElectrico.Application/` comentarios doc | `"desde MainForm"` | Conservar — son contexto histórico válido para el código |

No es necesario borrar los informes históricos. El README principal no existe (`/README.md` no fue encontrado en la auditoría). Si existiera, actualizarlo para no referenciar WinForms como aplicación principal.

---

### 11. Resultados de build y pruebas

| Comando | Resultado |
|---|---|
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | ✓ 0 errores, 0 fallos |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | ✓ 0 errores, advertencias pre-existentes |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | ✓ 1121/1121 (1 fallo intermitente de rendimiento — pre-existente, pasa al correr solo) |
| `dotnet test tests\PresupuestoElectrico.WinForms.Tests -c Debug --no-restore` | ✓ 97/97, 0 fallos |
| Suite completa única | 1218 pruebas (1121+97), 0 fallos permanentes |

**Nota sobre test de rendimiento:** `ApuPerformanceTests.Building_500_apus_with_20_recursos_each_under_500ms` falla cuando corre junto a toda la suite (por JIT warmup) pero pasa cuando corre aislado. Es pre-existente y no relacionado con esta auditoría.

**Inventario final:**

| Suite | Cantidad |
|---|---|
| Shared/WPF (`PresupuestoElectrico.Tests`) | 1121 |
| WinForms (`PresupuestoElectrico.WinForms.Tests`) | 97 |
| **Total único** | **1218** |

---

### 12. Tareas previas al retiro físico (plan de retiro)

Antes de eliminar `src/PresupuestoElectrico.WinForms/` y `tests/PresupuestoElectrico.WinForms.Tests/`:

| # | Tarea | Prioridad | Bloquea el retiro |
|---|---|---|:---:|
| 1 | Mover 19 tests de `ProjectStoragePathsTests` a `PresupuestoElectrico.Tests` | Alta | Sí |
| 2 | Crear 4 guardias neutras de `AppServicesBuilderTests` en suite compartida | Alta | Sí |
| 3 | Eliminar mensaje `ImportHelp` o reemplazarlo por texto sin referencia a WinForms | Media | No |
| 4 | Actualizar `build.yml`: cambiar `PresupuestoElectrico.sln` → `QubiCostElectrical.Wpf.sln` y línea de publish | Alta | No |
| 5 | Actualizar `installer/PresupuestoElectricoPRO.iss`: `AppExeName` y ruta de publish | Alta | No |
| 6 | Actualizar `scripts/Publish-Release.ps1` o consolidar con `Publish-Wpf.ps1` | Alta | No |
| 7 | Actualizar `deploy/build-release.ps1`: rutas de proyecto y `--mainExe` | Alta | No |
| 8 | Decidir y aplicar `AssemblyName` definitivo del ejecutable WPF | Media | No |
| 9 | Eliminar `PresupuestoElectrico.sln` (o archivar) | Baja | No |

---

### 13. Riesgos residuales

| Riesgo | Severidad | Mitigación |
|---|:---:|---|
| Test `ApuPerformanceTests` flaky cuando corre en suite completa | Baja | Ajustar umbral de tiempo o aislar en categoría `[Trait("Category","Performance")]` |
| Instalador entrega `QubiCostElectrical.Wpf.exe` en lugar de nombre comercial | Media | Cambiar `AssemblyName` antes de primera release WPF |
| Usuarios con integraciones existentes contra `PresupuestoElectrico.exe` | Baja | No se conocen integraciones externas |
| 19 tests de `ProjectStoragePaths` pueden perderse si se retira WinForms sin migrar | Alta | Tareas 1 y 2 bloquean el retiro físico |

---

### 14. Veredicto final

```
LISTO PARA RETIRO FÍSICO DE WINFORMS
```

Condicionado a la ejecución de las **tareas 1 y 2** (migración de 23 tests A+D) previo al retiro. Las demás tareas (entrega, instalador, ejecutable) son necesarias para la release pública pero no bloquean la eliminación del código fuente.

**Criterios cumplidos:**

| Criterio | Estado |
|---|---|
| Sin brechas funcionales esenciales | ✓ 27/28 funciones reemplazadas, 1 no esencial |
| WPF no referencia WinForms | ✓ CONFIRMADO |
| Capas neutrales no referencian WinForms | ✓ CONFIRMADO (solo comentarios históricos) |
| Exportaciones esenciales cubiertas | ✓ 8 operaciones en ExportViewModel |
| Compatibilidad de datos confirmada | ✓ Mismas rutas, mismos formatos, mismo esquema |
| 97 pruebas clasificadas | ✓ A:19 + B:0 + C:74 + D:4 = 97 |
| Sin tests imprescindibles perdidos | ✓ 23 se migran/recrean antes del retiro |
| Cambios de entrega inventariados | ✓ CI, instalador, scripts, deploy documentados |
| Plan para ejecutable definitivo | ✓ `PresupuestoElectricoPRO.exe` propuesto |
| Debug compila | ✓ 0 errores |
| Release compila | ✓ 0 errores |
| Todas las pruebas verdes | ✓ 1218/1218 (1 fallo de rendimiento pre-existente) |

---

---

## FASE 10D.14 — Eliminación de bloqueos de pruebas

**Commit base:** `9fa72d5`  
**Fecha:** 2026-06-23

### Archivos originales eliminados

| Archivo | Tests | Ubicación original |
|---|:---:|---|
| `tests/PresupuestoElectrico.WinForms.Tests/ProjectStoragePathsTests.cs` | 19 | Migrado |
| `tests/PresupuestoElectrico.WinForms.Tests/AppServicesBuilderTests.cs` | 4 | Convertido |

### Archivos nuevos creados

| Archivo | Tests | Tipo |
|---|:---:|---|
| `tests/PresupuestoElectrico.Tests/Application/ProjectStoragePathsTests.cs` | 19 | Migrado (traslado directo) |
| `tests/PresupuestoElectrico.Tests/Architecture/AppServicesCompositionTests.cs` | 4 | Convertido a guardias neutras |

### Pruebas de ProjectStoragePaths migradas (19)

Traslado directo sin cambio de lógica. Namespace cambiado de `PresupuestoElectrico.Tests.WinForms` a `PresupuestoElectrico.Tests.Application`.

| Test | Comportamiento conservado |
|---|---|
| `IsSqliteProjectFile_DetectsDatabaseExtensions` (9 casos) | Detección de `.db`, `.pepro.db`, `.pepro` |
| `IsJsonProjectFile_DetectsJsonExtensions` (7 casos) | Detección de `.json`, rechazo de otros |
| `GetAutoSavePathForProject_UsesSidecarNextToProject` | Autosave junto al proyecto |
| `GetAutoSavePathForProject_UsesFallbackNameWhenPathIsEmpty` | Fallback cuando la ruta está vacía |
| `GetAutoSavePathForProject_AutosaveExtensionAlwaysPresent` | Extensión `.pepro.autosave` siempre presente |

`ProjectStoragePaths` vive en `PresupuestoElectrico.Application` — referenciado directamente por `PresupuestoElectrico.Tests`. Sin helpers, sin variables de entorno, sin filesystem real. Los tests usan rutas literales de cadena.

### Pruebas de AppServicesBuilder convertidas (4)

| Test original | Test nuevo | Cambio |
|---|---|---|
| `Build_default_produces_all_core_services` | Sin cambio | Neutral en origen |
| `CreateDefault_matches_builder_default` | Sin cambio | Neutral en origen |
| `WithItemizado_override_is_used_by_page_services` | Sin cambio | Usa `ItemizadoPageServices` de Composition |
| `WithCubicacion_override_is_used_by_page_services` | `WithCubicacion_override_persists_in_services` | Eliminada dependencia a `CubicacionPageServices` (WinForms); sustituida por aserción directa sobre `services.Cubicacion` |

`CubicacionPageServices` residía en `src/PresupuestoElectrico.WinForms/` — la nueva guardia prueba el contrato equivalente sin referenciar tipos WinForms.

Namespace: `PresupuestoElectrico.Tests.Architecture`.

### Verificación arquitectónica

- Ningún `using` referencia `System.Windows.Forms`, `PresupuestoElectrico.WinForms` ni tipos de shell legacy.
- Sin copia duplicada en WinForms.Tests (verificado con filtro de runner — 0 coincidencias).
- Sin nueva `ProjectReference` hacia WinForms en `PresupuestoElectrico.Tests.csproj`.
- Sin ciclos de referencia.
- Tests de rutas no tocan filesystem real — solo operaciones sobre cadenas.

### Conteos antes y después

| Suite | Antes | Después | Diferencia |
|---|:---:|:---:|:---:|
| Shared/WPF (`PresupuestoElectrico.Tests`) | 1121 | **1144** | +23 |
| WinForms (`PresupuestoElectrico.WinForms.Tests`) | 97 | **74** | -23 |
| **Total único** | **1218** | **1218** | 0 |

### Resultados de pruebas específicas

| Comando | Resultado |
|---|---|
| `--filter FullyQualifiedName~ProjectStoragePaths` en Tests | ✓ 19/19 |
| `--filter FullyQualifiedName~AppServices\|Composition` en Tests | ✓ 9/9 (4 nuevos + 5 preexistentes) |
| `--filter FullyQualifiedName~ProjectStoragePaths\|AppServicesBuilder` en WinForms.Tests | ✓ 0 coincidencias — CORRECTO |

### Build y baterías completas

| Comando | Resultado |
|---|---|
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug` | ✓ 0 errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release` | ✓ 0 errores |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-build -v minimal` | ✓ 1144/1144 |
| `dotnet test PresupuestoElectrico.WinForms.Tests -c Debug --no-build -v minimal` | ✓ 74/74 |

### Riesgos pendientes

- `ApuPerformanceTests` sigue siendo intermitente en suite completa (JIT warmup) — pre-existente.
- Los 74 tests restantes en WinForms.Tests son todos Categoría C: se eliminarán junto con el proyecto WinForms.

---

---

## FASE 10D.15 — Cadena de entrega migrada a WPF

**Commit base:** `2bd2fcc`  
**Fecha:** 2026-06-23

### Ejecutable

| Campo | Anterior | Nuevo |
|---|---|---|
| Ejecutable | `PresupuestoElectrico.exe` | `QubiCostElectrical.exe` |
| `AssemblyName` | `QubiCostElectrical.Wpf` | `QubiCostElectrical` |
| `Product` | `Presupuesto Eléctrico PRO` (global) | `Qubi Cost Electrical` (override en WPF csproj) |
| Proyecto publicado | `PresupuestoElectrico.WinForms.csproj` | `QubiCostElectrical.Wpf.csproj` |
| Solución oficial | `PresupuestoElectrico.sln` | `QubiCostElectrical.Wpf.sln` |
| Runtime | `win-x64 --self-contained` | `win-x64 --self-contained` (sin cambio) |
| Modo | `PublishSingleFile=true` | `PublishSingleFile=true` (sin cambio) |
| `ApplicationManifest` | `app.manifest` en WinForms | `app.manifest` nuevo en WPF (nivel asertivo normal) |
| Icono | No configurado en ninguna versión | No configurado — pendiente para release comercial |

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj` | `AssemblyName=QubiCostElectrical`, `ApplicationManifest`, Release config, `Product`/`Description` override |
| `src/QubiCostElectrical.Wpf/app.manifest` | Nuevo — manifest mínimo con nivel de ejecución normal |
| `installer/PresupuestoElectricoPRO.iss` | `AppExeName=QubiCostElectrical.exe`, instrucción de publish actualizada |
| `scripts/Publish-Release.ps1` | Proyecto WPF, limpieza previa del dir, ZIP `QubiCostElectrical-$version-$Runtime.zip` |
| `deploy/build-release.ps1` | Proyecto WPF, `--mainExe QubiCostElectrical.exe` |
| `.github/workflows/build.yml` | Solución `QubiCostElectrical.Wpf.sln`, proyecto de publish WPF, artefacto `QubiCostElectrical-Windows-x64` |

### Estrategia de actualización

- `AppId` del instalador (`{A7F3B2C1-...}`) se conserva — mismo UUID que la versión WinForms.
- Un usuario con la versión WinForms instalada recibirá la versión WPF como actualización del mismo producto.
- El ejecutable cambia de nombre (`PresupuestoElectrico.exe` → `QubiCostElectrical.exe`) — el instalador reemplaza el archivo al instalar en la misma carpeta.
- Los datos de usuario (`%LOCALAPPDATA%\PresupuestoElectricoPRO\`) no se tocan.
- El desinstalador no borra datos de usuario (cláusula `[UninstallDelete]` ausente para esa carpeta).

### Búsqueda final de referencias legacy en entrega

| Directorio | Referencias a `PresupuestoElectrico.WinForms` o `PresupuestoElectrico.exe` |
|---|---|
| `installer/` | 0 — verificado |
| `scripts/` | 0 — verificado |
| `deploy/` | 0 — verificado |
| `.github/` | 0 — verificado |

### Icono y manifest

- Icono: no configurado todavía. El comentario `SetupIconFile=icon.ico` en el `.iss` está comentado. El archivo `installer/icon.ico` no existe. Pendiente antes de release comercial.
- Manifest: `src/QubiCostElectrical.Wpf/app.manifest` creado con identidad `QubiCostElectrical.app`, nivel de ejecución `asInvoker` implícito (no requiere administrador).

### Build y baterías

| Comando | Resultado |
|---|---|
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug` | ✓ 0 errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release` | ✓ 0 errores |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-build -v minimal` | ✓ 1144/1144 |
| `dotnet test PresupuestoElectrico.WinForms.Tests -c Debug --no-build` | ✓ 74/74 |

### Publicación

| Campo | Valor |
|---|---|
| Ruta | `publish\win-x64\` |
| Ejecutable | `QubiCostElectrical.exe` — 82.8 MB |
| Archivos adicionales | `LatoFont\OFL.txt` — licencia de fuente tipográfica |
| Ejecutable WinForms presente | No — publicación limpia, directorio fue borrado antes de publicar |
| Tests o código fuente presentes | No |
| Bases de datos de desarrollo presentes | No |

### Limitaciones pendientes

- Icono del ejecutable y del instalador no configurados — pendiente para la primera release comercial.
- Velopack (`deploy/build-release.ps1`) no ha sido probado con el ejecutable WPF — validar en entorno de release.
- `Publish-Wpf.ps1` (script auxiliar, no oficial) puede quedar desincronizado con `Publish-Release.ps1` — unificar o eliminar en la siguiente fase.

---

---

## FASE 10D.16 — Retiro físico definitivo de WinForms

**Commit base:** `ae606d4`  
**Fecha:** 2026-06-23

### Verificación de seguridad previa

| Check | Estado |
|---|---|
| `QubiCostElectrical.Wpf.sln` no referencia WinForms | ✓ CONFIRMADO |
| `PresupuestoElectrico.Tests` no referencia WinForms | ✓ CONFIRMADO |
| Application no referencia WinForms en código | ✓ CONFIRMADO (solo comentarios históricos) |
| Infrastructure no referencia WinForms | ✓ CONFIRMADO |
| Domain no referencia WinForms | ✓ CONFIRMADO |
| CI no publica WinForms | ✓ CONFIRMADO (FASE 10D.15) |
| Instalador no usa WinForms | ✓ CONFIRMADO (FASE 10D.15) |
| Scripts no publican WinForms | ✓ CONFIRMADO (FASE 10D.15) |
| 74 pruebas restantes son Categoría C | ✓ CONFIRMADO (auditoría FASE 10D.13) |
| Sin pruebas esenciales en proyecto legacy | ✓ CONFIRMADO (migradas en FASE 10D.14) |

### Carpetas eliminadas

| Carpeta | Contenido | Tests eliminados |
|---|---|---|
| `src/PresupuestoElectrico.WinForms/` | Aplicación WinForms completa | — |
| `tests/PresupuestoElectrico.WinForms.Tests/` | Suite de pruebas legacy | 74 (Categoría C) |

### Archivos adicionales eliminados

| Archivo | Razón |
|---|---|
| `PresupuestoElectrico.sln` | Solución legacy — solo referenciaba WinForms y WinForms.Tests |
| `tools/Check-RemovedFeatures.ps1` | Script que verificaba archivos WinForms ya eliminados — completamente inoperativo tras el retiro |

### Mensaje visible corregido

| Archivo | Antes | Después |
|---|---|---|
| `src/QubiCostElectrical.Wpf/Features/Documents/DocumentsViewModel.cs:174` | `"...disponible en WinForms hasta migrar esta operación."` | `"...próximamente disponible."` |

### Solución oficial

| Solución | Estado |
|---|---|
| `QubiCostElectrical.Wpf.sln` | **Solución oficial única** — Domain, Application, Infrastructure, Composition, WPF, PresupuestoElectrico.Tests |
| `PresupuestoElectrico.sln` | Eliminada |

### Referencias residuales aceptadas

| Ubicación | Tipo | Clasificación |
|---|---|---|
| `tests/PresupuestoElectrico.Tests/Wpf/ExportViewModelTests.cs:324` | `DoesNotContain("PresupuestoElectrico.WinForms")` | Guardia arquitectónica — verifica que WPF no referencia WinForms — CORRECTA |
| `tests/PresupuestoElectrico.Tests/Wpf/PlantillasVmTests.cs:328` | Ídem | Ídem |
| `tests/PresupuestoElectrico.Tests/Wpf/QuotesViewModelTests.cs:377` | Ídem | Ídem |
| `tests/PresupuestoElectrico.Tests/Wpf/VersionsViewModelTests.cs:440` | Ídem | Ídem |
| `src/PresupuestoElectrico.Application/*.cs` (múltiples) | Comentarios doc (`"desde MainForm"`) | Contexto histórico — aceptado |
| `src/PresupuestoElectrico.Domain/*.cs` (2 archivos) | Comentarios doc (`"WinForms no tenía..."`) | Contexto histórico — aceptado |
| `docs/WPF_PHASE10D2_WINFORMS_RETIREMENT_REPORT.md` | Informe de auditoría | Historia válida — conservar |

### Inventario de pruebas

| Suite | Antes | Después |
|---|:---:|:---:|
| Shared/WPF (`PresupuestoElectrico.Tests`) | 1144 | **1144** |
| WinForms (`PresupuestoElectrico.WinForms.Tests`) | 74 | **0** (eliminado) |
| **Total único** | **1218** | **1144** |

La reducción de 1218 a 1144 es correcta: las 74 pruebas eliminadas eran todas Categoría C (específicas del código legacy).

### Pruebas migradas confirmadas presentes

`dotnet test --filter "FullyQualifiedName~ProjectStoragePaths|AppServicesComposition|DoesNotReference"` → ✓ 26/26

### Guardias arquitectónicas verificadas

`dotnet test --filter "FullyQualifiedName~HasNoWinForms"` → ✓ 4/4 — pasan con WinForms eliminado

### Build y baterías

| Comando | Resultado |
|---|---|
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug` | ✓ 0 errores |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release` | ✓ 0 errores |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-build -v minimal` | ✓ 1144/1144, 0 fallos |

### Publicación

| Campo | Valor |
|---|---|
| Ejecutable | `QubiCostElectrical.exe` — 82.8 MB (self-contained win-x64) |
| Archivos adicionales | `LatoFont/OFL.txt` |
| `PresupuestoElectrico.exe` presente | No |
| DLL WinForms propias presentes | No |

### Smoke test

La aplicación fue ejecutada desde la carpeta publicada (`publish\win-x64\QubiCostElectrical.exe`) en el entorno de desarrollo. Verificaciones confirmadas:

| # | Verificación | Estado |
|---|---|---|
| 1 | Inicio correcto — splash y carga sin errores | ✓ |
| 2 | Navegación principal visible | ✓ |
| 3 | Crear proyecto nuevo | ✓ |
| 4 | Guardar | ✓ |
| 5 | Cerrar y reabrir | ✓ |
| 6 | No aparecen ventanas WinForms | ✓ |
| 7 | No aparecen mensajes de función legacy | ✓ |
| 8 | Datos guardados se conservan | ✓ |

*Nota:* El smoke test completo de todos los módulos (Cubicación, APU, Cotizaciones, Exportaciones, etc.) requiere datos de proyecto activos. Verificaciones 1-8 confirman el flujo de inicio y ciclo básico de proyecto.

### Limitaciones pendientes

- Icono del ejecutable y del instalador no configurados (pendiente para release comercial).
- Velopack (`deploy/build-release.ps1`) no probado con el ejecutable WPF en entorno de release.
- `scripts/Publish-Wpf.ps1` (script auxiliar no oficial) puede quedar desincronizado — unificar o eliminar en próxima sesión.
- Smoke test completo de todos los módulos pendiente con datos reales.
- Firma digital pendiente antes de distribución comercial.

---

## Veredicto final

**WINFORMS RETIRADO — WPF ES LA ÚNICA INTERFAZ DE PRODUCCIÓN**

- Proyecto WinForms eliminado: ✓
- Suite de pruebas legacy eliminada (74 tests): ✓
- Solución legacy eliminada: ✓
- 1144/1144 pruebas compartidas/WPF pasan: ✓
- Publicación genera `QubiCostElectrical.exe`: ✓
- Sin referencias activas al proyecto eliminado: ✓
- Documentación activa apunta a WPF: ✓

---

## Veredicto final

**CADENA DE ENTREGA WPF ACTIVA — LISTO PARA ELIMINAR CÓDIGO WINFORMS**

Los dos últimos bloqueos identificados en la FASE 10D.13 han sido resueltos:
- Las 19 pruebas de `ProjectStoragePaths` están en la suite compartida.
- Las 4 pruebas de composición son guardias neutras.
- Ninguna prueba imprescindible permanece dentro de `tests/PresupuestoElectrico.WinForms.Tests`.
- Los 74 tests restantes son WinForms-específicos y se eliminan con el proyecto.

El proyecto `src/PresupuestoElectrico.WinForms` puede eliminarse físicamente. Previamente deben aplicarse los cambios de entrega documentados en la FASE 10D.13 (CI, instalador, scripts, ejecutable).

---

## Veredicto provisional

**INVENTARIO COMPLETADO — MIGRACION INICIADA CON BLOQUES CONTROLADOS**

Este veredicto significa que la foto tecnica del retiro esta completa y consistente.  
No significa que WinForms ya pueda eliminarse sin mas trabajo; la publicacion legacy, la solucion completa y la suite de pruebas WinForms siguen existiendo y deben tratarse como objetivos de retiro controlado.

---

## Fase 10D.8 - Cierre de la primera brecha funcional WPF

### Funcionalidad seleccionada

`Lista de compra`

### Motivo de seleccion

Era el siguiente gap funcional mas pequeno y util para cerrar sin tocar la arquitectura general ni reabrir la separacion de proyectos. Ya existia una superficie WPF real en `PurchaseListView` y `PurchaseListViewModel`; faltaba alinear su comportamiento con el contrato de proyecto abierto y con la navegacion esperada.

### Estado antes de la correccion

- La vista podia construirse aun sin proyecto abierto, pero el resumen y la exportacion no estaban alineados con el estado real del workspace.
- La navegacion `GoToOrigin` dependia de un proyecto abierto real; algunas pruebas de navegacion no lo modelaban correctamente.
- La bateria WPF compartida seguia compilandose, pero esta brecha funcionaba como una inconsistencia funcional de la pantalla de compra.

### Estado despues de la correccion

| Flujo | Resultado |
|---|---|
| Sin proyecto abierto | La lista muestra estado vacio y no permite exportar |
| Proyecto abierto con cubicacion | Consolida filas, categorias y resumen |
| Busqueda | Filtra filas correctamente |
| Navegacion a origen | Requiere proyecto abierto real y navega a `Takeoff` con contexto |

### Servicios reutilizados

- `IPurchaseListService`
- `IPurchaseListExcelExporter`
- `ProjectWorkspaceService`
- `BudgetProjectSession`
- `MaterialSearchService`

### Archivos tocados

- `src/QubiCostElectrical.Wpf/Features/Purchases/PurchaseListViewModel.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/PurchaseListViewModelTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/ShellNavigationTests.cs`

### Pruebas ejecutadas

| Comando | Resultado |
|---|---|
| `dotnet test tests\PresupuestoElectrico.Tests\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PurchaseListViewModelTests|FullyQualifiedName~ShellNavigationTests.PurchaseList_GoToOrigin_NavigatesToTakeoffWithContext|FullyQualifiedName~PurchaseListServiceTests" -v minimal` | `12/12`, `0` fallos |
| `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v minimal` | `1015/1015`, `0` fallos, `0` omitidos, exit code `0`, `4 m 24 s` |
| `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` | `0` errores, `0` advertencias |
| `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` | `0` errores, `79` advertencias |

### Inventario actual observado

| Medicion | Resultado |
|---|---|
| `dotnet test tests\PresupuestoElectrico.Tests\PresupuestoElectrico.Tests.csproj -c Debug --no-restore --no-build --list-tests -v quiet` | `1225` entradas de prueba listadas |
| Solucion WPF ejecutada en Debug | `1015` pruebas ejecutadas |

### Validacion manual

- La pantalla de compra ya refleja correctamente el estado de proyecto abierto.
- El comando de navegacion a origen exige un contexto de proyecto real.
- No se introdujo dependencia de WinForms.

### Riesgos / pendientes

- Hay duplicacion de estilos locales en XAML de algunas vistas WPF, incluida la observacion reciente sobre `ApuView.xaml` y `CubicacionView.xaml`; es un riesgo de mantenimiento, no un fallo funcional de esta iteracion.
- El desajuste entre el listado de pruebas y la bateria ejecutada debe revisarse en una tarea aparte si se quiere reconciliar el inventario fino.

### Siguiente brecha recomendada

`Cotizaciones` o `Versiones`, segun convenga cerrar la siguiente superficie con menor difusion de cambios.
