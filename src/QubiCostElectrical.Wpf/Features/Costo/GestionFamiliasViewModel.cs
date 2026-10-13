#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Origen de una familia en la gestión.
/// </summary>
public enum FamiliaOrigenFiltro
{
    Todas = 0,
    Creadas = 1,      // Creadas / personalizadas por el usuario
    ListaPrecios = 2, // Vienen de la lista de precios / banco de precios
    Estandar = 3      // Estándar del catálogo del sistema
}

/// <summary>
/// Fila de familia en la ventana de administración de familias.
/// </summary>
public sealed class GestionFamiliaItemVm : ViewModelBase
{
    private string nombre;
    private bool enEdicion;
    private string nombreEditado;

    public GestionFamiliaItemVm(
        string nombre,
        string claveCanonica,
        bool esEstandar,
        bool esPersonalizada,
        bool vieneDeListaPrecios,
        int materialesCount,
        string prefijoSugerido,
        string factorMarkupEfectivoLabel)
    {
        this.nombre = nombre;
        this.nombreEditado = nombre;
        ClaveCanonica = claveCanonica;
        EsEstandar = esEstandar;
        EsPersonalizada = esPersonalizada;
        VieneDeListaPrecios = vieneDeListaPrecios;
        MaterialesCount = materialesCount;
        PrefijoSugerido = prefijoSugerido;
        FactorMarkupEfectivoLabel = factorMarkupEfectivoLabel;
    }

    public string Nombre
    {
        get => nombre;
        set => SetProperty(ref nombre, value);
    }

    public string ClaveCanonica { get; set; }

    public bool EsEstandar { get; }

    public bool EsPersonalizada { get; }

    public bool VieneDeListaPrecios { get; }

    public string TipoLabel
    {
        get
        {
            if (EsPersonalizada) return "Creada";
            if (VieneDeListaPrecios) return "Lista de precios";
            if (EsEstandar) return "Estándar";
            return "Proyecto";
        }
    }

    public int MaterialesCount { get; }

    public bool EnUso => MaterialesCount > 0;

    public string MaterialesCountLabel => MaterialesCount switch
    {
        0 => "Sin uso",
        1 => "1 material",
        _ => $"{MaterialesCount} materiales"
    };

    public string PrefijoSugerido { get; }

    public string FactorMarkupEfectivoLabel { get; }

    public bool EnEdicion
    {
        get => enEdicion;
        set => SetProperty(ref enEdicion, value);
    }

    public string NombreEditado
    {
        get => nombreEditado;
        set => SetProperty(ref nombreEditado, value);
    }
}

/// <summary>
/// ViewModel para el diálogo de gestión (CRUD) de familias eléctricas del proyecto.
/// </summary>
public sealed class GestionFamiliasViewModel : ViewModelBase
{
    private readonly BudgetProjectSession session;
    private readonly IPriceBankStore? priceBankStore;
    private readonly Action onCatalogModified;

    private string searchText = "";
    private string nuevaFamiliaNombre = "";
    private string statusMessage = "";
    private FamiliaOrigenFiltro filtroOrigen = FamiliaOrigenFiltro.Todas;

    public ObservableCollection<GestionFamiliaItemVm> Familias { get; } = new();

    public ICollectionView FilteredFamilias { get; }

    public FamiliaOrigenFiltro FiltroOrigen
    {
        get => filtroOrigen;
        set
        {
            if (SetProperty(ref filtroOrigen, value))
            {
                FilteredFamilias.Refresh();
                OnPropertyChanged(nameof(FiltroTodasSelected));
                OnPropertyChanged(nameof(FiltroCreadasSelected));
                OnPropertyChanged(nameof(FiltroListaPreciosSelected));
                OnPropertyChanged(nameof(FiltroEstandarSelected));
            }
        }
    }

    public bool FiltroTodasSelected
    {
        get => filtroOrigen == FamiliaOrigenFiltro.Todas;
        set { if (value) FiltroOrigen = FamiliaOrigenFiltro.Todas; }
    }

    public bool FiltroCreadasSelected
    {
        get => filtroOrigen == FamiliaOrigenFiltro.Creadas;
        set { if (value) FiltroOrigen = FamiliaOrigenFiltro.Creadas; }
    }

    public bool FiltroListaPreciosSelected
    {
        get => filtroOrigen == FamiliaOrigenFiltro.ListaPrecios;
        set { if (value) FiltroOrigen = FamiliaOrigenFiltro.ListaPrecios; }
    }

