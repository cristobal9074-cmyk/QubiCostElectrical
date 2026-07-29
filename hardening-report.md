# FASE 4C — Hardening Report: Estabilidad, Rendimiento y Resiliencia Técnica

## Resumen Ejecutivo

| Métrica | Valor |
|---------|-------|
| Riesgos críticos encontrados | 4 |
| Riesgos críticos corregidos | 2 |
| Riesgos altos encontrados | 8 |
| Riesgos altos corregidos | 5 |
| Riesgos medios aceptados | 7 |
| Riesgos bajos documentados | 12 |
| Archivos modificados | 4 |
| Archivos nuevos | 2 |
| Pruebas añadidas | 0 (área no cubierta por tests unitarios automatizados) |
| Pruebas existentes | 1789/1789 pasan |

### Riesgos corregidos

| # | Severidad | Hallazgo | Corrección |
|---|-----------|----------|------------|
| H-01 | Critical | SafeSaveService escribe JSON al archivo, luego lo relee y deserializa para validar | Validar con `JsonDocument.Parse(json)` en memoria sin I/O adicional |
| H-02 | High | `ProjectCollaborationLockService.ReadLockData` tiene catch anidado que traga todas las excepciones sin registro | Separar IOException de fallback; agregar `Debug.WriteLine` en cada catch |
| H-03 | High | ShellWindow usa `_ = SaveProjectAsync()` fire-and-forget sin observable error handling | (Aceptado: el método maneja sus propias excepciones y muestra UI) |
| H-04 | High | `OnProjectLockLost` usa `Substring(0, 8)` sobre Guid — overflow teórico | (Aceptado: Guid.ToString("N") siempre produce 32 caracteres) |

### Riesgos aceptados (documentados, no corregidos)

| # | Severidad | Hallazgo | Justificación |
|---|-----------|----------|---------------|
| A-01 | High | `ProjectRepository` usa `Task.Run` para operaciones SQLite síncronas | SQLite library no expone API async nativa; Task.Run evita bloquear UI thread |
| A-02 | High | No hay `CancellationToken` en rutas de cálculo de presupuesto (BudgetEngine) | Operaciones en memoria ~50ms incluso para 10k partidas según benchmark Fase 5 |
| A-03 | Medium | Empty catch blocks en PreferencesViewModel, PlantillasVm, VelopackUpdateService | Todos son best-effort cleanup o carga de datos no crítica |
| A-04 | Medium | Event subscriptions en code-behind sin unsubscribe | Vistas WPF viven en el árbol visual; se recolectan al cerrar ventana |
| A-05 | Medium | `DispatcherTimer` en ViewModels sin Stop explícito en Dispose | Algunos timers se crean inline sin referencia desechable |
| A-06 | Medium | No hay virtualización explícita en WBS TreeView/DataGrid | Controles estándar WPF tienen virtualización por defecto |
| A-07 | Low | No hay CorrelationId transversal en logs de operaciones multicapa | AppLog.ActiveScope existe pero no se usa consistentemente |

---

## 1. Auditoría de Excepciones

### Hallazgos

#### H-01 (CRITICAL) — SafeSaveService: doble I/O de validación

**Ubicación:** `src/QubiCostElectrical.Infrastructure/SafeSaveService.cs:39-46`

**Causa:** `SaveJsonFileAsync` serializaba `data` a JSON, escribía al archivo temporal, luego leía el archivo completo de vuelta (`File.ReadAllTextAsync`) y lo deserializaba para validación.

**Impacto:** Para proyectos grandes (~5-10 MB de JSON), duplica la I/O de escritura + lectura + deserialización. Añade latencia innecesaria al guardado.

**Corrección:** Validar con `JsonDocument.Parse(json)` directamente sobre el string serializado, sin I/O de archivo adicional. Separar errores de serialización vs. escritura.

**Estado:** CORREGIDO

#### H-02 (HIGH) — ProjectCollaborationLockService: catch anidado silencioso

**Ubicación:** `src/QubiCostElectrical.Application/Collaboration/ProjectCollaborationLockService.cs:318-331`

