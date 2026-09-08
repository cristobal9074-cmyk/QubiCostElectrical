using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Prueba de guardia arquitectónica que audita todos los archivos .xaml del proyecto WPF
/// para asegurar que cualquier recurso referenciado con StaticResource o DynamicResource
/// exista en los diccionarios globales (DesignSystem/Theme.xaml) o localmente.
/// Esto evita en tiempo de compilación y CI excepciones en tiempo de ejecución como XamlParseException
/// por recursos faltantes (ej. Radius.Card).
/// </summary>
public sealed class XamlResourceIntegrityGuardTest
{
    private static readonly Regex KeyDefinitionRegex = new(@"x:Key\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ResourceReferenceRegex = new(@"\{(?:StaticResource|DynamicResource)\s+([^\},]+)\}", RegexOptions.Compiled);

    // Recursos del sistema WPF o estilos de sub-controles que se heredan del contenedor padre
    private static readonly HashSet<string> KnownExemptions = new(StringComparer.Ordinal)
    {
        // System / WPF runtime
        "ToolBar.SeparatorStyleKey",
        "SystemColors.ControlTextBrushKey",
        "SystemColors.WindowBrushKey",
        "PrecisionTwoDecimalsConverter",

        // Viaticos sub-sections (heredados de ViaticosProfesionalDialog.xaml)
        "VToggle", "VMealBtn", "VFieldLabel", "VInput", "VCombo", "VTabBtn", "VSideH2", "VChip",
        "Brush.CostoWhite50", "Brush.CostoWhite60", "Brush.CostoButtonBorder", "Brush.CostoButtonHover",
        "Brush.CostoChipValue", "Brush.CostoFlash", "Brush.CostoHeroAmber", "Brush.AmberBorder",

        // Project sub-controls (heredados de ProjectView.xaml)
        "EstadoSegBtn", "ProjectRailItem", "ProjectRailItemActive"
    };

    [Fact]
    public void AllXamlResourceReferences_MustResolveGloballyOrLocally()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var wpfDir = Path.Combine(repoRoot!, "src", "QubiCostElectrical.Wpf");
        Assert.True(Directory.Exists(wpfDir), $"Directorio WPF no encontrado: {wpfDir}");

        var designSystemDir = Path.Combine(wpfDir, "DesignSystem");
        var designSystemFiles = Directory.GetFiles(designSystemDir, "*.xaml", SearchOption.AllDirectories);

        var globalKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in designSystemFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match m in KeyDefinitionRegex.Matches(content))
            {
                globalKeys.Add(m.Groups[1].Value.Trim());
            }
        }

        var appXaml = Path.Combine(wpfDir, "App.xaml");
        if (File.Exists(appXaml))
        {
            var content = File.ReadAllText(appXaml);
            foreach (Match m in KeyDefinitionRegex.Matches(content))
            {
                globalKeys.Add(m.Groups[1].Value.Trim());
            }
        }

        var allXamlFiles = Directory.GetFiles(wpfDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var unresolved = new List<string>();

        foreach (var file in allXamlFiles)
        {
            var content = File.ReadAllText(file);
            var localKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in KeyDefinitionRegex.Matches(content))
            {
                localKeys.Add(m.Groups[1].Value.Trim());
            }

            foreach (Match rm in ResourceReferenceRegex.Matches(content))
            {
                var rawKey = rm.Groups[1].Value.Trim();

                // Ignorar referencias tipo x:Type, x:Static, etc.
                if (rawKey.StartsWith("x:") || rawKey.StartsWith("{") || rawKey.StartsWith("SystemColors.") || rawKey.StartsWith("SystemParameters."))
                    continue;

                if (KnownExemptions.Contains(rawKey))
                    continue;

                if (!globalKeys.Contains(rawKey) && !localKeys.Contains(rawKey))
                {
                    var relative = Path.GetRelativePath(repoRoot!, file);
                    unresolved.Add($"{relative} -> Recurso no encontrado: '{rawKey}'");
                }
            }
        }

        Assert.True(
            unresolved.Count == 0,
            $"Se detectaron {unresolved.Count} referencias a recursos XAML no definidos:\n" + string.Join("\n", unresolved));
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
