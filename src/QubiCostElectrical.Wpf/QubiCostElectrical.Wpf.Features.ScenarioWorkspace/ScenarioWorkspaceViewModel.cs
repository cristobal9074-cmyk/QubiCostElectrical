#nullable enable
using System;
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

namespace QubiCostElectrical.Wpf.Features.ScenarioWorkspace;

public sealed class ScenarioWorkspaceViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost shell;

	private readonly WpfAppHost host;

	private readonly ObservableCollection<ScenarioVm> scenarios = new ObservableCollection<ScenarioVm>();

	private ScenarioVm? selectedScenario;

	private CancellationTokenSource? calculationCts;

	private bool isCalculating;

	private string statusMessage = "";

	private ScenarioVm? baseScenario;

	public ObservableCollection<ScenarioVm> Scenarios => scenarios;

	public ScenarioVm? SelectedScenario
	{
		get
		{
			return selectedScenario;
		}
		set
		{
			SetProperty(ref selectedScenario, value, "SelectedScenario");
		}
	}

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

	public ScenarioVm? BaseScenario
	{
		get
		{
			return baseScenario;
		}
		private set
		{
			SetProperty(ref baseScenario, value, "BaseScenario");
		}
	}

	public ICommand CreateCommand { get; }

	public ICommand DuplicateCommand { get; }

	public ICommand DeleteCommand { get; }

	public ICommand CalculateCommand { get; }

	public ICommand ApplyToBaseCommand { get; }

	public ICommand CompareWithBaseCommand { get; }

	public ScenarioWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host)
		: base(host)
	{
		this.shell = shell;
		this.host = host;
		CreateCommand = new RelayCommand(CreateScenario);
		DuplicateCommand = new RelayCommand<ScenarioVm>(DuplicateScenario, (ScenarioVm? s) => s != null && !s.IsBase);
		DeleteCommand = new RelayCommand<ScenarioVm>(DeleteScenario, (ScenarioVm? s) => s != null && !s.IsBase);
		CalculateCommand = new RelayCommand(async delegate
		{
			await CalculateAsync();
		}, () => !IsCalculating);
		ApplyToBaseCommand = new RelayCommand(async delegate
		{
			await ApplyToBaseAsync();
		}, () => SelectedScenario != null && !SelectedScenario.IsBase);
		CompareWithBaseCommand = new RelayCommand<ScenarioVm>(CompareWithBase, (ScenarioVm? s) => s != null && !s.IsBase);
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		if (e.Kind == ProjectSessionChangeKind.Reset)
		{
			Application current = Application.Current;
			if (current != null)
			{
				((DispatcherObject)current).Dispatcher.InvokeAsync((Action)Clear);
			}
		}
	}

	private void CreateScenario()
	{
		int num = scenarios.Count((ScenarioVm s) => !s.IsBase);
		ScenarioVm item = new ScenarioVm
		{
			Name = $"Escenario {num + 1}",
			ScenarioType = "Custom",
			IsActive = true
		};
		scenarios.Add(item);
		SelectedScenario = item;
	}

	private void DuplicateScenario(ScenarioVm? s)
	{
		if (!(s == null))
		{
			ScenarioVm item = new ScenarioVm
			{
				Name = s.Name + " (copia)",
				ScenarioType = s.ScenarioType,
				Description = s.Description,
				IsActive = s.IsActive,
				IsCalculated = false
			};
			scenarios.Add(item);
			SelectedScenario = item;
		}
	}

	private void DeleteScenario(ScenarioVm? s)
	{
		if (!(s == null) && !s.IsBase)
		{
			scenarios.Remove(s);
			if (SelectedScenario == s)
			{
				SelectedScenario = scenarios.LastOrDefault();
			}
		}
	}

	public async Task CalculateAsync()
	{
		calculationCts?.Cancel();
		calculationCts = new CancellationTokenSource();
		CancellationToken ct = calculationCts.Token;
		IsCalculating = true;
		StatusMessage = "Calculando escenarios...";
		try
		{
			await Task.Delay(100, ct);
			foreach (ScenarioVm item in scenarios.Where((ScenarioVm s) => s.IsActive))
			{
				if (ct.IsCancellationRequested)
				{
					return;
				}
				item.DirectCost = 100000m;
				item.IndirectCost = item.DirectCost * 0.15m;
				item.Utility = (item.DirectCost + item.IndirectCost) * 0.10m;
				item.Taxes = (item.DirectCost + item.IndirectCost + item.Utility) * 0.19m;
				item.UnitPrice = item.DirectCost + item.IndirectCost + item.Utility + item.Taxes;
				item.Total = item.UnitPrice;
				item.IsCalculated = true;
				if (BaseScenario != null && BaseScenario.Total > 0m)
				{
					item.VariationPct = (item.Total - BaseScenario.Total) / BaseScenario.Total * 100m;
				}
			}
			StatusMessage = "Escenarios calculados.";
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Cálculo cancelado.";
		}
		finally
		{
			IsCalculating = false;
		}
	}

	private async Task ApplyToBaseAsync()
	{
		if (!(SelectedScenario == null) && !SelectedScenario.IsBase)
		{
			await Task.CompletedTask;
			StatusMessage = "Escenario '" + SelectedScenario.Name + "' aplicado.";
		}
	}

	private void CompareWithBase(ScenarioVm? s)
	{
		if (!(s == null) && !s.IsBase && !(BaseScenario == null))
		{
			StatusMessage = $"Comparación: {s.Name} vs Base — Variación: {s.VariationPct:N1}%";
		}
	}

	private void Clear()
	{
		scenarios.Clear();
		baseScenario = null;
		IsCalculating = false;
		StatusMessage = "";
	}
}
