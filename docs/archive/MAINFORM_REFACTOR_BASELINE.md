# MainForm Refactor — Línea Base

> Fecha: 2026-06-18 (actualizado)
> Rama: `master`
> Último hito navegación: commits `48731cf`–`3e123fe`

## Métricas de MainForm

| Métrica | Valor (aprox.) |
|---|---|
| Archivos `MainForm*.cs` | ~51 |
| Partial más grande | `MainForm.Shell.cs` (reducido respecto a baseline) |

## Compilación y pruebas

| Comando | Resultado |
|---|---|
| `dotnet build PresupuestoElectrico.sln` | 0 errores |
| `dotnet test PresupuestoElectrico.sln --no-build` | 570+ aprobados |

## Dependencias principales de MainForm

- `BudgetProjectSession` — estado de sesión completo
- `AppServices` — contenedor de servicios
- `NavigationService` / `NavigationPresenter` — estado de ruta e historial
- `PageNavigator` + `WorkspacePageFactory` — render de páginas workspace
- `MainFormNavigationService` — orquestador UI de navegación
- `ShellCompositionService` — construcción del shell y registro de `AppToast`
- `SQLiteProjectStore` — persistencia

## Arquitectura de páginas (vigente)

Todas las rutas principales usan `*WorkspacePage` registradas en `InitializeWorkspaceNavigation()`:

| Ruta | Clase |
|---|---|
| Inicio | `InicioWorkspacePage` |
| Proyecto | `ProyectoWorkspacePage` |
| Dashboard | `DashboardWorkspacePage` |
| Itemizado | `ItemizadoWorkspacePage` |
| Cubicación / C.SEC | `CubicacionWorkspacePage` / `CubicacionSCWorkspacePage` |
| Buscador (catálogo, compra, auditoría) | `PreciosWorkspacePage` |
| Plantillas, APU, Monedas, Revisión, Exportar, Preferencias | `*WorkspacePage` correspondiente |

**Retirado del código:** `PageRenderRegistry`, `IAppPageRenderer`, `IShellHost`, `LegacyDelegatePage`, stubs sin consumidor (`INavigationRequestService`, `IStatusService`, `IProjectChangeTracker`, `ToastNotificationService`, `DialogService`, `WorkspaceHost`).

Deps por página: interfaces `*PageHost` en `MainForm.WorkspaceNavigation.cs` (sustituyen el antiguo `IShellHost`).

## Funciones candidatas a eliminar (según epics anteriores)

- Calculadora (`MainForm.Calculadora.cs`, 95 líneas)
- CRM / Clientes (`MainForm.Clientes.cs`, 444 líneas)
- Estados de pago (`MainForm.EstadosPago.cs`, 652 líneas)
- Órdenes de cambio (`MainForm.ChangeOrders.cs`, 762 líneas)
- Varianza (`MainForm.Varianza.cs`, 141 líneas)
- Comparador (`MainForm.Comparador.cs`, 784 líneas)
- Mano de obra por jornada (no tiene archivo propio, parte de otro)
- Precios vencidos como página separada
- Reajuste polinómico (`MainForm.ReajustePrecios.cs`, 304 líneas)
- Sensibilidad como pantalla independiente (`MainForm.Sensibilidad.cs`, 632 líneas)

## Nota histórica

La tabla de páginas “extraídas parcialmente” con `IShellHost` quedó obsoleta tras la migración a `*WorkspacePage`. Ver sección “Arquitectura de páginas (vigente)” arriba.
