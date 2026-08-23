using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractWorkspaceViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost _shell;

	private readonly WpfAppHost _host;

	private readonly ObservableCollection<Contract> _items = new ObservableCollection<Contract>();

	private readonly AsyncDebounceRunner _searchDebounce;

	private Contract? _selected;

	private string _search = string.Empty;

	private string _statusMessage = string.Empty;

	private string _errorMessage = string.Empty;

	private bool _isBusy;

	public ObservableCollection<Contract> Items => _items;

	public Contract? Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			if (SetProperty(ref _selected, value, "Selected"))
			{
				RaiseCmds();
			}
		}
	}

	public string SearchText
	{
		get
		{
			return _search;
		}
		set
		{
			if (SetProperty(ref _search, value, "SearchText"))
			{
				_searchDebounce.Schedule();
			}
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		private set
		{
			SetProperty(ref _statusMessage, value, "StatusMessage");
		}
	}

	public string ErrorMessage
	{
		get
		{
			return _errorMessage;
		}
		private set
		{
			SetProperty(ref _errorMessage, value, "ErrorMessage");
		}
	}

	public bool IsReadOnly => _host.Session.IsReadOnly;

	public ICommand RefreshCommand { get; }

	public ICommand CreateCommand { get; }

	public ICommand EditCommand { get; }

	public ICommand VersionsCommand { get; }

	public ContractWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host)
		: base(host)
	{
		_shell = shell ?? throw new ArgumentNullException("shell");
		_host = host ?? throw new ArgumentNullException("host");
		_searchDebounce = new AsyncDebounceRunner(LoadAsync, TimeSpan.FromMilliseconds(250L));
		RefreshCommand = new RelayCommand(async delegate
		{
			await LoadAsync();
		}, () => !_isBusy);
		CreateCommand = new RelayCommand(delegate
		{
			CreateNew();
		}, () => !_isBusy && !IsReadOnly && _host.Permissions.CanContractCreate);
		EditCommand = new RelayCommand(delegate
		{
			EditSelected();
		}, () => Selected != null && !_isBusy && !IsReadOnly);
		VersionsCommand = new RelayCommand(delegate
		{
			ViewVersions();
		}, () => Selected != null && !_isBusy);
		_ = LoadAsync();
	}

	public async Task LoadAsync(CancellationToken ct = default(CancellationToken))
	{
		if (_isBusy)
		{
			return;
		}
		if (_host.Services.ContractService == null)
		{
			ErrorMessage = "Servicio de contratos no disponible.";
			return;
		}
		_isBusy = true;
		ErrorMessage = string.Empty;
		try
		{
			string projectPath = _host.Session.CurrentProjectPath ?? string.Empty;
			List<Contract> list = await _host.Services.ContractService.SearchAsync(projectPath, SearchText, null, 0, 200, ct);
			if (ct.IsCancellationRequested)
			{
				return;
			}
			_items.Clear();
			foreach (Contract item in list)
			{
				_items.Add(item);
			}
			StatusMessage = $"{list.Count} contrato(s) cargado(s).";
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			ErrorMessage = ex2.Message;
		}
		finally
		{
			_isBusy = false;
			RaiseCmds();
			OnPropertyChanged("IsReadOnly");
		}
	}

	private void CreateNew()
	{
		_shell.RequestNavigate(ShellRoute.ContractEditor, new ContractEditorNavContext(null));
	}

	private void EditSelected()
	{
		if (Selected != null)
			_shell.RequestNavigate(ShellRoute.ContractEditor, new ContractEditorNavContext(Selected));
	}

	private void ViewVersions()
	{
		if (Selected != null)
			_shell.RequestNavigate(ShellRoute.ContractVersions, new ContractVersionsNavContext(Selected.Id));
	}

	private void RaiseCmds()
	{
		(RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
		(VersionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		OnPropertyChanged("IsReadOnly");
		RaiseCmds();
		_ = LoadAsync();
	}

	protected override void DisposeCore()
	{
		_searchDebounce.Dispose();
	}
}
