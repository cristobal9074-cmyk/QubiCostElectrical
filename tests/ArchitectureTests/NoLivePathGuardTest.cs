using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Guard de hermeticidad de la suite de tests.
///
/// <para><b>Qué previene.</b> La suite de tests no puede depender de rutas absolutas de la
/// máquina del desarrollador. Un test anterior
/// (<c>FactorAjusteMoServiceTests.SyncAndVerifyPel039A2026Prueba2</c>) escribía sobre
/// <c>C:\Users\cristobal1\Documents\Presupuestos Electricos\*.qce</c> en cada
/// <c>dotnet test</c>, pisando presupuestos reales de cliente. En CI el test pasaba en
/// verde por accidente (la ruta no existe en el runner, así que hacía early return), lo
/// que ocultaba el problema justo donde debía detectarse.</para>
///
/// <para><b>Contrato.</b> Para leer un proyecto de obra real, un test debe pasar por
/// <c>QubiCostElectrical.Tests.Fixtures.LiveProjectGate</c>, que está cerrado por defecto
/// y se habilita con <c>QUBI_LIVE_PROJECTS=1</c>. Ese helper resuelve las rutas con
/// <c>Environment.SpecialFolder</c>, sin literales de usuario.</para>
///
/// <para><b>Por qué un guard de grep y no un test de comportamiento.</b> La condición que
/// queremos forbidding no es observable en runtime: es una propiedad del código fuente.
/// El repo ya usa este patrón en <c>NoMojibakeSourceGuardTest</c> y
/// <c>MaxLineCountTest</c>.</para>
/// </summary>
public sealed class NoLivePathGuardTest
{
    /// <summary>
    /// Rutas absolutas de usuario. Específicas de una máquina, nunca portables.
    /// Cubre disco local (<c>C:\Users\...</c>) y recursos UNC (<c>\\servidor\Users\...</c>).
    /// </summary>
    private static readonly Regex UserProfilePathPattern = new(
        @"(?:[A-Za-z]:\\|\\\\[^\\""'\s]+\\)Users\\[^\\""'\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Nombres de usuario sintéticos admitidos en datos de prueba. Son fixtures de
    /// rutas (por ejemplo la detección de carpetas de sincronización en la nube), no
    /// referencias a la máquina de nadie.
    /// </summary>
    private static readonly string[] SyntheticUserNames =
    {
        "ana", "runner", "runneradmin", "administrator", "user", "test", "dev", "developer"
    };

    /// <summary>
    /// Archivos exentos del escaneo: el propio guard contiene los patrones que busca.
    /// </summary>
    private static readonly string[] SelfExemptFiles =
    {
        "NoLivePathGuardTest.cs"
    };

    [Fact]
    public void TestSources_MustNotEmbedMachineSpecificUserPaths()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var testsDir = Path.Combine(repoRoot!, "tests");
        Assert.True(Directory.Exists(testsDir));

        var files = Directory.GetFiles(testsDir, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) &&
                        !SelfExemptFiles.Any(name => f.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var violations = new List<string>();

        foreach (var file in files)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (IOException)
            {
                continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in UserProfilePathPattern.Matches(lines[i]))
                {
                    string userSegment = ExtractUserSegment(match.Value);
                    if (userSegment.Length > 0 && SyntheticUserNames.Contains(userSegment, StringComparer.OrdinalIgnoreCase))
                        continue;

                    string relative = Path.GetRelativePath(repoRoot!, file);
                    violations.Add($"{relative}:{i + 1} -> Ruta absoluta de usuario '{match.Value}'. " +
                                    "Usar QubiCostElectrical.Tests.Fixtures.LiveProjectGate.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"La suite de tests debe ser hermetica: {violations.Count} referencias a rutas de la maquina del desarrollador:\n" +
            string.Join("\n", violations));
    }

    /// <summary>Extrae el nombre de usuario de una ruta que contiene el segmento Users.</summary>
    private static string ExtractUserSegment(string path)
    {
        int index = path.IndexOf("Users", StringComparison.OrdinalIgnoreCase);
        if (index < 0) return "";

        string rest = path[(index + "Users".Length)..].TrimStart('\\');
        int end = rest.IndexOf('\\');
        return end < 0 ? rest : rest[..end];
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
