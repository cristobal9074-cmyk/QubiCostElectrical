using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica de búsqueda movida a CubicacionAutocompleteService (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que la UI ya conoce, sin cambiar ninguna firma.
    private ObservableCollection<AutocompleteSuggestionVm> autocompleteResults = new();
    private bool autocompleteVisible;
    private string autocompleteStatusText = "Escribe código o descripción para buscar materiales.";
    private string autocompleteQueryText = "";

    public ObservableCollection<AutocompleteSuggestionVm> AutocompleteResults
    {
        get => autocompleteResults;
        internal set => SetProperty(ref autocompleteResults, value);
    }

    public bool AutocompleteVisible
    {
        get => autocompleteVisible;
        set
        {
            if (SetProperty(ref autocompleteVisible, value))
            {
                if (!value)
                {
                    autocompleteService.CancelPendingSearch();
                }
            }
        }
    }

    public AutocompleteSuggestionVm? AutocompleteFirstItem => autocompleteResults.Count > 0 ? autocompleteResults[0] : null;

    // Tab/Enter sin selección explícita solo deben autoaplicar cuando la primera
    // sugerencia es una coincidencia exacta del código escrito/pegado. Aplicar la
    // mejor coincidencia difusa "a ciegas" reemplazaba en silencio un código mal
    // pegado por un material no relacionado, borrando el marcado de error.
    public bool AutocompleteFirstItemIsExactMatch =>
        AutocompleteFirstItem != null &&
        string.Equals(AutocompleteFirstItem.Codigo, AutocompleteQueryText, StringComparison.OrdinalIgnoreCase);

    public string AutocompleteStatusText
    {
        get => autocompleteStatusText;
        internal set => SetProperty(ref autocompleteStatusText, value);
    }

    public string AutocompleteQueryText
    {
        get => autocompleteQueryText;
        internal set => SetProperty(ref autocompleteQueryText, value);
    }

    public Task SearchMaterialsAsync(string query) => autocompleteService.SearchMaterialsAsync(query);

    public void HideAutocomplete() => autocompleteService.HideAutocomplete();

    public Task NotifyCodigoChangedAsync(CubicacionRowVm row, string query) => autocompleteService.NotifyCodigoChangedAsync(row, query);

    internal void NotifyAutocompleteFirstItemChanged() => OnPropertyChanged(nameof(AutocompleteFirstItem));

    internal void InvalidateAutocompleteMaterialIndex() => autocompleteService.InvalidateMaterialIndex();
}
