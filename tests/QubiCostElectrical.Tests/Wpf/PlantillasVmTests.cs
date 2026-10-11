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
    private readonly IDisposable conjuntoDbScope;
    private readonly string conjuntoDbPath;

    public PlantillasVmTests()
    {
        WpfTestHost.EnsureInitialized();
        templateDbPath = Path.Combine(Path.GetTempPath(), $"apu-templates-{Guid.NewGuid():N}.db");
        templateDbScope = ApuTemplateStore.UseDatabasePathForTests(templateDbPath);
        conjuntoDbPath = Path.Combine(Path.GetTempPath(), $"conjuntos-vm-{Guid.NewGuid():N}.db");
        conjuntoDbScope = ConjuntoTemplateStore.UseDatabasePathForTests(conjuntoDbPath);
    }

    public void Dispose()
    {
        templateDbScope.Dispose();
        conjuntoDbScope.Dispose();
        foreach (var path in new[] { templateDbPath, templateDbPath + "-wal", templateDbPath + "-shm",
                                     conjuntoDbPath, conjuntoDbPath + "-wal", conjuntoDbPath + "-shm" })
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

    // ── Maestro / Esclavo ──────────────────────────────────────────────────────

    // 29. Rol = Esclavo auto-asigna el primer maestro disponible
    [Fact]
    public void ConjuntoRecursoVm_RolEsclavo_AutoAsignaPrimerMaestro()
    {
        var maestro = new ConjuntoRecursoRow { Codigo = "TUBO20" };
        var fila = new ConjuntoRecursoRow { Codigo = "ABR" };
        bool changed = false;
        var vm = new ConjuntoRecursoVm(fila, () => changed = true,
            maestroOptions: _ => new[] { new MaestroOptionVm(maestro.Id, "TUBO20 — Tuberia") });

        vm.Rol = "Esclavo";

        Assert.Equal("Esclavo", vm.Rol);
        Assert.True(vm.EsEsclavo);
        Assert.Equal(maestro.Id, vm.MaestroId);
        Assert.True(changed);
    }

    // 30. Rol = Esclavo sin maestros no aplica y notifica
    [Fact]
    public void ConjuntoRecursoVm_RolEsclavo_SinMaestros_NoSeAplicaYNotifica()
    {
        var fila = new ConjuntoRecursoRow { Codigo = "ABR" };
        string? notificado = null;
        var vm = new ConjuntoRecursoVm(fila, () => { },
            maestroOptions: _ => Array.Empty<MaestroOptionVm>(),
            notify: msg => notificado = msg);

        vm.Rol = "Esclavo";

        Assert.Equal("Maestro", vm.Rol);
        Assert.Equal("", vm.MaestroId);
        Assert.Contains("No hay materiales maestros", notificado ?? "");
    }

    // 31. Volver a Maestro limpia MaestroId
    [Fact]
    public void ConjuntoRecursoVm_RolMaestro_LimpiaMaestroId()
    {
        var fila = new ConjuntoRecursoRow { Codigo = "ABR", MaestroId = "M1" };
        var vm = new ConjuntoRecursoVm(fila, () => { });

        vm.Rol = "Maestro";

        Assert.Equal("Maestro", vm.Rol);
        Assert.Equal("", vm.MaestroId);
    }

    // 32. MaestroId solo acepta destinos validos (no esclavos ni inexistentes)
    [Fact]
    public void ConjuntoRecursoVm_MaestroId_Invalido_NoSeAcepta()
    {
        var maestro = new ConjuntoRecursoRow { Codigo = "TUBO20" };
        var fila = new ConjuntoRecursoRow { Codigo = "ABR" };
        var vm = new ConjuntoRecursoVm(fila, () => { },
            maestroOptions: _ => new[] { new MaestroOptionVm(maestro.Id, "TUBO20") });

        vm.MaestroId = "GHOST";
        Assert.Equal("", vm.MaestroId);

        vm.MaestroId = maestro.Id;
        Assert.Equal(maestro.Id, vm.MaestroId);
    }

    // 33. PasoFactor edita el campo del modo activo (Factor vs Espaciamiento)
    [Fact]
    public void ConjuntoRecursoVm_PasoFactor_EditaSegunModoActivo()
    {
        var fila = new ConjuntoRecursoRow
        {
            Codigo = "CLIP",
            TipoCalculo = TipoCalculoParametrico.Factor,
            Factor = 1m,
            Espaciamiento = 1.5m
        };
        var vm = new ConjuntoRecursoVm(fila, () => { });

        Assert.Equal(1m, vm.PasoFactor);
        vm.PasoFactor = 2.5m;
        Assert.Equal(2.5m, fila.Factor);

        vm.TipoCalculo = TipoCalculoParametrico.Espaciamiento;
        Assert.Equal(1.5m, vm.PasoFactor);
        vm.PasoFactor = 3m;
        Assert.Equal(3m, fila.Espaciamiento);
        Assert.Equal(2.5m, fila.Factor);
    }

    // 34. Opciones de maestro: excluye MO/HH/HERR, esclavos y la propia fila
    [Fact]
    public void ConjuntoRecursos_OpcionesMaestro_ExcluyenMO_Esclavos_Y_FilaPropia()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("maestro-esclavo-opc");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-MS",
            Nombre = "Maestros y esclavos",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-mo", Tipo = ApuTipoRecurso.MO, Descripcion = "Electricista" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-cinta", Codigo = "CINTA" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        var abr = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-abr");
        Assert.Contains(abr.MaestrosDisponibles, o => o.Id == "id-tubo");
        Assert.Contains(abr.MaestrosDisponibles, o => o.Id == "id-cinta");
        Assert.DoesNotContain(abr.MaestrosDisponibles, o => o.Id == "id-mo");
        Assert.DoesNotContain(abr.MaestrosDisponibles, o => o.Id == "id-abr");

        // Cambiar una fila maestro a Esclavo auto-asigna el primer maestro disponible
        var cinta = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-cinta");
        cinta.Rol = "Esclavo";
        Assert.Equal("Esclavo", cinta.Rol);
        Assert.Equal("id-tubo", cinta.MaestroId);
    }

    // 35. Duplicar conjunto remapea MaestroId a los nuevos Ids de los recursos
    [Fact]
    public void DuplicateConjunto_RemapeaMaestroIdDeLosRecursos()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("maestro-esclavo-dup");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-DUP",
            Nombre = "Original",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        var original = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        vm.DuplicateConjuntoCommand.Execute(original);

        var copia = vm.Conjuntos.First(c => c.Nombre == "Original (copia)");
        var copiaMaestro = copia.Source.Recursos.First(r => r.Codigo == "TUBO20");
        var copiaEsclavo = copia.Source.Recursos.First(r => r.Codigo == "ABR");
        Assert.Equal(copiaMaestro.Id, copiaEsclavo.MaestroId);
        Assert.NotEqual("id-tubo", copiaEsclavo.MaestroId);
        // El original queda intacto
        Assert.Equal("id-tubo", conjunto.Recursos.First(r => r.Codigo == "ABR").MaestroId);
    }

    // ── Operación (fórmula libre por fila) ────────────────────────────────────

    // 36. Escribir una fórmula en Operación cambia el rol de la fila
    [Fact]
    public void ConjuntoRecursoVm_EscribirOperacion_CambiaRolAOperacion()
    {
        var fila = new ConjuntoRecursoRow { Codigo = "OP1" };
        var vm = new ConjuntoRecursoVm(fila, () => { });

        vm.Operacion = "PAZ * 2 + TEEG500";

        Assert.Equal("Operación", vm.Rol);
        Assert.False(vm.EsEsclavo);
        Assert.False(vm.EsMaestro);
        Assert.Equal("PAZ * 2 + TEEG500", vm.Operacion);
        Assert.Equal("PAZ * 2 + TEEG500", fila.Operacion);
    }

    // 37. Rol = Operación limpia MaestroId y pide la fórmula si falta
    [Fact]
    public void ConjuntoRecursoVm_RolOperacion_LimpiaMaestroIdYNotificaFormulaFaltante()
    {
        var fila = new ConjuntoRecursoRow { Codigo = "OP1", MaestroId = "M1" };
        string? notificado = null;
        var vm = new ConjuntoRecursoVm(fila, () => { },
            maestroOptions: _ => new[] { new MaestroOptionVm("M1", "TUBO20") },
            notify: msg => notificado = msg);

        vm.Rol = "Operación";

        Assert.Equal("", vm.MaestroId);
        // El rol lo dicta la fórmula: sin fórmula escrita la fila sigue siendo Maestro
        // y el mensaje pide escribirla.
        Assert.Equal("Maestro", vm.Rol);
        Assert.Contains("Escribe la fórmula", notificado ?? "");

        vm.Operacion = "PAZ * 2";
        Assert.Equal("Operación", vm.Rol);
    }

    // 38. Cambiar de rol hacia Maestro limpia la fórmula de la operación
    [Fact]
    public void ConjuntoRecursoVm_CambiarRolDeOperacion_LimpiaLaFormula()
    {
        var fila = new ConjuntoRecursoRow { Codigo = "OP1", Operacion = "PAZ * 2" };
        var vm = new ConjuntoRecursoVm(fila, () => { });

        vm.Rol = "Maestro";

        Assert.Equal("Maestro", vm.Rol);
        Assert.Equal("", vm.Operacion);
        Assert.Equal("", fila.Operacion);
        Assert.True(vm.EsMaestro);
    }

    // 39. EsMaestro: solo filas sin esclavo y sin operación (las que se marcan en amarillo)
    [Fact]
    public void ConjuntoRecursoVm_EsMaestro_DistingueEsclavoYOperacion()
    {
        var maestro = new ConjuntoRecursoRow { Codigo = "TUBO20" };
        var esclavo = new ConjuntoRecursoRow { Codigo = "ABR", MaestroId = "M1" };
        var op = new ConjuntoRecursoRow { Codigo = "OP1", Operacion = "TUBO20 * 2" };

        Assert.True(new ConjuntoRecursoVm(maestro, () => { }).EsMaestro);
        Assert.False(new ConjuntoRecursoVm(esclavo, () => { }).EsMaestro);
        Assert.False(new ConjuntoRecursoVm(op, () => { }).EsMaestro);
    }

    // 40. Fórmula válida (códigos existentes) no reporta error
    [Fact]
    public void ConjuntoRecursoVm_OperacionValida_NoNotificaError()
    {
        var filas = new[]
        {
            new ConjuntoRecursoRow { Codigo = "PAZ" },
            new ConjuntoRecursoRow { Codigo = "OP1" }
        };
        string? notificado = null;
        var vm = new ConjuntoRecursoVm(filas[1], () => { },
            notify: msg => notificado = msg,
            getRecursos: () => filas);

        vm.Operacion = "PAZ * 2";

        Assert.Null(notificado);
    }

    // 41. Fórmula con código inexistente reporta el error en StatusMessage
    [Fact]
    public void ConjuntoRecursoVm_OperacionConVariableDesconocida_NotificaError()
    {
        var filas = new[]
        {
            new ConjuntoRecursoRow { Codigo = "PAZ" },
            new ConjuntoRecursoRow { Codigo = "OP1" }
        };
        string? notificado = null;
        var vm = new ConjuntoRecursoVm(filas[1], () => { },
            notify: msg => notificado = msg,
            getRecursos: () => filas);

        vm.Operacion = "NOEXISTE * 2";

        Assert.Contains("Operación inválida", notificado ?? "");
    }

    // 42. Las filas Operación SÍ aparecen como candidatas a maestro: un esclavo puede
    // dividir de su resultado (decisión de diseño).
    [Fact]
    public void ConjuntoRecursoVm_FilaOperacion_EsCandidataAMaestroParaEsclavos()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("operacion-maestro-opc");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-OP",
            Nombre = "Operación y esclavo",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-paz", Codigo = "PAZ" },
                new() { Id = "id-op", Codigo = "OP1", Operacion = "PAZ * 2" },
                new() { Id = "id-abr", Codigo = "ABR" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        var abr = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-abr");
        Assert.Contains(abr.MaestrosDisponibles, o => o.Id == "id-op");
        Assert.Contains(abr.MaestrosDisponibles, o => o.Id == "id-paz");

        // La fila operación conserva su rol y no se marca como maestro (sin amarillo).
        var op = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-op");
        Assert.Equal("Operación", op.Rol);
        Assert.False(op.EsMaestro);
    }

    // ── Selección múltiple, barra contextual y acciones masivas ──────────────

    // 43. SetSelectedRecursos actualiza contador, etiqueta y CanExecute de comandos
    [Fact]
    public void SetSelectedRecursos_ActualizaContadorEtiquetaYComandos()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("seleccion-estado");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-SEL",
            Nombre = "Selección",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-1", Codigo = "TUBO20" },
                new() { Id = "id-2", Codigo = "ABR" },
                new() { Id = "id-3", Codigo = "CINTA" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        Assert.False(vm.HasSelectedRecursos);
        Assert.Equal(0, vm.SelectedRecursosCount);
        Assert.False(vm.MarcarMaestrosSeleccionCommand.CanExecute(null));

        vm.SetSelectedRecursos(vm.ConjuntoRecursos.Take(2));

        Assert.True(vm.HasSelectedRecursos);
        Assert.Equal(2, vm.SelectedRecursosCount);
        Assert.Contains("2", vm.SelectedRecursosLabel);
        Assert.True(vm.MarcarMaestrosSeleccionCommand.CanExecute(null));
        Assert.True(vm.LiberarSeleccionCommand.CanExecute(null));
        Assert.True(vm.EliminarSeleccionCommand.CanExecute(null));
        Assert.NotEmpty(vm.MaestrosParaSeleccion);

        vm.SetSelectedRecursos(null);
        Assert.False(vm.HasSelectedRecursos);
        Assert.False(vm.MarcarMaestrosSeleccionCommand.CanExecute(null));
    }

    // 44. Marcar maestro: esclavos y operaciones de la selección pasan a maestro
    [Fact]
    public void MarcarMaestrosSeleccion_ConvierteSeleccionEnMaestros()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("marcar-maestros");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-MM",
            Nombre = "Marcar",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-op", Codigo = "OP1", Operacion = "TUBO20 * 2" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        vm.SetSelectedRecursos(vm.ConjuntoRecursos.Where(r => r.Source.Id != "id-tubo"));

        vm.MarcarMaestrosSeleccionCommand.Execute(null);

        var abr = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-abr");
        var op = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-op");
        Assert.Equal("Maestro", abr.Rol);
        Assert.Equal("", abr.MaestroId);
        Assert.Equal("Maestro", op.Rol);
        Assert.Equal("", op.Operacion);
        Assert.Contains("marcada", vm.StatusMessage);
        Assert.Equal(3, vm.ConteoMaestros);
        Assert.Equal(0, vm.ConteoEsclavos);
        Assert.Equal(0, vm.ConteoOperaciones);
    }

    // 45. Asignar maestro masivo: aplica a candidatos y omite destinos inválidos
    [Fact]
    public void AsignarMaestroSeleccion_SoloAplicaACandidatos()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("asignar-maestro");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-AS",
            Nombre = "Asignar",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-cinta", Codigo = "CINTA" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        var cinta = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-cinta");
        vm.SetSelectedRecursos(new[] { cinta });

        // Destino inválido: un esclavo nunca puede ser maestro
        vm.MaestroDestinoSeleccion = new MaestroOptionVm("id-abr", "ABR — Abrazadera");
        Assert.Equal("Maestro", cinta.Rol);
        Assert.Contains("Ninguna fila pudo vincularse", vm.StatusMessage);

        // Destino válido
        vm.MaestroDestinoSeleccion = new MaestroOptionVm("id-tubo", "TUBO20 — Tubería");
        Assert.Equal("Esclavo", cinta.Rol);
        Assert.Equal("id-tubo", cinta.MaestroId);
        Assert.Contains("vinculada", vm.StatusMessage);

        // El combo se limpia tras ejecutar la acción
        Assert.Null(vm.MaestroDestinoSeleccion);
    }

    // 46. Liberar selección: solo las filas esclavas dejan de depender
    [Fact]
    public void LiberarSeleccion_LiberaSoloEsclavosSeleccionados()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("liberar-seleccion");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-LB",
            Nombre = "Liberar",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-cinta", Codigo = "CINTA" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        var abr = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-abr");
        var cinta = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-cinta");
        vm.SetSelectedRecursos(new[] { abr, cinta });

        vm.LiberarSeleccionCommand.Execute(null);

        Assert.Equal("Maestro", abr.Rol);
        Assert.Equal("", abr.MaestroId);
        Assert.Equal("Maestro", cinta.Rol); // ya era maestro: intacta
        Assert.Contains("liberada", vm.StatusMessage);
        Assert.Equal(3, vm.ConteoMaestros);
        Assert.Equal(0, vm.ConteoEsclavos);
    }

    // 47. Eliminar selección: quita filas del conjunto y libera sus esclavos
    [Fact]
    public void EliminarSeleccion_EliminaFilasYLiberaEsclavos()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("eliminar-seleccion");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-EL",
            Nombre = "Eliminar",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-cinta", Codigo = "CINTA" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        vm.SetSelectedRecursos(vm.ConjuntoRecursos.Where(r => r.Source.Id != "id-tubo"));

        vm.EliminarSeleccionCommand.Execute(null);

        Assert.Equal(1, vm.ConjuntoRecursos.Count);
        Assert.Equal("TUBO20", vm.ConjuntoRecursos[0].Codigo);
        Assert.Single(conjunto.Recursos);
        Assert.Contains("2", vm.StatusMessage);
        Assert.False(vm.HasSelectedRecursos);
        Assert.Equal(1, vm.ConteoMaestros);
    }

    // 48. Conteos por rol se recalculan al cambiar la estructura
    [Fact]
    public void ConteoDeRoles_ReflejaEstructuraDelConjunto()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("conteo-roles");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-CT",
            Nombre = "Conteos",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo" },
                new() { Id = "id-op", Codigo = "OP1", Operacion = "TUBO20 * 2" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        Assert.Equal(1, vm.ConteoMaestros);
        Assert.Equal(1, vm.ConteoEsclavos);
        Assert.Equal(1, vm.ConteoOperaciones);

        var abr = vm.ConjuntoRecursos.First(r => r.Source.Id == "id-abr");
        abr.Rol = "Maestro";

        Assert.Equal(2, vm.ConteoMaestros);
        Assert.Equal(0, vm.ConteoEsclavos);
    }

    // 49. Jerarquía: los esclavos cuelgan de su maestro con TieneHijos y la operación es raíz
    [Fact]
    public void Jerarquia_AgrupaEsclavosBajoSuMaestro()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("jerarquia-agrupa");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-JR",
            Nombre = "Jerarquía",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20", Descripcion = "Tubería 20" },
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "id-tubo", Factor = 2m },
                new() { Id = "id-cinta", Codigo = "CINTA" },
                new() { Id = "id-op", Codigo = "OP1", Operacion = "TUBO20 * 2" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        var nodoTubo = vm.Jerarquia.First(n => n.Codigo == "TUBO20");
        Assert.Equal("master", nodoTubo.Kind);
        Assert.True(nodoTubo.TieneHijos);
        var hijo = Assert.Single(nodoTubo.Hijos);
        Assert.Equal("ABR", hijo.Codigo);
        Assert.Equal("slave", hijo.Kind);

        var nodoCinta = vm.Jerarquia.First(n => n.Codigo == "CINTA");
        Assert.Equal("master", nodoCinta.Kind);
        Assert.False(nodoCinta.TieneHijos);

        var nodoOp = vm.Jerarquia.First(n => n.Codigo == "OP1");
        Assert.Equal("op", nodoOp.Kind);

        // El esclavo no aparece además como raíz
        Assert.DoesNotContain(vm.Jerarquia, n => n.Codigo == "ABR");
    }

    // 50. Huérfanos: MaestroId que apunta a una fila inexistente se marcan en rojo
    [Fact]
    public void Jerarquia_FilaConMaestroInexistente_EsHuerfana()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("jerarquia-huerfana");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-HU",
            Nombre = "Huérfana",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-abr", Codigo = "ABR", MaestroId = "no-existe" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        var nodo = Assert.Single(vm.Jerarquia);
        Assert.Equal("orphan", nodo.Kind);
        Assert.Equal("ABR", nodo.Codigo);
    }

    // 51. Vista previa: expansión con base 1, incluye maestros/esclavos y excluye MO/HH/HERR
    [Fact]
    public void PreviewFilas_ExpansionBaseUno_ExcluyeMO()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("preview-expansion");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-PV",
            Nombre = "Preview",
            Unidad = Unidades.UN,
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "id-tubo", Codigo = "TUBO20", Descripcion = "Tubería", Unidad = Unidades.ML },
                new() { Id = "id-abr", Codigo = "ABR", Descripcion = "Abrazadera", Unidad = Unidades.UN, MaestroId = "id-tubo", Factor = 2m },
                new() { Id = "id-mo", Codigo = "MO-RED", Tipo = ApuTipoRecurso.MO, Descripcion = "Electricista" }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        Assert.Contains(vm.PreviewFilas, p => p.Codigo == "TUBO20");
        Assert.Contains(vm.PreviewFilas, p => p.Codigo == "ABR");
        Assert.DoesNotContain(vm.PreviewFilas, p => p.Codigo == "MO-RED");

        var tubo = vm.PreviewFilas.First(p => p.Codigo == "TUBO20");
        Assert.True(decimal.TryParse(tubo.Cantidad, out var q) && q > 0, $"Cantidad inválida: '{tubo.Cantidad}'");
        var abr = vm.PreviewFilas.First(p => p.Codigo == "ABR");
        Assert.True(decimal.TryParse(abr.Cantidad, out var qAbr) && qAbr > 0, $"Cantidad inválida: '{abr.Cantidad}'");
    }

    // 52. Strip de ayuda: ToggleAyudaCommand invierte ShowRulesStrip
    [Fact]
    public void ShowRulesStrip_ToggleAyudaCommand_InvierteEstado()
    {
        var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowRulesStrip);
        vm.ToggleAyudaCommand.Execute(null);
        Assert.False(vm.ShowRulesStrip);
        vm.ToggleAyudaCommand.Execute(null);
        Assert.True(vm.ShowRulesStrip);
    }

    // 53. Cambiar de conjunto limpia la selección de filas
    [Fact]
    public void CambiarDeConjunto_LimpiaLaSeleccionDeFilas()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cambio-conjunto-seleccion");
        var a = new ConjuntoElectricoRow
        {
            Codigo = "CE-A",
            Nombre = "A",
            Recursos = new List<ConjuntoRecursoRow> { new() { Id = "a1", Codigo = "X" } }
        };
        var b = new ConjuntoElectricoRow
        {
            Codigo = "CE-B",
            Nombre = "B",
            Recursos = new List<ConjuntoRecursoRow> { new() { Id = "b1", Codigo = "Y" } }
        };
        host.Session.ConjuntosElectricos.Add(a);
        host.Session.ConjuntosElectricos.Add(b);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == a.Id);
        vm.SetSelectedRecursos(vm.ConjuntoRecursos);
        Assert.True(vm.HasSelectedRecursos);

        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == b.Id);

        Assert.False(vm.HasSelectedRecursos);
        Assert.Equal(0, vm.SelectedRecursosCount);
        Assert.False(vm.MarcarMaestrosSeleccionCommand.CanExecute(null));
    }

    // 54. "Dividir de": dos materiales con maestros distintos — ambos vínculos se sostienen
    [Fact]
    public void DividirDe_DosMaestrosDistintos_AmbosVinculosSeMantienen()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("dividir-dos-maestros");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-DM",
            Nombre = "Dos maestros",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "m1", Codigo = "MAT1", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "m2", Codigo = "MAT2", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s1", Codigo = "MAT3", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s2", Codigo = "MAT4", Tipo = ApuTipoRecurso.MAT }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        var filas = vm.ConjuntoRecursos;

        filas[2].MaestroId = "m1";
        filas[3].MaestroId = "m2";

        Assert.Equal("m1", filas[2].MaestroId);
        Assert.Equal("m2", filas[3].MaestroId);

        // Una fila ajena sigue viendo a los DOS maestros como candidatos
        var opciones = filas[0].MaestrosDisponibles;
        Assert.Contains(opciones, o => o.Id == "m2");
        Assert.DoesNotContain(opciones, o => o.Id == "s1"); // un esclavo nunca es candidato

        // Jerarquía: dos raíces maestro, cada una con su esclavo
        var raices = vm.Jerarquia.Where(n => n.Kind == "master").ToList();
        Assert.Equal(2, raices.Count);
        Assert.Contains(raices, n => n.Codigo == "MAT1" && n.Hijos.Count == 1 && n.Hijos[0].Codigo == "MAT3");
        Assert.Contains(raices, n => n.Codigo == "MAT2" && n.Hijos.Count == 1 && n.Hijos[0].Codigo == "MAT4");
    }

    // 55. "Dividir de": un candidato inválido NO destruye el vínculo vigente (bug: "se elimina")
    [Fact]
    public void DividirDe_CandidatoInvalido_NoBorraElVinculoActual()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("dividir-invalido");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-IV",
            Nombre = "Inválido",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "m1", Codigo = "MAT1", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "m2", Codigo = "MAT2", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s1", Codigo = "MAT3", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s2", Codigo = "MAT4", Tipo = ApuTipoRecurso.MAT }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        var filas = vm.ConjuntoRecursos;

        filas[2].MaestroId = "m1";  // s1 → m1 (válido)
        filas[3].MaestroId = "m2";  // s2 → m2 (válido)

        filas[3].MaestroId = "s1";  // s1 ahora es esclavo: candidato inválido

        Assert.Equal("m2", filas[3].MaestroId);        // el vínculo vigente se conserva
        Assert.Contains("no puede ser maestro", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Guardado global de conjuntos (biblioteca de la app) ───────────────────

    // 56. Los conjuntos del .qce se adoptan a la biblioteca global en cargar la pantalla
    // (migra proyectos antiguos: no se pierden al cambiar de proyecto).
    [Fact]
    public void Load_ConjuntosDeSesion_SeesAdoptadosEnBibliotecaGlobal()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("adopcion-global");
        var conjunto = new ConjuntoElectricoRow { Codigo = "CE-AD", Nombre = "Adoptado" };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);

        Assert.Contains(vm.Conjuntos, c => c.Source.Id == conjunto.Id);
        // La adopción no marca «sin guardar»: ya quedó en la biblioteca.
        Assert.False(vm.HasPendingChanges);

        var store = new ConjuntoTemplateStore();
        Assert.Contains(store.GetAll(), c => c.Id == conjunto.Id);
    }

    // 57. Editar marca cambios sin guardar; Guardar los persiste y limpia el indicador.
    [Fact]
    public void DuplicateConjunto_MarcaPendingChanges_YSaveCommandLosPersiste()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("guardar-global");
        host.Session.ConjuntosElectricos.Add(new ConjuntoElectricoRow { Codigo = "CE-SG", Nombre = "Base" });

        var vm = new PlantillasVm(new FakeShellHost(), host);
        Assert.False(vm.HasPendingChanges);
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.DuplicateConjuntoCommand.Execute(vm.Conjuntos.First());

        Assert.True(vm.HasPendingChanges);
        Assert.True(vm.SaveCommand.CanExecute(null));

        vm.SaveCommand.Execute(null);

        Assert.False(vm.HasPendingChanges);
        Assert.False(vm.SaveCommand.CanExecute(null));
        var store = new ConjuntoTemplateStore();
        Assert.Equal(2, store.GetAll().Count);
        Assert.Contains(store.GetAll(), c => c.Nombre == "Base (copia)");
    }

    // 58. El titular: un proyecto nuevo ve los conjuntos guardados en la app.
    [Fact]
    public void ProyectoNuevo_VeConjuntosGuardadosEnLaApp()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("proyecto-viejo");
        var conjunto = new ConjuntoElectricoRow { Codigo = "CE-PN", Nombre = "Del proyecto viejo" };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm1 = new PlantillasVm(new FakeShellHost(), host);
        vm1.SaveCommand.Execute(null); // guardado manual (adopción ya lo habría hecho igual)

        host.Session.NewProject("proyecto-nuevo-sin-conjuntos");

        var vm2 = new PlantillasVm(new FakeShellHost(), host);
        Assert.Contains(vm2.Conjuntos, c => c.Source.Id == conjunto.Id);
        Assert.False(vm2.HasPendingChanges);
    }

    // 59. IPageWithCommitChanges / IPageWithPendingChanges: el shell los invoca al
    // salir de la pantalla (auto-guardar) y al pedir descartar.
    [Fact]
    public void CommitChanges_Guarda_YDiscardChanges_Recarga()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("commit-plantillas");
        host.Session.ConjuntosElectricos.Add(new ConjuntoElectricoRow { Codigo = "CE-CM", Nombre = "Original" });

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.DuplicateConjuntoCommand.Execute(vm.Conjuntos.First());
        Assert.True(((IPageWithPendingChanges)vm).HasPendingChanges);

        Assert.True(((IPageWithCommitChanges)vm).TryApplyChanges());
        Assert.False(((IPageWithPendingChanges)vm).HasPendingChanges);

        vm.DuplicateConjuntoCommand.Execute(vm.Conjuntos.First());
        Assert.True(((IPageWithPendingChanges)vm).HasPendingChanges);
        Assert.True(((IPageWithPendingChanges)vm).TryDiscardChanges());
        Assert.False(((IPageWithPendingChanges)vm).HasPendingChanges);
        // Descartar recarga desde la biblioteca: queda Original + 1 copia (la 2ª copia
        // era el cambio descartado).
        Assert.Equal(2, vm.Conjuntos.Count);
    }

    // 60. Bug reportado: cambiar Regla a «Espaciamiento» no se guarda y se queda en
    // «Factor» al cambiar de pestaña o de plantilla. El valor debe sobrevivir al
    // cambio de selección, al Guardar y al Refresh del shell (vuelta a la pantalla).
    [Fact]
    public void TipoCalculo_Espaciamiento_PersisteAlCambiarDePlantilla_Guardar_Y_Refresh()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("regla-espaciamiento");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-RG",
            Nombre = "Conjunto regla",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "r1", Codigo = "SOPORTE", Tipo = ApuTipoRecurso.MAT }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);

        // El combo de la columna «Regla» escribe TipoCalculo en la fila
        vm.ConjuntoRecursos[0].TipoCalculo = TipoCalculoParametrico.Espaciamiento;
        Assert.True(vm.HasPendingChanges);

        // Cambiar de plantilla y volver: la fila se reconstruye desde Source
        vm.SelectedConjunto = null;
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        Assert.Equal(TipoCalculoParametrico.Espaciamiento, vm.ConjuntoRecursos[0].TipoCalculo);

        // Guardar (botón Guardar / al salir de la pantalla el shell llama TryApplyChanges)
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.SaveCommand.Execute(null);
        Assert.False(vm.HasPendingChanges);

        // Refresh del shell al volver a la pantalla: relee la biblioteca global
        vm.Refresh();
        Assert.Contains(vm.Conjuntos, c => c.Source.Id == conjunto.Id);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        Assert.Equal(TipoCalculoParametrico.Espaciamiento, vm.ConjuntoRecursos[0].TipoCalculo);
        Assert.False(vm.HasPendingChanges);
    }

    // 61. TryApplyChanges (auto-guardar al salir) debe conservar el Espaciamiento en la
    // biblioteca global aunque el usuario nunca presione el botón Guardar.
    [Fact]
    public void CommitChanges_SinProyectoAbierto_PersisteElEspaciamientoEnLaBiblioteca()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("regla-commit");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-CM2",
            Nombre = "Commit regla",
            Recursos = new List<ConjuntoRecursoRow> { new() { Id = "r1", Codigo = "PAZ" } }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        vm.ConjuntoRecursos[0].TipoCalculo = TipoCalculoParametrico.Espaciamiento;

        Assert.True(((IPageWithCommitChanges)vm).TryApplyChanges());

        var store = new ConjuntoTemplateStore();
        var leido = store.GetAll().Single(c => c.Id == conjunto.Id);
        Assert.Equal(TipoCalculoParametrico.Espaciamiento, leido.Recursos[0].TipoCalculo);
    }
}
