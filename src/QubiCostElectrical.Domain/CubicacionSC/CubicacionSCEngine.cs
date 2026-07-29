using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Motor de cubicación C.SC: agrega los materiales de todos los circuitos activos y reservas
/// equipadas, aplicando la configuración de tubería flexible por diámetro.
/// Equivale a la hoja RESUMEN del Excel de origen.
/// </summary>
public static partial class CubicacionSCEngine
{
    public readonly record struct MaterialSC(string Nombre, decimal Cantidad, string Unidad);

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
    private static IEnumerable<MaterialSC> CircuitoMateriales(CircuitoSCRow c, CubicacionScConfig config)
    {
        if (c.EsActivo)
        {
            yield return new($"TUBERIA {c.TipoTuberia} {c.SeccionTuberia}", c.LargoTuberia, "ML");
            yield return new($"CONDUCTOR {c.ConductorDerivacion} {c.SeccionDerivacion}", c.LargoDerivacion, "ML");
            yield return new($"CONDUCTOR {c.ConductorDistribucion} {c.SeccionDistribucion}", c.LargoDistribucion, "ML");

            var flex = config.Flexibles.FirstOrDefault(f =>
                string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase));
            if (flex != null && c.TotalCajas > 0 && flex.MetrosPorCaja > 0m)
                yield return new(flex.TipoFlexible, c.TotalCajas * flex.MetrosPorCaja, "ML");

            foreach (var a in c.Artefactos)
                yield return new(a.Nombre, a.Cantidad, "UN");

            yield return new("SENSOR DE MOVIMIENTO", c.SensoresMovimiento, "UN");
            yield return new("SEÑALETICA EMERGENCIA", c.Senaleticas, "UN");
            yield return new("CINTA LED", c.CintaLedMts, "ML");
            yield return new("MONTAJE LUMINARIA", c.MontajesAlumbrado, "UN");
            yield return new("CAJA 5/8", c.Cajas58, "UN");
            yield return new("CAJA 100X100", c.Cajas100x100, "UN");
            yield return new("CAJA 100X65", c.Cajas100x65, "UN");
        }
        else if (c.EsReservaEquipada)
        {
            if (c.Artefactos.Count == 0)
                yield return new("INT 9/12", 1m, "UN");
            else
                foreach (var a in c.Artefactos)
                    yield return new(a.Nombre, a.Cantidad, "UN");
        }
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
        foreach (var c in cuadro.Circuitos)
        foreach (var m in CircuitoMateriales(c, config))
            AddMaterial(totals, m);
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
            var totals = new Dictionary<string, MaterialSC>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in cuadro.Circuitos)
            foreach (var m in CircuitoMateriales(c, config))
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
    private static IEnumerable<(string PisoLabel, MaterialSC Mat)> CircuitoMaterialesPorPiso(CircuitoSCRow c, CubicacionScConfig config)
    {
        if (c.EsActivo && c.TienePisos)
        {
            string primerTramo = TramoLabel(c.PisosTramos[0]);
            decimal factor = c.NumConductores * c.FactorMerma;
            foreach (var tramo in c.PisosTramos)
            {
                var label = TramoLabel(tramo);
                yield return (label, new($"TUBERIA {c.TipoTuberia} {c.SeccionTuberia}", tramo.MetrosTuberia, "ML"));
                yield return (label, new($"CONDUCTOR {c.ConductorDerivacion} {c.SeccionDerivacion}",
                    Math.Round(tramo.MetrosTuberia * factor, 0, MidpointRounding.AwayFromZero), "ML"));
                yield return (label, new($"CONDUCTOR {c.ConductorDistribucion} {c.SeccionDistribucion}",
                    Math.Round(tramo.MetrosBandeja * factor, 0, MidpointRounding.AwayFromZero), "ML"));
            }
            // Materiales no dependientes del largo → al primer tramo del circuito.
            foreach (var m in CircuitoMateriales(c, config).Where(m => !EsTuberia(m.Nombre) && !EsConductor(m.Nombre)))
                yield return (primerTramo, m);
        }
        else
        {
            var piso = string.IsNullOrWhiteSpace(c.Ubicacion) ? SinPisoKey : c.Ubicacion.Trim();
            foreach (var m in CircuitoMateriales(c, config))
                yield return (piso, m);
        }
    }

    private static string TramoLabel(CircuitoPisoTramo t) =>
        string.IsNullOrWhiteSpace(t.ZonaNombre) ? "Sin zona" : t.ZonaNombre.Trim();

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
        foreach (var c in cuadro.Circuitos)
        {
            if (!c.EsActivo && !c.EsReservaEquipada) continue;
            foreach (var (piso, mat) in CircuitoMaterialesPorPiso(c, config))
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
            .SelectMany(t => t.Circuitos)
            .Any(c => c.EsActivo && c.TotalCajas > 0 &&
                      config.Flexibles.All(f =>
                          !string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase)));
    }

    public static void ApplySistema(CircuitoSCRow circuito, string nombreSistema, CubicacionScConfig config)
    {
        ArgumentNullException.ThrowIfNull(circuito);
        ArgumentNullException.ThrowIfNull(config);
        circuito.Sistema = nombreSistema ?? "";
        var sistema = config.Sistemas.FirstOrDefault(s =>
            string.Equals(s.Nombre, nombreSistema, StringComparison.OrdinalIgnoreCase));
        if (sistema == null) return;
        circuito.NumConductores = sistema.NumConductores;
        circuito.MermaPct       = sistema.MermaPct;
    }
}
