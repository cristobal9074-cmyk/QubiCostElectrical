using System.Windows;

namespace QubiCostElectrical.Wpf.Shell;

public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
    }

    public static void Show(Window owner)
    {
        var dialog = new KeyboardShortcutsDialog { Owner = owner };
        dialog.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
