using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class UpdatesViewModelTests
{
    // ── Fake para aislar UpdatesViewModel de Velopack real ────────────────

    private sealed class FakeUpdateService : IUpdateService
    {
        public bool IsInstalled { get; set; } = true;
        public string Channel { get; set; } = "win";
        public string? AvailableVersion { get; set; } = null;
        public bool ThrowOnCheck { get; set; }
        public bool ThrowOnDownload { get; set; }
        public bool CancelOnDownload { get; set; }
        public List<int> ReportedProgress { get; } = new();
        public TaskCompletionSource? HoldCheckCompletion { get; set; }
        public int CheckCalls { get; private set; }
        public bool ApplyCalled { get; private set; }
        public TaskCompletionSource<bool> DownloadCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string?> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            CheckCalls++;
            if (HoldCheckCompletion != null)
                await HoldCheckCompletion.Task;
            else
                await Task.Yield();
            if (ThrowOnCheck) throw new HttpRequestException("Simulated network error.");
            ct.ThrowIfCancellationRequested();
            return AvailableVersion;
        }

        public async Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default)
        {
            await Task.Yield();
            try
            {
                if (ThrowOnDownload) throw new IOException("Simulated download failure.");
                if (CancelOnDownload) throw new OperationCanceledException();
                for (int p = 0; p <= 100; p += 25)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(p);
                    ReportedProgress.Add(p);
                }
            }
            finally
            {
                DownloadCompleted.TrySetResult(true);
            }
        }

        public void ApplyAndRestart() => ApplyCalled = true;
    }

    private static UpdatesViewModel Make(
        FakeUpdateService? svc = null,
        bool confirmInstall = true,
        bool hasUnsaved = false)
    {
        svc ??= new FakeUpdateService();
        return new UpdatesViewModel(svc, () => confirmInstall, () => hasUnsaved);
    }

    // ── 1. Construcción ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var vm = Make();
        Assert.NotNull(vm);
    }

    // ── 2. Versión instalada ──────────────────────────────────────────────

    [Fact]
    public void CurrentVersion_MatchesProductVersionInfo()
    {
        var vm = Make();
        Assert.Equal(ProductVersionInfo.ProductVersionLabel, vm.CurrentVersion);
    }

    // ── 3. Canal ─────────────────────────────────────────────────────────

    [Fact]
    public void Channel_ReflectsService()
    {
        var svc = new FakeUpdateService { Channel = "win" };
        var vm = Make(svc);
        Assert.Equal("win", vm.Channel);
    }

    // ── 4. Estado inicial ─────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsIdle()
    {
        var vm = Make();
        Assert.Equal(UpdateState.Idle, vm.State);
    }

    [Fact]
    public void InitialState_CommandsAreNotNull()
    {
        var vm = Make();
        Assert.NotNull(vm.CheckForUpdatesCommand);
        Assert.NotNull(vm.DownloadUpdateCommand);
        Assert.NotNull(vm.InstallAndRestartCommand);
    }

    // ── 5. Comprobación sin actualización ─────────────────────────────────

    [Fact]
    public async Task CheckCommand_NoUpdate_SetsUpToDate()
    {
        var svc = new FakeUpdateService { AvailableVersion = null };
        var vm = Make(svc);
        await RunCheckAsync(vm);

        Assert.Equal(UpdateState.UpToDate, vm.State);
        Assert.Null(vm.AvailableVersion);
        Assert.NotNull(vm.LastCheckedAt);
    }

    [Fact]
    public async Task CheckCommand_NoUpdate_StatusContainsActualizado()
    {
        var vm = Make(new FakeUpdateService { AvailableVersion = null });
        await RunCheckAsync(vm);
        Assert.Contains("actualizado", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── 6. Actualización disponible ───────────────────────────────────────

    [Fact]
    public async Task CheckCommand_WithUpdate_SetsUpdateAvailable()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);

        Assert.Equal(UpdateState.UpdateAvailable, vm.State);
        Assert.Equal("1.0.0-rc.3", vm.AvailableVersion);
    }

    [Fact]
    public async Task CheckCommand_WithUpdate_StatusContainsVersion()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        Assert.Contains("1.0.0-rc.3", vm.StatusMessage);
    }

    // ── 7. Error de red ───────────────────────────────────────────────────

    [Fact]
    public async Task CheckCommand_OnNetworkError_SetsErrorState()
    {
        var svc = new FakeUpdateService { ThrowOnCheck = true };
        var vm = Make(svc);
        await RunCheckAsync(vm);

        Assert.Equal(UpdateState.Error, vm.State);
        Assert.True(vm.HasError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.DoesNotContain("http", vm.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ── 8. Comprobación concurrente bloqueada ─────────────────────────────

    [Fact]
    public async Task CheckCommand_WhileChecking_DoesNotRunConcurrently()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var svc = new FakeUpdateService { HoldCheckCompletion = hold };
        var vm = Make(svc);
        var t1 = RunCheckAsync(vm);
        var t2 = RunCheckAsync(vm);
        hold.SetResult();
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, svc.CheckCalls);
    }

    // ── 9. Descarga habilitada con actualización ──────────────────────────

    [Fact]
    public async Task DownloadCommand_EnabledWhenUpdateAvailable()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        Assert.True(vm.DownloadUpdateCommand.CanExecute(null));
    }

    // ── 10. Descarga deshabilitada sin actualización ──────────────────────

    [Fact]
    public async Task DownloadCommand_DisabledWhenUpToDate()
    {
        var vm = Make(new FakeUpdateService { AvailableVersion = null });
        await RunCheckAsync(vm);
        Assert.False(vm.DownloadUpdateCommand.CanExecute(null));
    }

    // ── 11. Progreso ──────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadCommand_ReportsProgress()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);

        Assert.True(svc.ReportedProgress.Count > 0);
        Assert.Equal(100, svc.ReportedProgress[^1]);
        Assert.Equal(UpdateState.ReadyToInstall, vm.State);
    }

    // ── 12. Error de descarga ─────────────────────────────────────────────

    [Fact]
    public async Task DownloadCommand_OnError_SetsErrorState()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3", ThrowOnDownload = true };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);

        Assert.Equal(UpdateState.Error, vm.State);
        Assert.True(vm.HasError);
    }

    // ── 13. Estado listo para instalar ────────────────────────────────────

    [Fact]
    public async Task DownloadCommand_OnSuccess_SetsReadyToInstall()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);

        Assert.Equal(UpdateState.ReadyToInstall, vm.State);
        Assert.True(vm.CanInstallVisible);
    }

    // ── 14. Confirmación cancelada ────────────────────────────────────────

    [Fact]
    public async Task InstallCommand_WhenConfirmCancelled_DoesNotApply()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc, confirmInstall: false);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.False(svc.ApplyCalled);
    }

    // ── 15. Confirmación aceptada ─────────────────────────────────────────

    [Fact]
    public async Task InstallCommand_WhenConfirmed_CallsApplyAndRestart()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc, confirmInstall: true);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.True(svc.ApplyCalled);
    }

    // ── 16. Proyecto con cambios pendientes ───────────────────────────────

    [Fact]
    public async Task InstallCommand_WhenProjectHasUnsavedChanges_DoesNotApply()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc, confirmInstall: true, hasUnsaved: true);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.False(svc.ApplyCalled);
    }

    // ── 17. Guardado cancelado (proyecto sin guardar bloqueado) ───────────

    [Fact]
    public async Task InstallCommand_WhenUnsaved_StatusMessageMentionsUnsaved()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc, confirmInstall: true, hasUnsaved: true);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.Contains("cambios sin guardar", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── 18. Guardado completado (sin cambios pendientes) ──────────────────

    [Fact]
    public async Task InstallCommand_WhenNoUnsaved_ProceedsToApply()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc, confirmInstall: true, hasUnsaved: false);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.True(svc.ApplyCalled);
    }

    // ── 19. Instalación y reinicio solicitados ────────────────────────────

    [Fact]
    public async Task InstallCommand_SetsApplyingState()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var states = new List<UpdateState>();
        var vm = Make(svc, confirmInstall: true);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.State)) states.Add(vm.State); };
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        await RunInstallAsync(vm);

        Assert.Contains(UpdateState.Applying, states);
    }

    // ── 20. CanExecute se actualiza correctamente ─────────────────────────

    [Fact]
    public async Task AfterCheck_DownloadCommandBecomesEnabled()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0" };
        var vm = Make(svc);
        Assert.False(vm.DownloadUpdateCommand.CanExecute(null));
        await RunCheckAsync(vm);
        Assert.True(vm.DownloadUpdateCommand.CanExecute(null));
    }

    // ── 21. Mensajes de estado ────────────────────────────────────────────

    [Fact]
    public void InitialStatusMessage_IsNotEmpty()
    {
        var vm = Make();
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage));
    }

    [Fact]
    public async Task CheckCommand_DuringCheck_StatusIndicatesChecking()
    {
        var checkStarted = new TaskCompletionSource<bool>();
        var checkCanContinue = new TaskCompletionSource<bool>();
        var svc = new SlowFakeUpdateService(checkStarted, checkCanContinue);
        var vm = new UpdatesViewModel(svc, () => true, () => false);

        var checkTask = Task.Run(() => vm.CheckForUpdatesCommand.Execute(null));
        await checkStarted.Task;
        Assert.Equal(UpdateState.Checking, vm.State);
        checkCanContinue.SetResult(true);
        await checkTask;
    }

    // ── 22. Sin dependencias WinForms ─────────────────────────────────────

    [Fact]
    public void UpdatesViewModel_HasNoWinFormsReference()
    {
        var asm = typeof(UpdatesViewModel).Assembly;
        var winFormsRef = asm.GetReferencedAssemblies()
            .Any(r => r.Name?.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) == true);
        Assert.False(winFormsRef, "UpdatesViewModel assembly must not reference WinForms.");
    }

    // ── 23. Uso correcto del servicio ─────────────────────────────────────

    [Fact]
    public async Task CheckCommand_CallsServiceOnce()
    {
        var svc = new FakeUpdateService();
        var vm = Make(svc);
        await RunCheckAsync(vm);
        Assert.Equal(1, svc.CheckCalls);
    }

    // ── 24. Excepción controlada ──────────────────────────────────────────

    [Fact]
    public async Task CheckCommand_OnException_DoesNotCrash()
    {
        var svc = new FakeUpdateService { ThrowOnCheck = true };
        var vm = Make(svc);
        var ex = await Record.ExceptionAsync(() => RunCheckAsync(vm));
        Assert.Null(ex);
        Assert.Equal(UpdateState.Error, vm.State);
    }

    // ── 25. Descarga cancelada ────────────────────────────────────────────

    [Fact]
    public async Task DownloadCommand_OnCancellation_RestoresUpdateAvailable()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3", CancelOnDownload = true };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        Assert.Equal(UpdateState.UpdateAvailable, vm.State);
    }

    // ── 26. IsUpdateAvailable abarca ReadyToInstall ───────────────────────

    [Fact]
    public async Task IsUpdateAvailable_TrueInReadyToInstall()
    {
        var svc = new FakeUpdateService { AvailableVersion = "1.0.0-rc.3" };
        var vm = Make(svc);
        await RunCheckAsync(vm);
        await RunDownloadAsync(vm, svc);
        Assert.True(vm.IsUpdateAvailable);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task RunCheckAsync(UpdatesViewModel vm)
    {
        vm.CheckForUpdatesCommand.Execute(null);
        await Task.Delay(150);
    }

    private static async Task RunDownloadAsync(UpdatesViewModel vm, FakeUpdateService svc)
    {
        vm.DownloadUpdateCommand.Execute(null);
        try
        {
            await svc.DownloadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // El flujo de error/cancelación se valida por el estado final del ViewModel.
        }

        try
        {
            await WaitUntilAsync(
                () => vm.State == UpdateState.ReadyToInstall && vm.DownloadProgress == 100,
                TimeSpan.FromSeconds(10));
        }
        catch
        {
            // El progreso final puede quedar sin bombear en el dispatcher de pruebas.
        }
    }

    private static async Task RunInstallAsync(UpdatesViewModel vm)
    {
        vm.InstallAndRestartCommand.Execute(null);
        await Task.Delay(50);
    }

    // Fake lento para test de estado Checking intermedio
    private sealed class SlowFakeUpdateService : IUpdateService
    {
        private readonly TaskCompletionSource<bool> started;
        private readonly TaskCompletionSource<bool> canContinue;

        public SlowFakeUpdateService(TaskCompletionSource<bool> started, TaskCompletionSource<bool> canContinue)
        {
            this.started = started;
            this.canContinue = canContinue;
        }

        public bool IsInstalled => true;
        public string Channel => "win";

        public async Task<string?> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            started.TrySetResult(true);
            await canContinue.Task;
            return null;
        }

        public Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
        public void ApplyAndRestart() { }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
                break;

            System.Windows.Application.Current?.Dispatcher?.Invoke(() => { }, DispatcherPriority.Background);

            await Task.Delay(25);
        }
    }
}