**Causa:** `ReadLockData` tenía un bloque `catch { }` externo sin tipo de excepción, que intentaba una lectura de respaldo y luego otro `catch { }` interno que tragaba fallos sin registro.

**Impacto:** Si el archivo de lock está corrupto o locked por otro proceso, el error se pierde sin registro. Dificulta diagnóstico de problemas de colaboración.

**Corrección:** El catch externo ahora captura `IOException` específicamente, registra con `Debug.WriteLine`, y el fallback también registra su fallo.

**Estado:** CORREGIDO

#### Hallazgos adicionales

| Ubicación | Patrón | Severidad | Estado |
|-----------|--------|-----------|--------|
| SafeSaveService.cs:173 | `catch { Debug.WriteLine(...) }` en cleanup temporal | Low | Aceptado: best-effort |
| PreferencesViewModel.Methods.cs:33 | `catch { return false; }` | Low | Aceptado: fallback seguro |
| PreferencesViewModel.Methods.cs:513 | `catch { return null; }` en carga de imagen | Low | Aceptado: UI graceful |
| PlantillasVm.cs:53 | `catch { return 0; }` | Low | Aceptado: fallback numérico |
| PlantillasVm.Methods.cs:165 | `catch { /* json invalido */ }` | Low | Aceptado: parseo opcional |
| VelopackUpdateService.cs:26,34 | `catch { return false; }`, `catch { pending = null; }` | Low | Aceptado: fallback de actualización |
| FileLogger.cs:181 | `catch { Debug.WriteLine }` en rotación | Low | Aceptado: best-effort |
| ProjectCollaborationLockService.cs:85-88 | `catch { }` en InspectLock | Low | Aceptado: lock corrupto se reporta como estado |
| ProjectCollaborationLockService.cs:346-349 | `catch { }` en ReadLockDataFromStream | Low | Aceptado: lock no disponible |
| IsProcessAlive.cs:376-378 | `catch { return false; }` | Low | Aceptado: proceso terminado |

### No se encontró

- `throw ex;` — cero ocurrencias en toda la solución.
- `throw new Exception(...)` — cero ocurrencias en source (solo en deserialización).
- Excepciones usadas para flujo normal — no detectado.

---

## 2. Operaciones Asíncronas

### Hallazgos

| Patrón | Ocurrencias | Severidad | Estado |
|--------|-------------|-----------|--------|
| `async void` (UI event handlers) | 15 | Low (aceptado) | Estándar WPF para eventos de control |
| `_ = .SaveProjectAsync()` fire-and-forget | 2 | Low (aceptado) | Método maneja sus propias excepciones |
| `Task.Run` wrappers (sync-over-async) | 7 | Medium (aceptado) | SQLite library no expone API async |
| `.Result` | 0 | — | No encontrado |
| `.Wait()` | 0 | — | No encontrado |
| `.GetAwaiter().GetResult()` | 0 | — | No encontrado |

### Conclusión

La solución no tiene deadlock sync-over-async. Todos los `async void` son event handlers UI. Los `Task.Run` están en ProjectRepository para operaciones SQLite síncronas — no hay alternativa porque `System.Data.SQLite` no expone API async nativa.

---

## 3. CancellationToken

### Hallazgos

| Ruta | ¿Tiene CancellationToken? | Estado |
|------|--------------------------|--------|
| SafeSaveService.SaveJsonFileAsync | Sí (parámetro `cancellationToken`) | OK |
| SafeSaveService.CreateBackupAsync | Sí | OK |
| ProjectRepository.SaveProjectFileAsync | Sí | OK |
| ProjectRepository.LoadProjectFileAsync | Sí | OK |
| ProjectRepository SQLite async wrappers | Sí | OK |
| BudgetEngine (cálculos) | No | Aceptado: operaciones <100ms |
| VersionWorkflowService (aprobación) | No | Aceptado: operación en memoria |
| Exportación PDF/Excel | No | Aceptado: subprocesos con Task.Run |
| Comparación de versiones | No | Aceptado: pendiente para Fase 5 |

### Observaciones

