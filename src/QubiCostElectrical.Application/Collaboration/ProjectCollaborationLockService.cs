using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical;

public sealed class ProjectCollaborationLockService : IDisposable
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan HeartbeatExpiry = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string instanceId = Guid.NewGuid().ToString("N");
    private Timer? heartbeatTimer;
    private string? currentLockPath;
    private FileStream? lockStream;
    private CollaborationUserContext? currentUser;
    private bool disposed;
    private int consecutiveHeartbeatFailures;

    public event EventHandler<LockLostEventArgs>? LockLost;

    public ProjectCollaborationLockService()
    {
        heartbeatTimer = new Timer(_ => Task.Run(() => UpdateHeartbeat()), null, HeartbeatInterval, HeartbeatInterval);
    }

    public string? CurrentLockPath => currentLockPath;

    public LockCheckResult CheckLock(string projectPath)
    {
        return InspectLock(projectPath).State switch
        {
            ProjectLockState.NotLocked => LockCheckResult.NotLocked,
            ProjectLockState.OwnLock => LockCheckResult.LockedBySameMachineAlive,
            ProjectLockState.LocalMachineAlive => LockCheckResult.LockedBySameMachineAlive,
            ProjectLockState.LocalMachineDead => LockCheckResult.LockedBySameMachineDead,
            ProjectLockState.RemoteRecent => LockCheckResult.LockedByOtherMachine,
            ProjectLockState.HeartbeatExpired => LockCheckResult.LockedByOtherMachine,
            ProjectLockState.Corrupt => LockCheckResult.LockedByOtherMachine,
            _ => LockCheckResult.NotLocked
        };
    }

    public ProjectLockInspection InspectLock(string projectPath)
    {
        var lockPath = GetLockPath(projectPath);
        if (!File.Exists(lockPath))
            return new ProjectLockInspection(ProjectLockState.NotLocked, null, lockPath, null, "No existe bloqueo");

        try
        {
            var data = ReadLockData(lockPath);
            if (data == null)
                return new ProjectLockInspection(ProjectLockState.Corrupt, null, lockPath, null, "El archivo de bloqueo no contiene datos válidos");

            TimeSpan? age = data.LastHeartbeatUtc == default ? null : DateTime.UtcNow - data.LastHeartbeatUtc;
            bool sameMachine = string.Equals(data.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            bool sameInstance = string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase);
            bool processAlive = IsProcessAlive(data.ProcessId);
            bool heartbeatFresh = age.HasValue && age.Value <= HeartbeatExpiry;

            if (sameInstance && processAlive && lockStream != null)
                return new ProjectLockInspection(ProjectLockState.OwnLock, data, lockPath, age, "Bloqueo propio activo");

            if (sameMachine)
            {
                if (processAlive && heartbeatFresh)
                    return new ProjectLockInspection(ProjectLockState.LocalMachineAlive, data, lockPath, age, "Otro proceso local activo");

                return new ProjectLockInspection(ProjectLockState.LocalMachineDead, data, lockPath, age, "Proceso local inexistente o sin respuesta");
            }

            if (!heartbeatFresh)
                return new ProjectLockInspection(ProjectLockState.HeartbeatExpired, data, lockPath, age, "Bloqueo remoto expirado");

            return new ProjectLockInspection(ProjectLockState.RemoteRecent, data, lockPath, age, "Bloqueo remoto activo");
        }
        catch
        {
            return new ProjectLockInspection(ProjectLockState.Corrupt, null, lockPath, null, "El archivo de bloqueo está corrupto o no pudo leerse");
        }
    }

    public bool TryAcquireLock(string projectPath, CollaborationUserContext user)
    {
        var lockPath = GetLockPath(projectPath);
        if (currentLockPath != null && !string.Equals(currentLockPath, lockPath, StringComparison.OrdinalIgnoreCase))
            ReleaseLock();

        currentUser = user;
        var inspection = InspectLock(projectPath);

        if (inspection.State is ProjectLockState.Corrupt or ProjectLockState.LocalMachineAlive or ProjectLockState.RemoteRecent)
            return false;

        if (inspection.State == ProjectLockState.OwnLock)
        {
            currentLockPath = lockPath;
            UpdateHeartbeat();
            return true;
        }

        if (inspection.State is ProjectLockState.LocalMachineDead or ProjectLockState.HeartbeatExpired)
        {
            if (!TryRemoveForeignLock(lockPath, inspection.Data))
                return false;
        }

        return TryCreateExclusiveLock(lockPath, projectPath, user);
    }

    public bool ForceAcquireExpiredLock(string projectPath, CollaborationUserContext user)
    {
        currentUser = user;
        var lockPath = GetLockPath(projectPath);
        var inspection = InspectLock(projectPath);
        if (!inspection.CanTakeOver)
            return false;

        if (!TryRemoveForeignLock(lockPath, inspection.Data))
            return false;

        return TryCreateExclusiveLock(lockPath, projectPath, user);
    }

    public void ReleaseLock()
    {
        CloseLockStream();

        if (currentLockPath == null)
            return;

        try
        {
            if (File.Exists(currentLockPath))
            {
                var data = ReadLockData(currentLockPath);
                if (data != null && string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                    File.Delete(currentLockPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] No se pudo liberar bloqueo: {ex.Message}");
        }

        currentLockPath = null;
    }

    public void RemoveStaleLock(string projectPath)
    {
        var lockPath = GetLockPath(projectPath);
        try
        {
            if (!File.Exists(lockPath))
                return;

            var data = ReadLockData(lockPath);
            if (data == null)
                return;

            if (string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                return;

            var inspection = InspectLock(projectPath);
            if (!inspection.CanTakeOver)
                return;

            TryRemoveForeignLock(lockPath, data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] No se pudo eliminar bloqueo obsoleto: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        heartbeatTimer?.Dispose();
        heartbeatTimer = null;
        ReleaseLock();
    }

    private bool TryCreateExclusiveLock(string lockPath, string projectPath, CollaborationUserContext user)
    {
        try
        {
            var dir = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            CloseLockStream();
            lockStream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            currentLockPath = lockPath;

            var data = BuildLockData(projectPath, user);
            WriteLockDataToStream(data);
            return true;
        }
        catch (IOException)
        {
            CloseLockStream();
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] No se pudo adquirir bloqueo: {ex.Message}");
            CloseLockStream();
            return false;
        }
    }

    private bool TryRemoveForeignLock(string lockPath, ProjectLockData? existing)
    {
        try
        {
            if (existing != null && string.Equals(existing.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                return true;

            CloseLockStream();
            if (File.Exists(lockPath))
                File.Delete(lockPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] No se pudo retirar bloqueo ajeno: {ex.Message}");
            return false;
        }
    }

    private ProjectLockData BuildLockData(string projectPath, CollaborationUserContext user)
    {
        var now = DateTime.UtcNow;
        return new ProjectLockData
        {
            Version = 2,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            InstanceId = instanceId,
            UserId = user.UserId,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            UserRole = user.RoleDisplayName,
            CreatedAtUtc = now,
            LastHeartbeatUtc = now,
            NormalizedPath = Path.GetFullPath(projectPath),
            LockMode = "ExclusiveEdit"
        };
    }

    private void UpdateHeartbeat()
    {
        if (currentLockPath == null || lockStream == null || currentUser == null)
            return;

        try
        {
            var data = ReadLockData(currentLockPath);
            if (data == null || !string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                consecutiveHeartbeatFailures++;
                if (consecutiveHeartbeatFailures >= 3)
                {
                    LockLost?.Invoke(this, new LockLostEventArgs("El archivo de bloqueo ya no pertenece a este proceso."));
                }
                return;
            }

            data = data with { LastHeartbeatUtc = DateTime.UtcNow };
            WriteLockDataToStream(data);
            consecutiveHeartbeatFailures = 0;
        }
        catch (Exception ex)
        {
            consecutiveHeartbeatFailures++;
            if (consecutiveHeartbeatFailures >= 3)
            {
                LockLost?.Invoke(this, new LockLostEventArgs($"Error al actualizar latido de red: {ex.Message}"));
            }
        }
    }

    private void WriteLockDataToStream(ProjectLockData data)
    {
        if (lockStream == null)
            return;

        var json = JsonSerializer.Serialize(data, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        lockStream.SetLength(0);
        lockStream.Position = 0;
        lockStream.Write(bytes, 0, bytes.Length);
        lockStream.Flush(true);
    }

    private ProjectLockData? ReadLockData(string lockPath)
    {
        if (string.Equals(lockPath, currentLockPath, StringComparison.OrdinalIgnoreCase) && lockStream != null)
            return ReadLockDataFromStream(lockStream);

        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return ReadLockDataFromStream(stream);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[ProjectLock] No se pudo leer bloqueo desde stream: {ex.Message}");
            try
            {
                string json = File.ReadAllText(lockPath);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonSerializer.Deserialize<ProjectLockData>(json, JsonOpts);
            }
            catch (Exception fallbackEx)
            {
                Debug.WriteLine($"[ProjectLock] Tampoco se pudo leer bloqueo como texto: {fallbackEx.Message}");
                return null;
            }
        }
    }

    private static ProjectLockData? ReadLockDataFromStream(Stream stream)
    {
        try
        {
            if (stream.CanSeek)
                stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            string json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<ProjectLockData>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private void CloseLockStream()
    {
        try
        {
            lockStream?.Dispose();
        }
        catch
        {
        }
        finally
        {
            lockStream = null;
        }
    }

    private static string GetLockPath(string projectPath) => projectPath + ".lock";

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            return !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class LockLostEventArgs : EventArgs
{
    public string Reason { get; }
    public LockLostEventArgs(string reason) => Reason = reason;
}
