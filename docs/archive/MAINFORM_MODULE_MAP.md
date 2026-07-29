# MainForm Module Map — Clasificación de partials

> Rama: `master`
> Fecha: 2026-06-18 (actualizado)

## Estado vigente (navegación)

Migración de shell **completada** para rutas comerciales:

- **Render:** `PageNavigator` + `WorkspacePageFactory` + `*WorkspacePage` (`MainForm.WorkspaceNavigation.cs`).
- **Orquestación:** `MainFormNavigationService` → `ShellNavigation.RenderPage`.
- **Eliminado:** `PageRenderRegistry`, `IAppPageRenderer`, `IShellHost`, `MainForm.Pages.cs`, `MainForm.IShellHost.cs`, `LegacyDelegatePage`, stubs de servicios sin consumidor.
- **Toasts:** `AppToast` / `AppToastHost` (no `ToastNotificationService`).

El mapa de archivos inferior conserva la clasificación histórica del refactor; las filas marcadas como “Eliminar” en navegación ya no aplican o fueron retiradas.

## Leyenda

| Código | Categoría |
|---|---|
| SHELL | Infraestructura visual y navegación |
| ITEMIZADO | Módulo de itemizado |
| CUBICACIÓN | Módulo de cubicación |
| PRECIOS | Catálogo, lista de compra, auditoría |
| APU | Análisis de precios unitarios |
| REVISIÓN | Revisión final, validaciones |
| EXPORTACIÓN | Excel, PDF, BC3, cartas |
| PLANTILLAS | Plantillas de cubicación |
| PROYECTO | Ciclo de vida, carga/guardado |
| PREFERENCIAS | Configuración de usuario |
| DASHBOARD | Panel de inicio/resumen |
| MONEDAS | Tipo de cambio |
| LEGACY | Función candidata a eliminación |
| EXPERIMENTAL | Función no definitiva |
| UTILIDAD | Código compartido sin UI directa |

## Mapa de archivos

