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
}
