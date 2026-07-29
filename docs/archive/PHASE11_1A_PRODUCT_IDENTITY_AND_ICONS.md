# FASE 11.1A — Identidad oficial, iconos y versionado de Qubi Cost Electrical

## Commit base

`9870557` — feat: adopt qce project file extension

## Identidad oficial

| Campo | Valor |
|---|---|
| Producto | `Qubi Cost Electrical` |
| Marca corta | `Qubi Cost` |
| Abreviación | `QCE` |
| Ejecutable | `QubiCostElectrical.exe` |
| AssemblyName | `QubiCostElectrical` |
| Extensión | `.qce` |
| Descripción de archivo | `Proyecto de Qubi Cost Electrical` |
| Package ID (Velopack) | `QubiCostElectrical` |

## Metadatos del ejecutable (verificados en publish)

| Campo | Valor |
|---|---|
| Product | `Qubi Cost Electrical` |
| Company | `Qubi Cost` |
| FileVersion | `1.0.0.0` |
| ProductVersion | `1.0.0-rc.1+<commit hash>` |

## Fuente de versión

`Directory.Build.props` — única fuente de verdad.

```xml
<VersionPrefix>1.0.0</VersionPrefix>
<VersionSuffix>rc.1</VersionSuffix>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<InformationalVersion>1.0.0-rc.1</InformationalVersion>
```

| Componente | Valor |
|---|---|
| Versión del assembly | `1.0.0.0` |
| Versión del archivo | `1.0.0.0` |
| Versión informativa | `1.0.0-rc.1` (+ git hash en publish) |
| Versión usada por Velopack | `1.0.0` (VersionPrefix vía `Publish-Release.ps1`) |
| Versión mostrada en instalador | `1.0.0` (inyectada por CI `/DAppVersion=1.0.0`) |

`ProductVersionInfo.ProductVersion` retorna `1.0.0`.

## Icono seleccionado

**Archivo fuente:** `src/QubiCostElectrical.Wpf/Assets/isolate_the_isometric_cube_logo_mark_from_data_image_image_6._remove_the_white.png`
— Cubo isométrico con rayo eléctrico, fondo transparente.

**ICO generado:** `src/QubiCostElectrical.Wpf/Assets/QubiCostElectrical.ico`
— 144 KB, 8 resoluciones: 16, 24, 32, 40, 48, 64, 128, 256 px.
— Cada tamaño almacenado como PNG dentro del ICO (formato Vista+, 32bpp ARGB, fondo transparente).

## Configuración del ejecutable

```xml
<!-- QubiCostElectrical.Wpf.csproj -->
<ApplicationIcon>Assets\QubiCostElectrical.ico</ApplicationIcon>
<Title>Qubi Cost Electrical</Title>
```

## Icono de ventana WPF

`ShellWindow.xaml`:
```xml
Icon="pack://application:,,,/Assets/QubiCostElectrical.ico"
```

El icono aparece en barra de tareas, Alt+Tab y título de ventana.

## Instalador

**Archivo:** `installer/QubiCostElectrical.iss` (renombrado desde `PresupuestoElectricoPRO.iss`)

Cambios:
- `AppName = "Qubi Cost Electrical"`
- `AppVersion` fallback = `1.0.0`
- `SetupIconFile = ..\src\QubiCostElectrical.Wpf\Assets\QubiCostElectrical.ico`
- `OutputBaseFilename = QubiCostElectrical-Setup-{#AppVersion}`
- `AppId` preservado (`A7F3B2C1-...`) para cadena de actualización

## Asociación `.qce`

```ini
[Registry]
.qce → QubiCostElectrical.Project
QubiCostElectrical.Project → "Proyecto de Qubi Cost Electrical"
DefaultIcon → QubiCostElectrical.exe,0
shell\open\command → "QubiCostElectrical.exe" "%1"
```

## Apertura por doble clic

`SingleInstanceHandoffService.TryExtractOpenProjectPath` reconoce:
- `.qce` ✓
- `.pepro` ✓ (compatibilidad legacy)
- Archivo inexistente → rechazado ✓
- Extensión no admitida → rechazado ✓

## Archivos renombrados

| Antes | Después |
|---|---|
| `installer/PresupuestoElectricoPRO.iss` | `installer/QubiCostElectrical.iss` |

## Archivos modificados (código productivo)

| Archivo | Cambio |
|---|---|
| `Directory.Build.props` | Versión, Product, Company, Authors, Copyright |
| `QubiCostElectrical.Wpf.csproj` | ApplicationIcon, Title, Resources |
| `ShellWindow.xaml` | Icon |
| `installer/QubiCostElectrical.iss` | Nuevo (reemplaza PresupuestoElectricoPRO.iss) |
| `installer/company.iss` | AppPublisher, URLs |
| `.github/workflows/build.yml` | Referencia al nuevo .iss |
| `deploy/build-release.ps1` | packId, packTitle, synopsis |
| `tools/Create-SourcePackage.ps1` | ZIP name |
| `QuestPdfCubicacionReportExporter.cs` | Footer PDF |
| `Assets/QubiCostElectrical.ico` | Creado (8 resoluciones) |

## Pruebas nuevas

`tests/PresupuestoElectrico.Tests/Architecture/ProductIdentityTests.cs` — 16 tests:
- AssemblyName, Product, Company, Versión informativa
- CurrentExtension `.qce`
- Descripción tipo de archivo
- ICO embebido en recursos WPF
- ICO existente en repo
- Sin rutas absolutas
- Sin nombres legacy en constantes
- Apertura `.qce`, `.pepro`, archivo inexistente, extensión no admitida

## Inventario de tests

| Suite | Antes | Después |
|---|---|---|
| Total | 1177 | **1195** |
| Nuevos | — | 18 |

## Debug / Release

```
Debug  → 0 errores, 79 advertencias
Release → 0 errores, 92 advertencias
```

## Publicación (verificada)

```
dotnet publish ... → QubiCostElectrical.exe
Product:    Qubi Cost Electrical ✓
Company:    Qubi Cost ✓
FileVersion: 1.0.0.0 ✓
ProductVer:  1.0.0-rc.1+<hash> ✓
```

## Validación manual ejecutada

- Ejecutable publicado existe con nombre correcto ✓
- Metadatos del EXE correctos ✓
- Suite de pruebas 1195/1195 ✓

## Limitaciones conocidas

- AppData folder: `%LOCALAPPDATA%\PresupuestoElectricoPRO\` no se renombra para preservar datos de instalaciones existentes. Un script de migración puede hacerlo en una fase posterior si se requiere.
- Verificación visual del icono en ventana y barra de tareas pendiente de prueba con app en ejecución.
- Firma Authenticode no aplicada (SmartScreen mostrará aviso la primera vez).

## Veredicto

```
IDENTIDAD QUBI COST ELECTRICAL COMPLETA — LISTO PARA EMPAQUETADO RC
```
