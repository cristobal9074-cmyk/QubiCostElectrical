# Fase 9 — Informe de seguridad de archivos

## Flujo de guardado actual

```
Usuario → SaveProjectAsync → ProjectWorkspace.SaveAsync → ProjectRepository.SaveProjectFileAsync
                                                                    ↓
                                                          SafeSaveService.SaveJsonFileAsync
                                                                    ↓
                                                    1. Serializar a JSON en archivo temporal (.tmp)
                                                    2. Leer y validar JSON deserializando
                                                    3. Si archivo existe: File.Replace (atómico NTFS)
                                                       + backup en .backups/ antes del replace
                                                    4. Si archivo nuevo: File.Move
```

## Garantías

| Garantía | Mecanismo |
|---|---|
| **Atomicidad** | `File.Replace` es atómico en NTFS; archivo original nunca se pierde si falla |
| **Validación** | JSON se escribe, se lee, se deserializa antes de reemplazar |
| **Consistencia** | Si serialización o validación falla, archivo temporal se limpia, original intacto |
| **Backup** | Backup automático antes de cada reemplazo en `.backups/{nombre}_{timestamp}.bak` |
| **Rotación** | Máximo 10 backups por proyecto; los más antiguos se eliminan |
| **Limpieza** | Temporal se elimina en éxito y en fallo; nunca queda basura |

## Flujo de autoguardado

```
Timer (60s) → snapshot → SafeSaveService.SaveJsonFileAsync → {proyecto}.pepro.autosave
                                                                    ↓
                                              Eliminado en guardado manual exitoso
                                                                    ↓
                                              Detectado al abrir proyecto si es más reciente
```

## Flujo de lock

```
Abrir proyecto → CheckLock (archivo .lock) → ¿Misma máquina? → ¿Proceso vivo?
                                                                    ↓
                                              Libre → TryAcquireLock (crea .lock)
                                              Misma máquina vivo → ofrecer solo lectura
                                              Misma máquina muerto → ofrecer forzar
                                              Otra máquina → ofrecer forzar
                                                                    ↓
                                        Heartbeat cada 30s (actualiza LastHeartbeatUtc)
                                                                    ↓
                                        ReleaseLock al cerrar proyecto
```

## Diagrama de estados del guardado

```
Estado inicial → ¿Archivo existe?
    ├── Sí → Backup → File.Replace(tmp, original, backup) → Éxito
    │         └── Falla backup → continua sin backup → File.Replace
    └── No  → File.Move(tmp, path) → Éxito

Falla en cualquier paso:
    ├── Temporal limpio
    ├── Archivo original intacto
    └── Resultado con IsSuccess=false y ErrorMessage
```
