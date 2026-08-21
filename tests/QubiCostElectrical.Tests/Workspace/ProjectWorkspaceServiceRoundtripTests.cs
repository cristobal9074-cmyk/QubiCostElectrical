using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Workspace;

public sealed class ProjectWorkspaceServiceRoundtripTests
{
    [Fact]
    public async Task SaveAsAndOpen_PreservesProjectInfoFields()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("roundtrip");
        session.ProjectInfo.Nombre = "Proyecto WPF Roundtrip";
        session.ProjectInfo.Cliente = "Cliente QA";
        session.ProjectInfo.MetrosCuadrados = 1250m;
        session.ProjectInfo.OfertaPdf.ScopeText = "Alcance persistido";
        session.MarkDirty("test");

        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
        string path = Path.Combine(Path.GetTempPath(), $"qce-wpf-{Guid.NewGuid():N}.qce");

        try
        {
            await workspace.SaveAsAsync(path);

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.Equal("Proyecto WPF Roundtrip", session2.ProjectInfo.Nombre);
            Assert.Equal("Cliente QA", session2.ProjectInfo.Cliente);
            Assert.Equal(1250m, session2.ProjectInfo.MetrosCuadrados);
            Assert.Equal("Alcance persistido", session2.ProjectInfo.OfertaPdf.ScopeText);
            Assert.False(session2.IsDirty);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }

    [Fact]
    public async Task Open_WhenJsonIsCorrupted_UsesBackup()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("backup");
        session.ProjectInfo.Nombre = "Proyecto con respaldo";
        session.ProjectInfo.Cliente = "Cliente Respaldo";
        session.MarkDirty("test");

        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
        string path = Path.Combine(Path.GetTempPath(), $"qce-backup-{Guid.NewGuid():N}.qce");
        string backupPath = "";

        try
        {
            await workspace.SaveAsAsync(path);

            backupPath = await services.ProjectRepository.CreateNumberedSQLiteBackupAsync(path, 6, "Test backup");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                stream.SetLength(32);
            }

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.Equal("Proyecto con respaldo", session2.ProjectInfo.Nombre);
            Assert.Equal("Cliente Respaldo", session2.ProjectInfo.Cliente);
            Assert.False(session2.IsDirty);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch (Exception)
            {
            }
        }
    }

    [Fact]
    public async Task Open_WithProjectFolder_ResolvesProjectFileInsideFolder()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("folder-open");
        session.ProjectInfo.Nombre = "Proyecto en carpeta";
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 12m
        });

        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
        string folder = Path.Combine(Path.GetTempPath(), $"qce-folder-{Guid.NewGuid():N}");
        string path = Path.Combine(folder, "ProyectoEnCarpeta.qce");

        try
        {
            Directory.CreateDirectory(folder);
            await workspace.SaveAsAsync(path);

            var session2 = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(session2, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(folder);

            Assert.Equal("Proyecto en carpeta", session2.ProjectInfo.Nombre);
            Assert.Single(session2.Itemizado);
            Assert.Equal("Canalización", session2.Itemizado[0].Descripcion);
            Assert.Equal(path, session2.CurrentProjectPath);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (Exception)
            {
            }
        }
    }
}
