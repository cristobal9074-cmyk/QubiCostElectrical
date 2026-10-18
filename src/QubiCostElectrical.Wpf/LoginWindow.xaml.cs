using System;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;

namespace QubiCostElectrical.Wpf;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(IAuthService authService, IUserPreferencesStore? preferencesStore = null)
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(authService, preferencesStore);
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateFirstUserVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.IsFirstUser) ||
            e.PropertyName == nameof(LoginViewModel.HasError))
        {
            Dispatcher.Invoke(UpdateFirstUserVisibility);
        }
    }

    private void UpdateFirstUserVisibility()
    {
        var isFirst = _viewModel.IsFirstUser;
        FirstUserBanner.Visibility = isFirst ? Visibility.Visible : Visibility.Collapsed;
        DisplayNamePanel.Visibility = isFirst ? Visibility.Visible : Visibility.Collapsed;
        RememberMeCheck.Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible;
        ErrorBanner.Visibility = _viewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Pin = PinBox.Password;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.CanInteract)
        {
            if (_viewModel.LoginCommand.CanExecute(null))
                _viewModel.LoginCommand.Execute(null);
            e.Handled = true;
        }

        if (e.Key == Key.Escape)
        {
            Application.Current.Shutdown();
            e.Handled = true;
        }
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
