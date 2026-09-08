using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Features.Project;

namespace QubiCostElectrical.Wpf.Shell;

partial class ShellViewModel
{
    public async Task NewProjectAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            IsProjectPickerOpen = false;

            if (projectWorkspace.HasOpenProject)
            {
                if (!await CloseProjectAsync())
                    return;
            }
            else if (currentPageVm is IPageWithPendingChanges pending && pending.HasPendingChanges)
            {
                if (!ShowPendingChangesOnNewDialog())
                    return;
                pending.TryDiscardChanges();
            }

            var dialog = new NewProjectDialog();
            dialog.Owner = Application.Current?.MainWindow;
            if (dialog.ShowDialog() != true)
                return;

            var fullPath = dialog.FullPath;

            if (File.Exists(fullPath))
            {
                if (!WpfDialogService.Confirm("Archivo existente",
                        $"Ya existe un archivo en:\n{fullPath}\n\n¿Desea reemplazarlo?", isWarning: true))
                    return;
            }

            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var userName = session.ProjectInfo.UsuarioNombre;
                var userRole = session.ProjectInfo.UsuarioRol;

                projectLifecycle.PrepareWorkspaceForNewOrOpen();
                session.NewProject(userName, userRole);
                session.InitializeProjectParameters(
                    dialog.ProjectName,
                    dialog.Client,
                    dialog.Location,
                    dialog.Currency,
                    dialog.BuildClosingPolicy(),
                    dialog.SelectedTemplate?.CreateInstanceRows());

                await projectWorkspace.SaveAsAsync(fullPath);
                projectLifecycle.AfterProjectMaterialized(
                    addRecent: () => recentProjects.AddProject(session.CurrentProjectPath));

