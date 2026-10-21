# Qubi Cost Electrical — Guía para agentes

## Raíz del proyecto

`C:\Users\cristobal1\Desktop\QubiCost Electrical`

## Producto

- **Nombre comercial:** Qubi Cost Electrical
- **Ejecutable:** `QubiCostElectrical.exe`
- **Formato de proyecto:** `.qce` (SQLite)
- **UI:** WPF (`src/QubiCostElectrical.Wpf`) — WinForms retirado
- **Versión vigente:** ver `Directory.Build.props` y `docs/PRODUCT.md` (`1.1.1`)

## Comandos

```powershell
Set-Location "C:\Users\cristobal1\Desktop\QubiCost Electrical"
dotnet build QubiCostElectrical.Wpf.sln -c Release
dotnet test QubiCostElectrical.Wpf.sln -c Release
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
.\scripts\Validate-Release.ps1
.\scripts\Verify-ProductIdentity.ps1
```

## Reglas de la suite de tests

- **La suite es hermética: nunca lee ni escribe archivos fuera del repositorio.**
  En concreto, **nunca toca los `.qce` de obra reales** que están en la carpeta de trabajo del
  usuario. Un test que haya hecho eso (`SyncAndVerifyPel039A2026Prueba2`) reescribía
  presupuestos de cliente en cada `dotnet test`.
- **Para leer un proyecto de obra real**, un test debe pasar por
  `QubiCostElectrical.Tests.Fixtures.LiveProjectGate`. Está **cerrado por defecto** y se
  habilita con la variable de entorno `QUBI_LIVE_PROJECTS=1` (raíz configurable con
  `QUBI_LIVE_PROJECTS_ROOT`, libro Excel con `QUBI_LIVE_EXCEL_ROOT`).
  Aun con la compuerta abierta, el test debe trabajar sobre una **copia en directorio
  temporal**, nunca sobre el archivo original.
- **`tests/ArchitectureTests/NoLivePathGuardTest.cs` falla el build** si reaparece una ruta
  absoluta de máquina del desarrollador dentro de `tests/`. No lo borres: es la red de
  seguridad que impide repetir el incidente.
- **La calibración de un proyecto de obra se hace con la herramienta manual**
  `tools/LiveProjectCalibration`, no con un test. Es *dry-run* por defecto y exige `--apply`
  para escribir; deja una copia `.precalibration` del destino.
- **No correr dos procesos de test a la vez en la misma máquina.** Los tests de pegado
  usan el portapapeles de Windows, que es un recurso global: en paralelo, uno se queda
  bloqueado y la corrida se cuelga o se trunca. Ejecuta los proyectos de test de a uno:
  `dotnet test tests/QubiCostElectrical.Tests/QubiCostElectrical.Tests.csproj` y después
  `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`.

## Arquitectura

- **QubiCostElectrical.Domain / Application / Infrastructure:** negocio, SQLite, cálculos, import/export.
- **QubiCostElectrical.Wpf:** UI WPF; pantallas en `Features/{Modulo}/`.
- **tests/QubiCostElectrical.Tests:** pruebas unitarias.

## Diseño visual

- Tokens: `src/QubiCostElectrical.Wpf/DesignSystem/*.xaml`
- Shell: cabecera oscura + navegación horizontal + barra contextual + contenido ancho completo.

## Módulos implementados (WPF)

Shell, dashboard, proyecto, itemizado, cubicación, cubicación SC, APU, banco de precios, cotizaciones, lista de compra, revisión, versiones, oferta y documentos (wizard), documentos, preferencias, órdenes de cambio.

## Distribución

- Build local: `.\deploy\build-release.ps1` (Velopack)
- Firma Authenticode: `.\deploy\Sign-Authenticode.ps1` con `CODE_SIGNING_CERT_PFX` y `CODE_SIGNING_CERT_PASSWORD`
- Proceso de release: `docs/RELEASE_PROCESS.md`
- Smoke test humano: `docs/SMOKE_TEST.md`

## Licencias de terceros

