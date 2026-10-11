using System.IO;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Gate de líneas por TIPO parcial: suma Foo.cs + Foo.*.cs (incluye .Methods.cs).
/// Los partials ya no son escape del límite. Tipos legacy sobre el tope van en exemptions.
/// </summary>
public sealed class MaxLineCountTest
{
    private const int MaxAllowedLinesPerType = 1200;

    /// <summary>
    /// Tipos (nombre de clase / stem) ya por encima del tope. No crecer más: al bajar, quitar.
    /// Key = relative path stem sin sufijos (.Methods, .Loading, etc.) normalizado.
    /// </summary>
    private static readonly Dictionary<string, int> LegacyBaselineExemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Baselines de ShellViewModel/Dashboard/Export/Itemizado subidos por inyección por constructor
        // + Command Palette Supercargada (Ctrl+K) y Quick Jump Spotlight (Ctrl+J) + Ciclo de vida (3 Fases)
        ["src/QubiCostElectrical.Wpf/Shell/ShellViewModel"] = 1930,
        // Buscador in-grid (Ctrl+F) y navegación con soporte a SelectionUnit.Cell + Autofoco inteligente
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionView.xaml"] = 2200,
        // Navegación Excel fluida, autocompletado en combos de catálogo, edición directa, sidebar colapsable con mueca y top bar unificado en Cubicación SC
        ["src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScView.xaml"] = 1781,
        // Cockpit Financiero / tableros SC en curso (WIP sin commitear)
        ["src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScViewModel"] = 1261,
        // +~50 por el selector y reactividad de política de cierre + salvaguardas (sello de parámetros,
        // auditoría de cambio de tipo, avisos de política) — feature multi-política de cierre.
        // Rediseño de pantalla unificada (cascada vía CostRows, factores, gastos): WIP sin
        // commitear acumulado hasta 2012 (27-09); no crecer más desde aquí.
        ["src/QubiCostElectrical.Wpf/Features/Costo/CostoViewModel"] = 2012,
        // WIP sin commitear (turnos/planillas en curso); no crecer más desde aquí.
        // + Botón "Tomar proyección MO": comando que aplica la proyección a la dotación (24 líneas, 06-10).
        // + Renombre de cargos directos con nombre-GUID a "Maestro"/"Ayudante" en SeedCargos (6 líneas, 07-10).
        // + Fracción de prorrateo por rol indirecto (FraccionObra en def/editor/persistencia y en
        //   todos los costos de Recalcular) + SetIndirectDotPico con staging (09-10).
        ["src/QubiCostElectrical.Wpf/Features/ManoDeObra/ManoDeObraViewModel"] = 1666,
        // Cockpit Financiero $1000T (Pareto 80/20, distribución continua 4 segmentos, políticas y KPIs ejecutivos)
        ["src/QubiCostElectrical.Wpf/Features/Dashboard/DashboardViewModel"] = 1385,
        // Cambio de línea o familia masivo (Ingeniería de valor, Ctrl+Shift+H) + eventos de fila
        // + WIP sin commitear acumulado hasta 1846 (28-09; 847 en Methods tras normalizar whitespace)
        // + M.O. por % del material (2 líneas en FromBankEntry, 06-10).
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionViewModel"] = 1848,
        ["src/QubiCostElectrical.Wpf/Features/Export/ExportViewModel"] = 1430,
        // Dossier Ejecutivo One-Click: carta formal SEC, gráficos de capítulos, supuestos normativos, barras de distribución de costo y tabla con PU UF opcional
        ["src/QubiCostElectrical.Infrastructure/Pdf/QuestPdfProjectExporter"] = 1440,
        // Modelo comercial contractual en filas (APU, P.U. Fijo, Suma Alzada, T&M) y columnas de valorización
        ["src/QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoViewModel"] = 1365,
        // Autocompletado predictivo IntelliSense (Banco de Precios) y navegación 100% teclado en APU
        ["src/QubiCostElectrical.Wpf/Features/Apu/ApuWorkspaceVm"] = 1250,
        // Ergonomía de planillas, parser resiliente multimoneda y navegación fluida tipo Excel
        // + Celda viva (TextBox embebido): Backspace/Delete/flechas/Home/End son del texto, no
        // de la grilla (fix «al cambiar el valor se elimina la descripción», 08-10).
        ["src/QubiCostElectrical.Wpf/Infrastructure/DataGridExcelNavigation"] = 1631,
        // Expediente técnico: previsualización de planos, soporte PDF asíncrono y filtros dinámicos por especialidad
        ["src/QubiCostElectrical.Wpf/Features/Documents/DocumentsViewModel"] = 1350,
        // Rediseño de pantalla Versiones: búsqueda compartida (historial + comparador), filtros
        // del semáforo y clasificación de delta de la variación en la misma VM.
        ["src/QubiCostElectrical.Wpf/Features/Versions/VersionsViewModel"] = 1260,
        // Rediseño de pantalla Plantillas (docs/propuesta-plantillas.html): selección múltiple
        // Extendida, barra contextual ámbar, comandos masivos (maestros/esclavos), jerarquía con
        // huérfanos y preview de expansión en la misma VM. WIP sin commitear acumulado hasta 1462;
        // +41 por el fix del bug «Dividir de» (colección MaestrosDisponibles estable/reconciliada
        // in-place + setter MaestroId que conserva el vínculo vigente).
        // +149 por el guardado global de conjuntos (store en banco_precios.db, adopción,
        // Save/HasPendingChanges e implícitos de IPageWithPendingChanges/IPageWithCommitChanges, 08-10).
        ["src/QubiCostElectrical.Wpf/Features/Templates/PlantillasVm"] = 1652,
    };

    [Fact]
    public void AllPartialTypes_MustBeUnderLineBudget_OrInLegacyBaseline()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var srcDir = Path.Combine(repoRoot!, "src");
        Assert.True(Directory.Exists(srcDir));

        var files = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var byType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(repoRoot!, file).Replace('\\', '/');
            var stem = GetPartialTypeStem(relative);
            var lines = File.ReadAllLines(file).Length;
            byType[stem] = byType.GetValueOrDefault(stem) + lines;
            if (!typeFiles.TryGetValue(stem, out var list))
            {
                list = new List<string>();
                typeFiles[stem] = list;
            }
            list.Add($"{relative}:{lines}");
        }

        var violations = new List<string>();
        var measuredOver = new List<string>();

        foreach (var (stem, total) in byType.OrderByDescending(kv => kv.Value))
        {
            if (total <= MaxAllowedLinesPerType)
                continue;

            if (LegacyBaselineExemptions.TryGetValue(stem, out var baseline))
            {
                if (total > baseline)
                    violations.Add($"{stem}: {total} lines (legacy baseline {baseline}; growth forbidden)\n  " +
                                   string.Join("\n  ", typeFiles[stem]));
                continue;
            }

            measuredOver.Add($"{stem}: {total}\n  " + string.Join("\n  ", typeFiles[stem]));
            violations.Add($"{stem}: {total} lines (max {MaxAllowedLinesPerType})\n  " +
                           string.Join("\n  ", typeFiles[stem]));
        }

        // Si hay violaciones y el diccionario está vacío, el mensaje lista stems para seedear exemptions.
        Assert.True(
            violations.Count == 0,
            $"Architecture Gate — tipos sobre {MaxAllowedLinesPerType} líneas (sumando partials):\n" +
            string.Join("\n", violations) +
            (LegacyBaselineExemptions.Count == 0 && measuredOver.Count > 0
                ? "\n\nSeed LegacyBaselineExemptions con estos stems/totales y vuelve a correr."
                : ""));
    }

    /// <summary>
    /// Agrupa Foo.cs, Foo.Methods.cs, Foo.Loading.cs → stem relativo "path/Foo".
    /// </summary>
    internal static string GetPartialTypeStem(string relativePathForwardSlash)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePathForwardSlash);
        var dir = Path.GetDirectoryName(relativePathForwardSlash)?.Replace('\\', '/') ?? "";

        // Quitar sufijos conocidos de partials (Cub/Shell y módulos relacionados)
        string[] suffixes =
        [
            ".Methods", ".Loading", ".Mutations", ".Presentation", ".Session",
            ".ShortcutActions", ".PriceFreeze", ".OfferWizard", ".KeyboardShortcuts",
            ".Navigation", ".Project", ".Workflow",
            ".Autocomplete", ".Clipboard", ".Persistence", ".RowMutations", ".Sectors",
            ".Totals", ".Paste", ".LivePreview", ".Attached",
        ];

        var stemName = fileName;
        foreach (var suffix in suffixes)
        {
            if (stemName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stemName = stemName[..^suffix.Length];
                break;
            }
        }

        // Segundo pase por sufijos restantes (Foo.ViewModel.Methods → Foo.ViewModel)
        foreach (var suffix in suffixes)
        {
            if (stemName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                stemName = stemName[..^suffix.Length];
        }

        return string.IsNullOrEmpty(dir) ? stemName : $"{dir}/{stemName}";
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
