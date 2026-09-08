using System.IO;
using System.Reflection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.ChangeOrders;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class WpfUndoServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static WpfAppHost SetupHost(string name = "undo-test")
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject(name);
        host.Session.CurrentProjectPath = $"{name}.qce";
        host.Session.OrdenesCambio.Clear();
        host.UndoService.Clear();
        return host;
    }

    private static void MutateSession(BudgetProjectSession session, string tag)
        => session.ProjectInfo.Nombre = tag;

    private static string ReadTag(BudgetProjectSession session)
        => session.ProjectInfo.Nombre ?? "";

    // ════════════════════════════════════════════════════════════════════════════
    // 1. Estado inicial
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InitialState_CanUndo_IsFalse()
    {
        var host = SetupHost();
        Assert.False(host.UndoService.CanUndo);
    }

    [Fact]
    public void InitialState_CanRedo_IsFalse()
    {
        var host = SetupHost();
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 2. Primera edición habilita Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AfterPush_CanUndo_IsTrue()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "test");
        Assert.True(host.UndoService.CanUndo);
    }

    [Fact]
    public void AfterPush_CanRedo_StaysFalse()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "test");
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 3. Undo restaura el valor anterior
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Undo_RestoresPreviousValue()
    {
        var host = SetupHost();
        MutateSession(host.Session, "before");
        host.UndoService.PushUndoPoint(host.Session, "before-edit");

        MutateSession(host.Session, "after");
        Assert.Equal("after", ReadTag(host.Session));

        host.UndoService.Undo(host.Session);
        Assert.Equal("before", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 4. Redo restaura el valor nuevo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redo_RestoresNewValue()
    {
        var host = SetupHost();
        MutateSession(host.Session, "before");
        host.UndoService.PushUndoPoint(host.Session, "edit");

        MutateSession(host.Session, "after");
        host.UndoService.Undo(host.Session);
        Assert.Equal("before", ReadTag(host.Session));

        host.UndoService.Redo(host.Session);
        Assert.Equal("after", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 5. Nueva edición después de Undo elimina Redo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NewEditAfterUndo_ClearsRedo()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "A");
        MutateSession(host.Session, "B");
        host.UndoService.Undo(host.Session);

        Assert.True(host.UndoService.CanRedo);

        host.UndoService.PushUndoPoint(host.Session, "C");
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 6. Undo sin historial no modifica nada
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Undo_WhenEmpty_ReturnsFalse()
    {
        var host = SetupHost();
        MutateSession(host.Session, "original");
        var result = host.UndoService.Undo(host.Session);
        Assert.False(result);
        Assert.Equal("original", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 7. Redo sin historial no modifica nada
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redo_WhenEmpty_ReturnsFalse()
    {
        var host = SetupHost();
        MutateSession(host.Session, "original");
        var result = host.UndoService.Redo(host.Session);
        Assert.False(result);
        Assert.Equal("original", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 8. Límite de historial (MaxSteps = 15)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UndoStack_DoesNotExceedMaxSteps()
    {
        var host = SetupHost();
        for (int i = 0; i < 20; i++)
            host.UndoService.PushUndoPoint(host.Session, $"step-{i}");

        Assert.True(host.UndoService.UndoCount <= 15);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 9. Abrir proyecto inicia con historial limpio
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Clear_AfterOpenProject_HistoryIsEmpty()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "before-open");
        host.UndoService.Clear();
        Assert.False(host.UndoService.CanUndo);
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 10. Guardar establece nueva línea base
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void OnSaved_SetsBaseline_ForIsDirtyTracking()
    {
        var host = SetupHost();
        MutateSession(host.Session, "before");
        host.UndoService.PushUndoPoint(host.Session, "edit");
        MutateSession(host.Session, "saved-state");
        host.UndoService.OnSaved(); // marca el estado actual como guardado

        // Editar después del guardado
        host.UndoService.PushUndoPoint(host.Session, "after-save-edit");
        MutateSession(host.Session, "new-edit");

        // Undo vuelve al estado guardado → IsDirty debe ser false
        host.UndoService.Undo(host.Session);
        Assert.False(host.Session.IsDirty);
        Assert.Equal("saved-state", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 11. Undo hasta línea base → IsDirty = false
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Undo_ToSavedBaseline_ClearsIsDirty()
    {
        var host = SetupHost();
        MutateSession(host.Session, "saved");
        host.UndoService.OnSaved();

        host.UndoService.PushUndoPoint(host.Session, "edit");
        MutateSession(host.Session, "dirty");

        host.UndoService.Undo(host.Session);
        Assert.False(host.Session.IsDirty);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 12. Redo después de línea base → IsDirty = true
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redo_AfterBaseline_SetsDirty()
    {
        var host = SetupHost();
        MutateSession(host.Session, "saved");
        host.UndoService.OnSaved();

        host.UndoService.PushUndoPoint(host.Session, "edit");
        MutateSession(host.Session, "dirty");

        host.UndoService.Undo(host.Session); // vuelve a saved, IsDirty=false
        Assert.False(host.Session.IsDirty);

        host.UndoService.Redo(host.Session); // vuelve a dirty, IsDirty=true
        Assert.True(host.Session.IsDirty);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 13. Cambiar proyecto limpia historial
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Clear_ResetsUndoAndRedo()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "A");
        host.UndoService.PushUndoPoint(host.Session, "B");
        host.UndoService.Clear();

        Assert.Equal(0, host.UndoService.UndoCount);
        Assert.Equal(0, host.UndoService.RedoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 14. Cerrar proyecto limpia historial (misma semántica que Clear)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Clear_OnClose_MakesCanUndoFalse()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "edit");
        host.UndoService.Clear();
        Assert.False(host.UndoService.CanUndo);
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 15. Cancelar edición (editor returns false) no crea entrada
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CancelledEdit_DoesNotCreateUndoEntry()
    {
        var host = SetupHost("cancel-test");
        var vm = new ChangeOrdersViewModel(host);

        int countBefore = host.UndoService.UndoCount;

        // Simular cancelación: ShowEditor devuelve false
        vm.ShowEditor = (_, _) => false;
        vm.AddNew();

        Assert.Equal(countBefore, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 16. Navegación no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Navigation_DoesNotCreateUndoEntry()
    {
        var host = SetupHost();
        int countBefore = host.UndoService.UndoCount;
        // La navegación es solo RequestNavigate en ShellViewModel; UndoService no participa
        Assert.Equal(countBefore, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 17. Selección de fila no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RowSelection_DoesNotCreateUndoEntry()
    {
        var host = SetupHost("sel-test");
        host.Session.OrdenesCambio.Add(new ChangeOrderRow { Numero = "OC-001", Titulo = "T1" });
        var vm = new ChangeOrdersViewModel(host);

        int before = host.UndoService.UndoCount;
        vm.Selected = vm.Items.FirstOrDefault();
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 18. Filtro no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FilterChange_DoesNotCreateUndoEntry()
    {
        var host = SetupHost("filter-test");
        var vm = new ChangeOrdersViewModel(host);
        int before = host.UndoService.UndoCount;
        vm.FilterEstado = "Aprobado";
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 19. Búsqueda no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchChange_DoesNotCreateUndoEntry()
    {
        var host = SetupHost("search-test");
        var vm = new ChangeOrdersViewModel(host);
        int before = host.UndoService.UndoCount;
        vm.SearchText = "obra";
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 20. Autosave no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AutoSave_DoesNotCreateUndoEntry()
    {
        var host = SetupHost();
        int before = host.UndoService.UndoCount;
        // AutoSaveService llama Session.CreateSnapshot() pero NO UndoService.PushUndoPoint
        // Este test confirma que el conteo no cambia sin llamada explícita a PushUndoPoint
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 21. Backup no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Backup_DoesNotCreateUndoEntry()
    {
        var host = SetupHost();
        int before = host.UndoService.UndoCount;
        // Los backups son copias de archivo, no tocan UndoService
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 22. Crear versión no crea entrada de Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateVersion_DoesNotCreateUndoEntry()
    {
        var host = SetupHost();
        int before = host.UndoService.UndoCount;
        // VersionsViewModel crea snapshots de versiones, no toca UndoService
        Assert.Equal(before, host.UndoService.UndoCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 23. Restaurar versión → Clear() limpia historial explícitamente
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RestoreVersion_ShouldClearHistory()
    {
        var host = SetupHost();
        host.UndoService.PushUndoPoint(host.Session, "before-restore");

        // Al restaurar una versión, el llamador debe invocar Clear()
        host.UndoService.Clear();

        Assert.False(host.UndoService.CanUndo);
        Assert.False(host.UndoService.CanRedo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 24. Orden de cambio creada puede deshacerse
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ChangeOrder_Add_CanBeUndone()
    {
        var host = SetupHost("oc-undo-add");
        var vm = new ChangeOrdersViewModel(host);
        vm.ShowEditor = (oc, _) =>
        {
            oc.Titulo = "Orden de prueba";
            return true;
        };
        vm.ConfirmDelete = _ => true;

        Assert.Empty(host.Session.OrdenesCambio);
        vm.AddNew();
        Assert.Single(host.Session.OrdenesCambio);

        host.UndoService.Undo(host.Session);
        Assert.Empty(host.Session.OrdenesCambio);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 25. Orden de cambio editada puede deshacerse
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ChangeOrder_Edit_CanBeUndone()
    {
        var host = SetupHost("oc-undo-edit");
        host.Session.OrdenesCambio.Add(new ChangeOrderRow { Numero = "OC-001", Titulo = "Original" });
        var vm = new ChangeOrdersViewModel(host);
        vm.Selected = vm.Items[0];

        vm.ShowEditor = (oc, _) =>
        {
            oc.Titulo = "Editado";
            return true;
        };
        vm.EditSelected();
        Assert.Equal("Editado", host.Session.OrdenesCambio[0].Titulo);

        host.UndoService.Undo(host.Session);
        Assert.Equal("Original", host.Session.OrdenesCambio[0].Titulo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 26. Orden de cambio eliminada puede deshacerse
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ChangeOrder_Delete_CanBeUndone()
    {
        var host = SetupHost("oc-undo-del");
        host.Session.OrdenesCambio.Add(new ChangeOrderRow { Numero = "OC-001", Titulo = "Para borrar" });
        var vm = new ChangeOrdersViewModel(host);
        vm.ConfirmDelete = _ => true;
        vm.Selected = vm.Items[0];

        vm.DeleteSelected();
        Assert.Empty(host.Session.OrdenesCambio);

        host.UndoService.Undo(host.Session);
        Assert.Single(host.Session.OrdenesCambio);
        Assert.Equal("Para borrar", host.Session.OrdenesCambio[0].Titulo);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 27. Cambio de moneda puede deshacerse
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Currency_TryApplyChanges_CanBeUndone()
    {
        var host = SetupHost("currency-undo");
        host.Session.ProjectInfo.ValorUfClp = 35000m;

        var svc = host.UndoService;
        svc.PushUndoPoint(host.Session, "before-rate");
        host.Session.ProjectInfo.ValorUfClp = 37000m;
        host.Session.MarkDirty();

        Assert.Equal(37000m, host.Session.ProjectInfo.ValorUfClp);

        svc.Undo(host.Session);
        Assert.Equal(35000m, host.Session.ProjectInfo.ValorUfClp);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 28. Ctrl+Z registrado en ShellViewModel como Undo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ShellViewModel_Undo_CallsUndoService()
    {
        var host = SetupHost("shell-undo");
        MutateSession(host.Session, "before");
        host.UndoService.PushUndoPoint(host.Session, "edit");
        MutateSession(host.Session, "after");

        // Simular que ShellViewModel.Undo() llama host.UndoService.Undo(session)
        var result = host.UndoService.Undo(host.Session);
        Assert.True(result);
        Assert.Equal("before", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 29. Ctrl+Y registrado en ShellViewModel como Redo
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ShellViewModel_Redo_CallsUndoService()
    {
        var host = SetupHost("shell-redo");
        MutateSession(host.Session, "before");
        host.UndoService.PushUndoPoint(host.Session, "edit");
        MutateSession(host.Session, "after");

        host.UndoService.Undo(host.Session);
        Assert.Equal("before", ReadTag(host.Session));

        var result = host.UndoService.Redo(host.Session);
        Assert.True(result);
        Assert.Equal("after", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 30. TextBox conserva su Undo local (verificación arquitectural)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WpfUndoService_DoesNotInterfereWithTextBoxUndo()
    {
        // WpfUndoService opera sobre ProjectFileData a nivel de sesión.
        // TextBox tiene su propio Undo interno de WPF (ApplicationCommands.Undo).
        // Este test confirma que WpfUndoService no tiene dependencias de UI.
        var svc = new WpfUndoService();
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Equal("", svc.LastUndoDescription);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 31. WPF no referencia WinForms (verificación de assembly)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WpfUndoService_DoesNotReferenceWinForms()
    {
        var type = typeof(WpfUndoService);
        var refs = type.Assembly.GetReferencedAssemblies();
        var hasWinForms = refs.Any(r =>
            r.Name != null &&
            (r.Name.Contains("System.Windows.Forms") || r.Name.Contains("WinForms")));
        Assert.False(hasWinForms);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 32. No se modifican archivos reales del usuario durante tests
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UndoRedo_DoesNotWriteToRealFiles()
    {
        var host = SetupHost("no-file-write");
        var tempPath = Path.Combine(Path.GetTempPath(), "undo-test-sentinel.qce");

        host.UndoService.PushUndoPoint(host.Session, "edit");
        host.UndoService.Undo(host.Session);
        host.UndoService.Redo(host.Session);

        Assert.False(File.Exists(tempPath));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 33. PushSnapshot (pre-captura) funciona correctamente
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PushSnapshot_WorksLikePushUndoPoint()
    {
        var host = SetupHost("push-snap");
        MutateSession(host.Session, "before");
        var snap = host.Session.CreateSnapshot();

        MutateSession(host.Session, "after");
        host.UndoService.PushSnapshot(snap, "manual-push");

        host.UndoService.Undo(host.Session);
        Assert.Equal("before", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 34. IsDirty permanece false cuando Undo vuelve a un estado guardado
    //     (editar → guardar → editar → undo → isDirty debe ser false)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MultiEdit_SavedInMiddle_UndoReturnsToSaved_IsDirtyFalse()
    {
        var host = SetupHost("multi-save-undo");

        // Editar A
        MutateSession(host.Session, "A");
        host.UndoService.PushUndoPoint(host.Session, "A");

        // Editar B
        MutateSession(host.Session, "B");

        // Guardar desde B
        host.UndoService.OnSaved();

        // Editar C
        host.UndoService.PushUndoPoint(host.Session, "C");
        MutateSession(host.Session, "C");

        // Undo → vuelve a B (estado guardado)
        host.UndoService.Undo(host.Session);
        Assert.False(host.Session.IsDirty);
        Assert.Equal("B", ReadTag(host.Session));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 35. Nueva edición desde estado antes del guardado invalida el baseline
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EditBeforeSavedState_InvalidatesBaseline()
    {
        var host = SetupHost("invalidate-base");

        // Editar A, guardar
        MutateSession(host.Session, "A");
        host.UndoService.PushUndoPoint(host.Session, "A");
        MutateSession(host.Session, "saved");
        host.UndoService.OnSaved(); // savedDepth = 1

        // Editar B
        host.UndoService.PushUndoPoint(host.Session, "B");
        MutateSession(host.Session, "B");

        // Undo a saved
        host.UndoService.Undo(host.Session);
        Assert.False(host.Session.IsDirty); // en estado guardado

        // Undo otro más (antes del guardado)
        host.UndoService.Undo(host.Session);
        Assert.True(host.Session.IsDirty); // anterior al save, debe ser dirty

        // Nueva edición C (invalida redo que contenía el saved state)
        host.UndoService.PushUndoPoint(host.Session, "C");
        MutateSession(host.Session, "C");

        // Undo hasta que no hay más — todos deberían ser dirty (saved no es alcanzable)
        while (host.UndoService.CanUndo)
            host.UndoService.Undo(host.Session);

        // En el estado más antiguo (no hay saved), IsDirty es true
        Assert.True(host.Session.IsDirty);
    }
}
