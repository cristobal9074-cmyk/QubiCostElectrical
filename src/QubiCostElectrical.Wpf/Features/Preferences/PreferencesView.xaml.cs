using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public partial class PreferencesView : UserControl
{
    private readonly WpfAppHost host;

    public PreferencesView(WpfAppHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.TrySave();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.ApplyChanges();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.Cancel();
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        vm.RestoreDefaults(() =>
            WpfDialogService.Confirm("Restaurar predeterminados",
                "Se restablecerán todas las preferencias a los valores de fábrica.\n¿Continuar?",
                isWarning: true));
    }

    private void OnBrowseProjectFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var dlg = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta predeterminada de proyectos"
        };
        if (dlg.ShowDialog() == true)
            vm.DefaultProjectFolder = dlg.FolderName;
    }

    private void OnClearProjectFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.DefaultProjectFolder = "";
    }

    private void OnBrowsePriceBankDatabaseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar base de datos del banco de precios",
            Filter = "SQLite (*.db)|*.db|Todos los archivos|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            vm.PriceBankDatabasePath = dlg.FileName;
    }

    private void OnClearPriceBankDatabaseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.PriceBankDatabasePath = "";
    }

    private void OnOpenLogsFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        vm.OpenLogsFolder(host.FileOpener);
    }

    private void OnOpenRecoveryFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        vm.OpenRecoveryFolder(host.FileOpener);
    }

    private void OnBrowseLogoClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar logo de membrete",
            Filter = "Logo PNG/JPG (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        var error = vm.ValidateLogoPath(dlg.FileName);
        if (error != null) { WpfDialogService.Error("Logo no válido", error); return; }
        vm.LogoPath = dlg.FileName;
    }

    private void OnClearLogoClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.LogoPath = "";
    }

    private void OnBrowseSignatureClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar imagen de firma",
            Filter = "Firma PNG/JPG (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        var error = vm.ValidateSignaturePath(dlg.FileName);
        if (error != null) { WpfDialogService.Error("Firma no válida", error); return; }
        vm.ProfessionalSignatureImagePath = dlg.FileName;
    }

    private void OnClearSignatureClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm) vm.ProfessionalSignatureImagePath = "";
    }

    private async void OnCreateUserClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (!WpfDialogService.Confirm("Crear usuario", "Se creará una nueva cuenta local para este equipo. ¿Deseas continuar?"))
            return;
        await vm.CreateUserAsync();
    }

    private async void OnToggleUserActiveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button btn || btn.CommandParameter is not AuthUserSummary user) return;
        var action = user.IsActive ? "desactivar" : "activar";
        if (!WpfDialogService.Confirm(
                "Estado de usuario",
                $"Se va a {action} la cuenta de {user.DisplayName} ({user.Email}). ¿Continuar?",
                isWarning: user.IsActive))
            return;
        await vm.ToggleUserActiveAsync(user);
    }

    private async void OnRotateUserRoleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button btn || btn.CommandParameter is not AuthUserSummary user) return;

        var nextRole = user.Role switch
        {
            UserRole.Admin => UserRole.Estimator,
            UserRole.Estimator => UserRole.Viewer,
            _ => UserRole.Admin
        };

        if (!WpfDialogService.Confirm(
                "Cambiar rol",
                $"Se cambiará el rol de {user.DisplayName} ({user.Email}) desde {user.Role} a {nextRole}. ¿Continuar?",
                isWarning: true))
            return;

        await vm.ChangeUserRoleAsync(user, nextRole);
    }

    private async void OnResetUserPinClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button btn || btn.CommandParameter is not AuthUserSummary user) return;

        var pin = WpfDialogService.PromptText("Reset PIN", $"Nuevo PIN para {user.DisplayName} ({user.Email})", "");
        if (string.IsNullOrWhiteSpace(pin))
            return;

        if (!WpfDialogService.Confirm(
                "Reset PIN",
                $"Se restablecerá el PIN de {user.DisplayName} ({user.Email}). ¿Confirmas la operación?",
                isWarning: true))
            return;

        await vm.ResetUserPinAsync(user, pin.Trim());
    }

    private async void OnUnlockUserClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button btn || btn.CommandParameter is not AuthUserSummary user) return;
        if (!WpfDialogService.Confirm("Desbloquear cuenta",
                $"Se desbloqueará la cuenta de {user.DisplayName} ({user.Email}) y se reiniciarán intentos fallidos. ¿Continuar?",
                isWarning: true))
            return;
        await vm.UnlockUserAsync(user);
    }

    private AuthUserSummary? GetContextUser(object? sender)
    {
        if (sender is MenuItem { Parent: ContextMenu menu } && menu.PlacementTarget is DataGridRow row)
            return row.Item as AuthUserSummary;
        if (DataContext is PreferencesViewModel vm)
            return vm.SelectedUser;
        return null;
    }

    private async void OnContextToggleUserClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var user = GetContextUser(sender);
        if (user == null) return;
        await vm.ToggleUserActiveAsync(user);
    }

    private async void OnContextRotateRoleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var user = GetContextUser(sender);
        if (user == null) return;
        var nextRole = user.Role switch
        {
            UserRole.Admin => UserRole.Estimator,
            UserRole.Estimator => UserRole.Viewer,
            _ => UserRole.Admin
        };
        await vm.ChangeUserRoleAsync(user, nextRole);
    }

    private async void OnContextResetPinClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var user = GetContextUser(sender);
        if (user == null) return;
        var pin = WpfDialogService.PromptText("Reset PIN", $"Nuevo PIN para {user.DisplayName} ({user.Email})", "");
        if (string.IsNullOrWhiteSpace(pin))
            return;
        await vm.ResetUserPinAsync(user, pin.Trim());
    }

    private async void OnContextUnlockUserClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        var user = GetContextUser(sender);
        if (user == null) return;
        if (!WpfDialogService.Confirm("Desbloquear cuenta",
                $"Se desbloqueará la cuenta de {user.DisplayName} ({user.Email}) y se reiniciarán intentos fallidos. ¿Continuar?",
                isWarning: true))
            return;
        await vm.UnlockUserAsync(user);
    }

    private async void OnUsersGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm && vm.HandleShortcutCaptureKey(e))
            return;

        if (DataContext is not PreferencesViewModel vm2 || vm2.SelectedUser == null)
            return;

        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            var user = vm2.SelectedUser;
            var nextRole = user.Role switch
            {
                UserRole.Admin => UserRole.Estimator,
                UserRole.Estimator => UserRole.Viewer,
                _ => UserRole.Admin
            };
            await vm2.ChangeUserRoleAsync(user, nextRole);
        }
        else if (e.Key == Key.U && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await vm2.UnlockUserAsync(vm2.SelectedUser);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm)
            vm.HandleShortcutCaptureKey(e);
    }

    private void OnChangeShortcutClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button { Tag: KeyboardShortcutPreferenceRowVm row }) return;
        vm.BeginShortcutCapture(row);
    }

    private void OnResetShortcutClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (sender is not Button { Tag: KeyboardShortcutPreferenceRowVm row }) return;
        vm.ResetShortcut(row);
    }

    private void OnResetAllShortcutsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (!WpfDialogService.Confirm(
                "Restaurar atajos",
                "¿Restaurar todos los atajos de Cubicación a los valores predeterminados?",
                isWarning: true))
            return;
        vm.ResetAllCubicacionShortcuts();
    }

    private void OnResetAllShellShortcutsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel vm) return;
        if (!WpfDialogService.Confirm(
                "Restaurar atajos",
                "¿Restaurar los atajos de shell / itemizado a los valores predeterminados?",
                isWarning: true))
            return;
        vm.ResetAllShellShortcuts();
    }
}
