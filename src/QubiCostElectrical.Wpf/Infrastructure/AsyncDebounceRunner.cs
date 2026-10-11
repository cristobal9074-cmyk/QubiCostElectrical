#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class AsyncDebounceRunner : IDisposable
{
    private readonly DispatcherTimer _timer;

    private readonly Func<CancellationToken, Task> _loadAsync;

    private CancellationTokenSource? _cts;

    private Task? _lastTask;

    private bool _disposed;

    public AsyncDebounceRunner(Func<CancellationToken, Task> loadAsync, TimeSpan debounceInterval)
    {
        _loadAsync = loadAsync ?? throw new ArgumentNullException("loadAsync");
        _timer = new DispatcherTimer
        {
            Interval = debounceInterval
        };
        _timer.Tick += OnTick;
    }

    public void Schedule()
    {
        if (!_disposed)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    public void RunNow()
    {
        if (!_disposed)
        {
            _timer.Stop();
            _ = RunAsync();
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _timer.Stop();
            await RunAsync();
        }
    }

    private async Task RunAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        CancellationTokenSource cts = (_cts = new CancellationTokenSource());
        Task? lastTask = _lastTask;
        if (lastTask != null && !lastTask.IsCompleted)
        {
            await lastTask;
        }
        _lastTask = RunCoreAsync(cts.Token);
        await _lastTask;
    }

    private async Task RunCoreAsync(CancellationToken ct)
    {
        try
        {
            await _loadAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex2)
        {
            AppLog.Warning("AsyncDebounce", "Carga de búsqueda falló", ex2);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
