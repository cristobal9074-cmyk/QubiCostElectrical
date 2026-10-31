using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public class TipologiasEngineTests
{
    private static TipologiasEdificioData CreateSampleEdificio()
    {
        var data = new TipologiasEdificioData();

        // Tipología A: 2D+2B (12 focos, 18 enchufes, 1 TDA, 180m cable)
        var tipoA = new TipologiaDefinicion
        {
            Id = "tipo-a",
            Codigo = "TIPO-A",
            Nombre = "Depto 2D+2B",
            SuperficieM2 = 65m
        };
        tipoA.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "LUM-001",
            Descripcion = "Centro de Alumbrado LED",
            Unidad = "un",
            Categoria = "Artefactos",
            CantidadPorDepto = 12m,
            MatClp = 4500m,
            MoClp = 2500m,
            PartidaCodigo = "1.17"
        });
        tipoA.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "ENC-001",
            Descripcion = "Enchufe Doble 10A",
            Unidad = "un",
            Categoria = "Artefactos",
            CantidadPorDepto = 18m,
            MatClp = 3200m,
            MoClp = 1800m,
            PartidaCodigo = "1.19"
        });
        tipoA.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "TAB-001",
            Descripcion = "Tablero TDA Depto",
            Unidad = "un",
            Categoria = "Tableros",
            CantidadPorDepto = 1m,
            MatClp = 85000m,
            MoClp = 35000m,
            PartidaCodigo = "1.24"
        });
        tipoA.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "CON-001",
            Descripcion = "Conductor EVA 2.5 mm2",
            Unidad = "m",
            Categoria = "Conductores",
            CantidadPorDepto = 180m,
            MatClp = 420m,
            MoClp = 280m,
            PartidaCodigo = "1.13"
        });

        // Tipología B: 1D+1B (8 focos, 12 enchufes, 1 TDA, 120m cable)
        var tipoB = new TipologiaDefinicion
        {
            Id = "tipo-b",
            Codigo = "TIPO-B",
            Nombre = "Depto 1D+1B",
            SuperficieM2 = 42m
        };
        tipoB.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "LUM-001",
            Descripcion = "Centro de Alumbrado LED",
            Unidad = "un",
            Categoria = "Artefactos",
            CantidadPorDepto = 8m,
            MatClp = 4500m,
            MoClp = 2500m,
            PartidaCodigo = "1.17"
        });
        tipoB.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "ENC-001",
            Descripcion = "Enchufe Doble 10A",
            Unidad = "un",
            Categoria = "Artefactos",
            CantidadPorDepto = 12m,
            MatClp = 3200m,
            MoClp = 1800m,
            PartidaCodigo = "1.19"
        });
        tipoB.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "TAB-001",
            Descripcion = "Tablero TDA Depto",
            Unidad = "un",
            Categoria = "Tableros",
            CantidadPorDepto = 1m,
            MatClp = 85000m,
            MoClp = 35000m,
            PartidaCodigo = "1.24"
        });
        tipoB.Items.Add(new TipologiaRecursoItem
        {
            Codigo = "CON-001",
            Descripcion = "Conductor EVA 2.5 mm2",
            Unidad = "m",
            Categoria = "Conductores",
            CantidadPorDepto = 120m,
            MatClp = 420m,
            MoClp = 280m,
            PartidaCodigo = "1.13"
        });

        data.Tipologias.Add(tipoA);
        data.Tipologias.Add(tipoB);

        // Distribución: 10 pisos con 4 Deptos A y 2 Deptos B por piso
        // Total: 40 Deptos A + 20 Deptos B = 60 departamentos en el edificio
        for (int p = 1; p <= 10; p++)
        {
            data.Distribucion.Add(new TipologiaDistribucionPiso
            {
                ZonaId = $"piso-{p}",
                ZonaNombre = $"Piso {p}",
                TipologiaId = "tipo-a",
                CantidadDeptos = 4
            });
            data.Distribucion.Add(new TipologiaDistribucionPiso
            {
                ZonaId = $"piso-{p}",
                ZonaNombre = $"Piso {p}",
                TipologiaId = "tipo-b",
                CantidadDeptos = 2
            });
        }

        return data;
    }

    [Fact]
    public void CalcularResumenTipologias_CalculatesCorrectDepartmentCounts()
    {
        var data = CreateSampleEdificio();
        var resumen = TipologiasEngine.CalcularResumenTipologias(data);

        Assert.Equal(2, resumen.Count);

        var rA = resumen.First(r => r.Codigo == "TIPO-A");
        Assert.Equal(40, rA.CantidadDeptosTotal);
        Assert.Equal(4, rA.TotalRecursosDistintos);

        var rB = resumen.First(r => r.Codigo == "TIPO-B");
        Assert.Equal(20, rB.CantidadDeptosTotal);
        Assert.Equal(4, rB.TotalRecursosDistintos);

        Assert.Equal(60, data.TotalDepartamentos);
    }

    [Fact]
    public void CalcularConsolidadoMateriales_MultipliesAndAggregatesCorrectly()
    {
        var data = CreateSampleEdificio();
        var consolidados = TipologiasEngine.CalcularConsolidadoMateriales(data);

        Assert.Equal(4, consolidados.Count);

        // LUM-001: (40 deptos A * 12) + (20 deptos B * 8) = 480 + 160 = 640 centros
        var lum = consolidados.First(c => c.Codigo == "LUM-001");
        Assert.Equal(640m, lum.CantidadTotal);
        Assert.Equal(4500m, lum.MatClp);
        Assert.Equal(640m * 4500m, lum.TotalMatClp);

        // ENC-001: (40 deptos A * 18) + (20 deptos B * 12) = 720 + 240 = 960 enchufes
        var enc = consolidados.First(c => c.Codigo == "ENC-001");
        Assert.Equal(960m, enc.CantidadTotal);

        // TAB-001: (40 * 1) + (20 * 1) = 60 tableros
        var tab = consolidados.First(c => c.Codigo == "TAB-001");
        Assert.Equal(60m, tab.CantidadTotal);

        // CON-001: (40 * 180m) + (20 * 120m) = 7200 + 2400 = 9600 metros de cable
        var con = consolidados.First(c => c.Codigo == "CON-001");
        Assert.Equal(9600m, con.CantidadTotal);
    }

    [Fact]
    public void GenerarFilasCubicacion_MapsToItemizadoPartidas()
    {
        var data = CreateSampleEdificio();
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "it-13", Item = "1.13", Descripcion = "Alimentadores departamentos" },
            new() { Id = "it-17", Item = "1.17", Descripcion = "Centros alumbrado departamentos" },
            new() { Id = "it-19", Item = "1.19", Descripcion = "Centros enchufes departamentos" },
            new() { Id = "it-24", Item = "1.24", Descripcion = "Tableros departamentos" }
        };

        var filas = TipologiasEngine.GenerarFilasCubicacion(data, itemizado, valorUfClp: 38000m);

        Assert.Equal(4, filas.Count);

        var fLum = filas.First(f => f.Codigo == "LUM-001");
        Assert.Equal("it-17", fLum.ItemId);
        Assert.Equal(640m, fLum.Cantidad);

        var fEnc = filas.First(f => f.Codigo == "ENC-001");
        Assert.Equal("it-19", fEnc.ItemId);
        Assert.Equal(960m, fEnc.Cantidad);

        var fTab = filas.First(f => f.Codigo == "TAB-001");
        Assert.Equal("it-24", fTab.ItemId);
        Assert.Equal(60m, fTab.Cantidad);

        var fCon = filas.First(f => f.Codigo == "CON-001");
        Assert.Equal("it-13", fCon.ItemId);
        Assert.Equal(9600m, fCon.Cantidad);
    }

    [Fact]
    public void SQLiteProjectStore_SavesAndLoadsTipologiasPersistently()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test-tipologias-{Guid.NewGuid():N}.qce");
        try
        {
            var projectData = new ProjectFileData();
            projectData.Proyecto.Nombre = "Edificio Test Tipologias";
            projectData.Tipologias = CreateSampleEdificio();

            SQLiteProjectStore.SaveSnapshot(tempDb, projectData);
            Assert.True(File.Exists(tempDb));

            var loaded = SQLiteProjectStore.LoadSnapshot(tempDb);
            Assert.NotNull(loaded.Tipologias);
            Assert.Equal(2, loaded.Tipologias.Tipologias.Count);
            Assert.Equal(20, loaded.Tipologias.Distribucion.Count);
            Assert.Equal(60, loaded.Tipologias.TotalDepartamentos);

            var mat = TipologiasEngine.CalcularConsolidadoMateriales(loaded.Tipologias);
            Assert.Equal(4, mat.Count);
            Assert.Equal(9600m, mat.First(x => x.Codigo == "CON-001").CantidadTotal);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
