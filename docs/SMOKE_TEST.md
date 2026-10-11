# Smoke Test Checklist - Qubi Cost Electrical

Checklist obligatorio antes de marcar una release como estable o comercial (GA).

**Versión de referencia:** `1.1.1` (`Directory.Build.props` / `docs/PRODUCT.md`).

Scripts de demo y onboarding relacionados:

- `docs/DEMO_SCRIPT_15MIN.md` (demo comercial ~15 min)
- `docs/FIRST_RUN_TEAM_ONBOARDING.md` (sesión Admin + Estimador + Visor, 20 min)

---

### Firma Semana A (humano obligatorio)

Registro oficial: ejecutar `.\scripts\Record-SmokeSemanaA.ps1`. Esta firma humana no se marca a mano en este markdown salvo contingencia; el script actualiza esta sección y la sección **Última ejecución**.

| Campo | Valor |
|-------|-------|
| Fecha | 2026-09-08 |
| Probador | Cristóbal |
| Build | `publish\win-x64\QubiCostElectrical.exe` / versión 1.1.1 |
| Resultado | [x] PASS · [ ] FAIL |

**Bloques mínimos a firmar (45–60 min):**

1. [x] Cubicación completa (puntos 1–14 arriba, o criterio P0 compacto)
2. [x] Cubicación SC básico (puntos 1–10)
3. [x] Roles: Admin / Estimador / Visor (Visor no edita ni exporta; Estimador edita; Admin completo)
4. [x] **Cerrar con X** tras editar hace &lt;2,5 s: reabrir `.qce` conserva el último cambio (flush al salir)
5. [x] Oferta: **Generar oferta →** muestra overlay «Generando documentos…» y deshabilita el botón

**Automatizado (no firma humana):** `dotnet test QubiCostElectrical.Wpf.sln -c Release` — suite completa.

**Pendiente comercial frío:** instalador / firma Authenticode (fuera de este ciclo).

---

## Última ejecución

El estado humano de **Semana A** se registra con `.\scripts\Record-SmokeSemanaA.ps1`. No marcar PASS humano sin evidencia del probador.

| Campo | Valor |
|-------|-------|
| Fecha | 2026-09-08 |
| Versión | 1.1.1 |
| Script | `dotnet test QubiCostElectrical.Wpf.sln -c Release` |
| Tests funcionales | OK (**1821/1821**) |
| Smoke humano Semana A | **PASS** · Probador: Cristóbal · Fecha: 2026-09-08 |
| Instalador | Fuera de este ciclo |

Conteo verificado con `dotnet test --list-tests` (Release, 2026-08-25). Suite completa: `dotnet test QubiCostElectrical.Wpf.sln -c Release`.

---

## Semana A - Confianza (humano, 45–60 min)

Validación GUI con un `.qce` real. No sustituye la suite automatizada. Tiempo objetivo: 45–60 min. Anotar PASS/FAIL en cada casilla.

**Precondiciones**

- App arranca desde el launcher canónico (`publish\win-x64\QubiCostElectrical.exe` o acceso directo actualizado).
- Cuenta **Administrador** conocida. Tras el bloque de roles, cuentas **Estimador** y **Visor**.
- Excel o una hoja con una columna `CANT` (número o fórmula) para el pegado.

### Cubicación (completo)

Reutiliza los criterios P0 (persistencia, `10+2`, pegar CANT, Ctrl+Z, Visor solo lectura) como script numerado. Incluye guardar, cerrar y reabrir.

