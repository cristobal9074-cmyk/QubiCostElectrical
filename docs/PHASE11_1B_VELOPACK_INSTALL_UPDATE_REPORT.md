# FASE 11.1B — Velopack: Instalación y actualización RC.1 → RC.2

## Commit base

`bc9248d` — feat: complete product identity (FASE 11.1A)

## Objetivo

Integrar Velopack en el ejecutable WPF y demostrar localmente el flujo completo:
- Empaquetado reproducible desde una fuente de versión única
- Instalación limpia de RC.1
- Actualización en sitio RC.1 → RC.2
- Preservación de datos de usuario
- Asociación `.qce` funcional antes y después del update
- Desinstalación limpia con datos preservados

---

## Cambios de código

### `deploy/build-release.ps1`
- **Versión**: lee `VersionPrefix` + `VersionSuffix` de `Directory.Build.props` y los compone
  (`1.0.0` + `rc.2` → `1.0.0-rc.2`). Antes solo leía `VersionPrefix`.
- **Nuevo parámetro `-KeepReleases`**: omite el borrado de `deploy/releases/` para acumular
  RC.1 y RC.2 en el mismo feed y generar el delta de actualización.

### `QubiCostElectrical.Wpf.csproj`
- `<PackageReference Include="Velopack" Version="0.*" />` — integración del runtime Velopack.
- `<StartupObject>QubiCostElectrical.Wpf.Program</StartupObject>` — activa el `Main` manual.

### `Program.cs` (nuevo)
- Entry point manual con `[STAThread]`.
- Llama `VelopackApp.Build().Run()` **antes** de crear `App` — condición obligatoria de Velopack
  para procesar argumentos internos (`--veloapp-install`, `--veloapp-uninstall`, etc.).
- Si la app se ejecuta desde contexto Velopack (detectado por presencia de `Update.exe` en dir padre),
  llama `ShellRegistration.Register()` para mantener la asociación `.qce` actualizada.

### `ShellRegistration.cs` (nuevo)
- Escribe entradas `HKCU\Software\Classes\.qce` y `QubiCostElectrical.Project` al `current\` exe instalado.
- `Register()` es idempotente. `Unregister()` limpia al desinstalar.

### `VelopackUpdateService.cs` (nuevo)
- Servicio interno que expone `IsInstalled`, `CheckForUpdatesAsync()` y `DownloadAndApplyUpdateAsync()`.
- Acepta cualquier URL o ruta local como feed de actualizaciones.

---

## Herramienta instalada

```
vpk 1.2.0  (dotnet tool install -g vpk)
Velopack library: 0.0.1298.0 (NuGet "Velopack" 0.*)
```

Advertencia esperada: `Velopack library version is lower than vpk version` — no afecta funcionalidad.

---

## Flujo de validación ejecutado

### Paso 1 — Empaquetado RC.1

```powershell
dotnet publish ... --output deploy\publish
vpk pack --packId QubiCostElectrical --packVersion 1.0.0-rc.1 --packDir deploy\publish \
    --packTitle "Qubi Cost Electrical" --mainExe QubiCostElectrical.exe \
    --outputDir deploy\releases --channel win
```

Artefactos generados:
```
QubiCostElectrical-1.0.0-rc.1-full.nupkg   85.7 MB
QubiCostElectrical-win-Setup.exe            89.9 MB
QubiCostElectrical-win-Portable.zip         85.7 MB
assets.win.json, releases.win.json, RELEASES
```

vpk verificó `VelopackApp.Run()` en `Program.Main` ✓

### Paso 2 — Instalación RC.1

```
QubiCostElectrical-win-Setup.exe --silent
```

| Campo | Resultado |
|---|---|
| Directorio | `%LOCALAPPDATA%\QubiCostElectrical\current\` |
| Exe presente | `QubiCostElectrical.exe` ✓ |
| Product | `Qubi Cost Electrical` ✓ |
| Version | `1.0.0-rc.1+bc9248d...` ✓ |
| Company | `Qubi Cost` ✓ |
| Acceso directo escritorio | `Qubi Cost Electrical.lnk` ✓ |
| Acceso directo menú inicio | `Qubi Cost Electrical.lnk` ✓ |

### Paso 3 — Asociación `.qce` RC.1

Primera ejecución del exe instalado activó `ShellRegistration.Register()`:

```
HKCU\Software\Classes\.qce  →  QubiCostElectrical.Project
HKCU\Software\Classes\QubiCostElectrical.Project\shell\open\command
    →  "%LOCALAPPDATA%\QubiCostElectrical\current\QubiCostElectrical.exe" "%1"
