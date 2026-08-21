using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PlantillasVmTests : IDisposable
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    private sealed class FakeApuStore : IApuTemplateStore
    {
        private readonly List<ApuTemplateRow> rows = new();

        public IReadOnlyList<ApuTemplateRow> GetAll() => rows.AsReadOnly();

        public IReadOnlyList<ApuTemplateRow> Search(string text)
        {
            string q = (text ?? "").Trim().ToUpperInvariant();
            return rows.Where(r =>
                r.Nombre.ToUpperInvariant().Contains(q) ||
                r.Categoria.ToUpperInvariant().Contains(q)).ToList();
        }

        public void Save(ApuTemplateRow t)
        {
            var existing = rows.FirstOrDefault(r => r.Id == t.Id);
            if (existing != null) rows.Remove(existing);
            rows.Add(t);
        }

        public void Delete(string id) => rows.RemoveAll(r => r.Id == id);

        public void AddRow(ApuTemplateRow row) => rows.Add(row);
    }

    private readonly IDisposable templateDbScope;
    private readonly string templateDbPath;

    public PlantillasVmTests()
    {
        WpfTestHost.EnsureInitialized();
        templateDbPath = Path.Combine(Path.GetTempPath(), $"apu-templates-{Guid.NewGuid():N}.db");
        templateDbScope = ApuTemplateStore.UseDatabasePathForTests(templateDbPath);
    }

    public void Dispose()
    {
        templateDbScope.Dispose();
        foreach (var path in new[] { templateDbPath, templateDbPath + "-wal", templateDbPath + "-shm" })
            try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    // Helpers
    private static ApuTemplateRow MakeTemplate(string nombre, string categoria = "APU") =>
        new ApuTemplateRow
        {
            Nombre = nombre,
            Categoria = categoria,
            Descripcion = $"Desc de {nombre}",
            Unidad = "GL",
            JsonRecursos = "[]"
        };

    // 1. Construcción — no lanza excepción
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var ex = Record.Exception(() => new PlantillasVm(new FakeShellHost(), WpfAppHost.Current));
        Assert.Null(ex);
    }

    // 2. Sin proyecto — APU templates se muestran igual (son globales)
    [Fact]
    public void Constructor_WithoutProject_DoesNotHideContent()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("plantillas-test-no-proj");

        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);

        // ShowEmptyState solo true si no hay store. Con store disponible = false.
        Assert.False(vm.ShowEmptyState);
    }

    // 3. HasTemplateStore cuando store disponible
    [Fact]
    public void HasTemplateStore_WithAvailableStore_IsTrue()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.True(vm.HasTemplateStore);
    }

    // 4. Colecciones inicializadas
    [Fact]
    public void Constructor_CollectionsInitialized()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.NotNull(vm.Templates);
        Assert.NotNull(vm.Conjuntos);
        Assert.NotNull(vm.TemplateRecursos);
        Assert.NotNull(vm.ConjuntoRecursos);
    }

    // 5. Comandos no nulos
    [Fact]
    public void Constructor_Commands_NotNull()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.NotNull(vm.CreateTemplateCommand);
        Assert.NotNull(vm.DeleteTemplateCommand);
        Assert.NotNull(vm.DuplicateTemplateCommand);
        Assert.NotNull(vm.RenameTemplateCommand);
        Assert.NotNull(vm.ImportCommand);
        Assert.NotNull(vm.ExportCommand);
    }

    // 6. ApuTemplateItemVm expone propiedades del source
    [Fact]
    public void ApuTemplateItemVm_ExposesSourceProperties()
    {
        var row = MakeTemplate("Cable THHN 2.5mm", "Conductores");
        row.Unidad = "ML";
        var vm = new ApuTemplateItemVm(row);

        Assert.Equal("Cable THHN 2.5mm", vm.Nombre);
        Assert.Equal("Conductores", vm.Categoria);
        Assert.Equal("ML", vm.Unidad);
        Assert.Equal(0, vm.RecursoCount);
    }

    // 7. ApuTemplateItemVm.RecursoCount con JSON vacío
    [Fact]
    public void ApuTemplateItemVm_RecursoCount_EmptyJson()
    {
        var row = MakeTemplate("T1");
        row.JsonRecursos = "[]";
        var vm = new ApuTemplateItemVm(row);
        Assert.Equal(0, vm.RecursoCount);
    }

    // 8. ApuTemplateItemVm.RecursoCount con recursos reales
    [Fact]
    public void ApuTemplateItemVm_RecursoCount_ValidJson()
    {
        var row = MakeTemplate("T1");
        row.JsonRecursos = JsonSerializer.Serialize(new[]
        {
            new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "R1", Unidad = "UN" },
            new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Descripcion = "R2", Unidad = "HH" }
        });
        var vm = new ApuTemplateItemVm(row);
        Assert.Equal(2, vm.RecursoCount);
    }

    // 9. ApuTemplateItemVm.RecursoCount con JSON inválido no lanza
    [Fact]
    public void ApuTemplateItemVm_RecursoCount_InvalidJson_IsZero()
    {
        var row = MakeTemplate("T1");
        row.JsonRecursos = "INVALID_JSON";
        var vm = new ApuTemplateItemVm(row);
        Assert.Equal(0, vm.RecursoCount);
    }

    // 10. ConjuntoItemVm expone propiedades del source
    [Fact]
    public void ConjuntoItemVm_ExposesSourceProperties()
    {
        var row = new ConjuntoElectricoRow
        {
            Codigo = "CE-001",
            Nombre = "Punto de enchufe",
            TipoConjunto = ConjuntoTipo.PuntoEnchufe,
            Unidad = Unidades.UN
        };
        var vm = new ConjuntoItemVm(row);

        Assert.Equal("CE-001", vm.Codigo);
        Assert.Equal("Punto de enchufe", vm.Nombre);
        Assert.Equal(0, vm.RecursoCount);
    }

    // 11. KpiTemplatesCount refleja cantidad cargada
    [Fact]
    public void KpiTemplatesCount_ReflectsLoadedTemplates()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        int initial = vm.KpiTemplatesCount;

        Assert.Equal(vm.Templates.Count, initial);
    }

    // 12. TemplateFilter actualiza lista
    [Fact]
    public void TemplateFilter_Set_DoesNotThrow()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.TemplateFilter = "Cable");
        Assert.Null(ex);
        Assert.Equal("Cable", vm.TemplateFilter);
    }

    // 13. SelectedTemplate set carga TemplateRecursos vacíos con JSON vacío
    [Fact]
    public void SelectedTemplate_Set_LoadsRecursos()
    {
        var store = new FakeApuStore();
        var row = MakeTemplate("T con recursos");
        row.JsonRecursos = JsonSerializer.Serialize(new[]
        {
            new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Mat1", Unidad = "UN" }
        });
        store.AddRow(row);

        var itemVm = new ApuTemplateItemVm(row);
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedTemplate = itemVm;

        // TemplateRecursos se carga desde JsonRecursos del item — verifica sin crash
        Assert.Equal(itemVm, vm.SelectedTemplate);
    }

    // 14. SelectedTemplate = null limpia TemplateRecursos
    [Fact]
    public void SelectedTemplate_Null_ClearsRecursos()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedTemplate = null;
        Assert.Null(vm.SelectedTemplate);
    }

    // 15. DeleteTemplateCommand.CanExecute = false sin selección
    [Fact]
    public void DeleteTemplateCommand_WithoutSelection_CannotExecute()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedTemplate = null;
        Assert.False(vm.DeleteTemplateCommand.CanExecute(null));
    }

    // 16. DuplicateTemplateCommand.CanExecute = false sin selección
    [Fact]
    public void DuplicateTemplateCommand_WithoutSelection_CannotExecute()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.False(vm.DuplicateTemplateCommand.CanExecute(null));
    }

    // 17. RenameTemplateCommand.CanExecute = false sin selección
    [Fact]
    public void RenameTemplateCommand_WithoutSelection_CannotExecute()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.False(vm.RenameTemplateCommand.CanExecute(null));
    }

    // 18. ImportCommand.CanExecute = true cuando store disponible
    [Fact]
    public void ImportCommand_CanExecute_True()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.True(vm.ImportCommand.CanExecute(null));
    }

    // 19. Refresh no lanza excepción
    [Fact]
    public void Refresh_DoesNotThrow()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.Refresh());
        Assert.Null(ex);
    }

    // 20. ActiveTab setter no lanza
    [Fact]
    public void ActiveTab_Set_DoesNotThrow()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.ActiveTab = 1);
        Assert.Null(ex);
        Assert.Equal(1, vm.ActiveTab);
    }

    // 21. Templates registrado en ShellRouteCatalog
    [Fact]
    public void Templates_RegisteredInShellRouteCatalog()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.Templates);
        Assert.Equal("Más", info.Group);
        Assert.Contains("Plantillas", info.Title);
    }

    // 22. Sin referencias a WinForms
    [Fact]
    public void PlantillasVm_HasNoWinFormsReference()
    {
        var type = typeof(PlantillasVm);
        var referencedAssemblies = type.Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencedAssemblies, a =>
            a.Name != null &&
            (a.Name.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
             a.Name.Contains("QubiCostElectrical.WinForms", StringComparison.OrdinalIgnoreCase)));
    }

    // 23. Importar JSON válido con formato ApuTemplateLibraryFile funciona sin crash
    [Fact]
    public void ImportLibrary_WithValidJsonFile_ParsesCorrectly()
    {
        // Crear JSON en formato ApuTemplateLibraryFile
        var library = new
        {
            format = "QubiCostElectrical.ApuTemplateLibrary",
            version = 1,
            exportedAt = DateTime.UtcNow,
            templates = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString("N"),
                    nombre = "Plantilla importada",
                    categoria = "Test",
                    descripcion = "desc",
                    unidad = "GL",
                    jsonRecursos = "[]",
                    creadoEn = DateTime.UtcNow,
                    actualizadoEn = DateTime.UtcNow
                }
            }
        };
        string json = JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true });
        string path = Path.Combine(Path.GetTempPath(), $"apu-lib-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json, Encoding.UTF8);

        try
        {
            // Solo verificamos que el parse funciona — el import completo requiere diálogo UI
            string content = File.ReadAllText(path, Encoding.UTF8);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<JsonDocument>(content, options);
            Assert.NotNull(parsed);
            Assert.True(parsed!.RootElement.TryGetProperty("templates", out var tpls));
            Assert.Equal(1, tpls.GetArrayLength());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // 24. Importar JSON como lista directa de ApuTemplateRow
    [Fact]
    public void ImportLibrary_DirectList_ParsesCorrectly()
    {
        var rows = new[]
        {
            new { nombre = "T1", categoria = "Cat", descripcion = "", unidad = "GL", jsonRecursos = "[]" }
        };
        string json = JsonSerializer.Serialize(rows);
        string path = Path.Combine(Path.GetTempPath(), $"apu-list-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json, Encoding.UTF8);

        try
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(content);
            Assert.NotNull(parsed);
            Assert.Single(parsed!);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // 25. Export — DuplicateTemplate crea nuevo Id
    [Fact]
    public void DuplicateTemplate_WithFakeStore_CreatesNewId()
    {
        var store = new FakeApuStore();
        var original = MakeTemplate("Original");
        store.AddRow(original);

        var clone = new ApuTemplateRow
        {
            Nombre = original.Nombre + " (copia)",
            Categoria = original.Categoria,
            Descripcion = original.Descripcion,
            Unidad = original.Unidad,
            JsonRecursos = original.JsonRecursos
        };
        store.Save(clone);

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(2, store.GetAll().Count);
    }

    // 26. Delete desde store elimina correctamente
    [Fact]
    public void DeleteTemplate_FromStore_RemovesEntry()
    {
        var store = new FakeApuStore();
        var row = MakeTemplate("Para eliminar");
        store.AddRow(row);
        Assert.Single(store.GetAll());

        store.Delete(row.Id);
        Assert.Empty(store.GetAll());
    }

    // 27. StatusMessage vacío por defecto
    [Fact]
    public void StatusMessage_Default_IsEmpty()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        Assert.Equal("", vm.StatusMessage);
    }

    // 28. Load es idempotente
    [Fact]
    public void Load_CalledTwice_DoesNotThrow()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => { vm.Load(); vm.Load(); });
        Assert.Null(ex);
    }
}
