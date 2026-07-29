namespace QubiCostElectrical;

/// <summary>
/// Estado de seguimiento de ejecución (distinto de <see cref="ActivityStatus"/> de planificación).
/// </summary>
public enum ProgressTrackingStatus
{
    NotStarted,
    Started,
    InProgress,
    Suspended,
    Completed,
    Cancelled,
    Blocked,
    Delayed,
    Ahead,
    OnTrack,
    NotApplicable
}