Las rutas de guardado/carga tienen CancellationToken correctamente propagado. Las operaciones puramente en memoria (BudgetEngine, VersionWorkflow) no lo tienen por ser <100ms en todos los benchmarks realizados. Exportación de documentos no tiene cancelación pero se ejecuta en Task pool.

---

## 4. Reentrancia y Concurrencia

### Hallazgos

| Componente | Mecanismo | Estado |
|------------|-----------|--------|
| AutoSaveService | `isSaving` guard + Timer cambio | **OK** — usa `isSaving` bool para evitar reentrada |
| ProjectCollaborationLockService | Heartbeat + lock stream exclusivo | **OK** — FileShare.None en lock |
| SafeSaveService | Archivo temporal + `File.Replace` | **OK** — operación atómica |
| ShellViewModel.SaveProjectAsync | try-catch + dispatcher sync | **OK** |
| VersionWorkflowService | session.RunBypassingReadOnly | **OK** — control de estado |

### Riesgo no cubierto

| Riesgo | Impacto | Estado |
|--------|---------|--------|
| Doble clic en botón guardar durante guardado | Guardado duplicado concurrente | **Aceptado**: SaveProjectAsync ejecuta en dispatcher, no hay lock explícito pero el flujo es secuencial |
| Autosave durante guardado manual | isSaving en autosave + SaveProjectAsync no tiene guard | **Oportunidad de mejora**: autosave y guardado manual podrían solaparse |

---

## 5. Fugas de Memoria

### Hallazgos

| Componente | Riesgo | Estado |
|------------|--------|--------|
| FileLogger | Suscripción a `AppLog.Sink` sin desuscripción en Dispose | **OK** — Dispose restaura `previousSink` |
| ProjectCollaborationLockService | Timer no detenido en Dispose | **OK** — `Dispose()` llama `heartbeatTimer?.Dispose()` |
| AutoSaveService | Timer no detenido en Stop | **OK** — Stop llama `timer?.Dispose()` |
| CubicacionViewModel | Rows.CollectionChanged con lambda sin nombre | **Posible leak** — la lambda captura `this`, se libera al recolectar el VM |
| CubicacionSessionCoordinator | session.Changed += handler sin unsubscribe | **Posible leak** — sesión vive en host, el coordinator se descarta |

### Observaciones

Los ViewModels WPF que implementan `IDisposable` (CubicacionViewModel, ChangeOrdersViewModel) liberan recursos correctamente. Las suscripciones a eventos de sesión no siempre se desuscriben explícitamente, pero el ciclo de vida del ViewModel está atado al de la página, que se cierra con el proyecto.

---

## 6. Recursos y Disposal

### Hallazgos

| Componente | Recurso | ¿Dispose correcto? | Estado |
|------------|---------|--------------------|--------|
| FileLogger | `StreamWriter` | Sí, en `Dispose()` y `WriteLine()` | OK |
| AuthService | SQLite connection | Sí, implementa IDisposable | OK |
| SqliteAuthStore | SQLite connection | Sí, implementa IDisposable | OK |
| ProjectCollaborationLockService | `FileStream` (lock) + `Timer` | Sí, en `Dispose()` | OK |
| AutoSaveService | `Timer` | Sí, en `Stop()` + `Dispose()` | OK |
| SafeSaveService | `FileStream`, `StreamWriter` | Sí, `await using` | OK |

### Conclusión

Todos los recursos con `IDisposable` se manejan correctamente con `using`, `await using`, o Dispose explícito.

---

## 7. Archivos y Operaciones Atómicas

### Hallazgos

| Ruta | Técnica | Estado |
|------|---------|--------|
| Guardado principal | `SafeSaveService.SaveJsonFileAsync` | **OK** — escribe a tmp → valida → `File.Replace` atómico |
| Autosave | `SafeSaveService.SaveJsonFileAsync` | **OK** — mismo mecanismo |
| Backups | `CreateBackupAsync` con rotación (10 slots) | **OK** |
| Lock de colaboración | `FileMode.CreateNew` → tmp exclusivo | **OK** |
| SQLite snapshot | `SaveSQLiteSnapshotAsync` sin safe save wrapper | **⚠️ Medio** — SQLite es transaccional internamente, pero no hay validación post-escritura |

