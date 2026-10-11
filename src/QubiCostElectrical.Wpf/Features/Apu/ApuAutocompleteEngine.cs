using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed class ApuAutocompleteSuggestionVm
{
    public ApuAutocompleteSuggestionVm(
        string codigo,
        string descripcion,
        string unidad,
        decimal matClp,
        decimal matUf,
        string proveedor,
        string matchReason,
        string sourceBadge)
    {
        Codigo = codigo ?? "";
        Descripcion = descripcion ?? "";
        Unidad = string.IsNullOrWhiteSpace(unidad) ? "un" : unidad;
        MatClp = matClp;
        MatUf = matUf;
        Proveedor = proveedor ?? "";
        MatchReason = matchReason ?? "";
        SourceBadge = sourceBadge ?? "BANCO";
    }

    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public decimal MatClp { get; }
    public decimal MatUf { get; }
    public string Proveedor { get; }
    public string MatchReason { get; }
    public string SourceBadge { get; }

    public bool HasProveedor => !string.IsNullOrWhiteSpace(Proveedor);
    public string MatClpDisplay => MatClp > 0m ? $"$ {MatClp:N0}" : (MatUf > 0m ? "—" : "$ 0");
    public string MatUfDisplay => MatUf > 0m ? $"{MatUf:N4} UF" : "0.0000 UF";
}

public sealed class ApuAutocompleteEngine
{
    private readonly BudgetProjectSession session;
    private readonly IMaterialSearchService materialSearch;
    private readonly IPriceBankStore? priceBankStore;
    private List<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> materialIndex = new();

    public ApuAutocompleteEngine(
        BudgetProjectSession session,
        IMaterialSearchService materialSearch,
        IPriceBankStore? priceBankStore = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.materialSearch = materialSearch ?? throw new ArgumentNullException(nameof(materialSearch));
        this.priceBankStore = priceBankStore;
    }

    public void InvalidateIndex() => materialIndex.Clear();

