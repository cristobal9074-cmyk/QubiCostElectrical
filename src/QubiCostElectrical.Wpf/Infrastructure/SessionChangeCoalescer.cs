using System;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Agrupa ráfagas de <see cref="QubiCostElectrical.ProjectSessionChangedEventArgs"/> en el dispatcher UI.
/// </summary>
public sealed class SessionChangeCoalescer : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly TimeSpan delay;
    private readonly Action flush;
    private DispatcherTimer? timer;
    private bool disposed;
    private bool pending;

    public SessionChangeCoalescer(Dispatcher dispatcher, TimeSpan delay, Action flush)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.delay = delay;
        this.flush = flush ?? throw new ArgumentNullException(nameof(flush));
    }

    public void Signal()
    {
        if (disposed) return;
        pending = true;
        if (timer == null)
        {
            timer = new DispatcherTimer(delay, DispatcherPriority.Background, OnTick, dispatcher);
            timer.Start();
            return;
        }

        timer.Stop();
        timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        timer?.Stop();
        if (!pending || disposed) return;
        pending = false;
        flush();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= OnTick;
            timer = null;
        }
    }
}
