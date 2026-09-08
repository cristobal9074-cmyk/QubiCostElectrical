namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Pantallas que pueden silenciar <c>Session.Changed</c> cuando están en cache inactiva.
/// </summary>
public interface ISessionChangeAware
{
    void SetSessionChangeActive(bool active);

    /// <summary>Hay eventos acumulados que se flushearán al reactivar.</summary>
    bool HasPendingSessionChanges { get; }
}
