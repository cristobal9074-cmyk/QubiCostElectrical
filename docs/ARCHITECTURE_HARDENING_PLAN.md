# Plan de endurecimiento arquitectónico (olas)

Objetivo: subir salud estructural (hoy ~5.7 → meta interna “20/10”) sin features nuevas ni instalador.
Prioridad acordada: **carpetas → Shell/Session → Costo/Cub → scopes tipados → lock/autosave → gate líneas**.

## Ola 1 — cimientos

| Ítem | Qué | Done |
|------|-----|------|
| 1.1 Dual-path | Borrar carpetas vacías + DEAD; mover LIVE a `Features/` | ✅ |
| 1.2 Dirty tipado | `ProjectDirtyScope` + overload `MarkDirty`; hot-path migrado | ✅ |
| 1.3 Lock harden | `OwnsActiveLock` + RO al `LockLost` + cancel debounce | ✅ |
| 1.4 Costo slice | `CostoBreakdownLineBuilder` en Application + tests | ✅ |
| 1.5 Gate líneas | Contar por tipo parcial; no excluir `*.Methods.cs` a ciegas | ✅ |
| 1.6 Shell factory | Extraer `ShellPageFactory` (CreateView) | ✅ |

### Dual-path (estado)

- **Unificado en `Features/`:** ApuCatalog, ApuMerge, EconomicEditor, Contracts, ScenarioWorkspace, About; PrecioLista*; VMs SC; `AsyncDebounceRunner` → `Infrastructure/`.
- **Purgado DEAD:** stubs `IComponentConnector` y dual-path huérfanos.
- **Árbol legacy `QubiCostElectrical.Wpf.Features.*`:** 0 carpetas.

## Ola 2 — estructura ✅

1. ~~Mover LIVE dual-path → `Features/`~~
2. ~~`ShellPageCache`~~ + ~~`ShellNavigationCoordinator`~~ + ~~`ShellProjectLifecycle`~~
3. ~~`ProjectDirtyTracker` / `ProjectAccessGate`~~ internos en Session
4. ~~`WpfAppHostBuilder`~~
5. ~~Coalesce `Session.Changed` (75 ms) + silenciar páginas cacheadas inactivas (`ISessionChangeAware`)~~

## Ola 3 — extracción de negocio ✅

1. ~~GO/MO builders desde Costo VM~~ → `CostoMoLaborProjectionBuilder`, `CostoGoComposeProjectionBuilder`
2. ~~Sector-expression / variables cantidad (Cub)~~ → `PartidaCantidadVariablesBuilder`, `CubicacionSectorExpressionCompactor` (eval ya en Domain/`QuantityFormulaService` + Application takeoff)
3. ~~Coalescer `Session.Changed`~~ (adelantado a Ola 2)
4. ~~Pulido post-Ola 3~~ → `CubicacionSectorNavigation`, `OfferPackagePresetCatalog`, `ExportEligibilityEvaluator`, `GastoOperacionalCategoryMaintenance`

## Ola 4 — contrato lock completo ✅

1. ~~Lease mid-write~~ → `TryBeginWriteLease` / `RequiresExclusiveLease` / `ProjectLockLostException`; ownership leída desde disco
2. ~~Sidecar en LockLostPending~~ → `.autosave` sigue; `.qce` no; audit `BloqueoPerdido`
3. ~~PrepareForAppExit skip `.qce`~~ si RO/lock lost; flush sidecar; `ProjectLockLostException` no bloquea salida
4. ~~Tests de carrera~~ → `ProjectLockMidWriteTests`, `LockLostPendingExitTests`

## Criterio “20/10” interno

- Un solo árbol `Features/` ✅
- Shell facade orquestación adelgazada ✅ parcial
- Dirty tipado en hot-path ✅
- Lock lost = no más writes `.qce` ✅ (Ola 1 + lease mid-write Ola 4)
- Costo desglose + MO/GO proyección testeable sin WPF ✅
- Gate de líneas sin escape Methods ✅
- Session.Changed coalescido + no refresh de cache inactiva ✅
- Cub: variables R1..Rn + compactación de expresiones fuera de WPF ✅
- Autosave + lock: contrato LockLostPending unificado ✅