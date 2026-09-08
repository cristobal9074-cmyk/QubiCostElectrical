using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Autoguardado en dos capas: sidecar JSON (.autosave) periódico para recuperación ante crash,
/// y persistencia silenciosa debounced al archivo .qce real cuando la sesión está dirty.
/// </summary>
public sealed class AutoSaveService : INotifyPropertyChanged, IDisposable
{
    /// <summary>Intervalo del sidecar de recuperación (.autosave).</summary>
    public static readonly TimeSpan SidecarInterval = TimeSpan.FromSeconds(15);

    /// <summary>Espera tras el último cambio antes de persistir al .qce.</summary>
    public static readonly TimeSpan PersistDebounceInterval = TimeSpan.FromMilliseconds(2500);

    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly IAppLogger? logger;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedHandler;

    private Timer? sidecarTimer;
    private Timer? persistDebounceTimer;
    private bool sessionSubscribed;
    private bool isPersisting;
    private bool isWritingSidecar;
    private bool disposed;
    private string statusText = "";
    private DateTime? lastAutoSaveAt;

    public AutoSaveService(BudgetProjectSession session, IProjectWorkspaceService projectWorkspace)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.projectWorkspace = projectWorkspace ?? throw new ArgumentNullException(nameof(projectWorkspace));
        logger = WpfAppHost.Logger;
        sessionChangedHandler = OnSessionChanged;
    }

    public string StatusText
    {
        get => statusText;
        private set
        {
            if (statusText == value) return;
            statusText = value;
            OnPropertyChanged();
        }
    }

    public DateTime? LastAutoSaveAt
    {
        get => lastAutoSaveAt;
        private set
        {
            if (lastAutoSaveAt == value) return;
            lastAutoSaveAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastAutoSaveAgo));
            UpdateStatus();
        }
    }

    public string LastAutoSaveAgo
    {
        get
        {
            if (!lastAutoSaveAt.HasValue) return "";
            var ago = DateTime.UtcNow - lastAutoSaveAt.Value;
            if (ago.TotalSeconds < 5) return "ahora";
            if (ago.TotalSeconds < 60) return $"hace {(int)ago.TotalSeconds}s";
            if (ago.TotalMinutes < 60) return $"hace {(int)ago.TotalMinutes}min";
            return $"hace {(int)ago.TotalHours}h";
        }
    }

    public void Start()
    {
        if (disposed) return;
        Stop();
        StatusText = "Auto-guardado activo";
        SubscribeSession();
        sidecarTimer = new Timer(async _ => await OnSidecarTimerAsync().ConfigureAwait(false), null, SidecarInterval, SidecarInterval);
    }

    public void Stop()
    {
        UnsubscribeSession();
        CancelPersistDebounce();
        sidecarTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        sidecarTimer?.Dispose();
        sidecarTimer = null;
        StatusText = "";
        lastAutoSaveAt = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
    }

    /// <summary>
    /// Arma el mismo debounce de 2,5 s hacia persistencia al .qce (p. ej. al salir de una página).
    /// </summary>
    public void RequestPersistSoon()
    {
        if (disposed) return;
        if (!CanPersistToProject()) return;
        ArmPersistDebounce();
    }

    /// <summary>
    /// Cancela el debounce y persiste de inmediato al .qce (cierre de app / flush forzado).
    /// No-op si solo lectura (p. ej. LockLostPending).
    /// </summary>
    public async Task FlushPersistAsync()
    {
        if (disposed) return;
        CancelPersistDebounce();
        if (!CanPersistToProject()) return;
        await PersistToProjectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Escribe el sidecar .autosave aunque la sesión esté en solo lectura (LockLostPending).
    /// </summary>
    public async Task FlushSidecarAsync()
    {
        if (disposed) return;
        await WriteSidecarAsync().ConfigureAwait(false);
    }

    public void MarkSaved()
    {
        LastAutoSaveAt = DateTime.UtcNow;
    }

    private void UpdateStatus()
    {
        if (session.IsDirty)
            StatusText = lastAutoSaveAt.HasValue ? $"Auto {LastAutoSaveAgo}" : "Sin guardar";
        else
            StatusText = lastAutoSaveAt.HasValue ? $"Guardado {LastAutoSaveAgo}" : "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static string GetAutoSavePath(string projectPath)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
            return projectPath + ".autosave";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QubiCostElectrical", "Autosave",
            Guid.NewGuid().ToString("N") + ProjectFileTypes.CurrentExtension + ProjectFileTypes.AutoSaveSuffix);
    }

    public static string GetAutoSaveRecoveryDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QubiCostElectrical", "Autosave");
    }

    private void SubscribeSession()
    {
        if (sessionSubscribed) return;
        session.Changed += sessionChangedHandler;
        sessionSubscribed = true;
    }

    private void UnsubscribeSession()
    {
        if (!sessionSubscribed) return;
        session.Changed -= sessionChangedHandler;
        sessionSubscribed = false;
    }

    private void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (disposed) return;
        if (!CanPersistToProject()) return;
        ArmPersistDebounce();
    }

    private bool CanPersistToProject()
    {
        if (!session.IsDirty) return false;
        if (session.IsReadOnly) return false;
        if (string.IsNullOrWhiteSpace(session.CurrentProjectPath) &&
            !projectWorkspace.HasOpenProject)
            return false;
        return true;
    }

    private void ArmPersistDebounce()
    {
        if (disposed) return;
        persistDebounceTimer ??= new Timer(async _ => await PersistToProjectAsync().ConfigureAwait(false));
        // Disparo único: reinicia la espera de 2,5 s ante cada cambio.
        persistDebounceTimer.Change(PersistDebounceInterval, Timeout.InfiniteTimeSpan);
    }

    private void CancelPersistDebounce()
    {
        if (persistDebounceTimer is null) return;
        persistDebounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        persistDebounceTimer.Dispose();
        persistDebounceTimer = null;
    }

    /// <summary>Cancela el debounce pendiente (p. ej. al perder el bloqueo SMB).</summary>
    public void CancelPendingPersist()
    {
        if (disposed) return;
        CancelPersistDebounce();
        StatusText = session.IsDirty ? "Solo lectura — sin auto-guardar" : StatusText;
    }

    /// <summary>
    /// Persistencia silenciosa al .qce real (mismo camino que Guardar manual).
    /// </summary>
    private async Task PersistToProjectAsync()
    {
        if (disposed) return;

        // No apilar guardados concurrentes; rearmar si sigue dirty para no perder el flush.
        if (isPersisting)
        {
            if (CanPersistToProject())
                ArmPersistDebounce();
            return;
        }

        if (!CanPersistToProject()) return;

        isPersisting = true;
        try
        {
            await projectWorkspace.SaveAsync().ConfigureAwait(false);

            if (disposed) return;

            var path = session.CurrentProjectPath;
            if (!string.IsNullOrWhiteSpace(path))
                DeleteAutoSave(path);

            MarkSaved();
            StatusText = "Guardado automático";
            Debug.WriteLine("[AutoSave] Persistencia silenciosa al .qce completada");
        }
        catch (ProjectLockLostException ex)
        {
            if (!disposed)
                StatusText = "Solo lectura — sin auto-guardar";
            logger?.Warning("AutoSave", ex.Message);
            // No borrar .autosave: el trabajo local sigue recuperable.
        }
        catch (Exception ex)
        {
            if (!disposed)
                StatusText = "Error al auto-guardar";
            logger?.Error("AutoSave", "Error en persistencia automática al .qce", ex);
        }
        finally
        {
            isPersisting = false;
        }
    }

    /// <summary>
    /// Sidecar JSON periódico (.autosave) como respaldo ante crash / LockLostPending.
    /// </summary>
    private async Task OnSidecarTimerAsync()
        => await WriteSidecarAsync().ConfigureAwait(false);

    private async Task WriteSidecarAsync()
    {
        if (disposed || isPersisting || isWritingSidecar) return;
        if (!session.IsDirty) return;

        // Sidecar permitido en RO (LockLostPending); solo el .qce está bloqueado.
        if (string.IsNullOrWhiteSpace(session.CurrentProjectPath) &&
            !projectWorkspace.HasOpenProject)
            return;

        isWritingSidecar = true;
        try
        {
            var snapshot = session.CreateSnapshot();
            var path = session.CurrentProjectPath;

            var autoSavePath = GetAutoSavePath(path);
            var dir = Path.GetDirectoryName(autoSavePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var options = ProjectJsonSerializer.Options;
            var result = await SafeSaveService.SaveJsonFileAsync(autoSavePath, snapshot, options).ConfigureAwait(false);

            if (disposed) return;

            if (result.IsSuccess)
            {
                LastAutoSaveAt = DateTime.UtcNow;
                if (session.IsReadOnly)
                    StatusText = "Copia local (.autosave) — bloqueo perdido";
                Debug.WriteLine($"[AutoSave] Sidecar .autosave completado: {autoSavePath}");
            }
            else
            {
                StatusText = "Error al auto-guardar";
                logger?.Warning("AutoSave", $"No se pudo escribir sidecar: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            if (!disposed)
                StatusText = "Error al auto-guardar";
            logger?.Error("AutoSave", "Error en autoguardado sidecar", ex);
        }
        finally
        {
            isWritingSidecar = false;
        }
    }

    public static DateTime? GetAutoSaveTimestamp(string projectPath)
    {
        var path = GetAutoSavePath(projectPath);
        if (!File.Exists(path)) return null;
        return File.GetLastWriteTimeUtc(path);
    }

    public static bool IsAutoSaveNewer(string projectPath)
    {
        var autoPath = GetAutoSavePath(projectPath);
        if (!File.Exists(autoPath)) return false;

        var projectFile = projectPath;
        if (!File.Exists(projectFile)) return false;

        return File.GetLastWriteTimeUtc(autoPath) > File.GetLastWriteTimeUtc(projectFile);
    }

    /// <summary>
    /// Lee el sidecar JSON de autoguardado si existe. Devuelve null si no hay archivo o es inválido.
    /// </summary>
    public static ProjectFileData? TryLoadAutoSave(string projectPath)
    {
        try
        {
            var autoPath = GetAutoSavePath(projectPath);
            if (!File.Exists(autoPath))
                return null;

            var json = File.ReadAllText(autoPath);
            return System.Text.Json.JsonSerializer.Deserialize<ProjectFileData>(json, ProjectJsonSerializer.Options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSave] No se pudo leer autosave: {ex.Message}");
            return null;
        }
    }

    public static void DeleteAutoSave(string projectPath)
    {
        try
        {
            var path = GetAutoSavePath(projectPath);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSave] No se pudo eliminar autosave: {ex.Message}");
        }
    }
}
