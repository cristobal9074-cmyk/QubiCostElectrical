using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.EconomicEditor;

public sealed record EconomicConceptVm
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ConceptCode { get; set; } = "";
    public string ConceptName { get; set; } = "";
    public string Category { get; set; } = "Indirecto";
    public string ConceptType { get; set; } = "Porcentaje";
    public string BaseType { get; set; } = "SubtotalAnterior";
    public decimal RateOrAmount { get; set; }
    public int SortOrder { get; set; }
    public bool IsInformative { get; set; }
    public bool IncludedInTotal { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public decimal CalculatedValue { get; set; }
    public decimal SubtotalBefore { get; set; }
    public decimal SubtotalAfter { get; set; }
}

public sealed partial class EconomicEditorViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<EconomicConceptVm> concepts = new();
    private CancellationTokenSource? calculationCts;
    private bool isCalculating;
    private string statusMessage = "";
    private decimal directCost;
    private decimal totalCost;
    private bool isDirty;

    public EconomicEditorViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        AddConceptCommand = new RelayCommand(AddConcept);
        RemoveConceptCommand = new RelayCommand<EconomicConceptVm?>(RemoveConcept, c => c != null);
        CalculateCommand = new RelayCommand(async () => await CalculateAsync(), () => !IsCalculating);
    }

    public ObservableCollection<EconomicConceptVm> Concepts => concepts;
    public bool IsCalculating { get => isCalculating; private set => SetProperty(ref isCalculating, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public decimal DirectCost { get => directCost; private set => SetProperty(ref directCost, value); }
    public decimal TotalCost { get => totalCost; private set => SetProperty(ref totalCost, value); }
    public bool IsDirty { get => isDirty; private set => SetProperty(ref isDirty, value); }
    public bool HasService => host.Services.EconomicCalculation != null;

    public ICommand AddConceptCommand { get; }
    public ICommand RemoveConceptCommand { get; }
    public ICommand CalculateCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Clear);
    }
}
