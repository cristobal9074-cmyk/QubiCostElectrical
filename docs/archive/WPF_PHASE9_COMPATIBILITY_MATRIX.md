# Fase 9 — Matriz de compatibilidad

## Plataformas

| Plataforma | Framework-dependent | Self-contained |
|---|---|---|
| Windows 10 x64 | ✓ | ✓ |
| Windows 11 x64 | ✓ | ✓ |
| Windows Server 2022 | ✓ | ✓ |
| Windows ARM64 (x64 emulado) | ✓ | ✓ |

## Dependencias de Fase 9

| Dependencia | Versión | Licencia | Propósito |
|---|---|---|---|
| .NET 8.0 | 8.0.x | MIT | Runtime |
| System.Text.Json | (Framework) | MIT | Serialización JSON de logs y locks |

Sin dependencias nuevas de terceros. Todas las bibliotecas son parte del framework o ya estaban referenciadas.

## Compatibilidad con Fase 8

| Componente Fase 8 | Compatible Fase 9 | Notas |
|---|---|---|
| DeliveryView/DeliveryViewModel | ✓ | Sin cambios |
| QuestPDF/ClosedXML/OpenXML | ✓ | Sin cambios |
| IFileDialogService/WpfFileDialogService | ✓ | Sin cambios |
| IFileOpenService/WpfFileOpenService | ✓ | Sin cambios |

## Compatibilidad con módulos existentes

| Módulo | Compatible | Notas |
|---|---|---|
| Domain (fórmulas, cálculos) | ✓ | Sin cambios |
| Application (servicios) | ✓ | Sin cambios |
| ProjectRepository | ✓ | SafeSaveService delegado internamente, interfaz sin cambios |
| SQLite (WAL, Serializable) | ✓ | Sin cambios |
| WinForms | ✓ | Sin cambios |

## Configuración regional

| Cultura | Formato | Soporte |
|---|---|---|
| es-CL | Fechas, números, moneda CLP/UF | ✓ |
| es-ES | Formato europeo | ✓ |
| en-US | Formato inglés | ✓ |
| Otras | Formato invariante | ✓ |

## Limitaciones conocidas

- Lock no funciona entre plataformas (Linux ↔ Windows) — heartbeat usa `Process.GetProcessById` específico de SO
- El autoguardado no se ejecuta si la sesión no está marcada como dirty
- SafeSaveService no protege contra fallos de hardware a nivel de sector de disco