| Archivo | Líneas | Categoría | Dependencias clave | Controles propios | Destino | Orden |
|---|---|---|---|---|---|---|
| MainForm.cs | 304 | SHELL | CompositionRoot, AppServices | Ninguno específico | MainForm.cs (reducido) | 0 |
| MainForm.Shell.cs | 2,051 | SHELL | AppServices, NavigationService | ToolStrip, paneles, titlebar | AppShellControl, AppTitleBar, etc. | 10 |
| MainForm.NavigationRedesign.cs | 236 | SHELL | NavigationService | Menú contextual | NavigationService + PageNavigator | ✅ |
| MainForm.WorkspaceNavigation.cs | ~175 | SHELL | WorkspacePageFactory, PageNavigator | Ninguno | Mantener (registro páginas) | ✅ |
| MainForm.PageTransition.cs | 65 | SHELL | Workspace | Ninguno | PageNavigator | ✅ |
| MainForm.Workflow.cs | ~15 | SHELL | WorkflowUiService | Ninguno | Mantener | ✅ |
| MainForm.UiFactory.cs | 483 | UTILIDAD | AppTheme | Botones, labels, paneles | Shared/ | — |
| MainForm.UiState.cs | 266 | UTILIDAD | — | Ninguno (clases estado) | Mantener, luego distribuir | — |
| MainForm.ThemeDpi.cs | 191 | UTILIDAD | AppTheme | Ninguno | Shared/ | — |
| MainForm.Performance.cs | 203 | UTILIDAD | NavigationPage | Ninguno (helpers) | Shared/ | — |
| MainForm.LayoutFixes.cs | 520 | UTILIDAD | Varios | Ninguno (fixes) | Shared/ | — |
| MainForm.ContextualTooltips.cs | 279 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.CorporateTheme.cs | 161 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.CorporateLogo.cs | 107 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.ErgonomiaPro.cs | 222 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.Toast.cs | 186 | UTILIDAD | — | Panel | AppToastHost | 0 |
| MainForm.GlobalSearchAlerts.cs | 831 | UTILIDAD | ProjectSession, AppServices | Ninguno (lógica) | NotificationService + SearchService | — |
| MainForm.BudgetCalculationCache.cs | 67 | UTILIDAD | BudgetCalculation | Ninguno | Shared/ | — |
| MainForm.Analisis.Cache.cs | 21 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.Async.cs | 178 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.CommandPaletteHistory.cs | 36 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.NotificationModels.cs | 15 | UTILIDAD | — | Ninguno | Shared/ | — |
| MainForm.WindowChrome.cs | 240 | SHELL | — | Ninguno (NC area) | AppShellControl | 10 |
| MainForm.SidebarCompact.cs | 101 | SHELL | — | Panel | AppNavigationBar | 10 |
| MainForm.Lifecycle.cs | 32 | SHELL | — | Ninguno | ProjectLifecycleCoordinator | 0 |
| **MainForm.Itemizado.cs** | 515 | ITEMIZADO | ProjectSession, AppServices | gridItemizado | Features/Itemizado/ | 1 |
| **MainForm.Itemizado.ClipboardPaste.cs** | 335 | ITEMIZADO | ProjectSession | gridItemizado | Features/Itemizado/ | 1 |
| **MainForm.ItemizadoProFeatures.cs** | 181 | ITEMIZADO | ProjectSession | gridItemizado | Features/Itemizado/ | 1 |
| **MainForm.ExcelPaste.cs** | 197 | ITEMIZADO | ProjectSession | Ninguno | Features/Itemizado/ | 1 |
| **MainForm.Materiales.cs** | 1,080 | PRECIOS | ProjectSession, AppServices | gridMateriales | Features/Precios/ | 2 |
| MainForm.Materiales.Virtual.cs | 178 | PRECIOS | gridMateriales | Ninguno (virtual mode) | Features/Precios/ | 2 |
| MainForm.Materiales.SearchDebounce.cs | 36 | PRECIOS | — | Ninguno | Features/Precios/ | 2 |
| MainForm.Compra.cs | 332 | PRECIOS | ProjectSession | Ninguno | Features/Precios/ | 2 |
| MainForm.PriceAudit.cs | 114 | PRECIOS | ProjectSession | Ninguno | Features/Precios/ | 2 |
| **MainForm.ApuPro.cs** | 746 | APU | ProjectSession, AppServices | Varios | Features/Apu/ | 3 |
| MainForm.ApuAuto.cs | 227 | APU | ProjectSession | Ninguno | Features/Apu/ | 3 |
| **MainForm.RevisionFinal.cs** | 804 | REVISIÓN | ProjectSession, AppServices | gridRevisionFinal | Features/Revision/ | 4 |
| MainForm.FinalReviewCache.cs | 67 | REVISIÓN | BudgetCalculation | Ninguno | Features/Revision/ | 4 |
| **MainForm.Export.cs** | 504 | EXPORTACIÓN | ProjectSession, AppServices | Ninguno | Features/Exportacion/ | 5 |
| MainForm.Excel.cs | 871 | EXPORTACIÓN | ClosedXML, ProjectSession | Ninguno | Features/Exportacion/ | 5 |
| MainForm.OfferPdf.cs | 748 | EXPORTACIÓN | ProjectSession | Ninguno | Features/Exportacion/ | 5 |
| MainForm.PresentationLetter.cs | 276 | EXPORTACIÓN | ProjectSession | Ninguno | Features/Exportacion/ | 5 |
| MainForm.Bc3.cs | 106 | EXPORTACIÓN | Bc3ProjectExchange | Ninguno | Features/Exportacion/ | 5 |
| **MainForm.Plantillas.cs** | 1,104 | PLANTILLAS | ProjectSession, AppServices | gridPlantillas, gridPlantillaItems | Features/Plantillas/ | 6 |
| MainForm.TemplateImportExport.cs | 539 | PLANTILLAS | ProjectSession | Ninguno | Features/Plantillas/ | 6 |
| **MainForm.Project.cs** | 588 | PROYECTO | ProjectSession, SQLiteProjectStore | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectLoader.cs | 202 | PROYECTO | ProjectSession, SQLiteProjectStore | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectHome.cs | 86 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectCenter.cs | 353 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectState.cs | 187 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectSwitcher.cs | 296 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.ProjectSessionEvents.cs | 82 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.Database.cs | 526 | PROYECTO | SQLite | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.AutoSave.cs | 308 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.History.cs | 569 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.Versiones.cs | 815 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.StatusHistory.cs | 312 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| MainForm.Onboarding.cs | 166 | PROYECTO | ProjectSession | Ninguno | ProjectLifecycle/ | 7 |
| **MainForm.Dashboard.cs** | 1,085 | DASHBOARD | ProjectSession, AppServices | Paneles | Features/Dashboard/ | 8 |
| MainForm.DashboardExpandedCharts.cs | 172 | DASHBOARD | ProjectSession | Ninguno | Features/Dashboard/ | 8 |
| MainFormDashboardServices.cs | 46 | DASHBOARD | — | Ninguno | Features/Dashboard/ | 8 |
| **MainForm.CubicacionProxy.cs** | 45 | CUBICACIÓN | ProjectSession | Ninguno | Pages/CubicacionPage/ | 9 |
| **MainForm.Preferences.cs** | 726 | PREFERENCIAS | ProjectSession, AppServices | Varios | Features/Preferencias/ | — |
| MainForm.Monedas.cs | 172 | MONEDAS | Services | Ninguno | Ya extraído (MonedasPage) | 0 |
| MainForm.Calculadora.cs | 95 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.Clientes.cs | 444 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.EstadosPago.cs | 652 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.ChangeOrders.cs | 762 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.Varianza.cs | 141 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.Comparador.cs | 784 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.ReajustePrecios.cs | 304 | LEGACY | ProjectSession | — | Eliminar | 0 |
| MainForm.Sensibilidad.cs | 632 | LEGACY | ProjectSession, AppServices | — | Eliminar / mantener solo en Dashboard | 0 |
| MainForm.SidebarCompact.cs | 101 | SHELL | — | Panel | AppNavigationBar | 10 |

