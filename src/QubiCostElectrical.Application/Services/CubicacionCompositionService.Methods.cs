using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class CubicacionCompositionService
{
    public void EnsureProject(ProjectFileData data)
    {
        if (data == null) return;
        EnsureLists(data);
        RemoveDuplicateLegacyRows(data);

        if (!data.CubicacionRecursos.Any() && data.Cubicacion.Any(HasLegacyContent))
            DecomposeAllFromLegacy(data);
        else
            DecomposeOrphanLegacyRows(data);

        SyncLegacyFromNormalized(data);
        EnsurePartidaApuLinks(data);
    }

    public void SyncNormalizedFromLegacyRow(ProjectFileData data, CubicacionRow row)
    {
        if (data == null || row == null) return;
        EnsureLists(data);

        var recurso = data.CubicacionRecursos.FirstOrDefault(r =>
            string.Equals(r.Id, row.Id, StringComparison.OrdinalIgnoreCase));

        if (recurso == null)
        {
            recurso = CreateRecursoFromLegacy(row, data.CubicacionRecursos.Count);
            data.CubicacionRecursos.Add(recurso);
            data.CubicacionValorizaciones.Add(CreateValorizacionFromLegacy(row, recurso.Id, data.Proyecto.ValorUfClp));
            data.CubicacionZonaCantidades.AddRange(CreateZonasFromLegacy(row, data.Zonas, recurso.Id));
            return;
        }

        UpdateRecursoFromLegacy(recurso, row);
        UpdateValorizacionFromLegacy(data, row, recurso.Id, data.Proyecto.ValorUfClp);
        ReplaceZonasFromLegacy(data, row, data.Zonas, recurso.Id);
    }

    public void SyncLegacyFromNormalized(ProjectFileData data)
    {
        if (data == null) return;
        EnsureLists(data);

        try
        {
            var legacyRows = data.Cubicacion.ToArray();
            var valorizaciones = data.CubicacionValorizaciones.ToArray();
            var zonaCantidades = data.CubicacionZonaCantidades.ToArray();
            var recursos = data.CubicacionRecursos.ToArray();

            var legacyById = new Dictionary<string, CubicacionRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in legacyRows)
            {
                if (!legacyById.ContainsKey(row.Id))
                    legacyById[row.Id] = row;
            }

            var valorizacionByRecursoId = new Dictionary<string, CubicacionValorizacionRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in valorizaciones)
            {
                if (!string.IsNullOrWhiteSpace(v.RecursoId) && !valorizacionByRecursoId.ContainsKey(v.RecursoId))
                    valorizacionByRecursoId[v.RecursoId] = v;
            }

            var zonasByRecursoId = new Dictionary<string, List<CubicacionZonaCantidadRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var z in zonaCantidades)
            {
                if (string.IsNullOrWhiteSpace(z.RecursoId)) continue;
                if (!zonasByRecursoId.TryGetValue(z.RecursoId, out var list))
                    zonasByRecursoId[z.RecursoId] = list = new List<CubicacionZonaCantidadRow>();
                list.Add(z);
            }

            foreach (var recurso in recursos.OrderBy(r => r.Orden).ThenBy(r => r.Codigo, StringComparer.OrdinalIgnoreCase))
            {
                if (!legacyById.TryGetValue(recurso.Id, out var row))
                {
                    row = new CubicacionRow { Id = recurso.Id };
                    data.Cubicacion.Add(row);
                    legacyById[recurso.Id] = row;
                }

                valorizacionByRecursoId.TryGetValue(recurso.Id, out var valorizacion);
                zonasByRecursoId.TryGetValue(recurso.Id, out var zonasForRecurso);
                ComposeLegacyRow(recurso, row, valorizacion, zonasForRecurso, data.Proyecto.ValorUfClp);
                if (row.MatClp == 0m && row.MoClp == 0m && !string.IsNullOrWhiteSpace(recurso.Codigo))
                {
                    var material = data.Materiales.FirstOrDefault(m => string.Equals(m.Codigo, recurso.Codigo, StringComparison.OrdinalIgnoreCase));
                    if (material != null && (material.MatClp > 0m || material.MoClp > 0m))
                    {
                        row.MatClp = material.MatClp;
                        row.MoClp = MaterialMoResolver.MoClpEfectiva(material, data.Proyecto);
                        row.RecalcularUf(data.Proyecto.ValorUfClp);
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Tolerancia a mutación concurrente en UI durante snapshot en segundo plano
        }
    }

    public void ApplyMaterial(ProjectFileData data, CubicacionRow row, MaterialRow material)
    {
        if (data == null || row == null || material == null) return;

        decimal matClp = Math.Round(material.MatClp, 0, MidpointRounding.AwayFromZero);
        decimal moClp = Math.Round(MaterialMoResolver.MoClpEfectiva(material, data.Proyecto), 0, MidpointRounding.AwayFromZero);
        PrecioCongeladoRow? snapshot = null;
        string fuente = "Lista de precios";
        DateTime fecha = DateTime.Today;

        if (data.Proyecto.PreciosCongelados)
        {
            snapshot = material.PrecioCongelado ?? new ProjectPriceFreezeService()
                .SnapshotMaterial(material, data.Proyecto, data.Proyecto.FechaCongelamientoPrecios);
            matClp = Math.Round(snapshot.ResolveMatClp(), 0, MidpointRounding.AwayFromZero);
            moClp = Math.Round(snapshot.ResolveMoClp(), 0, MidpointRounding.AwayFromZero);
            fuente = "Precio congelado";
            fecha = snapshot.FechaPrecio;
        }

        row.Codigo = CodeHelper.Normalize(material.Codigo);
        row.Descripcion = material.Descripcion;
        row.Unidad = material.Unidad;
        row.MatClp = matClp;
        row.MoClp = moClp;
        row.RecalcularUf(data.Proyecto.ValorUfClp);

        row.Tipo = string.Equals(material.Categoria, "Equipo", StringComparison.OrdinalIgnoreCase) ? "EQ" : "MAT";
        if (!string.IsNullOrWhiteSpace(material.Categoria))
            row.Area = material.Categoria;

        SyncNormalizedFromLegacyRow(data, row);

        var valorizacion = data.CubicacionValorizaciones.FirstOrDefault(v =>
            string.Equals(v.RecursoId, row.Id, StringComparison.OrdinalIgnoreCase));
        if (valorizacion != null)
        {
            WriteValorizacionPrices(valorizacion, matClp, moClp, data.Proyecto.ValorUfClp);
            valorizacion.FuentePrecio = fuente;
            valorizacion.FechaPrecio = fecha;
            valorizacion.PrecioSnapshot = snapshot;
        }
    }

    /// <summary>
    /// Aplica precios de valorización a la fila legacy. Prefiere MatClp/MoClp; si faltan,
    /// interpreta MatUf/MoUf como CLP contaminado (umbrales) o conserva UF legacy sin
    /// inventar pesos cuando ValorUfClp=0.
    /// </summary>
    private static void ApplyValorizacionPricesToRow(
        CubicacionRow row,
        CubicacionValorizacionRow valorizacion,
        decimal valorUfClp)
    {
        bool hasClp = valorizacion.MatClp > 0m || valorizacion.MoClp > 0m;
        if (hasClp)
        {
            row.MatClp = valorizacion.MatClp;
            row.MoClp = valorizacion.MoClp;
            row.RecalcularUf(valorUfClp);
            return;
        }

        // Legacy: MatUf/MoUf podían guardar UF reales o pesos mal puestos en la columna UF.
        // Nunca convertir MoUf=1 → 1×ValorUF (eso mostraba MO=40.500 con UF=40.500).
        decimal matClp = PriceConversion.LegacyUfFieldToClp(valorizacion.MatUf, valorUfClp, isMo: false);
        decimal moClp = PriceConversion.LegacyUfFieldToClp(valorizacion.MoUf, valorUfClp, isMo: true);

        if (matClp > 0m || moClp > 0m)
        {
            row.MatClp = matClp;
            row.MoClp = moClp;
            row.RecalcularUf(valorUfClp);
            return;
        }

        // Solo UF legacy sin valor UF del proyecto: no inventar CLP; conservar MatUf/MoUf.
        row.MatClp = 0m;
        row.MoClp = 0m;
        row.MatUf = valorizacion.MatUf;
        row.MoUf = valorizacion.MoUf;
    }

    public void ApplyMedicionesToPartida(ProjectFileData data, string itemId, IEnumerable<MeasurementLineRow> lines)
    {
        if (data == null || string.IsNullOrWhiteSpace(itemId)) return;
        EnsureLists(data);

        var mediciones = (lines ?? Enumerable.Empty<MeasurementLineRow>())
            .Where(m => string.Equals(m.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var recursos = data.CubicacionRecursos
            .Where(r => string.Equals(r.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (recursos.Count == 0)
        {
            foreach (var legacy in data.Cubicacion.Where(c =>
                         string.Equals(c.ItemId, itemId, StringComparison.OrdinalIgnoreCase) && HasLegacyContent(c)))
                SyncNormalizedFromLegacyRow(data, legacy);

            recursos = data.CubicacionRecursos
                .Where(r => string.Equals(r.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var unscoped = mediciones
            .Where(m => string.IsNullOrWhiteSpace(m.RecursoId))
            .ToList();

        foreach (var recurso in recursos)
        {
            var scoped = mediciones
                .Where(m => string.Equals(m.RecursoId, recurso.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (unscoped.Count > 0 && recursos.Count == 1)
                scoped.AddRange(unscoped);

            var scopedTotals = scoped
                .Where(m => m.ZonaOffset >= 0)
                .GroupBy(m => m.ZonaOffset)
                .ToDictionary(g => g.Key, g => g.Sum(l => MeasurementLineService.ComputeParcial(l)));

            if (scopedTotals.Count == 0)
                continue;

            ReplaceZonasFromTotals(data, recurso, scopedTotals);
        }

        SyncLegacyFromNormalized(data);
    }

    public void ClearComposition(ProjectFileData data)
    {
        if (data == null) return;
        data.CubicacionRecursos?.Clear();
        data.CubicacionZonaCantidades?.Clear();
        data.CubicacionValorizaciones?.Clear();
        data.PartidaApuVinculos?.Clear();
    }

    public void EnsurePartidaApuLinks(ProjectFileData data)
    {
        if (data == null) return;
        EnsureLists(data);

        foreach (var apu in data.Apus.Where(a => !string.IsNullOrWhiteSpace(a.ItemId)))
        {
            var link = data.PartidaApuVinculos.FirstOrDefault(v =>
                string.Equals(v.ItemId, apu.ItemId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.Modo, "Principal", StringComparison.OrdinalIgnoreCase));

            if (link == null)
            {
                data.PartidaApuVinculos.Add(new PartidaApuVinculoRow
                {
                    ItemId = apu.ItemId,
                    ApuId = apu.Id,
                    Modo = "Principal",
                    ActualizadoEn = AppClock.Now
                });
                continue;
            }

            if (!string.Equals(link.ApuId, apu.Id, StringComparison.OrdinalIgnoreCase))
            {
                var existingApu = data.Apus.FirstOrDefault(a =>
                    string.Equals(a.Id, link.ApuId, StringComparison.OrdinalIgnoreCase));
                if (existingApu == null)
                {
                    link.ApuId = apu.Id;
                    link.ActualizadoEn = AppClock.Now;
                }
            }
        }
    }

    // Repara datos corruptos con filas legacy duplicadas (mismo Id) que se vieron en producción
    // y hacían fallar SyncLegacyFromNormalized más adelante. Se conserva la primera aparición de
    // cada Id (la que ya se estaba mostrando) y se descartan las repetidas — es autoreparación,
    // no debería perder ningún recurso real ya que un Id duplicado nunca fue un estado válido.
    private static void RemoveDuplicateLegacyRows(ProjectFileData data)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        data.Cubicacion.RemoveAll(row => !seen.Add(row.Id ?? ""));
    }

    private static void EnsureLists(ProjectFileData data)
    {
        data.CubicacionRecursos ??= new List<CubicacionRecursoRow>();
        data.CubicacionZonaCantidades ??= new List<CubicacionZonaCantidadRow>();
        data.CubicacionValorizaciones ??= new List<CubicacionValorizacionRow>();
        data.PartidaApuVinculos ??= new List<PartidaApuVinculoRow>();
        data.Cubicacion ??= new List<CubicacionRow>();
        data.Zonas ??= new List<ZonaProyecto>();
    }

    private void DecomposeOrphanLegacyRows(ProjectFileData data)
    {
        // HashSet en vez de .Any(...) dentro del loop: con miles de filas ese escaneo lineal
        // por cada fila legacy era O(N²) — mismo tipo de cuello de botella que ComposeLegacyRow.
        var recursoIds = new HashSet<string>(
            data.CubicacionRecursos.Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in data.Cubicacion.Where(HasLegacyContent).ToList())
        {
            if (!recursoIds.Contains(row.Id))
            {
                SyncNormalizedFromLegacyRow(data, row);
                recursoIds.Add(row.Id);
            }
        }
    }

    private void DecomposeAllFromLegacy(ProjectFileData data)
    {
        data.CubicacionRecursos.Clear();
        data.CubicacionZonaCantidades.Clear();
        data.CubicacionValorizaciones.Clear();

        int orden = 0;
        foreach (var row in data.Cubicacion.Where(HasLegacyContent))
        {
            var recurso = CreateRecursoFromLegacy(row, orden++);
            data.CubicacionRecursos.Add(recurso);
            data.CubicacionValorizaciones.Add(CreateValorizacionFromLegacy(row, recurso.Id, data.Proyecto.ValorUfClp));
            data.CubicacionZonaCantidades.AddRange(CreateZonasFromLegacy(row, data.Zonas, recurso.Id));
        }

        foreach (var med in data.Mediciones)
        {
            if (string.IsNullOrWhiteSpace(med.RecursoId) && !string.IsNullOrWhiteSpace(med.CubicacionRowId))
                med.RecursoId = med.CubicacionRowId;
        }
    }

    private static CubicacionRecursoRow CreateRecursoFromLegacy(CubicacionRow row, int orden)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id,
            ItemId = row.ItemId ?? "",
            Tipo = string.IsNullOrWhiteSpace(row.Tipo) ? TipoRecurso.MAT : row.Tipo,
            Codigo = row.Codigo ?? "",
            Descripcion = row.Descripcion ?? "",
            Unidad = row.Unidad ?? "",
            Area = string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area,
            Orden = orden
        };

    private static void UpdateRecursoFromLegacy(CubicacionRecursoRow recurso, CubicacionRow row)
    {
        recurso.ItemId = row.ItemId ?? "";
        recurso.Tipo = string.IsNullOrWhiteSpace(row.Tipo) ? TipoRecurso.MAT : row.Tipo;
        recurso.Codigo = row.Codigo ?? "";
        recurso.Descripcion = row.Descripcion ?? "";
        recurso.Unidad = row.Unidad ?? "";
        recurso.Area = string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area;
    }

    private static CubicacionValorizacionRow CreateValorizacionFromLegacy(CubicacionRow row, string recursoId, decimal valorUfClp)
    {
        ResolveClpFromLegacyRow(row, out decimal matClp, out decimal moClp);
        var valorizacion = new CubicacionValorizacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            RecursoId = recursoId,
            ItemId = row.ItemId ?? "",
            MermaPctOverride = row.MermaPctOverride,
            HerramientasPctOverride = row.HerramientasPctOverride,
            RedondeoMultiplo = row.RedondeoMultiplo,
            RedondeoModo = string.IsNullOrWhiteSpace(row.RedondeoModo) ? ModoRedondeo.Ninguno : row.RedondeoModo,
            TotalExpression = row.TotalExpression ?? ""
        };
        WriteValorizacionPrices(valorizacion, matClp, moClp, valorUfClp, row.MatUf, row.MoUf);
        return valorizacion;
    }

    private static void UpdateValorizacionFromLegacy(ProjectFileData data, CubicacionRow row, string recursoId, decimal valorUfClp)
    {
        var valorizacion = data.CubicacionValorizaciones.FirstOrDefault(v =>
            string.Equals(v.RecursoId, recursoId, StringComparison.OrdinalIgnoreCase));

        if (valorizacion == null)
        {
            data.CubicacionValorizaciones.Add(CreateValorizacionFromLegacy(row, recursoId, valorUfClp));
            return;
        }

        ResolveClpFromLegacyRow(row, out decimal matClp, out decimal moClp);
        valorizacion.ItemId = row.ItemId ?? "";
        WriteValorizacionPrices(valorizacion, matClp, moClp, valorUfClp, row.MatUf, row.MoUf);
        valorizacion.MermaPctOverride = row.MermaPctOverride;
        valorizacion.HerramientasPctOverride = row.HerramientasPctOverride;
        valorizacion.RedondeoMultiplo = row.RedondeoMultiplo;
        valorizacion.RedondeoModo = string.IsNullOrWhiteSpace(row.RedondeoModo) ? ModoRedondeo.Ninguno : row.RedondeoModo;
        valorizacion.TotalExpression = row.TotalExpression ?? "";
    }

    /// <summary>
    /// Contrato fase 5: MatClp/MoClp = pesos; MatUf/MoUf = UF derivados.
    /// Si no hay CLP y solo hay UF legacy en la fila, se conservan esos UF sin inventar pesos.
    /// </summary>
    private static void WriteValorizacionPrices(
        CubicacionValorizacionRow valorizacion,
        decimal matClp,
        decimal moClp,
        decimal valorUfClp,
        decimal legacyMatUf = 0m,
        decimal legacyMoUf = 0m)
    {
        valorizacion.MatClp = matClp;
        valorizacion.MoClp = moClp;
        if (matClp > 0m || moClp > 0m)
        {
            valorizacion.MatUf = PriceConversion.ClpToUf(matClp, valorUfClp);
            valorizacion.MoUf = PriceConversion.ClpToUf(moClp, valorUfClp);
            return;
        }

        valorizacion.MatUf = legacyMatUf;
        valorizacion.MoUf = legacyMoUf;
    }

    /// <summary>
    /// Obtiene MAT/MO en CLP desde la fila legacy. Prefiere MatClp/MoClp; si están en 0 y
    /// MatUf/MoUf parecen pesos contaminados, los usa; si son UF reales pequeños, deja CLP=0
    /// (EnsureCubicacionClp / catálogo completan al cargar con ValorUfClp).
    /// </summary>
    private static void ResolveClpFromLegacyRow(CubicacionRow row, out decimal matClp, out decimal moClp)
    {
        matClp = row.MatClpConFallback;
        moClp = row.MoClpConFallback;
    }

    private static IEnumerable<CubicacionZonaCantidadRow> CreateZonasFromLegacy(
        CubicacionRow row,
        IReadOnlyList<ZonaProyecto> zonas,
        string recursoId)
    {
        var expressions = row.QtyExpressions ?? Array.Empty<string>();
        var result = new List<CubicacionZonaCantidadRow>();

        for (int i = 0; i < expressions.Length && i < MaxCubicacionSectors; i++)
        {
            string expr = expressions[i] ?? "";
            if (string.IsNullOrWhiteSpace(expr)) continue;

            decimal qty = ResolveExpressionQty(expr);
            result.Add(new CubicacionZonaCantidadRow
            {
                RecursoId = recursoId,
                ItemId = row.ItemId ?? "",
                ZonaId = ResolveZonaId(zonas, i),
                ZonaOffset = i,
                Cantidad = qty,
                Expresion = expr
            });
        }

        if (result.Count == 0 && row.Cantidad != 0m)
        {
            result.Add(new CubicacionZonaCantidadRow
            {
                RecursoId = recursoId,
                ItemId = row.ItemId ?? "",
                ZonaOffset = 0,
                ZonaId = ResolveZonaId(zonas, 0),
                Cantidad = row.Cantidad,
                Expresion = row.Cantidad.ToString("G", CultureInfo.InvariantCulture)
            });
        }

        return result;
    }

    private static void ReplaceZonasFromLegacy(
        ProjectFileData data,
        CubicacionRow row,
        IReadOnlyList<ZonaProyecto> zonas,
        string recursoId)
    {
        data.CubicacionZonaCantidades.RemoveAll(z =>
            string.Equals(z.RecursoId, recursoId, StringComparison.OrdinalIgnoreCase));
        data.CubicacionZonaCantidades.AddRange(CreateZonasFromLegacy(row, zonas, recursoId));
    }

    private static void ReplaceZonasFromTotals(
        ProjectFileData data,
        CubicacionRecursoRow recurso,
        IReadOnlyDictionary<int, decimal> totalsByZona)
    {
        data.CubicacionZonaCantidades.RemoveAll(z =>
            string.Equals(z.RecursoId, recurso.Id, StringComparison.OrdinalIgnoreCase));

        foreach (var (offset, total) in totalsByZona.OrderBy(x => x.Key))
        {
            if (total == 0m) continue;
            data.CubicacionZonaCantidades.Add(new CubicacionZonaCantidadRow
            {
                RecursoId = recurso.Id,
                ItemId = recurso.ItemId,
                ZonaOffset = offset,
                ZonaId = ResolveZonaId(data.Zonas, offset),
                Cantidad = total,
                Expresion = total.ToString("G", CultureInfo.InvariantCulture)
            });
        }
    }

    private static void ComposeLegacyRow(
        CubicacionRecursoRow recurso,
        CubicacionRow row,
        CubicacionValorizacionRow? valorizacion,
        List<CubicacionZonaCantidadRow>? zonasUnordered,
        decimal valorUfClp)
    {
        row.Id = recurso.Id;
        row.ItemId = recurso.ItemId;
        row.Tipo = recurso.Tipo;
        row.Codigo = recurso.Codigo;
        row.Descripcion = recurso.Descripcion;
        row.Unidad = recurso.Unidad;
        row.Area = recurso.Area;

        if (valorizacion != null)
        {
            ApplyValorizacionPricesToRow(row, valorizacion, valorUfClp);
            row.MermaPctOverride = valorizacion.MermaPctOverride;
            row.HerramientasPctOverride = valorizacion.HerramientasPctOverride;
            row.RedondeoMultiplo = valorizacion.RedondeoMultiplo;
            row.RedondeoModo = valorizacion.RedondeoModo;
            row.TotalExpression = valorizacion.TotalExpression ?? "";
        }

        var zonas = (zonasUnordered ?? new List<CubicacionZonaCantidadRow>())
            .OrderBy(z => z.ZonaOffset)
            .ToList();

        int maxOffset = zonas.Count == 0 ? -1 : zonas.Max(z => z.ZonaOffset);
        var expressions = new string[Math.Max(0, maxOffset + 1)];
        decimal total = 0m;

        foreach (var zona in zonas)
        {
            if (zona.ZonaOffset < 0 || zona.ZonaOffset >= MaxCubicacionSectors) continue;
            if (expressions.Length <= zona.ZonaOffset)
                Array.Resize(ref expressions, zona.ZonaOffset + 1);
            expressions[zona.ZonaOffset] = string.IsNullOrWhiteSpace(zona.Expresion)
                ? (zona.Cantidad == 0m ? "" : zona.Cantidad.ToString("G", CultureInfo.InvariantCulture))
                : zona.Expresion;
            total += zona.Cantidad;
        }

        row.QtyExpressions = expressions;
        row.Cantidad = total;
    }

    private static string ResolveZonaId(IReadOnlyList<ZonaProyecto> zonas, int offset)
    {
        var activas = (zonas ?? Array.Empty<ZonaProyecto>())
            .Where(z => z.Activa)
            .OrderBy(z => z.Orden)
            .ToList();
        if (offset >= 0 && offset < activas.Count)
            return activas[offset].Id;
        return "";
    }

    private static decimal ParseQty(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static decimal ResolveExpressionQty(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
            return 0m;

        if (QuantityFormulaService.IsFormulaLike(expr))
        {
            var result = QuantityFormulaService.Evaluate(expr);
            return result.Success ? result.Value : 0m;
        }

        return ParseQty(expr);
    }

    private static bool HasLegacyContent(CubicacionRow row)
    {
        if (row == null) return false;
        return !string.IsNullOrWhiteSpace(row.Codigo) ||
               !string.IsNullOrWhiteSpace(row.Descripcion) ||
               row.Cantidad != 0m ||
               row.MatUf != 0m ||
               row.MoUf != 0m ||
               (row.QtyExpressions != null && row.QtyExpressions.Any(v => !string.IsNullOrWhiteSpace(v)));
    }
}
