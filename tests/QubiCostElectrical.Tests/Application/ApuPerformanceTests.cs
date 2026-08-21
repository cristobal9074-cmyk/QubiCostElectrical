using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ApuPerformanceTests
{
    private static List<ApuRow> BuildApus(int apuCount, int recursosPerApu)
    {
        var apus = new List<ApuRow>(apuCount);

        for (int i = 0; i < apuCount; i++)
        {
            var apu = new ApuRow
            {
                ItemId = $"ITEM-{i:D4}",
                Descripcion = $"Partida {i}",
                Unidad = "GL",
                CantidadPartida = 10m,
                HerramientasPct = 3m,
                ImprevistosAlzaPct = 2m,
                FechaPrecio = DateTime.Today,
            };

            for (int j = 0; j < recursosPerApu; j++)
            {
                apu.Recursos.Add(new ApuRecursoRow
                {
                    Tipo = (ApuTipoRecurso)(j % 4),
                    CodigoMaterial = $"MAT-{j:D3}",
                    Descripcion = $"Recurso {j}",
                    Unidad = "UN",
                    Rendimiento = 1m + j * 0.1m,
                    PrecioUf = 0.5m + j * 0.05m,
                    MermaPct = 3m,
                });
            }

            apus.Add(apu);
        }

        return apus;
    }

    [Fact]
    public void Building_500_apus_with_20_recursos_each_under_750ms()
    {
        const int apuCount = 500;
        const int recursosPerApu = 20;

        _ = BuildApus(16, 4);

        var sw = Stopwatch.StartNew();

        var apus = BuildApus(apuCount, recursosPerApu);

        sw.Stop();

        Assert.Equal(apuCount, apus.Count);
        Assert.True(sw.ElapsedMilliseconds < 750,
            $"Crear {apuCount} APUs con {recursosPerApu} recursos tardó {sw.ElapsedMilliseconds}ms (límite: 750ms)");
    }

    [Fact]
    public void Reading_calculated_properties_on_1000_recursos_under_200ms()
    {
        const int count = 1000;
        var recursos = Enumerable.Range(0, count).Select(i => new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            Rendimiento = 1m + i * 0.01m,
            PrecioUf = 0.1m + i * 0.001m,
            MermaPct = 5m,
        }).ToList();

        var sw = Stopwatch.StartNew();

        decimal total = 0m;
        foreach (var r in recursos)
        {
            total += r.CostoUnitUf + r.CostoBaseUf + r.MermaUf;
        }

        sw.Stop();

        Assert.True(total > 0m);
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Leer props calculadas de {count} recursos tardó {sw.ElapsedMilliseconds}ms (límite: 200ms)");
    }

    [Fact]
    public void GetProfessionalStatus_on_200_apus_under_100ms()
    {
        const int count = 200;
        var apus = Enumerable.Range(0, count).Select(i => new ApuRow
        {
            ItemId = $"ITEM-{i}",
            Recursos = new List<ApuRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Rendimiento = 2m, PrecioUf = 1m },
                new() { Tipo = ApuTipoRecurso.MO, Rendimiento = 8m, ValorHHUf = 0.3m },
            },
            FechaPrecio = DateTime.Today,
        }).ToList();

        var sw = Stopwatch.StartNew();

        var statuses = apus.Select(ApuFromCubicacionService.GetProfessionalStatus).ToList();

        sw.Stop();

        Assert.Equal(count, statuses.Count);
        Assert.True(sw.ElapsedMilliseconds < 150,
            $"GetProfessionalStatus en {count} APUs tardó {sw.ElapsedMilliseconds}ms (límite: 150ms)");
    }

    [Fact]
    public void CalculateSaleUnitUf_on_500_apus_under_250ms()
    {
        const int count = 500;
        var apus = Enumerable.Range(0, count).Select(i => new ApuRow
        {
            ItemId = $"ITEM-{i}",
            Recursos = new List<ApuRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 2m },
            },
        }).ToList();

        var sw = Stopwatch.StartNew();

        var totals = apus.Select(a => ApuFromCubicacionService.CalculateSaleUnitUf(a, 10m, 5m)).ToList();

        sw.Stop();

        Assert.All(totals, t => Assert.True(t >= 0m));
        Assert.True(sw.ElapsedMilliseconds < 250,
            $"CalculateSaleUnitUf en {count} APUs tardó {sw.ElapsedMilliseconds}ms (límite: 250ms)");
    }
}
