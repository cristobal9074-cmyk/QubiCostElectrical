using System;
using System.Collections.Generic;
using System.Linq;
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
