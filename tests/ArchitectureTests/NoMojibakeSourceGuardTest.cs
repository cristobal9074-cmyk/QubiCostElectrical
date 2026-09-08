using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

public sealed class NoMojibakeSourceGuardTest
{
    private static readonly Regex MojibakePattern = new(
        @"(Ã¡|Ã©|Ã­|Ã³|Ãº|Ã±|Ã|Ã‰|Ã|Ã“|Ãš|Ã‘|Â·|Â¿|Â¡|â‚¬|â€“|â€”|\uFFFD)",
        RegexOptions.Compiled);

    [Fact]
    public void AllSourceFiles_MustBeCleanUtf8_WithoutMojibakeArtifacts()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var srcDir = Path.Combine(repoRoot!, "src");
        Assert.True(Directory.Exists(srcDir));

        var files = Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var violations = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = MojibakePattern.Match(lines[i]);
                if (match.Success)
                {
                    string relative = Path.GetRelativePath(repoRoot!, file);
                    violations.Add($"{relative}:{i + 1} -> Contiene artefacto mojibake '{match.Value}' en: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Se encontraron {violations.Count} ocurrencias de mojibake en código fuente:\n" + string.Join("\n", violations));
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
