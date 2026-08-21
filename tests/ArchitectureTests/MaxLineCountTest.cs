using System.IO;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Architecture test gate: Enforces a maximum line count limit of 400 lines for all .cs source files in src/.
/// Partial continuation files (.Methods.cs) and generated files (.g.cs, .g.i.cs) are excluded.
/// No legacy exemptions.
/// </summary>
public sealed class MaxLineCountTest
{
    private const int MaxAllowedLines = 400;

    /// <summary>
    /// No legacy exemptions — all source files in src/ must be under 400 lines.
    /// </summary>
    private static readonly HashSet<string> LegacyBaselineExemptions = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    [Fact]
    public void AllSourceFiles_MustBeUnder400Lines_OrInLegacyBaseline()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var srcDir = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcDir), $"Directory src/ not found at {srcDir}");

        var files = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith(".Methods.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var violations = new List<string>();
        var staleExemptions = new List<string>();

        foreach (var file in files)
        {
            var lineCount = File.ReadAllLines(file).Length;
            var relativePath = Path.GetRelativePath(repoRoot, file);

            if (lineCount > MaxAllowedLines)
            {
                if (!LegacyBaselineExemptions.Contains(relativePath))
                {
                    violations.Add($"{relativePath}: {lineCount} lines (max allowed: {MaxAllowedLines})");
                }
            }
            else
            {
                if (LegacyBaselineExemptions.Contains(relativePath))
                {
                    staleExemptions.Add($"{relativePath}: now {lineCount} lines (can be removed from LegacyBaselineExemptions)");
                }
            }
        }

        Assert.True(
            violations.Count == 0 && staleExemptions.Count == 0,
            (violations.Count > 0
                ? $"Architecture Gate Failure — NEW file(s) exceed {MaxAllowedLines} lines:\n" + string.Join("\n", violations) + "\n"
                : "") +
            (staleExemptions.Count > 0
                ? $"Architecture Gate Notification — Refactored file(s) are now under {MaxAllowedLines} lines! Remove from LegacyBaselineExemptions:\n" + string.Join("\n", staleExemptions)
                : ""));
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
