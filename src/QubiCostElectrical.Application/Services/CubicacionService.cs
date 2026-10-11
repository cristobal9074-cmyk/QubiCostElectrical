using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record QtyHeaderUpdateResult(string[] Values, bool Changed, bool Stored, bool AllDefaults);

public sealed class CubicacionService : ICubicacionService
{
    public List<CubicacionRow> GetRowsForItem(IEnumerable<CubicacionRow> cubicacion, string itemId, bool onlyWithCode = false)
        => CubicacionItemIndex.FilterRows(cubicacion, itemId, onlyWithCode);

    public List<CubicacionRow> GetRowsForItem(IReadOnlyDictionary<string, List<CubicacionRow>> index, string itemId, bool onlyWithCode = false)
        => CubicacionItemIndex.GetRows(index, itemId, onlyWithCode);

    public CubicacionRow? GetVisualRow(IEnumerable<CubicacionRow> cubicacion, string itemId, int visualIndex)
    {
        if (visualIndex < 0) return null;
        var rows = GetRowsForItem(cubicacion, itemId);
        return visualIndex < rows.Count ? rows[visualIndex] : null;
    }

    public Result<CubicacionRow, ValidationError> TryGetOrCreateVisualRow(List<CubicacionRow> cubicacion, string itemId, int visualIndex)
    {
        if (cubicacion == null)
            return Result<CubicacionRow, ValidationError>.Failure(new ValidationError("CUB-ROW-001", "La lista de cubicación no puede ser nula.", nameof(cubicacion)));
        if (string.IsNullOrWhiteSpace(itemId))
            return Result<CubicacionRow, ValidationError>.Failure(new ValidationError("CUB-ROW-002", "Debe seleccionar una partida antes de agregar materiales.", nameof(itemId)));
        if (visualIndex < 0)
            return Result<CubicacionRow, ValidationError>.Failure(new ValidationError("CUB-ROW-003", "La fila visual no puede ser negativa.", nameof(visualIndex)));

        return Result<CubicacionRow, ValidationError>.Success(GetOrCreateVisualRow(cubicacion, itemId, visualIndex));
    }

    public CubicacionRow GetOrCreateVisualRow(List<CubicacionRow> cubicacion, string itemId, int visualIndex)
    {
        var existing = GetVisualRow(cubicacion, itemId, visualIndex);
        if (existing != null) return existing;

        var row = new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = itemId ?? "",
            Tipo = "MAT",
            Area = "General"
        };

