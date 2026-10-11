using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class RecentProjectsServiceTests : IDisposable
{
    private readonly string tempFile;
    private readonly IDisposable scope;
    private readonly RecentProjectsService svc;

    public RecentProjectsServiceTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), $"recent_test_{Guid.NewGuid():N}.json");
        scope = UserPreferencesStore.UsePreferencesFilePathForTests(tempFile);
        svc = new RecentProjectsService();
    }

    public void Dispose() => scope.Dispose();

    // ─── Básicos ──────────────────────────────────────────────────────────

    [Fact]
    public void Add_SingleProject_AppearsInGetProjects()
    {
        svc.AddProject(@"C:\proyecto\test.pepro");
        var list = svc.GetProjects();
        Assert.Single(list);
        Assert.Equal(@"C:\proyecto\test.pepro", list[0].Path);
    }

    [Fact]
    public void Add_SamePath_NoDuplicate()
    {
        svc.AddProject(@"C:\proyecto\test.pepro");
        svc.AddProject(@"C:\proyecto\test.pepro");
        Assert.Single(svc.GetProjects());
    }

    [Fact]
    public void Add_SamePathDifferentCase_NoDuplicate()
    {
        svc.AddProject(@"C:\Proyecto\Test.pepro");
        svc.AddProject(@"c:\proyecto\test.pepro");
        Assert.Single(svc.GetProjects());
    }

    [Fact]
    public void Add_MoreThan8_KepsOnlyLast8()
    {
        for (int i = 1; i <= 10; i++)
            svc.AddProject($@"C:\p{i}\proj.pepro");

        Assert.Equal(8, svc.GetProjects().Count);
    }

    [Fact]
    public void Add_ReordersToMRU()
    {
        svc.AddProject(@"C:\a\a.pepro");
        svc.AddProject(@"C:\b\b.pepro");
        svc.AddProject(@"C:\a\a.pepro");

        var list = svc.GetProjects();
        Assert.Equal(@"C:\a\a.pepro", list[0].Path);
        Assert.Equal(@"C:\b\b.pepro", list[1].Path);
    }

    [Fact]
    public void Remove_ExistingEntry_Removed()
    {
        svc.AddProject(@"C:\a\a.pepro");
        svc.AddProject(@"C:\b\b.pepro");
        svc.RemoveProject(@"C:\a\a.pepro");

        var list = svc.GetProjects();
        Assert.Single(list);
        Assert.Equal(@"C:\b\b.pepro", list[0].Path);
    }

    [Fact]
    public void Remove_NonExistentEntry_NoError()
    {
        svc.AddProject(@"C:\a\a.pepro");
        svc.RemoveProject(@"C:\inexistente\x.pepro");
        Assert.Single(svc.GetProjects());
    }

    [Fact]
    public void GetProjects_MissingFile_ReturnsEntryWithMinDate()
    {
        svc.AddProject(@"C:\no\existe.pepro");
        var list = svc.GetProjects();
        Assert.Single(list);
        Assert.Equal(DateTime.MinValue, list[0].LastModified);
    }

    [Fact]
    public void GetProjects_ExistingFile_ReturnsRealDate()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            svc.AddProject(path);
            var list = svc.GetProjects();
            Assert.Single(list);
            Assert.NotEqual(DateTime.MinValue, list[0].LastModified);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RemoveMissing_RemovesOnlyNonExistentFiles()
    {
        var existing = System.IO.Path.GetTempFileName();
        try
        {
            svc.AddProject(existing);
            svc.AddProject(@"C:\ghost\file.pepro");
            svc.RemoveMissing();
            var list = svc.GetProjects();
            Assert.Single(list);
            Assert.Equal(existing, list[0].Path);
        }
        finally
        {
            File.Delete(existing);
        }
    }

    [Fact]
    public void Persistence_SurvivesNewInstance()
    {
        svc.AddProject(@"C:\mi\proyecto.pepro");

        var svc2 = new RecentProjectsService();
        var list = svc2.GetProjects();
        Assert.Single(list);
        Assert.Equal(@"C:\mi\proyecto.pepro", list[0].Path);
    }

    // ─── Persistencia de eliminación ──────────────────────────────────────

    [Fact]
    public void Remove_PersistsAfterNewInstance()
    {
        svc.AddProject(@"C:\borrar\archivo.pepro");
        svc.RemoveProject(@"C:\borrar\archivo.pepro");

        var svc2 = new RecentProjectsService();
        Assert.Empty(svc2.GetProjects());
    }

    [Fact]
    public void Remove_AllEquivalentDuplicates()
    {
        // Forzar duplicados directamente en prefs (simulando corrupción o bug previo)
        var prefs = UserPreferencesStore.Load();
        prefs.RecentFiles.Add(@"C:\dup\a.pepro");
        prefs.RecentFiles.Add(@"C:\dup\a.pepro");
        prefs.RecentFiles.Add(@"C:\DUP\A.pepro");
        UserPreferencesStore.Save(prefs);

        svc.RemoveProject(@"C:\dup\a.pepro");

        var svc2 = new RecentProjectsService();
        Assert.Empty(svc2.GetProjects());
    }

    [Fact]
    public void Remove_CaseInsensitive()
    {
        svc.AddProject(@"C:\Mayus\Proyecto.pepro");
        svc.RemoveProject(@"c:\mayus\proyecto.pepro");

        Assert.Empty(svc.GetProjects());
    }

    [Fact]
    public void Remove_PersistenceVerifiedByNewLoad()
    {
        svc.AddProject(@"C:\persistencia\test.pepro");
        svc.RemoveProject(@"C:\persistencia\test.pepro");

        // Recargar directamente desde prefs
        var prefs = UserPreferencesStore.Load();
        Assert.DoesNotContain(prefs.RecentFiles,
            r => r.Equals(@"C:\persistencia\test.pepro", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Aislamiento: no escribe en LocalAppData real ─────────────────────

    [Fact]
    public void Scope_DoesNotWriteToRealLocalAppData()
    {
        var activePath = UserPreferencesStore.PreferencesFilePath;

        // La ruta de la app real es %LOCALAPPDATA%\QubiCostElectricalPRO\preferences.json
        // El scope de test redirige a un archivo temporal — no debe contener esa carpeta específica.
        Assert.DoesNotContain("QubiCostElectricalPRO", activePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Add_GoesToIsolatedTempFile_NotRealAppData()
    {
        svc.AddProject(@"C:\sentinel\check.pepro");

        Assert.Equal(tempFile, UserPreferencesStore.PreferencesFilePath);
        Assert.StartsWith(Path.GetTempPath(), tempFile, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(tempFile));

        var loaded = UserPreferencesStore.Load();
        Assert.Contains(@"C:\sentinel\check.pepro", loaded.RecentFiles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("QubiCostElectricalPRO", tempFile, StringComparison.OrdinalIgnoreCase);
    }

    // ─── GetProjects: no reinserta rutas eliminadas ───────────────────────

    [Fact]
    public void GetProjects_DoesNotReinsertManuallyRemovedPaths()
    {
        svc.AddProject(@"C:\fantasma\proyecto.pepro");
        svc.RemoveProject(@"C:\fantasma\proyecto.pepro");

        // Llamar GetProjects no debe reincorporar la ruta
        var list1 = svc.GetProjects();
        var list2 = svc.GetProjects();
        Assert.Empty(list1);
        Assert.Empty(list2);
    }

    [Fact]
    public void GetProjects_DeduplicatesExistingEntries()
    {
        // Forzar duplicados en prefs
        var prefs = UserPreferencesStore.Load();
        prefs.RecentFiles.Add(@"C:\dup2\b.pepro");
        prefs.RecentFiles.Add(@"C:\dup2\b.pepro");
        UserPreferencesStore.Save(prefs);

        var list = svc.GetProjects();
        Assert.Single(list);
    }

    [Fact]
    public void TogglePin_PinsAndUnpinsProject()
    {
        svc.AddProject(@"C:\test\pinned.pepro");
        Assert.False(svc.IsPinned(@"C:\test\pinned.pepro"));

        bool pinned = svc.TogglePin(@"C:\test\pinned.pepro");
        Assert.True(pinned);
        Assert.True(svc.IsPinned(@"C:\test\pinned.pepro"));

        bool unpinned = svc.TogglePin(@"C:\test\pinned.pepro");
        Assert.False(unpinned);
        Assert.False(svc.IsPinned(@"C:\test\pinned.pepro"));
    }

    [Fact]
    public void GetProjects_PinnedProjectsAppearFirst()
    {
        svc.AddProject(@"C:\test\first.pepro");
        svc.AddProject(@"C:\test\second.pepro");
        svc.AddProject(@"C:\test\third.pepro");

        // Normal MRU order: third, second, first
        // Pin 'first'
        svc.TogglePin(@"C:\test\first.pepro");

        var list = svc.GetProjects();
        Assert.Equal(3, list.Count);
        Assert.Equal(@"C:\test\first.pepro", list[0].Path);
        Assert.True(list[0].IsPinned);
        Assert.False(list[1].IsPinned);
        Assert.False(list[2].IsPinned);
    }

    [Fact]
    public void RemoveProject_CleansPinnedState()
    {
        svc.AddProject(@"C:\test\pinned.pepro");
        svc.TogglePin(@"C:\test\pinned.pepro");
        Assert.True(svc.IsPinned(@"C:\test\pinned.pepro"));

        svc.RemoveProject(@"C:\test\pinned.pepro");
        Assert.False(svc.IsPinned(@"C:\test\pinned.pepro"));
    }
}
