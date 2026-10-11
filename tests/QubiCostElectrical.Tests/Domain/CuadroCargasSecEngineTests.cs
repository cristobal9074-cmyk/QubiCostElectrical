using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class CuadroCargasSecEngineTests : IDisposable
{
    private readonly string _tempDir;

    public CuadroCargasSecEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sec_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void EstimateCircuitPower_UsesExplicitWattageIfGiven()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            Tipo = "A",
            PotenciaW = 1850m
        };

        decimal p = CuadroCargasSecEngine.EstimateCircuitPowerW(c);
        Assert.Equal(1850m, p);
    }

    [Fact]
    public void EstimateCircuitPower_ParsesArtifactsWithWattage()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            Tipo = "A",
            Artefactos = new List<ArtefactoSCRow>
            {
                new() { Nombre = "Downlight LED 12W", Cantidad = 10m },
                new() { Nombre = "Campana Industrial 150W", Cantidad = 2m }
            }
        };

        // 10 * 12W + 2 * 150W = 120 + 300 = 420W
        decimal p = CuadroCargasSecEngine.EstimateCircuitPowerW(c);
        Assert.Equal(420m, p);
    }

    [Fact]
    public void EstimateCircuitPower_ParsesSocketsStandardValues()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            Tipo = "E",
            Artefactos = new List<ArtefactoSCRow>
            {
                new() { Nombre = "ENCH D10", Cantidad = 4m } // 4 * 300W = 1200W
            }
        };

        decimal p = CuadroCargasSecEngine.EstimateCircuitPowerW(c);
        Assert.Equal(1200m, p);
    }

    [Fact]
    public void EstimateCircuitPower_FallbacksToUsageTypeDefaultWhenNoArtifacts()
    {
        var cAlum = new CircuitoSCRow { Estado = "Activo", Tipo = "A" };
        var cEnch = new CircuitoSCRow { Estado = "Activo", Tipo = "E" };
        var cClima = new CircuitoSCRow { Estado = "Activo", Tipo = "CL" };

        Assert.Equal(1000m, CuadroCargasSecEngine.EstimateCircuitPowerW(cAlum));
        Assert.Equal(1500m, CuadroCargasSecEngine.EstimateCircuitPowerW(cEnch));
        Assert.Equal(2200m, CuadroCargasSecEngine.EstimateCircuitPowerW(cClima));
    }

    [Fact]
    public void CalculateTableroSummary_ComputesPowersAndPhaseUnbalance()
    {
        var board = new CuadroCargaSCRow
        {
            Nombre = "TDA-01",
            Descripcion = "Tablero General Alumbrado",
            Circuitos = new List<CircuitoSCRow>
            {
                new() { Numero = 1, Tipo = "A", Sistema = "M", FaseRst = "R", PotenciaW = 1000m, Estado = "Activo" },
                new() { Numero = 2, Tipo = "A", Sistema = "M", FaseRst = "S", PotenciaW = 1000m, Estado = "Activo" },
                new() { Numero = 3, Tipo = "A", Sistema = "M", FaseRst = "T", PotenciaW = 1000m, Estado = "Activo" }
            }
        };

        var summary = CuadroCargasSecEngine.CalculateTableroSummary(board);

        Assert.Equal(3.0m, summary.PotenciaTotalInstaladaKw);
        Assert.Equal(1.0m, summary.PotenciaFaseRKw);
        Assert.Equal(1.0m, summary.PotenciaFaseSKw);
        Assert.Equal(1.0m, summary.PotenciaFaseTKw);
        Assert.Equal(0.0m, summary.PorcentajeDesbalance);
        Assert.True(summary.ConformeSec);
    }

    [Fact]
    public void AutoBalancearFases_ReducesUnbalanceToConformSec()
    {
        // Unbalanced board where all 9 circuits are on Phase R initially
        var board = new CuadroCargaSCRow
        {
            Nombre = "TDA-Desbalanceado",
            Circuitos = new List<CircuitoSCRow>
            {
                new() { Numero = 1, Tipo = "CL", Sistema = "M", FaseRst = "R", PotenciaW = 2000m, Estado = "Activo" },
                new() { Numero = 2, Tipo = "CL", Sistema = "M", FaseRst = "R", PotenciaW = 2000m, Estado = "Activo" },
                new() { Numero = 3, Tipo = "CL", Sistema = "M", FaseRst = "R", PotenciaW = 2000m, Estado = "Activo" },
                new() { Numero = 4, Tipo = "E", Sistema = "M", FaseRst = "R", PotenciaW = 1500m, Estado = "Activo" },
                new() { Numero = 5, Tipo = "E", Sistema = "M", FaseRst = "R", PotenciaW = 1500m, Estado = "Activo" },
                new() { Numero = 6, Tipo = "E", Sistema = "M", FaseRst = "R", PotenciaW = 1500m, Estado = "Activo" },
                new() { Numero = 7, Tipo = "A", Sistema = "M", FaseRst = "R", PotenciaW = 1000m, Estado = "Activo" },
                new() { Numero = 8, Tipo = "A", Sistema = "M", FaseRst = "R", PotenciaW = 1000m, Estado = "Activo" },
                new() { Numero = 9, Tipo = "A", Sistema = "M", FaseRst = "R", PotenciaW = 1000m, Estado = "Activo" }
            }
        };

        var before = CuadroCargasSecEngine.CalculateTableroSummary(board);
        Assert.False(before.ConformeSec);
        Assert.Equal(200m, before.PorcentajeDesbalance);

        // Run auto-balancing
        CuadroCargasSecEngine.AutoBalancearFases(board);

        var after = CuadroCargasSecEngine.CalculateTableroSummary(board);
        Assert.True(after.ConformeSec, $"Expected desbalance <= 10%, but got {after.PorcentajeDesbalance}%");
        Assert.True(after.PorcentajeDesbalance <= 10.0m);
    }

    [Fact]
    public void ExportCuadroCargasSec_GeneratesValidWorkbookWithSecSheet()
    {
        var board = new CuadroCargaSCRow
        {
            Nombre = "TDA Servicios Comunes",
            Descripcion = "Edificio Central",
            Circuitos = new List<CircuitoSCRow>
            {
                new() { Numero = 1, Tipo = "A", Sistema = "M", FaseRst = "R", PotenciaW = 1000m, Estado = "Activo", Descripcion = "Pasillos Piso 1" },
                new() { Numero = 2, Tipo = "E", Sistema = "M", FaseRst = "S", PotenciaW = 1500m, Estado = "Activo", Descripcion = "Enchufes Conserjería" },
                new() { Numero = 3, Tipo = "F", Sistema = "T", FaseRst = "R-S-T", PotenciaW = 3000m, Estado = "Activo", Descripcion = "Bomba de Agua" }
            }
        };

        var project = new ProjectFileData
        {
            CubicacionSC = new List<CuadroCargaSCRow> { board }
        };

        var ctx = new ExportProjectContext
        {
            Data = project,
            ExportedAt = DateTime.Now
        };
        var exporter = new CubicacionSCTechnicalReportExcelExporter();
        string path = Path.Combine(_tempDir, "cuadro_sec.xlsx");

        exporter.ExportCuadroCargasSec(ctx, path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);
        Assert.True(wb.Worksheets.Contains("05_CUADRO_CARGAS_SEC"));
        var ws = wb.Worksheet("05_CUADRO_CARGAS_SEC");
        Assert.Contains("CUADRO DE CARGAS SEC", ws.Cell(1, 1).GetString());
        Assert.Contains("TDA Servicios Comunes", ws.Cell(5, 1).GetString());
    }
}