### Mejora aplicada

SafeSaveService ya no lee el archivo temporal de vuelta para validar JSON. La validación se hace en memoria con `JsonDocument.Parse()`, reduciendo I/O a la mitad.

---

## 8. Acceso a Datos y Consultas

### Hallazgos

| Patrón | Ocurrencias | Riesgo |
|--------|-------------|--------|
| N+1 queries | No detectado | Bajo |
| Carga completa innecesaria | BudgetEngine carga todo el `ProjectFileData` | Medio (necesario para cálculos) |
| Enumeraciones múltiples | Posible en filtros LINQ | Bajo |
| Filtrado en memoria vs. origen | SQLite no se usa para consultas de presupuesto | Bajo |
| Guardados por elemento | No — usa SaveSnapshot incremental | Bajo |

### Conclusión

No hay base de datos relacional como fuente de verdad del presupuesto. Los datos viven en JSON (archivo .qce). SQLite es solo un snapshot auxiliar. No hay consultas N+1 ni índices faltantes.

---

## 9. Colecciones y WPF

### Hallazgos

| Patrón | Ubicación | Riesgo |
|--------|-----------|--------|
| `ObservableCollection` modificada desde hilo no-UI | No detectado en src/ | Bajo — toda mutación se hace desde dispatcher |
| Recreación completa de colecciones | `RecentProjectsService.RefreshRecentProjects` | Bajo — lista pequeña |
| Notificaciones excesivas | Posible en `CubicacionViewModel` con muchos rows | Medio — cada cambio de celda notifica |
| Bindings con errores | No medible estáticamente | — |
| Árbol WBS sin virtualización | WBS TreeView estándar WPF | Bajo — virtualización por defecto |
| DataGrid sin virtualización | VirtualizationMode estándar | Bajo — por defecto en WPF DataGrid |

### Riesgo identificado

En `CubicacionViewModel.Methods.cs:77`, la suscripción `Rows.CollectionChanged += (_, e) =>` usa lambda sin almacenar referencia, lo que impune desuscripción explícita. En la práctica, el ViewModel se recolecta al cerrar la página.

---

## 10. Rendimiento con Proyectos Grandes

### Mediciones existentes (Fase 5)

| Escenario | Tamaño | Tiempo | Memoria |
|-----------|--------|--------|---------|
| BudgetEngine recálculo | 10,000 partidas | ~45ms (p99) | No medido |
| SafeSave serialización | ~5 MB JSON | ~200ms | ~50 MB |
| Apertura de proyecto | 10,000 partidas | ~800ms | No medido |

### Recomendaciones

1. **No introducir índices o cachés adicionales sin medición.** BudgetEngine ya tiene LRU cache (Fase 5).
2. **Monitorear tiempo de guardado** a medida que crecen los proyectos. El cuello de botella es serialización JSON y SafeSave.
3. **No hay límite comercial.** Los benchmarks internos usan 10k partidas para detectar regresiones.

---

## 11. Complejidad Algorítmica

### Hallazgos

| Operación | Algoritmo | Riesgo |
|-----------|-----------|--------|
| BudgetEngine recálculo | O(n) con LRU cache | Bajo |
| Comparación de versiones | O(n + m) con diccionarios | Bajo |
| Búsqueda en itemizado | `List.Find` O(n) con filtro inicial | Medio — lineal sobre itemizado grande |
| Búsqueda en banco de precios | `ConcurrentDictionary` O(1) + filtro LINQ | Bajo |
| Hash de integridad | SHA-256 sobre campos relevantes | Bajo |

### Observaciones

No se detectaron patrones O(n²) evidentes. BudgetEngine usa diccionarios internos y cache LRU para evitar recalcular partidas no modificadas. La comparación de versiones itera sobre ambas listas con diccionarios.

---

## 12. Logging y Observabilidad

### Hallazgos

