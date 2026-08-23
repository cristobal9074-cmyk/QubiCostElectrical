using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractVersionsViewModel : SessionAwareViewModelBase
{
	private readonly IShellNavigationHost _shell;

	private readonly WpfAppHost _host;

	private ContractId _contractId;

	private readonly ObservableCollection<ContractVersion> _versions = new ObservableCollection<ContractVersion>();

	private ContractVersion? _selected;

	private bool _isBusy;

	private string _integrityMessage = string.Empty;

	private string _errorMessage = string.Empty;

	public ObservableCollection<ContractVersion> Versions => _versions;

	public ContractVersion? Selected
	{
		get
		{
			return _selected;
		}
		set
		{
			if (SetProperty(ref _selected, value, "Selected"))
			{
				IntegrityMessage = string.Empty;
				(VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
			}
		}
	}

	public string IntegrityMessage
	{
		get
		{
			return _integrityMessage;
		}
		private set
		{
			SetProperty(ref _integrityMessage, value, "IntegrityMessage");
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

	public ICommand VerifyIntegrityCommand { get; }

	public ICommand BackCommand { get; }

	public ContractVersionsViewModel(IShellNavigationHost shell, WpfAppHost host, ContractId contractId)
		: base(host)
	{
		_shell = shell ?? throw new ArgumentNullException("shell");
		_host = host ?? throw new ArgumentNullException("host");
		_contractId = contractId ?? throw new ArgumentNullException("contractId");
		VerifyIntegrityCommand = new RelayCommand(async delegate
		{
			await VerifySelectedIntegrityAsync();
		}, () => Selected != null && !_isBusy);
		BackCommand = new RelayCommand(delegate
		{
			GoBack();
		});
		_ = LoadVersionsAsync();
	}

	public async Task LoadVersionsAsync()
	{
		if (_isBusy)
		{
			return;
		}
		_isBusy = true;
		ErrorMessage = string.Empty;
		try
		{
			List<ContractVersion> obj = await (_host.Services.ContractService ?? throw new InvalidOperationException("Servicio de contratos no disponible.")).GetVersionsAsync(_contractId);
			_versions.Clear();
			foreach (ContractVersion item in obj)
			{
				_versions.Add(item);
			}
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

	private async Task VerifySelectedIntegrityAsync()
	{
		if (Selected == null || _isBusy)
		{
			return;
		}
		_isBusy = true;
		ErrorMessage = string.Empty;
		IntegrityMessage = string.Empty;
		try
		{
			IntegrityMessage = "✓ Integridad Verificada. Hash coincidente: " + (await (_host.Services.ContractService ?? throw new InvalidOperationException("Servicio de contratos no disponible.")).VerifyIntegrityAsync(Selected.Id)).Hash;
		}
		catch (Exception ex)
		{
			ErrorMessage = "❌ ERROR DE INTEGRIDAD: " + ex.Message;
		}
		finally
		{
			_isBusy = false;
		}
	}

	public void LoadForContract(ContractId contractId)
	{
		_contractId = contractId ?? throw new ArgumentNullException(nameof(contractId));
		_ = LoadVersionsAsync();
	}

	private void GoBack()
	{
		_shell.RequestNavigate(ShellRoute.ContractWorkspace);
	}

	protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
	{
	}
}
