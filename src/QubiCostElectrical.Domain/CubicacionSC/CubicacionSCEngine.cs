using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Motor de cubicación C.SC: agrega los materiales de todos los circuitos activos y reservas
/// equipadas, aplicando la configuración de tubería flexible por diámetro.
/// Equivale a la hoja RESUMEN del Excel de origen.
/// </summary>
public static class CubicacionSCEngine
{
    public readonly record struct MaterialSC(string Nombre, decimal Cantidad, string Unidad);

    /// <summary>Material SC agregado con desglose por zona del proyecto (para «Tomar de SC»).</summary>
    public sealed record MaterialConZonasSC(
        string Nombre,
        string Unidad,
        string Categoria,
        decimal Total,
        IReadOnlyList<(string ZonaNombre, decimal Cantidad)> Zonas);

    // Grupo de materiales (de un tablero o de un piso) con sus subtotales por categoría, para el
    // resumen. Materiales viene ordenado por categoría y nombre.
    public sealed record ResumenGrupoSC(
        string Nombre, string Detalle,
        decimal TuberiaMl, decimal ConductorMl, int Cajas, int Artefactos,
        IReadOnlyList<MaterialSC> Materiales);

    /// <summary>
    /// Materiales que aporta UN circuito (tubería, conductores, terminal flexible, artefactos,
    /// sensores/señalética/cinta/montaje y cajas). Fuente única usada por todas las agregaciones.
    /// </summary>
    private static IEnumerable<MaterialSC> CircuitoMateriales(CircuitoSCRow c, CubicacionScConfig config, bool isDepto = false)
    {
        if (c.EsActivo)
        {
            if (c.LargoTuberia > 0m)
                yield return new($"TUBERIA {c.TipoTuberia} {c.SeccionTuberia}", c.LargoTuberia, "ML");
            if (c.LargoTuberia2 > 0m)
                yield return new($"TUBERIA {c.TipoTuberia2} {c.SeccionTuberia2}", c.LargoTuberia2, "ML");
            if (c.LargoDerivacion > 0m)
                yield return new($"CONDUCTOR {c.ConductorDerivacion} {c.SeccionDerivacion}", c.LargoDerivacion, "ML");

            if (!isDepto)
            {
                if (c.LargoDistribucion > 0m)
                    yield return new($"CONDUCTOR {c.ConductorDistribucion} {c.SeccionDistribucion}", c.LargoDistribucion, "ML");

                var flex = config.Flexibles.FirstOrDefault(f =>
                    string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase));
                if (flex != null && c.TotalCajasEfectivas > 0m && flex.MetrosPorCaja > 0m)
                    yield return new(flex.TipoFlexible, c.TotalCajasEfectivas * flex.MetrosPorCaja, "ML");
            }

            if (c.CajasSalidaBandeja > 0m)
            if (c.CajasSalidaBandeja > 0m)
                yield return new("CAJA METÁLICA SALIDA BANDEJA", c.CajasSalidaBandeja, "UN");

            foreach (var a in c.Artefactos)
                if (a.Cantidad > 0m && !string.IsNullOrWhiteSpace(a.Nombre))
                    yield return new(a.Nombre, a.Cantidad, "UN");

            if (c.SensoresMovimiento > 0m)
                yield return new("SENSOR DE MOVIMIENTO", c.SensoresMovimiento, "UN");
            if (c.Senaleticas > 0m)
            if (c.Senaleticas > 0m)
                yield return new("SEÑALETICA EMERGENCIA", c.Senaleticas, "UN");
            if (c.CintaLedMts > 0m)
                yield return new("CINTA LED", c.CintaLedMts, "ML");
            if (c.MontajesAlumbrado > 0m)
                yield return new("MONTAJE LUMINARIA", c.MontajesAlumbrado, "UN");

            if (c.TotalCajas > 0)
            {
                string nombreCaja58 = isDepto ? "CAJA 5/8\"" : "CAJA 5/8";
                string nombreCaja100 = isDepto ? "CAJA A11 (100X100)" : "CAJA 100X100";
                string nombreCaja65 = isDepto ? "CAJA A01 (100X65)" : "CAJA 100X65";

                if (c.Cajas58 > 0)
                    yield return new(nombreCaja58, c.Cajas58, "UN");
                if (c.Cajas100x100 > 0)
                    yield return new(nombreCaja100, c.Cajas100x100, "UN");
                if (c.Cajas100x65 > 0)
                    yield return new(nombreCaja65, c.Cajas100x65, "UN");
            }
            else if (c.TieneElementosConPunto && c.TotalElementosConPunto > 0)
            {
                yield return new(CajaParaCanalizacion(c.TipoTuberia), c.TotalElementosConPunto, "UN");
            }
        }
        else if (c.EsReservaEquipada)
        {
            if (c.Artefactos.Count == 0)
                yield return new("INT 9/12", 1m, "UN");
            else
                foreach (var a in c.Artefactos)
                    if (a.Cantidad > 0m && !string.IsNullOrWhiteSpace(a.Nombre))
                        yield return new(a.Nombre, a.Cantidad, "UN");
        }
    }

    private static string CajaParaCanalizacion(string? tipoTuberia)
    {
        if (string.Equals(tipoTuberia, "PVCLH", StringComparison.OrdinalIgnoreCase))
            return "CAJA 5/8 LH";
        if (string.Equals(tipoTuberia, "PVC", StringComparison.OrdinalIgnoreCase))
            return "CAJA 5/8";
        if (string.Equals(tipoTuberia, "EMT", StringComparison.OrdinalIgnoreCase))
            return "CAJA METÁLICA A01";
        return "CAJA SEGÚN CANALIZACIÓN";
    }

    private static void AddMaterial(IDictionary<string, MaterialSC> totals, MaterialSC m)
    {
        if (m.Cantidad <= 0m || string.IsNullOrWhiteSpace(m.Nombre)) return;
        var nombre = m.Nombre.Trim();
        totals[nombre] = totals.TryGetValue(nombre, out var cur)
            ? cur with { Cantidad = cur.Cantidad + m.Cantidad }
            : new MaterialSC(nombre, m.Cantidad, m.Unidad);
    }

    // El subtotal "Tubería (ML)" es la canalización (conduit); el terminal flexible ("TUBERIA
    // FLEXIBLE …") queda fuera del KPI aunque sí aparece en el detalle de materiales.
    private static bool EsTuberia(string nombre) =>
        nombre.StartsWith("TUBERIA", StringComparison.OrdinalIgnoreCase) &&
        nombre.IndexOf("FLEXIBLE", StringComparison.OrdinalIgnoreCase) < 0;
    private static bool EsConductor(string nombre) => nombre.StartsWith("CONDUCTOR", StringComparison.OrdinalIgnoreCase);
    private static bool EsCaja(string nombre) => nombre.StartsWith("CAJA", StringComparison.OrdinalIgnoreCase);

    private static ResumenGrupoSC BuildGrupo(string nombre, string detalle, IReadOnlyDictionary<string, MaterialSC> totals)
    {
        var materiales = totals.Values
            .OrderBy(m => EsTuberia(m.Nombre) ? 0 : EsConductor(m.Nombre) ? 1 : EsCaja(m.Nombre) ? 3 : 2)
            .ThenBy(m => m.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
        decimal tuberia = materiales.Where(m => EsTuberia(m.Nombre)).Sum(m => m.Cantidad);
        decimal conductor = materiales.Where(m => EsConductor(m.Nombre)).Sum(m => m.Cantidad);
        int cajas = (int)Math.Round(materiales.Where(m => EsCaja(m.Nombre)).Sum(m => m.Cantidad));
        int artefactos = (int)Math.Round(materiales
            .Where(m => m.Unidad == "UN" && !EsCaja(m.Nombre)).Sum(m => m.Cantidad));
        return new ResumenGrupoSC(nombre, detalle, tuberia, conductor, cajas, artefactos, materiales);
    }

    /// <summary>
    /// Calcula el resumen de materiales para todos los cuadros de carga del proyecto.
    /// </summary>
    public static IReadOnlyDictionary<string, MaterialSC> Aggregate(
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);

        var totals = new Dictionary<string, MaterialSC>(StringComparer.OrdinalIgnoreCase);
        foreach (var cuadro in cuadros)
        {
            bool isDepto = cuadro.EsDepto;
            foreach (var c in cuadro.Circuitos)
                foreach (var m in CircuitoMateriales(c, config, isDepto))
                    AddMaterial(totals, m);
        }
        return totals;
    }

    /// <summary>Resumen total del proyecto como un solo grupo (con subtotales por categoría).</summary>
    public static ResumenGrupoSC AggregateTotal(IEnumerable<CuadroCargaSCRow> cuadros, CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);
        var lista = cuadros.ToList();
        var totals = (Dictionary<string, MaterialSC>)Aggregate(lista, config);
        int circuitos = lista.Sum(t => t.Circuitos.Count(c => c.EsActivo || c.EsReservaEquipada));
        return BuildGrupo("Total proyecto", $"{lista.Count} tablero(s) · {circuitos} circuitos", totals);
    }

    /// <summary>Materiales agrupados por tablero (cada cuadro de carga es un grupo).</summary>
    public static IReadOnlyList<ResumenGrupoSC> AggregatePorTablero(IEnumerable<CuadroCargaSCRow> cuadros, CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);
        var grupos = new List<ResumenGrupoSC>();
        foreach (var cuadro in cuadros)
        {
            bool isDepto = cuadro.EsDepto;
            var totals = new Dictionary<string, MaterialSC>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in cuadro.Circuitos)
                foreach (var m in CircuitoMateriales(c, config, isDepto))
                    AddMaterial(totals, m);
            if (totals.Count == 0) continue;
            int circuitos = cuadro.Circuitos.Count(c => c.EsActivo || c.EsReservaEquipada);
            grupos.Add(BuildGrupo(cuadro.Nombre, $"{circuitos} circuitos", totals));
        }
        return grupos;
    }

    private const string SinPisoKey = "";

    // Materiales de un circuito atribuidos a una etiqueta de piso (texto). Circuitos cubicados
    // por zona (PisosTramos, elegidas del catálogo de zonas del proyecto) reparten tubería/
    // conductor por tramo y el resto (flexible, cajas, artefactos, sensores, montaje) al primer
    // tramo. Circuitos con piso escrito a mano (Ubicación, un solo piso) van a ese texto tal
    // cual — así, si coincide con el nombre de una zona, se fusiona con esos totales.
    private static IEnumerable<(string PisoLabel, MaterialSC Mat)> CircuitoMaterialesPorPiso(CircuitoSCRow c, CubicacionScConfig config, bool isDepto = false)
    {
        if (c.EsActivo && c.TienePisos)
        {
            string primerTramo = TramoLabel(c.PisosTramos[0]);
            decimal factor = c.NumConductores * c.FactorMerma;
            foreach (var tramo in c.PisosTramos)
            {
                var label = TramoLabel(tramo);
                if (tramo.MetrosTuberia > 0m)
                {
                    yield return (label, new($"TUBERIA {c.TipoTuberia} {c.SeccionTuberia}", tramo.MetrosTuberia, "ML"));
                    var deriv = Math.Round(tramo.MetrosTuberia * factor, 0, MidpointRounding.AwayFromZero);
                    if (deriv > 0m)
                        yield return (label, new($"CONDUCTOR {c.ConductorDerivacion} {c.SeccionDerivacion}", deriv, "ML"));
                }
                if (!isDepto && tramo.MetrosBandeja > 0m)
                {
                    var dist = Math.Round(tramo.MetrosBandeja * factor, 0, MidpointRounding.AwayFromZero);
                    if (dist > 0m)
                        yield return (label, new($"CONDUCTOR {c.ConductorDistribucion} {c.SeccionDistribucion}", dist, "ML"));
                }
            }
            if (c.MetrosTuberia2 > 0m)
            {
                yield return (primerTramo, new($"TUBERIA {c.TipoTuberia2} {c.SeccionTuberia2}", c.MetrosTuberia2, "ML"));
                var deriv2 = Math.Round(c.MetrosTuberia2 * factor, 0, MidpointRounding.AwayFromZero);
                if (deriv2 > 0m)
                    yield return (primerTramo, new($"CONDUCTOR {c.ConductorDerivacion} {c.SeccionDerivacion}", deriv2, "ML"));
            }
            // Materiales no dependientes del largo -> al primer tramo del circuito.
            foreach (var m in CircuitoMateriales(c, config, isDepto).Where(m => !EsTuberia(m.Nombre) && !EsConductor(m.Nombre)))
                if (m.Cantidad > 0m)
                    yield return (primerTramo, m);
        }
        else
        {
            var piso = string.IsNullOrWhiteSpace(c.Ubicacion) ? SinPisoKey : c.Ubicacion.Trim();
            foreach (var m in CircuitoMateriales(c, config, isDepto))
                if (m.Cantidad > 0m)
                    yield return (piso, m);
        }
    }

    private static string TramoLabel(CircuitoPisoTramo t) =>
        string.IsNullOrWhiteSpace(t.ZonaNombre) ? "Sin zona" : t.ZonaNombre.Trim();

    /// <summary>
    /// Agrega materiales SC con cantidades por zona del proyecto, para el diálogo «Tomar de SC».
    /// </summary>
    public static IReadOnlyList<MaterialConZonasSC> AggregateConZonas(
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config,
        IReadOnlyList<ZonaProyecto>? zonas = null,
        Func<CircuitoSCRow, bool>? circuitPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);

        var porMaterial = new Dictionary<string, Dictionary<string, (decimal Cantidad, string Unidad)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cuadro in cuadros)
        {
            bool isDepto = cuadro.EsDepto;
            foreach (var circuito in cuadro.Circuitos)
            {
                if (!circuito.EsActivo && !circuito.EsReservaEquipada) continue;
                if (circuitPredicate != null && !circuitPredicate(circuito)) continue;
                foreach (var (pisoLabel, mat) in CircuitoMaterialesPorPiso(circuito, config, isDepto))
                {
                    if (mat.Cantidad <= 0m || string.IsNullOrWhiteSpace(mat.Nombre)) continue;
                    if (!porMaterial.TryGetValue(mat.Nombre, out var porZona))
                    {
                        porZona = porMaterial[mat.Nombre] = new(StringComparer.OrdinalIgnoreCase);
                    }
                    var prev = porZona.TryGetValue(pisoLabel, out var cur) ? cur.Cantidad : 0m;
                    porZona[pisoLabel] = (prev + mat.Cantidad, mat.Unidad);
                }
            }
        }

        var ordenZona = (zonas ?? Array.Empty<ZonaProyecto>())
            .Select((z, i) => (z.Nombre, Orden: i))
            .GroupBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Orden, StringComparer.OrdinalIgnoreCase);

        var resultado = new List<MaterialConZonasSC>();
        foreach (var (nombre, porZona) in porMaterial)
        {
            var total = porZona.Values.Sum(z => z.Cantidad);
            if (total <= 0m) continue;
            var unidad = porZona.Values.First().Unidad;
            var categoria = GetCategoriaFromNombre(nombre);
            var zonasList = new List<(string ZonaNombre, decimal Cantidad)>();

            foreach (var (zonaNombre, _) in ordenZona.OrderBy(kv => kv.Value))
            {
                var qty = porZona.TryGetValue(zonaNombre, out var z) ? z.Cantidad : 0m;
                zonasList.Add((zonaNombre, qty));
            }

            foreach (var (zonaNombre, z) in porZona)
            {
                if (!ordenZona.ContainsKey(zonaNombre))
                    zonasList.Add((zonaNombre, z.Cantidad));
            }

            resultado.Add(new MaterialConZonasSC(nombre, unidad, categoria, total, zonasList));
        }

        return resultado
            .OrderBy(m => m.Categoria)
            .ThenBy(m => m.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetCategoriaFromNombre(string nombre)
    {
        if (nombre.StartsWith("TUBERIA FLEXIBLE", StringComparison.OrdinalIgnoreCase))
            return "Flexible";
        if (nombre.StartsWith("TUBERIA", StringComparison.OrdinalIgnoreCase))
            return "Tubería";
        if (nombre.StartsWith("CONDUCTOR", StringComparison.OrdinalIgnoreCase))
            return "Conductor";
        if (nombre.StartsWith("CAJA", StringComparison.OrdinalIgnoreCase))
            return "Cajas";
        if (nombre.StartsWith("SENSOR", StringComparison.OrdinalIgnoreCase))
            return "Sensores";
        if (nombre.StartsWith("SEÑALETICA", StringComparison.OrdinalIgnoreCase))
            return "Señalética";
        if (nombre.StartsWith("CINTA", StringComparison.OrdinalIgnoreCase))
            return "Cinta LED";
        if (nombre.StartsWith("MONTAJE", StringComparison.OrdinalIgnoreCase))
            return "Montaje";
        if (nombre.StartsWith("INT", StringComparison.OrdinalIgnoreCase))
            return "Interruptores";
        return "Artefactos";
    }

    /// <summary>
    /// Materiales agrupados por piso/zona. Los circuitos cubicados por zona (PisosTramos, del
    /// catálogo de zonas del proyecto) reparten su tubería y conductor entre las zonas según el
    /// metraje de cada tramo; el resto se atribuye a la primera zona. Los circuitos con un piso
    /// escrito a mano se agrupan por ese texto (se fusiona con una zona si el nombre coincide).
    /// Los circuitos sin piso caen en "Sin piso". <paramref name="zonas"/> ordena el resultado
    /// según el orden de las zonas del proyecto; opcional (si se omite, orden alfabético).
    /// </summary>
    public static IReadOnlyList<ResumenGrupoSC> AggregateMaterialesPorPiso(
        IEnumerable<CuadroCargaSCRow> cuadros, CubicacionScConfig config, IReadOnlyList<ZonaProyecto>? zonas = null)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);

        var porPiso = new Dictionary<string, Dictionary<string, MaterialSC>>(StringComparer.OrdinalIgnoreCase);
        var circuitosPorPiso = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var etiquetaOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cuadro in cuadros)
        {
            bool isDepto = cuadro.EsDepto;
            foreach (var c in cuadro.Circuitos)
            {
                if (!c.EsActivo && !c.EsReservaEquipada) continue;
                foreach (var (piso, mat) in CircuitoMaterialesPorPiso(c, config, isDepto))
                {
                    if (!porPiso.TryGetValue(piso, out var totals))
                    {
                        totals = porPiso[piso] = new(StringComparer.OrdinalIgnoreCase);
                        circuitosPorPiso[piso] = new();
                        etiquetaOriginal[piso] = piso;
                    }
                    circuitosPorPiso[piso].Add(c.Id);
                    AddMaterial(totals, mat);
                }
            }
        }

        var ordenZona = (zonas ?? Array.Empty<ZonaProyecto>())
            .Select((z, i) => (z.Nombre, Orden: i))
            .GroupBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Orden, StringComparer.OrdinalIgnoreCase);

        return porPiso.Keys
            .Where(p => porPiso[p].Count > 0)
            .OrderBy(p => p == SinPisoKey ? 2 : ordenZona.ContainsKey(p) ? 0 : 1)
            .ThenBy(p => ordenZona.TryGetValue(p, out int o) ? o : int.MaxValue)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => BuildGrupo(
                p == SinPisoKey ? "Sin piso" : etiquetaOriginal[p],
                $"{circuitosPorPiso[p].Count} circuitos",
                porPiso[p]))
            .ToList();
    }

    public sealed record ArtefactoZonaTotal(string Nombre, string Categoria, decimal Cantidad);
    public sealed record ZonaResumenSC(string ZonaId, string ZonaNombre, decimal Total, IReadOnlyList<ArtefactoZonaTotal> Artefactos);

    /// <summary>
    /// Desglosa los artefactos de todos los circuitos por zona del proyecto, para el control por
    /// piso. Suma la misma referencia dentro de cada zona y respeta el orden de las zonas del
    /// proyecto; los artefactos sin zona (o con una zona ya inexistente) caen en un grupo "Sin
    /// zona" al final. Cuantifica sin precio, igual que <see cref="Aggregate"/>.
    /// </summary>
    public static IReadOnlyList<ZonaResumenSC> AggregateArtefactosPorZona(
        IEnumerable<CuadroCargaSCRow> cuadros,
        IReadOnlyList<ZonaProyecto> zonas)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(zonas);

        // Orden estable: primero las zonas del proyecto (por Orden), luego "Sin zona".
        const string SinZonaId = "";
        var orden = new List<(string Id, string Nombre)>();
        var nombrePorId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var z in zonas.Where(z => z.Activa).OrderBy(z => z.Orden))
        {
            if (nombrePorId.TryAdd(z.Id, z.Nombre))
                orden.Add((z.Id, z.Nombre));
        }

        // acumulado[zonaId][nombreArtefacto] = (categoria, cantidad)
        var acumulado = new Dictionary<string, Dictionary<string, (string Categoria, decimal Cantidad)>>(StringComparer.Ordinal);

        foreach (var cuadro in cuadros)
            foreach (var c in cuadro.Circuitos)
            {
                if (!c.EsActivo && !c.EsReservaEquipada) continue;
                foreach (var a in c.Artefactos)
                {
                    if (a.Cantidad <= 0m || string.IsNullOrWhiteSpace(a.Nombre)) continue;
                    var zonaId = nombrePorId.ContainsKey(a.ZonaId ?? "") ? a.ZonaId! : SinZonaId;
                    var porArtefacto = acumulado.TryGetValue(zonaId, out var d)
                        ? d
                        : acumulado[zonaId] = new(StringComparer.OrdinalIgnoreCase);
                    var nombre = a.Nombre.Trim();
                    var prev = porArtefacto.TryGetValue(nombre, out var cur) ? cur.Cantidad : 0m;
                    porArtefacto[nombre] = (a.Categoria, prev + a.Cantidad);
                }
            }

        if (acumulado.ContainsKey(SinZonaId) && !nombrePorId.ContainsKey(SinZonaId))
            orden.Add((SinZonaId, "Sin zona"));

        var resultado = new List<ZonaResumenSC>();
        foreach (var (id, nombre) in orden)
        {
            if (!acumulado.TryGetValue(id, out var porArtefacto) || porArtefacto.Count == 0) continue;
            var items = porArtefacto
                .Select(kv => new ArtefactoZonaTotal(kv.Key, kv.Value.Categoria, kv.Value.Cantidad))
                .OrderBy(i => i.Categoria)
                .ThenBy(i => i.Nombre)
                .ToList();
            resultado.Add(new ZonaResumenSC(id, nombre, items.Sum(i => i.Cantidad), items));
        }
        return resultado;
    }

    /// <summary>
    /// Indica si hay circuitos activos con cajas cuya sección de tubería no tiene
    /// mapeo de tubería flexible en la configuración.
    /// </summary>
    public static bool HasFlexiblesWithoutConfig(
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);

        return cuadros
            .Where(t => !t.EsDepto)
            .SelectMany(t => t.Circuitos)
            .Any(c => c.EsActivo && c.TotalCajasEfectivas > 0 &&
                      config.Flexibles.All(f =>
                          !string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Aplica la configuración del sistema al circuito: número de conductores y merma.
    /// Operación idempotente: si el sistema no existe en la config, solo asigna el nombre.
    /// </summary>
    public static void ApplySistema(CircuitoSCRow circuito, string nombreSistema, CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(circuito);
        ArgumentNullException.ThrowIfNull(config);
        circuito.Sistema = nombreSistema ?? "";
        var sistema = config.Sistemas.FirstOrDefault(s =>
            string.Equals(s.Nombre, nombreSistema, StringComparison.OrdinalIgnoreCase));
        if (sistema == null) return;
        circuito.NumConductores = sistema.NumConductores;
        circuito.MermaPct = sistema.MermaPct;
    }
}
