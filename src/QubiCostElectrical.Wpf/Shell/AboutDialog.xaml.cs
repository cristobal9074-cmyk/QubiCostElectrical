#nullable enable
using System.Windows;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Wpf.Shell;

public partial class AboutDialog : Window
{
    public AboutViewModel ViewModel { get; }

    public AboutDialog(ILicenseService? licenseService = null)
    {
        InitializeComponent();
        ViewModel = new AboutViewModel(licenseService);
        DataContext = ViewModel;
    }

    public static void Show(Window owner, ILicenseService? licenseService = null)
    {
        if (!ShellWindow.CanShowModalDialog)
            return;

        var dialog = new AboutDialog(licenseService) { Owner = owner };
        dialog.ShowDialog();
    }

    private void OnTabGeneralClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 0;
    }

    private void OnTabChangelogClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 1;
    }

    private void OnTabLegalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 2;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
