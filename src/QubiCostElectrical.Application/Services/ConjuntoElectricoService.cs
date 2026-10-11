using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ConjuntoApplyMode
{
    CubicacionOnly,
    CubicacionYApu
}

public sealed class ConjuntoExpansionResult
{
    public List<CubicacionRow> Rows { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ConjuntoBulkApplyResult
{
    public int PartidasProcesadas { get; set; }
    public int FilasCubicacion { get; set; }
    public int ApusActualizados { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class ConjuntoElectricoService
{
    public ConjuntoExpansionResult ExpandToCubicacion(
        ConjuntoElectricoRow conjunto,
        ItemizadoRow partida,
        IEnumerable<MaterialRow>? materiales,
        bool cantidadesEnCero = false)
    {
        var result = new ConjuntoExpansionResult();
        if (conjunto == null || partida == null) return result;

        decimal baseQty = partida.Cantidad < 0m ? 0m : partida.Cantidad;
        // Si la partida aún no tiene cantidad, expandir como receta unitaria (base = 1)
        // para que la plantilla sí cargue materiales visibles en cubicación.
        decimal expandBase = baseQty > 0m ? baseQty : 1m;
        var mats = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();

        // Paso 1: cantidades de los maestros (filas sin MaestroId). Los esclavos usan la
        // cantidad de su maestro como base en lugar de la cantidad de la partida. Las filas
        // con Operación se resuelven por su fórmula (combinación de otras filas por código).
        var maestros = ResolveMasterQuantities(conjunto, expandBase, result.Warnings);

        var creadas = new List<(ConjuntoRecursoRow recurso, CubicacionRow row)>();
        var rowPorRecursoId = new Dictionary<string, CubicacionRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var recurso in conjunto.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            if (recurso.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH or ApuTipoRecurso.HERR)
                continue;

            decimal rowBase = expandBase;
            if (!string.IsNullOrWhiteSpace(recurso.MaestroId))
            {
                if (maestros.TryGetValue(recurso.MaestroId, out var maestroQty))
                    rowBase = maestroQty;
                else
                    result.Warnings.Add(
                        $"El material '{RowLabel(recurso)}' es esclavo pero su maestro no existe o no es válido: se calculó sobre la cantidad de la partida.");
            }

            decimal qty;
            if (cantidadesEnCero)
            {
                // Carga en cero: la plantilla solo aporta la lista de materiales y sus fórmulas
                // maestro→esclavo; las cantidades las ingresa el usuario en la hoja de cubicación.
                qty = 0m;
            }
            else if (!string.IsNullOrWhiteSpace(recurso.Operacion))
            {
                // Fila Operación: la cantidad la dicta su fórmula (ya evaluada por código en
                // ResolveMasterQuantities). Sin fallback: 0 es un resultado válido de la fórmula.
                qty = maestros.TryGetValue(recurso.Id, out var qtyOperacion) ? qtyOperacion : 0m;
            }
            else
            {
                qty = ComputeQuantity(rowBase, recurso, conjunto.Parametros);
                if (qty <= 0m)
                {
                    qty = recurso.Rendimiento > 0m ? recurso.Rendimiento : 1m;
                    if (recurso.Factor > 0m)
                        qty *= recurso.Factor;
                }
            }

            var material = FindMaterial(mats, recurso.Codigo);
            if (material == null && string.IsNullOrWhiteSpace(recurso.Descripcion))
            {
                string reference = string.IsNullOrWhiteSpace(recurso.Codigo) ? "sin código ni descripción" : $"'{recurso.Codigo}'";
                result.Warnings.Add($"No se encontró el material {reference} en la plantilla '{conjunto.Nombre}'.");
                continue;
            }

            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = partida.Id ?? "",
                Tipo = MapCubicacionTipo(recurso.Tipo),
                Codigo = material?.Codigo ?? recurso.Codigo,
                Descripcion = material?.Descripcion ?? recurso.Descripcion,
                Unidad = material?.Unidad ?? recurso.Unidad,
                Cantidad = qty,
                MatUf = material?.MatUf ?? recurso.PrecioUf,
                MoUf = material?.MoUf ?? 0m,
                Area = conjunto.TipoConjunto,
                Operacion = recurso.Operacion ?? ""
            };
            result.Rows.Add(row);
            creadas.Add((recurso, row));
            if (!string.IsNullOrWhiteSpace(recurso.Id))
                rowPorRecursoId[recurso.Id] = row;
        }

        // Paso 2: vínculos maestro→esclavo por código de fila (se resuelve después del bucle
        // para no depender del orden de las filas dentro de la plantilla). Si la fila maestro
        // no llegó a generarse (material faltante), la fila queda como maestro/independiente.
        foreach (var (recurso, row) in creadas)
        {
            if (string.IsNullOrWhiteSpace(recurso.MaestroId)) continue;
            if (!rowPorRecursoId.TryGetValue(recurso.MaestroId, out var maestroRow)) continue;
            row.Esclavo = ToVinculo(recurso, maestroRow.Codigo ?? "", conjunto.Parametros);
        }

        result.Rows = MergeRows(result.Rows);
        return result;
    }

    public ApuRow BuildApu(
        ConjuntoElectricoRow conjunto,
        ItemizadoRow partida,
        IEnumerable<MaterialRow>? materiales)
    {
        if (conjunto == null) throw new ArgumentNullException(nameof(conjunto));
        if (partida == null) throw new ArgumentNullException(nameof(partida));

        decimal baseQty = partida.Cantidad < 0m ? 0m : partida.Cantidad;
        decimal perUnitBase = baseQty > 0m ? baseQty : 1m;
        var mats = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var maestros = ResolveMasterQuantities(conjunto, perUnitBase);
        var apu = new ApuRow
        {
            ItemId = partida.Id ?? "",
            Descripcion = string.IsNullOrWhiteSpace(conjunto.Nombre) ? partida.Descripcion : conjunto.Nombre,
            Unidad = string.IsNullOrWhiteSpace(conjunto.Unidad) ? partida.Unidad : conjunto.Unidad,
            CantidadPartida = baseQty,
            HerramientasPct = conjunto.HerramientasPct,
            MermaPct = conjunto.MermaPct,
            TransportePct = conjunto.TransportePct,
            ImprevistosAlzaPct = conjunto.ImprevistosAlzaPct,
            FuentePrecio = $"Conjunto:{conjunto.Codigo}",
            FechaPrecio = DateTime.Today,
            Estado = "OK"
        };

        foreach (var recurso in conjunto.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            decimal rend;
            if (!string.IsNullOrWhiteSpace(recurso.Operacion))
            {
                // Fila Operación: el rendimiento por unidad es la cantidad resuelta por su
                // fórmula (ya evaluada por código en ResolveMasterQuantities) / cantidad partida.
                rend = maestros.TryGetValue(recurso.Id, out var qtyOperacion)
                    ? qtyOperacion / perUnitBase
                    : 0m;
            }
            else if (recurso.TipoCalculo == TipoCalculoParametrico.Espaciamiento
                || !string.IsNullOrWhiteSpace(recurso.MaestroId))
            {
                // Filas paramétricas y esclavos: el rendimiento por unidad es la cantidad
                // resuelta (base = partida, o la cantidad del maestro si es esclavo) / cantidad de partida.
                decimal rowBase = perUnitBase;
                if (!string.IsNullOrWhiteSpace(recurso.MaestroId)
                    && maestros.TryGetValue(recurso.MaestroId, out var maestroQty))
                    rowBase = maestroQty;
                rend = ComputeQuantity(rowBase, recurso, conjunto.Parametros) / perUnitBase;
            }
            else
            {
                rend = ComputeRendimiento(baseQty, recurso, conjunto.Parametros);
            }
            if (rend <= 0m) continue;

            var material = FindMaterial(mats, recurso.Codigo);
            apu.Recursos.Add(BuildApuRecurso(recurso, material, rend));
        }

        return apu;
    }

    public ConjuntoElectricoRow? SuggestConjunto(string? descripcion, IEnumerable<ConjuntoElectricoRow>? conjuntos)
    {
        string text = (descripcion ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var list = (conjuntos ?? Enumerable.Empty<ConjuntoElectricoRow>()).ToList();
        if (list.Count == 0) return null;

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ConjuntoElectricoRow? best = null;
        int bestScore = 0;

        foreach (var conjunto in list)
        {
            int score = 0;
            foreach (var keyword in SplitKeywords(conjunto.Keywords))
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    score += 3;
            }

            if (!string.IsNullOrWhiteSpace(conjunto.Nombre) &&
                text.Contains(conjunto.Nombre, StringComparison.OrdinalIgnoreCase))
                score += 4;

            if (!string.IsNullOrWhiteSpace(conjunto.TipoConjunto) &&
                text.Contains(conjunto.TipoConjunto, StringComparison.OrdinalIgnoreCase))
                score += 2;

            foreach (var token in tokens)
            {
                if (token.Length < 4) continue;
                if ((conjunto.Nombre ?? "").Contains(token, StringComparison.OrdinalIgnoreCase)) score++;
                if ((conjunto.Codigo ?? "").Contains(token, StringComparison.OrdinalIgnoreCase)) score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = conjunto;
            }
        }

        return bestScore >= 2 ? best : null;
    }

    public ConjuntoBulkApplyResult ApplyBulk(
        ConjuntoElectricoRow conjunto,
        IEnumerable<ItemizadoRow> partidas,
        List<CubicacionRow> cubicacion,
        List<ApuRow> apus,
        IEnumerable<MaterialRow>? materiales,
        ConjuntoApplyMode mode,
        bool replaceCubicacion)
    {
        var result = new ConjuntoBulkApplyResult();
        if (conjunto == null) return result;

        foreach (var partida in (partidas ?? Enumerable.Empty<ItemizadoRow>()).Where(p => !p.EsTitulo))
        {
            if (replaceCubicacion)
                cubicacion.RemoveAll(c => string.Equals(c.ItemId, partida.Id, StringComparison.OrdinalIgnoreCase));

            var expanded = ExpandToCubicacion(conjunto, partida, materiales);
            result.Warnings.AddRange(expanded.Warnings);
            cubicacion.AddRange(expanded.Rows);
            result.FilasCubicacion += expanded.Rows.Count;

            if (mode == ConjuntoApplyMode.CubicacionYApu)
            {
                var apu = BuildApu(conjunto, partida, materiales);
                var existing = apus.FirstOrDefault(a => string.Equals(a.ItemId, partida.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    apus.Add(apu);
                    result.ApusActualizados++;
                }
                else
                {
                    existing.Descripcion = apu.Descripcion;
                    existing.Unidad = apu.Unidad;
                    existing.CantidadPartida = apu.CantidadPartida;
                    existing.HerramientasPct = apu.HerramientasPct;
                    existing.MermaPct = apu.MermaPct;
                    existing.TransportePct = apu.TransportePct;
                    existing.ImprevistosAlzaPct = apu.ImprevistosAlzaPct;
                    existing.FuentePrecio = apu.FuentePrecio;
                    existing.Recursos = apu.Recursos;
                    result.ApusActualizados++;
                }
            }

            result.PartidasProcesadas++;
        }

        return result;
    }

    public void TouchVersion(ConjuntoElectricoRow conjunto)
    {
        if (conjunto == null) return;
        conjunto.Version = Math.Max(1, conjunto.Version + 1);
        conjunto.ActualizadoEn = AppClock.Now;
    }

    /// <summary>
    /// Clona un conjunto con Ids nuevos en los recursos. MaestroId se remapea al nuevo Id
    /// del maestro copiado (si el destino ya no existe, la copia queda liberada como maestro).
    /// </summary>
    public static ConjuntoElectricoRow CloneConjunto(ConjuntoElectricoRow source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var recursos = new List<ConjuntoRecursoRow>();
        foreach (var r in source.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            string nuevoId = Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(r.Id)) idMap[r.Id] = nuevoId;
            recursos.Add(new ConjuntoRecursoRow
            {
                Id = nuevoId,
                MaestroId = r.MaestroId ?? "",
                Operacion = r.Operacion ?? "",
                Tipo = r.Tipo,
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Rendimiento = r.Rendimiento,
                Factor = r.Factor,
                Minimo = r.Minimo,
                TipoCalculo = r.TipoCalculo,
                Espaciamiento = r.Espaciamiento,
                PrecioUf = r.PrecioUf,
                ValorHHUf = r.ValorHHUf,
                MermaPct = r.MermaPct
            });
        }

        // Paso 2: remapear MaestroId a los nuevos Ids; destino inexistente -> liberado (maestro).
        foreach (var r in recursos)
        {
            string viejo = r.MaestroId ?? "";
            r.MaestroId = !string.IsNullOrWhiteSpace(viejo) && idMap.TryGetValue(viejo, out var nuevo)
                ? nuevo
                : "";
        }

        return new ConjuntoElectricoRow
        {
            Codigo = source.Codigo,
            Nombre = source.Nombre,
            TipoConjunto = source.TipoConjunto,
            Descripcion = source.Descripcion,
            Unidad = source.Unidad,
            Keywords = source.Keywords,
            HerramientasPct = source.HerramientasPct,
            MermaPct = source.MermaPct,
            TransportePct = source.TransportePct,
            ImprevistosAlzaPct = source.ImprevistosAlzaPct,
            Parametros = new Dictionary<string, decimal>(source.Parametros ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase),
            Recursos = recursos,
            Version = source.Version,
            ActualizadoEn = source.ActualizadoEn
        };
    }

    /// <summary>
    /// Libera los esclavos que apuntan a una fila maestro eliminada (los convierte en maestros).
    /// Retorna la cantidad de filas liberadas.
    /// </summary>
    public static int LiberarEsclavos(ConjuntoElectricoRow conjunto, string maestroIdEliminado)
    {
        if (conjunto?.Recursos == null || string.IsNullOrWhiteSpace(maestroIdEliminado)) return 0;

        int liberados = 0;
        foreach (var r in conjunto.Recursos)
        {
            if (string.Equals((r.MaestroId ?? "").Trim(), maestroIdEliminado, StringComparison.OrdinalIgnoreCase))
            {
                r.MaestroId = "";
                liberados++;
            }
        }
        return liberados;
    }

    private static ApuRecursoRow BuildApuRecurso(ConjuntoRecursoRow recurso, MaterialRow? material, decimal rendimiento)
    {
        bool isLabor = recurso.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH;
        decimal precio = material?.MatUf ?? recurso.PrecioUf;
        decimal hh = recurso.ValorHHUf > 0m ? recurso.ValorHHUf : material?.MoUf ?? recurso.PrecioUf;

        return new ApuRecursoRow
        {
            Tipo = recurso.Tipo,
            CodigoMaterial = CodeHelper.Normalize(material?.Codigo ?? recurso.Codigo),
            Descripcion = material?.Descripcion ?? recurso.Descripcion,
            Unidad = isLabor ? (string.IsNullOrWhiteSpace(recurso.Unidad) ? "HH" : recurso.Unidad) : material?.Unidad ?? recurso.Unidad,
            Rendimiento = rendimiento,
            PrecioUf = isLabor ? 0m : precio,
            ValorHHUf = isLabor ? hh : 0m,
            RendHH = isLabor ? rendimiento : 0m,
            MermaPct = recurso.MermaPct,
            FuentePrecio = "Conjunto eléctrico",
            FechaPrecio = DateTime.Today,
            Estado = (isLabor ? hh : precio) <= 0m ? "Falta precio" : "OK"
        };
    }

    private static decimal ComputeQuantity(decimal baseQty, ConjuntoRecursoRow recurso, Dictionary<string, decimal>? parametros)
    {
        // Modo Espaciamiento: Cantidad = Ceil(base / pasóo). Ej: soporte cada 1.5 m -> Ceil(1000/1.5) = 667.
        if (recurso.TipoCalculo == TipoCalculoParametrico.Espaciamiento && recurso.Espaciamiento > 0m)
            return ApplyMin((decimal)Math.Ceiling((double)(baseQty / recurso.Espaciamiento)), recurso.Minimo);

        // Modo Factor: Cantidad = Base x Rendimiento x Factor.
        decimal rend = recurso.Rendimiento <= 0m ? 1m : recurso.Rendimiento;
        return ApplyMin(baseQty * rend * Scale(recurso, parametros), recurso.Minimo);
    }

    /// <summary>
    /// Cantidad de una fila esclava dada la cantidad de su maestro, con los parámetros ya
    /// resueltos de la plantilla (véase <see cref="CubicacionRow.Esclavo"/>). Es la única
    /// fuente de verdad de la propagación maestro→esclavo en la hoja de cubicación.
    /// </summary>
    public static decimal ComputeEsclavoValue(decimal baseQty, CubicacionEsclavoVinculo vinculo)
    {
        if (vinculo == null) throw new ArgumentNullException(nameof(vinculo));

        if (vinculo.TipoCalculo == TipoCalculoParametrico.Espaciamiento && vinculo.Espaciamiento > 0m)
            return ApplyMin((decimal)Math.Ceiling((double)(baseQty / vinculo.Espaciamiento)), vinculo.Minimo);

        decimal rend = vinculo.Rendimiento <= 0m ? 1m : vinculo.Rendimiento;
        decimal factor = vinculo.Factor <= 0m ? 1m : vinculo.Factor;
        return ApplyMin(baseQty * rend * factor, vinculo.Minimo);
    }

    /// <summary>Copia los parámetros de cálculo de un recurso de plantilla a la fila de cubicación.</summary>
    private static CubicacionEsclavoVinculo ToVinculo(ConjuntoRecursoRow recurso, string maestroCodigo, Dictionary<string, decimal>? parametros)
        => new()
        {
            MaestroCodigo = maestroCodigo ?? "",
            TipoCalculo = recurso.TipoCalculo,
            Rendimiento = recurso.Rendimiento <= 0m ? 1m : recurso.Rendimiento,
            Factor = Scale(recurso, parametros),
            Espaciamiento = recurso.Espaciamiento,
            Minimo = recurso.Minimo
        };

    /// <summary>
    /// Cantidades de las filas maestro (sin MaestroId) para una base dada. Los esclavos no
    /// se incluyen: nunca son base de otro recurso (un nivel, sin cadenas ni ciclos). Las
    /// filas con Operación se evalúan por su fórmula (referencias por código a las demás
    /// filas) en orden topológico; errores o ciclos quedan en <paramref name="advertencias"/>
    /// y la fila queda en 0 sin romper la expansión.
    /// </summary>
    private static Dictionary<string, decimal> ResolveMasterQuantities(
        ConjuntoElectricoRow conjunto, decimal baseQty, List<string>? advertencias = null)
    {
        var quantities = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var operaciones = new List<ConjuntoRecursoRow>();
        var filasBase = new List<CubicacionRow>();

        foreach (var recurso in conjunto.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            if (!string.IsNullOrWhiteSpace(recurso.MaestroId)) continue;
            if (string.IsNullOrWhiteSpace(recurso.Id)) continue;
            if (!string.IsNullOrWhiteSpace(recurso.Operacion))
            {
                operaciones.Add(recurso);
                continue;
            }
            decimal qty = ComputeQuantity(baseQty, recurso, conjunto.Parametros);
            quantities[recurso.Id] = qty;
            filasBase.Add(new CubicacionRow { Id = recurso.Id, Codigo = recurso.Codigo, Cantidad = qty });
        }

        if (operaciones.Count == 0) return quantities;

        // Filas temporales para reutilizar el resolver de operaciones (mismo camino que la
        // hoja de cubicación): base = filas ya calculadas, pendientes = filas Operación.
        var temporales = new List<CubicacionRow>(filasBase.Count + operaciones.Count);
        temporales.AddRange(filasBase);
        var pendientes = new Dictionary<string, CubicacionRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in operaciones)
        {
            var temp = new CubicacionRow { Id = op.Id, Codigo = op.Codigo, Operacion = op.Operacion, Cantidad = 0m };
            temporales.Add(temp);
            pendientes[op.Id] = temp;
        }

        OperacionFilaService.Resolver(temporales, advertencias);

        foreach (var kv in pendientes)
            quantities[kv.Key] = kv.Value.Cantidad;
        return quantities;
    }

    private static string RowLabel(ConjuntoRecursoRow recurso)
    {
        string desc = (recurso.Descripcion ?? "").Trim();
        if (desc.Length > 0) return desc;
        string codigo = (recurso.Codigo ?? "").Trim();
        return codigo.Length > 0 ? codigo : "sin código";
    }

    private static decimal ComputeRendimiento(decimal baseQty, ConjuntoRecursoRow recurso, Dictionary<string, decimal>? parametros)
        => ApplyMin(recurso.Rendimiento * Scale(recurso, parametros), recurso.Minimo);

    private static decimal Scale(ConjuntoRecursoRow recurso, Dictionary<string, decimal>? parametros)
    {
        decimal factor = recurso.Factor <= 0m ? 1m : recurso.Factor;
        if (parametros == null || parametros.Count == 0) return factor;
        if (parametros.TryGetValue("factor", out decimal globalFactor) && globalFactor > 0m)
            factor *= globalFactor;
        return factor;
    }

    private static decimal ApplyMin(decimal value, decimal minimo) => minimo > 0m && value < minimo ? minimo : value;

    private static MaterialRow? FindMaterial(IReadOnlyList<MaterialRow> materiales, string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return materiales.FirstOrDefault(m => string.Equals(m.Codigo.Trim(), codigo.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string MapCubicacionTipo(ApuTipoRecurso tipo) => tipo switch
    {
        ApuTipoRecurso.MO => "MO",
        ApuTipoRecurso.EQ => "EQ",
        _ => "MAT"
    };

    private static IEnumerable<string> SplitKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords)) yield break;
        foreach (var part in keywords.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!string.IsNullOrWhiteSpace(part)) yield return part;
    }

    private static List<CubicacionRow> MergeRows(IEnumerable<CubicacionRow> rows)
    {
        // El orden de la plantilla se preserva: GroupBy mantiene la primera aparición de cada
        // grupo. Antes se reordenaba por Tipo/Código, lo que mezclaba filas al cargar y no
        // coincidía con el orden de la pestaña Plantillas.
        return (rows ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) || !string.IsNullOrWhiteSpace(x.Descripcion))
            .GroupBy(x => new
            {
                Codigo = (x.Codigo ?? "").Trim().ToUpperInvariant(),
                Tipo = (x.Tipo ?? "").Trim().ToUpperInvariant(),
                Unidad = (x.Unidad ?? "").Trim().ToUpperInvariant(),
                x.MatUf,
                x.MoUf,
                Operacion = x.Operacion ?? ""
            })
            .Select(g =>
            {
                var first = g.First();
                return new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = first.ItemId,
                    Tipo = first.Tipo,
                    Codigo = first.Codigo,
                    Descripcion = first.Descripcion,
                    Unidad = first.Unidad,
                    Cantidad = g.Sum(x => x.Cantidad),
                    MatUf = first.MatUf,
                    MoUf = first.MoUf,
                    Area = first.Area,
                    Operacion = first.Operacion ?? "",
                    Esclavo = first.Esclavo?.Clone()
                };
            })
            .ToList();
    }
}
