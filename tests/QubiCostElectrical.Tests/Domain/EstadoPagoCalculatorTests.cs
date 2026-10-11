using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public class EstadoPagoCalculatorTests
{
    // Partidas del demo de docs/propuesta-estados-pago.html (curva acumulada mensual).
    private static readonly (string Id, decimal Monto, decimal[] Cum)[] Demo =
    {
        ("01.01", 186400m, new[] { 6m, 14m, 22m, 30m, 40m, 50m }),
        ("01.02", 214850m, new[] { 4m, 10m, 17m, 25m, 33m, 42m }),
        ("01.03", 328600m, new[] { 0m, 4m, 9m, 15m, 22m, 31m }),
        ("01.04", 142300m, new[] { 0m, 0m, 5m, 10m, 16m, 24m }),
        ("02.01", 176950m, new[] { 0m, 0m, 0m, 4m, 9m, 15m }),
        ("02.02", 68400m,  new[] { 8m, 18m, 28m, 40m, 52m, 64m }),
        ("03.01", 94700m,  new[] { 0m, 0m, 6m, 14m, 24m, 36m }),
        ("04.01", 58900m,  new[] { 0m, 0m, 0m, 0m, 0m, 0m }),
    };

    private static readonly decimal DemoTotal = Demo.Sum(x => x.Monto); // 1.271.100

    private static List<EstadoPagoPartidaInput> Partidas()
        => Demo.Select(x => new EstadoPagoPartidaInput(x.Id, x.Id, "Partida " + x.Id, x.Monto)).ToList();

    private static Dictionary<string, decimal> Cum(int mesIndex)
        => Demo.ToDictionary(x => x.Id, x => x.Cum[mesIndex], System.StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, decimal> Avances(int mesIndex)
    {
        var av = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var x in Demo)
        {
            decimal anterior = mesIndex <= 0 ? 0m : x.Cum[mesIndex - 1];
            av[x.Id] = x.Cum[mesIndex] - anterior;
        }
        return av;
    }

    private static EstadoPagoConfig Cfg(decimal ant = 10m, decimal ret = 5m, decimal iva = 19m, int meses = 12)
        => new() { AnticipoPct = ant, RetencionPct = ret, IvaPct = iva, CantMeses = meses };

    // Test 1: junio del demo — paridad exacta con calculoMes() del mockup
    [Fact]
    public void CalcularMes_JunioDemo_ParidadConMockup()
    {
        var detalle = EstadoPagoCalculator.CalcularMes(Partidas(), Cum(4), Avances(5), Cfg());
        var r = detalle.Totales;

        Assert.Equal(109123.5m, r.TrabajoMes);
        Assert.Equal(314742m, r.AcumAntes);
        Assert.Equal(423865.5m, r.Acum);
        Assert.Equal(10912.35m, r.RecuperoMes);
        Assert.Equal(127110m, r.AnticipoTotal);
        Assert.Equal(84723.45m, r.AnticipoSaldo);
        Assert.Equal(5456.175m, r.RetencionMes);
        Assert.Equal(21193.275m, r.RetencionAcum);
        Assert.Equal(92754.975m, r.Neto);
        Assert.Equal(17623.44525m, r.Iva);
        Assert.Equal(110378.42025m, r.Total);
        Assert.Equal(33.34635m, r.PctAcum, 5);
    }

    // Test 2: filas del detalle — monto del mes y acumulado por partida
    [Fact]
    public void CalcularMes_Filas_DerivanDeAvanceYAcumulado()
    {
        var detalle = EstadoPagoCalculator.CalcularMes(Partidas(), Cum(4), Avances(5), Cfg());
        var fila = detalle.Filas.Single(f => f.ItemId == "01.01");

        Assert.Equal(40m, fila.AvanceAnterior);
        Assert.Equal(10m, fila.AvanceMes);
        Assert.Equal(18640m, fila.MontoMes);   // 186.400 × 10 %
        Assert.Equal(50m, fila.AcumPct);
    }

    // Test 3: proyecto sin monto → ceros, sin división por cero
    [Fact]
    public void CalcularMes_TotalCero_TotalesEnCero()
    {
        var partidas = new List<EstadoPagoPartidaInput> { new("a", "1.0", "Sin precio", 0m) };
        var detalle = EstadoPagoCalculator.CalcularMes(
            partidas,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal> { ["a"] = 50m },
            Cfg());

        var r = detalle.Totales;
        Assert.Equal(0m, r.TrabajoMes);
        Assert.Equal(0m, r.AnticipoTotal);
        Assert.Equal(0m, r.RecuperoMes);
        Assert.Equal(0m, r.Neto);
        Assert.Equal(0m, r.Iva);
        Assert.Equal(0m, r.Total);
        Assert.Equal(0m, r.PctAcum);
    }

    // Test 4: anticipo + retención > 100 % → el neto no baja de cero
    [Fact]
    public void CalcularMes_NetoNegativo_SeaCero()
    {
        var partidas = new List<EstadoPagoPartidaInput> { new("a", "1.0", "Partida", 1000m) };
        var detalle = EstadoPagoCalculator.CalcularMes(
            partidas,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal> { ["a"] = 100m },
            Cfg(ant: 200m, ret: 50m));

        Assert.Equal(0m, detalle.Totales.Neto);
        Assert.Equal(0m, detalle.Totales.Total);
    }

    // Test 5: el avance del mes se clampea para no pasar del 100 % acumulado
    [Fact]
    public void CalcularMes_AvanceExcesivo_SeaAcumulado100()
    {
        var partidas = new List<EstadoPagoPartidaInput> { new("a", "1.0", "Partida", 500m) };
        var detalle = EstadoPagoCalculator.CalcularMes(
            partidas,
            new Dictionary<string, decimal> { ["a"] = 80m },
            new Dictionary<string, decimal> { ["a"] = 50m },
            Cfg());

        var fila = detalle.Filas.Single();
        Assert.Equal(20m, fila.AvanceMes);
        Assert.Equal(100m, fila.AcumPct);
        Assert.Equal(100m, fila.MontoMes); // 500 × 20 %
    }

    // Test 6: BuildAcumulado suma solo hasta el mes pedido y respeta año y tope 100 %
    [Fact]
    public void BuildAcumulado_SumaHastaElMes_YFiltraAnio()
    {
        var m1 = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(2026, 1),
            Anio = 2026,
            Mes = 1,
            AvancesPorItem = new Dictionary<string, decimal> { ["a"] = 60m, ["b"] = 90m }
        };
        var m2 = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(2026, 2),
            Anio = 2026,
            Mes = 2,
            AvancesPorItem = new Dictionary<string, decimal> { ["a"] = 60m, ["b"] = 25m }
        };
        var otroAnio = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(2025, 3),
            Anio = 2025,
            Mes = 3,
            AvancesPorItem = new Dictionary<string, decimal> { ["a"] = 10m }
        };

        var acum = EstadoPagoCalculator.BuildAcumulado(new[] { m1, m2, otroAnio }, 2026, 1);

        Assert.Equal(60m, acum["a"]);
        Assert.Equal(90m, acum["b"]); // aún no suma febrero

        var acum2 = EstadoPagoCalculator.BuildAcumulado(new[] { m1, m2, otroAnio }, 2026, 2);
        Assert.Equal(100m, acum2["a"]); // 60 + 60 clampeado a 100
        Assert.Equal(100m, acum2["b"]); // 90 + 25 clampeado a 100
    }

    // Test 7: KPIs del demo (mesBase = junio) con 3 estados pagados
    [Fact]
    public void BuildKpis_DemoJunio_ParidadConMockup()
    {
        var meses = new[]
        {
            new EstadoPagoMesRow { Id = "2026-01", Anio = 2026, Mes = 1, Estado = EstadoPagoEst.Pago },
            new EstadoPagoMesRow { Id = "2026-02", Anio = 2026, Mes = 2, Estado = EstadoPagoEst.Pago },
            new EstadoPagoMesRow { Id = "2026-03", Anio = 2026, Mes = 3, Estado = EstadoPagoEst.Pago },
            new EstadoPagoMesRow { Id = "2026-06", Anio = 2026, Mes = 6, Estado = EstadoPagoEst.Borrador },
        };

        var kpis = EstadoPagoCalculator.BuildKpis(Partidas(), Cum(5), meses, Cfg());

        Assert.Equal(DemoTotal, kpis.TotalContratado);
        Assert.Equal(3, kpis.Pagados);
        Assert.Equal(12, kpis.CantMeses);
        Assert.Equal(423865.5m, kpis.Acumulado);
        Assert.Equal(DemoTotal - 423865.5m, kpis.Saldo);
        Assert.Equal(21193.275m, kpis.RetencionAcum, 3);
        Assert.True(kpis.AvanceFisicoPct > 0m && kpis.AvanceFisicoPct <= 100m);
    }

    // Test 8: la copia para "Duplicar estado anterior" clampea cada partida al saldo del acumulado anterior
    [Fact]
    public void ClampedCopy_LimitaAlSaldoDelAcumuladoAnterior()
    {
        var origen = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(2026, 2),
            Anio = 2026,
            Mes = 2,
            AvancesPorItem = new Dictionary<string, decimal> { ["a"] = 60m, ["b"] = -5m, ["c"] = 30m }
        };
        var anterior = new Dictionary<string, decimal> { ["a"] = 70m, ["b"] = 0m };

        var copia = EstadoPagoCalculator.ClampedCopy(origen, anterior);

        Assert.Equal(30m, copia["a"]); // saldo 30 (70 + 30 = 100)
        Assert.Equal(0m, copia["b"]);   // negativo se recorta a 0
        Assert.Equal(30m, copia["c"]);  // sin acumulado previo: libre hasta 100
    }
}
