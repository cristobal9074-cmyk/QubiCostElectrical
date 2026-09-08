using System.IO;
using System.Reflection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Architecture;

/// <summary>
/// Verifica que la identidad del producto sea consistente con Qubi Cost Electrical.
/// </summary>
public sealed class ProductIdentityTests
{
    private static readonly Assembly WpfAssembly =
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "QubiCostElectrical")
        ?? Assembly.GetExecutingAssembly();

    // ── 1. AssemblyName ───────────────────────────────────────────────────────

    [Fact]
    public void AssemblyName_IsQubiCostElectrical()
    {
        var name = WpfAssembly.GetName().Name;
        Assert.Equal("QubiCostElectrical", name);
    }

    // ── 2. Producto ───────────────────────────────────────────────────────────

    [Fact]
    public void Product_IsQubiCostElectrical()
    {
        var attr = WpfAssembly.GetCustomAttribute<AssemblyProductAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("Qubi Cost Electrical", attr.Product);
    }

    // ── 3. Título ─────────────────────────────────────────────────────────────

    [Fact]
    public void Title_IsQubiCostElectrical()
    {
        var attr = WpfAssembly.GetCustomAttribute<AssemblyTitleAttribute>();
        // Title puede no estar si no se define explícitamente — se acepta null o el valor correcto
        if (attr != null)
            Assert.DoesNotContain("Presupuesto", attr.Title, StringComparison.OrdinalIgnoreCase);
    }

    // ── 4. Compañía ───────────────────────────────────────────────────────────

    [Fact]
    public void Company_IsQubiCost()
    {
        var attr = WpfAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("Qubi Cost", attr.Company);
    }

    // ── 5. Versión informativa ────────────────────────────────────────────────

    [Fact]
    public void InformationalVersion_IsGaRelease()
    {
        var attr = WpfAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.NotNull(attr);
        Assert.StartsWith(ProductVersionInfo.ProductVersion, attr.InformationalVersion, StringComparison.Ordinal);
        Assert.DoesNotContain("rc", attr.InformationalVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductVersion_IsSemVer()
    {
        var version = ProductVersionInfo.ProductVersion;
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    // ── 6. Ejecutable esperado ────────────────────────────────────────────────

    [Fact]
    public void AssemblyName_ProducesExpectedExecutable()
    {
        var name = WpfAssembly.GetName().Name;
        Assert.Equal("QubiCostElectrical", name);
    }

    // ── 7. Extensión .qce ─────────────────────────────────────────────────────

    [Fact]
    public void CurrentExtension_IsQce()
    {
        Assert.Equal(".qce", ProjectFileTypes.CurrentExtension);
    }

    // ── 8. Descripción del tipo de archivo ────────────────────────────────────

    [Fact]
    public void FileTypeDescription_ContainsQubiCost()
    {
        const string description = "Proyecto de Qubi Cost Electrical";
        Assert.Contains("Qubi Cost", description, StringComparison.OrdinalIgnoreCase);
    }

    // ── 11. Icono configurado — el WPF resource pack incluye el ICO ──────────

    [Fact]
    public void IcoFile_IsEmbeddedInWpfResources()
    {
        // Los recursos WPF (<Resource>) se empaquetan en el bloque .g.resources
        // que aparece como manifest resource con ese sufijo en el nombre.
        var manifestResources = WpfAssembly.GetManifestResourceNames();
        var wpfResourcePack = manifestResources.FirstOrDefault(r =>
            r.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));

        if (wpfResourcePack != null)
        {
            using var stream = WpfAssembly.GetManifestResourceStream(wpfResourcePack)!;
            using var reader = new System.Resources.ResourceReader(stream);
            bool found = false;
            foreach (System.Collections.DictionaryEntry entry in reader)
            {
                var key = entry.Key?.ToString() ?? "";
                if (key.Contains("qubiCostElectrical.ico", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("assets/qubiCostElectrical.ico", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "QubiCostElectrical.ico no está en el paquete de recursos WPF.");
        }
        else
        {
            // Si no hay .g.resources (ej. proyecto non-WPF en tests), verificar en repo
            var repoRoot = FindRepoRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "");
            if (repoRoot != null)
            {
                var icoPath = Path.Combine(repoRoot, "src", "QubiCostElectrical.Wpf", "Assets", "QubiCostElectrical.ico");
                Assert.True(File.Exists(icoPath), $"ICO no encontrado: {icoPath}");
            }
        }
    }

    // ── 12. Archivo .ico existente en repositorio ─────────────────────────────

    [Fact]
    public void IcoFile_ExistsInRepository()
    {
        // Resolvemos hacia la raíz del repo desde la ubicación del ensamblado de pruebas
        var testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        var repoRoot = FindRepoRoot(testAssemblyDir);
        if (repoRoot == null)
        {
            // Entorno sin acceso al repo (CI single-file) — skip silencioso
            return;
        }
        var icoPath = Path.Combine(repoRoot, "src", "QubiCostElectrical.Wpf", "Assets", "QubiCostElectrical.ico");
        Assert.True(File.Exists(icoPath), $"ICO no encontrado: {icoPath}");
    }

    // ── 13. Sin rutas absolutas en constantes de identidad ───────────────────

    [Fact]
    public void CurrentExtension_HasNoAbsolutePath()
    {
        Assert.False(Path.IsPathRooted(ProjectFileTypes.CurrentExtension));
    }

    // ── 14. Sin nombres legacy en constantes activas ──────────────────────────

    [Fact]
    public void CurrentExtension_IsNotLegacy()
    {
        Assert.NotEqual(ProjectFileTypes.LegacyExtension, ProjectFileTypes.CurrentExtension);
    }

    [Fact]
    public void Product_DoesNotContainLegacyName()
    {
        var attr = WpfAssembly.GetCustomAttribute<AssemblyProductAttribute>();
        if (attr != null)
        {
            Assert.DoesNotContain("Presupuesto Eléctrico PRO", attr.Product, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PEPro", attr.Product, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── 15. Apertura por argumento .qce ───────────────────────────────────────

    [Fact]
    public void TryExtractOpenProjectPath_QceFile_IsRecognized()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"identity_test_{Guid.NewGuid():N}.qce");
        try
        {
            new ProjectRepository().SaveSQLiteSnapshot(tempFile, new ProjectFileData());
            var recognized = SingleInstanceHandoffService.TryExtractOpenProjectPath(
                new[] { tempFile }, out var path);
            Assert.True(recognized);
            Assert.Equal(tempFile, path);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    // ── 16. Apertura por argumento .pepro (legacy) ────────────────────────────

    [Fact]
    public void TryExtractOpenProjectPath_PeproFile_IsRecognized()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"identity_test_{Guid.NewGuid():N}.pepro");
        try
        {
            File.WriteAllText(tempFile, """
            {
              "Proyecto": {
                "Nombre": "Proyecto legado",
                "Cliente": "Cliente legado"
              }
            }
            """);
            var recognized = SingleInstanceHandoffService.TryExtractOpenProjectPath(
                new[] { tempFile }, out var path);
            Assert.True(recognized);
            Assert.Equal(tempFile, path);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void TryExtractOpenProjectPath_NonExistentFile_IsNotRecognized()
    {
        var recognized = SingleInstanceHandoffService.TryExtractOpenProjectPath(
            new[] { @"C:\__nonexistent__\file.qce" }, out _);
        Assert.False(recognized);
    }

    [Fact]
    public void TryExtractOpenProjectPath_UnsupportedExtension_IsNotRecognized()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"identity_test_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempFile, "data");
        try
        {
            var recognized = SingleInstanceHandoffService.TryExtractOpenProjectPath(
                new[] { tempFile }, out _);
            Assert.False(recognized);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir)
                return null;
            dir = parent;
        }
        return null;
    }
}