1. [ ] Login: ventana **Iniciar sesión** → PIN. Abrir shell.
2. [ ] **Nuevo proyecto** (Ctrl+N o tarjeta en **Resumen**). Diálogo **Nuevo proyecto**: nombre `Smoke Cub 111`, cliente y carpeta destino. Confirmar. Queda un `.qce`.
3. [ ] Barra: **Itemizado**. **+ Partida** (2 partidas mínimas). Completar código y descripción.
4. [ ] Barra: **Cubicación** → **Cubicación**. Seleccionar la primera partida en el panel izquierdo.
5. [ ] Vista **Detalle**. **Agregar material** (o autocompletar **CÓDIGO**). Completar **DESCRIPCIÓN** y **UD**.
6. [ ] Vista **Casillas**. En una casilla de cantidad escribir `10+2` y Enter. Resultado visible = **12**. (Las casillas admiten fórmulas `2+3`, `10/2`; barra de estado con chip `fx`.)
7. [ ] Volver a **Detalle**. Columna **CANT** muestra 12 (o el total coherente). La fórmula no se pierde al cambiar de vista.
8. [ ] Cabecera: **Guardar** (o Ctrl+S). Toast de guardado si aplica.
9. [ ] Selector de proyecto: **Cerrar proyecto**. Luego **Abrir proyecto…** el mismo `.qce`.
10. [ ] **Cubicación** → misma partida: cantidad **12** y fórmula `10+2` intactas.
11. [ ] Pegar Excel: copiar una celda con `8` o `3+1` bajo encabezado **CANT**. En la grilla, **Pegar desde Excel** o Ctrl+V. Confirmar preview. Columna **CANT** actualiza cantidad o fórmula según el contenido.
12. [ ] Editar una celda de cantidad o código. **Ctrl+Z** (o botón Deshacer del header). Valor anterior restaurado.
13. [ ] **Cerrar sesión**. Login como **Visor**. Abrir el mismo `.qce`. Grilla y barra de fórmulas **no editables**.
14. [ ] **Cerrar sesión**. Login como **Estimador**. La misma grilla **sí** permite editar.

Criterio P0 compacto (si el tiempo aprieta, no omitir 8–10 ni 13):

- [ ] Persistencia: editar cantidad/fórmula → Guardar → Cerrar proyecto → reabrir → valores intactos
- [ ] Fórmulas: Casillas `10+2` Enter → cantidad 12; visible tras recargar
- [ ] Pegar: Ctrl+V / **Pegar desde Excel** en **CANT**
- [ ] Undo: Ctrl+Z restaura
- [ ] Visor: solo lectura; Estimador: edición OK

### Cubicación SC básico

Pantalla: **Cubicación** → **Cubicación SC** (`CubicacionScView`). Título: **Cubicación de Servicios Comunes (C.SC)**.

1. [ ] Con el proyecto smoke abierto (Estimador o Admin), ir a **Cubicación SC**.
2. [ ] **+ Agregar tablero**. Queda un cuadro en **Cuadros de servicios comunes**.
3. [ ] **+ Agregar circuito**. Completar al menos un circuito en **2 · PLANILLA DE CIRCUITOS**.
4. [ ] **Guardar**.
5. [ ] Ir a **Cubicación** (toma de partidas). Seleccionar una partida y una fila de recurso (o fila vacía lista para tomar).
6. [ ] **Alt+L** (acción **Tomar de SC**). Se abre **Tomar de cubicación SC** (`ResumenScTomarDialog`), subtítulo «Tomar material de servicios comunes».
7. [ ] Elegir un material. **Tomar seleccionado**. La cantidad aparece en la partida/fila activa.
8. [ ] **Guardar**. Cerrar y reabrir el proyecto. La cantidad tomada sigue en la partida.
9. [ ] **Cierre** → **Oferta y exportación**. Wizard: **1. Validar oferta** → **2. Configurar** → **3. Vista previa** → **4. Exportar**.
10. [ ] En el paso Exportar: **Exportar PDF** (oferta comercial) o **Exportar Excel**. Archivo válido en disco.

Atajo de referencia: catálogo Cubicación, `Tomar de SC` = Alt+L.

### Roles (sesión onboarding 20 min)

Detalle minuto a minuto: `docs/FIRST_RUN_TEAM_ONBOARDING.md`. Aquí el gate de smoke:

1. [ ] Login Admin. **Más** → **Preferencias** (o **Preferencias globales**). Barra: **Usuarios**.
2. [ ] Sección **USUARIOS Y PERMISOS**. Crear **Estimador** (combo `Estimator` / etiqueta Estimador) y **Visor** (`Viewer`). Botón **Crear usuario**. PIN 4–6 dígitos.
3. [ ] **Cerrar sesión**. Login Estimador. Abrir o crear `.qce`. Editar Itemizado o Cubicación. **Guardar**. OK.
4. [ ] **Cerrar sesión**. Login Visor. Mismo proyecto: no edita celdas; no exporta (oferta/PDF/Excel bloqueados). Mensaje de rol: solo consulta.
5. [ ] Verificar explícitamente que **Visor no edita ni exporta**: intento de editar celda rechazado o bloqueado y acciones de exportación no disponibles o deshabilitadas.

---

