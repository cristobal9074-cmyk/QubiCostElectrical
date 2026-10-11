using System;
using System.IO;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using QubiCostElectrical.Tests.Fixtures;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Test canónico de persistencia: garantiza que un proyecto completo sobrevive
/// save/load SQLite sin pérdida de datos (snapshot completo e incremental multi-scope).
/// </summary>
public sealed class SQLiteProjectRoundTripTests : IDisposable
{
    private readonly string _dbPath;

    public SQLiteProjectRoundTripTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_roundtrip_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_FullProject_RoundTrip_PreservesAllData()
    {
        var original = ProjectFileDataFixtures.BuildFullProject();

        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        ProjectFileDataAssertions.AssertEquivalent(original, loaded);
    }

    [Fact]
    public void SaveSnapshot_ThenIncrementalScopes_ThenLoad_FullProject_RoundTrip_PreservesAllData()
    {
        var baseline = ProjectFileDataFixtures.BuildFullProject();
        SQLiteProjectStore.SaveSnapshot(_dbPath, baseline);

        var modified = ProjectFileDataFixtures.BuildFullProjectModified();

        string[] scopes =
        [
            "project_info",
            "itemizado",
            "materiales",
            "cubicacion",
            "apu",
            "qty_headers",
            "plantillas",
            "conjuntos_electricos",
            "calculadora",
            "versiones",
            "ordenes_cambio",
            "zonas",
            "mediciones",
            "cubicacion_sc",
            "estados_pago"
        ];

        foreach (string scope in scopes)
            SQLiteProjectStore.SaveIncremental(_dbPath, modified, new[] { scope });

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        ProjectFileDataAssertions.AssertEquivalent(modified, loaded);
    }
}
