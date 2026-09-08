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
        // Baselines de ShellViewModel/Dashboard/Export/Itemizado subidos por la migración del
        // service-locator a inyección por constructor (el patrón compat-ctor añade el ctor
        // primario concreto + asignaciones de campos). Sin lógica nueva.
        ["src/QubiCostElectrical.Wpf/Shell/ShellViewModel"] = 1737,
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionView.xaml"] = 2100,
        // +~20 por el selector de política de cierre + salvaguardas (sello de parámetros,
        // auditoría de cambio de tipo, avisos de política) — feature multi-política de cierre.
        ["src/QubiCostElectrical.Wpf/Features/Costo/CostoViewModel"] = 1410,
        ["src/QubiCostElectrical.Wpf/Features/ManoDeObra/ManoDeObraViewModel"] = 1205,
        ["src/QubiCostElectrical.Wpf/Features/Dashboard/DashboardViewModel"] = 1265,
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionViewModel"] = 1750,
        ["src/QubiCostElectrical.Wpf/Features/Export/ExportViewModel"] = 1378,
        ["src/QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoViewModel"] = 1284,
        ["src/QubiCostElectrical.Wpf/Infrastructure/DataGridExcelNavigation"] = 1222,
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
