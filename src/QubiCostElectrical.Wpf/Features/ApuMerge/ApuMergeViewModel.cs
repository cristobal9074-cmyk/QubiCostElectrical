using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ApuMerge;

public sealed class ApuMergeViewModel : WorkspacePageViewModelBase
{
	private readonly IShellNavigationHost shell;

	private readonly ObservableCollection<ConflictVm> conflicts = new ObservableCollection<ConflictVm>();

	private readonly ObservableCollection<DiffVm> diffs = new ObservableCollection<DiffVm>();

	private string apuId = "";

	private string code = "";

	private string statusMessage = "";

	private bool isReady;

	public ObservableCollection<ConflictVm> Conflicts => conflicts;

	public ObservableCollection<DiffVm> Diffs => diffs;

	public string ApuId
	{
		get => apuId;
		private set => SetProperty(ref apuId, value, nameof(ApuId));
	}

	public string Code
	{
		get => code;
		private set => SetProperty(ref code, value, nameof(Code));
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value, nameof(StatusMessage));
	}

	public bool IsReady
	{
		get => isReady;
		private set => SetProperty(ref isReady, value, nameof(IsReady));
	}

	public bool IsReadOnly => Session.IsReadOnly;

	public ICommand ResolveKeepLocalCommand { get; }

	public ICommand ResolveAcceptCatalogCommand { get; }

	public ICommand ResolveKeepBaseCommand { get; }

	public ICommand CreateMergeVersionCommand { get; }

	public ICommand CancelCommand { get; }

	private readonly IApuVersionRepository apuVersions;

	public ApuMergeViewModel(IShellNavigationHost shell, WpfAppHost host)
		: this(shell, host.Session, host.ProjectWorkspace, host.Services.ApuVersionRepository) { }

	public ApuMergeViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IApuVersionRepository apuVersions)
		: base(session, workspace)
	{
		this.apuVersions = apuVersions;
		this.shell = shell;
		ResolveKeepLocalCommand = new RelayCommand<ConflictVm>(ResolveKeepLocal, c => c != null && CanEdit);
		ResolveAcceptCatalogCommand = new RelayCommand<ConflictVm>(ResolveAcceptCatalog, c => c != null && CanEdit);
		ResolveKeepBaseCommand = new RelayCommand<ConflictVm>(ResolveKeepBase, c => c != null && CanEdit);
		CreateMergeVersionCommand = new RelayCommand(async () => await CreateMergeVersionAsync(), () => IsReady && CanEdit && !IsLoading);
		CancelCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.ApuCatalog));
		SyncWorkspaceGate();
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		SyncWorkspaceGate();
		OnPropertyChanged(nameof(IsReadOnly));
		if (e.Kind == ProjectSessionChangeKind.Reset)
			CancelCommand.Execute(null);
	}

	protected override void OnWorkspaceGateChanged()
	{
		(ResolveKeepLocalCommand as RelayCommand<ConflictVm>)?.RaiseCanExecuteChanged();
		(ResolveAcceptCatalogCommand as RelayCommand<ConflictVm>)?.RaiseCanExecuteChanged();
		(ResolveKeepBaseCommand as RelayCommand<ConflictVm>)?.RaiseCanExecuteChanged();
		(CreateMergeVersionCommand as RelayCommand)?.RaiseCanExecuteChanged();
	}

	public void Initialize(string apuId, string code)
	{
		ApuId = apuId;
		Code = code;
		LoadComparison();
	}

	private void LoadComparison()
	{
		IsLoading = true;
		try
		{
			conflicts.Clear();
			diffs.Clear();
			conflicts.Add(new ConflictVm
			{
				Dimension = "Componentes",
				LocalValue = "3 ítems",
				CatalogValue = "5 ítems",
				BaseValue = "4 ítems",
				Resolution = "KeepLocal"
			});
			conflicts.Add(new ConflictVm
			{
				Dimension = "Recursos",
				LocalValue = "8 refs",
				CatalogValue = "10 refs",
				BaseValue = "9 refs",
				Resolution = "KeepLocal"
			});
			diffs.Add(new DiffVm
			{
				Dimension = "Costo Directo",
				LocalValue = "$ 100.000",
				CatalogValue = "$ 120.000",
				Impact = "+$ 20.000"
			});
			diffs.Add(new DiffVm
			{
				Dimension = "Precio Final",
				LocalValue = "$ 150.000",
				CatalogValue = "$ 180.000",
				Impact = "+$ 30.000"
			});
			IsReady = true;
			StatusMessage = "Comparación cargada. Revise los conflictos y resuelva.";
		}
		finally
		{
			IsLoading = false;
			OnWorkspaceGateChanged();
		}
	}

	private void ResolveKeepLocal(ConflictVm? c)
	{
		if (c != null && CanEdit)
			c.Resolution = "KeepLocal";
	}

	private void ResolveAcceptCatalog(ConflictVm? c)
	{
		if (c != null && CanEdit)
			c.Resolution = "AcceptCatalog";
	}

	private void ResolveKeepBase(ConflictVm? c)
	{
		if (c != null && CanEdit)
			c.Resolution = "KeepBase";
	}

	private async Task CreateMergeVersionAsync()
	{
		if (!CanEdit || IsLoading)
			return;

		IApuVersionRepository versionRepo = apuVersions;
		if (versionRepo == null)
		{
			StatusMessage = "Repositorio de versiones no disponible.";
			return;
		}

		IsLoading = true;
		OnWorkspaceGateChanged();
		try
		{
			int nextNumber = await versionRepo.GetNextVersionNumberAsync(ApuId);
			ApuVersion mergeVersion = new ApuVersion
			{
				VersionId = Guid.NewGuid().ToString("N"),
				ApuId = ApuId,
				Code = Code,
				Name = $"{Code} (merge v{nextNumber})",
				VersionNumber = nextNumber,
				Status = ApuVersionStatus.Draft,
				Author = SessionManager.Current?.DisplayName ?? "merge",
				Notes = $"Merge creado desde catálogo. Conflictos: {conflicts.Count(c => c.Resolution != "KeepLocal")} resueltos.",
				CreatedAt = DateTime.UtcNow,
				EffectiveDate = DateTime.UtcNow
			};
			await versionRepo.SaveAsync(mergeVersion);
			StatusMessage = $"Versión merge {nextNumber} creada: {mergeVersion.VersionId}";
			IsReady = false;
		}
		catch (Exception ex)
		{
			StatusMessage = "Error al crear merge: " + ex.Message;
		}
		finally
		{
			IsLoading = false;
			OnWorkspaceGateChanged();
		}
	}
}
