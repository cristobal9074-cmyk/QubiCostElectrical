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

            if (host.ProjectWorkspace.HasOpenProject)
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

                var session = host.Session;
                var userName = session.ProjectInfo.UsuarioNombre;
                var userRole = session.ProjectInfo.UsuarioRol;

                host.UndoService.Clear();
                host.Services.BudgetCalculation.ResetIncrementalCache();
                ClearPageCache();
                session.NewProject(userName, userRole);
                session.ProjectInfo.Nombre = dialog.ProjectName;
                session.ProjectInfo.Cliente = dialog.Client;
                session.ProjectInfo.Ubicacion = dialog.Location;
                session.ProjectInfo.MonedaPresupuesto = dialog.Currency;

                await host.ProjectWorkspace.SaveAsAsync(fullPath);
                host.InitializePlanningRepositories(fullPath);
                host.ProjectEdit.Discard();
                host.Documents.DiscardChanges();
                host.ProjectLock.ReleaseLock();
                host.Audit.SetUser(CollaborationSessionBridge.CurrentUserContext());
                host.Audit.Enable();
                if (host.ProjectLock.TryAcquireLock(host.Session.CurrentProjectPath, CollaborationSessionBridge.CurrentUserContext()))
                    host.Audit.RecordLifecycle("BloqueoAdquirido", "Nuevo proyecto");
                autoSave.Start();

                RoleSessionSync.Apply(host.Session, host.Permissions.CurrentRole);
                SyncReadOnlyFromSession();
                recentProjects.AddProject(host.Session.CurrentProjectPath);
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
        if (!host.ProjectWorkspace.HasOpenProject)
            return true;

        bool hasDirtyState = host.Session.IsDirty ||
            (currentPageVm is IPageWithPendingChanges p && p.HasPendingChanges);

        if (hasDirtyState)
        {
            var choice = ShowCloseProjectDialog();
            switch (choice)
            {
                case ThreeWayResult.Save:
                    if (!TryApplyCurrentPageChanges())
                        return false;
                    try
                    {
                        await host.ProjectWorkspace.SaveAsync();
                        AutoSaveService.DeleteAutoSave(host.Session.CurrentProjectPath);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("ProjectWorkspace", "Falló el guardado al cerrar el proyecto", ex);
                        WpfDialogService.Error("Guardar proyecto", $"{OperationFailureMessages.ToUserMessage(ex)}\n\nEl cierre fue cancelado.");
                        return false;
                    }
                    break;

                case ThreeWayResult.Discard:
                    if (currentPageVm is IPageWithPendingChanges pending)
                        pending.TryDiscardChanges();
                    break;

                case ThreeWayResult.Cancel:
                    return false;
            }
        }

        autoSave.Stop();
        host.Audit.RecordLifecycle("Cerrar", host.Session.CurrentProjectPath);
        host.Audit.Disable();
        host.ProjectLock.ReleaseLock();
        host.Session.SetLockReadOnly(false);

        host.UndoService.Clear();
        ClearPageCache();
        host.Session.NewProject(Environment.UserName);
        host.ProjectEdit.Discard();
        host.Documents.DiscardChanges();
        RoleSessionSync.Apply(host.Session, host.Permissions.CurrentRole);
        SyncReadOnlyFromSession();

        PostCloseNavigate();
        RefreshChrome();
        OnPropertyChanged(nameof(HasOpenProject));
        return true;
    }

    public async Task OpenProjectAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            IsProjectPickerOpen = false;

            if (host.ProjectWorkspace.HasOpenProject)
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

            if (host.ProjectWorkspace.HasOpenProject)
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
        host.Audit.SetUser(user);

        var inspection = host.ProjectLock.InspectLock(path);
        bool acquired = false;
        if (inspection.IsWritableCandidate)
            acquired = host.ProjectLock.TryAcquireLock(path, user);

        if (!acquired && inspection.State is not ProjectLockState.NotLocked)
        {
            var resolution = WpfDialogService.ResolveProjectLock(inspection);
            switch (resolution)
            {
                case ProjectLockResolution.Cancel:
                    return;
                case ProjectLockResolution.TakeOver:
                    acquired = host.ProjectLock.ForceAcquireExpiredLock(path, user);
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
        host.Session.SetLockReadOnly(!acquired, acquired ? null : lockReason);
        SyncReadOnlyFromSession();

        try
        {
            await host.ProjectWorkspace.OpenAsync(path);
            host.InitializePlanningRepositories(path);
            host.Services.BudgetCalculation.ResetIncrementalCache();
            ClearPageCache();
            TryApplyCachedUfValue();
            host.UndoService.Clear();
            host.ProjectEdit.Discard();
            host.Documents.DiscardChanges();
            if (acquired)
            {
                host.Audit.Enable();
                host.Audit.RecordLifecycle("Abrir", "Edición exclusiva adquirida");
            }
            else
            {
                host.Audit.Disable();
            }
            autoSave.Start();

            RoleSessionSync.Apply(host.Session, host.Permissions.CurrentRole);
            SyncReadOnlyFromSession();
            recentProjects.AddProject(host.Session.CurrentProjectPath);
            RefreshRecentProjects();
            RefreshChrome();
            RefreshCurrentPage();
            var nextRoute = routeBeforeOpen == ShellRoute.Dashboard && host.Session.Itemizado.Count > 0
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

    public async Task SaveProjectAsync()
    {
        if (!host.ProjectWorkspace.HasOpenProject)
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
            host.Session.ProjectInfo.UsuarioNombre = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
            host.Session.ProjectInfo.UsuarioRol = host.Permissions.RoleDisplayName;

            await host.ProjectWorkspace.SaveAsync();
            AutoSaveService.DeleteAutoSave(host.Session.CurrentProjectPath);
            RefreshChrome();
            WpfDialogService.Info("Guardar", "Proyecto guardado correctamente.");
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", "Falló el guardado del proyecto", ex);
            WpfDialogService.Error("Guardar proyecto", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task SaveProjectAsAsync()
    {
        if (!host.ProjectWorkspace.HasOpenProject)
        {
            await OpenProjectAsync();
            if (!host.ProjectWorkspace.HasOpenProject)
                return;
        }
        if (IsReadOnly)
        {
            WpfDialogService.Warning("Guardar como", "El proyecto está en modo solo lectura. Abra una copia escribible para usar Guardar como.");
            return;
        }

        string defaultName = ProjectTextValidator.MakeSafeFileName(host.Session.ProjectInfo.Nombre) + ProjectFileTypes.CurrentExtension;
        var path = WpfDialogService.PromptSaveFile(defaultName);
        if (path == null)
            return;

        var targetLock = host.ProjectLock.InspectLock(path);
        if (targetLock.State is ProjectLockState.LocalMachineAlive or ProjectLockState.RemoteRecent or ProjectLockState.Corrupt)
        {
            WpfDialogService.Warning("Guardar como", $"El destino ya está bloqueado o no es seguro ({targetLock.HolderSummary}).");
            return;
        }

        try
        {
            if (!TryApplyCurrentPageChanges())
                return;

            await host.ProjectWorkspace.SaveAsAsync(path);
            host.ProjectLock.ReleaseLock();
            var user = CollaborationSessionBridge.CurrentUserContext();
            if (host.ProjectLock.TryAcquireLock(host.Session.CurrentProjectPath, user))
            {
                host.Session.SetLockReadOnly(false);
                host.Audit.RecordLifecycle("GuardarComo", host.Session.CurrentProjectPath);
            }
            SyncReadOnlyFromSession();
            autoSave.Start();
            recentProjects.AddProject(host.Session.CurrentProjectPath);
            RefreshRecentProjects();
            RefreshChrome();
            RefreshCurrentPage();
            WpfDialogService.Info("Guardar como", $"Copia guardada en:{Environment.NewLine}{host.Session.CurrentProjectPath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", "Falló 'Guardar como'", ex);
            WpfDialogService.Error("Guardar como", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void OnSaved()
    {
        host.UndoService.OnSaved();
        AutoSaveService.DeleteAutoSave(host.Session.CurrentProjectPath);
        Application.Current?.Dispatcher.InvokeAsync(RefreshChrome);
    }

    private void TryApplyCachedUfValue()
    {
        if (host.Session.ValorUfClp > 0m) return;
        var currency = host.Session.ProjectCurrency;
        if (string.Equals(currency, "UF", StringComparison.OrdinalIgnoreCase)) return;

        var cached = EconomicIndicatorCache.TryRead();
        if (cached == null || EconomicIndicatorCache.IsStale(cached.FetchedAt)) return;

        host.Session.ValorUfClp = cached.UfClp;
        host.Session.MarkDirty();
    }

    private void RefreshRecentProjects()
    {
        recentProjectItems.Clear();
        foreach (var item in recentProjects.GetProjects())
            recentProjectItems.Add(item);
    }

    private async void OnProjectLockLost(object? sender, LockLostEventArgs e)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            string? backupPath = null;
            if (host.ProjectWorkspace.HasOpenProject && host.Session.IsDirty)
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var tempDir = Path.Combine(appData, "QubiCostElectrical", "TempBackups");
                    Directory.CreateDirectory(tempDir);
                    
                    var fileName = Path.GetFileNameWithoutExtension(host.Session.CurrentProjectPath) + 
                                   $"_backup_{Guid.NewGuid().ToString("N").Substring(0, 8)}.qce";
                    backupPath = Path.Combine(tempDir, fileName);
                    
                    var snapshot = host.Session.CreateSnapshot();
                    await host.Services.ProjectRepository.SaveSQLiteSnapshotAsync(backupPath, snapshot);
                }
                catch (Exception ex)
                {
                    AppLog.Error("LockLostRecovery", "No se pudo realizar el auto-guardado de emergencia local.", ex);
                }
            }

            host.Session.SetLockReadOnly(true, "Se perdió la exclusividad de edición por problemas de red.");
            SyncReadOnlyFromSession();
            RefreshChrome();
            RefreshCurrentPage();

            string message = "Se ha perdido la conexión de red o la exclusividad del archivo de bloqueo.\n\n" +
                             "El proyecto ha pasado a modo SOLO LECTURA para prevenir pérdida de datos.";
            if (backupPath != null)
            {
                message += $"\n\nSe ha guardado una copia de seguridad de emergencia local en:\n{backupPath}";
            }
            
            WpfDialogService.Warning("Exclusividad de Bloqueo Perdida", message);
        });
    }
}
