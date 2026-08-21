using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Workspace;

public sealed class ProjectEditWorkspaceServiceTests
{
    [Fact]
    public void Validate_RejectsEmptyNombre()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new ProjectEditWorkspaceService(session);
        var snapshot = service.Load();
        snapshot.Nombre = "   ";

        var errors = service.Validate(snapshot);

        Assert.Contains(errors, e => e.Contains("Nombre del proyecto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_MarksSessionDirtyAndPersistsFields()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new ProjectEditWorkspaceService(session);
        var snapshot = service.Load();
        snapshot.Nombre = "Torre Central";
        snapshot.Cliente = "Inmobiliaria Norte";
        snapshot.ScopeText = "Alcance de prueba";
        snapshot.ValidityDays = 45;

        service.Apply(snapshot);

        Assert.True(session.IsDirty);
        Assert.Equal("Torre Central", session.ProjectInfo.Nombre);
        Assert.Equal("Inmobiliaria Norte", session.ProjectInfo.Cliente);
        Assert.Equal("Alcance de prueba", session.ProjectInfo.OfertaPdf.ScopeText);
        Assert.Equal(45, session.ProjectInfo.OfertaPdf.ValidityDays);
    }

    [Fact]
    public void Discard_ClearsWorkingCopy()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new ProjectEditWorkspaceService(session);
        _ = service.Load();
        service.Discard();
        Assert.False(service.HasWorkingCopy);
    }

    // Regresión: el IVA % no tenía forma de editarse desde la UI (faltaba en el snapshot
    // completo: Load/Clone/Validate/Apply/ContentEquals). Este grupo de tests cubre el
    // recorrido completo del campo, igual que ya estaba cubierto para GG% y Utilidad%.
    [Fact]
    public void Load_IncludesCurrentIvaPct()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        session.ProjectInfo.IvaPct = 19m;
        var service = new ProjectEditWorkspaceService(session);

        var snapshot = service.Load();

        Assert.Equal(19m, snapshot.IvaPct);
    }

    [Fact]
    public void Apply_PersistsIvaPct()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new ProjectEditWorkspaceService(session);
        var snapshot = service.Load();
        snapshot.IvaPct = 0m;

        service.Apply(snapshot);

        Assert.Equal(0m, session.ProjectInfo.IvaPct);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_RejectsIvaPctOutOfRange(decimal invalidIva)
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new ProjectEditWorkspaceService(session);
        var snapshot = service.Load();
        snapshot.IvaPct = invalidIva;

        var errors = service.Validate(snapshot);

        Assert.Contains(errors, e => e.Contains("IVA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Clone_CopiesIvaPct()
    {
        var snapshot = new ProjectEditSnapshot { IvaPct = 19m };
        var clone = snapshot.Clone();
        Assert.Equal(19m, clone.IvaPct);
    }

    [Fact]
    public void ContentEquals_DetectsIvaPctChange()
    {
        var a = new ProjectEditSnapshot { IvaPct = 19m };
        var b = a.Clone();
        Assert.True(a.ContentEquals(b));

        b.IvaPct = 0m;
        Assert.False(a.ContentEquals(b));
    }
}
