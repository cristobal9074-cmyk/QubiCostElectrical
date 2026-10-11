using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CommandPalette;

public sealed class CommandPaletteEngine
{
    private readonly BudgetProjectSession session;
    private readonly IShellNavigationHost shell;
    private readonly IPriceBankStore priceBankStore;
    private readonly IProjectWorkspaceService workspace;
    private readonly WpfUndoService undo;

    public CommandPaletteEngine(
        BudgetProjectSession session,
        IShellNavigationHost shell,
        IPriceBankStore priceBankStore,
        IProjectWorkspaceService workspace,
        WpfUndoService undo)
    {
        this.session = session;
        this.shell = shell;
        this.priceBankStore = priceBankStore;
        this.workspace = workspace;
        this.undo = undo;
    }

    public IReadOnlyList<CommandPaletteItemVm> Search(
        string rawQuery,
        CommandPaletteCategory categoryFilter = CommandPaletteCategory.All,
        Action<string>? showToast = null)
    {
        var query = rawQuery?.Trim() ?? string.Empty;
        var results = new List<CommandPaletteItemVm>();

        // 1. Detección de comandos de lenguaje natural si hay texto
        if (!string.IsNullOrWhiteSpace(query))
        {
            var nlActions = NaturalLanguageActionParser.Parse(query, session, shell, priceBankStore, undo, showToast);
            foreach (var act in nlActions)
            {
                if (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Actions)
                {
                    results.Add(act);
                }
            }
        }

        // 2. Módulos y Comandos del Sistema (sin rutas Hidden / ciclo de obra)
        if (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Navigation || categoryFilter == CommandPaletteCategory.Actions)
        {
            foreach (var item in GetStaticNavigationItems())
            {
                var score = FuzzyMatcher.Score(item.Title, query, item.Subtitle);
                if (score > 0)
                {
                    item.Score = score;
                    results.Add(item);
                }
            }
        }

        // 3. Partidas Contractuales del Proyecto Activo
        var hasOpenProject = workspace.HasOpenProject || session.Itemizado.Count > 0;
        var partidaMap = session.Itemizado
            .Where(p => !p.EsTitulo)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        if (hasOpenProject && (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Partidas))
        {
            foreach (var partida in session.Itemizado)
            {
                if (partida.EsTitulo) continue;

                var label = !string.IsNullOrWhiteSpace(partida.Item)
                    ? $"{partida.Item} — {partida.Descripcion}"
                    : partida.Descripcion;

                var secondary = $"Partida contractual #{partida.Item} · {partida.Unidad} · Cant: {partida.Cantidad:N2}";
                var score = FuzzyMatcher.Score(partida.Descripcion, query, partida.Item);
                if (score > 0)
                {
                    var pId = partida.Id;
                    var pItem = partida.Item;
                    var pDesc = partida.Descripcion;

                    results.Add(new CommandPaletteItemVm(
                        label,
                        secondary,
                        $"#{partida.Item}",
                        CommandPaletteCategory.Partidas,
                        () =>
                        {
                            session.PendingTakeoffNavigation = new TakeoffNavigationContext(pId, pItem, pDesc);
                            shell.RequestNavigate(ShellRoute.Takeoff, session.PendingTakeoffNavigation);
                            return Task.CompletedTask;
                        },
                        icon: "\uE8A5",
                        score: score + 50));
                }
            }
        }

        // 4. Recursos y Materiales en Cubicación (de TODAS las partidas del proyecto)
        if (hasOpenProject && (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Cubicacion || categoryFilter == CommandPaletteCategory.Materiales))
        {
            var cubicacionHits = 0;
            const int maxCubicacionHits = 35;

            foreach (var row in session.Cubicacion)
            {
                if (cubicacionHits >= maxCubicacionHits) break;
                if (string.IsNullOrWhiteSpace(row.Descripcion) && string.IsNullOrWhiteSpace(row.Codigo)) continue;

                var score = FuzzyMatcher.Score(row.Descripcion, query, $"{row.Codigo} {row.Categoria}");
                if (score > 0)
                {
                    cubicacionHits++;
                    var rItemId = row.ItemId;
                    var rRowId = row.Id;
                    var rCodigo = row.Codigo ?? "";
                    var rDesc = row.Descripcion ?? "";

                    var parentPartida = partidaMap.TryGetValue(rItemId, out var p) ? p : null;
                    var partidaLabel = parentPartida != null ? $"Partida {parentPartida.Item}" : "Cubicación";
                    var partidaDesc = parentPartida?.Descripcion ?? "";

                    var title = !string.IsNullOrWhiteSpace(row.Codigo) && !string.IsNullOrWhiteSpace(row.Descripcion)
                        ? $"{row.Codigo} — {row.Descripcion}"
                        : (!string.IsNullOrWhiteSpace(row.Descripcion) ? row.Descripcion : (row.Codigo ?? "Recurso"));

                    var priceStr = row.PrecioUnitClpConFallback > 0
                        ? $" · ${row.PrecioUnitClpConFallback:N0} / {row.Unidad}"
                        : (!string.IsNullOrWhiteSpace(row.Unidad) ? $" · {row.Unidad}" : "");

                    var cantStr = row.Cantidad > 0 ? $" · Cant: {row.Cantidad:N2}" : "";
                    var subtitle = $"{partidaLabel} ({partidaDesc}){cantStr}{priceStr}";

                    results.Add(new CommandPaletteItemVm(
                        title,
                        subtitle,
                        partidaLabel,
                        CommandPaletteCategory.Cubicacion,
                        () =>
                        {
                            session.PendingTakeoffNavigation = new TakeoffNavigationContext(
                                rItemId,
                                parentPartida?.Item ?? "",
                                partidaDesc,
                                TargetRowId: rRowId,
                                TargetCode: rCodigo);
                            shell.RequestNavigate(ShellRoute.Takeoff, session.PendingTakeoffNavigation);
                            return Task.CompletedTask;
                        },
                        icon: "\uE805",
                        score: score + 45));
                }
            }
        }

        // 5. Recursos y Mano de Obra en APUs de TODAS las partidas
        if (hasOpenProject && (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Apu || categoryFilter == CommandPaletteCategory.Materiales))
        {
            var apuHits = 0;
            const int maxApuHits = 25;

            foreach (var apu in session.Apus)
            {
                if (apuHits >= maxApuHits) break;
                if (apu.Recursos == null) continue;

                var parentPartida = partidaMap.TryGetValue(apu.ItemId, out var p) ? p : null;
                var partidaLabel = parentPartida != null ? $"Partida {parentPartida.Item}" : $"Partida {apu.ItemId}";

                foreach (var rec in apu.Recursos)
                {
                    if (apuHits >= maxApuHits) break;
                    if (string.IsNullOrWhiteSpace(rec.Descripcion) && string.IsNullOrWhiteSpace(rec.CodigoMaterial)) continue;

                    var score = FuzzyMatcher.Score(rec.Descripcion, query, rec.CodigoMaterial);
                    if (score > 0)
                    {
                        apuHits++;
                        var aItemId = apu.ItemId;
                        var aCodigo = rec.CodigoMaterial ?? "";
                        var aDesc = rec.Descripcion ?? "";

                        var title = !string.IsNullOrWhiteSpace(rec.CodigoMaterial) && !string.IsNullOrWhiteSpace(rec.Descripcion)
                            ? $"{rec.CodigoMaterial} — {rec.Descripcion}"
                            : (!string.IsNullOrWhiteSpace(rec.Descripcion) ? rec.Descripcion : (rec.CodigoMaterial ?? "Insumo APU"));

                        var priceStr = rec.PrecioBaseUf > 0
                            ? $" · {rec.PrecioBaseUf:N2} UF / {rec.Unidad}"
                            : (rec.PrecioUf > 0 ? $" · {rec.PrecioUf:N2} UF / {rec.Unidad}" : "");

                        results.Add(new CommandPaletteItemVm(
                            title,
                            $"APU {partidaLabel} · Rend: {rec.Rendimiento:N2} {rec.Unidad}{priceStr}",
                            parentPartida != null ? $"Partida {parentPartida.Item}" : "APU",
                            CommandPaletteCategory.Apu,
                            () =>
                            {
                                shell.RequestNavigate(ShellRoute.Apu, new ApuNavContext(aItemId, aCodigo, aDesc));
                                return Task.CompletedTask;
                            },
                            icon: "\uE8C0",
                            score: score + 40));
                    }
                }
            }
        }

        // 6. Tableros y Circuitos de Servicios Comunes (Cubicación SC)
        if (hasOpenProject && (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.TablerosSc))
        {
            foreach (var cuadro in session.CubicacionSC)
            {
                var score = FuzzyMatcher.Score(cuadro.Nombre, query, cuadro.Descripcion);
                if (score > 0)
                {
                    results.Add(new CommandPaletteItemVm(
                        $"Tablero: {cuadro.Nombre}",
                        $"{cuadro.Circuitos.Count} circuitos · {cuadro.Descripcion}",
                        "Tablero",
                        CommandPaletteCategory.TablerosSc,
                        () =>
                        {
                            shell.RequestNavigate(ShellRoute.CubicacionSc);
                            return Task.CompletedTask;
                        },
                        icon: "\uE74C",
                        score: score + 40));
                }

                // Circuitos individuales
                foreach (var cir in cuadro.Circuitos)
                {
                    var cirTitle = $"{cuadro.Nombre} / C{cir.Numero}: {cir.Descripcion}";
                    var cirScore = FuzzyMatcher.Score(cir.Descripcion, query, $"{cuadro.Nombre} C{cir.Numero}");
                    if (cirScore > 0)
                    {
                        results.Add(new CommandPaletteItemVm(
                            cirTitle,
                            $"Tipo: {cir.Tipo} · Cable: {cir.ConductorDerivacion} {cir.SeccionDerivacion} · {cir.PotenciaW:N0} W",
                            $"C{cir.Numero}",
                            CommandPaletteCategory.TablerosSc,
                            () =>
                            {
                                shell.RequestNavigate(ShellRoute.CubicacionSc);
                                return Task.CompletedTask;
                            },
                            icon: "\uE719",
                            score: cirScore));
                    }
                }
            }
        }

        // 7. Banco de Precios Central
        if (categoryFilter == CommandPaletteCategory.All || categoryFilter == CommandPaletteCategory.Materiales)
        {
            var materialHits = 0;
            const int maxMaterials = 15;

            foreach (var entry in priceBankStore.GetAll())
            {
                if (materialHits >= maxMaterials) break;

                var score = FuzzyMatcher.Score(entry.Descripcion, query, entry.Codigo);
                if (score > 0)
                {
                    materialHits++;
                    var bCode = entry.Codigo ?? "";
                    results.Add(new CommandPaletteItemVm(
                        $"{entry.Codigo} — {entry.Descripcion}",
                        $"Banco de Precios · ${entry.MatClpConFallback:N0} / {entry.Unidad}",
                        "Insumo Central",
                        CommandPaletteCategory.Materiales,
                        () =>
                        {
                            shell.RequestNavigate(ShellRoute.Prices, new PriceBankNavigationContext(bCode));
                            return Task.CompletedTask;
                        },
                        icon: "\uE8B8",
                        score: score + 30));
                }
            }
        }

        // Ordenar por relevancia descendente
        return results
            .OrderByDescending(r => r.IsHeroAction)
            .ThenByDescending(r => r.Score)
            .Take(50)
            .ToList();
    }

