using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CommandPalette;

public static class NaturalLanguageActionParser
{
    private static readonly Regex PctNumberRegex = new(@"(\d+(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);
    private static readonly Regex PureNumberRegex = new(@"\b(\d+(?:[.,]\d+)?)\b", RegexOptions.Compiled);

    public static IEnumerable<CommandPaletteItemVm> Parse(
        string rawQuery,
        BudgetProjectSession session,
        IShellNavigationHost shell,
        Action<string>? showToast = null)
        => Parse(rawQuery, session, shell, null, null, showToast);

    public static IEnumerable<CommandPaletteItemVm> Parse(
        string rawQuery,
        BudgetProjectSession session,
        IShellNavigationHost shell,
        IPriceBankStore? priceBankStore,
        WpfUndoService? undo,
        Action<string>? showToast = null)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
            yield break;

        var q = rawQuery.Trim().ToLowerInvariant();
        var hasOpenProject = !string.IsNullOrWhiteSpace(session.CurrentProjectPath) ||
                             !string.IsNullOrWhiteSpace(session.ProjectInfo.Nombre);

        if (!hasOpenProject)
            yield break;

        var canEditBudget = !session.IsReadOnly && (shell.Host?.Permissions?.CanEditBudget ?? true);

        // 1. Margen / Utilidad
        if (canEditBudget && (q.Contains("margen") || q.Contains("utilidad") || q.StartsWith("ut ") || q.StartsWith("ut=")))
        {
            if (TryExtractNumber(q, out var val) && val >= 0 && val <= 100)
            {
                var current = session.ProjectInfo.UtilidadPct;
                yield return new CommandPaletteItemVm(
                    $"Aplicar Margen de Utilidad: {val:N1}%",
                    $"Actualizar utilidad del proyecto de {current:N1}% a {val:N1}% y recalcular costos",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        session.ProjectInfo.UtilidadPct = val;
                        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                        RefreshActivePage(shell);
                        showToast?.Invoke($"Margen de utilidad actualizado a {val:N1}%");
                        return Task.CompletedTask;
                    },
                    icon: "\uE945",
                    isHeroAction: true,
                    heroBadge: "MARGEN / UTILIDAD",
                    score: 1200);
            }
        }