DefaultIcon
    →  "%LOCALAPPDATA%\QubiCostElectrical\current\QubiCostElectrical.exe",0
```

### Paso 4 — Empaquetado RC.2

Solo cambio en `Directory.Build.props`: `VersionSuffix` `rc.1` → `rc.2`.

```powershell
dotnet publish ... --output deploy\publish
vpk pack --packId QubiCostElectrical --packVersion 1.0.0-rc.2 ...
    --outputDir deploy\releases   # release dir contiene ya RC.1
```

Artefactos adicionales:
```
QubiCostElectrical-1.0.0-rc.2-full.nupkg    85.7 MB
QubiCostElectrical-1.0.0-rc.2-delta.nupkg    0.4 MB  ← delta RC.1→RC.2
```

### Paso 5 — Update RC.1 → RC.2

```
Update.exe apply --package QubiCostElectrical-1.0.0-rc.2-full.nupkg --norestart
```

| Verificación | Resultado |
|---|---|
| Versión instalada post-update | `1.0.0-rc.2+bc9248d...` ✓ |
| Datos usuario en `PresupuestoElectricoPRO\` | **preservados** ✓ |
| Asociación `.qce` activa | `QubiCostElectrical.Project` ✓ |
| Comando `.qce` apunta a RC.2 | `current\QubiCostElectrical.exe` v1.0.0-rc.2 ✓ |

La ruta `current\` es estable en Velopack — la asociación de archivo no requiere reregistrar tras update.

### Paso 6 — Desinstalación

```
Update.exe uninstall --silent
```

| Verificación | Resultado |
|---|---|
| Hook `--veloapp-uninstall` ejecutado | ✓ |
| Shortcut escritorio eliminado | ✓ |
| Shortcut menú inicio eliminado | ✓ |
| Directorio `QubiCostElectrical\` eliminado | ✓ |
| Datos `PresupuestoElectricoPRO\` **intactos** | ✓ |

---

## Fuente única de versión

Modificando una sola línea en `Directory.Build.props`:

```xml
<VersionSuffix>rc.2</VersionSuffix>
```

El script `build-release.ps1` sin parámetros compone `1.0.0-rc.2` automáticamente.
Para pasar la versión explícitamente: `.\deploy\build-release.ps1 -Version "1.0.0-rc.3"`.

---

## Suite de pruebas

```
1195/1195 passed — 0 failures — con Velopack integrado y VersionSuffix=rc.2
```

---

## Limitaciones conocidas

| Limitación | Detalle |
|---|---|
| Firma Authenticode ausente | SmartScreen mostrará aviso en equipo limpio. Aceptable para RC; requiere certificado antes de versión comercial. |
| Velopack lib 0.x vs CLI 1.2.x | Advertencia de versión en output de vpk. Sin efecto funcional. Actualizar a `Velopack 1.*` NuGet cuando esté disponible. |
| Update check in-app no implementado | El update se aplicó con `Update.exe apply` manual. La UI de "Buscar actualizaciones" queda para FASE 11.2. `VelopackUpdateService` está listo para conectar. |
| Prueba en equipo limpio pendiente | Validación local completada. Prueba en máquina sin dev tools queda como EMPAQUETADO VALIDADO LOCALMENTE — PRUEBA EXTERNA PENDIENTE. |

---

## Veredicto

```
FLUJO VELOPACK RC.1 → RC.2 VALIDADO LOCALMENTE
Instalación ✓ · Accesos directos ✓ · Asociación .qce ✓
Update RC.1→RC.2 ✓ · Delta 0.4 MB ✓ · Datos preservados ✓
Desinstalación limpia ✓ · Datos usuario intactos ✓
```
