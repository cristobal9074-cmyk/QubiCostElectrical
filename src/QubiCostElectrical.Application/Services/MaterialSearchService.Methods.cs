using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

public sealed partial class MaterialSearchService : IMaterialSearchService
{
    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string v = value.Trim();

        if (v.Contains('|'))
            v = v.Split('|')[0].Trim();

        v = RemoveDiacritics(v);

        return v
            .Replace("-", "")
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("_", "")
            .Replace("/", "")
            .Replace("→", "")
            .ToUpperInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public List<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> BuildIndex(IEnumerable<MaterialRow> materiales)
    {
        var index = new List<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)>();

        foreach (var material in materiales)
        {
            if (material == null) continue;

            string codigoKey = Normalize(material.Codigo);
            string descripcionKey = Normalize(material.Descripcion);
            string categoriaKey = Normalize(material.Categoria);
            string subcategoriaKey = Normalize(material.Subcategoria);
            string subcategoria2Key = Normalize(material.Subcategoria2);
            string tipoInstalacionKey = Normalize(material.TipoInstalacion);
            string especificacionKey = Normalize(material.EspecificacionTecnica);
            string marcaKey = Normalize(material.Marca);
            string equivalenciaKey = Normalize(material.EquivalenciaKey);
            string proveedorKey = Normalize(material.Proveedor);
            string unidadKey = Normalize(material.Unidad);

            string hierarchyKey = Normalize(GetHierarchy(material));

            index.Add((
                material,
                codigoKey,
                descripcionKey,
                string.Join(" ", new[]
                {
                    codigoKey,
                    descripcionKey,
                    categoriaKey,
                    subcategoriaKey,
                    subcategoria2Key,
                    tipoInstalacionKey,
                    especificacionKey,
                    marcaKey,
                    equivalenciaKey,
                    proveedorKey,
                    unidadKey,
                    hierarchyKey
                }.Where(x => !string.IsNullOrWhiteSpace(x)))
            ));
        }

