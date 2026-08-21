using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectMigrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public ProjectMigrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"qubicost-migration-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Ignorar errores de limpieza en el dispose de tests
        }
    }

    private sealed class FakeValidationService : IValidationService
    {
        public BudgetValidationResult ValidateProject(ProjectFileData project) => new BudgetValidationResult();
        public BudgetValidationResult ValidateBudget(ProjectInfoRow proyecto, System.Collections.Generic.IEnumerable<ItemizadoRow> itemizado, System.Collections.Generic.IEnumerable<MaterialRow> materiales, System.Collections.Generic.IEnumerable<CubicacionRow> cubicacion, System.Collections.Generic.IEnumerable<TemplateRow> plantillas) => new BudgetValidationResult();
        public BudgetValidationResult ValidateApu(ApuViewModel apu) => new BudgetValidationResult();
    }

    [Fact]
    public async Task OpenAsync_LegacyJsonAndSqliteSidecar_MigratesToSingleSqliteFileCorrectly()
    {
        // Arrange
        string legacyJsonPath = Path.Combine(_tempDirectory, "ProyectoLegacy.pepro");
        string legacySqlitePath = Path.Combine(_tempDirectory, "ProyectoLegacy.pepro.db");

        // 1. Crear metadatos JSON legacy (sólo metadatos del proyecto y estado vacío de colecciones)
        var legacyMetadata = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Presupuesto Migrado",
                Cliente = "Constructora Delta",
                Ubicacion = "Santiago"
            }
        };
        string jsonContent = JsonSerializer.Serialize(legacyMetadata, ProjectJsonSerializer.Options);
        await File.WriteAllTextAsync(legacyJsonPath, jsonContent);

        // 2. Crear datos SQLite sidecar con los detalles reales del itemizado y materiales
        var sqliteDetails = new ProjectFileData();
        sqliteDetails.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Tablero Eléctrico TDA" });
        sqliteDetails.Materiales.Add(new MaterialRow { Codigo = "M1", Descripcion = "Interruptor Termomagnético 16A" });
        
        // Guardamos el snapshot SQLite en el archivo sidecar
        SQLiteProjectStore.SaveSnapshot(legacySqlitePath, sqliteDetails);

        var session = new BudgetProjectSession();
        var repository = new ProjectRepository();
        var validation = new FakeValidationService();
        var workspaceService = new ProjectWorkspaceService(session, repository, validation);

        // Act
        await workspaceService.OpenAsync(legacyJsonPath);

        // Assert
        // El proyecto en sesión debe tener combinados los metadatos del JSON y los datos del SQLite sidecar
        Assert.Equal("Presupuesto Migrado", session.ProjectInfo.Nombre);
        Assert.Equal("Constructora Delta", session.ProjectInfo.Cliente);
        
        Assert.Single(session.Itemizado);
        Assert.Equal("Tablero Eléctrico TDA", session.Itemizado[0].Descripcion);
        
        Assert.Single(session.Materiales);
        Assert.Equal("Interruptor Termomagnético 16A", session.Materiales[0].Descripcion);

        // Comprobar que los archivos legacy se eliminaron y se creó el nuevo archivo SQLite unificado .qce
        string expectedUnifiedPath = Path.Combine(_tempDirectory, "ProyectoLegacy.qce");
        
        Assert.True(File.Exists(expectedUnifiedPath), "El archivo .qce unificado no se creó.");
        Assert.False(File.Exists(legacyJsonPath), "El archivo JSON legacy .pepro no se eliminó.");
        Assert.False(File.Exists(legacySqlitePath), "El archivo SQLite sidecar legacy .pepro.db no se eliminó.");
    }
}
