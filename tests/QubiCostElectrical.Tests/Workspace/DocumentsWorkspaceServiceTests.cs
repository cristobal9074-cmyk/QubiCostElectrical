using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Workspace;

public sealed class DocumentsWorkspaceServiceTests
{
    [Fact]
    public void Duplicate_ReturnsNewRowAddedToWorkingCopy()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new DocumentsWorkspaceService(session);
        var original = service.AddNew(AntecedenteTecnicoTipo.Plano);
        original.Codigo = "E-01";

        var copy = service.Duplicate(original);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("E-01", copy.Codigo);
        Assert.Equal(2, service.GetRows().Count);
    }

    [Fact]
    public void Filter_BySearchAndTipo()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        session.ProjectInfo.AntecedentesTecnicos = new List<AntecedenteTecnicoRow>
        {
            new() { Tipo = AntecedenteTecnicoTipo.Plano, Codigo = "E-01", Descripcion = "Unifilar" },
            new() { Tipo = AntecedenteTecnicoTipo.Addenda, Codigo = "AD-1", Descripcion = "Cambio tablero" }
        };

        var service = new DocumentsWorkspaceService(session);
        var filtered = service.Filter("tablero", AntecedenteTecnicoTipo.Addenda);

        Assert.Single(filtered);
        Assert.Equal("AD-1", filtered[0].Codigo);
    }

    [Fact]
    public void SaveChanges_PersistsRowsToProject()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        var service = new DocumentsWorkspaceService(session);
        var row = service.AddNew(AntecedenteTecnicoTipo.Consulta);
        row.Codigo = "C-01";
        service.SetFechaCorte(new DateTime(2026, 3, 1));
        service.SaveChanges();

        Assert.True(session.IsDirty);
        Assert.Single(session.ProjectInfo.AntecedentesTecnicos);
        Assert.Equal("C-01", session.ProjectInfo.AntecedentesTecnicos[0].Codigo);
        Assert.Equal(new DateTime(2026, 3, 1), session.ProjectInfo.FechaCorteAntecedentes);
    }

    [Fact]
    public void SyncPlanosToOffer_UsesWorkingCopyBeforeSave()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester");
        session.ProjectInfo.AntecedentesTecnicos = new List<AntecedenteTecnicoRow>();
        session.ProjectInfo.OfertaPdf = new OfferPdfSettings();

        var service = new DocumentsWorkspaceService(session);
        var row = service.AddNew(AntecedenteTecnicoTipo.Plano);
        row.Codigo = "E-NEW";
        row.EsVigente = true;

        int added = service.SyncPlanosToOffer();

        Assert.Equal(1, added);
        Assert.Contains(session.ProjectInfo.OfertaPdf.Planos, p => p.Codigo == "E-NEW");
    }
}