## Smoke automatizado

Ejecutar antes de la validación humana:

```powershell
Set-Location "C:\Users\cristobal1\Desktop\QubiCost Electrical"
.\scripts\Run-SmokeAutomated.ps1
```

El script verifica identidad, build Release, suite de tests, gates Fase 1–3, navegación unificada, roundtrip `.qce` y golden-file Excel. El último resultado queda en `docs/SMOKE_TEST_AUTOMATED_LAST_RUN.txt`.

Conteo actual de tests: **2501** en total (Release, 2026-10-11: 2499 superados, 1 omitido, 1 fallo flaky de umbral de tiempo en `ApuPerformanceTests`).

### Histórico automatizado

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

### Histórico 2026-06-28

| Script | `dotnet test QubiCostElectrical.Wpf.sln` |
| Configuración | Debug |
| Identidad (`Verify-ProductIdentity -Strict`) | OK |
| Build | OK (0 errores) |
| Tests funcionales | OK (1357/1358; 1 performance test flaky bajo carga paralela) |
| Gate Fase 1 (`WpfPhase1GateTests`) | OK |
| Gate Fase 2 (`WpfPhase2GateTests`) | OK |
| Gate Fase 3 (`WpfPhase3GateTests`) | OK |
| Navegación unificada (`ShellRouteNavigationTests`) | OK |
| Persistencia `.qce` (`SQLiteProjectRoundTripTests`) | OK |
| Export Excel golden-file | OK |
| T1 (SobreVenta markup) | Implementado y verificado |
| T2 (WAL UNC block) | Implementado |
| T4 (IVA default true) | Implementado y verificado |

---

## Bloque auth + import (smoke humano sugerido)

- [ ] Preferencias → Usuarios: crear cuenta, cambiar rol (F2), desactivar, reset PIN, desbloquear (Ctrl+U / menú contextual)
- [ ] Preferencias → Política PIN: guardar longitudes y flags; crear usuario con PIN débil → debe rechazar
- [ ] Preferencias → Auditoría: tras cada acción admin aparece entrada persistente (reiniciar app y verificar)
- [ ] Itemizado → Importar Excel: archivo con filas rechazadas/advertencias → resumen post-import cuadra con preview
- [ ] Itemizado → Pegar: pegado con duplicados WBS → preview y resumen coherentes

## Precondiciones

- [ ] Tag `v*` creado y coincide con `Directory.Build.props`
- [ ] `.\scripts\Validate-Release.ps1` pasó sin errores *(requiere repo limpio; omitir si hay cambios locales pendientes de commit)*
- [x] `.\scripts\Verify-ProductIdentity.ps1 -Strict` pasó sin errores
- [ ] Artefactos descargados del GitHub Release o generados con `.\deploy\build-release.ps1 -Sign`

## Instalación (fuera de este ciclo)

El instalador (`QubiCostElectrical-Setup-*.exe`), SmartScreen, asociación `.qce` del setup y firma Authenticode del paquete de instalación **no entran en Semana A**. Quedan pendientes de GA. El smoke de este ciclo usa el exe publish / acceso directo.

Checklist histórico (no ejecutar ahora):

- [ ] El instalador (`QubiCostElectrical-Setup-*.exe`) se ejecuta sin errores
- [ ] Windows SmartScreen no bloquea (binario firmado con Authenticode)
- [ ] La carpeta de instalación contiene `QubiCostElectrical.exe`
- [ ] La aplicación se inicia desde acceso directo
- [ ] Asociación `.qce` abre el proyecto en la app

## Funcionalidad básica

> Validación humana. Flujo GUI con proyecto `.qce` real.

- [ ] Splash y login cargan sin errores visibles
- [ ] Dashboard (**Resumen**) muestra KPIs y acciones
- [ ] Proyecto nuevo se crea correctamente (`.qce`)
- [ ] Proyecto existente se carga desde archivo
- [ ] Proyecto se guarda y recarga sin pérdida de datos
- [ ] Cerrar sesión cierra el proyecto abierto y vuelve a login (no cierra Windows de la app)
- [ ] Undo disponible tras guardar (historial reiniciado)

## Módulos principales

Navegación unificada: **Cierre → Oferta y exportación** (entrada única; sin botón duplicado en barra principal).

