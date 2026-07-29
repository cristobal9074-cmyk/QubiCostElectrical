using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ResumenItemizadoViewModel : ViewModelBase
{
    private readonly List<ItemizadoPartidaVm> allItems = new();
    private string searchText = "";
    private string activeFilter = "Todas";
    private bool showAll = true;
    private bool onlyCubicacion;
    private bool onlySinCubicar;
    private bool onlyTitulos;

    public ResumenItemizadoViewModel(IReadOnlyList<ItemizadoPartidaVm> items, string? initialId = null)
    {
        allItems.AddRange(items);
        InitialId = initialId;

        Filters = new ObservableCollection<FilterChipVm>
        {
            new() { Nombre = "Todas", IsActive = true, Count = items.Count },
            new() { Nombre = "Con cubicación", IsActive = false, Count = items.Count(i => i.EsSeccion == false && i.TieneCubicacion) },
            new() { Nombre = "Sin cubicar", IsActive = false, Count = items.Count(i => i.EsSeccion == false && !i.TieneCubicacion) },
            new() { Nombre = "Títulos", IsActive = false, Count = items.Count(i => i.EsSeccion) },
        };

        CerrarCommand = new RelayCommand(Cerrar);
        AplicarFiltros();
    }

    public string? InitialId { get; }

    public ObservableCollection<FilterChipVm> Filters { get; }
    public ObservableCollection<ItemizadoPartidaVm> FlatRows { get; } = new();

    public string ActiveFilter
    {
        get => activeFilter;
        set
        {
            if (SetProperty(ref activeFilter, value))
            {
                showAll = string.Equals(value, "Todas", StringComparison.OrdinalIgnoreCase);
                onlyCubicacion = string.Equals(value, "Con cubicación", StringComparison.OrdinalIgnoreCase);
                onlySinCubicar = string.Equals(value, "Sin cubicar", StringComparison.OrdinalIgnoreCase);
                onlyTitulos = string.Equals(value, "Títulos", StringComparison.OrdinalIgnoreCase);
                foreach (var chip in Filters)
                    chip.IsActive = string.Equals(chip.Nombre, value, StringComparison.OrdinalIgnoreCase);
                AplicarFiltros();
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
                AplicarFiltros();
        }
    }

    private ItemizadoPartidaVm? selectedRow;
    public ItemizadoPartidaVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (SetProperty(ref selectedRow, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand CerrarCommand { get; }

    public Action? OnCerrar { get; set; }

    public string SummaryText
    {
        get
        {
            int partidas = allItems.Count(i => !i.EsSeccion);
            int cubicadas = allItems.Count(i => !i.EsSeccion && i.TieneCubicacion);
            decimal totalMat = allItems.Sum(i => i.MaterialTotal);
            decimal totalMo = allItems.Sum(i => i.ManoObraTotal);
            return $"{partidas} partidas · {cubicadas} con cubicación · Total: $ {totalMat + totalMo:N0}";
        }
    }

    private void AplicarFiltros()
    {
        FlatRows.Clear();

        var search = SearchText?.Trim() ?? "";
        bool hasSearch = search.Length > 0;

        foreach (var item in allItems)
        {
            if (!PassesFilter(item)) continue;
            if (hasSearch && !MatchesSearch(item, search)) continue;
            FlatRows.Add(item);
        }

        if (InitialId != null)
            SelectedRow = FlatRows.FirstOrDefault(r =>
                string.Equals(r.Id, InitialId, StringComparison.OrdinalIgnoreCase));
    }

    private bool PassesFilter(ItemizadoPartidaVm item)
    {
        if (showAll) return true;
        if (onlyTitulos) return item.EsSeccion;
        if (item.EsSeccion) return false;
        if (onlyCubicacion) return item.TieneCubicacion;
        if (onlySinCubicar) return !item.TieneCubicacion;
        return true;
    }

    private static bool MatchesSearch(ItemizadoPartidaVm item, string search)
    {
        if (item.Descripcion.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (item.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void Cerrar() => OnCerrar?.Invoke();
}

public sealed class FilterChipVm : ViewModelBase
{
    private bool isActive;
    public string Nombre { get; init; } = "";
    public int Count { get; init; }
    public bool IsActive
    {
        get => isActive;
        set => SetProperty(ref isActive, value);
    }
    public string Display => $"{Nombre} ({Count})";
}
