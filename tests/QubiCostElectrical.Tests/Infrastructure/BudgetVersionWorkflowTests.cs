using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class BudgetVersionWorkflowTests
{
    private readonly VersionWorkflowService service = new();

    private static BudgetVersionRow CreateTestVersion(string id, string codigo, string estado)
    {
        return new BudgetVersionRow
        {
            Id = id,
            Codigo = codigo,
            Estado = estado,
            Nombre = "Versión " + codigo,
            MontoVentaUf = 100m,
            Snapshot = new BudgetVersionData
            {
                Itemizado = new List<ItemizadoRow>
                {
                    new() { Id = "part1", Item = "1.1", Descripcion = "Partida 1", Cantidad = 10m }
                }
            }
        };
    }

    [Fact]
    public void Workflow_Borrador_To_PendienteDeRevision_Succeeds()
    {
        var session = new BudgetProjectSession();
        var version = CreateTestVersion("v1", "V1", "Borrador");
        session.Versiones.Add(version);

        var result = service.EnviarARevision(session, "v1", "UsuarioTest", "Envío inicial", "base", 15m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pendiente de revisión", version.Estado);
        Assert.Equal("UsuarioTest", version.EnviadoPor);
        Assert.Equal("Envío inicial", version.ComentarioEnvio);
        Assert.Equal(15m, version.ImpactoEconomicoUf);

        // Auditoría
        var audit = session.ProjectInfo.AuditLog.Last();
        Assert.Equal("EnvioARevision", audit.Action);
        Assert.Equal("Borrador", audit.OldValue);
        Assert.Equal("Pendiente de revisión", audit.NewValue);
    }

    [Fact]
    public void Workflow_InvalidTransition_Fails_And_RecordsAudit()
    {
        var session = new BudgetProjectSession();
        var version = CreateTestVersion("v1", "V1", "Borrador");
        session.Versiones.Add(version);

        // Intentar aprobar directamente desde Borrador
        var result = service.AprobarVersion(session, "v1", "UsuarioTest", "Aprobación directa");

        Assert.False(result.IsSuccess);
        Assert.Equal("Borrador", version.Estado);

        // Auditoría registra el intento inválido
        var audit = session.ProjectInfo.AuditLog.Last();
        Assert.Equal("IntentoTransicionInvalida", audit.Action);
        Assert.Equal("Aprobada", audit.NewValue);
    }

    [Fact]
    public void Workflow_Rechazar_RequiresMotivo()
    {
        var session = new BudgetProjectSession();
        var version = CreateTestVersion("v1", "V1", "Pendiente de revisión");
        session.Versiones.Add(version);

        // Intento rechazar sin motivo
        var result = service.RechazarVersion(session, "v1", "UsuarioTest", "");
        Assert.False(result.IsSuccess);
        Assert.Equal("Pendiente de revisión", version.Estado);

        // Rechazo con motivo
        result = service.RechazarVersion(session, "v1", "UsuarioTest", "Faltan APUs");
        Assert.True(result.IsSuccess);
        Assert.Equal("Rechazada", version.Estado);
    }

    [Fact]
    public void Workflow_ApprovedVersion_IsReadOnly_And_DetectsAlteration()
    {
        var session = new BudgetProjectSession();
        var version = CreateTestVersion("v1", "V1", "En revisión");
        session.Versiones.Add(version);
        session.ActiveVersionId = "v1";

        // 1. Aprobar versión
        var result = service.AprobarVersion(session, "v1", "Aprobador", "Todo OK");
        Assert.True(result.IsSuccess);
        Assert.Equal("Aprobada", version.Estado);

        // 2. Comprobar que la sesión ahora es de Solo Lectura
        Assert.True(session.IsReadOnly);

        // 3. Comprobar integridad inicial
        Assert.True(service.VerificarIntegridad(version));

        // 4. Alterar datos de la versión aprobada (simular intrusión externa)
        version.Snapshot.Itemizado[0].Cantidad = 999m;

        // 5. Verificar que se detecta la alteración
        Assert.False(service.VerificarIntegridad(version));
    }
}
