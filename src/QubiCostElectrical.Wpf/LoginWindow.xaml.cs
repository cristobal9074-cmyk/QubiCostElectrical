using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;

namespace QubiCostElectrical.Wpf;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private bool _isPasswordVisible;

    public LoginWindow(IAuthService authService, IRemoteLicenseService? licenseService = null, IUserPreferencesStore? preferencesStore = null)
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(authService, licenseService, preferencesStore);
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisibility();
    }

    public LoginWindow(IAuthService authService, IUserPreferencesStore? preferencesStore)
        : this(authService, null, preferencesStore)
    {
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.HasError))
        {
            Dispatcher.Invoke(UpdateVisibility);
        }
    }

    private void UpdateVisibility()
    {
        ErrorBanner.Visibility = _viewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        if (!_isPasswordVisible)
        {
            _viewModel.Pin = PinBox.Password;
        }
    }

    private void OnVisiblePasswordChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPasswordVisible)
        {
            _viewModel.Pin = VisiblePasswordBox.Text;
        }
    }

    private void OnTogglePasswordVisibility(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            VisiblePasswordBox.Text = PinBox.Password;
            PinBox.Visibility = Visibility.Collapsed;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            EyeIcon.Text = "🔒";
            VisiblePasswordBox.Focus();
            VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
        }
        else
        {
            PinBox.Password = VisiblePasswordBox.Text;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            PinBox.Visibility = Visibility.Visible;
            EyeIcon.Text = "👁";
            PinBox.Focus();
        }
    }

    private void OnForgotPasswordClick(object sender, RoutedEventArgs e)
    {
        HelpBanner.Visibility = HelpBanner.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
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
