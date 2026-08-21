using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Workspace;

public sealed class ProjectFormatPolicyTests
{
    [Fact]
    public async Task SaveAs_WhenExtensionIsMissing_CreatesQceFile()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("policy");
        session.ProjectInfo.Nombre = "Proyecto Política";

        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
        var basePath = Path.Combine(Path.GetTempPath(), $"policy-save-{Guid.NewGuid():N}");
        var expectedPath = basePath + ".qce";

        try
        {
            await workspace.SaveAsAsync(basePath);

            Assert.True(File.Exists(expectedPath));
            Assert.Equal(expectedPath, session.CurrentProjectPath);
            Assert.False(File.Exists(basePath + ".pepro"));
        }
        finally
        {
            try
            {
                if (File.Exists(expectedPath))
                    File.Delete(expectedPath);
                if (File.Exists(basePath))
                    File.Delete(basePath);
            }
            catch (Exception)
            {
            }
        }
    }

    [Fact]
    public async Task SaveAs_WhenLegacyExtensionIsProvided_CreatesQceFile()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("policy");
        session.ProjectInfo.Nombre = "Proyecto Política";

        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
        var basePath = Path.Combine(Path.GetTempPath(), $"policy-save-legacy-{Guid.NewGuid():N}.pepro");
        var expectedPath = Path.ChangeExtension(basePath, ".qce");

        try
        {
            await workspace.SaveAsAsync(basePath);

            Assert.True(File.Exists(expectedPath));
            Assert.Equal(expectedPath, session.CurrentProjectPath);
            Assert.False(File.Exists(basePath));
        }
        finally
        {
            try
            {
                if (File.Exists(expectedPath))
                    File.Delete(expectedPath);
                if (File.Exists(basePath))
                    File.Delete(basePath);
            }
            catch (Exception)
            {
            }
        }
    }

    [Fact]
    public async Task Open_LegacyPepro_ImportsAndMigratesToQce()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);

        var legacyPath = Path.Combine(Path.GetTempPath(), $"policy-open-{Guid.NewGuid():N}.pepro");
        var migratedPath = Path.ChangeExtension(legacyPath, ".qce");
        var originalJson = """
        {
          "Proyecto": {
            "Nombre": "Proyecto legado",
            "Cliente": "Cliente legado",
            "Ubicacion": "Obra legado"
          }
        }
        """;

        try
        {
            await File.WriteAllTextAsync(legacyPath, originalJson);
            await workspace.OpenAsync(legacyPath);

            Assert.False(File.Exists(legacyPath));
            Assert.True(File.Exists(migratedPath));
            Assert.Equal(migratedPath, session.CurrentProjectPath);
            Assert.Equal("Proyecto legado", session.ProjectInfo.Nombre);
            Assert.Equal("Cliente legado", session.ProjectInfo.Cliente);
            Assert.Equal("Obra legado", session.ProjectInfo.Ubicacion);
            Assert.False(session.IsDirty);
        }
        finally
        {
            try
            {
                if (File.Exists(legacyPath))
                    File.Delete(legacyPath);
                if (File.Exists(migratedPath))
                    File.Delete(migratedPath);
                var backup = legacyPath + ".bak";
                if (File.Exists(backup))
                    File.Delete(backup);
            }
            catch (Exception)
            {
            }
        }
    }

    [Fact]
    public async Task Open_InvalidQce_Rejected()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);

        var path = Path.Combine(Path.GetTempPath(), $"policy-invalid-{Guid.NewGuid():N}.qce");
        try
        {
            await File.WriteAllTextAsync(path, "{ not valid ");
            await Assert.ThrowsAsync<InvalidDataException>(() => workspace.OpenAsync(path));
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
    public async Task Open_QceSqlite_LoadsSnapshot()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);

        var path = Path.Combine(Path.GetTempPath(), $"policy-sqlite-{Guid.NewGuid():N}.qce");
        try
        {
            session.NewProject("policy");
            session.ProjectInfo.Nombre = "Proyecto SQLite";
            await workspace.SaveAsAsync(path);

            var reload = new BudgetProjectSession();
            var workspace2 = new ProjectWorkspaceService(reload, services.ProjectRepository, services.Validation);
            await workspace2.OpenAsync(path);

            Assert.Equal("Proyecto SQLite", reload.ProjectInfo.Nombre);
            Assert.Equal(path, reload.CurrentProjectPath);
            Assert.False(reload.IsDirty);
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
}
