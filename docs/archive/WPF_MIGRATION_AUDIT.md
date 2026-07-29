# WPF Migration Audit

Fecha: 2026-06-21  
Rama: `master`

## Baseline pre-implementación

| Verificación | Resultado |
|--------------|-----------|
| `dotnet build PresupuestoElectrico.sln -c Debug` | **FALLA** — NETSDK1045 (`QubiCostElectrical.Wpf` en `net10.0-windows`, SDK 8.0.421) |
| `dotnet build PresupuestoElectrico.sln -c Release` | **FALLA** — mismo error |
| `dotnet test PresupuestoElectrico.sln -c Debug` | **749 total** — 746 OK, **3 FAIL** (preexistentes) |

### Pruebas fallidas (baseline)

1. `WorkspaceNavigationRegistrationTests.Workspace_factory_count_matches_primary_routes`
2. `NavigationPageCatalogTests.TryFromKey_RemovedExperimentalKeys_AreNotRegistered` (`OrdenesCambio`)
3. `WinFormsThemeCompletenessTests.WinFormsSources_DoNotUseHardcodedWhite_OutsideAllowList`

## Proyectos y frameworks

| Proyecto | TFM |
|----------|-----|
| Domain | net8.0 |
| Application | net8.0 |
| Infrastructure | net8.0-windows |
| WinForms | net8.0-windows |
| Tests | net8.0-windows |
| WPF (antes) | net10.0-windows |
| WPF (Fase 1) | **net8.0-windows** |

## Dependencias WPF Fase 1

- Sin referencia a WinForms.
- Sin referencia a Domain/Application/Infrastructure en Fase 1 (shell con datos mock).

## Acción correctiva Fase 1

- Alinear WPF a `net8.0-windows` para compatibilidad con SDK y resto de solución.

## Resultado post-implementación Fase 1

| Verificación | Resultado |
|--------------|-----------|
| `dotnet build PresupuestoElectrico.sln -c Debug` | **OK** (0 errores, 76 advertencias — mayoría preexistentes en Domain/WinForms/Tests) |
| `dotnet build PresupuestoElectrico.sln -c Release` | **OK** (0 errores, 76 advertencias) |
| `dotnet build QubiCostElectrical.Wpf -c Debug` | **OK** (0 errores, 0 advertencias) |
| `dotnet test PresupuestoElectrico.sln -c Debug` | **749 total** — 746 OK, **3 FAIL** (mismo baseline; sin regresiones nuevas) |

### Verificación visual

```powershell
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
```
