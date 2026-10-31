# Qubi Cost Electrical â€” GuÃ­a para agentes

## RaÃ­z del proyecto

`C:\Users\cristobal1\Desktop\QubiCost Electrical`

## Producto

- **Nombre comercial:** Qubi Cost Electrical
- **Ejecutable:** `QubiCostElectrical.exe`
- **Formato de proyecto:** `.qce` (SQLite)
- **UI:** WPF (`src/QubiCostElectrical.Wpf`) â€” WinForms retirado
- **VersiÃ³n vigente:** ver `Directory.Build.props` y `docs/PRODUCT.md` (`1.1.7`)

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

- **La suite es hermÃ©tica: nunca lee ni escribe archivos fuera del repositorio.**
  En concreto, **nunca toca los `.qce` de obra reales** que estÃ¡n en la carpeta de trabajo del
  usuario. Un test que haya hecho eso (`SyncAndVerifyPel039A2026Prueba2`) reescribÃ­a
  presupuestos de cliente en cada `dotnet test`.
- **Para leer un proyecto de obra real**, un test debe pasar por
  `QubiCostElectrical.Tests.Fixtures.LiveProjectGate`. EstÃ¡ **cerrado por defecto** y se
  habilita con la variable de entorno `QUBI_LIVE_PROJECTS=1` (raÃ­z configurable con
  `QUBI_LIVE_PROJECTS_ROOT`, libro Excel con `QUBI_LIVE_EXCEL_ROOT`).
  Aun con la compuerta abierta, el test debe trabajar sobre una **copia en directorio
  temporal**, nunca sobre el archivo original.
- **`tests/ArchitectureTests/NoLivePathGuardTest.cs` falla el build** si reaparece una ruta
  absoluta de mÃ¡quina del desarrollador dentro de `tests/`. No lo borres: es la red de
  seguridad que impide repetir el incidente.
- **La calibraciÃ³n de un proyecto de obra se hace con la herramienta manual**
  `tools/LiveProjectCalibration`, no con un test. Es *dry-run* por defecto y exige `--apply`
  para escribir; deja una copia `.precalibration` del destino.
- **No correr dos procesos de test a la vez en la misma mÃ¡quina.** Los tests de pegado
  usan el portapapeles de Windows, que es un recurso global: en paralelo, uno se queda
  bloqueado y la corrida se cuelga o se trunca. Ejecuta los proyectos de test de a uno:
  `dotnet test tests/QubiCostElectrical.Tests/QubiCostElectrical.Tests.csproj` y despuÃ©s
  `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`.

## Arquitectura

- **QubiCostElectrical.Domain / Application / Infrastructure:** negocio, SQLite, cÃ¡lculos, import/export.
- **QubiCostElectrical.Wpf:** UI WPF; pantallas en `Features/{Modulo}/`.
- **tests/QubiCostElectrical.Tests:** pruebas unitarias.

## DiseÃ±o visual

- Tokens: `src/QubiCostElectrical.Wpf/DesignSystem/*.xaml`
- Shell: cabecera oscura + navegaciÃ³n horizontal + barra contextual + contenido ancho completo.

## MÃ³dulos implementados (WPF)

Shell, dashboard, proyecto, itemizado, cubicaciÃ³n, cubicaciÃ³n SC, APU, banco de precios, cotizaciones, lista de compra, revisiÃ³n, versiones, oferta y documentos (wizard), documentos, preferencias, Ã³rdenes de cambio.

## DistribuciÃ³n

- Build local: `.\deploy\build-release.ps1` (Velopack)
- Firma Authenticode: `.\deploy\Sign-Authenticode.ps1` con `CODE_SIGNING_CERT_PFX` y `CODE_SIGNING_CERT_PASSWORD`
- Proceso de release: `docs/RELEASE_PROCESS.md`
- Smoke test humano: `docs/SMOKE_TEST.md`

## Licencias de terceros

- **QuestPDF:** Community si ingresos anuales &lt; USD 1M; configurar `QuestPDF.Settings.License` en `QuestPdfBootstrap`. Para licencia comercial, variable `QUESTPDF_LICENSE=Professional`.
- Detalle operativo en `docs/RELEASE_PROCESS.md` secciÃ³n Licencias.

## Roles y permisos

| Rol | Editar presupuesto | Exportar | Banco de precios | Preferencias globales |
|-----|-------------------|----------|------------------|----------------------|
| Administrador | SÃ­ | SÃ­ | SÃ­ | SÃ­ |
| Estimador | SÃ­ | SÃ­ | SÃ­ | No |
| Visor | No (solo lectura) | No | No | No |

PolÃ­tica en `UserRolePermissions` (Domain). La sesiÃ³n aplica restricciones vÃ­a `RoleSessionSync` al iniciar.

## Arquitectura de permisos

