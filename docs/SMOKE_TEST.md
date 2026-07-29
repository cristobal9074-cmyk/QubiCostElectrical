# Smoke Test Checklist — Qubi Cost Electrical

> Checklist obligatorio antes de marcar una release como estable o comercial (GA).
> Versión de referencia: la de `Directory.Build.props` (ej. `1.0.0`).

## Smoke automatizado

Ejecutar antes de la validación humana:

```powershell
Set-Location "C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1"
.\scripts\Run-SmokeAutomated.ps1
```

El script verifica identidad, build Release, suite completa de tests, gates Fase 1–3, navegación unificada, roundtrip `.qce` y golden-file Excel. El último resultado queda en `docs/SMOKE_TEST_AUTOMATED_LAST_RUN.txt`.

### Última ejecución automatizada

| Campo | Valor |
|-------|-------|
| Fecha | 2026-07-02 |
| Script | `dotnet test QubiCostElectrical.Wpf.sln -c Release` |
| Configuración | Release |
| Build | OK (0 errores) |
| Tests funcionales | OK (**1505/1505**) |
| Import Excel sucios (`ItemizadoDirtyExcelImportTests`) | OK (10 escenarios) |
| Resumen import (`ItemizadoImportSummaryTests`) | OK |
| Auth admin audit/unlock (`AuthAdminAuditTests`) | OK |
| Política PIN (`AuthPinPolicyTests`) | OK |
| Evidencia | `docs/SMOKE_TEST_AUTOMATED_LAST_RUN.txt` |

## Bloque auth + import (smoke humano sugerido)

- [ ] Preferencias → Usuarios: crear cuenta, cambiar rol (F2), desactivar, reset PIN, desbloquear (Ctrl+U / menú contextual)
- [ ] Preferencias → Política PIN: guardar longitudes y flags; crear usuario con PIN débil → debe rechazar
- [ ] Preferencias → Auditoría: tras cada acción admin aparece entrada persistente (reiniciar app y verificar)
- [ ] Itemizado → Importar Excel: archivo con filas rechazadas/advertencias → resumen post-import cuadra con preview
- [ ] Itemizado → Pegar: pegado con duplicados WBS → preview y resumen coherentes

### Última ejecución automatizada (histórico 2026-06-28)
| Script | `dotnet test QubiCostElectrical.Wpf.sln` |
| Configuración | Debug |
| Identidad (`Verify-ProductIdentity -Strict`) | OK |
| Build | OK (0 errores) |
| Tests funcionales | OK (1357/1358 — 1 performance test flaky bajo carga paralela) |
| Gate Fase 1 (`WpfPhase1GateTests`) | OK |
| Gate Fase 2 (`WpfPhase2GateTests`) | OK |
| Gate Fase 3 (`WpfPhase3GateTests`) | OK |
| Navegación unificada (`ShellRouteNavigationTests`) | OK |
| Persistencia `.qce` (`SQLiteProjectRoundTripTests`) | OK |
| Export Excel golden-file | OK |
| T1 (SobreVenta markup) | Implementado y verificado |
| T2 (WAL UNC block) | Implementado |
| T4 (IVA default true) | Implementado y verificado |

## Precondiciones

- [ ] Tag `v*` creado y coincide con `Directory.Build.props`
- [ ] `.\scripts\Validate-Release.ps1` pasó sin errores *(requiere repo limpio; omitir si hay cambios locales pendientes de commit)*
- [x] `.\scripts\Verify-ProductIdentity.ps1 -Strict` pasó sin errores
- [ ] Artefactos descargados del GitHub Release o generados con `.\deploy\build-release.ps1 -Sign`

## Instalación

> Validación humana — no cubierta por smoke automatizado.

- [ ] El instalador (`QubiCostElectrical-Setup-*.exe`) se ejecuta sin errores
- [ ] Windows SmartScreen no bloquea (binario firmado con Authenticode)
- [ ] La carpeta de instalación contiene `QubiCostElectrical.exe`
- [ ] La aplicación se inicia desde acceso directo
- [ ] Asociación `.qce` abre el proyecto en la app

## Funcionalidad básica

> Validación humana — flujo GUI con proyecto `.qce` real.

- [ ] Splash y login cargan sin errores visibles
- [ ] Dashboard muestra KPIs y acciones
- [ ] Proyecto nuevo se crea correctamente (`.qce`)
- [ ] Proyecto existente se carga desde archivo
- [ ] Proyecto se guarda y recarga sin pérdida de datos
- [ ] Cerrar sesión cierra el proyecto abierto
- [ ] Undo disponible tras guardar (historial reiniciado)