        // 2. Gastos Generales (GG)
        if (canEditBudget && (q.Contains("gastos generales") || q.Contains("gg") || q.StartsWith("g.g")))
        {
            if (TryExtractNumber(q, out var val) && val >= 0 && val <= 100)
            {
                var current = session.ProjectInfo.GastosGeneralesPct;
                yield return new CommandPaletteItemVm(
                    $"Aplicar Gastos Generales: {val:N1}%",
                    $"Actualizar GG del proyecto de {current:N1}% a {val:N1}% y recalcular costos",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        session.ProjectInfo.GastosGeneralesPct = val;
                        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                        RefreshActivePage(shell);
                        showToast?.Invoke($"Gastos generales actualizados a {val:N1}%");
                        return Task.CompletedTask;
                    },
                    icon: "\uE825",
                    isHeroAction: true,
                    heroBadge: "GASTOS GENERALES",
                    score: 1200);
            }
        }

        // 3. Descuento
        if (canEditBudget && (q.Contains("descuento") || q.StartsWith("desc ") || q.StartsWith("desc=")))
        {
            if (TryExtractNumber(q, out var val) && val >= 0 && val <= 100)
            {
                var current = session.ProjectInfo.DescuentoPct;
                yield return new CommandPaletteItemVm(
                    $"Aplicar Descuento Comercial: {val:N1}%",
                    $"Actualizar descuento del proyecto de {current:N1}% a {val:N1}%",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        session.ProjectInfo.DescuentoPct = val;
                        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                        RefreshActivePage(shell);
                        showToast?.Invoke($"Descuento actualizado a {val:N1}%");
                        return Task.CompletedTask;
                    },
                    icon: "\uE8C0",
                    isHeroAction: true,
                    heroBadge: "DESCUENTO",
                    score: 1200);
            }
        }

        // 4. Factor de Riesgo / Imprevistos
        if (canEditBudget && (q.Contains("riesgo") || q.Contains("imprevisto") || q.Contains("contingencia")))
        {
            if (TryExtractNumber(q, out var val) && val >= 0 && val <= 100)
            {
                var current = session.ProjectInfo.RiesgoPct;
                yield return new CommandPaletteItemVm(
                    $"Aplicar Factor de Riesgo: {val:N1}%",
                    $"Actualizar provisión de riesgo de {current:N1}% a {val:N1}%",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        session.ProjectInfo.RiesgoPct = val;
                        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                        RefreshActivePage(shell);
                        showToast?.Invoke($"Factor de riesgo actualizado a {val:N1}%");
                        return Task.CompletedTask;
                    },
                    icon: "\uE7BA",
                    isHeroAction: true,
                    heroBadge: "RIESGO",
                    score: 1200);
            }
        }

        // 5. Ir a Tableros o Circuitos de Servicios Comunes (SC)
        if (q.StartsWith("tablero") || q.StartsWith("ir a tablero") || q.Contains("tdf") || q.Contains("tda"))
        {
            var cleanTerm = q.Replace("ir a tablero", "").Replace("tablero", "").Trim();
            if (!string.IsNullOrWhiteSpace(cleanTerm))
            {
                foreach (var cuadro in session.CubicacionSC)
                {
                    if (cuadro.Nombre.Contains(cleanTerm, StringComparison.OrdinalIgnoreCase) ||
                        cleanTerm.Contains(cuadro.Nombre.ToLowerInvariant()))
                    {
                        var boardName = cuadro.Nombre;
                        yield return new CommandPaletteItemVm(
                            $"Ir a Tablero SC: {boardName}",
                            $"Servicios Comunes · {cuadro.Circuitos.Count} circuitos instalados · {cuadro.Descripcion}",
                            "Enter ↵",
                            CommandPaletteCategory.TablerosSc,
                            () =>
                            {
                                shell.RequestNavigate(ShellRoute.CubicacionSc);
                                return Task.CompletedTask;
                            },
                            icon: "\uE74C",
                            isHeroAction: true,
                            heroBadge: "TABLERO SC",
                            score: 1100);
                        break;
                    }
                }
            }
        }

        // 6. Revisar partidas en $0 / sin APU
        if (q.Contains("partidas en $0") || q.Contains("partidas en cero") || q.Contains("sin apu") || q.Contains("revisar partidas"))
        {
            yield return new CommandPaletteItemVm(
                "Revisar Partidas en $0 y sin APU",
                "Abrir oferta y exportación (F8)",
                "F8",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.OfferDocuments);
                    return Task.CompletedTask;
                },
                icon: "\uE7BA",
                isHeroAction: true,
                heroBadge: "AUDITORÍA",
                score: 1150);
        }

        // 7. Duplicar circuito / alimentador
        if (q.Contains("duplicar circuito") || q.Contains("duplicar alimentador") || q.Contains("duplicar"))
        {
            yield return new CommandPaletteItemVm(
                "Duplicar Circuito / Alimentador (Ctrl+D)",
                "Cubicación SC → Duplicar circuito seleccionado con protecciones y canalizaciones",
                "Ctrl+D",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.CubicacionSc);
                    return Task.CompletedTask;
                },
                icon: "\uE8C8",
                isHeroAction: true,
                heroBadge: "SERVICIOS COMUNES",
                score: 1150);
        }

        // 8. Cargar plantillas RIC estándar SEC
        if (canEditBudget && (q.Contains("plantilla") || q.Contains("template")))
        {
            var templates = ProjectTemplateCatalog.All.Where(t => !t.EsEnBlanco).ToList();

            var matched = templates.Where(t =>
                q.Contains(t.Nombre.ToLowerInvariant()) ||
                (q.Contains("industrial") && t.Nombre.Contains("Industrial", StringComparison.OrdinalIgnoreCase)) ||
                (q.Contains("residencial") && t.Nombre.Contains("Residencial", StringComparison.OrdinalIgnoreCase)) ||
                (q.Contains("comercial") && t.Nombre.Contains("Comercial", StringComparison.OrdinalIgnoreCase)) ||
                (q.Contains("solar") && t.Nombre.Contains("Solar", StringComparison.OrdinalIgnoreCase))
            ).ToList();

            if (matched.Count == 0 && (q.Contains("cargar plantilla") || q == "plantilla" || q == "plantillas" || q.Contains("plantilla ric")))
            {
                matched = templates;
            }

            foreach (var tmpl in matched)
            {
                var template = tmpl;
                yield return new CommandPaletteItemVm(
                    $"Cargar Plantilla RIC: {template.Nombre}",
                    $"{template.Descripcion} · {template.PartidasCount} partidas/títulos estándar SEC",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        var audit = shell.Host?.Audit ?? new ProjectAuditRecorder(session);
                        var undoService = undo ?? shell.Host?.UndoService ?? new WpfUndoService();
                        ItemizadoTemplateCoordinator.LoadTemplate(
                            session,
                            template.Id,
                            undoService,
                            audit,
                            () => RefreshActivePage(shell),
                            msg => showToast?.Invoke(msg));
                        return Task.CompletedTask;
                    },
                    icon: "\uE8D6",
                    isHeroAction: true,
                    heroBadge: "PLANTILLA RIC",
                    score: 1300);
            }
        }

        // 9. Reemplazo masivo de materiales / familias
        if (canEditBudget && (q.Contains("reemplazar") || q.Contains("reemplazo") || q.StartsWith("cambiar ")))
        {
            var matchReplace = Regex.Match(q, @"(?:reemplazar|cambiar)\s+(?:material(?:es)?\s+)?([a-zA-Z0-9_-]+)\s+(?:por|a)\s+([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
            if (matchReplace.Success)
            {
                var src = matchReplace.Groups[1].Value.ToUpperInvariant();
                var dst = matchReplace.Groups[2].Value.ToUpperInvariant();
                yield return new CommandPaletteItemVm(
                    $"Reemplazar Materiales: {src} ➔ {dst}",
                    $"Abrir asistente de sustitución y conversión normativa de materiales ({src} ➔ {dst})",
                    "Enter ↵",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        var owner = System.Windows.Application.Current?.MainWindow;
                        var undoService = undo ?? shell.Host?.UndoService ?? new WpfUndoService();
                        var pbStore = priceBankStore ?? shell.Host?.PriceBankStore;
                        if (pbStore != null)
                        {
                            FamilyBatchReplaceDialog.Execute(owner, session, pbStore, undoService, src, dst);
                            RefreshActivePage(shell);
                        }
                        return Task.CompletedTask;
                    },
                    icon: "\uE8C8",
                    isHeroAction: true,
                    heroBadge: "REEMPLAZO MASIVO",
                    score: 1350);
            }
            else
            {
                yield return new CommandPaletteItemVm(
                    "Reemplazar Materiales y Familias (Ctrl+Shift+H)",
                    "Abrir asistente de sustitución masiva y conversión normativa de materiales",
                    "Ctrl+Shift+H",
                    CommandPaletteCategory.Actions,
                    () =>
                    {
                        var owner = System.Windows.Application.Current?.MainWindow;
                        var undoService = undo ?? shell.Host?.UndoService ?? new WpfUndoService();
                        var pbStore = priceBankStore ?? shell.Host?.PriceBankStore;
                        if (pbStore != null)
                        {
                            FamilyBatchReplaceDialog.Execute(owner, session, pbStore, undoService);
                            RefreshActivePage(shell);
                        }
                        return Task.CompletedTask;
                    },
                    icon: "\uE8C8",
                    isHeroAction: true,
                    heroBadge: "REEMPLAZO MASIVO",
                    score: 1250);
            }
        }

        // 10. Filtrar partidas pendientes, sin APU o sin cubicación
        if (q.Contains("filtrar pendientes") || q.Contains("partidas pendientes") || q.Contains("ver pendientes") || (q.Contains("pendientes") && !q.Contains("tarea")))
        {
            yield return new CommandPaletteItemVm(
                "Filtrar Partidas Pendientes",
                "Ir a Itemizado y filtrar únicamente partidas incompletas o pendientes de análisis",
                "Enter ↵",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.Itemized, new ItemizadoNavigationContext(FilterMode: ItemizadoFilterMode.Incompletos));
                    showToast?.Invoke("Mostrando partidas pendientes.");
                    return Task.CompletedTask;
                },
                icon: "\uE71C",
                isHeroAction: true,
                heroBadge: "FILTRO ITEMIZADO",
                score: 1280);
        }

        if (q.Contains("sin apu") || q.Contains("sin precio"))
        {
            yield return new CommandPaletteItemVm(
                "Filtrar Partidas sin APU / sin Precio",
                "Ir a Itemizado y mostrar partidas que no cuentan con análisis de precios unitarios",
                "Enter ↵",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.Itemized, new ItemizadoNavigationContext(FilterMode: ItemizadoFilterMode.SinPrecio));
                    showToast?.Invoke("Mostrando partidas sin APU.");
                    return Task.CompletedTask;
                },
                icon: "\uE7BA",
                isHeroAction: true,
                heroBadge: "FILTRO ITEMIZADO",
                score: 1280);
        }

        if (q.Contains("sin cubicar") || q.Contains("sin cubicacion"))
        {
            yield return new CommandPaletteItemVm(
                "Filtrar Partidas sin Cubicar",
                "Ir a Itemizado y mostrar partidas que no registran cantidades cubicadas",
                "Enter ↵",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.Itemized, new ItemizadoNavigationContext(FilterMode: ItemizadoFilterMode.SinCubicar));
                    showToast?.Invoke("Mostrando partidas sin cubicar.");
                    return Task.CompletedTask;
                },
                icon: "\uE81E",
                isHeroAction: true,
                heroBadge: "FILTRO ITEMIZADO",
                score: 1280);
        }

        // 11. Exportar oferta a PDF / Excel
        if (q.Contains("exportar pdf") || q.Contains("exportar a pdf") || q.Contains("exportar oferta") ||
            q.Contains("exportar excel") || q.Contains("exportar a excel") || q.Contains("generar oferta") || q.Contains("dossier"))
        {
            yield return new CommandPaletteItemVm(
                "Exportar Dossier de Oferta (PDF / Excel)",
                "Asistente de cierre comercial, validación técnica y generación de documentos",
                "Alt+O",
                CommandPaletteCategory.Actions,
                () =>
                {
                    shell.RequestNavigate(ShellRoute.OfferDocuments);
                    return Task.CompletedTask;
                },
                icon: "\uE896",
                isHeroAction: true,
                heroBadge: "EXPORTACIÓN",
                score: 1300);
        }
    }

    private static bool TryExtractNumber(string text, out decimal value)
    {
        value = 0m;
        var pctMatch = PctNumberRegex.Match(text);
        if (pctMatch.Success)
        {
            var raw = pctMatch.Groups[1].Value.Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        var numMatch = PureNumberRegex.Match(text);
        if (numMatch.Success)
        {
            var raw = numMatch.Groups[1].Value.Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private static void RefreshActivePage(IShellNavigationHost shell)
    {
        if (shell is ShellViewModel shellVm)
        {
            shellVm.RefreshCurrentPage();
        }
    }
}
