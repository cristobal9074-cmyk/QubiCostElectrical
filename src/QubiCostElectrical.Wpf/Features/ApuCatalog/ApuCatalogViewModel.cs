using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ApuCatalog;

public sealed class ApuCatalogViewModel : WorkspacePageViewModelBase
{
	private readonly IShellNavigationHost shell;

	private readonly ObservableCollection<ApuCatalogEntryVm> entries = new ObservableCollection<ApuCatalogEntryVm>();

	private ApuCatalogEntryVm? selectedEntry;

	private CancellationTokenSource? searchCts;

	private string searchText = "";

	private string statusMessage = "";

	private int totalCount;

	private int currentPage;

	private int pageSize = 20;

	private bool hasMore;

	private string? categoryFilter;

	public ObservableCollection<ApuCatalogEntryVm> Entries => entries;

	public ApuCatalogEntryVm? SelectedEntry
	{
		get => selectedEntry;
		set => SetProperty(ref selectedEntry, value, nameof(SelectedEntry));
	}

	public string SearchText
	{
		get => searchText;
		set => SetProperty(ref searchText, value, nameof(SearchText));
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value, nameof(StatusMessage));
	}

	public int TotalCount
	{
		get => totalCount;
		private set => SetProperty(ref totalCount, value, nameof(TotalCount));
	}

	public int CurrentPage
	{
		get => currentPage;
		private set => SetProperty(ref currentPage, value, nameof(CurrentPage));
	}

	public bool HasMore
	{
		get => hasMore;
		private set => SetProperty(ref hasMore, value, nameof(HasMore));
	}

	public string? CategoryFilter
	{
		get => categoryFilter;
		set => SetProperty(ref categoryFilter, value, nameof(CategoryFilter));
	}

	public ICommand SearchCommand { get; }

	public ICommand NextPageCommand { get; }

	public ICommand PreviousPageCommand { get; }

	public ICommand CompareCommand { get; }

	private readonly IApuCatalogRepository apuCatalog;

	public ApuCatalogViewModel(IShellNavigationHost shell, WpfAppHost host)
		: this(shell, host.Session, host.ProjectWorkspace, host.Services.ApuCatalogRepository) { }

	public ApuCatalogViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IApuCatalogRepository apuCatalog)
		: base(session, workspace)
	{
		this.shell = shell;
		this.apuCatalog = apuCatalog;
		SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !ShowEmptyState && !IsLoading);
		NextPageCommand = new RelayCommand(async () => await LoadPageAsync(currentPage + 1), () => HasMore && !ShowEmptyState && !IsLoading);
		PreviousPageCommand = new RelayCommand(async () => await LoadPageAsync(currentPage - 1), () => currentPage > 0 && !ShowEmptyState && !IsLoading);
		CompareCommand = new RelayCommand<ApuCatalogEntryVm>(CompareEntry, e => e != null && !ShowEmptyState);
		SyncWorkspaceGate();
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		SyncWorkspaceGate();
		if (e.Kind == ProjectSessionChangeKind.Reset)
		{
			Application.Current?.Dispatcher.InvokeAsync((Action)Clear);
		}
	}

	protected override void OnWorkspaceGateChanged()
	{
		(SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(CompareCommand as RelayCommand<ApuCatalogEntryVm>)?.RaiseCanExecuteChanged();
	}

	public async Task SearchAsync()
	{
		if (ShowEmptyState)
			return;

		IApuCatalogRepository apuCatalogRepository = apuCatalog;
		if (apuCatalogRepository == null)
		{
			StatusMessage = "Catálogo no disponible.";
			return;
		}

		searchCts?.Cancel();
		searchCts = new CancellationTokenSource();
		CancellationToken ct = searchCts.Token;
		IsLoading = true;
		OnWorkspaceGateChanged();
		entries.Clear();
		try
		{
			ApuCatalogSearchResult apuCatalogSearchResult = await apuCatalogRepository.SearchAsync(new ApuCatalogSearchRequest
			{
				Query = SearchText,
				Category = string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter,
				MaxResults = pageSize,
				Offset = 0
			});
			if (ct.IsCancellationRequested)
				return;

			foreach (ApuCatalogEntry entry in apuCatalogSearchResult.Entries)
			{
				entries.Add(new ApuCatalogEntryVm
				{
					ApuId = entry.ApuId,
					Code = entry.Code,
					Name = entry.Name,
					Category = entry.Category ?? string.Empty,
					Unit = entry.Unit,
					VersionCount = entry.VersionCount,
					ActiveVersionNumber = entry.ActiveVersionNumber,
					ActiveVersionStatus = entry.ActiveVersionStatus.ToString(),
					LastPrice = entry.LastPrice,
					IsActive = entry.IsActive,
					Owner = entry.Owner
				});
			}

			TotalCount = apuCatalogSearchResult.FilteredCount;
			CurrentPage = 0;
			HasMore = apuCatalogSearchResult.FilteredCount > pageSize;
			StatusMessage = $"{apuCatalogSearchResult.FilteredCount} resultados";
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Búsqueda cancelada.";
		}
		catch (Exception ex)
		{
			StatusMessage = "Error: " + ex.Message;
		}
		finally
		{
			IsLoading = false;
			OnWorkspaceGateChanged();
		}
	}

	public async Task LoadPageAsync(int page)
	{
		if (page < 0 || ShowEmptyState)
			return;

		IApuCatalogRepository apuCatalogRepository = apuCatalog;
		if (apuCatalogRepository == null)
			return;

		searchCts?.Cancel();
		searchCts = new CancellationTokenSource();
		CancellationToken ct = searchCts.Token;
		IsLoading = true;
		OnWorkspaceGateChanged();
		try
		{
			ApuCatalogSearchResult apuCatalogSearchResult = await apuCatalogRepository.SearchAsync(new ApuCatalogSearchRequest
			{
				Query = SearchText,
				Category = string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter,
				MaxResults = pageSize,
				Offset = page * pageSize
			});
			if (ct.IsCancellationRequested)
				return;

			entries.Clear();
			foreach (ApuCatalogEntry entry in apuCatalogSearchResult.Entries)
			{
				entries.Add(new ApuCatalogEntryVm
				{
					ApuId = entry.ApuId,
					Code = entry.Code,
					Name = entry.Name,
					Category = entry.Category,
					Unit = entry.Unit,
					VersionCount = entry.VersionCount,
					ActiveVersionNumber = entry.ActiveVersionNumber,
					ActiveVersionStatus = entry.ActiveVersionStatus.ToString(),
					LastPrice = entry.LastPrice,
					IsActive = entry.IsActive,
					Owner = entry.Owner
				});
			}

			CurrentPage = page;
			HasMore = (page + 1) * pageSize < apuCatalogSearchResult.FilteredCount;
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			IsLoading = false;
			OnWorkspaceGateChanged();
		}
	}

	private void CompareEntry(ApuCatalogEntryVm? entry)
	{
		if (entry == null || ShowEmptyState)
			return;

		shell.RequestNavigate(ShellRoute.ApuMerge);
	}

	private void Clear()
	{
		entries.Clear();
		SearchText = string.Empty;
		TotalCount = 0;
		CurrentPage = 0;
		HasMore = false;
		StatusMessage = string.Empty;
	}
}
