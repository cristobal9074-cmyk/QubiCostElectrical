using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Verifica que SQLiteProjectStore guarda y carga proyectos sin pérdida de datos.
/// Usa archivos temporales que se limpian en cada test.
/// </summary>
public sealed class SQLiteProjectStoreTests : IDisposable
{
    private readonly string _dbPath;

    public SQLiteProjectStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesItemizado()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
        Assert.Equal(original.Itemizado[0].Id, loaded.Itemizado[0].Id);
        Assert.Equal(original.Itemizado[0].Descripcion, loaded.Itemizado[0].Descripcion);
        Assert.Equal(original.Itemizado[0].Unidad, loaded.Itemizado[0].Unidad);
        Assert.Equal(original.Itemizado[1].EsTitulo, loaded.Itemizado[1].EsTitulo);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesCubicacion()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Cubicacion.Count, loaded.Cubicacion.Count);
        Assert.Equal(original.Cubicacion[0].ItemId, loaded.Cubicacion[0].ItemId);
        Assert.Equal(original.Cubicacion[0].Codigo, loaded.Cubicacion[0].Codigo);
        Assert.Equal(original.Cubicacion[0].Cantidad, loaded.Cubicacion[0].Cantidad);
        Assert.Equal(original.Cubicacion[0].MatUf, loaded.Cubicacion[0].MatUf);
        Assert.Equal(original.Cubicacion[1].MoUf, loaded.Cubicacion[1].MoUf);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesNormalizedCompositionLayers()
    {
        var original = BuildTestData();
        original.Cubicacion[0].QtyExpressions = new[] { "10", "5" };
        original.Cubicacion[0].TotalExpression = "15";
        new CubicacionCompositionService().EnsureProject(original);

        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.CubicacionRecursos.Count, loaded.CubicacionRecursos.Count);
        Assert.Equal(original.CubicacionZonaCantidades.Count, loaded.CubicacionZonaCantidades.Count);
        Assert.Equal(original.CubicacionValorizaciones.Count, loaded.CubicacionValorizaciones.Count);
        Assert.Equal("10", loaded.CubicacionZonaCantidades.First(z => z.RecursoId == "C001" && z.ZonaOffset == 0).Expresion);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesTotalExpressionOnCubicacionRow()
    {
        var original = BuildTestData();
        original.Cubicacion[0].TotalExpression = "largo * 2";
        original.Cubicacion[0].QtyExpressions = Array.Empty<string>();

        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal("largo * 2", loaded.Cubicacion[0].TotalExpression);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesMateriales()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Materiales.Count, loaded.Materiales.Count);
        var mat = loaded.Materiales.First(m => m.Codigo == "MAT001");
        Assert.Equal("Cable NYY 2.5mm²", mat.Descripcion);
        Assert.Equal("ML", mat.Unidad);
        Assert.Equal(0.35m, mat.MatUf);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesProyecto()
    {
        var original = BuildTestData();
        original.Proyecto!.Nombre = "Test Proyecto Eléctrico";
        original.Proyecto.Cliente = "Constructora XYZ";
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal("Test Proyecto Eléctrico", loaded.Proyecto?.Nombre);
        Assert.Equal("Constructora XYZ", loaded.Proyecto?.Cliente);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesQtyHeaders()
    {
        var original = BuildTestData();
        original.QtyHeadersByItem["P001"] = new[] { "Piso 1", "Piso 2", "Piso 3" };
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.True(loaded.QtyHeadersByItem.ContainsKey("P001"));
        Assert.Equal(new[] { "Piso 1", "Piso 2", "Piso 3" }, loaded.QtyHeadersByItem["P001"]);
    }

    [Fact]
    public void SaveIncremental_CubicacionScope_UpdatesOnlyCubicacion()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        // Agrego una fila de cubicación nueva
        original.Cubicacion.Add(new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = "P001",
            Tipo = "MO",
            Codigo = "MO003",
            Descripcion = "Instalación adicional",
            Unidad = "GL",
            Cantidad = 1m,
            MoUf = 2.0m
        });

        SQLiteProjectStore.SaveIncremental(_dbPath, original, new[] { "cubicacion" });
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        // La cubicación nueva debe estar presente
        Assert.Equal(original.Cubicacion.Count, loaded.Cubicacion.Count);
        Assert.Contains(loaded.Cubicacion, c => c.Codigo == "MO003");
        // El itemizado no cambió
        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
    }

    [Fact]
    public void SaveIncremental_EmptyScopes_DoesNothing()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var modifiedData = BuildTestData();
        modifiedData.Itemizado.Clear(); // vacío, pero no se guarda porque scope vacío

        SQLiteProjectStore.SaveIncremental(_dbPath, modifiedData, Array.Empty<string>());
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        // El itemizado original debe seguir intacto
        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
    }

    [Fact]
    public void IsDatabaseHealthy_ReturnsTrueAfterSave()
    {
        SQLiteProjectStore.SaveSnapshot(_dbPath, BuildTestData());
        Assert.True(SQLiteProjectBackup.IsDatabaseHealthy(_dbPath));
    }

    [Fact]
    public void IsDatabaseHealthy_ReturnsFalseForNonexistent()
    {
        Assert.False(SQLiteProjectBackup.IsDatabaseHealthy("/tmp/no_existe_este_archivo.db"));
    }

    [Fact]
    public void GetLastSavedAt_ReturnsRecentTimestamp()
    {
        var before = DateTime.Now.AddSeconds(-2);
        SQLiteProjectStore.SaveSnapshot(_dbPath, BuildTestData());
        var after = DateTime.Now.AddSeconds(2);

        var savedAt = SQLiteProjectStore.GetLastSavedAt(_dbPath);

        Assert.True(savedAt >= before, $"savedAt={savedAt} debería ser >= {before}");
        Assert.True(savedAt <= after, $"savedAt={savedAt} debería ser <= {after}");
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesCompleteProjectInfo()
    {
        var before = DateTime.Now.AddSeconds(-2);
        var original = BuildProjectInfoRoundTripData();

        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var after = DateTime.Now.AddSeconds(2);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        var project = Assert.IsType<ProjectInfoRow>(loaded.Proyecto);

        Assert.Equal("Proyecto ProjectInfo completo", project.Nombre);
        Assert.Equal("Cliente ProjectInfo", project.Cliente);
        Assert.Equal("Obra Norte", project.Ubicacion);
        Assert.Equal("Hospital", project.TipoProyecto);
        Assert.Equal("Presupuestador", project.UsuarioNombre);
        Assert.Equal("Jefe", project.UsuarioRol);
        Assert.Equal(40768.69m, project.ValorUfClp);
        Assert.Equal("UF", project.MonedaPresupuesto);
        Assert.Equal(915.97m, project.TipoCambioUsdClp);
        Assert.Equal("Revision final", project.EstadoPresupuesto);
        Assert.False(project.AutoActualizarIndicadores);
        Assert.Equal(0m, project.GastosGeneralesPct);
        Assert.Equal(0m, project.UtilidadPct);
        Assert.Equal(0m, project.IvaPct);
        Assert.True(project.IncluirIvaEnTotal);
        Assert.Equal(3.5m, project.HerramientasPct);
        Assert.Equal(2.5m, project.MermasPct);
        Assert.Equal("APU", project.ApuCostingMode);
        Assert.Equal(1.25m, project.RiesgoPct);
        Assert.Equal(0.75m, project.DescuentoPct);
        Assert.Equal(1234.56m, project.MetrosCuadrados);
        Assert.Equal(90, project.PriceExpiryWarningDays);
        Assert.Equal(120, project.PriceExpiryCriticalDays);
        Assert.Equal(42m, project.Variables["FactorEspecial"]);

        Assert.InRange(project.LastSQLiteSavedAt, before, after);
    }

    [Fact]
    public void SaveIncremental_ProjectInfoScope_UpdatesProjectInfoAndSavedTimestamp()
    {
        var original = BuildProjectInfoRoundTripData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var firstLoaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        var firstSavedAt = firstLoaded.Proyecto!.LastSQLiteSavedAt;

        var modified = BuildProjectInfoRoundTripData();
        modified.Proyecto!.Nombre = "Proyecto ProjectInfo incremental";
        modified.Proyecto.Cliente = "Cliente modificado";
        modified.Proyecto.GastosGeneralesPct = 0m;
        modified.Proyecto.UtilidadPct = 7.25m;
        modified.Proyecto.IvaPct = 0m;
        modified.Proyecto.TipoProyecto = "Industrial";
        modified.Proyecto.LastSQLiteSavedAt = DateTime.MinValue;

        SQLiteProjectStore.SaveIncremental(_dbPath, modified, new[] { "project_info" });
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal("Proyecto ProjectInfo incremental", loaded.Proyecto?.Nombre);
        Assert.Equal("Cliente modificado", loaded.Proyecto?.Cliente);
        Assert.Equal(0m, loaded.Proyecto?.GastosGeneralesPct);
        Assert.Equal(7.25m, loaded.Proyecto?.UtilidadPct);
        Assert.Equal(0m, loaded.Proyecto?.IvaPct);
        Assert.Equal("Industrial", loaded.Proyecto?.TipoProyecto);
        Assert.True(loaded.Proyecto!.LastSQLiteSavedAt >= firstSavedAt);
        Assert.NotEqual(DateTime.MinValue, loaded.Proyecto.LastSQLiteSavedAt);
    }

    [Fact]
    public void SaveIncremental_MetaScopeWithoutProjectInfo_UpdatesLastSQLiteSavedAtInJson()
    {
        var original = BuildProjectInfoRoundTripData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var firstLoaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        var firstSavedAt = firstLoaded.Proyecto!.LastSQLiteSavedAt;

        Thread.Sleep(50);

        var modified = BuildProjectInfoRoundTripData();
        modified.Cubicacion =
        [
            new CubicacionRow
            {
                Id = "CUB-001",
                ItemId = "ITEM-001",
                Tipo = "MAT",
                Codigo = "MAT-001",
                Descripcion = "Cable incremental",
                Unidad = "ML",
                Cantidad = 2m,
                MatUf = 10m
            }
        ];
        modified.Proyecto!.LastSQLiteSavedAt = new DateTime(2020, 1, 1);

        var before = DateTime.Now.AddSeconds(-1);
        SQLiteProjectStore.SaveIncremental(_dbPath, modified, new[] { "cubicacion", "meta" });
        var after = DateTime.Now.AddSeconds(1);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        Assert.Single(loaded.Cubicacion);
        Assert.True(loaded.Proyecto!.LastSQLiteSavedAt > firstSavedAt);
        Assert.InRange(loaded.Proyecto.LastSQLiteSavedAt, before, after);
        Assert.NotEqual(new DateTime(2020, 1, 1), loaded.Proyecto.LastSQLiteSavedAt);
    }

    [Fact]
    public void SaveSnapshot_Twice_LastDataWins()
    {
        var v1 = BuildTestData();
        v1.Proyecto!.Nombre = "Versión 1";
        SQLiteProjectStore.SaveSnapshot(_dbPath, v1);

        var v2 = BuildTestData();
        v2.Proyecto!.Nombre = "Versión 2";
        SQLiteProjectStore.SaveSnapshot(_dbPath, v2);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        Assert.Equal("Versión 2", loaded.Proyecto?.Nombre);
    }

    // --- Error paths ---

    [Fact]
    public void LoadSnapshot_ArchivoInexistente_LanzaFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            SQLiteProjectStore.LoadSnapshot(Path.Combine(Path.GetTempPath(), $"no_existe_{Guid.NewGuid():N}.db")));
    }

    [Fact]
    public void SaveSnapshot_RutaVacia_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SQLiteProjectStore.SaveSnapshot("", BuildTestData()));
    }

    [Fact]
    public void LoadSnapshot_DBCorrupta_LanzaExcepcion()
    {
        string corruptPath = Path.Combine(Path.GetTempPath(), $"pe_corrupt_{Guid.NewGuid():N}.db");
        try
        {
            File.WriteAllBytes(corruptPath, new byte[] { 0x00, 0x01, 0x02, 0xFF });
            Assert.ThrowsAny<Exception>(() => SQLiteProjectStore.LoadSnapshot(corruptPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(corruptPath)) File.Delete(corruptPath); } catch (IOException) { /* lock SQLite liberado por el SO */ }
        }
    }

    [Fact]
    public void IsDatabaseHealthy_DBCorrupta_RetornaFalse()
    {
        string corruptPath = Path.Combine(Path.GetTempPath(), $"pe_corrupt_{Guid.NewGuid():N}.db");
        try
        {
            File.WriteAllBytes(corruptPath, new byte[] { 0x00, 0x01, 0x02, 0xFF });
            Assert.False(SQLiteProjectBackup.IsDatabaseHealthy(corruptPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(corruptPath)) File.Delete(corruptPath); } catch (IOException) { /* lock SQLite liberado por el SO */ }
        }
    }

    // --- Ciclo completo con cubicación compleja ---

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservaApus()
    {
        var original = BuildTestData();
        original.Apus.Add(new ApuRow
        {
            Id = "APU001",
            ItemId = "P001",
            Descripcion = "APU Canalización PVC",
            CantidadPartida = 50m
        });
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.Apus);
        Assert.Equal("APU001", loaded.Apus[0].Id);
        Assert.Equal(50m, loaded.Apus[0].CantidadPartida);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_CubicacionConMultiplesPartidas_TodosLosRegistros()
    {
        var original = BuildTestData();
        // Añadir cubicación compleja con múltiples partidas y tipos
        for (int i = 0; i < 20; i++)
        {
            original.Cubicacion.Add(new CubicacionRow
            {
                Id = $"C{100 + i:D3}",
                ItemId = $"P{i % 3 + 1:D3}",
                Tipo = i % 2 == 0 ? "MAT" : "MO",
                Codigo = $"COD{i:D3}",
                Descripcion = $"Material de prueba {i}",
                Unidad = "ML",
                Cantidad = i * 1.5m,
                MatUf = i * 0.1m,
                MoUf = i * 0.05m
            });
        }
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Cubicacion.Count, loaded.Cubicacion.Count);
        var lastLoaded = loaded.Cubicacion.Last();
        var lastOriginal = original.Cubicacion.Last();
        Assert.Equal(lastOriginal.Codigo, lastLoaded.Codigo);
        Assert.Equal(lastOriginal.Cantidad, lastLoaded.Cantidad);
        Assert.Equal(lastOriginal.MatUf, lastLoaded.MatUf);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservaZonasProyecto()
    {
        var original = BuildTestData();
        original.Zonas.Add(new ZonaProyecto { Id = "Z001", Nombre = "Piso 1" });
        original.Zonas.Add(new ZonaProyecto { Id = "Z002", Nombre = "Piso 2" });
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(2, loaded.Zonas.Count);
        Assert.Equal("Piso 1", loaded.Zonas[0].Nombre);
        Assert.Equal("Piso 2", loaded.Zonas[1].Nombre);
    }

    [Fact]
    public void SaveSnapshot_ThenLoad_PreservaCubicacionSC_RoundTrip()
    {
        var original = BuildTestData();
        original.CubicacionSC = new List<CuadroCargaSCRow>
        {
            new CuadroCargaSCRow
            {
                Id = "SC001",
                Nombre = "TDA SC",
                Descripcion = "Tablero sector A",
                CircuitosProyectados = 48,
                Circuitos = new List<CircuitoSCRow>
                {
                    new CircuitoSCRow
                    {
                        Id = "CIRC001",
                        Numero = 1,
                        Tipo = "Alumbrado",
                        DistanciaTablero = 15m,
                        LargoCircuito = 45m,
                        TipoTuberia = "PVCLH",
                        SeccionDerivacion = "4mm2",
                        Artefactos = new List<ArtefactoSCRow>
                        {
                            new ArtefactoSCRow { Nombre = "INT 9/12", Cantidad = 8 },
                            new ArtefactoSCRow { Nombre = "INT 9/15", Cantidad = 4 }
                        }
                    }
                }
            }
        };
        original.CubicacionScConfig = new CubicacionScConfig
        {
            TiposTuberia = new List<string> { "PVC", "EMT", "CUSTOM_TEST" },
            Sistemas = new List<SistemaSC>
            {
                new SistemaSC { Nombre = "Monofásico", NumConductores = 3m, MermaPct = 5m }
            }
        };

        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.CubicacionSC);
        var cuadro = loaded.CubicacionSC[0];
        Assert.Equal("SC001", cuadro.Id);
        Assert.Equal("TDA SC", cuadro.Nombre);
        Assert.Equal("Tablero sector A", cuadro.Descripcion);
        Assert.Equal(48, cuadro.CircuitosProyectados);
        Assert.Single(cuadro.Circuitos);

        var circ = cuadro.Circuitos[0];
        Assert.Equal("CIRC001", circ.Id);
        Assert.Equal(1, circ.Numero);
        Assert.Equal("Alumbrado", circ.Tipo);
        Assert.Equal(15m, circ.DistanciaTablero);
        Assert.Equal(45m, circ.LargoCircuito);
        Assert.Equal(2, circ.Artefactos.Count);
        Assert.Equal("INT 9/12", circ.Artefactos[0].Nombre);
        Assert.Equal(8m, circ.Artefactos[0].Cantidad);

        Assert.NotNull(loaded.CubicacionScConfig);
        Assert.Contains("CUSTOM_TEST", loaded.CubicacionScConfig.TiposTuberia);
        Assert.Single(loaded.CubicacionScConfig.Sistemas);
        Assert.Equal("Monofásico", loaded.CubicacionScConfig.Sistemas[0].Nombre);
    }

    [Fact]
    public void SaveIncremental_CubicacionScScope_ActualizaCuadrosYConfig()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        original.CubicacionSC = new List<CuadroCargaSCRow>
        {
            new CuadroCargaSCRow { Id = "SC-INC-01", Nombre = "TDA Incremental", CircuitosProyectados = 24 }
        };
        original.CubicacionScConfig = new CubicacionScConfig
        {
            TiposTuberia = new List<string> { "INCREMENTAL_TIPO" }
        };

        SQLiteProjectStore.SaveIncremental(_dbPath, original, new[] { "cubicacion_sc" });
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.CubicacionSC);
        Assert.Equal("SC-INC-01", loaded.CubicacionSC[0].Id);
        Assert.Equal("TDA Incremental", loaded.CubicacionSC[0].Nombre);
        Assert.Equal(24, loaded.CubicacionSC[0].CircuitosProyectados);
        Assert.Contains("INCREMENTAL_TIPO", loaded.CubicacionScConfig.TiposTuberia);

        // El resto del proyecto no se tocó
        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
    }

    [Fact]
    public void SaveSnapshot_LegacyData_RoundTrip_PreservesVersionesOrdenesCambio()
    {
        var original = BuildLegacyProjectData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.Versiones);
        Assert.Equal("V-LEG-01", loaded.Versiones[0].Codigo);
        Assert.Equal("Revision cliente", loaded.Versiones[0].Nombre);

        Assert.Equal(2, loaded.OrdenesCambio.Count);
        Assert.Equal("OC-APROB", loaded.OrdenesCambio[0].Numero);
        Assert.Equal("Aprobada", loaded.OrdenesCambio[0].Estado);
        Assert.Equal(12.5m, loaded.OrdenesCambio[0].DeltaCostoUf);
        Assert.Equal("OC-BORR", loaded.OrdenesCambio[1].Numero);
        Assert.Equal("Borrador", loaded.OrdenesCambio[1].Estado);
    }

    [Fact]
    public void SaveSnapshot_LegacyData_MigrateToNewDatabase()
    {
        var original = BuildLegacyProjectData();
        string sourceDb = Path.Combine(Path.GetTempPath(), $"pe_legacy_src_{Guid.NewGuid():N}.db");
        string targetDb = Path.Combine(Path.GetTempPath(), $"pe_legacy_dst_{Guid.NewGuid():N}.db");
        try
        {
            SQLiteProjectStore.SaveSnapshot(sourceDb, original);
            var loaded = SQLiteProjectStore.LoadSnapshot(sourceDb);

            loaded.Proyecto!.Nombre = "Proyecto migrado";
            SQLiteProjectStore.SaveSnapshot(targetDb, loaded);

            var migrated = SQLiteProjectStore.LoadSnapshot(targetDb);
            Assert.Equal("Proyecto migrado", migrated.Proyecto?.Nombre);
            Assert.Single(migrated.Versiones);
            Assert.Equal(2, migrated.OrdenesCambio.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (string path in new[] { sourceDb, targetDb })
            {
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            }
        }
    }



    [Fact]
    public void SaveSnapshot_ThenLoad_PreservesConjuntosAntecedentesAndApuCondiciones()
    {
        var original = BuildExtendedSnapshotData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.ConjuntosElectricos);
        Assert.Equal("PE-01", loaded.ConjuntosElectricos[0].Codigo);
        Assert.Single(loaded.ConjuntosElectricos[0].Recursos);
        Assert.Equal("MAT001", loaded.ConjuntosElectricos[0].Recursos[0].Codigo);

        Assert.Single(loaded.Proyecto!.AntecedentesTecnicos);
        Assert.Equal("EL-001", loaded.Proyecto.AntecedentesTecnicos[0].Codigo);
        Assert.Equal(8m, loaded.Proyecto.LaborDaySettings.HorasPorJornada);

        Assert.Single(loaded.Apus);
        Assert.Equal(15m, loaded.Apus[0].CondicionesLaborales.FactorAlturaPct);
        Assert.True(loaded.Apus[0].CondicionesLaborales.EdificioEnOperacion);
    }

    [Fact]
    public void SaveIncremental_ConjuntosScope_UpdatesWithoutFullSave()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        original.ConjuntosElectricos =
        [
            new ConjuntoElectricoRow
            {
                Id = "CE1",
                Codigo = "PE-01",
                Nombre = "Punto enchufe",
                Recursos = [new ConjuntoRecursoRow { Id = "R1", Codigo = "MAT001", Rendimiento = 2m }]
            }
        ];

        SQLiteProjectStore.SaveIncremental(_dbPath, original, new[] { "conjuntos_electricos" });
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.ConjuntosElectricos);
        Assert.Equal("PE-01", loaded.ConjuntosElectricos[0].Codigo);
        Assert.Equal(2m, loaded.ConjuntosElectricos[0].Recursos[0].Rendimiento);
    }

    [Fact]
    public void SaveIncremental_ProjectInfoScope_PreservesAntecedentesTecnicos()
    {
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        original.Proyecto!.AntecedentesTecnicos =
        [
            new AntecedenteTecnicoRow
            {
                Id = "AT1",
                Codigo = "EL-002",
                Tipo = AntecedenteTecnicoTipo.Especificacion,
                Descripcion = "Especificación tableros"
            }
        ];
        original.Proyecto.LaborDaySettings = new LaborDaySettingsRow { HorasPorJornada = 9m };

        SQLiteProjectStore.SaveIncremental(_dbPath, original, new[] { "project_info" });
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Single(loaded.Proyecto!.AntecedentesTecnicos);
        Assert.Equal("EL-002", loaded.Proyecto.AntecedentesTecnicos[0].Codigo);
        Assert.Equal(9m, loaded.Proyecto.LaborDaySettings.HorasPorJornada);
    }

    private static ProjectFileData BuildExtendedSnapshotData()
    {
        var data = BuildTestData();
        data.Proyecto!.AntecedentesTecnicos =
        [
            new AntecedenteTecnicoRow
            {
                Id = "AT1",
                Codigo = "EL-001",
                Tipo = AntecedenteTecnicoTipo.Plano,
                Descripcion = "Plano eléctrico"
            }
        ];
        data.Proyecto.LaborDaySettings = new LaborDaySettingsRow { HorasPorJornada = 8m, DiasPorSemana = 5m };
        data.ConjuntosElectricos =
        [
            new ConjuntoElectricoRow
            {
                Id = "CE1",
                Codigo = "PE-01",
                Nombre = "Punto enchufe",
                Recursos = [new ConjuntoRecursoRow { Id = "R1", Codigo = "MAT001", Rendimiento = 3m }]
            }
        ];
        data.Apus =
        [
            new ApuRow
            {
                Id = "APU1",
                ItemId = "P001",
                Descripcion = "APU test",
                CondicionesLaborales = new ApuCondicionesLaboralesRow
                {
                    FactorAlturaPct = 15m,
                    EdificioEnOperacion = true
                }
            }
        ];
        return data;
    }

    private static ProjectFileData BuildLegacyProjectData()
    {
        var data = BuildTestData();

        data.Versiones =
        [
            new BudgetVersionRow
            {
                Id = "VER-LEG-01",
                Codigo = "V-LEG-01",
                Nombre = "Revision cliente",
                Estado = "Preliminar",
                Snapshot = new BudgetVersionData()
            }
        ];
        data.OrdenesCambio =
        [
            new ChangeOrderRow { Id = "OC-1", Numero = "OC-APROB", Estado = "Aprobada", DeltaCostoUf = 12.5m },
            new ChangeOrderRow { Id = "OC-2", Numero = "OC-BORR", Estado = "Borrador", DeltaCostoUf = 999m }
        ];
        data.ActiveVersionId = "VER-LEG-01";
        return data;
    }

    private static ProjectFileData BuildTestData()
    {
        string p1Id = "P001";
        string p2Id = "P002";

        return new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto Test",
                Cliente = "Cliente Test",
                GastosGeneralesPct = 15m,
                UtilidadPct = 10m
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "TIT001", Item = "1", Descripcion = "CAPÍTULO 1", Unidad = "", Cantidad = 0m, EsTitulo = true, NivelJerarquico = 1 },
                new() { Id = p1Id,    Item = "1.1", Descripcion = "Canalización PVC 20mm", Unidad = "ML", Cantidad = 50m, EsTitulo = false },
                new() { Id = p2Id,    Item = "1.2", Descripcion = "Tablero distribución 12 circuitos", Unidad = "UN", Cantidad = 2m, EsTitulo = false }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C001", ItemId = p1Id, Tipo = "MAT", Codigo = "MAT001", Descripcion = "Cable NYY 2.5mm²", Unidad = "ML", Cantidad = 50m, MatUf = 0.35m, MoUf = 0m },
                new() { Id = "C002", ItemId = p1Id, Tipo = "MO",  Codigo = "MO001",  Descripcion = "Instalación canalización", Unidad = "ML", Cantidad = 50m, MatUf = 0m, MoUf = 0.15m },
                new() { Id = "C003", ItemId = p2Id, Tipo = "MAT", Codigo = "MAT002", Descripcion = "Tablero 12 ctos", Unidad = "UN", Cantidad = 2m, MatUf = 12.0m, MoUf = 0m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT001", Descripcion = "Cable NYY 2.5mm²", Unidad = "ML", Categoria = "Cables", MatUf = 0.35m, MoUf = 0m },
                new() { Codigo = "MAT002", Descripcion = "Tablero 12 ctos", Unidad = "UN", Categoria = "Tableros", MatUf = 12.0m, MoUf = 0m },
                new() { Codigo = "MO001",  Descripcion = "Mano de obra electricista", Unidad = "HH", Categoria = "Mano de obra", MatUf = 0m, MoUf = 0.08m }
            },
            QtyHeadersByItem = new Dictionary<string, string[]>()
        };
    }

    private static ProjectFileData BuildProjectInfoRoundTripData()
    {
        var data = BuildTestData();
        data.Proyecto = new ProjectInfoRow
        {
            Nombre = "Proyecto ProjectInfo completo",
            Cliente = "Cliente ProjectInfo",
            Ubicacion = "Obra Norte",
            TipoProyecto = "Hospital",
            UsuarioNombre = "Presupuestador",
            UsuarioRol = "Jefe",
            FechaCreacion = new DateTime(2026, 6, 1, 8, 30, 0),
            FechaModificacion = new DateTime(2026, 6, 10, 9, 45, 0),
            ValorUfClp = 40768.69m,
            MonedaPresupuesto = "UF",
            TipoCambioUsdClp = 915.97m,
            FechaTipoCambio = new DateTime(2026, 6, 11),
            AutoActualizarIndicadores = false,
            EstadoPresupuesto = "Revision final",
            GastosGeneralesPct = 0m,
            UtilidadPct = 0m,
            HerramientasPct = 3.5m,
            MermasPct = 2.5m,
            ApuCostingMode = "APU",
            RiesgoPct = 1.25m,
            DescuentoPct = 0.75m,
            IvaPct = 0m,
            IncluirIvaEnTotal = true,
            MetrosCuadrados = 1234.56m,
            LastSQLiteSavedAt = new DateTime(2026, 5, 1, 12, 0, 0),
            PriceExpiryWarningDays = 90,
            PriceExpiryCriticalDays = 120
        };

        data.Proyecto.Variables["FactorEspecial"] = 42m;

        return data;
    }
}
