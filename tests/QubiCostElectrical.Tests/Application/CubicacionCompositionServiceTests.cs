using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionCompositionServiceTests
{
    private readonly CubicacionCompositionService _service = new();

    private static ProjectFileData ProjectWithLegacyRow()
    {
        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "CABLE2.5",
            Descripcion = "Cable 2,5 mm",
            Unidad = "ML",
            Cantidad = 15m,
            MatUf = 0.08m,
            MoUf = 0.02m,
            QtyExpressions = new[] { "10", "5" }
        };

        return new ProjectFileData
        {
            Cubicacion = new List<CubicacionRow> { row },
            Zonas = new List<ZonaProyecto>
            {
                new() { Id = "Z1", Nombre = "Piso 1", Orden = 0, Activa = true },
                new() { Id = "Z2", Nombre = "Piso 2", Orden = 1, Activa = true }
            }
        };
    }

    [Fact]
    public void EnsureProject_DecomposesLegacyCubicacionIntoLayers()
    {
        var data = ProjectWithLegacyRow();

        _service.EnsureProject(data);

        Assert.Single(data.CubicacionRecursos);
        Assert.Equal("CABLE2.5", data.CubicacionRecursos[0].Codigo);
        Assert.Single(data.CubicacionValorizaciones);
        Assert.Equal(0.08m, data.CubicacionValorizaciones[0].MatUf);
        Assert.Equal(0m, data.CubicacionValorizaciones[0].MatClp); // UF legacy sin ValorUfClp → CLP queda 0
        Assert.Equal(2, data.CubicacionZonaCantidades.Count);
        Assert.Equal(15m, data.Cubicacion[0].Cantidad);
    }

    [Fact]
    public void SyncLegacyFromNormalized_RebuildsQtyExpressionsFromZonas()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);

        data.CubicacionZonaCantidades.Clear();
        data.CubicacionZonaCantidades.Add(new CubicacionZonaCantidadRow
        {
            RecursoId = "R1",
            ItemId = "P1",
            ZonaOffset = 0,
            Cantidad = 7m,
            Expresion = "7"
        });

        _service.SyncLegacyFromNormalized(data);

        Assert.Equal(7m, data.Cubicacion[0].Cantidad);
        Assert.Equal("7", data.Cubicacion[0].QtyExpressions[0]);
    }

    [Fact]
    public void SyncLegacyFromNormalized_PreservesSector300()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);
        data.CubicacionZonaCantidades.Add(new CubicacionZonaCantidadRow
        {
            RecursoId = "R1",
            ItemId = "P1",
            ZonaOffset = 299,
            Cantidad = 4m,
            Expresion = "4"
        });

        _service.SyncLegacyFromNormalized(data);

        Assert.Equal(300, data.Cubicacion[0].QtyExpressions.Length);
        Assert.Equal("4", data.Cubicacion[0].QtyExpressions[299]);
        Assert.Equal(19m, data.Cubicacion[0].Cantidad);
    }

    [Fact]
    public void ApplyMaterial_WithFrozenPrices_StoresSnapshot()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);
        data.Proyecto.PreciosCongelados = true;
        data.Proyecto.FechaCongelamientoPrecios = new DateTime(2026, 1, 15);
        data.Proyecto.ValorUfClp = 39000m;
        data.Proyecto.ValorUfClpCongelado = 39000m;

        var row = data.Cubicacion[0];
        var material = new MaterialRow
        {
            Codigo = "CABLE2.5",
            Descripcion = "Cable congelado",
            Unidad = "ML",
            MatClp = 4680m,
            MoClp = 1170m,
            PrecioCongelado = new PrecioCongeladoRow
            {
                MatClp = 3510m,
                MoClp = 390m,
                MatUf = 0.09m,
                MoUf = 0.01m,
                FechaPrecio = new DateTime(2026, 1, 10),
                MonedaPrecio = Moneda.CLP,
                UfUsadaClp = 39000m
            }
        };

        _service.ApplyMaterial(data, row, material);

        var valorizacion = data.CubicacionValorizaciones.Single(v => v.RecursoId == "R1");
        Assert.Equal(3510m, valorizacion.MatClp);
        Assert.Equal(390m, valorizacion.MoClp);
        Assert.Equal(0.09m, valorizacion.MatUf);
        Assert.Equal("Precio congelado", valorizacion.FuentePrecio);
        Assert.NotNull(valorizacion.PrecioSnapshot);
        Assert.Equal(3510m, row.MatClp);
        Assert.Equal(0.09m, row.MatUf);
    }

    [Fact]
    public void ApplyMedicionesToPartida_UpdatesZonaCantidades()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);

        var mediciones = new List<MeasurementLineRow>
        {
            new()
            {
                ItemId = "P1",
                RecursoId = "R1",
                ZonaOffset = 0,
                Longitud = 4m,
                Unidades = 2m
            },
            new()
            {
                ItemId = "P1",
                RecursoId = "R1",
                ZonaOffset = 1,
                Longitud = 3m
            }
        };

        _service.ApplyMedicionesToPartida(data, "P1", mediciones);

        Assert.Equal(8m, data.CubicacionZonaCantidades.First(z => z.ZonaOffset == 0).Cantidad);
        Assert.Equal(3m, data.CubicacionZonaCantidades.First(z => z.ZonaOffset == 1).Cantidad);
        Assert.Equal(11m, data.Cubicacion[0].Cantidad);
    }

    [Fact]
    public void ApplyMedicionesToPartida_UnscopedMedicionesApplyOnlyWhenSingleRecurso()
    {
        var data = new ProjectFileData
        {
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "R1", ItemId = "P1", Codigo = "A", Cantidad = 1m },
                new() { Id = "R2", ItemId = "P1", Codigo = "B", Cantidad = 1m }
            },
            CubicacionRecursos = new List<CubicacionRecursoRow>
            {
                new() { Id = "R1", ItemId = "P1", Codigo = "A" },
                new() { Id = "R2", ItemId = "P1", Codigo = "B" }
            },
            CubicacionZonaCantidades = new List<CubicacionZonaCantidadRow>
            {
                new() { RecursoId = "R1", ItemId = "P1", ZonaOffset = 0, Cantidad = 1m, Expresion = "1" },
                new() { RecursoId = "R2", ItemId = "P1", ZonaOffset = 0, Cantidad = 1m, Expresion = "1" }
            },
            CubicacionValorizaciones = new List<CubicacionValorizacionRow>
            {
                new() { RecursoId = "R1", ItemId = "P1" },
                new() { RecursoId = "R2", ItemId = "P1" }
            }
        };

        var mediciones = new List<MeasurementLineRow>
        {
            new() { ItemId = "P1", ZonaOffset = 0, Longitud = 9m }
        };

        _service.ApplyMedicionesToPartida(data, "P1", mediciones);

        Assert.Equal(1m, data.CubicacionZonaCantidades.First(z => z.RecursoId == "R1").Cantidad);
        Assert.Equal(1m, data.CubicacionZonaCantidades.First(z => z.RecursoId == "R2").Cantidad);
    }

    [Fact]
    public void CreateZonasFromLegacy_PreservesFormulaExpression()
    {
        var data = new ProjectFileData
        {
            Cubicacion = new List<CubicacionRow>
            {
                new()
                {
                    Id = "R1",
                    ItemId = "P1",
                    Codigo = "X",
                    QtyExpressions = new[] { "10+2" }
                }
            },
            Zonas = new List<ZonaProyecto> { new() { Id = "Z1", Orden = 0, Activa = true } }
        };

        _service.EnsureProject(data);

        var zona = Assert.Single(data.CubicacionZonaCantidades);
        Assert.Equal("10+2", zona.Expresion);
        Assert.Equal(12m, zona.Cantidad);
    }

    [Fact]
    public void EnsureProject_WhenLegacyCodeChangesOnExistingRecurso_KeepsGridCode()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);

        var row = data.Cubicacion.Single(r => r.Id == "R1");
        row.Codigo = "COND30";
        row.Descripcion = "Conductor 3x10";
        row.Unidad = "UN";

        _service.EnsureProject(data);

        Assert.Equal("COND30", data.CubicacionRecursos.Single(r => r.Id == "R1").Codigo);
        Assert.Equal("Conductor 3x10", row.Descripcion);
        Assert.Equal("UN", row.Unidad);
        Assert.Equal("10", row.QtyExpressions[0]);
        Assert.Equal("5", row.QtyExpressions[1]);
        Assert.Equal(15m, row.Cantidad);
    }

    [Fact]
    public void EnsureProject_WhenLegacySectorChangesOnExistingRecurso_KeepsGridQuantity()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);

        var row = data.Cubicacion.Single(r => r.Id == "R1");
        row.QtyExpressions = new[] { "20", "5" };
        row.Cantidad = 25m;

        _service.EnsureProject(data);

        Assert.Equal(25m, row.Cantidad);
        Assert.Equal("20", row.QtyExpressions[0]);
        Assert.Equal(20m, data.CubicacionZonaCantidades.Single(z => z.ZonaOffset == 0).Cantidad);
        Assert.Equal(5m, data.CubicacionZonaCantidades.Single(z => z.ZonaOffset == 1).Cantidad);
    }

    [Fact]
    public void EnsureProject_CodeEditAfterMediciones_DoesNotReplaceZonaQuantities()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);
        _service.ApplyMedicionesToPartida(data, "P1", new List<MeasurementLineRow>
        {
            new() { ItemId = "P1", RecursoId = "R1", ZonaOffset = 0, Longitud = 4m, Unidades = 2m },
            new() { ItemId = "P1", RecursoId = "R1", ZonaOffset = 1, Longitud = 3m }
        });

        var row = data.Cubicacion.Single(r => r.Id == "R1");
        row.Codigo = "NUEVO";

        _service.EnsureProject(data);

        Assert.Equal("NUEVO", row.Codigo);
        Assert.Equal(8m, data.CubicacionZonaCantidades.Single(z => z.ZonaOffset == 0).Cantidad);
        Assert.Equal(3m, data.CubicacionZonaCantidades.Single(z => z.ZonaOffset == 1).Cantidad);
        Assert.Equal(11m, row.Cantidad);
    }

    [Fact]
    public void CreateSnapshot_AfterUnsyncedGridEdit_KeepsCodeAndQuantity()
    {
        var session = new BudgetProjectSession();
        session.NewProject("sync-grilla");
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "CABLE2.5",
            Descripcion = "Cable",
            Unidad = "ML",
            Cantidad = 15m,
            QtyExpressions = new[] { "10", "5" }
        });
        session.CreateSnapshot();

        var row = session.Cubicacion.Single(r => r.Id == "R1");
        row.Codigo = "COND30";
        row.Descripcion = "Conductor";
        row.QtyExpressions = new[] { "20", "5" };
        row.Cantidad = 25m;

        session.CreateSnapshot();

        var persisted = session.Cubicacion.Single(r => r.Id == "R1");
        Assert.Equal("COND30", persisted.Codigo);
        Assert.Equal("Conductor", persisted.Descripcion);
        Assert.Equal(25m, persisted.Cantidad);
    }

    [Fact]
    public void GridEdit_SurvivesSnapshotUndoAndReopen()
    {
        var session = new BudgetProjectSession();
        session.NewProject("fila-estable");
        session.ZonasProyecto.Add(new ZonaProyecto { Id = "Z1", Nombre = "Piso 1", Orden = 0, Activa = true });
        session.ZonasProyecto.Add(new ZonaProyecto { Id = "Z2", Nombre = "Piso 2", Orden = 1, Activa = true });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "CABLE2.5",
            Descripcion = "Cable",
            Unidad = "ML",
            MatClp = 1000m,
            MoClp = 200m,
            Cantidad = 15m,
            QtyExpressions = new[] { "10", "5" }
        });
        var before = session.CreateSnapshot();

        var row = session.Cubicacion.Single(r => r.Id == "R1");
        row.Codigo = "COND30";
        row.Descripcion = "Conductor 3x10";
        row.Unidad = "UN";
        row.MatClp = 2500m;
        row.MoClp = 800m;
        row.QtyExpressions = new[] { "10+2", "" };
        row.Cantidad = 12m;
        var after = session.CreateSnapshot();

        AssertGridEdit(after);

        session.ApplySnapshot(before, markDirty: true);
        var restored = session.Cubicacion.Single(r => r.Id == "R1");
        Assert.Equal("CABLE2.5", restored.Codigo);
        Assert.Equal(15m, restored.Cantidad);

        session.ApplySnapshot(after, markDirty: true);
        AssertGridEdit(session.CreateSnapshot());

        var path = Path.Combine(Path.GetTempPath(), $"pe_grid_{Guid.NewGuid():N}.db");
        try
        {
            SQLiteProjectStore.SaveSnapshot(path, after);
            SqliteConnection.ClearAllPools();
            var loaded = SQLiteProjectStore.LoadSnapshot(path);
            var reopened = new BudgetProjectSession();
            reopened.ApplySnapshot(loaded);
            AssertGridEdit(reopened.CreateSnapshot());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertGridEdit(ProjectFileData data)
    {
        var row = data.Cubicacion.Single(r => r.Id == "R1");
        Assert.Equal("COND30", row.Codigo);
        Assert.Equal("Conductor 3x10", row.Descripcion);
        Assert.Equal("UN", row.Unidad);
        Assert.Equal(2500m, row.MatClp);
        Assert.Equal(800m, row.MoClp);
        Assert.Equal("10+2", row.QtyExpressions[0]);
        Assert.Equal("", row.QtyExpressions[1]);
        Assert.Equal(12m, row.Cantidad);
        Assert.Equal("COND30", data.CubicacionRecursos.Single(r => r.Id == "R1").Codigo);
        Assert.Equal("10+2", data.CubicacionZonaCantidades.Single(z => z.ZonaOffset == 0).Expresion);
        Assert.DoesNotContain(data.CubicacionZonaCantidades, z => z.ZonaOffset == 1);
        Assert.Equal(2500m, data.CubicacionValorizaciones.Single(v => v.RecursoId == "R1").MatClp);
    }

    [Fact]
    public void ApplyMediciones_WritesQuantityOntoTheGrid()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);

        bool applied = _service.ApplyMedicionesToPartida(data, "P1", new List<MeasurementLineRow>
        {
            new() { ItemId = "P1", RecursoId = "R1", ZonaOffset = 0, Longitud = 4m, Unidades = 2m }
        });

        Assert.True(applied);
        var row = data.Cubicacion.Single(r => r.Id == "R1");
        Assert.Equal(8m, row.Cantidad);
        Assert.Equal("8", row.QtyExpressions[0]);
    }

    [Fact]
    public void SyncLegacy_WhenListsChangeBeforeCommit_LeavesTheGridUntouched()
    {
        var data = ProjectWithLegacyRow();
        _service.EnsureProject(data);
        var row = data.Cubicacion.Single(r => r.Id == "R1");
        string codigo = row.Codigo;
        decimal cantidad = row.Cantidad;
        string expresion = row.QtyExpressions[0];
        int zonas = data.CubicacionZonaCantidades.Count;

        bool applied = _service.SyncLegacyFromNormalized(data, () =>
            data.CubicacionRecursos.Add(new CubicacionRecursoRow { Id = "OTRO", Codigo = "X" }));

        Assert.False(applied);
        Assert.Equal(codigo, row.Codigo);
        Assert.Equal(cantidad, row.Cantidad);
        Assert.Equal(expresion, row.QtyExpressions[0]);
        Assert.Equal(zonas, data.CubicacionZonaCantidades.Count);
        Assert.DoesNotContain(data.Cubicacion, r => r.Id == "OTRO");
    }

    [Fact]
    public void EnsurePartidaApuLinks_CreatesPrincipalLink()
    {
        var data = new ProjectFileData
        {
            Itemizado = new List<ItemizadoRow> { new() { Id = "P1", Item = "1", Descripcion = "Partida" } },
            Apus = new List<ApuRow> { new() { Id = "A1", ItemId = "P1", Descripcion = "APU P1" } }
        };

        _service.EnsurePartidaApuLinks(data);

        var link = Assert.Single(data.PartidaApuVinculos);
        Assert.Equal("P1", link.ItemId);
        Assert.Equal("A1", link.ApuId);
        Assert.Equal("Principal", link.Modo);
    }
}
