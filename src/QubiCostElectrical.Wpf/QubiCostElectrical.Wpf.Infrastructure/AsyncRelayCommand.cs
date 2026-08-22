#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
	private readonly CommunityToolkit.Mvvm.Input.AsyncRelayCommand inner;

	public event EventHandler? CanExecuteChanged
	{
		add
		{
			inner.CanExecuteChanged += value;
		}
		remove
		{
			inner.CanExecuteChanged -= value;
		}
	}

	public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
	{
		inner = ((canExecute != null) ? new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(execute, canExecute) : new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(execute));
	}

	public bool CanExecute(object? parameter)
	{
		return inner.CanExecute(parameter);
	}

	public void Execute(object? parameter)
	{
		inner.Execute(parameter);
	}

	public void RaiseCanExecuteChanged()
	{
		inner.NotifyCanExecuteChanged();
	}
}