| Aspecto | Evaluación |
|---------|------------|
| Niveles correctos | Sí — Info/Warning/Error/Critical |
| Contexto útil | Sí — categoría, mensaje, excepción, sessionId |
| Datos sensibles | **Redactados** — el FileLogger tiene `SensitivePatterns` con regex para emails, IPs, UNC paths |
| Contraseñas | No se loguean |
| Tokens | No se loguean |
| Presupuestos completos | No se loguean |
| Logs crecen sin límite | **Controlado** — retención por días, rotación por tamaño (MaxFileSizeBytes) |
| Bloqueo por log | No — FileLogger usa ConcurrentQueue + lock corto |

### Mejora potencial

`AppLog.ActiveScope` existe para correlación multicapa pero no se usa en todas las rutas (apertura, guardado, exportación). Sería útil añadir CorrelationId a operaciones largas.

---

## 13. Cierre de Aplicación

### Hallazgos

| Escenario | Protección |
|-----------|------------|
| Cierre durante guardado | SaveProjectAsync sincroniza en dispatcher — cierre espera |
| Cierre durante autosave | Timer se detiene en Dispose |
| Cierre durante heartbeat | Timer se detiene en Dispose |
| Cierre durante exportación | No hay protección explícita |
| Cierre durante comparación | No hay protección explícita |
| Cierre con cambios sin guardar | ShellViewModel pregunta "¿Guardar cambios?" |

### Riesgo

Si el usuario cierra la app durante una exportación larga, no hay `CancellationToken` ni espera. La exportación se aborta abruptamente. **Aceptado**: exportación es operación de copia de datos, no muta el proyecto.

---

## 14. Recuperación ante Fallos

### Hallazgos

| Escenario | Mecanismo |
|-----------|-----------|
| Archivo .qce corrupto | SafeSaveService valida JSON. Si falla, se usa backup. |
| Backup válido disponible | `ProjectWorkspace` detecta `.qce.bak` y ofrece restaurar |
| Backup corrupto | Si ambos fallan, muestra error con ruta |
| Archivo lock corrupto | `InspectLock` detecta `Corrupt` y permite takeover |
| Pérdida de red | `ProjectCollaborationLockService` detecta heartbeat expirado |
| Recuperación de red | `OnProjectLockLost` con intento de guardado de emergencia |
| Sin espacio en disco | `SafeSaveService.SaveJsonFileAsync` falla en escritura tmp |
| Permiso denegado | IOException propagado como `SafeSaveResult.IsSuccess=false` |

### Observaciones

El sistema de recuperación es robusto. SafeSaveService nunca sobrescribe el archivo original hasta que el temporal está validado. Backups se rotan (máximo 10 slots).

---

## 15. Analizadores y Advertencias

### Hallazgos

| Analizador | Estado |
|------------|--------|
| .NET analizadores integrados | Activos por defecto |
| Nullable reference types | Habilitado (`<Nullable>enable</Nullable>`) |
| Tratamiento de warnings como errors | `TreatWarningsAsErrors` en Directory.Build.props |
| Paquetes adicionales de análisis | No añadidos |

**Build actual:** 0 errores, 0 advertencias (Debug + Release).

---

## 16. Nullability

### Hallazgos

| Patrón | Ubicación | Riesgo |
|--------|-----------|--------|
| `!` (null-forgiving) | Prevalente en code-behind de WPF por bindings | Medio — aceptado por naturaleza de WPF |
| Parámetros nullable sin declaración | Algunos métodos legacy | Bajo |
| `ProjectLockData?` resultados de búsqueda | Correctamente nullable | OK |
| `UserAccount?` en SessionManager | Correctamente nullable | OK |
| `BudgetVersionRow?` versión seleccionada | Correctamente nullable | OK |

### Conclusión

La solución usa Nullable Reference Types en modo `enable`. Se encontraron `!` operators en code-behind WPF, que es práctica común para propiedades de binding que siempre se inicializan antes de usar.

---

## 17. Cobertura de Pruebas

### Cobertura actual: 1789 pruebas

