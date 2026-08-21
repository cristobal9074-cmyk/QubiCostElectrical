using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class AutoSaveService : INotifyPropertyChanged, IDisposable
{
    private readonly WpfAppHost host;
    private readonly IAppLogger? logger;
    private Timer? timer;
    private bool isSaving;
    private bool disposed;
    private string statusText = "";
    private DateTime? lastAutoSaveAt;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public AutoSaveService(WpfAppHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        logger = WpfAppHost.Logger;
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
        timer = new Timer(async _ => await OnTimerAsync(), null, Interval, Interval);
    }

    public void Stop()
    {
        timer?.Change(Timeout.Infinite, Timeout.Infinite);
        timer?.Dispose();
        timer = null;
        StatusText = "";
        lastAutoSaveAt = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
    }

    public void MarkSaved()
    {
        LastAutoSaveAt = DateTime.UtcNow;
    }

    private void UpdateStatus()
    {
        if (host.Session.IsDirty)
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

    private async Task OnTimerAsync()
    {
        if (disposed || isSaving) return;
        if (!host.Session.IsDirty) return;
        if (string.IsNullOrWhiteSpace(host.Session.CurrentProjectPath) &&
            !host.ProjectWorkspace.HasOpenProject)
            return;

        isSaving = true;
        try
        {
            var snapshot = host.Session.CreateSnapshot();
            var path = host.Session.CurrentProjectPath;

            var autoSavePath = GetAutoSavePath(path);
            var dir = Path.GetDirectoryName(autoSavePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var options = ProjectJsonSerializer.Options;
            var result = await SafeSaveService.SaveJsonFileAsync(autoSavePath, snapshot, options);

            if (result.IsSuccess)
            {
                LastAutoSaveAt = DateTime.UtcNow;
                Debug.WriteLine($"[AutoSave] Autoguardado completado: {autoSavePath}");
            }
            else
            {
                StatusText = "Error al auto-guardar";
                logger?.Warning("AutoSave", $"No se pudo autoguardar: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al auto-guardar";
            logger?.Error("AutoSave", "Error en autoguardado", ex);
        }
        finally
        {
            isSaving = false;
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