## Prioridades de migración

| Prioridad | FASE | Módulo | Archivos | Líneas totales |
|---|---|---|---|---|
| 0 | Infraestructura | PageNavigator + WorkspacePageFactory | Completado | — |
| 1 | Itemizado | Extraer de MainForm | 4 archivos → 6+ nuevos | ~1,228 → migrar |
| 2 | Precios | Extraer de MainForm | 5 archivos → 5+ nuevos | ~1,740 → migrar |
| 3 | APU | Extraer de MainForm | 2 archivos → 4+ nuevos | ~973 → migrar |
| 4 | Revisión | Extraer de MainForm | 2 archivos → 3+ nuevos | ~871 → migrar |
| 5 | Exportación | Extraer de MainForm | 5 archivos, eliminar ClosedXML directo | ~2,505 → migrar |
| 6 | Plantillas | Extraer de MainForm | 2 archivos → 3+ nuevos | ~1,643 → migrar |
| 7 | Proyecto | Extraer ciclo de vida | 13 archivos → varios | ~4,445 → migrar |
| 8 | Dashboard | Consolidar | 3 archivos + duplicado Pages/ | ~1,303 → consolidar |
| 9 | Cubicación | Adaptar contrato | 1 archivo proxy + Pages/ existente | ~45 → adaptar |
| 10 | Shell | Extraer visual | 6+ archivos | ~2,700 → extraer |
| 11 | Limpieza | Eliminar legacy + adaptadores | ~10 archivos | ~3,000+ → eliminar |
