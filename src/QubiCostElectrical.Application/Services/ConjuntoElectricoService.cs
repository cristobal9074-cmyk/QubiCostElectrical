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
        IEnumerable<MaterialRow>? materiales)
    {
        var result = new ConjuntoExpansionResult();
        if (conjunto == null || partida == null) return result;

        decimal baseQty = partida.Cantidad < 0m ? 0m : partida.Cantidad;
        // Si la partida aún no tiene cantidad, expandir como receta unitaria (base = 1)
        // para que la plantilla sí cargue materiales visibles en cubicación.
        decimal expandBase = baseQty > 0m ? baseQty : 1m;
        var mats = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();

        foreach (var recurso in conjunto.Recursos ?? new List<ConjuntoRecursoRow>())
        {
            if (recurso.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH or ApuTipoRecurso.HERR)
                continue;

            decimal qty = ComputeQuantity(expandBase, recurso, conjunto.Parametros);
            if (qty <= 0m)
            {
                qty = recurso.Rendimiento > 0m ? recurso.Rendimiento : 1m;
                if (recurso.Factor > 0m)
                    qty *= recurso.Factor;
            }

            var material = FindMaterial(mats, recurso.Codigo);
            if (material == null && string.IsNullOrWhiteSpace(recurso.Descripcion))
            {
                string reference = string.IsNullOrWhiteSpace(recurso.Codigo) ? "sin código ni descripción" : $"'{recurso.Codigo}'";
                result.Warnings.Add($"No se encontró el material {reference} en la plantilla '{conjunto.Nombre}'.");
                continue;
            }

            result.Rows.Add(new CubicacionRow
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
                Area = conjunto.TipoConjunto
            });
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
        var mats = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
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
            decimal rend = ComputeRendimiento(baseQty, recurso, conjunto.Parametros);
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
            FuentePrecio = "Conjunto el�ctrico",
            FechaPrecio = DateTime.Today,
            Estado = (isLabor ? hh : precio) <= 0m ? "Falta precio" : "OK"
        };
    }

    private static decimal ComputeQuantity(decimal baseQty, ConjuntoRecursoRow recurso, Dictionary<string, decimal>? parametros)
    {
        // Modo Espaciamiento: Cantidad = Ceil(base / paso). Ej: soporte cada 1.5 m -> Ceil(1000/1.5) = 667.
        if (recurso.TipoCalculo == TipoCalculoParametrico.Espaciamiento && recurso.Espaciamiento > 0m)
            return ApplyMin((decimal)Math.Ceiling((double)(baseQty / recurso.Espaciamiento)), recurso.Minimo);

        // Modo Factor: Cantidad = Base x Rendimiento x Factor.
        decimal rend = recurso.Rendimiento <= 0m ? 1m : recurso.Rendimiento;
        return ApplyMin(baseQty * rend * Scale(recurso, parametros), recurso.Minimo);
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
        return (rows ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) || !string.IsNullOrWhiteSpace(x.Descripcion))
            .GroupBy(x => new
            {
                Codigo = (x.Codigo ?? "").Trim().ToUpperInvariant(),
                Tipo = (x.Tipo ?? "").Trim().ToUpperInvariant(),
                Unidad = (x.Unidad ?? "").Trim().ToUpperInvariant(),
                x.MatUf,
                x.MoUf
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
                    Area = first.Area
                };
            })
            .OrderBy(x => x.Tipo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
