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

public sealed class ApuMergeViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost shell;

	private readonly WpfAppHost host;

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
		get
		{
			return apuId;
		}
		private set
		{
			SetProperty(ref apuId, value, "ApuId");
		}
	}

	public string Code
	{
		get
		{
			return code;
		}
		private set
		{
			SetProperty(ref code, value, "Code");
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

	public bool IsReady
	{
		get
		{
			return isReady;
		}
		private set
		{
			SetProperty(ref isReady, value, "IsReady");
		}
	}

	public ICommand ResolveKeepLocalCommand { get; }

	public ICommand ResolveAcceptCatalogCommand { get; }

	public ICommand ResolveKeepBaseCommand { get; }

	public ICommand CreateMergeVersionCommand { get; }

	public ICommand CancelCommand { get; }

	public ApuMergeViewModel(IShellNavigationHost shell, WpfAppHost host)
		: base(host)
	{
		ApuMergeViewModel apuMergeViewModel = this;
		this.shell = shell;
		this.host = host;
		ResolveKeepLocalCommand = new RelayCommand<ConflictVm>(ResolveKeepLocal, (ConflictVm? c) => c != null);
		ResolveAcceptCatalogCommand = new RelayCommand<ConflictVm>(ResolveAcceptCatalog, (ConflictVm? c) => c != null);
		ResolveKeepBaseCommand = new RelayCommand<ConflictVm>(ResolveKeepBase, (ConflictVm? c) => c != null);
		CreateMergeVersionCommand = new RelayCommand(async delegate
		{
			await apuMergeViewModel.CreateMergeVersionAsync();
		}, () => apuMergeViewModel.IsReady);
		CancelCommand = new RelayCommand(delegate
		{
			shell.RequestNavigate(ShellRoute.ApuCatalog);
		});
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		if (e.Kind == ProjectSessionChangeKind.Reset)
		{
			CancelCommand.Execute(null);
		}
	}

	public void Initialize(string apuId, string code)
	{
		ApuId = apuId;
		Code = code;
		LoadComparison();
	}

	private void LoadComparison()
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

	private void ResolveKeepLocal(ConflictVm? c)
	{
		if (c != null)
		{
			c.Resolution = "KeepLocal";
		}
	}

	private void ResolveAcceptCatalog(ConflictVm? c)
	{
		if (c != null)
		{
			c.Resolution = "AcceptCatalog";
		}
	}

	private void ResolveKeepBase(ConflictVm? c)
	{
		if (c != null)
		{
			c.Resolution = "KeepBase";
		}
	}

	private async Task CreateMergeVersionAsync()
	{
		IApuVersionRepository versionRepo = host.Services.ApuVersionRepository;
		if (versionRepo == null)
		{
			StatusMessage = "Repositorio de versiones no disponible.";
			return;
		}
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
				Author = (SessionManager.Current?.DisplayName ?? "merge"),
				Notes = $"Merge creado desde catálogo. Conflictos: {conflicts.Count((ConflictVm c) => c.Resolution != "KeepLocal")} resueltos.",
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
	}
}