- **QuestPDF:** Community si ingresos anuales &lt; USD 1M; configurar `QuestPDF.Settings.License` en `QuestPdfBootstrap`. Para licencia comercial, variable `QUESTPDF_LICENSE=Professional`.
- Detalle operativo en `docs/RELEASE_PROCESS.md` sección Licencias.

## Roles y permisos

| Rol | Editar presupuesto | Exportar | Banco de precios | Preferencias globales |
|-----|-------------------|----------|------------------|----------------------|
| Administrador | Sí | Sí | Sí | Sí |
| Estimador | Sí | Sí | Sí | No |
| Visor | No (solo lectura) | No | No | No |

Política en `UserRolePermissions` (Domain). La sesión aplica restricciones vía `RoleSessionSync` al iniciar.

## Arquitectura de permisos

- `IPermissionService` / `UserRolePermissionService` en Application — única puerta de permisos.
- WPF expone la instancia en `WpfAppHost.Permissions`; `SessionManager` delega para compatibilidad.
- ViewModels consultan `host.Permissions`, no `UserRolePermissions` directamente.

## Navegación principal

- **Oferta y exportación** (`ShellRoute.OfferDocuments`): flujo único Revisión → Configurar → Vista previa → Exportar (menú Cierre; sin entrada duplicada en barra principal).
- **Cubicación SC** (`ShellRoute.CubicacionSc`): tableros y circuitos de servicios comunes en presupuesto.

## Documentación

- Fuente de verdad: este archivo y `docs/PRODUCT.md` (si existe).
- Informes históricos de fases WPF/WinForms purgados (2026-10-20); el detalle de releases vive en `CHANGELOG.md`.
- Operativa: `docs/RELEASE_PROCESS.md`, `docs/SMOKE_TEST.md`, `docs/UI_SCREEN_CONTRACT.md`.

## Alcance multiusuario

- **Colaboración por archivo en carpeta compartida (SMB/UNC):** un editor exclusivo por `.qce`; los demás abren en solo lectura.
- `ProjectCollaborationLockService` mantiene bloqueo OS (`FileShare.None`) + metadatos de usuario (nombre, correo, equipo) en `{proyecto}.qce.lock`.
- Heartbeat cada 30 s; bloqueo expirado (5 min sin respuesta) puede tomarse con confirmación explícita.
- **No** hay edición simultánea de dos usuarios en el mismo proyecto; **no** hay servidor central ni merge automático.
- Auditoría persistente en `project_audit_log` (esquema metadata-driven; última migración `Migration034` — ver `SQLiteSchemaMigrator`): aperturas, guardados, ediciones por módulo y cambios de partida.
- Pestaña **Actividad y auditoría** en Proyecto muestra el historial del archivo.
- Al guardar se actualiza `ProjectInfo.UsuarioNombre` / `UsuarioRol` (último responsable de persistencia).
- Vendible como **equipo con carpeta de red** (1 editor + N consultores), no como Google Docs ni ERP multiusuario en tiempo real.

## Reglas

- **Restricción de Red:** No utilices herramientas de búsqueda web (`search_web` o `read_url_content`) ni consumas recursos externos de internet a menos que el usuario te lo solicite explícitamente. Trabaja únicamente con el código fuente local, la documentación interna y tus conocimientos base.
- No cambiar SQLite, migraciones, cálculos ni fórmulas sin tarea explícita.
- Lógica de negocio en Domain/Application, nunca en code-behind de vistas.
- No reintroducir nombres `PresupuestoElectrico` en scripts operativos ni UI visible.
- Documentación histórica en `docs/WPF_*` puede mencionar WinForms; la fuente de verdad operativa es este archivo y `CLAUDE.md`.
- **Publicación y Pruebas del Usuario:**
  - El usuario abre y prueba la aplicación exclusivamente a través del acceso directo en el escritorio: `C:\Users\cristobal1\Desktop\Qubi Cost Electrical.lnk` (que apunta a `publish\win-x64\QubiCostElectrical.exe`).
  - **SIEMPRE que se realicen cambios de código, se DEBE compilar y publicar en `publish\win-x64`** para que el usuario pruebe la versión correcta.
  - El proyecto activo que el usuario utiliza como referencia para validar es: `C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026 ABIERTO.qce`.

