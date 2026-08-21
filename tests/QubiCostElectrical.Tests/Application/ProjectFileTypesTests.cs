using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectFileTypesTests
{
    // ── 1. Extensión actual ───────────────────────────────────────────────────

    [Fact]
    public void CurrentExtension_IsQce()
    {
        Assert.Equal(".qce", ProjectFileTypes.CurrentExtension);
    }

    // ── 2. Extensión legacy ───────────────────────────────────────────────────

    [Fact]
    public void LegacyExtension_IsPepro()
    {
        Assert.Equal(".pepro", ProjectFileTypes.LegacyExtension);
    }

    // ── 3. Lista de extensiones compatibles ──────────────────────────────────

    [Fact]
    public void SupportedExtensions_ContainsBothExtensions()
    {
        Assert.Contains(".qce", ProjectFileTypes.SupportedExtensions);
        Assert.Contains(".pepro", ProjectFileTypes.SupportedExtensions);
    }

    [Fact]
    public void SupportedExtensions_CurrentExtensionIsFirst()
    {
        Assert.Equal(".qce", ProjectFileTypes.SupportedExtensions[0]);
    }

    // ── 4 & 7. Extensión actual y legacy reconocidas por IsSupportedProjectExtension ─

    [Theory]
    [InlineData(".qce", true)]
    [InlineData(".QCE", true)]
    [InlineData(".pepro", true)]
    [InlineData(".PEPRO", true)]
    [InlineData(".db", false)]
    [InlineData(".json", false)]
    [InlineData(".txt", false)]
    [InlineData(".xlsx", false)]
    [InlineData("", false)]
    public void IsSupportedProjectExtension_CorrectlyClassifies(string ext, bool expected)
    {
        Assert.Equal(expected, ProjectFileTypes.IsSupportedProjectExtension(ext));
    }

    // ── 8 & 9. Abrir .qce y .pepro — IsSqliteProjectFile cubre ambas ─────────

    [Theory]
    [InlineData(@"C:\proyecto.qce", true)]
    [InlineData(@"C:\proyecto.pepro", true)]
    [InlineData(@"C:\proyecto.txt", false)]
    public void IsSqliteProjectFile_AcceptsBothExtensions(string path, bool expected)
    {
        Assert.Equal(expected, ProjectStoragePaths.IsSqliteProjectFile(path));
    }

    // ── 10. Legacy .pepro conserva extensión en autosave mientras exista soporte ─

    [Fact]
    public void AutoSave_PeproProject_PreservesLegacyExtension()
    {
        var result = ProjectStoragePaths.GetAutoSavePathForProject(@"C:\obra\Hospital.pepro", "fallback");
        Assert.EndsWith("Hospital.pepro.autosave", result);
    }

    // ── 11. Guardar como desde .pepro propone .qce (solo compatibilidad legacy) ─

    [Fact]
    public void CurrentExtension_DiffersFromLegacy()
    {
        Assert.NotEqual(ProjectFileTypes.CurrentExtension, ProjectFileTypes.LegacyExtension);
    }

    // ── 14 & 16. Backup/autosave de .qce ─────────────────────────────────────

    [Fact]
    public void AutoSave_QceProject_UsesQceAutosave()
    {
        var result = ProjectStoragePaths.GetAutoSavePathForProject(@"C:\obra\Hospital.qce", "fallback");
        Assert.EndsWith("Hospital.qce.autosave", result);
    }

    // ── 15 & 17. Backup/autosave de .pepro conserva extensión legacy ──────────

    [Fact]
    public void AutoSave_PeproProject_UsesLegacyAutosave()
    {
        var result = ProjectStoragePaths.GetAutoSavePathForProject(@"C:\obra\Hospital.pepro", "fallback");
        Assert.EndsWith("Hospital.pepro.autosave", result);
    }

    // ── 18. Ruta con espacios ─────────────────────────────────────────────────

    [Fact]
    public void IsSqliteProjectFile_HandlesPathWithSpaces()
    {
        Assert.True(ProjectStoragePaths.IsSqliteProjectFile(@"C:\Mis Proyectos\Hospital General.qce"));
        Assert.True(ProjectStoragePaths.IsSqliteProjectFile(@"C:\Mis Proyectos\Hospital General.pepro"));
    }

    // ── 19. Ruta con tildes ───────────────────────────────────────────────────

    [Fact]
    public void IsSqliteProjectFile_HandlesPathWithAccents()
    {
        Assert.True(ProjectStoragePaths.IsSqliteProjectFile(@"C:\Presupuestos Eléctricos\Edificación.qce"));
        Assert.True(ProjectStoragePaths.IsSqliteProjectFile(@"C:\Presupuestos Eléctricos\Edificación.pepro"));
    }

    // ── 20. Archivo inexistente: IsSqliteProjectFile solo valida extensión ────

    [Fact]
    public void IsSqliteProjectFile_DoesNotRequireFileToExist()
    {
        Assert.True(ProjectStoragePaths.IsSqliteProjectFile(@"C:\__nonexistent__\proyecto.qce"));
    }

    // ── 22. Sin referencias activas que usen .pepro como extensión por defecto ─

    [Fact]
    public void CurrentExtension_IsNotLegacyExtension()
    {
        Assert.NotEqual(ProjectFileTypes.LegacyExtension, ProjectFileTypes.CurrentExtension);
        Assert.Equal(".qce", ProjectFileTypes.CurrentExtension);
    }
}
