using System;
using System.IO;
using System.Reflection;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class CollaborationLockTests
{
    [Fact]
    public void HeartbeatFailure_TriggersLockLostEvent_AfterThreeConsecutiveFailures()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"lock-test-{Guid.NewGuid():N}.qce");
        var lockFile = tempFile + ".lock";

        try
        {
            using var lockService = new ProjectCollaborationLockService();
            var user = CollaborationUserContext.FromEnvironment("user1", "User One", "user1@example.com", "Estimador");
            
            bool acquired = lockService.TryAcquireLock(tempFile, user);
            Assert.True(acquired);

            bool eventFired = false;
            string? lostReason = null;
            lockService.LockLost += (sender, args) =>
            {
                eventFired = true;
                lostReason = args.Reason;
            };

            // Obtener campos privados vía reflexión
            var streamField = typeof(ProjectCollaborationLockService).GetField("lockStream", BindingFlags.NonPublic | BindingFlags.Instance);
            var updateMethod = typeof(ProjectCollaborationLockService).GetMethod("UpdateHeartbeat", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(streamField);
            Assert.NotNull(updateMethod);

            var stream = (FileStream?)streamField.GetValue(lockService);
            Assert.NotNull(stream);

            // Cerrar el stream para forzar excepciones de I/O en el heartbeat
            stream!.Close();

            // Ejecutar el heartbeat 3 veces
            updateMethod!.Invoke(lockService, null);
            Assert.False(eventFired); // Aún no debe dispararse (1er fallo)

            updateMethod!.Invoke(lockService, null);
            Assert.False(eventFired); // Aún no debe dispararse (2do fallo)

            updateMethod!.Invoke(lockService, null);
            Assert.True(eventFired); // Debe dispararse al 3er fallo consecutivo
            Assert.True(lostReason!.Contains("El archivo de bloqueo ya no pertenece") || lostReason.Contains("Error al actualizar latido"));
        }
        finally
        {
            if (File.Exists(lockFile))
            {
                try { File.Delete(lockFile); } catch {}
            }
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch {}
            }
        }
    }
}
