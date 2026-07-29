using System.Threading;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

/// <summary>
/// ViewModel de la sección Actualizaciones en Preferencias.
/// Coordina Check → Download → Apply mediante <see cref="IUpdateService"/>.
/// </summary>
public sealed class UpdatesViewModel : ViewModelBase
{
    private readonly IUpdateService service;
    private readonly Func<bool> confirmInstallFn;
    private readonly Func<bool> hasUnsavedProjectFn;

    private UpdateState state = UpdateState.Idle;
    private string? availableVersion;
    private int downloadProgress;
    private string statusMessage = "";
    private string? errorMessage;
    private DateTime? lastCheckedAt;
    private CancellationTokenSource? downloadCts;
    private int checkInProgress;

    /// <param name="service">Servicio de actualizaciones (real o fake para tests).</param>
    /// <param name="confirmInstallFn">
    ///   Devuelve true si el usuario confirma el reinicio.
    ///   Null usa el valor por defecto (true, solo para tests).
    /// </param>
    /// <param name="hasUnsavedProjectFn">
    ///   Devuelve true si el proyecto tiene cambios sin guardar.
    ///   Null usa WpfAppHost.Current.Session.IsDirty.
    /// </param>
    internal UpdatesViewModel(
        IUpdateService service,
        Func<bool>? confirmInstallFn = null,
        Func<bool>? hasUnsavedProjectFn = null)
    {
        this.service = service;
        this.confirmInstallFn = confirmInstallFn ?? DefaultConfirmInstall;
        this.hasUnsavedProjectFn = hasUnsavedProjectFn ?? DefaultHasUnsavedProject;

        CheckForUpdatesCommand = new RelayCommand(
            () => _ = CheckForUpdatesAsync(),
            () => CanCheck);
        DownloadUpdateCommand = new RelayCommand(
            () => _ = DownloadUpdateAsync(),
            () => CanDownload);
        InstallAndRestartCommand = new RelayCommand(
            () => _ = InstallAndRestartAsync(),
            () => CanInstall);

        statusMessage = service.IsInstalled
            ? "Haz clic en «Buscar actualizaciones» para comprobar."
            : "Actualizaciones no disponibles (ejecución directa, sin instalación Velopack).";
    }

    // ── Propiedades ───────────────────────────────────────────────────────

    public string CurrentVersion => ProductVersionInfo.ProductVersionLabel;
    public string Channel => service.Channel;
    public bool IsInstalled => service.IsInstalled;

    public UpdateState State
    {
        get => state;
        private set
        {
            if (!SetProperty(ref state, value)) return;
            OnPropertyChanged(nameof(IsChecking));
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(IsUpdateAvailable));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(CanInstallVisible));
            RaiseAllCommandsChanged();
        }
    }

    public string? AvailableVersion
    {
        get => availableVersion;
        private set => SetProperty(ref availableVersion, value);
    }

    public int DownloadProgress
    {
        get => downloadProgress;
        private set => SetProperty(ref downloadProgress, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public DateTime? LastCheckedAt
    {
        get => lastCheckedAt;
        private set => SetProperty(ref lastCheckedAt, value);
    }

    public bool IsChecking => state == UpdateState.Checking;
    public bool IsDownloading => state == UpdateState.Downloading;
    public bool IsUpdateAvailable => state is UpdateState.UpdateAvailable or UpdateState.ReadyToInstall;
    public bool HasError => state == UpdateState.Error;
    public bool CanInstallVisible => state == UpdateState.ReadyToInstall;

    // ── CanExecute ────────────────────────────────────────────────────────

    private bool CanCheck =>
        state is UpdateState.Idle or UpdateState.UpToDate or UpdateState.UpdateAvailable or UpdateState.Error;

    private bool CanDownload =>
        state == UpdateState.UpdateAvailable && service.IsInstalled;

    private bool CanInstall =>
        state == UpdateState.ReadyToInstall && service.IsInstalled;

    // ── Comandos ──────────────────────────────────────────────────────────

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }
    public ICommand InstallAndRestartCommand { get; }

    // ── Flujo principal ───────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync()
    {
        if (!CanCheck)
            return;

        if (Interlocked.Exchange(ref checkInProgress, 1) == 1)
            return;

        try
        {
            State = UpdateState.Checking;
            StatusMessage = "Verificando actualizaciones…";
            ErrorMessage = null;
            try
            {
                var version = await service.CheckForUpdatesAsync();
                LastCheckedAt = DateTime.Now;
                if (version is null)
                {
                    State = UpdateState.UpToDate;
                    AvailableVersion = null;
                    StatusMessage = $"Qubi Cost Electrical está actualizado.\nVersión instalada: {CurrentVersion}";
                }
                else
                {
                    State = UpdateState.UpdateAvailable;
                    AvailableVersion = version;
                    StatusMessage = $"Hay una nueva versión disponible: {version}";
                }
            }
            catch (OperationCanceledException)
            {
                State = UpdateState.Idle;
                StatusMessage = "Verificación cancelada.";
            }
            catch (Exception ex)
            {
                State = UpdateState.Error;
                ErrorMessage = "No fue posible comprobar las actualizaciones. Revisa tu conexión e inténtalo nuevamente.";
                StatusMessage = "";
                WpfAppHost.Logger?.Error("Updates", $"CheckForUpdates: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }
        finally
        {
            Interlocked.Exchange(ref checkInProgress, 0);
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (!CanDownload) return;
        State = UpdateState.Downloading;
        DownloadProgress = 0;
        StatusMessage = "Descargando actualización…";
        ErrorMessage = null;
        downloadCts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                if (downloadCts != null)
                    StatusMessage = $"Descargando… {p}%";
            });
            await service.DownloadUpdateAsync(progress, downloadCts.Token);
            State = UpdateState.ReadyToInstall;
            DownloadProgress = 100;
            StatusMessage = $"Descarga completada. Versión {AvailableVersion} lista para instalar.";
        }
        catch (OperationCanceledException)
        {
            State = UpdateState.UpdateAvailable;
            DownloadProgress = 0;
            StatusMessage = "Descarga cancelada.";
        }
        catch (Exception ex)
        {
            State = UpdateState.Error;
            ErrorMessage = "Error durante la descarga. Verifica tu conexión e inténtalo nuevamente.";
            StatusMessage = "";
            WpfAppHost.Logger?.Error("Updates", $"Download: {ex.GetType().Name}: {ex.Message}", ex);
        }
        finally
        {
            downloadCts?.Dispose();
            downloadCts = null;
        }
    }

    private async Task InstallAndRestartAsync()
    {
        if (!CanInstall) return;

        if (hasUnsavedProjectFn())
        {
            StatusMessage = "El proyecto tiene cambios sin guardar. Guarda o descarta los cambios antes de instalar.";
            return;
        }

        if (!confirmInstallFn()) return;

        State = UpdateState.Applying;
        StatusMessage = "Aplicando actualización y reiniciando…";
        await Task.Yield();
        service.ApplyAndRestart();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RaiseAllCommandsChanged()
    {
        (CheckForUpdatesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DownloadUpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (InstallAndRestartCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static bool DefaultConfirmInstall() => true;

    private static bool DefaultHasUnsavedProject() => false;
}
