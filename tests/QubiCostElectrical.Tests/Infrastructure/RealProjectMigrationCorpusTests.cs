using System;
using System.IO;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
using QubiCostElectrical.Tests.Fixtures;
using QubiCostElectrical.Tests.Fixtures.MigrationCorpus;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Batería de pruebas sobre el corpus de migraciones SQLite con bases de datos reales.
/// Garantiza que ningún cliente en obra sufra caídas ni pérdida de datos ante cambios de versión.
/// </summary>
public sealed class RealProjectMigrationCorpusTests : IDisposable
{
    private readonly string _tempDir;

    public RealProjectMigrationCorpusTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"qubi_corpus_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Dispose silencioso en limpieza de tests
        }
    }

    public static IEnumerable<object[]> HistoricalSchemas()
    {
        for (int version = 1; version <= 32; version++)
            yield return new object[] { version };
    }

    [Theory]
    [MemberData(nameof(HistoricalSchemas))]
    public void Migration_FromHistoricalMilestones_ToSchema33_ZeroDataLoss_IntegrityCheckOk(int historicalVersion)
    {
        string dbPath = Path.Combine(_tempDir, $"project_v{historicalVersion}.qce");
        var originalData = PEL039ReferenceProjectFixture.CreateSyntheticReferenceProject();

        // 1. Crear el archivo SQLite físico real en el esquema histórico indicado
        if (historicalVersion == 1)
        {
            MigrationCorpusGenerator.CreateSchemaV1LegacyDatabase(dbPath, originalData);
        }
        else if (historicalVersion == 14)
        {
            MigrationCorpusGenerator.CreateSchemaV14Database(dbPath, originalData);
        }
        else
        {
            MigrationCorpusGenerator.CreateHistoricalDatabase(dbPath, historicalVersion, originalData);
        }

        // 2. Ejecutar la migración al esquema vigente (v33) a través de LoadSnapshot
        var loaded = SQLiteProjectStore.LoadSnapshot(dbPath);

        // 3. Aserciones de Preservación de Datos (Zero Data Loss)
        Assert.NotNull(loaded.Proyecto);
        Assert.Equal(originalData.Proyecto?.Nombre, loaded.Proyecto?.Nombre);
        Assert.Equal(originalData.Itemizado.Count, loaded.Itemizado.Count);
        Assert.Equal(originalData.Materiales.Count, loaded.Materiales.Count);
        Assert.Equal(originalData.Cubicacion.Count, loaded.Cubicacion.Count);

        // 4. Aserciones de Integridad Física de SQLite
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal(SQLiteSchemaMigrator.LatestSchemaVersion, Convert.ToInt32(cmd.ExecuteScalar() ?? 0));

        cmd.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", Convert.ToString(cmd.ExecuteScalar()));

        cmd.CommandText = "PRAGMA foreign_key_check;";
        using var reader = cmd.ExecuteReader();
        Assert.False(reader.HasRows, "Se encontraron violaciones de llaves foráneas tras la migración.");

        // 5. Verificar que se creó el respaldo preventivo en .schema_backups
        string backupDir = Path.Combine(_tempDir, Path.GetFileName(dbPath) + ".schema_backups");
        Assert.True(Directory.Exists(backupDir), "No se creó el directorio de respaldos preventivos .schema_backups.");
        Assert.NotEmpty(Directory.GetFiles(backupDir, "*.bak"));
    }

    [Fact]
    public void Migration_IsStrictlyIdempotent_ReRunningMigrationCausesNoChangesOrErrors()
    {
        string dbPath = Path.Combine(_tempDir, "idempotency_test.qce");
        var data = PEL039ReferenceProjectFixture.CreateSyntheticReferenceProject();

        MigrationCorpusGenerator.CreateHistoricalDatabase(dbPath, 25, data);

        // Primera migración
        var firstLoad = SQLiteProjectStore.LoadSnapshot(dbPath);
        Assert.Equal(data.Itemizado.Count, firstLoad.Itemizado.Count);

        // Segunda migración consecutiva sobre el archivo ya en v33
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            // Debe ser un no-op sin excepciones
            SQLiteSchemaMigrator.MigrateToLatest(conn);
        }

        var secondLoad = SQLiteProjectStore.LoadSnapshot(dbPath);
        Assert.Equal(firstLoad.Itemizado.Count, secondLoad.Itemizado.Count);
        Assert.Equal(firstLoad.Cubicacion.Count, secondLoad.Cubicacion.Count);
        Assert.Equal(firstLoad.Materiales.Count, secondLoad.Materiales.Count);
    }

    [Fact]
    public void Migration_ForeignHigherVersion_CLINICAMEDS_EnsuresOwnTablesWithoutCorrupting()
    {
        // Simula bases como CLINICA MEDS.qce que poseen user_version = 60
        string dbPath = Path.Combine(_tempDir, "clinica_meds_sim.qce");
        var data = PEL039ReferenceProjectFixture.CreateSyntheticReferenceProject();

        MigrationCorpusGenerator.CreateHistoricalDatabase(dbPath, 33, data);

        // Forzar versión a 60
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 60;";
            cmd.ExecuteNonQuery();

            // Ejecutar migración: debe llamar a EnsureOwnTablesExist y preservar versión 60
            SQLiteSchemaMigrator.MigrateToLatest(conn);

            cmd.CommandText = "PRAGMA user_version;";
            Assert.Equal(60, Convert.ToInt32(cmd.ExecuteScalar() ?? 0));

            cmd.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", Convert.ToString(cmd.ExecuteScalar()));
        }

        var loaded = SQLiteProjectStore.LoadSnapshot(dbPath);
        Assert.Equal(data.Itemizado.Count, loaded.Itemizado.Count);
    }

    [Fact]
    public void ReferenceProject_PEL039A2026_CommittedCorpus_SurvivesMigrationAndRoundtrip()
    {
        string corpusPath = RequireCommittedCorpus();
        Assert.True(new FileInfo(corpusPath).Length > 1_000_000, "El corpus commiteado es demasiado chico para ser PEL039A2026 ABIERTO.qce.");
        string testPath = Path.Combine(_tempDir, "PEL039A2026-ABIERTO.qce");
        File.Copy(corpusPath, testPath, overwrite: true);

        var before = ReadCorpusFingerprint(testPath);
        Assert.True(before.Itemizado > 0, "El corpus commiteado no tiene partidas.");

        var loaded = SQLiteProjectStore.LoadSnapshot(testPath);

        Assert.Equal(before.Nombre, loaded.Proyecto?.Nombre);
        Assert.Equal(before.Itemizado, loaded.Itemizado.Count);
        Assert.Equal(before.Materiales, loaded.Materiales.Count);
        Assert.Equal(before.Cubicacion, loaded.Cubicacion.Count);
        Assert.Contains(loaded.Itemizado, row => row.Descripcion == before.FirstItemDescripcion);

        using (var conn = new SqliteConnection($"Data Source={testPath};Mode=ReadOnly"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            int version = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            Assert.True(version >= before.UserVersion, "La migración bajó el esquema del archivo de obra.");
            Assert.True(version <= SQLiteSchemaMigrator.LatestSchemaVersion || before.UserVersion > SQLiteSchemaMigrator.LatestSchemaVersion);
            cmd.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", Convert.ToString(cmd.ExecuteScalar()));
        }

        Assert.True(SQLiteProjectBackup.IsDatabaseHealthy(testPath));

        string roundtripPath = Path.Combine(_tempDir, "PEL039A2026_Roundtrip.qce");
        SQLiteProjectStore.SaveSnapshot(roundtripPath, loaded);
        var roundtripLoaded = SQLiteProjectStore.LoadSnapshot(roundtripPath);
        Assert.Equal(loaded.Itemizado.Count, roundtripLoaded.Itemizado.Count);
        Assert.Equal(loaded.Cubicacion.Count, roundtripLoaded.Cubicacion.Count);
        Assert.Equal(loaded.Materiales.Count, roundtripLoaded.Materiales.Count);
        Assert.Equal(loaded.Proyecto?.Nombre, roundtripLoaded.Proyecto?.Nombre);
        Assert.Contains(roundtripLoaded.Itemizado, row => row.Descripcion == before.FirstItemDescripcion);
    }

    private static string RequireCommittedCorpus()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string nested = Path.Combine(dir.FullName, "tests", "QubiCostElectrical.Tests", "Fixtures", "MigrationCorpus", "corpus", "PEL039A2026-ABIERTO.qce");
            if (File.Exists(nested))
                return nested;
            string local = Path.Combine(dir.FullName, "Fixtures", "MigrationCorpus", "corpus", "PEL039A2026-ABIERTO.qce");
            if (File.Exists(local))
                return local;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Falta tests/QubiCostElectrical.Tests/Fixtures/MigrationCorpus/corpus/PEL039A2026-ABIERTO.qce. Sin esa copia del proyecto de obra, CI no detecta una migración que borre filas.");
    }

    private readonly record struct CorpusFingerprint(int UserVersion, int Itemizado, int Materiales, int Cubicacion, string Nombre, string FirstItemDescripcion);

    private static CorpusFingerprint ReadCorpusFingerprint(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        int version = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        cmd.CommandText = "SELECT COUNT(*) FROM itemizado;";
        int itemizado = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        cmd.CommandText = "SELECT COUNT(*) FROM materiales;";
        int materiales = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        cmd.CommandText = "SELECT COUNT(*) FROM cubicacion;";
        int cubicacion = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        cmd.CommandText = "SELECT nombre FROM project_info LIMIT 1;";
        string nombre = Convert.ToString(cmd.ExecuteScalar()) ?? "";
        cmd.CommandText = "SELECT descripcion FROM itemizado ORDER BY sort_order LIMIT 1;";
        string first = Convert.ToString(cmd.ExecuteScalar()) ?? "";
        return new CorpusFingerprint(version, itemizado, materiales, cubicacion, nombre, first);
    }

    [Fact]
    public void Migration_OnStepFailure_AutoRestoresBackup_LeavingDatabaseIntact()
    {
        string dbPath = Path.Combine(_tempDir, "failure_recovery_test.qce");
        var data = PEL039ReferenceProjectFixture.CreateSyntheticReferenceProject();

        // 1. Crear base en v25
        MigrationCorpusGenerator.CreateHistoricalDatabase(dbPath, 25, data);

        // 2. Inyectar un trigger saboteador en schema_migrations para simular fallo en v26
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    applied_at TEXT NOT NULL,
                    app_version TEXT NOT NULL,
                    note TEXT NOT NULL DEFAULT ''
                );
                CREATE TRIGGER trg_fail_on_v26 BEFORE INSERT ON schema_migrations
                WHEN NEW.version = 26
                BEGIN
                    SELECT RAISE(FAIL, 'Fallo simulado de disco o red durante paso v26');
                END;
                """;
            cmd.ExecuteNonQuery();
        }

        // 3. Ejecutar migración: debe arrojar InvalidOperationException pero auto-restaurar
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            var ex = Assert.Throws<InvalidOperationException>(() => SQLiteSchemaMigrator.MigrateToLatest(conn));
            Assert.Contains("auto-restaurada de forma segura", ex.Message);

            // 4. Validar que la base de datos volvió intacta a v25
            using var verifyCmd = conn.CreateCommand();
            verifyCmd.CommandText = "PRAGMA user_version;";
            Assert.Equal(25, Convert.ToInt32(verifyCmd.ExecuteScalar() ?? 0));

            verifyCmd.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", Convert.ToString(verifyCmd.ExecuteScalar()));
        }

        // 5. Retirar el trigger saboteador y verificar que ahora sí puede migrar y cargar todos los datos intactos
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TRIGGER IF EXISTS trg_fail_on_v26;";
            cmd.ExecuteNonQuery();
        }

        var recovered = SQLiteProjectStore.LoadSnapshot(dbPath);
        Assert.Equal(data.Itemizado.Count, recovered.Itemizado.Count);
        Assert.Equal(data.Materiales.Count, recovered.Materiales.Count);
    }
}
