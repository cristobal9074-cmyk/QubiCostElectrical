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

public sealed class ApuCatalogViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost shell;

	private readonly WpfAppHost host;

	private readonly ObservableCollection<ApuCatalogEntryVm> entries = new ObservableCollection<ApuCatalogEntryVm>();

	private ApuCatalogEntryVm? selectedEntry;

	private CancellationTokenSource? searchCts;

	private bool isSearching;

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
		get
		{
			return selectedEntry;
		}
		set
		{
			SetProperty(ref selectedEntry, value, "SelectedEntry");
		}
	}

	public bool IsSearching
	{
		get
		{
			return isSearching;
		}
		private set
		{
			SetProperty(ref isSearching, value, "IsSearching");
		}
	}

	public string SearchText
	{
		get
		{
			return searchText;
		}
		set
		{
			SetProperty(ref searchText, value, "SearchText");
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

	public int TotalCount
	{
		get
		{
			return totalCount;
		}
		private set
		{
			SetProperty(ref totalCount, value, "TotalCount");
		}
	}

	public int CurrentPage
	{
		get
		{
			return currentPage;
		}
		private set
		{
			SetProperty(ref currentPage, value, "CurrentPage");
		}
	}

	public bool HasMore
	{
		get
		{
			return hasMore;
		}
		private set
		{
			SetProperty(ref hasMore, value, "HasMore");
		}
	}

	public string? CategoryFilter
	{
		get
		{
			return categoryFilter;
		}
		set
		{
			SetProperty(ref categoryFilter, value, "CategoryFilter");
		}
	}

	public ICommand SearchCommand { get; }

	public ICommand NextPageCommand { get; }

	public ICommand PreviousPageCommand { get; }

	public ICommand CompareCommand { get; }

	public ApuCatalogViewModel(IShellNavigationHost shell, WpfAppHost host)
		: base(host)
	{
		this.shell = shell;
		this.host = host;
		SearchCommand = new RelayCommand(async delegate
		{
			await SearchAsync();
		});
		NextPageCommand = new RelayCommand(async delegate
		{
			await LoadPageAsync(currentPage + 1);
		}, () => HasMore);
		PreviousPageCommand = new RelayCommand(async delegate
		{
			await LoadPageAsync(currentPage - 1);
		}, () => currentPage > 0);
		CompareCommand = new RelayCommand<ApuCatalogEntryVm>(CompareEntry, (ApuCatalogEntryVm? e) => e != null);
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

	public async Task SearchAsync()
	{
		IApuCatalogRepository apuCatalogRepository = host.Services.ApuCatalogRepository;
		if (apuCatalogRepository == null)
		{
			StatusMessage = "Catálogo no disponible.";
			return;
		}
		searchCts?.Cancel();
		searchCts = new CancellationTokenSource();
		CancellationToken ct = searchCts.Token;
		IsSearching = true;
		entries.Clear();
		try
		{
			ApuCatalogSearchResult apuCatalogSearchResult = await apuCatalogRepository.SearchAsync(new ApuCatalogSearchRequest
			{
				Query = SearchText,
				Category = (string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter),
				MaxResults = pageSize,
				Offset = 0
			});
			if (ct.IsCancellationRequested)
			{
				return;
			}
			foreach (ApuCatalogEntry entry in apuCatalogSearchResult.Entries)
			{
				entries.Add(new ApuCatalogEntryVm
				{
					ApuId = entry.ApuId,
					Code = entry.Code,
					Name = entry.Name,
					Category = (entry.Category ?? string.Empty),
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
		catch (Exception ex2)
		{
			StatusMessage = "Error: " + ex2.Message;
		}
		finally
		{
			IsSearching = false;
		}
	}

	public async Task LoadPageAsync(int page)
	{
		if (page < 0)
		{
			return;
		}
		IApuCatalogRepository apuCatalogRepository = host.Services.ApuCatalogRepository;
		if (apuCatalogRepository == null)
		{
			return;
		}
		searchCts?.Cancel();
		searchCts = new CancellationTokenSource();
		CancellationToken ct = searchCts.Token;
		IsSearching = true;
		try
		{
			ApuCatalogSearchResult apuCatalogSearchResult = await apuCatalogRepository.SearchAsync(new ApuCatalogSearchRequest
			{
				Query = SearchText,
				Category = (string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter),
				MaxResults = pageSize,
				Offset = page * pageSize
			});
			if (ct.IsCancellationRequested)
			{
				return;
			}
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
			IsSearching = false;
		}
	}

	private void CompareEntry(ApuCatalogEntryVm? entry)
	{
		if (!(entry == null))
		{
			shell.RequestNavigate(ShellRoute.ApuMerge);
		}
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
