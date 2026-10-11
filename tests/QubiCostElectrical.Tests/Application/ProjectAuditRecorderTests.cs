using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectAuditRecorderTests
{
    [Fact]
    public void RecordLifecycle_AppendsAuditEntryToProject()
    {
        var session = new BudgetProjectSession();
        session.NewProject("Tester", "Estimador");
        var recorder = new ProjectAuditRecorder(session);
        recorder.SetUser(CollaborationUserContext.FromEnvironment("u1", "Tester", "t@q.com", "Estimador"));
        recorder.Enable();

        recorder.RecordLifecycle("Abrir", "Proyecto demo");

        Assert.Single(session.ProjectInfo.AuditLog);
        Assert.Equal("Abrir", session.ProjectInfo.AuditLog[0].Action);
        Assert.Equal("Tester", session.ProjectInfo.AuditLog[0].UserDisplayName);
    }

    [Fact]
    public void RecordPartidaChange_WritesTimelineAndAudit()
    {
        var session = new BudgetProjectSession();
        session.NewProject("Tester", "Estimador");
        var recorder = new ProjectAuditRecorder(session);
        recorder.SetUser(CollaborationUserContext.FromEnvironment("u1", "Tester", "", "Estimador"));
        recorder.Enable();

        recorder.RecordPartidaChange("P1", "Cantidad", "10", "20");

        Assert.Single(session.PartidaTimeline);
        Assert.Equal("CambioPartida", session.ProjectInfo.AuditLog[^1].Action);
        Assert.Equal("20", session.PartidaTimeline[0].ValorNuevo);
    }

    [Fact]
    public void NotifySaving_RegistersGuardadoEntryBeforeSnapshotIsTaken()
    {
        var session = new BudgetProjectSession();
        session.NewProject("Tester", "Estimador");
        var recorder = new ProjectAuditRecorder(session);
        recorder.SetUser(CollaborationUserContext.FromEnvironment("u1", "Tester", "", "Estimador"));
        recorder.Enable();

        // Reproduce el flujo real de ProjectWorkspaceService.SaveAsAsync: el aviso
        // llega antes de serializar, así que la entrada debe viajar dentro del snapshot.
        session.NotifySaving();
        var snapshot = session.CreateSnapshot();

        var guardado = Assert.Single(snapshot.Proyecto.AuditLog);
        Assert.Equal("Guardado", guardado.Action);
    }

    [Fact]
    public void MarkSaved_DoesNotRegisterGuardadoEntry()
    {
        var session = new BudgetProjectSession();
        session.NewProject("Tester", "Estimador");
        var recorder = new ProjectAuditRecorder(session);
        recorder.SetUser(CollaborationUserContext.FromEnvironment("u1", "Tester", "", "Estimador"));
        recorder.Enable();

        // MarkSaved corre DESPUÉS de escribir el .qce: una entrada registrada aquí
        // nunca se persistiría. La responsabilidad es de NotifySaving().
        session.MarkSaved();

        Assert.DoesNotContain(session.ProjectInfo.AuditLog, e => e.Action == "Guardado");
    }
}