        return index;
    }

    private IReadOnlyList<int>? GetCandidateIndexes(
        string normalizedQuery,
        IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery) || index.Count == 0) return null;

        EnsureInvertedIndex(index);
        string[] tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) tokens = new[] { normalizedQuery };

        List<int>? result = null;
        lock (invertedIndexLock)
        {
            foreach (string token in tokens)
            {
                if (token.Length < 2) continue;
                if (!invertedTokenIndex.TryGetValue(token, out var exact))
                {
                    // Prefijo de 3 caracteres para búsquedas tipo PVC, THHN, EMT, etc.
                    string prefix = token.Length >= 3 ? token[..3] : token;
                    if (!invertedTokenIndex.TryGetValue(prefix, out exact)) return null;
                }

                result = result == null
                    ? exact.ToList()
                    : result.Intersect(exact).ToList();

                if (result.Count == 0) return null;
            }
        }

        return result;
    }

    private void EnsureInvertedIndex(IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index)
    {
        int fingerprint = BuildIndexFingerprint(index);
        lock (invertedIndexLock)
        {
            if (fingerprint == invertedIndexFingerprint && invertedTokenIndex.Count > 0) return;

            var next = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < index.Count; i++)
            {
                foreach (string token in TokenizeForInvertedIndex(index[i]))
                {
                    if (!next.TryGetValue(token, out var list))
                    {
                        list = new List<int>();
                        next[token] = list;
                    }

                    if (list.Count == 0 || list[^1] != i)
                        list.Add(i);
                }
            }

            invertedTokenIndex = next;
            invertedIndexFingerprint = fingerprint;
        }
    }

    private static int BuildIndexFingerprint(IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + index.Count;
            for (int i = 0; i < index.Count; i++)
            {
                hash = hash * 31 + (index[i].CodigoKey?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0);
                hash = hash * 31 + (index[i].SearchKey?.Length ?? 0);
            }
            return hash;
        }
    }

    private static IEnumerable<string> TokenizeForInvertedIndex((MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey) entry)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddToken(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (string part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length >= 2) tokens.Add(part);
                if (part.Length >= 3) tokens.Add(part[..3]);
                if (part.Length >= 4) tokens.Add(part[..4]);
            }
        }

        AddToken(entry.CodigoKey);
        AddToken(entry.DescripcionKey);
        AddToken(entry.SearchKey);
        return tokens;
    }

    public List<MaterialRow> FindMatches(
        string search,
        IReadOnlyList<MaterialRow> materiales,
        IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index,
        int limit = 50)
    {
        return FindMatches(search, materiales, index, new MaterialCatalogFilter(), limit);
    }

    public List<MaterialRow> FindMatches(
        string search,
        IReadOnlyList<MaterialRow> materiales,
        IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index,
        MaterialCatalogFilter? filter,
        int limit = 50)
    {
        string rawSearch = search ?? string.Empty;
        string query = Normalize(rawSearch);
        var queryTokens = rawSearch
            .Split(new[] { ' ', '\t', '\r', '\n', '-', '_', '/', '\\', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string candidateQuery = queryTokens.Count > 1 ? string.Join(" ", queryTokens) : query;
        filter ??= new MaterialCatalogFilter();

        if (string.IsNullOrWhiteSpace(query) && filter.IsEmpty)
            return materiales.Take(limit).ToList();

        var scored = new List<(MaterialRow Material, int Score, string Codigo, string Descripcion)>();
        var candidates = GetCandidateIndexes(candidateQuery, index);
        IEnumerable<int> indexesToScan = candidates == null || candidates.Count == 0 && string.IsNullOrWhiteSpace(query)
            ? Enumerable.Range(0, index.Count)
            : candidates;

        foreach (int i in indexesToScan)
        {
            if (i < 0 || i >= index.Count) continue;
            var entry = index[i];
            if (!MatchesFilter(entry.Material, filter))
                continue;

            int score = string.IsNullOrWhiteSpace(query) ? 100 : -1;

            if (!string.IsNullOrWhiteSpace(query))
            {
                if (entry.CodigoKey.Equals(query, StringComparison.OrdinalIgnoreCase))
                    score = 0;
                else if (entry.CodigoKey.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    score = 5;
                else if (entry.DescripcionKey.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    score = 15;
                else if (entry.CodigoKey.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score = 25;
                else if (entry.DescripcionKey.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score = 35;
                else if (entry.SearchKey.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score = 45;
                else if (queryTokens.Count > 1)
                {
                    bool codeMatchesTokens = queryTokens.All(t => entry.CodigoKey.Contains(t, StringComparison.OrdinalIgnoreCase));
                    bool descMatchesTokens = queryTokens.All(t => entry.DescripcionKey.Contains(t, StringComparison.OrdinalIgnoreCase));
                    bool searchMatchesTokens = queryTokens.All(t => entry.SearchKey.Contains(t, StringComparison.OrdinalIgnoreCase));

                    if (codeMatchesTokens)
                        score = 12;
                    else if (descMatchesTokens)
                        score = 22;
                    else if (searchMatchesTokens)
                        score = 32;
                }

                if (score >= 0 && queryTokens.Count == 1)
                {
                    string token = queryTokens[0];
                    if (entry.CodigoKey.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                        score = Math.Min(score, 4);
                    else if (entry.DescripcionKey.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                        score = Math.Min(score, 14);
                    else if (entry.SearchKey.Contains(token, StringComparison.OrdinalIgnoreCase))
                        score = Math.Min(score, 40);
                }
            }

            if (score >= 0)
                scored.Add((entry.Material, score, entry.Material.Codigo, entry.Material.Descripcion));
        }

        return scored
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Descripcion, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Material)
            .Take(limit)
            .ToList();
    }


    public IReadOnlyList<MaterialCatalogQuickFilter> BuildQuickFilters(IEnumerable<MaterialRow> matches, int max = 8)
    {
        if (matches == null) return Array.Empty<MaterialCatalogQuickFilter>();

        var source = matches.Where(m => m != null).ToList();
        if (source.Count == 0) return Array.Empty<MaterialCatalogQuickFilter>();

        var result = new List<MaterialCatalogQuickFilter>();

        result.AddRange(source
            .Where(m => !string.IsNullOrWhiteSpace(m.Categoria))
            .GroupBy(m => m.Categoria.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, max / 3))
            .Select(g => new MaterialCatalogQuickFilter
            {
                Label = g.Key,
                Hint = $"Categoría · {g.Count():N0}",
                Count = g.Count(),
                Filter = new MaterialCatalogFilter { Categoria = g.Key }
            }));

        result.AddRange(source
            .Where(m => !string.IsNullOrWhiteSpace(m.Subcategoria))
            .GroupBy(m => new { Categoria = (m.Categoria ?? "").Trim(), Subcategoria = m.Subcategoria.Trim() })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Subcategoria, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, max / 3))
            .Select(g => new MaterialCatalogQuickFilter
            {
                Label = g.Key.Subcategoria,
                Hint = string.IsNullOrWhiteSpace(g.Key.Categoria) ? $"Subcategoría · {g.Count():N0}" : $"{g.Key.Categoria} · {g.Count():N0}",
                Count = g.Count(),
                Filter = new MaterialCatalogFilter { Categoria = g.Key.Categoria, Subcategoria = g.Key.Subcategoria }
            }));

        result.AddRange(source
            .Where(m => !string.IsNullOrWhiteSpace(m.Subcategoria2))
            .GroupBy(m => new
            {
                Categoria = (m.Categoria ?? "").Trim(),
                Subcategoria = (m.Subcategoria ?? "").Trim(),
                Subcategoria2 = m.Subcategoria2.Trim()
            })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Subcategoria2, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(g => new MaterialCatalogQuickFilter
            {
                Label = g.Key.Subcategoria2,
                Hint = string.Join(" › ", new[] { g.Key.Categoria, g.Key.Subcategoria }.Where(x => !string.IsNullOrWhiteSpace(x))) + $" · {g.Count():N0}",
                Count = g.Count(),
                Filter = new MaterialCatalogFilter { Categoria = g.Key.Categoria, Subcategoria = g.Key.Subcategoria, Subcategoria2 = g.Key.Subcategoria2 }
            }));

        result.AddRange(source
            .Where(m => !string.IsNullOrWhiteSpace(m.TipoInstalacion))
            .GroupBy(m => m.TipoInstalacion.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(g => new MaterialCatalogQuickFilter
            {
                Label = g.Key,
                Hint = $"Tipo instalación · {g.Count():N0}",
                Count = g.Count(),
                Filter = new MaterialCatalogFilter { TipoInstalacion = g.Key }
            }));

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .GroupBy(x => Normalize(x.Hint + "|" + x.Label), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(max)
            .ToList();
    }

    public bool MatchesFilter(MaterialRow material, MaterialCatalogFilter? filter)
    {
        if (material == null) return false;
        if (filter == null || filter.IsEmpty) return true;

        if (!MatchesExactOrContains(material.Categoria, filter.Categoria)) return false;
        if (!MatchesExactOrContains(material.Subcategoria, filter.Subcategoria)) return false;
        if (!MatchesExactOrContains(material.Subcategoria2, filter.Subcategoria2)) return false;
        if (!MatchesExactOrContains(material.TipoInstalacion, filter.TipoInstalacion)) return false;
        if (!MatchesExactOrContains(material.EspecificacionTecnica, filter.EspecificacionTecnica)) return false;

        return true;
    }

    public string GetHierarchy(MaterialRow material)
    {
        if (material == null) return "";
        return string.Join(" → ", new[]
        {
            material.Categoria,
            material.Subcategoria,
            material.Subcategoria2
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private bool MatchesExactOrContains(string source, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        string sourceKey = Normalize(source);
        string filterKey = Normalize(filter);
        if (string.IsNullOrWhiteSpace(filterKey)) return true;
        return sourceKey.Equals(filterKey, StringComparison.OrdinalIgnoreCase)
            || sourceKey.Contains(filterKey, StringComparison.OrdinalIgnoreCase);
    }
}
