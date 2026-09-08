using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.EconomicEditor;

public sealed class EconomicEditorViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost shell;
	private readonly BudgetProjectSession session;
	private readonly IProjectWorkspaceService workspace;
	private readonly IEconomicCalculationService economicCalc;

	private readonly ObservableCollection<EconomicConceptVm> concepts = new ObservableCollection<EconomicConceptVm>();

	private CancellationTokenSource? calculationCts;

	private bool isCalculating;

	private string statusMessage = "";

	private decimal directCost;

	private decimal totalCost;

	private bool isDirty;

	public ObservableCollection<EconomicConceptVm> Concepts => concepts;

	public bool IsCalculating
	{
		get
		{
			return isCalculating;
		}
		private set
		{
			SetProperty(ref isCalculating, value, "IsCalculating");
		}
	}

	public string StatusMessage
	{
		get
		{
			return statusMessage;
		}
		private set
		{
			SetProperty(ref statusMessage, value, "StatusMessage");
		}
	}

	public decimal DirectCost
	{
		get
		{
			return directCost;
		}
		set
		{
			SetProperty(ref directCost, value, "DirectCost");
		}
	}

	public decimal TotalCost
	{
		get
		{
			return totalCost;
		}
		private set
		{
			SetProperty(ref totalCost, value, "TotalCost");
		}
	}

	public bool IsDirty
	{
		get
		{
			return isDirty;
		}
		private set
		{
			SetProperty(ref isDirty, value, "IsDirty");
		}
	}

	public bool HasService => economicCalc != null;

	private bool showEmptyState;

	public bool ShowEmptyState
	{
		get => showEmptyState;
		private set => SetProperty(ref showEmptyState, value, nameof(ShowEmptyState));
	}

	public bool IsReadOnly => session.IsReadOnly;
	public bool CanEdit => !ShowEmptyState && !session.IsReadOnly;

	public ICommand AddConceptCommand { get; }

	public ICommand RemoveConceptCommand { get; }

	public ICommand CalculateCommand { get; }

	public EconomicEditorViewModel(IShellNavigationHost shell, WpfAppHost host)
		: this(shell, host.Session, host.ProjectWorkspace, host.Services.EconomicCalculation) { }

	public EconomicEditorViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IEconomicCalculationService economicCalc)
		: base(session)
	{
		this.shell = shell;
		this.session = session;
		this.workspace = workspace;
		this.economicCalc = economicCalc;
		AddConceptCommand = new RelayCommand(AddConcept, () => CanEdit);
		RemoveConceptCommand = new RelayCommand<EconomicConceptVm>(RemoveConcept, (EconomicConceptVm? c) => c != null && CanEdit);
		CalculateCommand = new RelayCommand(async delegate
		{
			await CalculateAsync();
		}, () => !IsCalculating);
		SyncWorkspaceGate();
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		SyncWorkspaceGate();
		if (e.Kind == ProjectSessionChangeKind.Reset)
		{
			Application current = Application.Current;
			if (current != null)
			{
				((DispatcherObject)current).Dispatcher.InvokeAsync((Action)Clear);
			}
		}
	}

	private void AddConcept()
	{
		if (!CanEdit) return;
		int num = ((concepts.Count > 0) ? concepts.Max((EconomicConceptVm c) => c.SortOrder) : 0);
		concepts.Add(new EconomicConceptVm
		{
			ConceptCode = $"CON-{num + 1}",
			ConceptName = "Nuevo concepto",
			SortOrder = num + 1
		});
		IsDirty = true;
	}

	private void RemoveConcept(EconomicConceptVm? concept)
	{
		if (!CanEdit) return;
		if (!(concept == null) && concepts.Remove(concept))
		{
			Reindex();
			IsDirty = true;
		}
	}

	private void Reindex()
	{
		int num = 1;
		foreach (EconomicConceptVm item in from c in concepts
			orderby c.SortOrder, c.ConceptCode
			select c)
		{
			item.SortOrder = num++;
		}
	}

	public async Task CalculateAsync()
	{
		calculationCts?.Cancel();
		calculationCts = new CancellationTokenSource();
		CancellationToken ct = calculationCts.Token;
		IsCalculating = true;
		StatusMessage = "Calculando...";
		try
		{
			var config = new EconomicConfig
			{
				Concepts = concepts.Select(ToDomainConcept).ToList().AsReadOnly()
			};
			var input = new EconomicInput
			{
				UnitDirectCost = DirectCost,
				Config = config
			};

			var service = economicCalc;
			EconomicResult result = service != null
				? await Task.Run(() => service.Calculate(input, ct), ct)
				: EconomicEngine.Calculate(input, ct);

			if (ct.IsCancellationRequested)
				return;

			var byCode = result.ConceptsApplied.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
			foreach (var item in concepts)
			{
				if (byCode.TryGetValue(item.ConceptCode, out var applied))
				{
					item.CalculatedValue = applied.RoundedValue;
					item.SubtotalBefore = applied.PreviousSubtotal;
					item.SubtotalAfter = applied.PostSubtotal;
				}
			}

			TotalCost = result.FinalUnitPrice;
			if (result.Errors.Count > 0)
				StatusMessage = "Error: " + string.Join("; ", result.Errors);
			else if (result.Warnings.Count > 0)
				StatusMessage = "Cálculo completado con advertencias: " + string.Join("; ", result.Warnings);
			else
				StatusMessage = "Cálculo completado.";
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Cálculo cancelado.";
		}
		catch (Exception ex2)
		{
			StatusMessage = "Error: " + ex2.Message;
		}
		finally
		{
			IsCalculating = false;
		}
	}

	private static EconomicConcept ToDomainConcept(EconomicConceptVm vm) => new()
	{
		Id = vm.Id,
		Code = vm.ConceptCode,
		Name = vm.ConceptName,
		Category = ParseCategory(vm.Category),
		CalculationType = ParseCalculationType(vm.ConceptType),
		CalculationBase = ParseCalculationBase(vm.BaseType),
		Value = vm.RateOrAmount,
		Order = vm.SortOrder,
		IsActive = vm.IsActive,
		AffectsFinalPrice = vm.IncludedInTotal,
		IsInformative = vm.IsInformative
	};

	private static readonly Dictionary<string, EconomicCategory> CategoryByLabel = new(StringComparer.OrdinalIgnoreCase)
	{
		["Indirecto"] = EconomicCategory.IndirectCost,
		["GastosGenerales"] = EconomicCategory.Overhead,
		["Administracion"] = EconomicCategory.Administration,
		["Supervision"] = EconomicCategory.Supervision,
		["Contingencia"] = EconomicCategory.Contingency,
		["Riesgo"] = EconomicCategory.Risk,
		["Utilidad"] = EconomicCategory.Profit,
		["Impuesto"] = EconomicCategory.Tax,
		["Recargo"] = EconomicCategory.Surcharge,
		["Descuento"] = EconomicCategory.Discount,
		["RetencionInformativa"] = EconomicCategory.InformativeWithholding,
		["Otro"] = EconomicCategory.Other
	};

    private static readonly Dictionary<string, CalculationType> CalcTypeByLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Porcentaje"] = CalculationType.Percentage,
        ["Fijo"] = CalculationType.FixedAmount,
        ["PorUnidad"] = CalculationType.AmountPerUnit,
        ["PorCantidad"] = CalculationType.AmountPerQuantity,
        ["PorcentajeAcumulado"] = CalculationType.CumulativePercentage,
        ["PorcentajeSobreBaseSeleccionada"] = CalculationType.PercentageOnSelectedBase
    };

    private static readonly Dictionary<string, CalculationBase> BaseByLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CostoDirecto"] = CalculationBase.DirectCost,
        ["Materiales"] = CalculationBase.Materials,
        ["ManoDeObra"] = CalculationBase.Labor,
        ["Equipos"] = CalculationBase.Equipment,
        ["Herramientas"] = CalculationBase.Tools,
        ["Subcontrato"] = CalculationBase.Subcontract,
        ["Transporte"] = CalculationBase.Transport,
        ["Auxiliares"] = CalculationBase.Auxiliaries,
        ["OtrosRecursos"] = CalculationBase.OtherResources,
        ["CostoDirectoMasIndirecto"] = CalculationBase.DirectCostPlusIndirect,
        ["SubtotalAnterior"] = CalculationBase.PreviousSubtotal,
        ["SubtotalAntesUtilidad"] = CalculationBase.SubtotalBeforeProfit,
        ["SubtotalAntesImpuesto"] = CalculationBase.SubtotalBeforeTax,
        ["PrecioAntesDescuentos"] = CalculationBase.PriceBeforeDiscounts,
        ["PrecioFinalAnterior"] = CalculationBase.FinalPreviousPrice,
        ["Personalizado"] = CalculationBase.Custom
    };

    private static EconomicCategory ParseCategory(string label) =>
        CategoryByLabel.TryGetValue(label, out var v) ? v : EconomicCategory.Other;

    private static CalculationType ParseCalculationType(string label) =>
        CalcTypeByLabel.TryGetValue(label, out var v) ? v : CalculationType.Percentage;

    private static CalculationBase ParseCalculationBase(string label) =>
        BaseByLabel.TryGetValue(label, out var v) ? v : CalculationBase.PreviousSubtotal;

	private void SyncWorkspaceGate()
	{
		ShowEmptyState = !workspace.HasOpenProject;
		OnPropertyChanged(nameof(CanEdit));
	}

	private void Clear()
	{
		concepts.Clear();
		DirectCost = 0m;
		TotalCost = 0m;
		IsDirty = false;
		StatusMessage = "";
	}
}
