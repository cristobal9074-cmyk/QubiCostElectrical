# FASE 11.0 — Política oficial de extensión de proyecto `.qce`

## Commit base

`7577886` — refactor: remove legacy WinForms application

## Formato oficial actual

`.qce` (Qubi Cost Electrical Project)

## Formato legacy

`.pepro` (Presupuesto Eléctrico PRO, solo migración y apertura)

## Formato interno actual

El archivo `.qce` es el formato comercial visible y debe contener el proyecto sin exponer archivos laterales al usuario. La detección se basa en el contenido interno del archivo, no en la extensión por sí sola.

## Fuente única de verdad

`src/PresupuestoElectrico.Application/ProjectFileTypes.cs`

```csharp
public static class ProjectFileTypes
{
    public const string CurrentExtension = ".qce";
    public const string LegacyExtension  = ".pepro";
    public const string AutoSaveSuffix   = ".autosave";
    public const string SqliteSuffix     = ".db";
    public static IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".qce", ".pepro" };
    public static bool IsSupportedProjectExtension(string ext) => ...
}
```

## Estrategia de compatibilidad

Los archivos `.pepro` se abren como legacy, se respaldan antes de migrar y se convierten a `.qce` para continuar trabajando sobre el formato actual. No se guardan cambios nuevos sobre `.pepro`.

## Comportamiento de Guardar

| Situación | Resultado |
|---|---|
| Proyecto nuevo | Se guarda como `.qce` |
| Proyecto `.qce` abierto → Guardar | Conserva la ruta `.qce` |
| Proyecto `.pepro` abierto → Guardar | Migra y guarda como `.qce` |

## Comportamiento de Guardar como

| Situación | Resultado |
|---|---|
| Proyecto nuevo | Sugiere `.qce` |
| Proyecto `.pepro` abierto → Guardar como | Sugiere `.qce` como predeterminado |
| Filtros disponibles | `.qce` como formato principal, `.pepro` sólo como heredado |

## Recientes

`RecentProjectsService` conserva entradas `.pepro` legacy para apertura e importación, pero el flujo normal crea y guarda `.qce`.

## Backups y autosaves

El autosave se genera como `<ruta-del-proyecto>.autosave`:

- `Hospital.qce` → `Hospital.qce.autosave`
- `Hospital.pepro` → `Hospital.pepro.autosave` (legacy, sólo mientras exista soporte histórico)

Fallback sin proyecto abierto usa `.qce.autosave`.

El sidecar SQLite se genera como `<nombre><ext>.db`:

- `Hospital.qce` → `Hospital.qce.db`
- `Hospital.pepro` → `Hospital.pepro.db` (legacy)

## Asociación de archivo

Registrada en el instalador (`installer/PresupuestoElectricoPRO.iss` sección `[Registry]`):

| Campo | Valor |
|---|---|
| Extensión | `.qce` |
| Descripción | `Proyecto de Qubi Cost Electrical` |
| Ejecutable | `QubiCostElectrical.exe` |
| Verbo | `Abrir` |

La asociación escribe en `HKCU\Software\Classes\.qce` (sin privilegios de administrador).

## Apertura por argumentos

`SingleInstanceHandoffService.TryExtractOpenProjectPath` usa `ProjectStoragePaths.IsSqliteProjectFile()`, que reconoce `.qce` y `.pepro`. Funciona sin modificación adicional.

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `ProjectFileTypes.cs` | Creado — fuente única de verdad |
| `ProjectStoragePaths.cs` | Reconoce `.qce`; autosave preserva extensión del proyecto |
| `ProjectRepository.cs` | Sidecar `.db` usa extensión del proyecto |
| `WpfDialogService.cs` | Filtros actualizados: `.qce` principal, `.pepro` heredado |
| `NewProjectDialog.xaml.cs` | Proyecto nuevo usa `.qce` |
| `ShellViewModel.cs` | Guardar como sugiere `.qce` |
| `AutoSaveService.cs` | Fallback usa `.qce.autosave` |
| `GlobalErrorHandler.cs` | Diálogo de emergencia usa `.qce` |
| `DashboardView.xaml` | Mensaje actualizado a `.qce` |
| `DashboardViewModel.cs` | Mensaje actualizado a `.qce` |
| `ProductVersionInfo.cs` | `ProjectFormatLabel` → `qce-6` |
| `installer/PresupuestoElectricoPRO.iss` | Asociación de archivo `.qce` |
| `tools/Create-SourcePackage.ps1` | Carpeta temporal renombrada |

## Pruebas

- `ProjectFileTypesTests.cs` — 22 tests nuevos (fuente de verdad, compatibilidad, autosave, tildes, espacios)
- `ProjectStoragePathsTests.cs` — actualizado: casos `.qce` agregados, `.pepro` conservados como legacy
- `AutoSaveServiceTests.cs` — casos `.qce` y `.pepro` paralelos

## Inventario de tests

| Suite | Antes | Después |
|---|---|---|
| Total | 1144 | 1177 |
| Nuevos | — | 33 |

## Validación

```
dotnet build QubiCostElectrical.Wpf.sln -c Debug   → 0 errores
dotnet build QubiCostElectrical.Wpf.sln -c Release  → 0 errores
dotnet test  QubiCostElectrical.Wpf.sln             → 1177/1177
```

## Riesgos

- Usuarios con proyectos `.pepro` existentes: pueden importarlos o abrirlos para migrar a `.qce`.
- Doble clic en `.pepro` desde el explorador de Windows: se admite como compatibilidad legacy si la asociación existe; el formato público sigue siendo `.qce`.
- El formato de intercambio `.peprox` no fue modificado; permanece igual.

## Veredicto

```
EXTENSIÓN `.QCE` ACTIVA — `.PEPRO` SOLO LEGACY
```
