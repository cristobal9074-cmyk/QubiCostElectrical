using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractEditorViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost _shell;

	private readonly WpfAppHost _host;

	private Contract _contract;

	private bool _isBusy;

	private string _errorMessage = string.Empty;

	public Contract Contract
	{
		get
		{
			return _contract;
		}
		set
		{
			SetProperty(ref _contract, value, "Contract");
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

	public ICommand SaveCommand { get; }

	public ICommand CancelCommand { get; }

	public ContractEditorViewModel(IShellNavigationHost shell, WpfAppHost host, Contract contract)
		: base(host)
	{
		_shell = shell ?? throw new ArgumentNullException("shell");
		_host = host ?? throw new ArgumentNullException("host");
		_contract = contract ?? throw new ArgumentNullException("contract");
		SaveCommand = new RelayCommand(async delegate
		{
			await SaveAsync();
		}, () => !_isBusy && !IsReadOnly);
		CancelCommand = new RelayCommand(delegate
		{
			GoBack();
		});
	}

	public async Task SaveAsync()
	{
		if (_isBusy || IsReadOnly)
		{
			return;
		}
		_isBusy = true;
		ErrorMessage = string.Empty;
		try
		{
			IContractService contractService = _host.Services.ContractService;
			if (contractService == null)
			{
				throw new InvalidOperationException("Servicio de contratos no disponible.");
			}
			if (!string.IsNullOrEmpty(_contract.Id?.Value))
			{
				await contractService.UpdateDraftAsync(_contract, _contract.RowVersion, _host.Session.CurrentUserName ?? "Admin");
			}
			else
			{
				await contractService.CreateAsync(_contract, _host.Session.CurrentUserName ?? "Admin");
			}
			GoBack();
		}
		catch (Exception ex)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			_isBusy = false;
		}
	}

	private void GoBack()
	{
		_shell.RequestNavigate(ShellRoute.ContractWorkspace);
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
		OnPropertyChanged("IsReadOnly");
	}
}
