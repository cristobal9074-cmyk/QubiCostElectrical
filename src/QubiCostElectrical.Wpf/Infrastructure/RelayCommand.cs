using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class RelayCommand : ICommand
{
    private readonly CommunityToolkit.Mvvm.Input.RelayCommand inner;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        inner = canExecute != null
            ? new CommunityToolkit.Mvvm.Input.RelayCommand(execute, canExecute)
            : new CommunityToolkit.Mvvm.Input.RelayCommand(execute);
    }

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            inner.CanExecuteChanged += value;
            CommandManager.RequerySuggested += value;
        }
        remove
        {
            inner.CanExecuteChanged -= value;
            CommandManager.RequerySuggested -= value;
        }
    }

    public bool CanExecute(object? parameter) => inner.CanExecute(parameter);
    public void Execute(object? parameter) => inner.Execute(parameter);
    public void RaiseCanExecuteChanged() => inner.NotifyCanExecuteChanged();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly CommunityToolkit.Mvvm.Input.RelayCommand<T> inner;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        inner = canExecute != null
            ? new CommunityToolkit.Mvvm.Input.RelayCommand<T>(execute, canExecute)
            : new CommunityToolkit.Mvvm.Input.RelayCommand<T>(execute);
    }

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            inner.CanExecuteChanged += value;
            CommandManager.RequerySuggested += value;
        }
        remove
        {
            inner.CanExecuteChanged -= value;
            CommandManager.RequerySuggested -= value;
        }
    }

    public bool CanExecute(object? parameter) => inner.CanExecute(parameter);
    public void Execute(object? parameter) => inner.Execute(parameter);
    public void RaiseCanExecuteChanged() => inner.NotifyCanExecuteChanged();
}