    public async Task<IReadOnlyList<ApuAutocompleteSuggestionVm>> SearchAsync(string rawQuery, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery) || rawQuery.Trim().Length < 2)
            return Array.Empty<ApuAutocompleteSuggestionVm>();

        string query = rawQuery.Trim();
        decimal valorUf = session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;

        if (materialIndex.Count == 0 && session.Materiales.Count > 0)
        {
            materialIndex = materialSearch.BuildIndex(session.Materiales);
        }

        return await Task.Run(() =>
        {
            var resultsMap = new Dictionary<string, ApuAutocompleteSuggestionVm>(StringComparer.OrdinalIgnoreCase);
            var tokens = query
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(materialSearch.Normalize)
                .Where(t => t.Length > 0)
                .ToArray();

            string normQuery = materialSearch.Normalize(query);

            // 1. Buscar en el catálogo local del proyecto (session.Materiales)
            foreach (var entry in materialIndex)
            {
                if (ct.IsCancellationRequested) return (IReadOnlyList<ApuAutocompleteSuggestionVm>)Array.Empty<ApuAutocompleteSuggestionVm>();

                bool match = false;
                string reason = "Catálogo";

                if (entry.CodigoKey.Equals(normQuery, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                    reason = "Código exacto";
                }
                else if (entry.DescripcionKey.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                    reason = "Descripción";
                }
                else if (tokens.Length > 0 && tokens.All(t => entry.DescripcionKey.Contains(t, StringComparison.OrdinalIgnoreCase) || entry.CodigoKey.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    match = true;
                    reason = "Coincidencia";
                }

                if (match)
                {
                    var mat = entry.Material;
                    decimal matClp = mat.MatClpConFallback;
                    decimal matUf = mat.MatUf;
                    if (matUf <= 0m && matClp > 0m)
                        matUf = PriceConversion.ClpToUf(matClp, valorUf);
                    else if (matClp <= 0m && matUf > 0m)
                        matClp = Math.Round(matUf * valorUf, MidpointRounding.AwayFromZero);

                    resultsMap[mat.Codigo] = new ApuAutocompleteSuggestionVm(
                        mat.Codigo,
                        mat.Descripcion,
                        mat.Unidad,
                        matClp,
                        matUf,
                        mat.Proveedor,
                        reason,
                        "PROYECTO");
                }
            }

            // 2. Buscar en recursos ya cargados en otros APUs del proyecto activo
            if (session.Apus.Count > 0)
            {
                foreach (var apu in session.Apus)
                {
                    if (ct.IsCancellationRequested) break;
                    if (apu.Recursos == null) continue;

                    foreach (var rec in apu.Recursos)
                    {
                        if (string.IsNullOrWhiteSpace(rec.CodigoMaterial) || resultsMap.ContainsKey(rec.CodigoMaterial))
                            continue;

                        string normCod = materialSearch.Normalize(rec.CodigoMaterial);
                        string normDesc = materialSearch.Normalize(rec.Descripcion);

                        bool match = normCod.Contains(normQuery, StringComparison.OrdinalIgnoreCase) ||
                                     normDesc.Contains(normQuery, StringComparison.OrdinalIgnoreCase) ||
                                     (tokens.Length > 0 && tokens.All(t => normDesc.Contains(t, StringComparison.OrdinalIgnoreCase) || normCod.Contains(t, StringComparison.OrdinalIgnoreCase)));

                        if (match)
                        {
                            decimal matUf = rec.PrecioUf;
                            decimal matClp = Math.Round(matUf * valorUf, MidpointRounding.AwayFromZero);

                            resultsMap[rec.CodigoMaterial] = new ApuAutocompleteSuggestionVm(
                                rec.CodigoMaterial,
                                rec.Descripcion,
                                rec.Unidad,
                                matClp,
                                matUf,
                                rec.FuentePrecio,
                                "En presupuesto",
                                "APU");
                        }
                    }
                }
            }

            // 3. Buscar en el Banco de Precios central si se dispone de él
            if (priceBankStore != null && resultsMap.Count < 20 && !ct.IsCancellationRequested)
            {
                try
                {
                    var bankEntries = priceBankStore.Search(query);
                    foreach (var entry in bankEntries)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (string.IsNullOrWhiteSpace(entry.Codigo) || resultsMap.ContainsKey(entry.Codigo))
                            continue;

                        decimal matClp = entry.MatClpConFallback;
                        decimal matUf = entry.MatUf;
                        if (matUf <= 0m && matClp > 0m)
                            matUf = PriceConversion.ClpToUf(matClp, valorUf);
                        else if (matClp <= 0m && matUf > 0m)
                            matClp = Math.Round(matUf * valorUf, MidpointRounding.AwayFromZero);

                        resultsMap[entry.Codigo] = new ApuAutocompleteSuggestionVm(
                            entry.Codigo,
                            entry.Descripcion,
                            entry.Unidad,
                            matClp,
                            matUf,
                            string.IsNullOrWhiteSpace(entry.Marca) ? entry.Proveedor : $"{entry.Marca} · {entry.Proveedor}".TrimEnd(' ', '·'),
                            "Banco central",
                            "BANCO");

                        if (resultsMap.Count >= 25) break;
                    }
                }
                catch
                {
                    // Fallback silencioso si la BD de banco está ocupada
                }
            }

            // 4. Ordenamiento por relevancia técnica
            return resultsMap.Values
                .OrderBy(s =>
                {
                    string code = materialSearch.Normalize(s.Codigo);
                    string desc = materialSearch.Normalize(s.Descripcion);

                    if (code.Equals(normQuery, StringComparison.OrdinalIgnoreCase)) return 0;
                    if (desc.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase)) return 1;
                    if (code.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase)) return 2;
                    if (tokens.Length > 0 && tokens.All(t => desc.Contains(t, StringComparison.OrdinalIgnoreCase))) return 3;
                    if (desc.Contains(normQuery, StringComparison.OrdinalIgnoreCase)) return 4;
                    return 5;
                })
                .ThenBy(s => s.Descripcion.Length)
                .Take(15)
                .ToList();
        }, ct);
    }
}
