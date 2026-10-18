using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

/// <summary>
/// Coordina la búsqueda predictiva IntelliSense y la aplicación de sugerencias
/// de Banco de Precios / Catálogo en la grilla de APU.
/// </summary>
public sealed class ApuAutocompleteCoordinator : ViewModelBase
{
    private readonly ApuAutocompleteEngine autocompleteEngine;
    private ObservableCollection<ApuAutocompleteSuggestionVm> results = new();
    private bool visible;
    private ApuAutocompleteSuggestionVm? selectedItem;
    private string statusText = "";
    private CancellationTokenSource? cts;

    public ApuAutocompleteCoordinator(ApuAutocompleteEngine autocompleteEngine)
    {
        this.autocompleteEngine = autocompleteEngine;
    }

    public ObservableCollection<ApuAutocompleteSuggestionVm> Results
    {
        get => results;
        set => SetProperty(ref results, value);
    }

    public bool Visible
    {
        get => visible;
        set
        {
            if (SetProperty(ref visible, value) && !value)
            {
                cts?.Cancel();
            }
        }
    }

    public ApuAutocompleteSuggestionVm? SelectedItem
    {
        get => selectedItem;
        set => SetProperty(ref selectedItem, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public ApuAutocompleteSuggestionVm? FirstItem =>
        Results.Count > 0 ? Results[0] : null;

    public async Task SearchAsync(string query)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        var ct = cts.Token;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            Visible = false;
            Results = new ObservableCollection<ApuAutocompleteSuggestionVm>();
            SelectedItem = null;
            return;
        }

        try
        {
            var searchResults = await autocompleteEngine.SearchAsync(query, ct);
            if (!ct.IsCancellationRequested)
            {
                Results = new ObservableCollection<ApuAutocompleteSuggestionVm>(searchResults);
                SelectedItem = FirstItem;
                Visible = searchResults.Count > 0;
                StatusText = searchResults.Count > 0
                    ? $"{searchResults.Count} sugerencias · ↑↓ navegar · Enter o Tab insertar"
                    : "Sin coincidencias";
                OnPropertyChanged(nameof(FirstItem));
            }
        }
        catch (OperationCanceledException) { }
    }

    public void ApplySuggestion(
        ApuRecursoVm row,
        ApuAutocompleteSuggestionVm suggestion,
        BudgetProjectSession session,
        WpfUndoService undo,
        Action onRowEdited,
        bool canEdit)
    {
        if (row == null || suggestion == null || !canEdit) return;
        undo.PushUndoPoint(session, $"Aplicar insumo {suggestion.Codigo}");

        row.CodigoMaterial = suggestion.Codigo;
        row.Descripcion = suggestion.Descripcion;
        row.Unidad = string.IsNullOrWhiteSpace(suggestion.Unidad) ? "un" : suggestion.Unidad;
        // El motor espera el precio del recurso en CLP (ver nota en MuestraUf); la sugerencia
        // trae MatClp ya resuelto desde el catálogo/banco.
        row.PrecioUf = suggestion.MatClp;
        row.Tipo = ApuTipoRecurso.MAT;
        row.FuentePrecio = suggestion.SourceBadge;
        if (!string.IsNullOrWhiteSpace(suggestion.Proveedor))
            row.Nota = suggestion.Proveedor;
        if (row.Rendimiento <= 0m)
            row.Rendimiento = 1m;

        row.RefreshCalculated();
        onRowEdited();
    }
}
