using System.IO;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// CostoView debe ser una sola pantalla unificada: sin pestañas, con cascada de costos,
/// factores, gastos operacionales, panel de política y controles de estado.
/// </summary>
public sealed class CostoViewUnifiedLayoutTests
{
    private const string CostoViewRelative = "src/QubiCostElectrical.Wpf/Features/Costo/CostoView.xaml";

    [Fact]
    public void CostoView_NoDebeTenerPestañas()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);
        var path = Path.Combine(repoRoot!, CostoViewRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Falta vista: {CostoViewRelative}");

        // Act
        var xaml = File.ReadAllText(path);

        // Assert
        Assert.False(xaml.Contains("<TabItem", StringComparison.Ordinal),
            "CostoView ya no debe declarar elementos <TabItem>");
        Assert.False(xaml.Contains("<TabControl", StringComparison.Ordinal),
            "CostoView ya no debe declarar elementos <TabControl>");
    }

    [Fact]
    public void CostoView_DebeConsolidarLosTresBloques()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);
        var path = Path.Combine(repoRoot!, CostoViewRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Falta vista: {CostoViewRelative}");

        // Act
        var xaml = File.ReadAllText(path);

        // Assert
        Assert.True(xaml.Contains("ItemsSource=\"{Binding CostRows}\"", StringComparison.Ordinal),
            "Debe conservar la cascada de costos (CostRows)");
        Assert.True(xaml.Contains("ItemsSource=\"{Binding CoefFactors}\"", StringComparison.Ordinal),
            "Debe conservar la sección de factores (CoefFactors)");
        Assert.True(xaml.Contains("ItemsSource=\"{Binding GastosOperacionales}\"", StringComparison.Ordinal),
            "Debe conservar gastos operacionales (GastosOperacionales)");
        Assert.True(xaml.Contains("ClosingPolicySection", StringComparison.Ordinal),
            "Debe conservar el panel derecho de política (ClosingPolicySection)");

        var tituloGastos = LineaCon(xaml, "Text=\"GASTOS OPERACIONALES\"");
        Assert.True(tituloGastos.Contains("CierreSec", StringComparison.Ordinal),
            "El título 'GASTOS OPERACIONALES' debe ir en un TextBlock con estilo de sección (CierreSec)");
    }

    [Fact]
    public void CostoView_DebeConservarContratoDeEstados()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);
        var path = Path.Combine(repoRoot!, CostoViewRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Falta vista: {CostoViewRelative}");

        // Act
        var xaml = File.ReadAllText(path);

        // Assert
        Assert.True(xaml.Contains("EmptyStateControl", StringComparison.Ordinal),
            "Debe conservar el control de estado vacío");
        Assert.True(xaml.Contains("SkeletonOverlayControl", StringComparison.Ordinal),
            "Debe conservar el control de esqueleto de carga");
        Assert.True(xaml.Contains("OnCoefTablePreviewKeyDown", StringComparison.Ordinal),
            "Debe conservar el manejo de teclado de la tabla de factores (OnCoefTablePreviewKeyDown)");
        Assert.True(xaml.Contains("x:Name=\"CoefFactorsList\"", StringComparison.Ordinal),
            "Debe conservar el nombre CoefFactorsList");
    }

    [Fact]
    public void CostoView_SinMojibake()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);
        var path = Path.Combine(repoRoot!, CostoViewRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Falta vista: {CostoViewRelative}");

        // Act
        var xaml = File.ReadAllText(path);

        // Assert: mismo criterio básico que NoMojibakeSourceGuardTest, acotado a esta vista.
        Assert.False(xaml.Contains("\uFFFD", StringComparison.Ordinal),
            "CostoView contiene caracteres de reemplazo (U+FFFD)");
        Assert.False(xaml.Contains("Ã", StringComparison.Ordinal),
            "CostoView contiene artefactos de codificación 'Ã'");
    }

    /// <summary>
    /// Devuelve la línea completa que contiene el fragmento indicado.
    /// </summary>
    private static string LineaCon(string xaml, string fragmento)
    {
        var indice = xaml.IndexOf(fragmento, StringComparison.Ordinal);
        if (indice < 0)
            return string.Empty;

        var inicio = xaml.LastIndexOf('\n', indice) + 1;
        var fin = xaml.IndexOf('\n', indice);
        if (fin < 0)
            fin = xaml.Length;

        return xaml.Substring(inicio, fin - inicio);
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
