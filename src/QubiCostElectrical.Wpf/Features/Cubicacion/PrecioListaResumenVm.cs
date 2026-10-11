#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PrecioListaResumenVm : ViewModelBase
{
    private readonly BudgetProjectSession session;

    private readonly IPriceBankStore priceBankStore;

    private readonly ObservableCollection<PrecioListaMaterialVm> allItems = new ObservableCollection<PrecioListaMaterialVm>();

    private readonly ICollectionView filteredView;

    private readonly List<PriceBankEntry> allBankEntries;

    private string searchText = "";

    private string activeFilter = "Todos";

    private bool autocompleteVisible;

    public ObservableCollection<FilterChipVm> Filters { get; } = new ObservableCollection<FilterChipVm>();

    public string ActiveFilter
    {
        get => activeFilter;
        set
        {
            if (SetProperty(ref activeFilter, value, nameof(ActiveFilter)))
            {
                filteredView?.Refresh();
                RefreshKpis();
            }
        }
    }

    public int TotalCount => allItems.Count;
    public int ConPrecioCount => allItems.Count(x => !x.SinPrecio);
    public int SinPrecioCount => allItems.Count(x => x.SinPrecio);

    public ObservableCollection<PriceBankSuggestionVm> AutocompleteResults { get; } = new ObservableCollection<PriceBankSuggestionVm>();

    public bool AutocompleteVisible
    {
        get
        {
            return autocompleteVisible;
        }
        set
        {
            SetProperty(ref autocompleteVisible, value, "AutocompleteVisible");
        }
    }

    public ICollectionView Items => filteredView;

    public string SearchText
    {
        get
        {
            return searchText;
        }
        set
        {
            if (SetProperty(ref searchText, value, "SearchText"))
            {
                filteredView?.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }
    }

    public int FilteredCount => ((IEnumerable)filteredView)?.Cast<object>().Count() ?? 0;

    public void SearchBank(string query)
    {
        AutocompleteResults.Clear();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return;
        }
        string value = query.Trim().ToLowerInvariant();
        foreach (PriceBankEntry allBankEntry in allBankEntries)
        {
            string text = (allBankEntry.Codigo ?? "").ToLowerInvariant();
            string text2 = (allBankEntry.Descripcion ?? "").ToLowerInvariant();
            if (text.Contains(value) || text2.Contains(value))
            {
                AutocompleteResults.Add(new PriceBankSuggestionVm(allBankEntry));
            }
        }
        AutocompleteVisible = AutocompleteResults.Count > 0;
    }

    public void ApplySuggestion(PrecioListaMaterialVm target, PriceBankSuggestionVm suggestion)
    {
        target.Codigo = suggestion.Codigo;
        target.Descripcion = suggestion.Descripcion;
        target.Unidad = suggestion.Unidad;
        target.MatClp = suggestion.MatClp;
        target.MoClp = suggestion.MoClp;
        target.FechaPrecio = DateTime.Today;
        ActualizarPrecioEnSesion(target);
    }

    public PrecioListaResumenVm(BudgetProjectSession session, IPriceBankStore priceBankStore, string autoFilterCodigo = "")
    {
        this.session = session;
        this.priceBankStore = priceBankStore;
        allBankEntries = priceBankStore.GetAll().ToList();
        foreach (PriceBankEntry item in allBankEntries.OrderBy((PriceBankEntry x) => x.Codigo))
        {
            allItems.Add(new PrecioListaMaterialVm(item, session));
        }
        filteredView = CollectionViewSource.GetDefaultView(allItems);
        filteredView.Filter = FilterPredicate;
        RefreshFilterChips();
        if (!string.IsNullOrWhiteSpace(autoFilterCodigo))
        {
            searchText = autoFilterCodigo;
            OnPropertyChanged("SearchText");
            filteredView.Refresh();
        }
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not PrecioListaMaterialVm item)
        {
            return false;
        }

        if (activeFilter == "Con precio" && item.SinPrecio)
        {
            return false;
        }

        if (activeFilter == "Sin precio" && !item.SinPrecio)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string value = searchText.Trim().ToLowerInvariant();
            if (!item.Codigo.ToLowerInvariant().Contains(value) &&
                !item.Descripcion.ToLowerInvariant().Contains(value) &&
                !(item.Proveedor ?? "").ToLowerInvariant().Contains(value))
            {
                return false;
            }
        }
        return true;
    }

    public void ActualizarPrecioEnSesion(PrecioListaMaterialVm item)
    {
        item.SyncToBank(priceBankStore, session.ProjectInfo.ValorUfClp);
        session.SyncCubicacionPricesFromMateriales();
        session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
        RefreshKpis();
    }

    public PriceBankEntry? LookupByCodigo(string codigo)
    {
        return priceBankStore.GetByCodigo(codigo.Trim());
    }

    public (decimal MatClp, decimal MoClp)? LookupPricesByDescripcion(string descripcion)
    {
        PriceBankEntry? priceBankEntry = priceBankStore.GetAll().FirstOrDefault((PriceBankEntry e) => string.Equals(e.Descripcion, descripcion.Trim(), StringComparison.OrdinalIgnoreCase));
        if (priceBankEntry == null)
        {
            return null;
        }
        return (priceBankEntry.MatClp, priceBankEntry.MoClp);
    }

    public void AgregarMaterial(string codigo, string descripcion, string unidad, string categoria, decimal matClp, decimal moClp, string proveedor, DateTime fechaPrecio)
    {
        if (priceBankStore.GetByCodigo(codigo) != null)
        {
            WpfDialogService.Info("Código duplicado", "Ya existe un material con el código '" + codigo + "' en el banco de precios.");
            return;
        }
        decimal valorUfClp = session.ProjectInfo.ValorUfClp;
        PriceBankEntry priceBankEntry = new PriceBankEntry
        {
            Codigo = codigo,
            Descripcion = descripcion,
            Unidad = (string.IsNullOrWhiteSpace(unidad) ? "un" : unidad),
            Categoria = (categoria ?? ""),
            MatClp = matClp,
            MoClp = moClp,
            Proveedor = (proveedor ?? ""),
            FechaPrecio = ((fechaPrecio == default(DateTime)) ? DateTime.Today : fechaPrecio),
            OrigenPrecio = "Lista de precios",
            ActualizadoEn = AppClock.Now
        };
        priceBankEntry.RecalcularUf(valorUfClp);
        priceBankStore.Upsert(priceBankEntry);
        MaterialRow? materialRow = session.Materiales.FirstOrDefault((MaterialRow x) => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (materialRow == null)
        {
            materialRow = new MaterialRow
            {
                Codigo = codigo,
                Descripcion = descripcion,
                Unidad = priceBankEntry.Unidad,
                Categoria = priceBankEntry.Categoria,
                MatClp = matClp,
                MoClp = moClp,
                Proveedor = (proveedor ?? ""),
                FechaPrecio = priceBankEntry.FechaPrecio,
                OrigenPrecio = "Lista de precios",
                UltimaActualizacionPrecio = AppClock.Now
            };
            materialRow.RecalcularUf(valorUfClp);
            session.Materiales.Add(materialRow);
        }
        session.MarkDirty("material_add");
        allItems.Add(new PrecioListaMaterialVm(priceBankEntry, session));
        RefreshKpis();
        ICollectionView? obj = filteredView;
        if (obj != null)
        {
            obj.Refresh();
        }
    }

    public void EliminarMaterial(PrecioListaMaterialVm vm)
    {
        priceBankStore.Delete(vm.Codigo);
        MaterialRow? materialRow = session.Materiales.FirstOrDefault((MaterialRow x) => string.Equals(x.Codigo, vm.Codigo, StringComparison.OrdinalIgnoreCase));
        if (materialRow != null)
        {
            session.Materiales.Remove(materialRow);
            session.MarkDirty("material_delete");
        }
        allItems.Remove(vm);
        RefreshKpis();
        ICollectionView? obj = filteredView;
        if (obj != null)
        {
            obj.Refresh();
        }
    }

    private void RefreshKpis()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ConPrecioCount));
        OnPropertyChanged(nameof(SinPrecioCount));
        OnPropertyChanged(nameof(FilteredCount));
        RefreshFilterChips();
    }

    private void RefreshFilterChips()
    {
        Filters.Clear();
        Filters.Add(new FilterChipVm { Nombre = "Todos", Count = TotalCount, IsActive = activeFilter == "Todos" });
        Filters.Add(new FilterChipVm { Nombre = "Con precio", Count = ConPrecioCount, IsActive = activeFilter == "Con precio" });
        Filters.Add(new FilterChipVm { Nombre = "Sin precio", Count = SinPrecioCount, IsActive = activeFilter == "Sin precio" });
    }
}
