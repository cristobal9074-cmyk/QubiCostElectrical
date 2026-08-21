using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class CubicacionSCEngineTests
{
    private static CubicacionScConfig DefaultConfig() => CubicacionScConfig.Default();

    private static CubicacionScConfig EmptyConfig() => new()
    {
        TiposTuberia = new(),
        DiametrosTuberia = new(),
        TiposConductor = new(),
        SeccionesConductor = new(),
        TiposCaja = new(),
        Sistemas = new(),
        Flexibles = new(),
        Artefactos = new()
    };

    private static CircuitoSCRow ActiveCircuito(string seccion = "20mm") => new()
    {
        Estado = "Activo",
        TipoTuberia = "PVCLH",
        SeccionTuberia = seccion,
        ConductorDerivacion = "H07V-U",
        SeccionDerivacion = "4mm2",
        ConductorDistribucion = "H07V-U",
        SeccionDistribucion = "1.5mm2",
        DistanciaTablero = 10m,
        MetrosTuberia = 20m,
        NumConductores = 3m,
        MermaPct = 0m,
        Cajas100x65 = 1
    };

    [Fact]
    public void Circuito_Disponible_NoAgregaMateriales()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(new CircuitoSCRow { Estado = "Disponible" });

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void Circuito_PorTuberia_AgregaTuberiaYConductorPvc()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm")); // TipoRecorrido "Tubería" por defecto

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.True(result.ContainsKey("TUBERIA PVCLH 20mm"));
        // Circuito por tubería: solo aporta el conductor del tramo PVC (derivación).
        Assert.True(result.ContainsKey("CONDUCTOR H07V-U 4mm2"));
        Assert.False(result.ContainsKey("CONDUCTOR H07V-U 1.5mm2"));
    }

    [Fact]
    public void Circuito_PorBandeja_AgregaConductorBandeja()
    {
        var c = ActiveCircuito("20mm");
        c.MetrosTuberia = 0m;
        c.MetrosBandeja = 20m;
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        // Circuito por bandeja: solo aporta el conductor del tramo bandeja (distribución).
        Assert.True(result.ContainsKey("CONDUCTOR H07V-U 1.5mm2"));
        Assert.False(result.ContainsKey("CONDUCTOR H07V-U 4mm2"));
    }

    [Fact]
    public void Circuito_Activo_AgregaTuberiaFlexibleCuandoEstaConfigurada()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm")); // Cajas100x65 = 1 por defecto

        // DefaultConfig tiene 20mm→FLEX 3/4 con 0.5 ML/caja
        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.True(result.ContainsKey("TUBERIA FLEXIBLE 20mm"), "Debe agregar tubería flexible cuando hay mapeo y cajas");
        Assert.Equal(0.5m, result["TUBERIA FLEXIBLE 20mm"].Cantidad);
    }

    [Fact]
    public void Circuito_Activo_SinFlexConfig_NoAgregaFlexible()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("63mm")); // sección sin mapeo en DefaultConfig

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.DoesNotContain(result.Keys, k => k.StartsWith("FLEX"));
    }

    [Fact]
    public void Artefactos_DeDistintasZonas_SeAcumulanEnElTotalDeMaterial()
    {
        var c = ActiveCircuito("20mm");
        // Mismo artefacto repartido en dos zonas del proyecto (control por piso), pero el total
        // de material del proyecto debe sumar ambas.
        c.Artefactos.Add(new ArtefactoSCRow { Nombre = "Downlight LED 12W", Cantidad = 3m, ZonaId = "z1" });
        c.Artefactos.Add(new ArtefactoSCRow { Nombre = "Downlight LED 12W", Cantidad = 2m, ZonaId = "z2" });
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.Equal(5m, result["Downlight LED 12W"].Cantidad);
    }

    [Fact]
    public void AggregateMaterialesPorPiso_AgrupaPorTextoDeUbicacionEscritoAMano()
    {
        var c1 = ActiveCircuito("20mm"); c1.Ubicacion = "1";
        var c2 = ActiveCircuito("20mm"); c2.Ubicacion = "1"; // mismo piso → suma
        var c3 = ActiveCircuito("20mm"); c3.Ubicacion = "2";
        var c4 = ActiveCircuito("20mm"); c4.Ubicacion = "";  // sin piso
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c1); cuadro.Circuitos.Add(c2); cuadro.Circuitos.Add(c3); cuadro.Circuitos.Add(c4);

        var porPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(new[] { cuadro }, DefaultConfig());

        Assert.Equal(new[] { "1", "2", "Sin piso" }, porPiso.Select(g => g.Nombre));
        // Piso "1" tiene dos circuitos de 20m de tubería = 40 ML.
        Assert.Equal(40m, porPiso[0].TuberiaMl);
        Assert.Equal(20m, porPiso[1].TuberiaMl);
    }

    [Fact]
    public void AggregateMaterialesPorPiso_ConTramos_RepartePorZona()
    {
        var c = ActiveCircuito("20mm");
        c.MetrosTuberia = 0m; c.MetrosBandeja = 0m;
        c.PisosTramos.Add(new CircuitoPisoTramo { ZonaId = "z1", ZonaNombre = "Piso 1", MetrosTuberia = 30m });
        c.PisosTramos.Add(new CircuitoPisoTramo { ZonaId = "z2", ZonaNombre = "Piso 2", MetrosTuberia = 70m });
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var porPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(new[] { cuadro }, DefaultConfig());

        var p1 = porPiso.Single(g => g.Nombre == "Piso 1");
        var p2 = porPiso.Single(g => g.Nombre == "Piso 2");
        Assert.Equal(30m, p1.TuberiaMl);  // tramo piso 1
        Assert.Equal(70m, p2.TuberiaMl);  // tramo piso 2
        // El circuito aporta 100m de tubería en total, repartidos por tramo.
        Assert.Equal(100m, p1.TuberiaMl + p2.TuberiaMl);
    }

    [Fact]
    public void AggregateMaterialesPorPiso_PisoManualConMismoNombreDeZona_SeFusiona()
    {
        // Un circuito de un solo piso escrito a mano ("Piso 1") debe sumarse con los circuitos
        // cubicados por zona cuyo nombre de zona coincide.
        var manual = ActiveCircuito("20mm");
        manual.Ubicacion = "Piso 1";
        var porZona = ActiveCircuito("20mm");
        porZona.MetrosTuberia = 0m;
        porZona.PisosTramos.Add(new CircuitoPisoTramo { ZonaId = "z1", ZonaNombre = "Piso 1", MetrosTuberia = 15m });
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(manual);
        cuadro.Circuitos.Add(porZona);

        var porPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(new[] { cuadro }, DefaultConfig());

        var grupo = Assert.Single(porPiso);
        Assert.Equal("Piso 1", grupo.Nombre);
        Assert.Equal(35m, grupo.TuberiaMl); // 20 (manual) + 15 (tramo)
    }

    [Fact]
    public void AggregateMaterialesPorPiso_OrdenaSegunOrdenDeZonasDelProyecto()
    {
        var c = ActiveCircuito("20mm");
        c.MetrosTuberia = 0m;
        c.PisosTramos.Add(new CircuitoPisoTramo { ZonaId = "z2", ZonaNombre = "Piso 2", MetrosTuberia = 5m });
        c.PisosTramos.Add(new CircuitoPisoTramo { ZonaId = "z1", ZonaNombre = "Piso 1", MetrosTuberia = 5m });
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);
        var zonas = new List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1 },
            new() { Id = "z2", Nombre = "Piso 2", Orden = 2 },
        };

        var porPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(new[] { cuadro }, DefaultConfig(), zonas);

        Assert.Equal(new[] { "Piso 1", "Piso 2" }, porPiso.Select(g => g.Nombre));
    }

    [Fact]
    public void AggregatePorTablero_UnGrupoPorCuadro_ConSubtotales()
    {
        var cuadro1 = new CuadroCargaSCRow { Nombre = "TDA A" };
        cuadro1.Circuitos.Add(ActiveCircuito("20mm"));
        var cuadro2 = new CuadroCargaSCRow { Nombre = "TDA B" };
        cuadro2.Circuitos.Add(ActiveCircuito("20mm"));
        cuadro2.Circuitos.Add(ActiveCircuito("20mm"));

        var grupos = CubicacionSCEngine.AggregatePorTablero(new[] { cuadro1, cuadro2 }, DefaultConfig());

        Assert.Equal(new[] { "TDA A", "TDA B" }, grupos.Select(g => g.Nombre));
        Assert.Equal(20m, grupos[0].TuberiaMl);
        Assert.Equal(40m, grupos[1].TuberiaMl);
    }

    [Fact]
    public void AggregateTotal_ResumeTodoElProyecto()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm"));
        cuadro.Circuitos.Add(ActiveCircuito("20mm"));

        var total = CubicacionSCEngine.AggregateTotal(new[] { cuadro }, DefaultConfig());

        Assert.Equal(40m, total.TuberiaMl);
        Assert.Contains(total.Materiales, m => m.Nombre == "TUBERIA PVCLH 20mm");
    }

    [Fact]
    public void AggregateArtefactosPorZona_AgrupaYSuma_ConSinZonaAlFinal()
    {
        var zonas = new List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1, Activa = true },
            new() { Id = "z2", Nombre = "Piso 2", Orden = 2, Activa = true },
        };
        var c1 = ActiveCircuito("20mm");
        c1.Artefactos.Add(new ArtefactoSCRow { Nombre = "Downlight", Cantidad = 4m, ZonaId = "z1" });
        c1.Artefactos.Add(new ArtefactoSCRow { Nombre = "Enchufe", Cantidad = 2m, ZonaId = "z2" });
        var c2 = ActiveCircuito("20mm");
        c2.Artefactos.Add(new ArtefactoSCRow { Nombre = "Downlight", Cantidad = 3m, ZonaId = "z1" }); // misma ref/zona → suma
        c2.Artefactos.Add(new ArtefactoSCRow { Nombre = "Sensor", Cantidad = 1m, ZonaId = "zX" });     // zona inexistente → Sin zona
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c1);
        cuadro.Circuitos.Add(c2);

        var resumen = CubicacionSCEngine.AggregateArtefactosPorZona(new[] { cuadro }, zonas);

        Assert.Equal(3, resumen.Count);
        Assert.Equal(new[] { "Piso 1", "Piso 2", "Sin zona" }, resumen.Select(r => r.ZonaNombre));
        Assert.Equal(7m, resumen[0].Total); // Downlight 4+3
        Assert.Equal(7m, resumen[0].Artefactos.Single(a => a.Nombre == "Downlight").Cantidad);
        Assert.Equal(2m, resumen[1].Total);
        Assert.Equal("Sin zona", resumen[2].ZonaNombre);
        Assert.Equal(1m, resumen[2].Total);
    }

    [Fact]
    public void ReservaEquipada_SinArtefactos_AgregaInterruptorDefecto()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(new CircuitoSCRow { Estado = "Reserva equipada" });

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.True(result.ContainsKey("INT 9/12"));
        Assert.Equal(1m, result["INT 9/12"].Cantidad);
    }

    [Fact]
    public void ReservaEquipada_ConArtefactos_NoAgregaInterruptorDefecto()
    {
        var cuadro = new CuadroCargaSCRow();
        var c = new CircuitoSCRow { Estado = "Reserva equipada" };
        c.Artefactos.Add(new ArtefactoSCRow { Nombre = "INT 9/24", Cantidad = 1m });
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        Assert.True(result.ContainsKey("INT 9/24"));
        Assert.False(result.ContainsKey("INT 9/12"));
    }

    [Fact]
    public void Materiales_Iguales_SeAcumulan()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm"));
        cuadro.Circuitos.Add(ActiveCircuito("20mm"));

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        // Cada circuito aporta LargoTuberia = LargoCircuito = 20m → total 40m
        Assert.Equal(40m, result["TUBERIA PVCLH 20mm"].Cantidad);
    }

    [Fact]
    public void HasFlexiblesWithoutConfig_ReturnTrue_CuandoHayCajasYSinMapeo()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("63mm")); // 63mm no está en DefaultConfig.Flexibles

        Assert.True(CubicacionSCEngine.HasFlexiblesWithoutConfig(new[] { cuadro }, DefaultConfig()));
    }

    [Fact]
    public void HasFlexiblesWithoutConfig_ReturnFalse_CuandoTodosMapean()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm")); // 20mm sí tiene mapeo

        Assert.False(CubicacionSCEngine.HasFlexiblesWithoutConfig(new[] { cuadro }, DefaultConfig()));
    }

    // --- Argumentos nulos ---

    [Fact]
    public void Aggregate_NullCuadros_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CubicacionSCEngine.Aggregate(null!, DefaultConfig()));
    }

    [Fact]
    public void Aggregate_NullConfig_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CubicacionSCEngine.Aggregate(Array.Empty<CuadroCargaSCRow>(), null!));
    }

    // --- Listas vacías / sin circuitos ---

    [Fact]
    public void Aggregate_ListaVacia_RetornaVacio()
    {
        var result = CubicacionSCEngine.Aggregate(Array.Empty<CuadroCargaSCRow>(), DefaultConfig());
        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_TableroSinCircuitos_RetornaVacio()
    {
        var cuadro = new CuadroCargaSCRow(); // sin Circuitos
        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());
        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_ConfigVacia_CircuitoActivo_NoLanzaExcepcion()
    {
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(ActiveCircuito("20mm"));

        // Config vacía no tiene flexibles → no debe tirar excepción
        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, EmptyConfig());

        Assert.True(result.ContainsKey("TUBERIA PVCLH 20mm"), "Sigue calculando tubería con config vacía");
        Assert.DoesNotContain(result.Keys, k => k.StartsWith("TUBERIA FLEXIBLE", StringComparison.OrdinalIgnoreCase));
    }

    // --- MermaPct afecta el largo de conductores ---

    [Fact]
    public void Circuito_ConMerma_EscalaLargoDerivacionYDistribucion()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            TipoTuberia = "PVC",
            SeccionTuberia = "20mm",
            ConductorDerivacion = "H07V-U",
            SeccionDerivacion = "4mm2",
            ConductorDistribucion = "H07V-U",
            SeccionDistribucion = "1.5mm2",
            DistanciaTablero = 10m,
            MetrosTuberia = 20m,
            NumConductores = 3m,
            MermaPct = 10m  // +10% sobre los conductores
        };
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, DefaultConfig());

        // Circuito por tubería: el conductor PVC (derivación) sale de L. PVC × conductores × merma,
        // redondeado a entero: 20 * 3 * 1.10 = 66. El conductor de bandeja no aplica.
        Assert.Equal(66m, result["CONDUCTOR H07V-U 4mm2"].Cantidad);
        Assert.False(result.ContainsKey("CONDUCTOR H07V-U 1.5mm2"));
    }

    [Fact]
    public void Circuito_SinMerma_LargoEsExacto()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            TipoTuberia = "PVC",
            SeccionTuberia = "20mm",
            ConductorDerivacion = "H07V-U",
            SeccionDerivacion = "4mm2",
            ConductorDistribucion = "H07V-U",
            SeccionDistribucion = "1.5mm2",
            DistanciaTablero = 5m,
            MetrosTuberia = 15m,
            NumConductores = 2m,
            MermaPct = 0m
        };
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, EmptyConfig());

        // Circuito por tubería: conductor PVC = L. PVC(15) × conductores(2) = 30. Bandeja no aplica.
        Assert.Equal(30m, result["CONDUCTOR H07V-U 4mm2"].Cantidad);
        Assert.False(result.ContainsKey("CONDUCTOR H07V-U 1.5mm2"));
    }

    // --- Sensores, señalética, cinta LED ---

    [Fact]
    public void Circuito_Activo_AgregaSensoresSeñaleticaYCintaLed()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            TipoTuberia = "PVC",
            SeccionTuberia = "20mm",
            ConductorDerivacion = "H07V-U",
            SeccionDerivacion = "4mm2",
            ConductorDistribucion = "H07V-U",
            SeccionDistribucion = "1.5mm2",
            DistanciaTablero = 1m,
            LargoCircuito = 1m,
            NumConductores = 1m,
            SensoresMovimiento = 2m,
            Senaleticas = 1m,
            CintaLedMts = 5m
        };
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, EmptyConfig());

        Assert.Equal(2m, result["SENSOR DE MOVIMIENTO"].Cantidad);
        Assert.Equal(1m, result["SEÑALETICA EMERGENCIA"].Cantidad);
        Assert.Equal(5m, result["CINTA LED"].Cantidad);
    }

    // --- Cajas acumulan correctamente ---

    [Fact]
    public void Circuito_Activo_AgregaTodasLasCajas()
    {
        var c = new CircuitoSCRow
        {
            Estado = "Activo",
            TipoTuberia = "PVC",
            SeccionTuberia = "63mm",
            ConductorDerivacion = "H07V-U",
            SeccionDerivacion = "4mm2",
            ConductorDistribucion = "H07V-U",
            SeccionDistribucion = "1.5mm2",
            DistanciaTablero = 1m,
            LargoCircuito = 1m,
            NumConductores = 1m,
            Cajas58 = 2,
            Cajas100x100 = 1,
            Cajas100x65 = 3
        };
        var cuadro = new CuadroCargaSCRow();
        cuadro.Circuitos.Add(c);

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro }, EmptyConfig());

        Assert.Equal(2m, result["CAJA 5/8"].Cantidad);
        Assert.Equal(1m, result["CAJA 100X100"].Cantidad);
        Assert.Equal(3m, result["CAJA 100X65"].Cantidad);
    }

    // --- Múltiples tableros ---

    [Fact]
    public void MultiplesTableros_AgregaTodasLasTuberias()
    {
        var cuadro1 = new CuadroCargaSCRow();
        cuadro1.Circuitos.Add(ActiveCircuito("20mm"));

        var cuadro2 = new CuadroCargaSCRow();
        cuadro2.Circuitos.Add(ActiveCircuito("20mm"));
        cuadro2.Circuitos.Add(ActiveCircuito("20mm"));

        var result = CubicacionSCEngine.Aggregate(new[] { cuadro1, cuadro2 }, DefaultConfig());

        // 3 circuitos × 20m = 60m de tubería
        Assert.Equal(60m, result["TUBERIA PVCLH 20mm"].Cantidad);
    }

    // ── ApplySistema ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplySistema_SistemaExistente_CopiaConductoresYMerma()
    {
        var config = new CubicacionScConfig
        {
            Sistemas = new() { new SistemaSC { Nombre = "Trifásico", NumConductores = 4m, MermaPct = 10m } }
        };
        var c = new CircuitoSCRow();

        CubicacionSCEngine.ApplySistema(c, "Trifásico", config);

        Assert.Equal("Trifásico", c.Sistema);
        Assert.Equal(4m, c.NumConductores);
        Assert.Equal(10m, c.MermaPct);
    }

    [Fact]
    public void ApplySistema_SistemaInexistente_SoloAsignaNombre()
    {
        var config = new CubicacionScConfig { Sistemas = new() };
        var c = new CircuitoSCRow { NumConductores = 3m, MermaPct = 5m };

        CubicacionSCEngine.ApplySistema(c, "Desconocido", config);

        Assert.Equal("Desconocido", c.Sistema);
        Assert.Equal(3m, c.NumConductores);
        Assert.Equal(5m, c.MermaPct);
    }

    [Fact]
    public void ApplySistema_NombreNulo_AsignaVacio()
    {
        var config = new CubicacionScConfig { Sistemas = new() };
        var c = new CircuitoSCRow();

        CubicacionSCEngine.ApplySistema(c, null!, config);

        Assert.Equal("", c.Sistema);
    }

    [Fact]
    public void ApplySistema_EsInsensibleAMayusculas()
    {
        var config = new CubicacionScConfig
        {
            Sistemas = new() { new SistemaSC { Nombre = "monofasico", NumConductores = 2m, MermaPct = 0m } }
        };
        var c = new CircuitoSCRow();

        CubicacionSCEngine.ApplySistema(c, "MONOFASICO", config);

        Assert.Equal(2m, c.NumConductores);
    }

    // Regresión: circuitos por bandeja no deben sumar metros de tubería (y viceversa) —
    // antes de agregar TipoRecorrido, LargoTuberia siempre devolvía LargoCircuito sin importar
    // cómo se canalizara realmente el circuito.
    // Los dos tramos (bandeja y tubería) son independientes: cada uno usa su propio metraje y no
    // se anulan entre sí. Un circuito puede tener ambos.
    [Fact]
    public void Tramos_SonIndependientes_SoloBandeja()
    {
        var c = ActiveCircuito();
        c.MetrosTuberia = 0m;
        c.MetrosBandeja = 20m;

        Assert.Equal(0m, c.LargoTuberia);
        Assert.Equal(20m, c.LargoBandeja);
        Assert.Equal(20m, c.LargoTotal);
    }

    [Fact]
    public void Tramos_SonIndependientes_AmbosTramos()
    {
        var c = ActiveCircuito();
        c.MetrosBandeja = 8m;
        c.MetrosTuberia = 12m;

        Assert.Equal(12m, c.LargoTuberia);
        Assert.Equal(8m, c.LargoBandeja);
        Assert.Equal(20m, c.LargoTotal);
        // Cada total sale de su propio tramo × conductores (3).
        Assert.Equal(24m, c.LargoDistribucion); // bandeja 8 × 3
        Assert.Equal(36m, c.LargoDerivacion);   // tubería 12 × 3
    }

    [Fact]
    public void Default_Tipos_IncluyeLosCincoSistemasDeUso()
    {
        var config = CubicacionScConfig.Default();

        Assert.Equal(new[] { "Alumbrado", "Enchufe", "Clima", "Computación", "Emergencia" }, config.Tipos);
    }

    [Fact]
    public void Default_Artefactos_TienenCategoria()
    {
        var config = CubicacionScConfig.Default();

        Assert.All(config.Artefactos, a => Assert.False(string.IsNullOrWhiteSpace(a.Categoria)));
        Assert.Contains(config.Artefactos, a => a.Nombre == "Downlight LED 12W" && a.Categoria == "Luminaria");
    }
}
