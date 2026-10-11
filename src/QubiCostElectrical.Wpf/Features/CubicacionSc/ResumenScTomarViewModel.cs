using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ResumenScTomarViewModel : ViewModelBase
{
    private readonly List<MaterialConZonasVm> allMaterials = new();
    private readonly List<GroupHeaderRowVm> allGroups = new();
    private string searchText = "";
    private string activeCategoria = "Todas";
    private readonly IReadOnlyList<ZonaProyecto> zonas;
    private bool showAllCategories = true;
    private FlatRowVm? selectedRow;

    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todas"] = "#64748b",
        ["Tubería"] = "#3b82f6",
        ["Conductor"] = "#8b5cf6",
        ["Cajas"] = "#f59e0b",
        ["Artefactos"] = "#ef4444",
        ["Sensores"] = "#22c55e",
        ["Señalética"] = "#f97316",
        ["Flexible"] = "#06b6d4",
        ["Cinta LED"] = "#14b8a6",
        ["Montaje"] = "#a855f7",
        ["Interruptores"] = "#ec4899"
    };

    public ObservableCollection<CategoriaChipVm> Categorias { get; }
    public ObservableCollection<FlatRowVm> FlatRows { get; } = new();

    public string ActiveCategoria
    {
        get => activeCategoria;
        set
        {
            if (!SetProperty(ref activeCategoria, value))
                return;
            showAllCategories = string.Equals(value, "Todas", StringComparison.OrdinalIgnoreCase);
            foreach (var categoria in Categorias)
                categoria.IsActive = string.Equals(categoria.Nombre, value, StringComparison.OrdinalIgnoreCase);
            AplicarFiltros();
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

    public FlatRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (SetProperty(ref selectedRow, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand TomarCommand { get; }
    public ICommand CerrarCommand { get; }
    public Action<MaterialConZonasVm>? OnTomar { get; set; }
    public Action? OnCerrar { get; set; }
    public int TotalMaterialesCount => allMaterials.Count;
    public int CategoriasCount => allGroups.Count;
    public int ZonasCount => zonas.Count;
    public string MaterialCountText => $"{allMaterials.Count} materiales disponibles";
    public string SidebarFooterText => $"{Categorias.Count} categorías · {allMaterials.Count} materiales";
    public IReadOnlyList<ZonaProyecto> Zonas => zonas;

    public ResumenScTomarViewModel(IReadOnlyList<CuadroCargaSCRow> cuadros, CubicacionScConfig config, IReadOnlyList<ZonaProyecto> zonas)
    {
        this.zonas = zonas;
        Categorias = new ObservableCollection<CategoriaChipVm>
        {
            new() { Nombre = "Todas", IsActive = true, ColorHex = "#64748b" }
        };
        CargarMateriales(cuadros, config);
        TomarCommand = new RelayCommand<MaterialConZonasVm>(Tomar);
        CerrarCommand = new RelayCommand(Cerrar);
    }

    private void CargarMateriales(IReadOnlyList<CuadroCargaSCRow> cuadros, CubicacionScConfig config)
    {
        var grouped = CubicacionSCEngine.AggregateConZonas(cuadros, config, zonas)
            .GroupBy(m => m.Categoria)
            .OrderBy(g => g.Key);

        allMaterials.Clear();
        allGroups.Clear();
        var categoriasSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            categoriasSet.Add(group.Key);
            var list = new List<MaterialConZonasVm>();
            foreach (var item in group)
            {
                var vmRow = new MaterialConZonasVm
                {
                    Nombre = item.Nombre,
                    Unidad = item.Unidad,
                    Categoria = group.Key,
                    Total = item.Total
                };
                foreach (var (zonaNombre, qty) in item.Zonas)
                {
                    vmRow.ZonaQties.Add(new ZonaQtyWrapper
                    {
                        ZonaNombre = zonaNombre,
                        Text = qty.ToString("0.##")
                    });
                }
                list.Add(vmRow);
                allMaterials.Add(vmRow);
            }

            allGroups.Add(new GroupHeaderRowVm
            {
                Nombre = group.Key,
                TotalMl = group.Sum(i => i.Total),
                MaterialCount = group.Count()
            });
        }

        RebuildCategorias(categoriasSet);
        OnPropertyChanged(nameof(SidebarFooterText));
        AplicarFiltros();
    }

    private void RebuildCategorias(HashSet<string> categoriasSet)
    {
        Categorias.Clear();
        Categorias.Add(new CategoriaChipVm
        {
            Nombre = "Todas",
            IsActive = string.Equals(activeCategoria, "Todas", StringComparison.OrdinalIgnoreCase),
            Count = allMaterials.Count,
            ColorHex = "#64748b"
        });

        foreach (var group in allGroups.OrderBy(g => CategorySortKey(g.Nombre)))
        {
            Categorias.Add(new CategoriaChipVm
            {
                Nombre = group.Nombre,
                IsActive = string.Equals(activeCategoria, group.Nombre, StringComparison.OrdinalIgnoreCase),
                Count = group.MaterialCount,
                ColorHex = CategoryColors.TryGetValue(group.Nombre, out var color) ? color : "#64748b"
            });
        }
    }

    private static int CategorySortKey(string name) => name switch
    {
        "Tubería" => 0,
        "Conductor" => 1,
        "Cajas" => 2,
        "Artefactos" => 3,
        "Sensores" => 4,
        "Señalética" => 5,
        "Flexible" => 6,
        "Cinta LED" => 7,
        "Montaje" => 8,
        "Interruptores" => 9,
        _ => 10
    };

    private void AplicarFiltros()
    {
        FlatRows.Clear();
        var search = SearchText?.Trim() ?? "";
        var filterByCategory = !showAllCategories;

        foreach (var group in allGroups)
        {
            IEnumerable<MaterialConZonasVm> query = allMaterials.Where(m => m.Categoria == group.Nombre);
            if (filterByCategory)
                query = query.Where(m => string.Equals(m.Categoria, activeCategoria, StringComparison.OrdinalIgnoreCase));
            if (search.Length > 0)
                query = query.Where(m => m.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));

            var list = query.ToList();
            if (list.Count == 0) continue;

            FlatRows.Add(new GroupHeaderRowVm
            {
                Nombre = group.Nombre,
                TotalMl = list.Sum(i => i.Total),
                MaterialCount = list.Count,
                IsExpanded = group.IsExpanded
            });

            if (!group.IsExpanded) continue;
            foreach (var row in list)
            {
                row._parent = this;
                FlatRows.Add(row);
            }
        }
    }

    private void Tomar(MaterialConZonasVm? row)
    {
        if (row == null) return;
        OnTomar?.Invoke(row);
        OnCerrar?.Invoke();
    }

    private void Cerrar() => OnCerrar?.Invoke();
}