    public bool FiltroEstandarSelected
    {
        get => filtroOrigen == FamiliaOrigenFiltro.Estandar;
        set { if (value) FiltroOrigen = FamiliaOrigenFiltro.Estandar; }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
                FilteredFamilias.Refresh();
        }
    }

    public string NuevaFamiliaNombre
    {
        get => nuevaFamiliaNombre;
        set => SetProperty(ref nuevaFamiliaNombre, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public int TotalFamilias => Familias.Count;

    public int FamiliasEnUsoCount => Familias.Count(f => f.EnUso);

    public int FamiliasCreadasCount => Familias.Count(f => f.EsPersonalizada);

    public int FamiliasListaPreciosCount => Familias.Count(f => f.VieneDeListaPrecios);

    public int FamiliasOcultasCount => session.ProjectInfo.FamiliasOcultas?.Count ?? 0;

    public bool HayFamiliasOcultas => FamiliasOcultasCount > 0;

    public ICommand CrearFamiliaCommand { get; }
    public ICommand IniciarEdicionCommand { get; }
    public ICommand GuardarEdicionCommand { get; }
    public ICommand CancelarEdicionCommand { get; }
    public ICommand EliminarFamiliaCommand { get; }
    public ICommand RestaurarEstandarCommand { get; }

    private readonly Func<string, string, bool, bool> confirmFn;
    private readonly Action<string, string> infoFn;

    public GestionFamiliasViewModel(
        BudgetProjectSession session,
        IPriceBankStore? priceBankStore,
        Action onCatalogModified,
        Func<string, string, bool, bool>? confirmFn = null,
        Action<string, string>? infoFn = null)
    {
        this.session = session;
        this.priceBankStore = priceBankStore;
        this.onCatalogModified = onCatalogModified;
        this.confirmFn = confirmFn ?? ((t, m, w) => WpfDialogService.Confirm(t, m, w));
        this.infoFn = infoFn ?? ((t, m) => WpfDialogService.Info(t, m));

        FilteredFamilias = CollectionViewSource.GetDefaultView(Familias);
        FilteredFamilias.Filter = FilterPredicate;

        CrearFamiliaCommand = new RelayCommand(CrearFamilia);
        IniciarEdicionCommand = new RelayCommand<GestionFamiliaItemVm>(IniciarEdicion);
        GuardarEdicionCommand = new RelayCommand<GestionFamiliaItemVm>(GuardarEdicion);
        CancelarEdicionCommand = new RelayCommand<GestionFamiliaItemVm>(CancelarEdicion);
        EliminarFamiliaCommand = new RelayCommand<GestionFamiliaItemVm>(EliminarFamilia);
        RestaurarEstandarCommand = new RelayCommand(RestaurarEstandar);

        CargarFamilias();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not GestionFamiliaItemVm item)
            return false;

        // Filtro por origen
        switch (filtroOrigen)
        {
            case FamiliaOrigenFiltro.Creadas when !item.EsPersonalizada:
                return false;
            case FamiliaOrigenFiltro.ListaPrecios when !item.VieneDeListaPrecios:
                return false;
            case FamiliaOrigenFiltro.Estandar when !item.EsEstandar:
                return false;
        }

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string query = searchText.Trim();
        return item.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.PrefijoSugerido.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.TipoLabel.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public void CargarFamilias()
    {
        Familias.Clear();

        // 1. Contar materiales en el proyecto por clave canónica de familia
        var materiales = session.Materiales;
        var conteos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in materiales)
        {
            if (string.IsNullOrWhiteSpace(m.Categoria))
                continue;
            string c = ElectricalFamilyCatalog.ClaveCanonica(m.Categoria);
            if (!string.IsNullOrEmpty(c))
                conteos[c] = conteos.TryGetValue(c, out int current) ? current + 1 : 1;
        }

        // 2. Obtener lista combinada de familias
        var bankCategories = (priceBankStore?.GetAll().Select(e => e.Categoria) ?? Enumerable.Empty<string>()).ToList();

        var bankKeysSet = new HashSet<string>(
            bankCategories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(ElectricalFamilyCatalog.ClaveCanonica),
            StringComparer.OrdinalIgnoreCase);

        var personalizadasSet = new HashSet<string>(
            (session.ProjectInfo.FamiliasPersonalizadas ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(ElectricalFamilyCatalog.ClaveCanonica),
            StringComparer.OrdinalIgnoreCase);

        var nombres = ElectricalFamilyCatalog.CombinarFamiliasConOcultas(
            session.ProjectInfo.FamiliasOcultas,
            session.ProjectInfo.FamiliasPersonalizadas ?? Enumerable.Empty<string>(),
            bankCategories);

        var estandarSet = new HashSet<string>(
            ElectricalFamilyCatalog.FamiliasEstandar.Select(ElectricalFamilyCatalog.ClaveCanonica),
            StringComparer.OrdinalIgnoreCase);

        foreach (var nombre in nombres)
        {
            string clave = ElectricalFamilyCatalog.ClaveCanonica(nombre);
            bool esEstandar = estandarSet.Contains(clave);
            bool esPersonalizada = personalizadasSet.Contains(clave);
            bool vieneDeListaPrecios = bankKeysSet.Contains(clave);

            int count = conteos.TryGetValue(clave, out int qty) ? qty : 0;
            string prefijo = MaterialCodeGenerator.Prefijo(nombre, "");
            decimal factor = session.GetFamilyPresentationFactor(nombre);
            string factorLabel = factor.ToString("N2", CultureInfo.CurrentCulture) + "×";

            Familias.Add(new GestionFamiliaItemVm(
                nombre,
                clave,
                esEstandar,
                esPersonalizada,
                vieneDeListaPrecios,
                count,
                prefijo,
                factorLabel));
        }

        FilteredFamilias.Refresh();
        OnPropertyChanged(nameof(TotalFamilias));
        OnPropertyChanged(nameof(FamiliasEnUsoCount));
        OnPropertyChanged(nameof(FamiliasCreadasCount));
        OnPropertyChanged(nameof(FamiliasListaPreciosCount));
        OnPropertyChanged(nameof(FamiliasOcultasCount));
        OnPropertyChanged(nameof(HayFamiliasOcultas));
    }

    private void CrearFamilia()
    {
        string nombre = (NuevaFamiliaNombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            StatusMessage = "Escribe el nombre de la familia para crearla.";
            return;
        }

        string clave = ElectricalFamilyCatalog.ClaveCanonica(nombre);
        if (string.IsNullOrEmpty(clave))
        {
            StatusMessage = "El nombre de la familia no es válido.";
            return;
        }

        // Verificar si ya existe en las visibles
        var existente = Familias.FirstOrDefault(f => string.Equals(f.ClaveCanonica, clave, StringComparison.OrdinalIgnoreCase));
        if (existente != null)
        {
            infoFn("Familia existente", $"Ya existe una familia activa con ese nombre o sinónimo («{existente.Nombre}»).");
            return;
        }

        // Si estaba oculta, desocultarla
        session.ProjectInfo.FamiliasOcultas ??= new List<string>();
        session.ProjectInfo.FamiliasOcultas.RemoveAll(o =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(o), clave, StringComparison.OrdinalIgnoreCase));

        // Registrar en personalizadas
        session.ProjectInfo.FamiliasPersonalizadas ??= new List<string>();
        if (!session.ProjectInfo.FamiliasPersonalizadas.Any(p =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(p), clave, StringComparison.OrdinalIgnoreCase)))
        {
            session.ProjectInfo.FamiliasPersonalizadas.Add(nombre);
        }

        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        NuevaFamiliaNombre = "";
        StatusMessage = $"Familia «{nombre}» creada exitosamente.";
        CargarFamilias();
        onCatalogModified();
    }

    private void IniciarEdicion(GestionFamiliaItemVm? item)
    {
        if (item == null)
            return;

        foreach (var f in Familias)
        {
            if (f.EnEdicion && !ReferenceEquals(f, item))
                f.EnEdicion = false;
        }

        item.NombreEditado = item.Nombre;
        item.EnEdicion = true;
    }

    private void CancelarEdicion(GestionFamiliaItemVm? item)
    {
        if (item == null)
            return;

        item.NombreEditado = item.Nombre;
        item.EnEdicion = false;
    }

    private void GuardarEdicion(GestionFamiliaItemVm? item)
    {
        if (item == null)
            return;

        string nuevoNombre = (item.NombreEditado ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nuevoNombre))
        {
            infoFn("Nombre requerido", "El nombre de la familia no puede estar vacío.");
            return;
        }

        if (string.Equals(nuevoNombre, item.Nombre, StringComparison.Ordinal))
        {
            item.EnEdicion = false;
            return;
        }

        string nuevaClave = ElectricalFamilyCatalog.ClaveCanonica(nuevoNombre);
        var conflicto = Familias.FirstOrDefault(f =>
            !ReferenceEquals(f, item) && string.Equals(f.ClaveCanonica, nuevaClave, StringComparison.OrdinalIgnoreCase));
        if (conflicto != null)
        {
            infoFn("Nombre duplicado", $"Ya existe otra familia con el nombre «{conflicto.Nombre}».");
            return;
        }

        string claveAnterior = item.ClaveCanonica;

        // Cascada en materiales del proyecto
        int matsActualizados = 0;
        foreach (var mat in session.Materiales)
        {
            if (string.Equals(ElectricalFamilyCatalog.ClaveCanonica(mat.Categoria), claveAnterior, StringComparison.OrdinalIgnoreCase))
            {
                mat.Categoria = nuevoNombre;
                matsActualizados++;
            }
        }

        // Cascada en cubicación
        foreach (var cub in session.Data.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            if (string.Equals(ElectricalFamilyCatalog.ClaveCanonica(cub.Categoria), claveAnterior, StringComparison.OrdinalIgnoreCase))
            {
                cub.Categoria = nuevoNombre;
            }
        }

        // Actualizar lista de personalizadas
        session.ProjectInfo.FamiliasPersonalizadas ??= new List<string>();
        session.ProjectInfo.FamiliasPersonalizadas.RemoveAll(p =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(p), claveAnterior, StringComparison.OrdinalIgnoreCase));
        session.ProjectInfo.FamiliasPersonalizadas.Add(nuevoNombre);

        // Migrar asignación en Costos si existía
        if (session.ProjectInfo.FamiliasPrecioAsignadas != null
            && session.ProjectInfo.FamiliasPrecioAsignadas.TryGetValue(claveAnterior, out string? target))
        {
            session.ProjectInfo.FamiliasPrecioAsignadas.Remove(claveAnterior);
            session.ProjectInfo.FamiliasPrecioAsignadas[nuevaClave] = target ?? "";
        }

        session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.ProjectInfo, ProjectDirtyScope.Cubicacion);
        session.SyncCubicacionPricesFromMateriales();
        session.SyncCubicacionGgUtFactorsFromVariables();

        StatusMessage = matsActualizados > 0
            ? $"Familia renombrada a «{nuevoNombre}». Se actualizaron {matsActualizados} materiales."
            : $"Familia renombrada a «{nuevoNombre}».";

        item.EnEdicion = false;
        CargarFamilias();
        onCatalogModified();
    }

    private void EliminarFamilia(GestionFamiliaItemVm? item)
    {
        if (item == null)
            return;

        if (item.MaterialesCount > 0)
        {
            string msg = $"La familia «{item.Nombre}» tiene {item.MaterialesCountLabel} en este presupuesto.\n\n" +
                         "Si la eliminas, esos materiales quedarán desvinculados (categoría «General») y la familia dejará de figurar en el catálogo.\n\n" +
                         "¿Deseas continuar con la eliminación?";
            if (!confirmFn("Eliminar familia con materiales", msg, true))
                return;

            // Desvincular materiales
            foreach (var mat in session.Materiales)
            {
                if (string.Equals(ElectricalFamilyCatalog.ClaveCanonica(mat.Categoria), item.ClaveCanonica, StringComparison.OrdinalIgnoreCase))
                    mat.Categoria = "General";
            }

            foreach (var cub in session.Data.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
            {
                if (string.Equals(ElectricalFamilyCatalog.ClaveCanonica(cub.Categoria), item.ClaveCanonica, StringComparison.OrdinalIgnoreCase))
                    cub.Categoria = "General";
            }

            session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
        }

        // Si es estándar, ocultarla para este proyecto
        if (item.EsEstandar)
        {
            session.ProjectInfo.FamiliasOcultas ??= new List<string>();
            if (!session.ProjectInfo.FamiliasOcultas.Any(o =>
                string.Equals(ElectricalFamilyCatalog.ClaveCanonica(o), item.ClaveCanonica, StringComparison.OrdinalIgnoreCase)))
            {
                session.ProjectInfo.FamiliasOcultas.Add(item.Nombre);
            }
        }

        // Quitar de personalizadas
        session.ProjectInfo.FamiliasPersonalizadas ??= new List<string>();
        session.ProjectInfo.FamiliasPersonalizadas.RemoveAll(p =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(p), item.ClaveCanonica, StringComparison.OrdinalIgnoreCase));

        // Quitar de asignaciones
        session.ProjectInfo.FamiliasPrecioAsignadas?.Remove(item.ClaveCanonica);

        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        session.SyncCubicacionPricesFromMateriales();
        session.SyncCubicacionGgUtFactorsFromVariables();

        StatusMessage = $"Familia «{item.Nombre}» eliminada del catálogo.";
        CargarFamilias();
        onCatalogModified();
    }

    private void RestaurarEstandar()
    {
        if (!HayFamiliasOcultas)
            return;

        string msg = $"Se restaurarán {FamiliasOcultasCount} familias estándar que habían sido dadas de baja en este proyecto.\n\n¿Deseas restaurarlas?";
        if (!confirmFn("Restaurar familias estándar", msg, false))
            return;

        session.ProjectInfo.FamiliasOcultas?.Clear();
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);

        StatusMessage = "Familias estándar restauradas correctamente.";
        CargarFamilias();
        onCatalogModified();
    }
}
