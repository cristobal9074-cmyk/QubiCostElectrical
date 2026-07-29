using Velopack;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Implementación real de <see cref="IUpdateService"/> sobre Velopack.
/// Mantiene la información de actualización pendiente entre Check → Download → Apply.
/// </summary>
internal sealed class VelopackUpdateService : IUpdateService
{
    private readonly string releasesSource;
    private UpdateInfo? pending;

    public VelopackUpdateService(string releasesSource)
    {
        this.releasesSource = releasesSource;
    }

    public string Channel => "win";

    public bool IsInstalled
    {
        get
        {
            try { return new UpdateManager(releasesSource).IsInstalled; }
            catch { return false; }
        }
    }

    public async Task<string?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        UpdateManager mgr;
        try { mgr = new UpdateManager(releasesSource); }
        catch { pending = null; return null; }
        if (!mgr.IsInstalled) { pending = null; return null; }
        pending = await mgr.CheckForUpdatesAsync();
        return pending?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (pending is null)
            throw new InvalidOperationException("Llama primero a CheckForUpdatesAsync.");
        var mgr = new UpdateManager(releasesSource);
        await mgr.DownloadUpdatesAsync(pending, p => progress?.Report(p), cancelToken: ct);
    }

    public void ApplyAndRestart()
    {
        if (pending is null)
            throw new InvalidOperationException("Llama primero a DownloadUpdateAsync.");
        var mgr = new UpdateManager(releasesSource);
        mgr.ApplyUpdatesAndRestart(pending);
    }
}
