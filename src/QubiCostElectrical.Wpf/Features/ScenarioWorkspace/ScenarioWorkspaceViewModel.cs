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

/// <summary>
/// Prototipo experimental de comparación de escenarios económicos (WIP).
/// Mantenido intencionalmente fuera de <see cref="QubiCostElectrical.Wpf.Navigation.ShellRoute"/>
/// en la versión actual (1.1.1) hasta completar integración de cálculo real y persistencia.
/// </summary>
public sealed class ScenarioWorkspaceViewModel : WorkspacePageViewModelBase
{
	private readonly ObservableCollection<ScenarioVm> scenarios = new ObservableCollection<ScenarioVm>();

	private ScenarioVm? selectedScenario;

	private CancellationTokenSource? calculationCts;

	private string statusMessage = "";

	private ScenarioVm? baseScenario;

	public ObservableCollection<ScenarioVm> Scenarios => scenarios;

	public ScenarioVm? SelectedScenario
	{
		get => selectedScenario;
		set => SetProperty(ref selectedScenario, value, nameof(SelectedScenario));
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value, nameof(StatusMessage));
	}

	public ScenarioVm? BaseScenario
	{
		get => baseScenario;
		private set => SetProperty(ref baseScenario, value, nameof(BaseScenario));
	}

	public bool IsReadOnly => Host.Session.IsReadOnly;

	public ICommand CreateCommand { get; }

	public ICommand DuplicateCommand { get; }

	public ICommand DeleteCommand { get; }

	public ICommand CalculateCommand { get; }

	public ICommand ApplyToBaseCommand { get; }

	public ICommand CompareWithBaseCommand { get; }

	public ScenarioWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host)
		: base(host)
	{
		_ = shell;
		CreateCommand = new RelayCommand(CreateScenario, () => CanEdit);
		DuplicateCommand = new RelayCommand<ScenarioVm>(DuplicateScenario, s => s != null && !s.IsBase && CanEdit);
		DeleteCommand = new RelayCommand<ScenarioVm>(DeleteScenario, s => s != null && !s.IsBase && CanEdit);
		CalculateCommand = new RelayCommand(async () => await CalculateAsync(), () => CanEdit && !IsLoading);
		ApplyToBaseCommand = new RelayCommand(async () => await ApplyToBaseAsync(),
			() => SelectedScenario != null && !SelectedScenario.IsBase && CanEdit);
		CompareWithBaseCommand = new RelayCommand<ScenarioVm>(CompareWithBase, s => s != null && !s.IsBase && !ShowEmptyState);
		SyncWorkspaceGate();
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		SyncWorkspaceGate();
		OnPropertyChanged(nameof(IsReadOnly));
		if (e.Kind == ProjectSessionChangeKind.Reset)
			Application.Current?.Dispatcher.InvokeAsync((Action)Clear);
	}

	protected override void OnWorkspaceGateChanged()
	{
		(CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(DuplicateCommand as RelayCommand<ScenarioVm>)?.RaiseCanExecuteChanged();
		(DeleteCommand as RelayCommand<ScenarioVm>)?.RaiseCanExecuteChanged();
		(CalculateCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(ApplyToBaseCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(CompareWithBaseCommand as RelayCommand<ScenarioVm>)?.RaiseCanExecuteChanged();
	}

	private void CreateScenario()
	{
		if (!CanEdit)
			return;

		int num = scenarios.Count(s => !s.IsBase);
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
		if (s == null || !CanEdit)
			return;

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

	private void DeleteScenario(ScenarioVm? s)
	{
		if (s == null || s.IsBase || !CanEdit)
			return;

		scenarios.Remove(s);
		if (SelectedScenario == s)
			SelectedScenario = scenarios.LastOrDefault();
	}

	public async Task CalculateAsync()
	{
		if (!CanEdit || IsLoading)
			return;

		calculationCts?.Cancel();
		calculationCts = new CancellationTokenSource();
		CancellationToken ct = calculationCts.Token;
		IsLoading = true;
		OnWorkspaceGateChanged();
		StatusMessage = "Calculando escenarios...";
		try
		{
			await Task.Delay(100, ct);
			foreach (ScenarioVm item in scenarios.Where(s => s.IsActive))
			{
				if (ct.IsCancellationRequested)
					return;

				item.DirectCost = 100000m;
				item.IndirectCost = item.DirectCost * 0.15m;
				item.Utility = (item.DirectCost + item.IndirectCost) * 0.10m;
				item.Taxes = (item.DirectCost + item.IndirectCost + item.Utility) * 0.19m;
				item.UnitPrice = item.DirectCost + item.IndirectCost + item.Utility + item.Taxes;
				item.Total = item.UnitPrice;
				item.IsCalculated = true;
				if (BaseScenario != null && BaseScenario.Total > 0m)
					item.VariationPct = (item.Total - BaseScenario.Total) / BaseScenario.Total * 100m;
			}

			StatusMessage = "Escenarios calculados.";
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Cálculo cancelado.";
		}
		finally
		{
			IsLoading = false;
			OnWorkspaceGateChanged();
		}
	}

	private async Task ApplyToBaseAsync()
	{
		if (SelectedScenario == null || SelectedScenario.IsBase || !CanEdit)
			return;

		await Task.CompletedTask;
		StatusMessage = "Escenario '" + SelectedScenario.Name + "' aplicado.";
	}

	private void CompareWithBase(ScenarioVm? s)
	{
		if (s == null || s.IsBase || BaseScenario == null)
			return;

		StatusMessage = $"Comparación: {s.Name} vs Base — Variación: {s.VariationPct:N1}%";
	}

	private void Clear()
	{
		scenarios.Clear();
		baseScenario = null;
		IsLoading = false;
		StatusMessage = "";
	}
}
