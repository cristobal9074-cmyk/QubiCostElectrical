using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class VersionWorkflowService : IVersionWorkflowService
{
    public Result EnviarARevision(
        BudgetProjectSession session,
        string versionId,
        string usuario,
        string comentarioEnvio,
        string versionBaseId,
        decimal impactoUf)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Result result = Result.Failure("Error inesperado.");
        session.RunBypassingReadOnly(() =>
        {
            var version = session.Versiones.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
            {
                result = Result.Failure("La versión no existe.");
                return;
            }

            if (version.Estado != "Borrador" && version.Estado != "Preliminar")
            {
                RecordAudit(session, usuario, "IntentoTransicionInvalida", version.Estado, "Pendiente de revisión", versionId, $"De {version.Estado} a Pendiente de revisión");
                result = Result.Failure($"No se puede enviar a revisión desde el estado '{version.Estado}'. Debe estar en 'Borrador'.");
                return;
            }

            string oldEstado = version.Estado;
            version.Estado = "Pendiente de revisión";
            version.EnviadoPor = usuario;
            version.FechaEnvio = AppClock.Now;
            version.ComentarioEnvio = comentarioEnvio ?? "";
            version.VersionBaseComparada = versionBaseId ?? "";
            version.ImpactoEconomicoUf = impactoUf;
            version.FechaModificacion = AppClock.Now;

            version.HistorialEstados.Add(new BudgetVersionStatusHistoryRow
            {
                EstadoAnterior = oldEstado,
                EstadoNuevo = version.Estado,
                Fecha = AppClock.Now,
                Usuario = usuario,
                Comentario = comentarioEnvio ?? ""
            });

            RecordAudit(session, usuario, "EnvioARevision", oldEstado, version.Estado, versionId, comentarioEnvio ?? "");
            session.MarkDirty("versiones");
            result = Result.Success();
        });

        return result;
    }

    public Result IniciarRevision(BudgetProjectSession session, string versionId, string usuario)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Result result = Result.Failure("Error inesperado.");
        session.RunBypassingReadOnly(() =>
        {
            var version = session.Versiones.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
            {
                result = Result.Failure("La versión no existe.");
                return;
            }

            if (version.Estado != "Pendiente de revisión")
            {
                RecordAudit(session, usuario, "IntentoTransicionInvalida", version.Estado, "En revisión", versionId, $"De {version.Estado} a En revisión");
                result = Result.Failure($"No se puede iniciar revisión desde el estado '{version.Estado}'.");
                return;
            }

            string oldEstado = version.Estado;
            version.Estado = "En revisión";
            version.FechaModificacion = AppClock.Now;

            version.HistorialEstados.Add(new BudgetVersionStatusHistoryRow
            {
                EstadoAnterior = oldEstado,
                EstadoNuevo = version.Estado,
                Fecha = AppClock.Now,
                Usuario = usuario,
                Comentario = "Inicio de revisión formal"
            });

            RecordAudit(session, usuario, "InicioRevision", oldEstado, version.Estado, versionId, "Inicio de revisión");
            session.MarkDirty("versiones");
            result = Result.Success();
        });

        return result;
    }

    public Result AprobarVersion(BudgetProjectSession session, string versionId, string usuario, string comentario)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Result result = Result.Failure("Error inesperado.");
        session.RunBypassingReadOnly(() =>
        {
            var version = session.Versiones.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
            {
                result = Result.Failure("La versión no existe.");
                return;
            }

            if (version.Estado != "En revisión")
            {
                RecordAudit(session, usuario, "IntentoTransicionInvalida", version.Estado, "Aprobada", versionId, $"De {version.Estado} a Aprobada");
                result = Result.Failure($"No se puede aprobar desde el estado '{version.Estado}'.");
                return;
            }

            var guard = new AuthorizationGuard(new UserRolePermissionService());
            var authCheck = guard.CheckApprovalSod(version, usuario);
            if (!authCheck.IsAllowed)
            {
                RecordAudit(session, usuario, "BloqueadoSoD", version.Estado, version.Estado, versionId, authCheck.UserMessage);
                result = Result.Failure(authCheck.UserMessage);
                return;
            }

            var integCheck = guard.CheckIntegrity(version);
            if (!integCheck.IsAllowed)
            {
                RecordAudit(session, usuario, "IntegridadBloqueada", version.Estado, version.Estado, versionId, integCheck.UserMessage);
                result = Result.Failure(integCheck.UserMessage);
                return;
            }

            string oldEstado = version.Estado;
            version.Estado = "Aprobada";
            version.AprobadoPor = usuario;
            version.FechaAprobacion = AppClock.Now;
            version.ComentarioAprobacion = comentario ?? "";
            version.ResumenEconomicoAprobadoUf = version.MontoVentaUf;
            version.IntegrityHash = VersionIntegrityHasher.CalculateHash(version);
            version.FechaModificacion = AppClock.Now;

            version.HistorialEstados.Add(new BudgetVersionStatusHistoryRow
            {
                EstadoAnterior = oldEstado,
                EstadoNuevo = version.Estado,
                Fecha = AppClock.Now,
                Usuario = usuario,
                Comentario = comentario ?? ""
            });

            RecordAudit(session, usuario, "Aprobacion", oldEstado, version.Estado, versionId, comentario ?? "");
            session.MarkDirty("versiones");
            result = Result.Success();
        });

        return result;
    }

    public Result RechazarVersion(BudgetProjectSession session, string versionId, string usuario, string motivo)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Result result = Result.Failure("Error inesperado.");
        session.RunBypassingReadOnly(() =>
        {
            var version = session.Versiones.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
            {
                result = Result.Failure("La versión no existe.");
                return;
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                result = Result.Failure("Debe especificar obligatoriamente un motivo para el rechazo.");
                return;
            }

            if (version.Estado != "En revisión" && version.Estado != "Pendiente de revisión")
            {
                RecordAudit(session, usuario, "IntentoTransicionInvalida", version.Estado, "Rechazada", versionId, $"De {version.Estado} a Rechazada");
                result = Result.Failure($"No se puede rechazar desde el estado '{version.Estado}'.");
                return;
            }

            string oldEstado = version.Estado;
            version.Estado = "Rechazada";
            version.RechazadoPor = usuario;
            version.FechaRechazo = AppClock.Now;
            version.MotivoRechazo = motivo;
            version.FechaModificacion = AppClock.Now;

            version.HistorialEstados.Add(new BudgetVersionStatusHistoryRow
            {
                EstadoAnterior = oldEstado,
                EstadoNuevo = version.Estado,
                Fecha = AppClock.Now,
                Usuario = usuario,
                Comentario = motivo
            });

            RecordAudit(session, usuario, "Rechazo", oldEstado, version.Estado, versionId, motivo);
            session.MarkDirty("versiones");
            result = Result.Success();
        });

        return result;
    }

    public Result MarcarObsoleta(BudgetProjectSession session, string versionId, string usuario)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Result result = Result.Failure("Error inesperado.");
        session.RunBypassingReadOnly(() =>
        {
            var version = session.Versiones.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
            {
                result = Result.Failure("La versión no existe.");
                return;
            }

            if (version.Estado != "Aprobada")
            {
                RecordAudit(session, usuario, "IntentoTransicionInvalida", version.Estado, "Obsoleta", versionId, $"De {version.Estado} a Obsoleta");
                result = Result.Failure($"Solo se pueden marcar como obsoletas las versiones aprobadas.");
                return;
            }

            string oldEstado = version.Estado;
            version.Estado = "Obsoleta";
            version.FechaModificacion = AppClock.Now;

            version.HistorialEstados.Add(new BudgetVersionStatusHistoryRow
            {
                EstadoAnterior = oldEstado,
                EstadoNuevo = version.Estado,
                Fecha = AppClock.Now,
                Usuario = usuario,
                Comentario = "Versión superada por una nueva versión aprobada"
            });

            RecordAudit(session, usuario, "Obsoleta", oldEstado, version.Estado, versionId, "Marcada obsoleta");
            session.MarkDirty("versiones");
            result = Result.Success();
        });

        return result;
    }

    public bool VerificarIntegridad(BudgetVersionRow version)
    {
        if (version == null) return false;
        if (version.Estado != "Aprobada") return true;

        if (string.IsNullOrEmpty(version.IntegrityHash))
            return false;

        string currentHash = VersionIntegrityHasher.CalculateHash(version);
        return string.Equals(version.IntegrityHash, currentHash, StringComparison.OrdinalIgnoreCase);
    }

    public SodCheckResult CheckApprovalSod(BudgetVersionRow version, string userEmail)
    {
        if (version == null) throw new ArgumentNullException(nameof(version));
        var guard = new AuthorizationGuard(new UserRolePermissionService());
        var result = guard.CheckApprovalSod(version, userEmail);
        if (result.IsAllowed)
            return SodCheckResult.Allowed;
        return SodCheckResult.Blocked(result.UserMessage);
    }

    private static void RecordAudit(
        BudgetProjectSession session,
        string usuario,
        string action,
        string estadoAnt,
        string estadoNue,
        string versionId,
        string comentario)
    {
        var entry = new ProjectAuditEntryRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurredAt = AppClock.Now,
            UserId = usuario,
            UserDisplayName = usuario,
            Action = action,
            Entity = "Version",
            EntityId = versionId,
            OldValue = estadoAnt,
            NewValue = estadoNue,
            Detail = comentario
        };
        session.ProjectInfo.AuditLog.Add(entry);
    }
}
