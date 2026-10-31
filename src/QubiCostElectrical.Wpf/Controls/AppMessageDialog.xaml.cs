using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Controls;

public enum AppMessageDialogResult
{
    None,
    Primary,
    Secondary,
    Cancel
}

public sealed partial class AppMessageDialog : Window, INotifyPropertyChanged
{
    private string dialogTitle = "";
    private string dialogMessage = "";

    public AppMessageDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppMessageDialogResult Result { get; private set; } = AppMessageDialogResult.None;

    public string DialogTitle
    {
        get => dialogTitle;
        private set
        {
            if (dialogTitle == value)
                return;
            dialogTitle = value;
            OnPropertyChanged();
        }
    }

    public string DialogMessage
    {
        get => dialogMessage;
        private set
        {
            if (dialogMessage == value)
                return;
            dialogMessage = value;
            OnPropertyChanged();
        }
    }

    public static Func<string, string, AppMessageDialogResult>? DialogHandlerForTesting { get; set; }

    public static AppMessageDialogResult Show(
        string title,
        string message,
        string primaryText = "Aceptar",
        string? secondaryText = null,
        string? cancelText = null,
        bool isWarning = false,
        bool isError = false)
    {
        if (DialogHandlerForTesting != null)
            return DialogHandlerForTesting(title, message);

        if (!ShellWindow.CanShowModalDialog)
            return AppMessageDialogResult.Cancel;

        var previous = Keyboard.FocusedElement as IInputElement;
        var dialog = new AppMessageDialog();
        if (Application.Current?.MainWindow is { IsLoaded: true } owner)
            dialog.Owner = owner;

        dialog.Configure(title, message, primaryText, secondaryText, cancelText, isWarning, isError);
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                previous?.Focus();
                ShellWindow.RestoreWorkspaceGridFocus();
            }, DispatcherPriority.Input);
        }
        return dialog.Result;
    }

    private void Configure(
        string title,
        string message,
        string primaryText,
        string? secondaryText,
        string? cancelText,
        bool isWarning,
        bool isError)
    {
        Title = title;
        DialogTitle = title;
        DialogMessage = message;

        PrimaryButton.Content = primaryText;

        if (string.IsNullOrWhiteSpace(secondaryText))
            SecondaryButton.Visibility = Visibility.Collapsed;
        else
            SecondaryButton.Content = secondaryText;

        if (string.IsNullOrWhiteSpace(cancelText))
            CancelButton.Visibility = Visibility.Collapsed;
        else
            CancelButton.Content = cancelText;

        if (isError)
        {
            // El banner ya muestra el mensaje completo con énfasis visual; mostrarlo también
            // en MessageText duplica el texto completo en pantalla (ver AppMessageDialog.xaml).
            MessageText.Visibility = Visibility.Collapsed;
            ErrorBanner.Visibility = Visibility.Visible;
            ErrorBannerText.Text = message;
        }
        else if (isWarning)
        {
            MessageText.Visibility = Visibility.Collapsed;
            WarningBanner.Visibility = Visibility.Visible;
            WarningBannerText.Text = message;
        }

        Loaded += (_, _) => PrimaryButton.Focus();
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        Result = AppMessageDialogResult.Primary;
        DialogResult = true;
    }

    private void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        Result = AppMessageDialogResult.Secondary;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = AppMessageDialogResult.Cancel;
        DialogResult = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
