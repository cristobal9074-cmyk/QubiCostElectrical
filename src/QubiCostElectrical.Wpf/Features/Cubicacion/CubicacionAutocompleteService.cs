using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class AutocompleteSuggestionVm
{
    public AutocompleteSuggestionVm(MaterialRow material, string matchReason)
    {
        Material = material;
        MatchReason = matchReason;
    }

    public MaterialRow Material { get; }
    public string MatchReason { get; }
    public string Codigo => Material.Codigo;
    public string Descripcion => Material.Descripcion;
    public string Unidad => Material.Unidad;
    public string Proveedor => Material.Proveedor;
    public decimal MatUf => Material.MatUf;
    public decimal MatClp => Material.MatClp;
    public decimal MoClp => Material.MoClp;
    public string MatClpDisplay => MatClp <= 0m ? "—" : MatClp.ToString("N0");
    public string MoClpDisplay => MoClp <= 0m ? "—" : MoClp.ToString("N0");
}

/// <summary>
/// Búsqueda de materiales para el autocompletado de la celda CÓDIGO — extraído de
/// CubicacionViewModel.Autocomplete.cs (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md). Comportamiento preservado tal cual,
/// incluyendo la doble cancelación redundante en HideAutocomplete() (AutocompleteVisible =
/// false ya cancela vía el setter de la propiedad; el Cancel() explícito que sigue es
/// redundante en el original y se mantiene así a propósito, no es un descuido).
/// </summary>
internal sealed class CubicacionAutocompleteService
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly IMaterialSearchService materialSearch;
    private readonly IPriceBankStore priceBankStore;
    private CancellationTokenSource? autocompleteCts;
    private List<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> materialIndex = new();

    public CubicacionAutocompleteService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        IMaterialSearchService materialSearch,
        IPriceBankStore priceBankStore)
    {
        this.parent = parent;
        this.session = session;
        this.materialSearch = materialSearch;
        this.priceBankStore = priceBankStore;
    }

    public void CancelPendingSearch() => autocompleteCts?.Cancel();

    public void InvalidateMaterialIndex() => materialIndex.Clear();

    public async Task SearchMaterialsAsync(string query)
    {
        autocompleteCts?.Cancel();
        autocompleteCts?.Dispose();
        autocompleteCts = new CancellationTokenSource();
        var ct = autocompleteCts.Token;

        if (string.IsNullOrWhiteSpace(query))
        {
            parent.AutocompleteQueryText = "";
            parent.AutocompleteVisible = false;
            parent.AutocompleteStatusText = "";
            parent.AutocompleteResults = new ObservableCollection<AutocompleteSuggestionVm>();
            return;
        }

        if (materialIndex.Count == 0 && session.Materiales.Count > 0)
            materialIndex = materialSearch.BuildIndex(session.Materiales);

        try
        {
            var usedCodes = new HashSet<string>(
                session.Cubicacion
                    .Select(c => c.Codigo)
                    .Where(c => !string.IsNullOrWhiteSpace(c)),
                StringComparer.OrdinalIgnoreCase);

            var projectResults = await Task.Run(() =>
            {
                var normQuery = materialSearch.Normalize(query);
                var tokens = query.Trim()
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(materialSearch.Normalize)
                    .Where(t => t.Length > 0)
                    .ToArray();

                if (tokens.Length > 1)
                {
                    // Multi-token fuzzy matching
                    var matched = new List<MaterialRow>();
                    foreach (var entry in materialIndex)
                    {
                        bool allMatch = true;
                        foreach (var token in tokens)
                        {
                            if (!entry.CodigoKey.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                                !entry.DescripcionKey.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                                !entry.SearchKey.Contains(token, StringComparison.OrdinalIgnoreCase))
                            {
                                allMatch = false;
                                break;
                            }
                        }
                        if (allMatch)
                            matched.Add(entry.Material);
                    }
                    return matched;
                }

                return materialSearch.FindMatches(query, session.Materiales, materialIndex, limit: 20);
            }, ct);

            // Solo consultar el Banco de Precios si no existe ningún resultado en el proyecto/cubicación
            var results = new List<MaterialRow>(projectResults);
            bool bankFailed = false;
            if (!ct.IsCancellationRequested && results.Count == 0)
            {
                try
                {
                    var bankMatches = await Task.Run(() => priceBankStore.Search(query), ct);
                    foreach (var entry in bankMatches)
                    {
                        if (results.Count >= 15) break;
                        if (results.Any(m => CodeHelper.Match(m.Codigo, entry.Codigo))) continue;
                        results.Add(CubicacionViewModel.FromBankEntry(entry));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    bankFailed = true;
                }
            }

            // Ordenamiento por prioridad:
            // 1. Coincidencia exacta de código
            // 2. Materiales ya utilizados en el proyecto activo (usados en cubicación)
            // 3. Catálogo del proyecto
            // 4. Banco de precios
            var normQ = materialSearch.Normalize(query);
            var sortedResults = results
                .OrderBy(m =>
                {
                    var normCode = materialSearch.Normalize(m.Codigo);
                    if (string.Equals(normCode, normQ, StringComparison.OrdinalIgnoreCase)) return 0;
                    if (usedCodes.Contains(m.Codigo)) return 1;
                    return 2;
                })
                .Take(12)
                .ToList();

            if (!ct.IsCancellationRequested)
            {
                var trimmed = query.Trim();
                parent.AutocompleteQueryText = trimmed;
                var inCatalog = sortedResults.Any(m =>
                    string.Equals(m.Codigo, trimmed, StringComparison.OrdinalIgnoreCase));

                parent.AutocompleteResults = new ObservableCollection<AutocompleteSuggestionVm>(
                    sortedResults.Select(m => new AutocompleteSuggestionVm(m, GetMatchReason(trimmed, m, usedCodes))));

                bool showCreate = !inCatalog && trimmed.Length >= 2;
                parent.AutocompleteVisible = sortedResults.Count > 0 || showCreate;
                bool exactCodeMatch = sortedResults.Count == 1 &&
                    string.Equals(sortedResults[0].Codigo, trimmed, StringComparison.OrdinalIgnoreCase);

                parent.AutocompleteStatusText = bankFailed
                    ? "No se pudo consultar el banco de precios."
                    : sortedResults.Count > 0
                    ? exactCodeMatch
                        ? "Coincidencia exacta · Enter aplica · Tab confirma y sigue"
                        : $"{sortedResults.Count:N0} sugerencia(s) · Enter aplica · Tab confirma y sigue"
                    : showCreate
                        ? "Sin coincidencias. Puedes crear un material nuevo con este código."
                        : "Sin coincidencias.";
                if (bankFailed || sortedResults.Count == 0)
                    ShellActivity.Report(parent.AutocompleteStatusText);
                else if (ShellActivity.Text.StartsWith("Sin coincidencias", StringComparison.Ordinal)
                    || ShellActivity.Text.StartsWith("No se pudo consultar", StringComparison.Ordinal))
                    ShellActivity.Report("");

                if (showCreate)
                    parent.QuickAddCodigoDraft = trimmed;
                else
                    parent.QuickAddCodigoDraft = null;

                parent.NotifyAutocompleteFirstItemChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void HideAutocomplete()
    {
        parent.AutocompleteVisible = false;
        autocompleteCts?.Cancel();
    }

    public async Task NotifyCodigoChangedAsync(CubicacionRowVm row, string query)
    {
        var trimmed = query.Trim();
        // Antes escaneaba TODO session.Materiales (.Any) en cada tecla mientras se escribe un
        // código — con un catálogo grande esto se sentía lento tecleando. El índice cacheado
        // de validación (Dictionary) ya existe para este mismo propósito; reusarlo aquí lo deja
        // en O(1) por tecla en vez de O(materiales).
        row.HasError = !string.IsNullOrWhiteSpace(row.Codigo) && !parent.GetValidationMaterialesIndex().ContainsKey(row.Codigo);
        row.ErrorMessage = row.HasError ? $"Código '{row.Codigo}' no existe en el catálogo." : "";

        await SearchMaterialsAsync(query);
    }

    private string GetMatchReason(string query, MaterialRow material, IReadOnlySet<string>? usedCodes = null)
    {
        string q = materialSearch.Normalize(query);
        if (string.IsNullOrWhiteSpace(q))
            return usedCodes != null && usedCodes.Contains(material.Codigo) ? "En proyecto" : "Sugerido";

        string code = materialSearch.Normalize(material.Codigo);
        string desc = materialSearch.Normalize(material.Descripcion);

        if (code.Equals(q, StringComparison.OrdinalIgnoreCase))
            return "Código exacto";
        if (usedCodes != null && usedCodes.Contains(material.Codigo))
            return "En proyecto";
        if (code.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return "Código";
        if (desc.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return "Descripción";
        if (code.Contains(q, StringComparison.OrdinalIgnoreCase))
            return "Código parcial";
        if (desc.Contains(q, StringComparison.OrdinalIgnoreCase))
            return "Descripción parcial";
        return "Catálogo";
    }
}