    private IEnumerable<CommandPaletteItemVm> GetStaticNavigationItems()
    {
        yield return new CommandPaletteItemVm("Dashboard / Cockpit Financiero", "Panel ejecutivo de control y resumen de rentabilidad", "Alt+1", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Dashboard); return Task.CompletedTask; }, "\uE80F");
        yield return new CommandPaletteItemVm("Itemizado Contractual", "Estructura de partidas, cubicaciones base y modelos comerciales", "Alt+2", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Itemized); return Task.CompletedTask; }, "\uE8A5");
        yield return new CommandPaletteItemVm("Cubicación de Partidas", "Desarrollo de mediciones, fórmulas y memorias de cálculo", "Alt+3", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Takeoff); return Task.CompletedTask; }, "\uE805");
        yield return new CommandPaletteItemVm("Cubicación SC (Circuitos)", "Tableros, alimentadores y circuitos de servicios comunes", "Alt+Shift+3", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.CubicacionSc); return Task.CompletedTask; }, "\uE74C");
        yield return new CommandPaletteItemVm("Análisis de Precios Unitarios (APU)", "Composición de materiales, mano de obra y equipos", "Alt+4", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Apu); return Task.CompletedTask; }, "\uE8C0");
        yield return new CommandPaletteItemVm("Estructura de Costos y Cierre", "Gastos generales, utilidad, factores por familia y políticas", "Alt+C", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Costo); return Task.CompletedTask; }, "\uE945");
        yield return new CommandPaletteItemVm("Banco de Precios Central", "Biblioteca corporativa de insumos y rendimientos", "Alt+6", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Prices); return Task.CompletedTask; }, "\uE8B8");
        yield return new CommandPaletteItemVm("Cotizaciones de Proveedores", "Comparador de ofertas, listas de precios y adjudicaciones", "Alt+7", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Quotes); return Task.CompletedTask; }, "\uE775");
        yield return new CommandPaletteItemVm("Estados de pago", "Cronograma mensual de cobros, anticipos y retenciones de la obra", "", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.EstadosPagos); return Task.CompletedTask; }, "\uE787");
        yield return new CommandPaletteItemVm("Lista Consolidada de Compra", "Explosión de insumos de compra con stock y mermas", "Alt+8", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Purchases); return Task.CompletedTask; }, "\uE7BF");
        yield return new CommandPaletteItemVm("Oferta Comercial y Exportación", "Asistente de propuesta formal, cartas y reportes PDF/Excel", "F8 · Alt+O", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.OfferDocuments); return Task.CompletedTask; }, "\uE896");
        yield return new CommandPaletteItemVm("Versiones y Trazabilidad", "Historial de versiones y auditoría del presupuesto", "Alt+0", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Versions); return Task.CompletedTask; }, "\uE81C");
        yield return new CommandPaletteItemVm("Preferencias", "Configuración general, roles y temas de la aplicación", "Alt+,", CommandPaletteCategory.Navigation, () => { shell.RequestNavigate(ShellRoute.Preferences); return Task.CompletedTask; }, "\uE713");
        yield return new CommandPaletteItemVm("Abrir proyecto", "Abrir un presupuesto existente", "Ctrl+O", CommandPaletteCategory.Actions, () => shell.OpenProjectAsync(), "\uE8E5");

        if (!session.IsReadOnly)
        {
            yield return new CommandPaletteItemVm("Nuevo proyecto", "Crear un nuevo presupuesto", "Ctrl+N", CommandPaletteCategory.Actions, () => shell.NewProjectAsync(), "\uE710");
            yield return new CommandPaletteItemVm("Guardar proyecto", "Guardar cambios en archivo SQLite .qce", "Ctrl+S", CommandPaletteCategory.Actions, () => shell.SaveProjectAsync(), "\uE74E");
            yield return new CommandPaletteItemVm("Guardar como", "Guardar copia del presupuesto con nuevo nombre", "Ctrl+Shift+S", CommandPaletteCategory.Actions, () => shell.SaveProjectAsAsync(), "\uE792");
            yield return new CommandPaletteItemVm("Cerrar proyecto", "Cerrar el presupuesto activo", "Ctrl+W", CommandPaletteCategory.Actions, () => shell.CloseProjectAsync(), "\uE8BB");
            yield return new CommandPaletteItemVm("Buscar y Reemplazar Material", "Reemplazar un material en todas las partidas manteniendo metrajes y fórmulas", "Ctrl+H", CommandPaletteCategory.Actions, () =>
            {
                shell.RequestNavigate(ShellRoute.Takeoff);
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    MaterialBatchReplaceDialog.Execute(System.Windows.Application.Current?.MainWindow, session, priceBankStore, undo);
                    shell.RefreshCurrentPage();
                });
                return Task.CompletedTask;
            }, "\uE8B8");
            yield return new CommandPaletteItemVm("Cambio de Línea o Familia Masivo", "Sustitución masiva de familias de materiales e ingeniería de valor", "Ctrl+Shift+H", CommandPaletteCategory.Actions, () =>
            {
                shell.RequestNavigate(ShellRoute.Takeoff);
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    FamilyBatchReplaceDialog.Execute(System.Windows.Application.Current?.MainWindow, session, priceBankStore, undo);
                    shell.RefreshCurrentPage();
                });
                return Task.CompletedTask;
            }, "\uE8B8");
            yield return new CommandPaletteItemVm("Deshacer", "Deshacer la última acción realizada", "Ctrl+Z", CommandPaletteCategory.Actions, () => { undo.Undo(session); shell.RefreshCurrentPage(); return Task.CompletedTask; }, "\uE7A7");
            yield return new CommandPaletteItemVm("Rehacer", "Rehacer la última acción deshecha", "Ctrl+Y", CommandPaletteCategory.Actions, () => { undo.Redo(session); shell.RefreshCurrentPage(); return Task.CompletedTask; }, "\uE7A6");
        }
    }
}
