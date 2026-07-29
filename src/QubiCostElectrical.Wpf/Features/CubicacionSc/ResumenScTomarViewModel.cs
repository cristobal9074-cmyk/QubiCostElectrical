using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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

    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Todas", "#64748b" },
        { "Tubería", "#3b82f6" },
        { "Conductor", "#8b5cf6" },
        { "Cajas", "#f59e0b" },
        { "Artefactos", "#ef4444" },
        { "Sensores", "#22c55e" },
        { "Señalética", "#f97316" },
        { "Flexible", "#06b6d4" },
        { "Cinta LED", "#14b8a6" },
        { "Montaje", "#a855f7" },
        { "Interruptores", "#ec4899" },
    };

    public ResumenScTomarViewModel(
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config,
        IReadOnlyList<ZonaProyecto> zonas)
    {
        this.zonas = zonas;

        Categorias = new ObservableCollection<CategoriaChipVm>();
        Categorias.Add(new CategoriaChipVm
        {
            Nombre = "Todas",
            IsActive = true,
            ColorHex = "#64748b",
        });

        CargarMateriales(cuadros, config);

        TomarCommand = new RelayCommand<MaterialConZonasVm>(Tomar);
        CerrarCommand = new RelayCommand(Cerrar);
    }

    public ObservableCollection<CategoriaChipVm> Categorias { get; }
    public ObservableCollection<FlatRowVm> FlatRows { get; } = new();

    public string ActiveCategoria
    {
        get => activeCategoria;
        set
        {
            if (SetProperty(ref activeCategoria, value))
            {
                showAllCategories = string.Equals(value, "Todas", StringComparison.OrdinalIgnoreCase);
                foreach (var chip in Categorias)
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

    private FlatRowVm? selectedRow;
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

    public string MaterialCountText
    {
        get
        {
            int total = allMaterials.Count;
            return $"{total} material(es)";
        }
    }

    public string SidebarFooterText => $"{Categorias.Count} categorías · {allMaterials.Count} materiales";

    public IReadOnlyList<ZonaProyecto> Zonas => zonas;

    private void CargarMateriales(IReadOnlyList<CuadroCargaSCRow> cuadros, CubicacionScConfig config)
    {
        var scMaterials = CubicacionSCEngine.AggregateConZonas(cuadros, config, zonas);
        var porCategoria = scMaterials
            .GroupBy(m => m.Categoria)
            .OrderBy(g => g.Key);

        allMaterials.Clear();
        allGroups.Clear();

        var categoriasSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var grupo in porCategoria)
        {
            var catName = grupo.Key;
            categoriasSet.Add(catName);

            var vmList = new List<MaterialConZonasVm>();
            foreach (var m in grupo)
            {
                var vm = new MaterialConZonasVm
                {
                    Nombre = m.Nombre,
                    Unidad = m.Unidad,
                    Categoria = catName,
                    Total = m.Total,
                };
                foreach (var (zNombre, cant) in m.Zonas)
                {
                    vm.ZonaQties.Add(new ZonaQtyWrapper
                    {
                        ZonaNombre = zNombre,
                        Text = cant.ToString("0.##"),
                    });
                }
                vmList.Add(vm);
                allMaterials.Add(vm);
            }

            allGroups.Add(new GroupHeaderRowVm
            {
                Nombre = catName,
                TotalMl = grupo.Sum(i => i.Total),
                MaterialCount = grupo.Count(),
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
            ColorHex = "#64748b",
        });
        foreach (var cat in allGroups.OrderBy(g =>
            g.Nombre switch
            {
                "Tubería" => 0, "Conductor" => 1, "Cajas" => 2,
                "Artefactos" => 3, "Sensores" => 4, "Señalética" => 5,
                "Flexible" => 6, "Cinta LED" => 7, "Montaje" => 8,
                "Interruptores" => 9, _ => 10,
            }))
        {
            Categorias.Add(new CategoriaChipVm
            {
                Nombre = cat.Nombre,
                IsActive = string.Equals(activeCategoria, cat.Nombre, StringComparison.OrdinalIgnoreCase),
                Count = cat.MaterialCount,
                ColorHex = CategoryColors.TryGetValue(cat.Nombre, out var c) ? c : "#64748b",
            });
        }
    }

    private void AplicarFiltros()
    {
        FlatRows.Clear();

        var search = SearchText?.Trim() ?? "";
        var filterByCat = !showAllCategories;

        foreach (var group in allGroups)
        {
            var matching = allMaterials.Where(m => m.Categoria == group.Nombre);

            if (filterByCat)
                matching = matching.Where(m => string.Equals(m.Categoria, activeCategoria, StringComparison.OrdinalIgnoreCase));

            if (search.Length > 0)
                matching = matching.Where(m => m.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));

            var lista = matching.ToList();
            if (lista.Count == 0) continue;

            FlatRows.Add(new GroupHeaderRowVm
            {
                Nombre = group.Nombre,
                TotalMl = lista.Sum(i => i.Total),
                MaterialCount = lista.Count,
                IsExpanded = group.IsExpanded,
            });

            if (group.IsExpanded)
            {
                foreach (var item in lista)
                {
                    item._parent = this;
                    FlatRows.Add(item);
                }
            }
        }
    }

    private void Tomar(MaterialConZonasVm? row)
    {
        if (row != null)
        {
            OnTomar?.Invoke(row);
            OnCerrar?.Invoke();
        }
    }

    private void Cerrar()
    {
        OnCerrar?.Invoke();
    }
}

public sealed class MaterialConZonasVm : FlatRowVm
{
    internal ResumenScTomarViewModel? _parent;
    public override bool IsGroupHeader => false;

    public string Nombre { get; init; } = "";
    public string Codigo => Nombre;
    public string Descripcion => Nombre;
    public string Unidad { get; init; } = "";
    public string Categoria { get; init; } = "";
    public decimal Total { get; set; }
    public string TotalDisplay => Total.ToString("#,##0.##");
    public string CodigoDisplay => Nombre;

    public ObservableCollection<ZonaQtyWrapper> ZonaQties { get; } = new();

    public void RecalcTotal()
    {
        Total = ZonaQties.Sum(z => decimal.TryParse(z.Text, out var v) ? v : 0m);
        foreach (var z in ZonaQties)
            z.NotifyValueChanged();
    }
}
