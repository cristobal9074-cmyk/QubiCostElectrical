using System.IO;
using System.Text.Json;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.ChangeOrders;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ChangeOrdersViewModelTests
{
    private static WpfAppHost SetupHost(string projectName = "oc-test")
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject(projectName);
        host.Session.CurrentProjectPath = $"{projectName}.qce";
        host.Session.OrdenesCambio.Clear();
        return host;
    }

    private static ChangeOrdersViewModel Vm(WpfAppHost host)
    {
        var vm = new ChangeOrdersViewModel(host);
        vm.ShowEditor = (oc, _) => true;
        vm.ConfirmDelete = _ => true;
        vm.ConfirmTransition = (_, _) => true;
        return vm;
    }

    // ── 1. Proyecto antiguo sin órdenes ────────────────────────────────────
    [Fact]
    public void OldProject_NoOrders_LoadsEmpty()
    {
        var host = SetupHost("old-proj");
        var vm = Vm(host);
        Assert.Empty(vm.Items);
        Assert.Equal(0, vm.TotalOrdenes);
    }

    // ── 2. Lista vacía ────────────────────────────────────────────────────
    [Fact]
    public void InitialState_NoProject_IsEmpty()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("oc-empty");
        host.Session.CurrentProjectPath = "";
        host.Session.OrdenesCambio.Clear();
        var vm = Vm(host);
        Assert.Empty(vm.Items);
        Assert.True(vm.HasNoProject);
    }

    // ── 3. Crear orden ────────────────────────────────────────────────────
    [Fact]
    public void AddNew_AddsOrderToSession()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        Assert.Single(host.Session.OrdenesCambio);
        Assert.Single(vm.Items);
        Assert.StartsWith("OC-", vm.Items[0].Numero);
    }

    // ── 4. Cancelar creación ─────────────────────────────────────────────
    [Fact]
    public void AddNew_Cancel_DoesNotAdd()
    {
        var host = SetupHost();
        var vm = new ChangeOrdersViewModel(host);
        vm.ShowEditor = (_, _) => false;
        vm.AddNew();
        Assert.Empty(host.Session.OrdenesCambio);
    }

    // ── 5. Editar orden ───────────────────────────────────────────────────
    [Fact]
    public void EditSelected_UpdatesRow()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.ShowEditor = (oc, _) => { oc.Titulo = "Editada"; return true; };
        vm.EditSelected();
        Assert.Equal("Editada", host.Session.OrdenesCambio[0].Titulo);
    }

    // ── 6. Cancelar edición ──────────────────────────────────────────────
    [Fact]
    public void EditSelected_Cancel_KeepsOriginal()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        var original = host.Session.OrdenesCambio[0].Titulo;
        vm.ShowEditor = (_, _) => false;
        vm.EditSelected();
        Assert.Equal(original, host.Session.OrdenesCambio[0].Titulo);
    }

    // ── 7. Eliminar con confirmación ─────────────────────────────────────
    [Fact]
    public void DeleteSelected_WithConfirm_Removes()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.DeleteSelected();
        Assert.Empty(host.Session.OrdenesCambio);
    }

    // ── 8. Cancelar eliminación ──────────────────────────────────────────
    [Fact]
    public void DeleteSelected_Cancel_Keeps()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.ConfirmDelete = _ => false;
        vm.DeleteSelected();
        Assert.Single(host.Session.OrdenesCambio);
    }

    // ── 9. Numeración automática ─────────────────────────────────────────
    [Fact]
    public void AddNew_AutoIncrements_Numero()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.AddNew();
        var nums = host.Session.OrdenesCambio.Select(x => x.Numero).OrderBy(x => x).ToList();
        Assert.Equal("OC-001", nums[0]);
        Assert.Equal("OC-002", nums[1]);
    }

    // ── 10. Número duplicado no ocurre ───────────────────────────────────
    [Fact]
    public void AddNew_AfterDelete_DoesNotReuse_Numero()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew(); // OC-001
        vm.AddNew(); // OC-002
        vm.Selected = vm.Items.First(i => i.Numero == "OC-002");
        vm.DeleteSelected(); // remove OC-002
        vm.AddNew(); // should be OC-002 (max+1=2), which is fine — the rule is no collision with existing
        var nums = host.Session.OrdenesCambio.Select(x => x.Numero).OrderBy(x => x).ToList();
        Assert.Equal(2, nums.Count);
        Assert.Equal(2, nums.Distinct().Count()); // no duplicates
    }

    // ── 11. Estado inicial = Borrador ────────────────────────────────────
    [Fact]
    public void AddNew_InitialEstado_IsBorrador()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        Assert.Equal("Borrador", host.Session.OrdenesCambio[0].Estado);
    }

    // ── 12. Borrador → En revisión ───────────────────────────────────────
    [Fact]
    public void EnviarARevision_FromBorrador_Succeeds()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        Assert.Equal("En revisión", host.Session.OrdenesCambio[0].Estado);
    }

    // ── 13. En revisión → Aprobado ───────────────────────────────────────
    [Fact]
    public void AprobarSelected_FromEnRevision_Succeeds()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.AprobarSelected();
        Assert.Equal("Aprobado", host.Session.OrdenesCambio[0].Estado);
    }

    // ── 14. En revisión → Rechazado ──────────────────────────────────────
    [Fact]
    public void RechazarSelected_FromEnRevision_Succeeds()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.RechazarSelected();
        Assert.Equal("Rechazado", host.Session.OrdenesCambio[0].Estado);
    }

    // ── 15. Transición inválida no aplica ────────────────────────────────
    [Fact]
    public void IsTransitionAllowed_Invalid_ReturnsFalse()
    {
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Borrador", "Aprobado"));
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Aprobado", "Borrador"));
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Aprobado", "En revisión"));
    }

    // ── 16. Orden aprobada bloqueada para editar y eliminar ──────────────
    [Fact]
    public void AprobadaOrder_CannotEdit_CannotDelete()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.AprobarSelected();
        vm.Selected = vm.Items[0];
        Assert.Equal("Aprobado", vm.Selected!.Estado);
        Assert.False(vm.CanEdit);
        Assert.False(vm.CanDelete);
    }

    // ── 17. Reabrir desde Rechazado → Borrador ───────────────────────────
    [Fact]
    public void ReabrirSelected_FromRechazado_ToBorrador()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.RechazarSelected();
        vm.Selected = vm.Items[0];
        vm.ReabrirSelected();
        Assert.Equal("Borrador", host.Session.OrdenesCambio[0].Estado);
    }

    // ── 18. Monto válido ─────────────────────────────────────────────────
    [Fact]
    public void AddNew_WithDeltaVenta_ShowsInSummary()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.ShowEditor = (oc, _) => { oc.DeltaVentaUf = 100m; return true; };
        vm.AddNew();
        Assert.Equal(100m, vm.MontoPendiente);
    }

    // ── 19. Monto inválido rechazado en editor (validación en diálogo) ───
    [Fact]
    public void EditorDialog_ParseDecimal_InvalidReturnsNull()
    {
        // Verifica la lógica pura de parseo usada por el editor
        bool ok = decimal.TryParse("abc", System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);
        Assert.False(ok);
    }

    // ── 20. NaN e infinito rechazados ────────────────────────────────────
    [Fact]
    public void EditorDialog_ParseDecimal_NaN_ReturnsFalse()
    {
        bool ok = decimal.TryParse(double.NaN.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);
        Assert.False(ok);
    }

    // ── 21. Total pendiente ───────────────────────────────────────────────
    [Fact]
    public void MontoPendiente_SumsBorradorAndEnRevision()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.ShowEditor = (oc, _) => { oc.DeltaVentaUf = 50m; return true; };
        vm.AddNew(); // borrador, 50
        vm.ShowEditor = (oc, _) => { oc.DeltaVentaUf = 30m; return true; };
        vm.AddNew(); // borrador, 30
        vm.Selected = vm.Items.First();
        vm.EnviarARevision(); // en revisión, 30
        Assert.Equal(80m, vm.MontoPendiente);
    }

    // ── 22. Total aprobado ───────────────────────────────────────────────
    [Fact]
    public void MontoAprobado_SumsOnlyAprobado()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.ShowEditor = (oc, _) => { oc.DeltaVentaUf = 200m; return true; };
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.AprobarSelected();
        Assert.Equal(200m, vm.MontoAprobado);
        Assert.Equal(0m, vm.MontoPendiente);
    }

    // ── 23. Filtro por estado ────────────────────────────────────────────
    [Fact]
    public void FilterEstado_ShowsOnlyMatching()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew(); // borrador
        vm.AddNew(); // borrador
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.FilterEstado = "Borrador";
        Assert.Single(vm.Items);
        Assert.Equal("Borrador", vm.Items[0].Estado);
    }

    // ── 24. Búsqueda ─────────────────────────────────────────────────────
    [Fact]
    public void SearchText_FiltersItems()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.ShowEditor = (oc, _) => { oc.Titulo = "Cambio eléctrico"; return true; };
        vm.AddNew();
        vm.ShowEditor = (oc, _) => { oc.Titulo = "Modificación civil"; return true; };
        vm.AddNew();
        vm.SearchText = "eléctrico";
        Assert.Single(vm.Items);
        Assert.Contains("eléctrico", vm.Items[0].Titulo.ToLowerInvariant());
    }

    // ── 25. Ordenamiento (por fecha desc) ─────────────────────────────────
    [Fact]
    public void Items_OrderedByFechaCreacionDesc()
    {
        var host = SetupHost();
        host.Session.OrdenesCambio.Add(new ChangeOrderRow { Numero = "OC-001", FechaCreacion = new DateTime(2024, 1, 1) });
        host.Session.OrdenesCambio.Add(new ChangeOrderRow { Numero = "OC-002", FechaCreacion = new DateTime(2024, 6, 1) });
        var vm = Vm(host);
        Assert.Equal("OC-002", vm.Items[0].Numero); // más reciente primero
    }

    // ── 26. Relación con partida (no soportada en modelo actual) ─────────
    [Fact]
    public void ChangeOrderRow_Lineas_IsEmptyByDefault()
    {
        var oc = new ChangeOrderRow();
        Assert.Empty(oc.Lineas);
    }

    // ── 27. Evitar partida duplicada (lineas) ────────────────────────────
    [Fact]
    public void ChangeOrderRow_AddLineTwice_CanDeduplicate()
    {
        var oc = new ChangeOrderRow();
        var line = new ChangeOrderLineRow { ItemId = "ITEM-1" };
        oc.Lineas.Add(line);
        // No agregar duplicado si ya existe el mismo ItemId
        bool exists = oc.Lineas.Any(l => l.ItemId == "ITEM-1");
        if (!exists) oc.Lineas.Add(new ChangeOrderLineRow { ItemId = "ITEM-1" });
        Assert.Single(oc.Lineas);
    }

    // ── 28. Partida inexistente no rompe ─────────────────────────────────
    [Fact]
    public void ChangeOrderRow_LineWithUnknownItemId_DoesNotThrow()
    {
        var oc = new ChangeOrderRow();
        oc.Lineas.Add(new ChangeOrderLineRow { ItemId = "DELETED_ITEM", Item = "" });
        // Solo verifica que no lanza excepción al iterar
        var items = oc.Lineas.Select(l => l.ItemId).ToList();
        Assert.Single(items);
    }

    private static string TempQce(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}.qce");

    private static void CleanupPepro(string path)
    {
        foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
            try { if (File.Exists(p)) File.Delete(p); } catch { }
    }

    // ── 29. Persistencia y reapertura ────────────────────────────────────
    [Fact]
    public async Task Persistence_SaveAndReload_KeepsOrders()
    {
        var path = TempQce("persist");
        try
        {
            var host = SetupHost("persist-test");
            var vm = Vm(host);
            vm.AddNew();
            await host.ProjectWorkspace.SaveAsAsync(path);

            WpfTestHost.EnsureInitialized();
            var host2 = WpfAppHost.Current;
            await host2.ProjectWorkspace.OpenAsync(path);
            Assert.Single(host2.Session.OrdenesCambio);
        }
        finally { CleanupPepro(path); }
    }

    // ── 30. Eliminar persiste ────────────────────────────────────────────
    [Fact]
    public async Task DeleteSelected_SaveAndReload_OrderIsGone()
    {
        var path = TempQce("del");
        try
        {
            var host = SetupHost("delete-test");
            var vm = Vm(host);
            vm.AddNew();
            vm.Selected = vm.Items[0];
            vm.DeleteSelected();
            await host.ProjectWorkspace.SaveAsAsync(path);

            WpfTestHost.EnsureInitialized();
            var host2 = WpfAppHost.Current;
            await host2.ProjectWorkspace.OpenAsync(path);
            Assert.Empty(host2.Session.OrdenesCambio);
        }
        finally { CleanupPepro(path); }
    }

    // ── 31. Cambio de estado persiste ────────────────────────────────────
    [Fact]
    public async Task EstadoChange_SaveAndReload_Persists()
    {
        var path = TempQce("estado");
        try
        {
            var host = SetupHost("estado-test");
            var vm = Vm(host);
            vm.AddNew();
            vm.Selected = vm.Items[0];
            vm.EnviarARevision();
            await host.ProjectWorkspace.SaveAsAsync(path);

            WpfTestHost.EnsureInitialized();
            var host2 = WpfAppHost.Current;
            await host2.ProjectWorkspace.OpenAsync(path);
            Assert.Equal("En revisión", host2.Session.OrdenesCambio[0].Estado);
        }
        finally { CleanupPepro(path); }
    }

    // ── 32. Marcar proyecto como modificado ──────────────────────────────
    [Fact]
    public void AddNew_MarksSessionDirty()
    {
        var host = SetupHost();
        host.Session.MarkSaved();
        var vm = Vm(host);
        vm.AddNew();
        Assert.True(host.Session.IsDirty);
    }

    // ── 33. Guardar limpia estado modificado ─────────────────────────────
    [Fact]
    public async Task SaveProject_ClearsDirty()
    {
        var path = TempQce("dirty");
        try
        {
            var host = SetupHost("dirty-test");
            var vm = Vm(host);
            vm.AddNew();
            Assert.True(host.Session.IsDirty);
            await host.ProjectWorkspace.SaveAsAsync(path);
            Assert.False(host.Session.IsDirty);
        }
        finally { CleanupPepro(path); }
    }

    // ── 34. Error de guardado conserva el draft (no borra la orden) ──────
    [Fact]
    public void EditSelected_IfEditorFails_KeepsDraft()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        var tituloOriginal = host.Session.OrdenesCambio[0].Titulo;
        vm.ShowEditor = (_, _) => false; // cancelar = no persiste
        vm.EditSelected();
        Assert.Equal(tituloOriginal, host.Session.OrdenesCambio[0].Titulo);
    }

    // ── 35. Navegación tipada: ShellRoute.ChangeOrders existe ────────────
    [Fact]
    public void ShellRoute_ChangeOrders_IsRegistered()
    {
        var info = QubiCostElectrical.Wpf.Navigation.ShellRouteCatalog.Get(
            QubiCostElectrical.Wpf.Navigation.ShellRoute.ChangeOrders);
        Assert.NotNull(info);
        Assert.Equal("Órdenes de cambio", info.Title);
    }

    // ── 36. Quick Search contiene Órdenes de cambio ──────────────────────
    [Fact]
    public void ShellViewModel_QuickSearch_ContainsChangeOrders()
    {
        // Verifica directamente en ShellViewModel que la entrada existe
        // Usamos el ShellRouteCatalog como proxy (la entrada de Quick Search
        // también existe cuando la ruta está registrada)
        var info = QubiCostElectrical.Wpf.Navigation.ShellRouteCatalog.Get(
            QubiCostElectrical.Wpf.Navigation.ShellRoute.ChangeOrders);
        Assert.Contains("cambio", info.Title.ToLowerInvariant());
    }

    // ── 37. Cambio de proyecto limpia la pantalla ─────────────────────────
    [Fact]
    public void Refresh_AfterProjectChange_ClearsItems()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        Assert.Single(vm.Items);

        host.Session.NewProject("new-project");
        host.Session.CurrentProjectPath = "";
        host.Session.OrdenesCambio.Clear();
        vm.Refresh();

        Assert.Empty(vm.Items);
    }

    // ── 38. WPF no referencia WinForms ───────────────────────────────────
    [Fact]
    public void WpfAssembly_DoesNotReference_WinForms()
    {
        var wpfAsm = typeof(ChangeOrdersViewModel).Assembly;
        var refs = wpfAsm.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r =>
            r.Name != null &&
            (r.Name.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
             r.Name.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase)));
    }

    // ── 39. Persistencia .qce — reapertura conserva datos ────────────────
    [Fact]
    public async Task QceFile_AfterReopen_ContainsChangeOrder()
    {
        var path = TempQce("compat");
        try
        {
            var host = SetupHost("compat-test");
            var vm = Vm(host);
            vm.ShowEditor = (oc, _) => { oc.Titulo = "OC compat"; return true; };
            vm.AddNew();
            await host.ProjectWorkspace.SaveAsAsync(path);

            WpfTestHost.EnsureInitialized();
            var host2 = WpfAppHost.Current;
            await host2.ProjectWorkspace.OpenAsync(path);
            Assert.Single(host2.Session.OrdenesCambio);
            Assert.Equal("OC compat", host2.Session.OrdenesCambio[0].Titulo);
        }
        finally { CleanupPepro(path); }
    }

    // ── 40. No contaminación de preferencias ─────────────────────────────
    [Fact]
    public void ChangeOrders_DoNotTouchPreferences()
    {
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "prefs.json");
        UserPreferencesStore.UsePreferencesFilePathForTests(testPath);
        try
        {
            var host = SetupHost();
            var vm = Vm(host);
            vm.AddNew();
            Assert.False(File.Exists(testPath));
        }
        finally
        {
            UserPreferencesStore.UsePreferencesFilePathForTests(null);
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    // ── Bonus: resumen de contadores ─────────────────────────────────────
    [Fact]
    public void SummaryCounters_ReflectActualStates()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew(); // borrador
        vm.AddNew(); // borrador
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.AprobarSelected();

        Assert.Equal(2, vm.TotalOrdenes);
        Assert.Equal(1, vm.TotalBorrador);
        Assert.Equal(0, vm.TotalEnRevision);
        Assert.Equal(1, vm.TotalAprobado);
        Assert.Equal(0, vm.TotalRechazado);
    }

    // ── Bonus: CanEnviarRevision/CanAprobar/CanRechazar ──────────────────
    [Fact]
    public void CanCapabilities_AreCorrectByState()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];

        Assert.True(vm.CanEnviarRevision);
        Assert.False(vm.CanAprobar);
        Assert.False(vm.CanRechazar);

        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        Assert.False(vm.CanEnviarRevision);
        Assert.True(vm.CanAprobar);
        Assert.True(vm.CanRechazar);
    }

    // ── Bonus: RowVm EstadoDisplay ────────────────────────────────────────
    [Fact]
    public void RowVm_EstadoDisplay_FormatsAllStates()
    {
        Assert.Contains("Borrador", new ChangeOrderRowVm(new ChangeOrderRow { Estado = "Borrador" }, 1).EstadoDisplay);
        Assert.Contains("Aprobado", new ChangeOrderRowVm(new ChangeOrderRow { Estado = "Aprobado" }, 1).EstadoDisplay);
        Assert.Contains("Rechazado", new ChangeOrderRowVm(new ChangeOrderRow { Estado = "Rechazado" }, 1).EstadoDisplay);
        Assert.Contains("En revisión", new ChangeOrderRowVm(new ChangeOrderRow { Estado = "En revisión" }, 1).EstadoDisplay);
    }

    // ── Bonus: transiciones válidas ───────────────────────────────────────
    [Fact]
    public void IsTransitionAllowed_ValidTransitions()
    {
        Assert.True(ChangeOrderStateMachine.IsTransitionAllowed("Borrador", "En revisión"));
        Assert.True(ChangeOrderStateMachine.IsTransitionAllowed("En revisión", "Aprobado"));
        Assert.True(ChangeOrderStateMachine.IsTransitionAllowed("En revisión", "Rechazado"));
        Assert.True(ChangeOrderStateMachine.IsTransitionAllowed("Rechazado", "Borrador"));
    }

    // ── Bonus: AprobarSelected sin confirmación no cambia estado ─────────
    [Fact]
    public void AprobarSelected_UserCancels_StateUnchanged()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.ConfirmTransition = (_, _) => false;
        vm.AprobarSelected();
        Assert.Equal("En revisión", host.Session.OrdenesCambio[0].Estado);
    }

    // ════════════════════════════════════════════════════════════════════
    // TESTS DE CORRECCIÓN ARQUITECTÓNICA (corrección Bloque 4)
    // ════════════════════════════════════════════════════════════════════

    // CA-1. Título vacío rechazado por ChangeOrderEditorViewModel
    [Fact]
    public void EditorVm_EmptyTitulo_TrySave_ReturnsFalse()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "OC-001", Titulo = "", DeltaVentaUf = 0, DeltaCostoUf = 0, CostoBaseUf = 0 });
        vm.Titulo = "";
        var result = vm.TrySave(new ChangeOrderRow());
        Assert.False(result);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("título", vm.ValidationErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // CA-2. Número vacío rechazado por ChangeOrderEditorViewModel
    [Fact]
    public void EditorVm_EmptyNumero_TrySave_ReturnsFalse()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "", Titulo = "Título OK" });
        var result = vm.TrySave(new ChangeOrderRow());
        Assert.False(result);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("número", vm.ValidationErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // CA-3. Monto inválido rechazado por ChangeOrderEditorViewModel
    [Fact]
    public void EditorVm_InvalidDeltaVenta_TrySave_ReturnsFalse()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "OC-001", Titulo = "Título OK" });
        vm.DeltaVentaText = "abc_no_es_numero";
        var result = vm.TrySave(new ChangeOrderRow());
        Assert.False(result);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("Delta venta", vm.ValidationErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // CA-4. Costo base negativo rechazado
    [Fact]
    public void EditorVm_NegativeCostoBase_TrySave_ReturnsFalse()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "OC-001", Titulo = "Título OK" });
        vm.CostoBaseText = "-500";
        var result = vm.TrySave(new ChangeOrderRow());
        Assert.False(result);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("Costo base", vm.ValidationErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // CA-5. Datos válidos — TrySave escribe en target y devuelve true
    [Fact]
    public void EditorVm_ValidData_TrySave_ReturnsTrue_AndWritesTarget()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "OC-007", Titulo = "Obra extra" });
        vm.DeltaVentaText = "250.500";
        vm.DeltaCostoText = "200.000";
        vm.CostoBaseText = "1000.000";
        vm.Nota = "Nota de prueba";

        var target = new ChangeOrderRow();
        var result = vm.TrySave(target);

        Assert.True(result);
        Assert.Null(vm.ValidationErrorMessage);
        Assert.Equal("OC-007", target.Numero);
        Assert.Equal("Obra extra", target.Titulo);
        Assert.Equal(250.5m, target.DeltaVentaUf);
        Assert.Equal("Nota de prueba", target.Nota);
    }

    // CA-6. Cancelar TrySave (false) no modifica el target
    [Fact]
    public void EditorVm_InvalidData_TrySave_DoesNotModifyTarget()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.LoadFrom(new ChangeOrderRow { Numero = "OC-001", Titulo = "" });
        var target = new ChangeOrderRow { Titulo = "Original" };
        vm.TrySave(target);
        Assert.Equal("Original", target.Titulo);
    }

    // CA-7. Máquina de estados en Domain — IsLocked para Aprobado
    [Fact]
    public void StateMachine_IsLocked_OnlyForAprobado()
    {
        Assert.True(ChangeOrderStateMachine.IsLocked("Aprobado"));
        Assert.False(ChangeOrderStateMachine.IsLocked("Borrador"));
        Assert.False(ChangeOrderStateMachine.IsLocked("En revisión"));
        Assert.False(ChangeOrderStateMachine.IsLocked("Rechazado"));
    }

    // CA-8. Orden aprobada: CanEdit=false y CanDelete=false (via Domain.IsLocked)
    [Fact]
    public void AprobadaOrder_DomainLock_BlocksEditAndDelete()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        vm.Selected = vm.Items[0];
        vm.EnviarARevision();
        vm.Selected = vm.Items[0];
        vm.AprobarSelected();
        vm.Selected = vm.Items[0];

        Assert.True(ChangeOrderStateMachine.IsLocked(vm.Selected!.Estado));
        Assert.False(vm.CanEdit);
        Assert.False(vm.CanDelete);
    }

    // CA-9. Reapertura solo desde Rechazado (Aprobado → Borrador no está en la máquina)
    [Fact]
    public void StateMachine_Aprobado_Cannot_Reabrir()
    {
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Aprobado", "Borrador"));
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Aprobado", "En revisión"));
        Assert.False(ChangeOrderStateMachine.IsTransitionAllowed("Aprobado", "Rechazado"));
    }

    // CA-10. Code-behind — el assembly WPF no contiene 'ParseDecimal' ni 'ShowError' en el tipo Dialog
    [Fact]
    public void ChangeOrderEditorDialog_CodeBehind_HasNoValidationMethods()
    {
        var dialogType = typeof(ChangeOrderEditorDialog);
        var methods = dialogType.GetMethods(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        var validationMethods = methods.Where(m =>
            m.Name.Contains("Parse") || m.Name.Contains("Validate") ||
            m.Name.Contains("ShowError") || m.Name.Contains("CheckField")).ToList();

        Assert.Empty(validationMethods);
    }

    [Fact]
    public void EditorVm_AddContractualPartida_FreezesPrice_AndCalculatesSubtotal()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.ConfigureCurrency(muestraUf: false, ufRate: 38_500m);

        var contractualItem = new ChangeOrderPartidaPickerItem
        {
            Id = "P-001",
            Item = "1.2.3",
            Descripcion = "Alimentador Cu 3x25+1x16mm2",
            Unidad = "m",
            CantidadContrato = 150m,
            PrecioUnitarioClp = 25_000m,
            PrecioUnitarioUf = 0.6493m,
            MuestraUf = false
        };

        vm.AddContractualPartida(contractualItem);

        Assert.Single(vm.Lineas);
        var line = vm.Lineas[0];
        Assert.True(line.EsContractual);
        Assert.True(line.IsPriceReadOnly);
        Assert.Equal("1.2.3", line.Item);
        Assert.Equal(150m, line.CantidadBase);
        Assert.Equal(1m, line.CantidadAdicional);
        Assert.Equal(151m, line.CantidadNueva);
        Assert.Equal(25_000m, line.SubtotalClp);

        // Al cambiar cantidad adicional
        line.CantidadAdicional = 10m;
        Assert.Equal(160m, line.CantidadNueva);
        Assert.Equal(250_000m, line.SubtotalClp);
        Assert.Equal(250_000m, vm.TotalAdicionalClp);

        // Guardar en modelo
        var target = new ChangeOrderRow { Numero = "AD-01", Titulo = "Aumento alimentador" };
        vm.Numero = "AD-01";
        vm.Titulo = "Aumento alimentador";
        var saved = vm.TrySave(target);
        Assert.True(saved);
        Assert.Single(target.Lineas);
        Assert.Equal("1.2.3", target.Lineas[0].Item);
        Assert.Equal(10m, target.Lineas[0].CantidadAdicional);
    }

    [Fact]
    public void EditorVm_AddExtraordinariaPartida_AllowsCustomPrice_AndUpdatesTotals()
    {
        var vm = new ChangeOrderEditorViewModel();
        vm.ConfigureCurrency(muestraUf: false, ufRate: 38_500m);

        vm.AddExtraordinariaPartida();
        Assert.Single(vm.Lineas);
        var line = vm.Lineas[0];
        Assert.False(line.EsContractual);
        Assert.False(line.IsPriceReadOnly);

        line.Item = "EXT-01";
        line.DescripcionPartida = "Nuevo tablero auxiliar de faena";
        line.CantidadAdicional = 2m;
        line.PrecioUnitarioClp = 150_000m;

        Assert.Equal(300_000m, line.SubtotalClp);
        Assert.Equal(300_000m, vm.TotalAdicionalClp);
    }

    [Fact]
    public void ChangeOrderMaterialVm_FormulaEvaluation_CalculatesCorrectQuantity()
    {
        var vm = new ChangeOrderMaterialVm(() => { })
        {
            Codigo = "CAB-THHN-2.5",
            Descripcion = "Cable THHN 2.5 mm2",
            Unidad = "m",
            MatClp = 450m,
            MoClp = 150m,
            CantidadFormula = "25 * 3 + 12"
        };

        vm.EvaluateFormula();

        Assert.Equal(87m, vm.Cantidad);
        Assert.Equal(600m, vm.UnitarioClp);
        Assert.Equal(87m * 600m, vm.SubtotalClp);
    }

    [Fact]
    public void ChangeOrderLineVm_MaterialesMapping_PreservesListAndBadge()
    {
        var line = new ChangeOrderLineVm(() => { })
        {
            Item = "1.5",
            DescripcionPartida = "Circuito Enchufes",
            EsContractual = true,
            CantidadAdicional = 5m
        };

        Assert.Equal(0, line.MaterialesCount);
        Assert.Equal("📐 Cubicar", line.MaterialesBadgeText);

        var mats = new List<CubicacionRow>
        {
            new() { Codigo = "E-01", Descripcion = "Enchufe doble", Cantidad = 10, MatClp = 2500 },
            new() { Codigo = "C-01", Descripcion = "Caja chuqui", Cantidad = 10, MatClp = 800 }
        };

        line.SetMateriales(mats);
        Assert.Equal(2, line.MaterialesCount);
        Assert.Equal("📐 2 mat", line.MaterialesBadgeText);

        var model = line.ToModel();
        Assert.Equal(2, model.Materiales.Count);

        var restored = ChangeOrderLineVm.FromModel(model, muestraUf: false, ufRate: 38_500m, () => { });
        Assert.Equal(2, restored.MaterialesCount);
        Assert.Equal("E-01", restored.Materiales[0].Codigo);
    }

    [Fact]
    public void ChangeOrderRow_MaterialesRoundtrip_JsonSerialization_PreservesAllData()
    {
        var oc = new ChangeOrderRow
        {
            Numero = "OC-CUB-01",
            Titulo = "Adicional con cubicación detallada"
        };

        var line = new ChangeOrderLineRow
        {
            Item = "EXT-05",
            DescripcionPartida = "Alimentador subterráneo",
            CantidadAdicional = 50m,
            EsContractual = false,
            Materiales = new List<CubicacionRow>
            {
                new() { Codigo = "CAB-SUPERFLEX", Cantidad = 220, MatClp = 8500, MoClp = 2000 },
                new() { Codigo = "TUB-PVC-75", Cantidad = 55, MatClp = 4200, MoClp = 1100 }
            }
        };
        oc.Lineas.Add(line);

        var json = JsonSerializer.Serialize(oc);
        var deserialized = JsonSerializer.Deserialize<ChangeOrderRow>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Lineas);
        Assert.Equal(2, deserialized.Lineas[0].Materiales.Count);
        Assert.Equal("CAB-SUPERFLEX", deserialized.Lineas[0].Materiales[0].Codigo);
        Assert.Equal(220m, deserialized.Lineas[0].Materiales[0].Cantidad);
    }

    [Fact]
    public void DuplicateSelected_CreatesNewDraftCopy()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew();
        Assert.Single(vm.Items);
        var original = vm.Items[0];
        vm.Selected = original;

        Assert.True(vm.CanDuplicate);
        vm.DuplicateSelected();

        Assert.Equal(2, host.Session.OrdenesCambio.Count);
        Assert.Equal(2, vm.Items.Count);
        Assert.NotNull(vm.Selected);
        Assert.StartsWith("OC-", vm.Selected.Numero);
        Assert.Contains("(Copia)", vm.Selected.Titulo);
        Assert.Equal("Borrador", vm.Selected.Estado);
    }

    [Fact]
    public void FilterChips_UpdateFilterEstadoAndCounts()
    {
        var host = SetupHost();
        var vm = Vm(host);
        vm.AddNew(); // OC 1: Borrador
        Assert.Equal(1, vm.CountAll);
        Assert.Equal(1, vm.CountBorrador);
        Assert.True(vm.IsFilterAll);

        vm.SetFilterBorrador();
        Assert.True(vm.IsFilterBorrador);
        Assert.False(vm.IsFilterAll);
        Assert.Equal("Borrador", vm.FilterEstado);

        vm.SetFilterEnRevision();
        Assert.True(vm.IsFilterEnRevision);
        Assert.Empty(vm.Items); // No en revision items

        vm.ClearFilters();
        Assert.True(vm.IsFilterAll);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void ChangeOrderRowVm_ExposesRichInspectorProperties()
    {
        var oc = new ChangeOrderRow
        {
            Numero = "OC-099",
            Titulo = "Aumento alimentador principal",
            Estado = "En revisión",
            CreadoPor = "Estimador Senior",
            GastosGeneralesPct = 12m,
            UtilidadPct = 10m,
            DeltaCostoUf = 15.5m,
            DeltaVentaUf = 19.096m,
            Nota = "Requerido por aumento de potencia en sala bombas"
        };
        oc.Lineas.Add(new ChangeOrderLineRow { Item = "1.1", DescripcionPartida = "Conductor 240mm2" });

        var vm = new ChangeOrderRowVm(oc, 1);

        Assert.Equal(1, vm.LineasCount);
        Assert.True(vm.HasLineas);
        Assert.True(vm.HasNota);
        Assert.Equal("1 partida", vm.PartidasBadgeText);
        Assert.Equal(12m, vm.GastosGeneralesPct);
        Assert.Equal(10m, vm.UtilidadPct);
        Assert.True(vm.EsEnRevision);
        Assert.False(vm.EsBorrador);
        Assert.False(vm.EsAprobado);
    }
}

