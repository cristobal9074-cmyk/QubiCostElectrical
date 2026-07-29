using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Pruebas de Segregación de Funciones (SoD) para el flujo de aprobación de versiones.
/// Principio: el usuario que creó o envió a revisión una versión NO puede aprobarla.
/// </summary>
public sealed class VersionWorkflowSodTests
{
    private readonly VersionWorkflowService _service = new();

    private static BudgetProjectSession MakeSession(BudgetVersionRow version)
    {
        var session = new BudgetProjectSession();
        session.Versiones.Add(version);
        return session;
    }

    private static BudgetVersionRow MakeVersionInReview(
        string createdBy = "creador@empresa.cl",
        string sentBy = "")
    {
        var v = new BudgetVersionRow
        {
            Id = "v1",
            Codigo = "V1",
            Estado = "En revisión",
            CreadoPor = createdBy,
            EnviadoPor = string.IsNullOrWhiteSpace(sentBy) ? createdBy : sentBy,
            MontoVentaUf = 500m,
            IntegrityHash = "",
            Snapshot = new BudgetVersionData
            {
                Itemizado = new List<ItemizadoRow>
                {
                    new() { Id = "p1", Item = "1.1", Descripcion = "Partida", Cantidad = 5m }
                }
            }
        };
        return v;
    }

    // ── CheckApprovalSod ─────────────────────────────────────────────────────

    [Fact]
    public void CheckApprovalSod_Blocked_WhenApproverIsCreator()
    {
        var version = MakeVersionInReview(createdBy: "autor@empresa.cl");
        var result = _service.CheckApprovalSod(version, "autor@empresa.cl");
        Assert.True(result.IsBlocked);
        Assert.Contains("creó", result.Reason);
    }

    [Fact]
    public void CheckApprovalSod_Blocked_WhenApproverIsSender()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl", sentBy: "enviador@empresa.cl");
        var result = _service.CheckApprovalSod(version, "enviador@empresa.cl");
        Assert.True(result.IsBlocked);
        Assert.Contains("envió", result.Reason);
    }

    [Fact]
    public void CheckApprovalSod_Allowed_WhenApproverIsDifferentUser()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl", sentBy: "estimador@empresa.cl");
        var result = _service.CheckApprovalSod(version, "admin@empresa.cl");
        Assert.False(result.IsBlocked);
        Assert.Equal("", result.Reason);
    }

    [Fact]
    public void CheckApprovalSod_Allowed_WhenCreatedByIsEmpty()
    {
        var version = MakeVersionInReview(createdBy: "", sentBy: "");
        var result = _service.CheckApprovalSod(version, "admin@empresa.cl");
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public void CheckApprovalSod_Allowed_WhenUserEmailIsEmpty()
    {
        var version = MakeVersionInReview(createdBy: "alguien@empresa.cl");
        var result = _service.CheckApprovalSod(version, "");
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public void CheckApprovalSod_CaseInsensitive_BlocksMatch()
    {
        var version = MakeVersionInReview(createdBy: "ADMIN@EMPRESA.CL");
        var result = _service.CheckApprovalSod(version, "admin@empresa.cl");
        Assert.True(result.IsBlocked);
    }

    // ── AprobarVersion con SoD ────────────────────────────────────────────────

    [Fact]
    public void AprobarVersion_Fails_WhenApproverIsCreator()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl");
        var session = MakeSession(version);

        var result = _service.AprobarVersion(session, "v1", "creador@empresa.cl", "aprobado");

        Assert.False(result.IsSuccess);
        Assert.Contains("Segregación", result.Error);
        Assert.NotEqual("Aprobada", version.Estado); // estado no cambia
    }

    [Fact]
    public void AprobarVersion_Fails_WhenApproverIsSender()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl", sentBy: "enviador@empresa.cl");
        var session = MakeSession(version);

        var result = _service.AprobarVersion(session, "v1", "enviador@empresa.cl", "aprobado");

        Assert.False(result.IsSuccess);
        Assert.Contains("Segregación", result.Error);
        Assert.Equal("En revisión", version.Estado);
    }

    [Fact]
    public void AprobarVersion_Succeeds_WhenApproverIsDifferentUser()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl", sentBy: "estimador@empresa.cl");
        var session = MakeSession(version);

        var result = _service.AprobarVersion(session, "v1", "admin@empresa.cl", "Todo correcto");

        Assert.True(result.IsSuccess);
        Assert.Equal("Aprobada", version.Estado);
        Assert.Equal("admin@empresa.cl", version.AprobadoPor);
        Assert.False(string.IsNullOrWhiteSpace(version.IntegrityHash));
    }

    [Fact]
    public void AprobarVersion_Succeeds_WhenCreatedByIsEmpty()
    {
        // Versiones antiguas sin CreadoPor no deben bloquearse por SoD
        var version = MakeVersionInReview(createdBy: "", sentBy: "");
        var session = MakeSession(version);

        var result = _service.AprobarVersion(session, "v1", "admin@empresa.cl", "Versión heredada");

        Assert.True(result.IsSuccess);
        Assert.Equal("Aprobada", version.Estado);
    }

    [Fact]
    public void AprobarVersion_SodAuditEntry_Recorded_WhenBlocked()
    {
        var version = MakeVersionInReview(createdBy: "creador@empresa.cl");
        var session = MakeSession(version);

        _service.AprobarVersion(session, "v1", "creador@empresa.cl", "intento");

        // Se debe haber registrado el evento de bloqueo en el audit log
        var blockedEntry = session.ProjectInfo.AuditLog
            .FirstOrDefault(a => a.Action == "BloqueadoSoD");
        Assert.NotNull(blockedEntry);
        Assert.Equal("creador@empresa.cl", blockedEntry.UserId);
    }

    // ── SodCheckResult helpers ─────────────────────────────────────────────────

    [Fact]
    public void SodCheckResult_Allowed_HasEmptyReason()
    {
        var allowed = SodCheckResult.Allowed;
        Assert.False(allowed.IsBlocked);
        Assert.Equal("", allowed.Reason);
    }

    [Fact]
    public void SodCheckResult_Blocked_HasReason()
    {
        var blocked = SodCheckResult.Blocked("Motivo de prueba");
        Assert.True(blocked.IsBlocked);
        Assert.Equal("Motivo de prueba", blocked.Reason);
    }
}
