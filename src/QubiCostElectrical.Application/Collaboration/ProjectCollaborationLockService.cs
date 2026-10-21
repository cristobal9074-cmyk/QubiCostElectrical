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
    /// <summary>Proyecto para el que exigimos lease (persiste tras LockLost hasta ReleaseLock intencional).</summary>
    private string? exclusiveEditProjectPath;
    private FileStream? lockStream;
    private CollaborationUserContext? currentUser;
    private bool disposed;
    private bool lockLostNotified;
    private int writeLeaseCount;
    private bool lockLostDeferred;

    public event EventHandler? LockLost;

    public ProjectCollaborationLockService()
    {
        heartbeatTimer = new Timer(_ => Task.Run(() => UpdateHeartbeat()), null, HeartbeatInterval, HeartbeatInterval);
    }

    public string? CurrentLockPath => currentLockPath;

    /// <summary>
    /// True si esta sesión abrió el proyecto en edición exclusiva y aún no liberó el contrato
    /// (incluye LockLostPending: ya no hay stream, pero sigue prohibido escribir el .qce).
    /// </summary>
    public bool RequiresExclusiveLease(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(exclusiveEditProjectPath) || string.IsNullOrWhiteSpace(projectPath))
            return false;
        try
        {
            return string.Equals(
                exclusiveEditProjectPath,
                Path.GetFullPath(projectPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Error al verificar ruta exclusiva para '{projectPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// True si esta instancia aún posee el .lock del proyecto.
    /// Lee el archivo en disco (no solo el stream local) para detectar takeover.
    /// </summary>
    public bool OwnsActiveLock(string projectPath)
    {
        if (disposed || string.IsNullOrWhiteSpace(projectPath))
            return false;
        if (lockStream == null || string.IsNullOrWhiteSpace(currentLockPath))
            return false;

        var expected = GetLockPath(projectPath);
        if (!string.Equals(currentLockPath, expected, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var data = ReadLockDataFromDisk(expected);
            return data != null &&
                   string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Error al leer lock en disco '{expected}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lease mid-write: mientras esté activo, un LockLost detectado por heartbeat
    /// se difiere hasta <see cref="IDisposable.Dispose"/> del lease.
    /// Devuelve null si ya no somos dueños.
    /// </summary>
    public IDisposable? TryBeginWriteLease(string projectPath)
    {
        if (!OwnsActiveLock(projectPath))
            return null;

        Interlocked.Increment(ref writeLeaseCount);
        return new WriteLease(this);
    }

    /// <summary>Actualiza el heartbeat si aún somos dueños; false si el lease se perdió.</summary>
    public bool TryRefreshLease(string projectPath)
    {
        if (!OwnsActiveLock(projectPath))
            return false;

        try
        {
            if (lockStream == null || currentUser == null)
                return false;

            var data = BuildLockData(projectPath, currentUser);
            WriteLockDataToStream(data);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Falló refresco de lease para '{projectPath}': {ex.Message}");
            return false;
        }
    }

    internal void RunHeartbeatForTests() => UpdateHeartbeat();

    /// <summary>Simula que el .lock pertenece a otra instancia (tests de carrera / mid-write).</summary>
    internal void SimulateForeignLockOnDiskForTests(string projectPath, ProjectLockData foreign)
    {
        var lockPath = GetLockPath(projectPath);
        if (string.Equals(currentLockPath, lockPath, StringComparison.OrdinalIgnoreCase) && lockStream != null)
        {
            WriteLockDataToStream(foreign);
            return;
        }

        var json = JsonSerializer.Serialize(foreign, JsonOpts);
        File.WriteAllText(lockPath, json);
    }

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
        lockLostNotified = false;
        var inspection = InspectLock(projectPath);

        if (inspection.State is ProjectLockState.Corrupt or ProjectLockState.LocalMachineAlive or ProjectLockState.RemoteRecent)
            return false;

        if (inspection.State == ProjectLockState.OwnLock)
        {
            currentLockPath = lockPath;
            exclusiveEditProjectPath = Path.GetFullPath(projectPath);
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
        lockLostNotified = false;
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
        writeLeaseCount = 0;
        lockLostDeferred = false;
        CloseLockStream();
        exclusiveEditProjectPath = null;

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
            exclusiveEditProjectPath = Path.GetFullPath(projectPath);

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
            // Disco primero: takeover visible aunque nuestro stream aún tenga JSON propio.
            var data = ReadLockDataFromDisk(currentLockPath) ?? ReadLockData(currentLockPath);
            if (data == null || !string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleOrNotifyLockLost();
                return;
            }

            data = data with { LastHeartbeatUtc = DateTime.UtcNow };
            WriteLockDataToStream(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Heartbeat falló al escribir stream: {ex.Message}");
            // Fallo al escribir: confirmar pérdida real (otro tomó el .lock) antes de avisar.
            try
            {
                var data = currentLockPath != null ? ReadLockDataFromDisk(currentLockPath) : null;
                if (data == null || !string.Equals(data.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                    ScheduleOrNotifyLockLost();
            }
            catch (Exception diskEx)
            {
                Debug.WriteLine($"[ProjectLock] Error al confirmar estado en disco tras fallo de heartbeat: {diskEx.Message}");
                ScheduleOrNotifyLockLost();
            }
        }
    }

    private void ScheduleOrNotifyLockLost()
    {
        if (Volatile.Read(ref writeLeaseCount) > 0)
        {
            lockLostDeferred = true;
            return;
        }

        NotifyLockLostOnce();
    }

    private void EndWriteLease()
    {
        if (Interlocked.Decrement(ref writeLeaseCount) > 0)
            return;

        if (!lockLostDeferred)
            return;

        lockLostDeferred = false;
        NotifyLockLostOnce();
    }

    private void NotifyLockLostOnce()
    {
        if (lockLostNotified)
            return;

        lockLostNotified = true;
        CloseLockStream();
        currentLockPath = null;
        // exclusiveEditProjectPath se mantiene: SaveAsync debe seguir exigiendo lease (fallar).
        writeLeaseCount = 0;
        lockLostDeferred = false;

        try
        {
            LockLost?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] LockLost handler falló: {ex.Message}");
        }
    }

    private sealed class WriteLease : IDisposable
    {
        private ProjectCollaborationLockService? owner;

        public WriteLease(ProjectCollaborationLockService owner) => this.owner = owner;

        public void Dispose()
        {
            var svc = Interlocked.Exchange(ref owner, null);
            svc?.EndWriteLease();
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

        return ReadLockDataFromDisk(lockPath);
    }

    private ProjectLockData? ReadLockDataFromDisk(string lockPath)
    {
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return ReadLockDataFromStream(stream);
        }
        catch (Exception streamEx)
        {
            Debug.WriteLine($"[ProjectLock] Stream falló al leer lock '{lockPath}': {streamEx.Message}. Intentando lectura directa de archivo.");
            try
            {
                string json = File.ReadAllText(lockPath);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonSerializer.Deserialize<ProjectLockData>(json, JsonOpts);
            }
            catch (Exception fileEx)
            {
                Debug.WriteLine($"[ProjectLock] Lectura directa de lock falló para '{lockPath}': {fileEx.Message}");
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Deserialización de lock desde stream falló: {ex.Message}");
            return null;
        }
    }

    private void CloseLockStream()
    {
        try
        {
            lockStream?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProjectLock] Error al cerrar lockStream: {ex.Message}");
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