- [ ] Navegación: **Proyecto** → **Itemizado** → **Cubicación** → **APU** → **Precios** → **Cierre** → **Oferta y exportación**
- [ ] Stepper «Línea de producción» visible con proyecto abierto; C.SC navegable
- [ ] Versiones: comparar V1 vs V2 (A/B) y vs estado actual; «Ir a partida» desde delta
- [ ] Itemizado: agregar, editar, importar, pegar (con guardado manual tras import)
- [ ] Itemizado. Atajos: Alt+↑/↓ mover fila, Supr eliminar, Ctrl+V pegar con vista previa
- [ ] Itemizado. Columna «Excl. cubic.» persiste tras guardar/recargar
- [ ] Itemizado. Eliminar capítulo con partidas no deja APUs huérfanos
- [ ] Itemizado. Búsqueda rápida «Crear nueva partida» agrega fila en Itemizado
- [ ] Itemizado. Atajos shell Alt+1…9 navegan a módulos principales
- [ ] Cubicación: editar filas, fórmulas, comentarios
- [ ] Cubicación. Eliminar recurso: fila desaparece de grilla y no reaparece tras guardar/recargar
- [ ] Cubicación. Eliminar partida en Itemizado: solo borra cubicación de esa partida
- [ ] Cubicación. Barra / casillas `10+2`, Enter → cantidad 12 y fórmula persiste
- [ ] Cubicación. Pegar desde Excel: columna CANT anclada actualiza cantidad o fórmula
- [ ] Cubicación. Partida excluida: lista visible, grilla solo lectura
- [ ] Cubicación. Ir a cubicar desde Itemizado: selecciona la partida correcta
- [ ] Cubicación. Solo lectura (Visor o partida excluida): no edita celdas ni fórmulas
- [ ] Cubicación. Filas vacías de relleno: editar una las incorpora al proyecto
- [ ] Cubicación. Undo tras editar cantidad/fórmula

### P0 - Cubicación (smoke rápido)

Ver script completo en **Semana A**. Checklist corto:

- [ ] Persistencia: editar cantidad/fórmula → Guardar → cerrar/reabrir → valores intactos
- [ ] Fórmulas: `10+2` Enter → cantidad 12; fórmula visible tras recargar
- [ ] Pegar: Ctrl+V desde Excel en columna CANT
- [ ] Undo: editar celda → Ctrl+Z restaura
- [ ] Logout/sesión: Visor → solo lectura; Estimador → edición OK

- [ ] Banco de precios: búsqueda de materiales
- [ ] Revisión final: bloqueantes visibles antes de exportar
- [ ] Documentos: sync planos/exclusiones marca proyecto dirty
- [ ] Exportación Excel genera archivo válido
- [ ] Exportación PDF genera archivo válido (oferta + memoria cubicación)

## Estabilidad

- [ ] Sin excepciones no controladas en uso normal (Release)
- [ ] La ventana se redimensiona correctamente
- [ ] El título muestra versión o nombre correcto del producto
- [ ] Cerrar y reabrir la app preserva preferencias

## Integridad de release

- [ ] `git status --porcelain` vacío en el commit del tag
- [ ] SHA-256 de artefactos coincide con `artifacts/SHA256SUMS.txt` o `build-manifest.json`
- [x] Tests (histórico): `dotnet test QubiCostElectrical.Wpf.sln`; 1357/1358 OK (2026-06-28)
- [x] Tests Cubicación (histórico): `dotnet test --filter "FullyQualifiedName~Cubicacion"`; 176/176 OK (2026-07-01)
- [x] Tests 2026-08-25: **1821/1821** (Release; incluye `CommercialReadinessSmokeTests`, `ShellNotificationServiceTests`)

## Firma y licencias

- [ ] Ejecutable e instalador firmados (`signtool verify /pa`) - instalador fuera de este ciclo
- [x] QuestPDF: elegibilidad Community confirmada (< USD 1M ingresos). Configurado en `QuestPdfBootstrap`

## Aprobación

| Campo | Valor |
|-------|-------|
| Versión probada | 1.1.1 |
| Artefacto (SHA-256) | `f48e5c192f27355272f8db57615aa56205e68d369a59c589a750f826551cc59b` |
| Probado por | Cristóbal (Smoke Semana A humano + suite automatizada 1821/1821) |
| Fecha | 2026-09-08 |
| Resultado | **PASS** — validación completa (Cubicación / SC / roles / flush persistencia / oferta y export) |
