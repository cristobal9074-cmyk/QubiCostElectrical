using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PriceBankSuggestionVm
{
    public PriceBankSuggestionVm(PriceBankEntry entry)
    {
        Source = entry;
        Codigo = entry.Codigo ?? "";
        Descripcion = entry.Descripcion ?? "";
        Unidad = entry.Unidad ?? "";
        MatClp = entry.MatClp;
        MoClp = entry.MoClp;
    }

    public PriceBankEntry Source { get; }
    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public decimal MatClp { get; }
    public decimal MoClp { get; }
    public string MatClpDisplay => MatClp <= 0m ? "—" : MatClp.ToString("N0");
    public string MoClpDisplay => MoClp <= 0m ? "—" : MoClp.ToString("N0");
}

public sealed class PrecioListaMaterialVm : INotifyPropertyChanged
{
    private readonly PriceBankEntry bankEntry;
    private readonly BudgetProjectSession session;
    private string codigo = "";
    private string descripcion = "";
    private string unidad = "";
    private decimal matClp;
    private decimal moClp;

    public PrecioListaMaterialVm(PriceBankEntry entry, BudgetProjectSession session)
    {
        bankEntry = entry;
        this.session = session;
        Codigo = entry.Codigo;
        Descripcion = entry.Descripcion;
        Unidad = entry.Unidad;
        Categoria = entry.Categoria;
        MatClp = entry.MatClp;
        MoClp = entry.MoClp;
        Proveedor = entry.Proveedor;
        FechaPrecio = entry.FechaPrecio;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Codigo
    {
        get => codigo;
        set { codigo = value ?? ""; OnPropertyChanged(nameof(Codigo)); }
    }

    public string Descripcion
    {
        get => descripcion;
        set { descripcion = value ?? ""; OnPropertyChanged(nameof(Descripcion)); }
    }

    public string Unidad
    {
        get => unidad;
        set { unidad = value ?? ""; OnPropertyChanged(nameof(Unidad)); }
    }

    public string Categoria { get; set; } = "";

    public decimal MatClp
    {
        get => matClp;
        set { matClp = value; OnPropertyChanged(nameof(MatClp)); OnPropertyChanged(nameof(SinPrecio)); }
    }

    public decimal MoClp
    {
        get => moClp;
        set { moClp = value; OnPropertyChanged(nameof(MoClp)); }
    }

    public string Proveedor { get; set; } = "";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;

    public bool SinPrecio => MatClp == 0m && MoClp == 0m;
    public int AntiguedadDias => FechaPrecio == default ? 0 : Math.Max(0, (DateTime.Today - FechaPrecio.Date).Days);
    public string VigenciaLabel => AntiguedadDias <= 90 ? "Vigente" : (AntiguedadDias <= 180 ? "Revisar" : "Vencido");
    public string VigenciaColor => AntiguedadDias <= 90 ? "Success" : (AntiguedadDias <= 180 ? "Amber" : "Danger");

    public void SyncToBank(IPriceBankStore store, decimal valorUfClp)
    {
        bankEntry.Codigo = Codigo;
        bankEntry.Descripcion = Descripcion;
        bankEntry.Unidad = Unidad;
        bankEntry.MatClp = MatClp;
        bankEntry.MoClp = MoClp;
        bankEntry.FechaPrecio = DateTime.Today;
        bankEntry.Proveedor = Proveedor;
        bankEntry.RecalcularUf(valorUfClp);
        store.Upsert(bankEntry);

        var projectRow = session.Materiales.FirstOrDefault(x =>
            string.Equals(x.Codigo, Codigo, StringComparison.OrdinalIgnoreCase));
        if (projectRow != null)
        {
            projectRow.Codigo = Codigo;
            projectRow.Descripcion = Descripcion;
            projectRow.Unidad = Unidad;
            projectRow.MatClp = MatClp;
            projectRow.MoClp = MoClp;
            projectRow.Proveedor = Proveedor;
            projectRow.FechaPrecio = DateTime.Today;
            projectRow.UltimaActualizacionPrecio = AppClock.Now;
            projectRow.RecalcularUf(valorUfClp);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

public sealed class PrecioListaResumenVm : ViewModelBase
{
    private readonly BudgetProjectSession session;
    private readonly IPriceBankStore priceBankStore;
    private readonly ObservableCollection<PrecioListaMaterialVm> allItems = new();
    private readonly ICollectionView? filteredView;
    private readonly List<PriceBankEntry> allBankEntries;
    private string searchText = "";
    private bool autocompleteVisible;

    public ObservableCollection<PriceBankSuggestionVm> AutocompleteResults { get; } = new();
    public bool AutocompleteVisible { get => autocompleteVisible; set => SetProperty(ref autocompleteVisible, value); }

    public void SearchBank(string query)
    {
        AutocompleteResults.Clear();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return;
        var q = query.Trim().ToLowerInvariant();
        foreach (var e in allBankEntries)
        {
            var codigo = (e.Codigo ?? "").ToLowerInvariant();
            var desc = (e.Descripcion ?? "").ToLowerInvariant();
            if (codigo.Contains(q) || desc.Contains(q))
                AutocompleteResults.Add(new PriceBankSuggestionVm(e));
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

        foreach (var e in allBankEntries.OrderBy(x => x.Codigo))
        {
            allItems.Add(new PrecioListaMaterialVm(e, session));
        }

        filteredView = CollectionViewSource.GetDefaultView(allItems);
        filteredView.Filter = FilterPredicate;

        if (!string.IsNullOrWhiteSpace(autoFilterCodigo))
        {
            searchText = autoFilterCodigo;
            OnPropertyChanged(nameof(SearchText));
            filteredView.Refresh();
        }
    }

    public ICollectionView Items => filteredView!;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
                filteredView?.Refresh();
        }
    }

    public int FilteredCount => filteredView?.Cast<object>().Count() ?? 0;

    private bool FilterPredicate(object obj)
    {
        if (obj is not PrecioListaMaterialVm m) return false;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var q = searchText.Trim().ToLowerInvariant();
            if (!m.Codigo.ToLowerInvariant().Contains(q) &&
                !m.Descripcion.ToLowerInvariant().Contains(q) &&
                !(m.Proveedor ?? "").ToLowerInvariant().Contains(q))
                return false;
        }

        return true;
    }

    public void ActualizarPrecioEnSesion(PrecioListaMaterialVm item)
    {
        item.SyncToBank(priceBankStore, session.ProjectInfo.ValorUfClp);
        session.SyncCubicacionPricesFromMateriales();
        session.MarkDirty("materiales", "cubicacion");
    }

    public PriceBankEntry? LookupByCodigo(string codigo)
    {
        return priceBankStore.GetByCodigo(codigo.Trim());
    }

    public (decimal MatClp, decimal MoClp)? LookupPricesByDescripcion(string descripcion)
    {
        var match = priceBankStore.GetAll().FirstOrDefault(e =>
            string.Equals(e.Descripcion, descripcion.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match == null) return null;
        return (match.MatClp, match.MoClp);
    }

    public void AgregarMaterial(string codigo, string descripcion, string unidad, string categoria,
        decimal matClp, decimal moClp, string proveedor, DateTime fechaPrecio)
    {
        var existingBank = priceBankStore.GetByCodigo(codigo);
        if (existingBank != null)
        {
            WpfDialogService.Info("Código duplicado",
                $"Ya existe un material con el código '{codigo}' en el banco de precios.");
            return;
        }

        var valorUf = session.ProjectInfo.ValorUfClp;
        var entry = new PriceBankEntry
        {
            Codigo = codigo,
            Descripcion = descripcion,
            Unidad = string.IsNullOrWhiteSpace(unidad) ? "un" : unidad,
            Categoria = categoria ?? "",
            MatClp = matClp,
            MoClp = moClp,
            Proveedor = proveedor ?? "",
            FechaPrecio = fechaPrecio == default ? DateTime.Today : fechaPrecio,
            OrigenPrecio = "Lista de precios",
            ActualizadoEn = AppClock.Now
        };
        entry.RecalcularUf(valorUf);
        priceBankStore.Upsert(entry);

        var projectRow = session.Materiales.FirstOrDefault(x =>
            string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (projectRow == null)
        {
            projectRow = new MaterialRow
            {
                Codigo = codigo,
                Descripcion = descripcion,
                Unidad = entry.Unidad,
                Categoria = entry.Categoria,
                MatClp = matClp,
                MoClp = moClp,
                Proveedor = proveedor ?? "",
                FechaPrecio = entry.FechaPrecio,
                OrigenPrecio = "Lista de precios",
                UltimaActualizacionPrecio = AppClock.Now
            };
            projectRow.RecalcularUf(valorUf);
            session.Materiales.Add(projectRow);
        }
        session.MarkDirty("material_add");

        allItems.Add(new PrecioListaMaterialVm(entry, session));
        RefreshKpis();
        filteredView?.Refresh();
    }

    public void EliminarMaterial(PrecioListaMaterialVm vm)
    {
        priceBankStore.Delete(vm.Codigo);

        var projectRow = session.Materiales.FirstOrDefault(x =>
            string.Equals(x.Codigo, vm.Codigo, StringComparison.OrdinalIgnoreCase));
        if (projectRow != null)
        {
            session.Materiales.Remove(projectRow);
            session.MarkDirty("material_delete");
        }

        allItems.Remove(vm);
        RefreshKpis();
        filteredView?.Refresh();
    }

    private void RefreshKpis()
    {
        OnPropertyChanged(nameof(FilteredCount));
    }
}
