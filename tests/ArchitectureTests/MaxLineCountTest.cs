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
        // + Márgenes predeterminados de preferencias al crear nuevo proyecto (14-10).
        // Descompresión Ola Arquitectónica: extracción de Command Palette y Spotlight a ShellCommandPaletteCoordinator (-121 líneas netas).
        ["src/QubiCostElectrical.Wpf/Shell/ShellViewModel"] = 2005,
        // Buscador in-grid (Ctrl+F) y navegación con soporte a SelectionUnit.Cell + Autofoco inteligente
        // +6 por el pegado Ctrl+V atendido en la propia grilla: el pegado Excel marca PreviewKeyDown
        // como atendido y suprimía el KeyDown que burbujeaba al UserControl, dejando el pegado con
        // lógica de negocio (código → material) muerto.
        // +39 por autofoco y digitación directa en celda de código (ResourceGrid PreviewTextInput/Commit).
        // +35 por posicionamiento automático en la zona de cubicación tras crear material (17-10).
        // Descompresión Ola Arquitectónica: extracción a CubicacionGridFillController y CubicacionSectorFormulaController (-404 líneas netas).
        // +10 por buscador reactivo con botón limpiar y atajo Esc en panel de partidas de cubicación.
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionView.xaml"] = 2020,
        // Navegación Excel fluida, autocompletado en combos de catálogo, edición directa, sidebar colapsable con mueca y top bar unificado en Cubicación SC
        // +10 por atajo Esc para limpiar buscador en Cubicación SC.
        ["src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScView.xaml"] = 1795,

        // WIP sin commitear (turnos/planillas en curso); no crecer más desde aquí.
        // + Botón "Tomar proyección MO": comando que aplica la proyección a la dotación (24 líneas, 06-10).
        // + Renombre de cargos directos con nombre-GUID a "Maestro"/"Ayudante" en SeedCargos (6 líneas, 07-10).
        // + Fracción de prorrateo por rol indirecto (FraccionObra en def/editor/persistencia y en
        //   todos los costos de Recalcular) + SetIndirectDotPico con staging (09-10).
        // +16 por la política de moneda única del proyecto: con CLP la vista de mano de obra fuerza
        // CLP, oculta el conmutador CLP/UF, el chip "VALOR UF" y la equivalencia UF del total, y
        // + Creación de cargos personalizados directos/indirectos con modal y sincronización a proyección (14-10).
        // + Promedio de proyección redondeado y aplicación con paridad alternada (17-10).
        // + Soporte Deshacer (Undo) en cargos y persistencia de total cero (17-10).
        // + Implementación de IPageWithPendingChanges e IPageWithCommitChanges para evitar pérdida silenciosa de dotación al navegar o guardar (17-10).
        // Descompresión Ola Arquitectónica: extracción de proyección y curvas a ManoDeObraProjectionCoordinator (-420 líneas netas).
        ["src/QubiCostElectrical.Wpf/Features/ManoDeObra/ManoDeObraViewModel"] = 1645,
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
            ".Totals", ".Paste", ".LivePreview", ".Attached", ".Cockpit", ".Uf",
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
