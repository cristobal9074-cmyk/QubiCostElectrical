# Fase 9 — Informe de estabilidad (Stability)

## Resumen

La Fase 9 implementa logging estructurado, manejo global de errores, guardado atómico, backups, autoguardado y bloqueo de proyectos para mejorar la estabilidad y seguridad de datos en la UI WPF.

## Archivos nuevos

| Archivo | Propósito |
|---|---|
| `Infrastructure/Diagnostics/IAppLogger.cs` | Interfaz de logging (Info/Warning/Error/Critical) |
| `Infrastructure/Diagnostics/LogConfiguration.cs` | Configuración: ruta, rotación, sesión ID, versión SO/arquitectura |
| `Infrastructure/Diagnostics/FileLogger.cs` | Logger JSONL, thread-safe, rotación por tamaño (10 MB), retención (30 días), redacción de datos sensibles |
| `Infrastructure/Diagnostics/GlobalErrorHandler.cs` | Manejadores globales: DispatcherUnhandledException, AppDomain.UnhandledException, TaskScheduler.UnobservedTaskException |
| `Infrastructure/AutoSaveService.cs` | Autoguardado cada 60s con SafeSaveService, exclusión mutua, detección dirty |
| `Infrastructure/ProjectLockService.cs` | Lock por proyecto con heartbeat 30s, detección de proceso vivo, sin auto-eliminación remota |
| `SafeSaveService.cs` (Infrastructure) | Guardado atómico JSON (tmp → Flush → validación → File.Replace/Move), backups rotativos (10 slots) |

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `App.xaml.cs` | `+GlobalErrorHandler.Initialize()` en OnStartup; `+Shutdown()` + log dispose en OnExit |
| `WpfAppHost.cs` | `+Logger` static property, `+FileLogger` initialization, `+AppLog.Sink` |
| `ShellViewModel.cs` | `+AutoSaveService`, `+ProjectLockService`, `+IsReadOnly`, lock check al abrir, autosave start/stop, cleanup en save |
| `ProjectRepository.cs` (Infrastructure) | `SaveProjectFileAsync` delega en `SafeSaveService` |
| `Directory.Build.props` | `+Company`, `+Product`, `+Copyright`, `+Description` |

## Cobertura de pruebas

- 5 tests SafeSaveService (creación, reemplazo atómico + backup, fallo no sobreescribe, rotación, fallo limpio)
- 5 tests ProjectLockService (adquirir lock, detectar libre, detectar misma máquina, liberar, stale)
- 4 tests AutoSaveService (ruta con/sin proyecto, detección más nuevo, eliminación)
- 5 tests FileLogger (escritura JSONL, errorId, redacción sensible, Dispose AppLog.Sink, niveles)
- Total: 19 tests nuevos (861/861 pasan)

## Decisiones técnicas

- SafeSaveService en Infrastructure (no WPF) para que ProjectRepository pueda usarlo sin dependencia circular
- AutoSaveService usa el mismo SafeSaveService que el guardado manual
- Lock no se declara obsoleto por tiempo solo; en otra máquina requiere acción explícita del usuario
- Eliminación de autosave solo desde ShellViewModel tras guardado exitoso
- Redacción de emails, IPs y rutas UNC en logs
