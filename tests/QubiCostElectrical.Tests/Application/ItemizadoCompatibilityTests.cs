using System.IO;
using System.Threading.Tasks;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoCompatibilityTests
{
    [Fact]
    public async Task Save_and_reload_preserves_itemizado_order_and_fields()
    {
        var session = new BudgetProjectSession();
        session.NewProject("compat", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Capítulo", EsTitulo = true });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1.1", Descripcion = "Partida A", Unidad = "UN", Cantidad = 2m });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00002", Item = "1.2", Descripcion = "Partida B", Unidad = "ML", Cantidad = 10.5m });

        string path = Path.Combine(Path.GetTempPath(), $"itemizado-compat-{Guid.NewGuid():N}.qce");
        try
        {
            var services = new AppServicesBuilder().Build();
            var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
            await workspace.SaveAsAsync(path);

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.Equal(3, session2.Itemizado.Count);
            Assert.Equal("1,0", session2.Itemizado[0].Item);
            Assert.Equal("Partida A", session2.Itemizado[1].Descripcion);
            Assert.Equal("ML", session2.Itemizado[2].Unidad);
            Assert.Equal(10.5m, session2.Itemizado[2].Cantidad);
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }
        }
    }

    [Fact]
    public async Task Import_replace_roundtrip_preserves_itemizado()
    {
        var session = new BudgetProjectSession();
        session.NewProject("import-rt", "Tester");
        var presenter = ItemizadoPresenterFactory.CreatePresenter(new AppServicesBuilder().Build(), session);

        presenter.CommitReplace(new[]
        {
            new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Capítulo", EsTitulo = true },
            new ItemizadoRow { Id = "1.0.00001", Item = "1.1", Descripcion = "Partida importada", Unidad = "UN", Cantidad = 3m }
        });

        string path = Path.Combine(Path.GetTempPath(), $"itemizado-import-rt-{Guid.NewGuid():N}.qce");
        try
        {
            var services = new AppServicesBuilder().Build();
            var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
            await workspace.SaveAsAsync(path);

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.Equal(2, session2.Itemizado.Count);
            Assert.True(session2.Itemizado[0].EsTitulo);
            Assert.Equal("Partida importada", session2.Itemizado[1].Descripcion);
            Assert.Equal(3m, session2.Itemizado[1].Cantidad);
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }
        }
    }

    [Fact]
    public async Task ExcluidoCubicacion_roundtrip_preserves_flag()
    {
        var session = new BudgetProjectSession();
        session.NewProject("excl-rt", "Tester");
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1,1",
            Descripcion = "Partida excluida",
            Unidad = "UN",
            Cantidad = 1m,
            ExcluidoCubicacion = true
        });

        string path = Path.Combine(Path.GetTempPath(), $"itemizado-excl-{Guid.NewGuid():N}.qce");
        try
        {
            var services = new AppServicesBuilder().Build();
            var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
            await workspace.SaveAsAsync(path);

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.True(session2.Itemizado[0].ExcluidoCubicacion);
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }
        }
    }
}
