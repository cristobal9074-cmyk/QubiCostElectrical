namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Abstracción sobre Velopack para comprobación y aplicación de actualizaciones.
/// Toda la lógica real está en <see cref="VelopackUpdateService"/>.
/// </summary>
internal interface IUpdateService
{
    bool IsInstalled { get; }
    string Channel { get; }

    /// <summary>Devuelve la versión disponible o null si no hay actualización.</summary>
    Task<string?> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Descarga la actualización encontrada por CheckForUpdatesAsync.</summary>
    Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default);

    /// <summary>Aplica la actualización descargada y reinicia la aplicación.</summary>
    void ApplyAndRestart();
}