                SyncReadOnlyFromSession();
                RefreshRecentProjects();
                RefreshChrome();
                RefreshCurrentPage();
                RequestNavigate(ShellRoute.Dashboard, force: true);
            }
            catch (Exception ex)
            {
                AppLog.Error("ProjectWorkspace", "Falló la creación de un proyecto nuevo", ex);
                WpfDialogService.Error("Nuevo proyecto", OperationFailureMessages.ToUserMessage(ex));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> CloseProjectAsync()
    {
        if (!projectWorkspace.HasOpenProject)
            return true;

        bool hasDirtyState = session.IsDirty ||
            (currentPageVm is IPageWithPendingChanges p && p.HasPendingChanges);

        if (hasDirtyState)
        {
            // Flujo profesional: al cerrar se guarda en silencio (sin Guardar/Descartar).
            // Descartar sigue disponible vía Undo mientras el proyecto esté abierto.
            if (!TryApplyCurrentPageChanges())
            {
                WpfDialogService.Warning(
                    "Cerrar proyecto",
                    "No se pudieron aplicar los cambios de la pantalla actual. Corrija el error antes de cerrar.");
                return false;
            }

            if (session.IsDirty && !session.IsReadOnly)
            {
                try
                {
                    await projectLifecycle.PersistOpenProjectAsync();
                }
                catch (Exception ex)
                {
                    AppLog.Error("ProjectWorkspace", "Falló el guardado al cerrar el proyecto", ex);
                    WpfDialogService.Error("Guardar proyecto", $"{OperationFailureMessages.ToUserMessage(ex)}\n\nEl cierre fue cancelado.");
                    return false;
                }
            }
        }

        projectLifecycle.TearDownOpenProject("Cerrar");
        SyncReadOnlyFromSession();

        PostCloseNavigate();
        RefreshChrome();
        OnPropertyChanged(nameof(HasOpenProject));
        return true;
    }

    /// <summary>
    /// Antes de apagar la app (X / Alt+F4 / Shutdown): aplica página, pregunta Guardar/Descartar/Cancelar
    /// si hay cambios, y libera el lock. En LockLostPending: no escribe .qce; preserva .autosave.
    /// </summary>
    public async Task<bool> PrepareForAppExitAsync()
    {
        if (!projectWorkspace.HasOpenProject)
            return true;

        if (!TryApplyCurrentPageChanges())
        {
            WpfDialogService.Warning(
                "Salir",
                "No se pudieron aplicar los cambios de la pantalla actual. Corrija el error antes de cerrar la aplicación.");
            return false;
        }

        bool hasDirtyState = session.IsDirty ||
            (currentPageVm is IPageWithPendingChanges p && p.HasPendingChanges);

        if (session.IsDirty && session.IsReadOnly)
        {
            // LockLostPending (u otro RO): no SaveAsync al .qce compartido.
            try
            {
                await autoSave.FlushSidecarAsync();
            }
            catch (Exception ex)
            {
                AppLog.Warning("ProjectWorkspace", "No se pudo escribir .autosave al salir con bloqueo perdido", ex);
            }

            projectLifecycle.ReleaseLockForAppExit();
            return true;
        }

        if (hasDirtyState && !session.IsReadOnly)
        {
            var choice = ShowExitAppDialog();
            if (choice == ThreeWayResult.Cancel)
                return false;

            if (choice == ThreeWayResult.Discard)
            {
                autoSave.CancelPendingPersist();
                AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
                projectLifecycle.ReleaseLockForAppExit();
                return true;
            }

            // Guardar
            try
            {
                await autoSave.FlushPersistAsync();
                if (session.IsDirty)
                {
                    await projectWorkspace.SaveAsync();
                    AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
                    autoSave.MarkSaved();
                }
            }
            catch (ProjectLockLostException ex)
            {
                AppLog.Warning("ProjectWorkspace", ex.Message);
                session.SetLockReadOnly(true, "Otro usuario tomó el control de edición. Solo lectura.");
                autoSave.CancelPendingPersist();
                try { await autoSave.FlushSidecarAsync(); } catch { /* best effort */ }
                projectLifecycle.ReleaseLockForAppExit();
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("ProjectWorkspace", "Falló el guardado al salir de la aplicación", ex);
                WpfDialogService.Error("Guardar proyecto", $"{OperationFailureMessages.ToUserMessage(ex)}\n\nLa salida fue cancelada.");
                return false;
            }
        }

        projectLifecycle.ReleaseLockForAppExit();
        return true;
    }

    public async Task OpenProjectAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            IsProjectPickerOpen = false;

            if (projectWorkspace.HasOpenProject)
            {
                if (!await CloseProjectAsync())
                    return;
            }
            else if (currentPageVm is IPageWithPendingChanges pending && pending.HasPendingChanges)
            {
                if (!ShowPendingChangesOnOpenDialog())
                    return;
                pending.TryDiscardChanges();
            }

            var path = WpfDialogService.PromptOpenFile();
            if (path == null)
                return;

            await OpenProjectFromPathAsync(path);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OpenRecentProjectAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            IsProjectPickerOpen = false;

            if (!File.Exists(path))
            {
                recentProjects.RemoveProject(path);
                RefreshRecentProjects();
                ShowMissingFileWarning(path);
                return;
            }

            if (projectWorkspace.HasOpenProject)
            {
                if (!await CloseProjectAsync())
                    return;
            }
            else if (currentPageVm is IPageWithPendingChanges pending && pending.HasPendingChanges)
            {
                if (!ShowPendingChangesOnOpenDialog())
                    return;
                pending.TryDiscardChanges();
            }

            await OpenProjectFromPathAsync(path);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RemoveRecentProject(string path)
    {
        recentProjects.RemoveProject(path);
        RefreshRecentProjects();
    }

    private async Task OpenProjectFromPathAsync(string path)
    {
        var routeBeforeOpen = CurrentRoute;
        var user = CollaborationSessionBridge.CurrentUserContext();
        audit.SetUser(user);

        var inspection = projectLock.InspectLock(path);
        bool acquired = false;
        if (inspection.IsWritableCandidate)
            acquired = projectLock.TryAcquireLock(path, user);

        if (!acquired && inspection.State is not ProjectLockState.NotLocked)
        {
            var resolution = WpfDialogService.ResolveProjectLock(inspection);
            switch (resolution)
            {
                case ProjectLockResolution.Cancel:
                    return;
                case ProjectLockResolution.TakeOver:
                    acquired = projectLock.ForceAcquireExpiredLock(path, user);
                    if (!acquired)
                    {
                        WpfDialogService.Warning("Proyecto bloqueado", "No se pudo tomar el control de edición. Intente de nuevo o abra en solo lectura.");
                        return;
                    }
                    break;
                case ProjectLockResolution.ReadOnly:
                    break;
            }
        }

        string lockReason = acquired
            ? ""
            : inspection.HolderSummary + " · Modo consulta (solo lectura).";
        session.SetLockReadOnly(!acquired, acquired ? null : lockReason);
        SyncReadOnlyFromSession();

        try
        {
            await projectWorkspace.OpenAsync(path);
            budgetCalculation.ResetIncrementalCache();
            ClearPageCache();
            TryApplyCachedUfValue();
            undo.Clear();
            projectEdit.Discard();
            documents.DiscardChanges();
            if (acquired)
            {
                audit.Enable();
                audit.RecordLifecycle("Abrir", "Edición exclusiva adquirida");
            }
            else
            {
                audit.Disable();
            }
            autoSave.Start();

            RoleSessionSync.Apply(session, permissions.CurrentRole);
            SyncReadOnlyFromSession();
            await TryOfferAutoSaveRestoreAsync(path);
            recentProjects.AddProject(session.CurrentProjectPath);
            RefreshRecentProjects();
            RefreshChrome();
            RefreshCurrentPage();
            var nextRoute = routeBeforeOpen == ShellRoute.Dashboard && session.Itemizado.Count > 0
                ? ShellRoute.Itemized
                : routeBeforeOpen;
            RequestNavigate(nextRoute, force: true);
            OnPropertyChanged(nameof(HasOpenProject));
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", $"Falló la apertura del proyecto '{path}'", ex);
            WpfDialogService.Error("Abrir proyecto", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    /// <summary>
    /// Si existe un .autosave más reciente que el .qce, ofrece restaurarlo a la sesión
    /// y opcionalmente guardarlo en el archivo del proyecto.
    /// </summary>
    private async Task TryOfferAutoSaveRestoreAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || session.IsReadOnly)
            return;
        if (!AutoSaveService.IsAutoSaveNewer(path))
            return;

        var ts = AutoSaveService.GetAutoSaveTimestamp(path);
        var when = ts.HasValue
            ? ts.Value.ToLocalTime().ToString("g")
            : "fecha desconocida";

        if (!WpfDialogService.Confirm(
                "Recuperar autoguardado",
                $"Hay un autoguardado más reciente que el archivo del proyecto ({when}).\n\n¿Desea restaurar esos cambios en la sesión?",
                isWarning: true))
            return;

        var data = AutoSaveService.TryLoadAutoSave(path);
        if (data == null)
        {
            WpfDialogService.Warning("Autoguardado", "No se pudo leer el archivo de autoguardado.");
            return;
        }

        session.ApplySnapshot(data, path, markDirty: true);
        ClearPageCache();
        RefreshCurrentPage();
        RefreshChrome();

        if (WpfDialogService.Confirm(
                "Guardar recuperación",
                "Los cambios del autoguardado están en memoria. ¿Desea guardarlos ahora en el archivo del proyecto?"))
        {
            try
            {
                await projectWorkspace.SaveAsync();
                AutoSaveService.DeleteAutoSave(path);
                RefreshChrome();
            }
            catch (Exception ex)
            {
                AppLog.Error("ProjectWorkspace", "Falló guardar tras restaurar autoguardado", ex);
                WpfDialogService.Error("Guardar recuperación", OperationFailureMessages.ToUserMessage(ex));
            }
        }
    }

    public async Task SaveProjectAsync()
    {
        if (!projectWorkspace.HasOpenProject)
        {
            WpfDialogService.Info("Guardar", "No hay proyecto abierto.");
            return;
        }
        if (IsReadOnly)
        {
            WpfDialogService.Warning("Guardar", "El proyecto está en modo solo lectura. Abra una copia escribible para guardar cambios.");
            return;
        }

        try
        {
            if (!TryApplyCurrentPageChanges())
                return;

            var user = CollaborationSessionBridge.CurrentUserContext();
            session.ProjectInfo.UsuarioNombre = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
            session.ProjectInfo.UsuarioRol = permissions.RoleDisplayName;

            await projectWorkspace.SaveAsync();
            AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
            RefreshChrome();
            ShellNotificationService.Instance.ShowSuccess("Proyecto guardado", $"Los cambios se guardaron correctamente ({AppClock.Now:HH:mm:ss}).");
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", "Falló el guardado del proyecto", ex);
            WpfDialogService.Error("Guardar proyecto", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task SaveProjectAsAsync()
    {
        if (!projectWorkspace.HasOpenProject)
        {
            await OpenProjectAsync();
            if (!projectWorkspace.HasOpenProject)
                return;
        }
        if (IsReadOnly)
        {
            WpfDialogService.Warning("Guardar como", "El proyecto está en modo solo lectura. Abra una copia escribible para usar Guardar como.");
            return;
        }

        string defaultName = ProjectTextValidator.MakeSafeFileName(session.ProjectInfo.Nombre) + ProjectFileTypes.CurrentExtension;
        var path = WpfDialogService.PromptSaveFile(defaultName);
        if (path == null)
            return;

        var targetLock = projectLock.InspectLock(path);
        if (targetLock.State is ProjectLockState.LocalMachineAlive or ProjectLockState.RemoteRecent or ProjectLockState.Corrupt)
        {
            WpfDialogService.Warning("Guardar como", $"El destino ya está bloqueado o no es seguro ({targetLock.HolderSummary}).");
            return;
        }

        try
        {
            if (!TryApplyCurrentPageChanges())
                return;

            await projectWorkspace.SaveAsAsync(path);
            projectLock.ReleaseLock();
            var user = CollaborationSessionBridge.CurrentUserContext();
            if (projectLock.TryAcquireLock(session.CurrentProjectPath, user))
            {
                session.SetLockReadOnly(false);
                audit.RecordLifecycle("GuardarComo", session.CurrentProjectPath);
            }
            SyncReadOnlyFromSession();
            autoSave.Start();
            recentProjects.AddProject(session.CurrentProjectPath);
            RefreshRecentProjects();
            RefreshChrome();
            RefreshCurrentPage();
            WpfDialogService.Info("Guardar como", $"Copia guardada en:{Environment.NewLine}{session.CurrentProjectPath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", "Falló 'Guardar como'", ex);
            WpfDialogService.Error("Guardar como", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void OnSaved()
    {
        undo.OnSaved();
        AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
        Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
    }

    private void TryApplyCachedUfValue()
    {
        if (session.ValorUfClp > 0m) return;
        var cached = EconomicIndicatorCache.TryRead();
        session.ValorUfClp = cached != null && cached.UfClp > 0m ? cached.UfClp : PriceConversion.DefaultValorUfClp;
        session.MarkDirty();
    }

    private void RefreshRecentProjects()
    {
        recentProjectItems.Clear();
        foreach (var item in recentProjects.GetProjects())
            recentProjectItems.Add(item);
    }
}
