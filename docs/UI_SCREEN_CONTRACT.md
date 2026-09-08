# Contrato de pantalla (UI/UX sistema)

Fuente operativa para módulos en `Features/`. Objetivo: una sola app, no pantallas sueltas.

## 1. Capas obligatorias

| Capa | Control / API | Cuándo |
|------|---------------|--------|
| **Empty** | `EmptyStateControl` o `WorkspacePageChrome` | `!HasOpenProject` |
| **Skeleton** | `SkeletonOverlayControl` | carga async inicial o recálculo bloqueante |
| **Contenido** | vista del módulo | proyecto abierto y datos listos |
| **RO** | Shell banner + `CanEdit=false` | lock SMB o rol Visor |

ViewModels de workspace deben exponer al menos `ShowEmptyState` y sincronizar con `SyncWorkspaceGate()` (`WorkspacePageViewModelBase`).

## 2. Tipografía y color

- **Prohibido** en vistas nuevas: `FontSize="13"`, hex sueltos, alturas mágicas repetidas.
- Usar tokens `Font.Size.*` y estilos `Text.*` de `DesignSystem/Typography.xaml`.
- Densos (Costo, MO, APU, EPP): migración progresiva; nuevos bloques solo con tokens.

Tokens añadidos para módulos densos: `Font.Size.kpi`, `Font.Size.price`, `Font.Size.hero`, `Font.Size.micro`, `Text.SectionTitle`, `Text.KpiInline`, `Text.Helper`, `Brush.HeroPanel`.

## 3. Grillas editables

```xml
xmlns:infra="clr-namespace:QubiCostElectrical.Wpf.Infrastructure"

<DataGrid Style="{StaticResource DataGrid.Workspace}"
          infra:DataGridExcelNavigation.Enable="True" ... />
```

Multi-celda / copiar-pegar: `DataGrid.Workspace.Excel`.

## 4. Política de feedback (`ModuleFeedback`)

| Situación | Canal |
|-----------|--------|
| Solo lectura bloquea acción | Toast warning (`NotifyReadOnly`) |
| Resultado de operación en módulo | `StatusMessage` persistente |
| Éxito global / acción secundaria | Toast success |
| Error que impide continuar | Modal (`NotifyError`) |
| Guardado manual | Solo chrome (`SaveStateText`) — **sin** toast duplicado |

No combinar toast + StatusMessage + modal con el mismo texto.

## 5. Chrome shell (1280×720)

- Stepper: `Padding.WorkflowStepper` (compacto).
- Banner RO: `Padding.ReadOnlyBanner`, `MaxHeight=40`, ellipsis.
- Contenido: preferir `Padding.PageCompact` en módulos densos cuando aplique.

## 6. Gate de arquitectura

`WorkspacePageContractTests` verifica que las vistas core incluyan `WorkspacePageChrome` o el par `EmptyStateControl` + `SkeletonOverlayControl`.

Incluye ApuCatalog, ApuMerge, Contracts (3) y ScenarioWorkspace (chrome listo; **sin** `ShellRoute`/menú — WIP navegación).

Preferencias: EmptyStateControl + SkeletonOverlayControl (chrome no calza por namescope `x:Name` en rail); `ShowEmptyState=false` siempre (ajustes globales).