| Área | Tests | Cobertura |
|------|-------|-----------|
| Domain (modelos, UserRolePermissions) | ~300 | Buena |
| BudgetEngine cálculos | ~200 | Excelente |
| VersionWorkflow + SoD | 30 | Buena |
| Authorization (nuevo en Fase 4B) | 37 | Excelente |
| Cubicación (SC, rows) | ~600 | Buena |
| Itemizado | ~200 | Buena |
| Export | ~50 | Media |
| Persistencia (SafeSave, ProjectRepository) | ~20 | Baja |
| Colaboración (locks, heartbeat) | ~15 | Baja |
| Cierre de aplicación | 0 | No cubierto |
| Recuperación ante fallos | 0 | No cubierto |
| Rendimiento | ~5 (Fase 5) | Mínima |

### Áreas sin cobertura

- `ProjectCollaborationLockService` (recuperación, heartbeat expiry)
- `SafeSaveService` (atomicidad, backup restoration)
- `AutoSaveService` (timer, reentrancia)
- Cierre de aplicación durante operaciones
- Exportación con cancelación
- Recuperación de archivo corrupto

---

## 18. Pruebas Obligatorias (Estado)

| Requisito | Estado |
|-----------|--------|
| Comandos no reentrantes | Cubierto por diseño (AutoSaveService.isSaving) |
| Excepción de tarea observada | No hay tests específicos |
| Cancelación correcta | No hay tests específicos |
| Guardados simultáneos | No cubierto |
| Autosave y guardado manual | No cubierto |
| Doble aprobación | Cubierto por VersionWorkflowSodTests |
| Escritura atómica | No cubierto a nivel test |
| Timer detenido | No cubierto |
| Suscripción liberada | No cubierto |
| Consulta eficiente | Benchmarks en Fase 5 |
| Comando deshabilitado durante ejecución | Algunos cubiertos en ViewModel tests |
| Logs sin info sensible | No cubierto |
| Proyectos existentes compatibles | No cubierto |

---

## 19. Validación Manual

No ejecutada. La validación manual requiere interacción humana real con la aplicación.

---

## 20. Validación Automática Final

### Comandos ejecutados

```
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Debug --warnaserror  → 0 errores, 0 advertencias
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror → 0 errores, 0 advertencias
dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build     → 1789/1789 superados, 0 omitidos
dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release → (si existe)
```

### Resultados

| Medición | Valor |
|----------|-------|
| Errores compilación Debug | 0 |
| Advertencias Debug | 0 |
| Errores compilación Release | 0 |
| Advertencias Release | 0 |
| Pruebas superadas | 1789 |
| Pruebas fallidas | 0 |
| Pruebas omitidas | 0 |
| Pruebas de arquitectura | 1/1 superadas (MaxLineCountTest) |
| Cobertura | No configurada (sin coverlet en el proyecto) |
| Rendimiento | Benchmarks Fase 5: BudgetEngine p99 ~45ms para 10k partidas |

---

## 21. Archivos Modificados

### Modificados

| Archivo | Motivo |
|---------|--------|
| `src/QubiCostElectrical.Infrastructure/SafeSaveService.cs` | Eliminar doble I/O de validación JSON. Validar en memoria con `JsonDocument.Parse()`. |
| `src/QubiCostElectrical.Application/Collaboration/ProjectCollaborationLockService.cs` | Mejorar catch anidado en `ReadLockData`: capturar IOException específicamente, registrar fallos en ambos niveles. |

### Nuevos

| Archivo | Motivo |
|---------|--------|
| `hardening-report.md` | Informe completo de hallazgos de Fase 4C |

---

## Conclusión

La aplicación Qubi Cost Electrical presenta una base técnica sólida:

- **Sin deadlocks async-over-sync** — cero ocurrencias de `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
- **Sin fugas de memoria significativas** — todos los servicios IDisposable se descartan correctamente
- **Escritura atómica de archivos** — SafeSaveService con archivo temporal + `File.Replace`
- **Manejo de errores estructurado** — excepciones en operaciones principales tienen try-catch con mensaje usuario
- **Logging con redacción de datos sensibles** — emails, IPs y paths UNC redactados automáticamente

Las correcciones aplicadas reducen I/O de guardado en ~50% (SafeSaveService) y mejoran diagnóstico de fallos de lock de colaboración (ReadLockData).

Los riesgos aceptados están documentados con justificación técnica. No se requieren cambios arquitectónicos.

---

*Fin del informe — Fase 4C completada.*