        cubicacion.Add(row);
        return row;
    }

    public void ClearRow(CubicacionRow row)
    {
        row.Codigo = "";
        row.Descripcion = "";
        row.Unidad = "";
        row.Cantidad = 0m;
        row.MatClp = 0m;
        row.MoClp = 0m;
        row.MatUf = 0m;
        row.MoUf = 0m;
        row.Tipo = "MAT";
        row.Area = "General";
        row.QtyExpressions = Array.Empty<string>();
        row.TotalExpression = "";
        row.Operacion = "";
        row.MermaPctOverride = -1m;
        row.HerramientasPctOverride = -1m;
        row.RedondeoMultiplo = 0m;
        row.RedondeoModo = "Ninguno";
    }

    public void RemoveEmptyRowsForItem(List<CubicacionRow> cubicacion, string itemId)
    {
        cubicacion.RemoveAll(x =>
            string.Equals(x.ItemId, itemId ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(x.Codigo) &&
            string.IsNullOrWhiteSpace(x.Operacion) &&
            x.Cantidad == 0m &&
            (x.QtyExpressions == null || !x.QtyExpressions.Any(v => !string.IsNullOrWhiteSpace(v))));
    }

    public void ApplyMaterial(CubicacionRow row, MaterialRow material, decimal valorUfClp = 0m, ProjectInfoRow? project = null)
    {
        row.Codigo = CodeHelper.Normalize(material.Codigo);
        row.Descripcion = material.Descripcion;
        row.Unidad = material.Unidad;
        row.Categoria = material.Categoria;
        row.MatClp = material.MatClp;
        row.MoClp = MaterialMoResolver.MoClpEfectiva(material, project);
        // El UF se deriva siempre del CLP con el valor UF vigente del proyecto, nunca se
        // copia el UF del material (puede venir en 0 o calculado con otro valor UF).
        row.RecalcularUf(valorUfClp);
        row.Tipo = string.Equals(material.Categoria, "Equipo", StringComparison.OrdinalIgnoreCase) ? "EQ" : "MAT";
    }

    public int SyncPricesFromCatalog(IEnumerable<CubicacionRow> cubicacion, IEnumerable<MaterialRow> materiales, decimal valorUfClp, ProjectInfoRow? project = null)
        => SyncPricesFromCatalog(cubicacion, materiales, valorUfClp, valorizaciones: null, project);

    /// <summary>
    /// Igual que <see cref="SyncPricesFromCatalog(IEnumerable{CubicacionRow}, IEnumerable{MaterialRow}, decimal)"/>
    /// y, si se pasóan valorizaciones, actualiza también el snapshot CLP de cada recurso.
    /// </summary>
    public int SyncPricesFromCatalog(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        decimal valorUfClp,
        IList<CubicacionValorizacionRow>? valorizaciones,
        ProjectInfoRow? project = null)
    {
        var byCode = (materiales ?? Enumerable.Empty<MaterialRow>())
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => CodeHelper.Normalize(m.Codigo))
            .ToDictionary(g => g.Key, g => g.First());

        Dictionary<string, CubicacionValorizacionRow>? valorizacionByRecurso = null;
        if (valorizaciones != null)
        {
            valorizacionByRecurso = new Dictionary<string, CubicacionValorizacionRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in valorizaciones)
            {
                if (!string.IsNullOrWhiteSpace(v.RecursoId) && !valorizacionByRecurso.ContainsKey(v.RecursoId))
                    valorizacionByRecurso[v.RecursoId] = v;
            }
        }

        int updated = 0;
        foreach (var row in cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            if (string.IsNullOrWhiteSpace(row.Codigo)) continue;
            if (!byCode.TryGetValue(CodeHelper.Normalize(row.Codigo), out var material)) continue;

            bool isCategoryUpdated = false;
            if (row.Categoria != material.Categoria)
            {
                row.Categoria = material.Categoria;
                isCategoryUpdated = true;
            }

            decimal moEfectiva = MaterialMoResolver.MoClpEfectiva(material, project);
            if (row.MatClp == material.MatClp && row.MoClp == moEfectiva)
            {
                if (isCategoryUpdated)
                {
                    updated++;
                }
                continue;
            }

            row.MatClp = material.MatClp;
            row.MoClp = moEfectiva;
            row.RecalcularUf(valorUfClp);

            if (valorizacionByRecurso != null &&
                valorizacionByRecurso.TryGetValue(row.Id, out var valorizacion))
            {
                valorizacion.MatClp = material.MatClp;
                valorizacion.MoClp = moEfectiva;
                valorizacion.MatUf = row.MatUf;
                valorizacion.MoUf = row.MoUf;
                valorizacion.FuentePrecio = "Lista de precios";
                valorizacion.FechaPrecio = material.FechaPrecio == default ? DateTime.Today : material.FechaPrecio;
            }

            updated++;
        }
        return updated;
    }

    public CubicacionRow CloneRowForItem(CubicacionRow source, string targetItemId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        return new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = targetItemId ?? string.Empty,
            Tipo = source.Tipo,
            Codigo = source.Codigo,
            Descripcion = source.Descripcion,
            Unidad = source.Unidad,
            Cantidad = source.Cantidad,
            MatClp = source.MatClp,
            MoClp = source.MoClp,
            MatUf = source.MatUf,
            MoUf = source.MoUf,
            Area = source.Area,
            QtyExpressions = (source.QtyExpressions ?? Array.Empty<string>()).ToArray(),
            TotalExpression = source.TotalExpression,
            MermaPctOverride = source.MermaPctOverride,
            HerramientasPctOverride = source.HerramientasPctOverride,
            RedondeoMultiplo = source.RedondeoMultiplo,
            RedondeoModo = source.RedondeoModo,
            NotaInterna = source.NotaInterna,
            GgUtFactor = source.GgUtFactor,
            Operacion = source.Operacion ?? "",
            Esclavo = source.Esclavo?.Clone()
        };
    }

    public CubicacionCloneResult CloneItemCubicacion(
        List<CubicacionRow> cubicacion,
        string sourceItemId,
        string targetItemId,
        Dictionary<string, string[]>? qtyHeadersByItem = null,
        IList<CubicacionCellCommentRow>? cellComments = null,
        CubicacionCloneOptions? options = null)
    {
        if (cubicacion == null) throw new ArgumentNullException(nameof(cubicacion));

        sourceItemId = (sourceItemId ?? string.Empty).Trim();
        targetItemId = (targetItemId ?? string.Empty).Trim();
        options ??= new CubicacionCloneOptions();

        if (string.IsNullOrWhiteSpace(sourceItemId))
            throw new ArgumentException("Debe indicar una partida origen.", nameof(sourceItemId));
        if (string.IsNullOrWhiteSpace(targetItemId))
            throw new ArgumentException("Debe indicar una partida destino.", nameof(targetItemId));
        if (string.Equals(sourceItemId, targetItemId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La partida origen y la partida destino deben ser distintas.");

        var sourceRows = cubicacion
            .Where(x => string.Equals(x.ItemId, sourceItemId, StringComparison.OrdinalIgnoreCase) && HasCubicacionContent(x))
            .ToList();

        if (sourceRows.Count == 0)
            return new CubicacionCloneResult(0, 0, false, false, 0, sourceItemId, targetItemId);

        var targetIndexesBefore = cubicacion
            .Select((row, index) => new { row, index })
            .Where(x => string.Equals(x.row.ItemId, targetItemId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList();

        int firstTargetIndex = targetIndexesBefore.Count > 0 ? targetIndexesBefore[0] : -1;
        int lastTargetIndex = targetIndexesBefore.Count > 0 ? targetIndexesBefore[^1] : -1;
        int rowsRemoved = 0;

        if (options.ReplaceTarget)
        {
            rowsRemoved = cubicacion.RemoveAll(x => string.Equals(x.ItemId, targetItemId, StringComparison.OrdinalIgnoreCase));
        }

        var clones = sourceRows.Select(row => CloneRowForItem(row, targetItemId)).ToList();
        int insertAt = options.ReplaceTarget
            ? (firstTargetIndex >= 0 ? Math.Min(firstTargetIndex, cubicacion.Count) : cubicacion.Count)
            : (lastTargetIndex >= 0 ? Math.Min(lastTargetIndex + 1, cubicacion.Count) : cubicacion.Count);
        cubicacion.InsertRange(insertAt, clones);

        bool headersCopied = false;
        if (qtyHeadersByItem != null && options.ReplaceTarget)
            qtyHeadersByItem.Remove(targetItemId);

        if (options.CopyQtyHeaders && qtyHeadersByItem != null && qtyHeadersByItem.TryGetValue(sourceItemId, out var headers))
        {
            qtyHeadersByItem[targetItemId] = (headers ?? Array.Empty<string>()).ToArray();
            headersCopied = true;
        }

        int commentsCopied = 0;
        if (cellComments != null && options.ReplaceTarget)
        {
            for (int i = cellComments.Count - 1; i >= 0; i--)
            {
                if (string.Equals(cellComments[i].ItemId, targetItemId, StringComparison.OrdinalIgnoreCase))
                    cellComments.RemoveAt(i);
            }
        }

        if (options.CopyCellComments && cellComments != null)
        {
            // sourceRows[i] se clonó 1:1 y en el mismo orden hacia clones[i] (ver arriba), así que
            // ese índice sirve de puente estable entre CubicacionRow.Id origen y destino, en vez de
            // reutilizar VisualRow (que ya no identifica de forma confiable a una fila específica).
            var sourceIdToCloneId = sourceRows
                .Select((row, index) => (row.Id, CloneId: clones[index].Id))
                .ToDictionary(x => x.Id, x => x.CloneId, StringComparer.OrdinalIgnoreCase);

            var now = AppClock.Now;
            var comments = cellComments
                .Where(x => string.Equals(x.ItemId, sourceItemId, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(x.CubicacionRowId)
                    && sourceIdToCloneId.ContainsKey(x.CubicacionRowId))
                .Select(x => new CubicacionCellCommentRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = targetItemId,
                    VisualRow = x.VisualRow,
                    CubicacionRowId = sourceIdToCloneId[x.CubicacionRowId],
                    ColumnKey = x.ColumnKey,
                    Comment = x.Comment,
                    Author = x.Author,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            foreach (var comment in comments)
                cellComments.Add(comment);

            commentsCopied = comments.Count;
        }

        return new CubicacionCloneResult(
            clones.Count,
            rowsRemoved,
            options.ReplaceTarget,
            headersCopied,
            commentsCopied,
            sourceItemId,
            targetItemId);
    }

    private static bool HasCubicacionContent(CubicacionRow row)
    {
        if (row == null) return false;
        return !string.IsNullOrWhiteSpace(row.Codigo) ||
               !string.IsNullOrWhiteSpace(row.Descripcion) ||
               !string.IsNullOrWhiteSpace(row.Operacion) ||
               row.Cantidad != 0m ||
               (row.QtyExpressions != null && row.QtyExpressions.Any(v => !string.IsNullOrWhiteSpace(v)));
    }

    public string DefaultQtyHeaderLabel(int index) => $"Zona {index + 1}";

    public string NormalizeQtyHeaderLabel(int index, string? value)
    {
        string label = (value ?? "").Trim();
        while (label.Contains("  ")) label = label.Replace("  ", " ");
        return string.IsNullOrWhiteSpace(label) ? DefaultQtyHeaderLabel(index) : label;
    }

    public string[] GetHeadersForItem(Dictionary<string, string[]> headersByItem, string itemId, int columnCount)
    {
        var result = new string[columnCount];
        headersByItem.TryGetValue(itemId ?? "", out var stored);

        for (int i = 0; i < columnCount; i++)
        {
            string raw = stored != null && i < stored.Length ? stored[i] : "";
            result[i] = NormalizeQtyHeaderLabel(i, raw);
        }

        return result;
    }

    public QtyHeaderUpdateResult SaveHeadersForItem(Dictionary<string, string[]> headersByItem, string itemId, IReadOnlyList<string?> labels, int columnCount)
    {
        var values = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            string raw = i < labels.Count ? labels[i] ?? "" : "";
            values[i] = NormalizeQtyHeaderLabel(i, raw);
        }

        bool hasExisting = headersByItem.TryGetValue(itemId ?? "", out var existing);
        bool allDefaults = AllQtyHeadersAreDefaults(values, columnCount);
        bool changed = hasExisting ? !SameQtyHeaders(existing, values, columnCount) : !allDefaults;

        if (hasExisting || !allDefaults)
            headersByItem[itemId ?? ""] = values;

        return new QtyHeaderUpdateResult(values, changed, hasExisting || !allDefaults, allDefaults);
    }

    public bool SameQtyHeaders(string[]? left, string[] right, int columnCount)
    {
        if (left == null) return false;

        for (int i = 0; i < columnCount; i++)
        {
            string a = i < left.Length ? NormalizeQtyHeaderLabel(i, left[i]) : DefaultQtyHeaderLabel(i);
            string b = i < right.Length ? NormalizeQtyHeaderLabel(i, right[i]) : DefaultQtyHeaderLabel(i);
            if (!string.Equals(a, b, StringComparison.Ordinal)) return false;
        }

        return true;
    }

    public bool AllQtyHeadersAreDefaults(string[] values, int columnCount)
    {
        for (int i = 0; i < columnCount; i++)
        {
            string value = i < values.Length ? NormalizeQtyHeaderLabel(i, values[i]) : DefaultQtyHeaderLabel(i);
            if (!string.Equals(value, DefaultQtyHeaderLabel(i), StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
