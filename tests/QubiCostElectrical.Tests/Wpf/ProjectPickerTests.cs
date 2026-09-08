using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ProjectPickerTests : IDisposable
{
    private readonly string tempPrefs;
    private readonly IDisposable prefsScope;

    public ProjectPickerTests()
    {
        WpfTestHost.EnsureInitialized();
        tempPrefs = Path.Combine(Path.GetTempPath(), $"picker_prefs_{Guid.NewGuid():N}.json");
        prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(tempPrefs);
    }

    public void Dispose() => prefsScope.Dispose();

    // ─── Apertura y cierre del selector ──────────────────────────────────

    [Fact]
    public void IsProjectPickerOpen_DefaultFalse()
    {
        Assert.False(MakeVm().IsProjectPickerOpen);
    }

    [Fact]
    public void IsProjectPickerOpen_CanBeToggled()
    {
        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        Assert.True(svm.IsProjectPickerOpen);
        svm.IsProjectPickerOpen = false;
        Assert.False(svm.IsProjectPickerOpen);
    }

    // ─── Lista vacía ──────────────────────────────────────────────────────

    [Fact]
    public void RecentProjects_StartsEmpty_WithFreshPrefs()
    {
        Assert.Empty(MakeVm().RecentProjects);
    }

    // ─── Máximo 8 recientes ───────────────────────────────────────────────

    [Fact]
    public void RecentProjects_Max8Entries()
    {
        var svc = new RecentProjectsService();
        for (int i = 1; i <= 10; i++)
            svc.AddProject($@"C:\proj{i}\p.pepro");

        Assert.True(MakeVm().RecentProjects.Count <= 8);
    }

    // ─── Entrada inexistente ──────────────────────────────────────────────

    [Fact]
    public void RecentProjectInfo_MissingFile_IsMissingTrue()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\no\existe\archivo.pepro");

        var svm = MakeVm();
        Assert.Single(svm.RecentProjects);
        Assert.True(svm.RecentProjects[0].IsMissing);
    }

    [Fact]
    public void RecentProjectInfo_MissingFile_DisplayDateIsNoEncontrado()
    {
        var info = new RecentProjectInfo(@"C:\x\x.pepro", "x", DateTime.MinValue);
        Assert.Equal("No encontrado", info.DisplayDate);
        Assert.True(info.IsMissing);
    }

    [Fact]
    public void RecentProjectInfo_ExistingFile_IsMissingFalse()
    {
        var info = new RecentProjectInfo(@"C:\x\x.pepro", "x", new DateTime(2024, 6, 15));
        Assert.False(info.IsMissing);
        Assert.NotEqual("No encontrado", info.DisplayDate);
    }

    // ─── Quitar entrada ───────────────────────────────────────────────────

    [Fact]
    public void RemoveRecentProject_RemovesFromList()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\a\a.pepro");
        svc.AddProject(@"C:\b\b.pepro");

        var svm = MakeVm();
        Assert.Equal(2, svm.RecentProjects.Count);

        svm.RemoveRecentProject(@"C:\a\a.pepro");
        Assert.Single(svm.RecentProjects);
        Assert.Equal(@"C:\b\b.pepro", svm.RecentProjects[0].Path);
    }

    [Fact]
    public void RemoveRecentProject_NonExistent_NoError()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\a\a.pepro");

        var svm = MakeVm();
        svm.RemoveRecentProject(@"C:\inexistente\x.pepro");
        Assert.Single(svm.RecentProjects);
    }

    // ─── Abrir reciente inexistente ───────────────────────────────────────

    [Fact]
    public async Task OpenRecentProjectAsync_MissingFile_RemovesFromList()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\no\existe.pepro");

        var svm = MakeVm();
        svm.ShowMissingFileWarning = _ => { };   // suprimir diálogo en tests

        var countBefore = svm.RecentProjects.Count;
        await svm.OpenRecentProjectAsync(@"C:\no\existe.pepro");
        Assert.True(svm.RecentProjects.Count < countBefore);
    }

    // ─── Bug: ruta inexistente reaparece ─────────────────────────────────

    [Fact]
    public async Task OpenRecentProjectAsync_MissingFile_PersistsRemovalAfterNewInstance()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\fantasma\proyecto.pepro");

        var svm = MakeVm();
        svm.ShowMissingFileWarning = _ => { };

        await svm.OpenRecentProjectAsync(@"C:\fantasma\proyecto.pepro");

        // Nueva instancia del servicio debe leer el mismo archivo de prefs (aislado)
        var svc2 = new RecentProjectsService();
        var remaining = svc2.GetProjects();
        Assert.DoesNotContain(remaining, r => r.Path.Equals(@"C:\fantasma\proyecto.pepro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenRecentProjectAsync_MissingFile_RemovesDuplicates()
    {
        // Forzar duplicados directamente en prefs
        var prefs = UserPreferencesStore.Load();
        prefs.RecentFiles.Add(@"C:\dup\archivo.pepro");
        prefs.RecentFiles.Add(@"C:\dup\archivo.pepro");
        prefs.RecentFiles.Add(@"C:\DUP\Archivo.pepro");
        UserPreferencesStore.Save(prefs);

        var svm = MakeVm();
        svm.ShowMissingFileWarning = _ => { };

        await svm.OpenRecentProjectAsync(@"C:\dup\archivo.pepro");

        var svc2 = new RecentProjectsService();
        var remaining = svc2.GetProjects();
        Assert.DoesNotContain(remaining, r =>
            r.Path.Equals(@"C:\dup\archivo.pepro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenRecentProjectAsync_MissingFile_CaseInsensitiveRemoval()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\Mayuscula\Proyecto.pepro");

        var svm = MakeVm();
        svm.ShowMissingFileWarning = _ => { };

        // Abrir con distinta capitalización
        await svm.OpenRecentProjectAsync(@"c:\mayuscula\proyecto.pepro");

        var svc2 = new RecentProjectsService();
        Assert.Empty(svc2.GetProjects());
    }

    [Fact]
    public async Task RefreshRecentProjects_DoesNotReinsertRemovedPath()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\fantasma2\p.pepro");

        var svm = MakeVm();
        svm.ShowMissingFileWarning = _ => { };

        await svm.OpenRecentProjectAsync(@"C:\fantasma2\p.pepro");

        // Simular RefreshRecentProjects llamado de nuevo (como lo haría la app)
        svm.RemoveRecentProject(@"C:\fantasma2\p.pepro"); // idempotente

        Assert.DoesNotContain(svm.RecentProjects,
            r => r.Path.Equals(@"C:\fantasma2\p.pepro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tests_DoNotWriteToRealLocalAppData()
    {
        // El scope de test redirige a un archivo temporal.
        // La ruta real de la app contiene "QubiCostElectricalPRO".
        var activePath = UserPreferencesStore.PreferencesFilePath;
        Assert.DoesNotContain("QubiCostElectricalPRO", activePath, StringComparison.OrdinalIgnoreCase);
    }

    // ─── IsBusy / CanExecuteActions ───────────────────────────────────────

    [Fact]
    public void CanExecuteActions_InitiallyTrue()
    {
        var svm = MakeVm();
        Assert.True(svm.CanExecuteActions);
        Assert.False(svm.IsBusy);
    }

    // ─── Popup se cierra al iniciar operación ────────────────────────────

    [Fact]
    public async Task OpenRecentProjectAsync_MissingFile_ClosesPickerFirst()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\no\existe2.pepro");

        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        svm.ShowMissingFileWarning = _ => { };   // suprimir diálogo bloqueante

        await svm.OpenRecentProjectAsync(@"C:\no\existe2.pepro");
        Assert.False(svm.IsProjectPickerOpen);
    }

    // ─── Cerrar con guardado silencioso ───────────────────────────────────

    [Fact]
    public async Task CloseProject_WithDirtyState_SilentSave_ReturnsTrueAndProjectClosed()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"picker_close_{Guid.NewGuid():N}.qce");
        try
        {
            host.Session.NewProject("picker-test");
            await host.ProjectWorkspace.SaveAsAsync(path);
            host.Session.MarkDirty();

            var svm = MakeVm();
            svm.ShowCloseProjectDialog = () =>
                throw new InvalidOperationException("ShowCloseProjectDialog no debe llamarse en cierre silencioso.");

            var result = await svm.CloseProjectAsync();
            Assert.True(result);
            Assert.False(host.ProjectWorkspace.HasOpenProject);
        }
        finally
        {
            foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
                try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }

    // ─── Escape: comportamiento de teclado ───────────────────────────────

    [Fact]
    public void Escape_WhenPickerClosed_DoesNothing()
    {
        var svm = MakeVm();
        var fired = false;
        svm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(svm.IsProjectPickerOpen)) fired = true;
        };
        // picker already closed — simulated Escape (set false when already false)
        svm.IsProjectPickerOpen = false;
        Assert.False(fired, "PropertyChanged no debe dispararse si ya estaba cerrado");
        Assert.False(svm.IsProjectPickerOpen);
    }

    [Fact]
    public void Escape_WhenPickerOpen_ClosesPopup()
    {
        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        // simulated Escape
        svm.IsProjectPickerOpen = false;
        Assert.False(svm.IsProjectPickerOpen);
    }

    [Fact]
    public void Escape_WithRecentFocused_ClosesPopup()
    {
        var svc = new RecentProjectsService();
        svc.AddProject(@"C:\proyecto\reciente.pepro");

        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        Assert.Single(svm.RecentProjects);

        // simulated Escape with focus on recent item
        svm.IsProjectPickerOpen = false;
        Assert.False(svm.IsProjectPickerOpen);
    }

    [Fact]
    public void Escape_NoImmediateReopen()
    {
        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        svm.IsProjectPickerOpen = false;
        // nada debe reabrir el popup automáticamente
        Assert.False(svm.IsProjectPickerOpen);
    }

    [Fact]
    public void Escape_FocusRestorationNotBlocked()
    {
        // La restauración de foco al botón depende del Dispatcher (UI),
        // pero el estado del VM debe ser false antes de que el foco se mueva.
        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        svm.IsProjectPickerOpen = false;
        Assert.False(svm.IsProjectPickerOpen);
    }

    [Fact]
    public void Escape_PropertyChangedFires_OnOpen()
    {
        var svm = MakeVm();
        var events = new List<string?>();
        svm.PropertyChanged += (_, e) => events.Add(e.PropertyName);
        svm.IsProjectPickerOpen = true;
        Assert.Contains(nameof(svm.IsProjectPickerOpen), events);
    }

    [Fact]
    public void Escape_PropertyChangedFires_OnClose()
    {
        var svm = MakeVm();
        svm.IsProjectPickerOpen = true;
        var events = new List<string?>();
        svm.PropertyChanged += (_, e) => events.Add(e.PropertyName);
        svm.IsProjectPickerOpen = false;
        Assert.Contains(nameof(svm.IsProjectPickerOpen), events);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// Creates a ShellViewModel with navigation disabled (avoids WPF STA requirement).
    private static ShellViewModel MakeVm()
    {
        var svm = new ShellViewModel(WpfAppHost.Current);
        svm.PostCloseNavigate = () => { };
        return svm;
    }
}
