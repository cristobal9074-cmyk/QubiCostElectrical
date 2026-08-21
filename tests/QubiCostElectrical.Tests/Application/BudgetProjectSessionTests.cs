using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Tests.Fixtures;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetProjectSessionTests
{
    [Fact]
    public void ApplySnapshot_PreservesZeroPercentages()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto cero",
                Cliente = "Test",
                GastosGeneralesPct = 0m,
                UtilidadPct = 0m,
                IvaPct = 0m,
                HerramientasPct = 0m,
                MermasPct = 0m,
                RiesgoPct = 0m,
                DescuentoPct = 0m
            }
        };

        session.ApplySnapshot(data, markDirty: false);

        Assert.Equal(0m, session.ProjectInfo.GastosGeneralesPct);
        Assert.Equal(0m, session.ProjectInfo.UtilidadPct);
        Assert.Equal(0m, session.ProjectInfo.IvaPct);
        Assert.Equal(0m, session.ProjectInfo.HerramientasPct);
        Assert.Equal(0m, session.ProjectInfo.MermasPct);
        Assert.Equal(0m, session.ProjectInfo.RiesgoPct);
        Assert.Equal(0m, session.ProjectInfo.DescuentoPct);
    }

    [Fact]
    public void ApplySnapshot_PreservesExactPercentages()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto exacto",
                Cliente = "Test",
                GastosGeneralesPct = 7.5m,
                UtilidadPct = 12.3m,
                IvaPct = 18.5m,
                HerramientasPct = 2.1m,
                MermasPct = 3.7m,
                RiesgoPct = 1.9m,
                DescuentoPct = 5.0m
            }
        };

        session.ApplySnapshot(data, markDirty: false);

        Assert.Equal(7.5m, session.ProjectInfo.GastosGeneralesPct);
        Assert.Equal(12.3m, session.ProjectInfo.UtilidadPct);
        Assert.Equal(18.5m, session.ProjectInfo.IvaPct);
        Assert.Equal(2.1m, session.ProjectInfo.HerramientasPct);
        Assert.Equal(3.7m, session.ProjectInfo.MermasPct);
        Assert.Equal(1.9m, session.ProjectInfo.RiesgoPct);
        Assert.Equal(5.0m, session.ProjectInfo.DescuentoPct);
    }

    [Fact]
    public void ApplySnapshot_PreservesTipoProyecto()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto completo",
                Cliente = "Test",
                TipoProyecto = "Edificio comercial",
                LastSQLiteSavedAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Local)
            }
        };

        session.ApplySnapshot(data, markDirty: false);

        Assert.Equal("Edificio comercial", session.ProjectInfo.TipoProyecto);
        Assert.Equal(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Local), session.ProjectInfo.LastSQLiteSavedAt);
    }

    [Fact]
    public void CreateSnapshot_AfterSetSQLiteSavedAt_IncludesTimestampInSnapshot()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.SetSQLiteSavedAt(new DateTime(2026, 6, 18, 14, 30, 0));

        var snapshot = session.CreateSnapshot();

        Assert.Equal(new DateTime(2026, 6, 18, 14, 30, 0), snapshot.Proyecto!.LastSQLiteSavedAt);
    }

    [Fact]
    public void ApplySnapshot_CargaCompleta_TodasLasColecciones()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = ProjectFileDataFixtures.BuildFullProject();
        session.ApplySnapshot(data, markDirty: false);

        Assert.Equal(2, session.Itemizado.Count);
        Assert.Equal(1, session.Materiales.Count);
        Assert.Equal(1, session.Cubicacion.Count);
        Assert.Single(session.Plantillas);
        Assert.Single(session.ConjuntosElectricos);
        Assert.Single(session.ProjectInfo.AntecedentesTecnicos);
        Assert.NotNull(session.Apus[0].CondicionesLaborales);
        Assert.Equal(15m, session.Apus[0].CondicionesLaborales.FactorAlturaPct);
        Assert.Single(session.Versiones);
        Assert.Single(session.Apus);
        Assert.Single(session.OrdenesCambio);
        Assert.Single(session.ZonasProyecto);
        Assert.Single(session.Mediciones);
        Assert.Single(session.Data.CubicacionSC);
        Assert.NotNull(session.Data.CubicacionScConfig);
        Assert.Equal("V1", session.ActiveVersionId);
        Assert.Single(session.QtyHeadersByItem);
        Assert.Equal(2, session.ProjectInfo.Variables.Count);
        Assert.Single(session.ProjectInfo.HistorialEstados);
        Assert.Single(session.ProjectInfo.CubicacionCellComments);
        Assert.Single(session.ProjectInfo.TechnicalValidationRules);
        Assert.Single(session.ProjectInfo.RealCosts);
        Assert.Single(session.ProjectInfo.PartidaWorkflow);
        Assert.Single(session.ProjectInfo.PartidaTimeline);
        Assert.Single(session.ProjectInfo.ProactiveAlertRules);
    }

    [Fact]
    public void ApplySnapshot_NoCrossContamination()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var projectA = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Proyecto A", Cliente = "Cliente A", TipoProyecto = "Edificio habitacional" },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "A1", Item = "1.1", Descripcion = "Partida A", Unidad = "ML", Cantidad = 100m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C-A1", ItemId = "A1", Codigo = "MAT-A", Descripcion = "Material A", Unidad = "ML", Cantidad = 100m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT-A", Descripcion = "Material A", Unidad = "ML", MatUf = 10m }
            }
        };

        var projectB = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Proyecto B", Cliente = "Cliente B", TipoProyecto = "Oficina" },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "B1", Item = "2.1", Descripcion = "Partida B", Unidad = "UN", Cantidad = 5m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C-B1", ItemId = "B1", Codigo = "MAT-B", Descripcion = "Material B", Unidad = "UN", Cantidad = 5m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT-B", Descripcion = "Material B", Unidad = "UN", MatUf = 50m }
            }
        };

        session.ApplySnapshot(projectA, markDirty: false);
        session.ApplySnapshot(projectB, markDirty: false);

        Assert.Equal("Proyecto B", session.ProjectInfo.Nombre);
        Assert.Equal("Cliente B", session.ProjectInfo.Cliente);
        Assert.Equal("Oficina", session.ProjectInfo.TipoProyecto);
        Assert.Single(session.Itemizado);
        Assert.Equal("Partida B", session.Itemizado[0].Descripcion);
        Assert.Single(session.Cubicacion);
        Assert.Equal("MAT-B", session.Cubicacion[0].Codigo);
        Assert.Single(session.Materiales);
        Assert.Equal("Material B", session.Materiales[0].Descripcion);

        Assert.DoesNotContain(session.Itemizado, i => i.Id == "A1");
        Assert.DoesNotContain(session.Cubicacion, c => c.ItemId == "A1");
        Assert.DoesNotContain(session.Materiales, m => m.Codigo == "MAT-A");
    }

    [Fact]
    public void CreateSnapshot_ApplySnapshot_RoundTrip_AllFieldsIdentical()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var original = ProjectFileDataFixtures.BuildFullProject();
        session.ApplySnapshot(original, markDirty: false);

        var snapshot = session.CreateSnapshot();

        ProjectFileDataAssertions.AssertEquivalent(original, snapshot);
    }

    [Fact]
    public void CreateSnapshot_ProducesDeepCopy()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Original", Cliente = "Test", TipoProyecto = "Casa" },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "P1", Item = "1.1", Descripcion = "Partida original", Unidad = "ML", Cantidad = 50m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C1", ItemId = "P1", Codigo = "MAT001", Descripcion = "Material original", Unidad = "ML", Cantidad = 50m }
            }
        };

        session.ApplySnapshot(data, markDirty: false);
        var snapshot = session.CreateSnapshot();

        snapshot.Proyecto.Nombre = "Modificado";
        snapshot.Itemizado[0].Descripcion = "Partida modificada";
        snapshot.Cubicacion[0].Descripcion = "Material modificado";

        Assert.Equal("Original", session.ProjectInfo.Nombre);
        Assert.Equal("Partida original", session.Itemizado[0].Descripcion);
        Assert.Equal("Material original", session.Cubicacion[0].Descripcion);
    }

    [Fact]
    public void NewProject_SetsDefaults()
    {
        var session = new BudgetProjectSession();
        session.NewProject("Juan Perez", "Arquitecto");

        Assert.Equal("Proyecto sin nombre", session.ProjectInfo.Nombre);
        Assert.Equal("Cliente sin nombre", session.ProjectInfo.Cliente);
        Assert.Equal("Sin ubicación", session.ProjectInfo.Ubicacion);
        Assert.Equal("Juan Perez", session.ProjectInfo.UsuarioNombre);
        Assert.Equal("Arquitecto", session.ProjectInfo.UsuarioRol);
        Assert.Equal("CLP", session.ProjectInfo.MonedaPresupuesto);
        Assert.Equal("Borrador", session.ProjectInfo.EstadoPresupuesto);
        Assert.Equal(14m, session.ProjectInfo.GastosGeneralesPct);
        Assert.Equal(18m, session.ProjectInfo.UtilidadPct);
        Assert.Equal(19m, session.ProjectInfo.IvaPct);
        Assert.Equal("Edificio habitacional", session.ProjectInfo.TipoProyecto);
        Assert.False(session.IsDirty);
        Assert.Empty(session.Itemizado);
        Assert.Empty(session.Materiales);
        Assert.Empty(session.Cubicacion);
    }

    [Fact]
    public void ApplySnapshot_PreservesPriceExpiryDays()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Test vencimiento",
                Cliente = "Test",
                PriceExpiryWarningDays = 0,
                PriceExpiryCriticalDays = 0
            }
        };

        session.ApplySnapshot(data, markDirty: false);

        Assert.Equal(0, session.ProjectInfo.PriceExpiryWarningDays);
        Assert.Equal(0, session.ProjectInfo.PriceExpiryCriticalDays);
    }

    [Fact]
    public void ApplySnapshot_RestoresIsDirtyState()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Sucio", Cliente = "Test" }
        };

        session.ApplySnapshot(data, markDirty: true);

        Assert.True(session.IsDirty);
    }

    [Fact]
    public void MultipleSnapshots_ScopeVersionsReset()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.MarkDirty("itemizado");

        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Nuevo", Cliente = "Test" }
        };

        session.ApplySnapshot(data, markDirty: false);

        Assert.False(session.IsDirty);
        Assert.Empty(session.GetDirtyScopes());
    }

    [Fact]
    public void SyncCubicacionPricesFromMateriales_PushesCatalogClpToCubicacion()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.ProjectInfo.ValorUfClp = 39000m;
        session.Materiales.Add(new MaterialRow { Codigo = "PVC20", MatClp = 12000m, MoClp = 3500m });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "PVC20",
            MatClp = 1000m,
            MoClp = 100m
        });
        session.CubicacionValorizaciones.Add(new CubicacionValorizacionRow
        {
            RecursoId = "R1",
            MatClp = 1000m,
            MoClp = 100m
        });

        int updated = session.SyncCubicacionPricesFromMateriales();

        Assert.Equal(1, updated);
        Assert.Equal(12000m, session.Cubicacion[0].MatClp);
        Assert.Equal(3500m, session.Cubicacion[0].MoClp);
        Assert.Equal(12000m, session.CubicacionValorizaciones[0].MatClp);
        Assert.Equal(3500m, session.CubicacionValorizaciones[0].MoClp);
        Assert.Equal(PriceConversion.ClpToUf(12000m, 39000m), session.CubicacionValorizaciones[0].MatUf);
        Assert.Equal(PriceConversion.ClpToUf(3500m, 39000m), session.CubicacionValorizaciones[0].MoUf);
    }

}
