namespace QubiCostElectrical;

public sealed record ProjectLockData
{
    public int Version { get; init; } = 2;
    public string MachineName { get; init; } = "";
    public int ProcessId { get; init; }
    public string InstanceId { get; init; } = "";
    public string UserId { get; init; } = "";
    public string UserDisplayName { get; init; } = "";
    public string UserEmail { get; init; } = "";
    public string UserRole { get; init; } = "";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastHeartbeatUtc { get; init; }
    public string NormalizedPath { get; init; } = "";
    public string LockMode { get; init; } = "ExclusiveEdit";
}

public enum LockCheckResult
{
    NotLocked,
    LockedBySameMachineAlive,
    LockedBySameMachineDead,
    LockedByOtherMachine
}

public enum ProjectLockState
{
    NotLocked,
    OwnLock,
    LocalMachineAlive,
    LocalMachineDead,
    RemoteRecent,
    HeartbeatExpired,
    Corrupt
}

public sealed record ProjectLockInspection(
    ProjectLockState State,
    ProjectLockData? Data,
    string LockPath,
    TimeSpan? Age,
    string Message)
{
    public bool IsWritableCandidate => State is ProjectLockState.NotLocked or ProjectLockState.OwnLock or ProjectLockState.LocalMachineDead or ProjectLockState.HeartbeatExpired;
    public bool IsBlocked => State is ProjectLockState.LocalMachineAlive or ProjectLockState.RemoteRecent or ProjectLockState.Corrupt;
    public bool CanTakeOver => State is ProjectLockState.LocalMachineDead or ProjectLockState.HeartbeatExpired or ProjectLockState.Corrupt;

    public string HolderSummary => ProjectLockDescriptions.DescribeHolder(Data, State);
}

public static class ProjectLockDescriptions
{
    public static string DescribeHolder(ProjectLockData? data, ProjectLockState state)
    {
        if (data == null)
            return state switch
            {
                ProjectLockState.NotLocked => "Sin bloqueo activo",
                ProjectLockState.Corrupt => "Bloqueo ilegible",
                _ => "Bloqueo desconocido"
            };

        string user = string.IsNullOrWhiteSpace(data.UserDisplayName) ? "Usuario desconocido" : data.UserDisplayName;
        string machine = string.IsNullOrWhiteSpace(data.MachineName) ? "equipo desconocido" : data.MachineName;
        string since = data.CreatedAtUtc == default
            ? ""
            : $" desde {data.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";

        if (state == ProjectLockState.OwnLock)
            return $"Usted tiene el control de edición{since}.";

        if (state == ProjectLockState.HeartbeatExpired)
            return $"{user} ({machine}) dejó de responder; el bloqueo expiró{since}.";

        return $"{user} ({machine}) está editando este proyecto{since}.";
    }
}