## Módulos principales

> Navegación unificada: **Cierre → Oferta y exportación** (entrada única; sin botón duplicado en barra principal).

- [ ] Navegación: Proyecto → Itemizado → Cubicación → APU → Precios → Revisión → **Oferta y exportación** *(menú Cierre)*
- [ ] Stepper «Línea de producción» visible con proyecto abierto; C.SC navegable
- [ ] Versiones: comparar V1 vs V2 (A/B) y vs estado actual; «Ir a partida» desde delta
- [ ] Itemizado: agregar, editar, importar, pegar (con guardado manual tras import)
- [ ] Itemizado — atajos: Alt+↑/↓ mover fila, Supr eliminar, Ctrl+V pegar con vista previa
- [ ] Itemizado — columna «Excl. cubic.» persiste tras guardar/recargar
- [ ] Itemizado — eliminar capítulo con partidas no deja APUs huérfanos
- [ ] Itemizado — búsqueda rápida «Crear nueva partida» agrega fila en Itemizado
- [ ] Itemizado — atajos shell Alt+1…9 navegan a módulos principales
- [ ] Cubicación: editar filas, fórmulas, comentarios
- [ ] Cubicación — **eliminar recurso**: fila desaparece de grilla y no reaparece tras guardar/recargar (capas normalizadas sincronizadas)
- [ ] Cubicación — **eliminar partida en Itemizado**: solo borra cubicación de esa partida; otras partidas conservan sus recursos
- [ ] Cubicación — **barra de fórmulas**: escribir `10+2`, Enter → cantidad 12 y fórmula persiste tras guardar/recargar
- [ ] Cubicación — **pegar desde Excel**: columna CANT anclada actualiza cantidad o fórmula según contenido
- [ ] Cubicación — **partida excluida** (columna «Excl. cubic.» en Itemizado): aparece en lista pero grilla en solo lectura; navegación desde Itemizado muestra aviso
- [ ] Cubicación — **Ir a cubicar** desde Itemizado: selecciona la partida correcta; contexto de navegación se limpia al aplicar
- [ ] Cubicación — **solo lectura** (rol Visor o partida excluida): no permite editar celdas ni barra de fórmulas
- [ ] Cubicación — **filas vacías de relleno** (8 filas inferiores): editar una fila vacía la incorpora al proyecto; estado vacío solo si no hay recursos con contenido
- [ ] Cubicación — **Undo** tras editar cantidad/fórmula: deshacer restaura valor anterior
- [ ] Banco de precios: búsqueda de materiales
- [ ] Revisión: blockers visibles antes de exportar
- [ ] Documentos: sync planos/exclusiones marca proyecto dirty
- [ ] Exportación Excel genera archivo válido
- [ ] Exportación PDF genera archivo válido (oferta + memoria cubicación)

## Estabilidad

> Validación humana.

- [ ] Sin excepciones no controladas en uso normal (Release)
- [ ] La ventana se redimensiona correctamente
- [ ] El título muestra versión o nombre correcto del producto
- [ ] Cerrar y reabrir la app preserva preferencias

## Integridad de release

- [ ] `git status --porcelain` vacío en el commit del tag
- [ ] SHA-256 de artefactos coincide con `artifacts/SHA256SUMS.txt` o `build-manifest.json`
- [x] Tests: `dotnet test QubiCostElectrical.Wpf.sln` — 1357/1358 OK (2026-06-28; 1 performance test flaky bajo carga paralela, pasa en aislamiento)
- [x] Tests Cubicación: `dotnet test --filter "FullyQualifiedName~Cubicacion"` — 176/176 OK (2026-07-01; C-Sprints 1–3)

## Firma y licencias

- [ ] Ejecutable e instalador firmados (`signtool verify /pa`)
- [x] QuestPDF: elegibilidad Community confirmada (< USD 1M ingresos) — configurado en `QuestPdfBootstrap`

## Aprobación

| Campo | Valor |
|-------|-------|
| Versión probada | 1.0.0 |
| Artefacto (SHA-256) | Pendiente — requiere build de instalador firmado |
| Probado por | Smoke automatizado (agente) + pendiente validación humana GUI |
| Fecha | 2026-06-28 |
| Resultado | **PASS parcial** — automatizado OK (T1/T2/T4 integrados, 1357/1358 tests); GUI/instalador pendiente (requiere EV cert) |