- `IPermissionService` / `UserRolePermissionService` en Application â€” Ãºnica puerta de permisos.
- WPF expone la instancia en `WpfAppHost.Permissions`; `SessionManager` delega para compatibilidad.
- ViewModels consultan `host.Permissions`, no `UserRolePermissions` directamente.

## NavegaciÃ³n principal

- **Oferta y exportaciÃ³n** (`ShellRoute.OfferDocuments`): flujo Ãºnico RevisiÃ³n â†’ Configurar â†’ Vista previa â†’ Exportar (menÃº Cierre; sin entrada duplicada en barra principal).
- **CubicaciÃ³n SC** (`ShellRoute.CubicacionSc`): tableros y circuitos de servicios comunes en presupuesto.

## DocumentaciÃ³n

- Fuente de verdad: este archivo y `docs/PRODUCT.md` (si existe).
- Informes histÃ³ricos de fases WPF/WinForms purgados (2026-10-20); el detalle de releases vive en `CHANGELOG.md`.
- Operativa: `docs/RELEASE_PROCESS.md`, `docs/SMOKE_TEST.md`, `docs/UI_SCREEN_CONTRACT.md`.

## Alcance multiusuario

- **ColaboraciÃ³n por archivo en carpeta compartida (SMB/UNC):** un editor exclusivo por `.qce`; los demÃ¡s abren en solo lectura.
- `ProjectCollaborationLockService` mantiene bloqueo OS (`FileShare.None`) + metadatos de usuario (nombre, correo, equipo) en `{proyecto}.qce.lock`.
- Heartbeat cada 30 s; bloqueo expirado (5 min sin respuesta) puede tomarse con confirmaciÃ³n explÃ­cita.
- **No** hay ediciÃ³n simultÃ¡nea de dos usuarios en el mismo proyecto; **no** hay servidor central ni merge automÃ¡tico.
- AuditorÃ­a persistente en `project_audit_log` (esquema metadata-driven; Ãºltima migraciÃ³n `Migration034` â€” ver `SQLiteSchemaMigrator`): aperturas, guardados, ediciones por mÃ³dulo y cambios de partida.
- PestaÃ±a **Actividad y auditorÃ­a** en Proyecto muestra el historial del archivo.
- Al guardar se actualiza `ProjectInfo.UsuarioNombre` / `UsuarioRol` (Ãºltimo responsable de persistencia).
- Vendible como **equipo con carpeta de red** (1 editor + N consultores), no como Google Docs ni ERP multiusuario en tiempo real.

## Reglas

- **RestricciÃ³n de Red:** No utilices herramientas de bÃºsqueda web (`search_web` o `read_url_content`) ni consumas recursos externos de internet a menos que el usuario te lo solicite explÃ­citamente. Trabaja Ãºnicamente con el cÃ³digo fuente local, la documentaciÃ³n interna y tus conocimientos base.
- No cambiar SQLite, migraciones, cÃ¡lculos ni fÃ³rmulas sin tarea explÃ­cita.
- LÃ³gica de negocio en Domain/Application, nunca en code-behind de vistas.
- No reintroducir nombres `PresupuestoElectrico` en scripts operativos ni UI visible.
- DocumentaciÃ³n histÃ³rica en `docs/WPF_*` puede mencionar WinForms; la fuente de verdad operativa es este archivo y `CLAUDE.md`.
- **Entornos DEV y PROD en la misma mÃ¡quina (Regla Estricta):**
  - **DEV (Entorno de desarrollo y pruebas activas):**
    - Acceso directo en el escritorio: `C:\Users\cristobal1\Desktop\Qubi Cost Electrical (Dev).lnk` (apunta a `publish\win-x64\QubiCostElectrical.exe`).
    - **SIEMPRE que se realicen cambios de cÃ³digo, se DEBE compilar y publicar Ãºnicamente en `publish\win-x64`** para que el usuario pruebe la versiÃ³n de desarrollo desde el acceso directo (Dev).
  - **PROD (InstalaciÃ³n oficial de producciÃ³n para pruebas reales de lanzamiento):**
    - Acceso directo en el escritorio: `C:\Users\cristobal1\Desktop\Qubi Cost Electrical.lnk` (apunta a `C:\Users\cristobal1\AppData\Local\QubiCostElectrical\current\QubiCostElectrical.exe`).
    - El usuario mantiene esta versiÃ³n oficial instalada para hacer las pruebas reales dentro de la app de lanzamiento para usuarios.
    - **REGLA ABSOLUTA:** **NUNCA modificar, sobrescribir, copiar archivos ni tocar `AppData\Local\QubiCostElectrical\current\`.** La versiÃ³n PROD solo se actualiza mediante releases oficiales empaquetados con Velopack (`build-release.ps1`), nunca copiando binarios de desarrollo manualmente.
  - El proyecto activo que el usuario utiliza como referencia para validar es: `C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL037A2026.qce` (o `PEL039A2026 ABIERTO.qce`).
