using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.FloorSummary;

public sealed class FloorMaterialItemVm : ViewModelBase
{
    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public decimal Cantidad { get; }
    public decimal PrecioUnitClp { get; }
    public decimal SubtotalClp { get; }
    public string Fuente { get; }
    public string Categoria { get; }

    public FloorMaterialItemVm(FloorSummaryMaterialItemDto dto)
    {
        Codigo = dto.Codigo;
        Descripcion = dto.Descripcion;
        Unidad = dto.Unidad;
        Cantidad = dto.Cantidad;
        PrecioUnitClp = dto.PrecioUnitClp;
        SubtotalClp = dto.SubtotalClp;
        Fuente = dto.Fuente;
        Categoria = dto.Categoria;
    }
}

public sealed class FloorCardVm : ViewModelBase
{
    private bool isExpanded;

    public string ZonaId { get; }
    public string ZonaNombre { get; }
    public int Orden { get; }
    public decimal CostoTotalClp { get; }
    public int MaterialesUnicosCount { get; }
    public decimal CantidadTotal { get; }
    public List<FloorMaterialItemVm> Materiales { get; }
    public string MetaText => $"{MaterialesUnicosCount} materiales · {CantidadTotal:#,##0.##} unidades totales";

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public FloorCardVm(FloorSummaryDto dto, bool expandDefault = false)
    {
        ZonaId = dto.ZonaId;
        ZonaNombre = dto.ZonaNombre;
        Orden = dto.Orden;
        CostoTotalClp = dto.CostoTotalClp;
        MaterialesUnicosCount = dto.MaterialesUnicosCount;
        CantidadTotal = dto.CantidadTotal;
        Materiales = dto.Materiales.Select(m => new FloorMaterialItemVm(m)).ToList();
        isExpanded = expandDefault;
    }
}

public sealed class FloorSummaryViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly FloorSummaryService summarySvc;

    private bool showEmptyState = true;
    private string searchText = "";
    private string selectedSourceFilter = "Todos"; // "Todos", "Cubicación", "Cub. SC"
    private int totalFloors;
    private int uniqueMaterialsCount;
    private decimal grandTotalCost;
    private int takeoffLinesCount;
    private string selectedConsolidacionMode = "Mostrar ambos (duplicados)";

    public string SelectedConsolidacionMode
    {
        get => selectedConsolidacionMode;
        set
        {
            if (SetProperty(ref selectedConsolidacionMode, value))
            {
                Refresh();
            }
        }
    }

    public List<string> ConsolidacionModeOptions { get; } = new()
    {
        "Mostrar ambos (duplicados)",
        "Automático (Usar Cubicación SC)",
        "Manual (Usar Cubicación regular)"
    };

    private string SelectedConsolidacionModeValue => SelectedConsolidacionMode switch
    {
        "Automático (Usar Cubicación SC)" => "auto",
        "Manual (Usar Cubicación regular)" => "manual",
        _ => "both"
    };

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
                ApplyFilters();
        }
    }

    public string SelectedSourceFilter
    {
        get => selectedSourceFilter;
        set
        {
            if (SetProperty(ref selectedSourceFilter, value))
                ApplyFilters();
        }
    }

    public int TotalFloors
    {
        get => totalFloors;
        private set => SetProperty(ref totalFloors, value);
    }

    public int UniqueMaterialsCount
    {
        get => uniqueMaterialsCount;
        private set => SetProperty(ref uniqueMaterialsCount, value);
    }

    public decimal GrandTotalCost
    {
        get => grandTotalCost;
        private set => SetProperty(ref grandTotalCost, value);
    }

    public int TakeoffLinesCount
    {
        get => takeoffLinesCount;
        private set => SetProperty(ref takeoffLinesCount, value);
    }

    public ObservableCollection<FloorCardVm> FloorCards { get; } = new();
    private List<FloorCardVm> allFloorCards = new();

    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand FilterSourceCommand { get; }

    public FloorSummaryViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public FloorSummaryViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        summarySvc = new FloorSummaryService();

        RefreshCommand = new RelayCommand(Refresh);
        ExportExcelCommand = new RelayCommand(ExportExcel, () => !ShowEmptyState);
        FilterSourceCommand = new RelayCommand<string>(filter => SelectedSourceFilter = filter ?? "Todos");

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);

    public void Refresh()
    {
        ShowEmptyState = !workspace.HasOpenProject;
        if (ShowEmptyState)
        {
            FloorCards.Clear();
            allFloorCards.Clear();
            TotalFloors = 0;
            UniqueMaterialsCount = 0;
            GrandTotalCost = 0;
            TakeoffLinesCount = 0;
            return;
        }

        var data = session.Data;
        var summaryDtos = summarySvc.GetSummary(BudgetVersionData.FromProjectFileData(data), SelectedConsolidacionModeValue);

        allFloorCards = summaryDtos.Select((dto, idx) => new FloorCardVm(dto, expandDefault: idx == 0)).ToList();

        // Calcular KPIs totales
        TotalFloors = summaryDtos.Count(d => !string.IsNullOrEmpty(d.ZonaId));
        UniqueMaterialsCount = summaryDtos.SelectMany(d => d.Materiales.Select(m => m.Codigo)).Distinct(StringComparer.OrdinalIgnoreCase).Count(c => !string.IsNullOrEmpty(c));
        GrandTotalCost = summaryDtos.Sum(d => d.CostoTotalClp);
        TakeoffLinesCount = data.Cubicacion.Count(c => !string.IsNullOrEmpty(c.Codigo)) + data.CubicacionSC.Sum(c => c.Circuitos.Count(cir => cir.EsActivo));

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        FloorCards.Clear();
        foreach (var card in allFloorCards)
        {
            var filteredMaterials = card.Materiales.AsEnumerable();

            // Filtro de origen
            if (SelectedSourceFilter == "Cubicación")
            {
                filteredMaterials = filteredMaterials.Where(m => m.Fuente == "Cubicación");
            }
            else if (SelectedSourceFilter == "Cub. SC")
            {
                filteredMaterials = filteredMaterials.Where(m => m.Fuente == "Cub. SC");
            }

            // Filtro de texto
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string term = SearchText.Trim().ToUpperInvariant();
                filteredMaterials = filteredMaterials.Where(m =>
                    (m.Codigo ?? "").ToUpperInvariant().Contains(term) ||
                    (m.Descripcion ?? "").ToUpperInvariant().Contains(term));
            }

            var matList = filteredMaterials.ToList();
            if (matList.Count > 0)
            {
                // Re-agrupar/Re-calcular para el Piso filtrado
                var dto = new FloorSummaryDto
                {
                    ZonaId = card.ZonaId,
                    ZonaNombre = card.ZonaNombre,
                    Orden = card.Orden,
                    Materiales = matList.Select(m => new FloorSummaryMaterialItemDto
                    {
                        Codigo = m.Codigo,
                        Descripcion = m.Descripcion,
                        Unidad = m.Unidad,
                        Cantidad = m.Cantidad,
                        PrecioUnitClp = m.PrecioUnitClp,
                        Fuente = m.Fuente,
                        Categoria = m.Categoria
                    }).ToList()
                };

                FloorCards.Add(new FloorCardVm(dto, expandDefault: card.IsExpanded));
            }
        }
    }

    private void ExportExcel()
    {
        WpfDialogService.Info("Exportar Resumen", "Funcionalidad de exportación a Excel en desarrollo para el próximo release.");
    }
}
