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

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            parent.AutocompleteQueryText = query ?? "";
            parent.AutocompleteVisible = false;
            parent.AutocompleteStatusText = "Escribe al menos 2 caracteres para ver sugerencias.";
            return;
        }

        if (materialIndex.Count == 0 && session.Materiales.Count > 0)
            materialIndex = materialSearch.BuildIndex(session.Materiales);

        try
        {
            var projectResults = await Task.Run(() =>
                materialSearch.FindMatches(query, session.Materiales, materialIndex, limit: 12), ct);

            // El Banco de Precios es un catálogo aparte del proyecto: completar con sus
            // coincidencias para que un código conocido del banco también aparezca aquí,
            // aunque todavía no se haya usado en este proyecto.
            var results = new List<MaterialRow>(projectResults);
            if (!ct.IsCancellationRequested && results.Count < 12)
            {
                var bankMatches = await Task.Run(() => priceBankStore.Search(query), ct);
                foreach (var entry in bankMatches)
                {
                    if (results.Count >= 12) break;
                    if (results.Any(m => CodeHelper.Match(m.Codigo, entry.Codigo))) continue;
                    results.Add(CubicacionViewModel.FromBankEntry(entry));
                }
            }

            if (!ct.IsCancellationRequested)
            {
                var trimmed = query.Trim();
                parent.AutocompleteQueryText = trimmed;
                var inCatalog = results.Any(m =>
                    string.Equals(m.Codigo, trimmed, StringComparison.OrdinalIgnoreCase));

                parent.AutocompleteResults = new ObservableCollection<AutocompleteSuggestionVm>(
                    results.Select(m => new AutocompleteSuggestionVm(m, GetMatchReason(trimmed, m))));

                bool showCreate = !inCatalog && trimmed.Length >= 2;
                parent.AutocompleteVisible = results.Count > 0 || showCreate;
                bool exactCodeMatch = results.Count == 1 &&
                    string.Equals(results[0].Codigo, trimmed, StringComparison.OrdinalIgnoreCase);

                parent.AutocompleteStatusText = results.Count > 0
                    ? exactCodeMatch
                        ? "Coincidencia exacta · Enter aplica · Tab confirma y sigue"
                        : $"{results.Count:N0} sugerencia(s) · Enter aplica · Tab confirma y sigue"
                    : showCreate
                        ? "Sin coincidencias. Puedes crear un material nuevo con este código."
                        : "Sin coincidencias.";

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

    private string GetMatchReason(string query, MaterialRow material)
    {
        string q = materialSearch.Normalize(query);
        if (string.IsNullOrWhiteSpace(q))
            return "Sugerido";

        string code = materialSearch.Normalize(material.Codigo);
        string desc = materialSearch.Normalize(material.Descripcion);

        if (code.Equals(q, StringComparison.OrdinalIgnoreCase))
            return "Código exacto";
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
