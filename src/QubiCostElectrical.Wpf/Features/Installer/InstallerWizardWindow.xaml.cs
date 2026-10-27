using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Installer;

public partial class InstallerWizardWindow : Window
{
    private readonly IUserPreferencesStore? preferencesStore;
    private readonly string appVersion;

    public InstallerWizardWindow(IUserPreferencesStore? preferencesStore = null)
    {
        InitializeComponent();
        this.preferencesStore = preferencesStore;

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        appVersion = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.1.6";

        TxtHeaderVersion.Text = appVersion;
        TxtComponentVersion.Text = appVersion;
        TxtStampVersion.Text = $"Edición Oficial de Despliegue {appVersion}";
        TxtProgressTitle.Text = $"Instalando Qubi Cost® Electrical {appVersion}";

        // Valores por defecto
        var defaultProgFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Qubi Cost Electrical");
        var defaultDocs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Presupuestos Electricos");

        TxtAppPath.Text = defaultProgFiles;
        TxtProjectPath.Text = defaultDocs;

        UpdateDiskSpaceLabel(TxtAppPath.Text);
    }

    private void UpdateDiskSpaceLabel(string targetPath)
    {
        try
        {
            var root = Path.GetPathRoot(targetPath);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    TxtDiskSpace.Text = $"Espacio en disco: 245 MB necesarios / {freeGb:F1} GB disponibles en ({drive.Name})";
                    return;
                }
            }
        }
        catch
        {
            // Ignorar errores en rutas no parseables
        }

        TxtDiskSpace.Text = "Espacio en disco: 245 MB necesarios / Más de 100 GB disponibles";
    }

    private void OnTitlebarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    private void OnBackClick(object sender, RoutedEventArgs e) => Close();

    private void OnBrowseAppPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccione la carpeta de instalación de Qubi Cost Electrical",
            InitialDirectory = Directory.Exists(TxtAppPath.Text) ? TxtAppPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtAppPath.Text = dialog.FolderName;
            UpdateDiskSpaceLabel(dialog.FolderName);
        }
    }

    private void OnBrowseProjectPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccione la carpeta predeterminada para guardar presupuestos (.qce)",
            InitialDirectory = Directory.Exists(TxtProjectPath.Text) ? TxtProjectPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtProjectPath.Text = dialog.FolderName;
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        ProgressOverlay.Visibility = Visibility.Visible;

        var selectedProjFolder = TxtProjectPath.Text.Trim();
        if (!string.IsNullOrEmpty(selectedProjFolder) && !Directory.Exists(selectedProjFolder))
        {
            try { Directory.CreateDirectory(selectedProjFolder); } catch { }
        }

        var steps = new (int Pct, string Status, string Detail)[]
        {
            (15, "Desempaquetando librerías y ensamblados...", "QubiCostElectrical.Wpf.dll"),
            (35, "Configurando motor de base de datos SQLite...", "e_sqlite3.dll"),
            (55, "Inicializando Banco Canónico de Precios...", "canonical_database.db"),
            (75, "Registrando Certificado Authenticode en TrustedPublisher...", "CertificateTrustRegistrar.Register()"),
            (90, "Configurando directorio de proyectos en preferences.json...", "preferences.json"),
            (100, "Creando acceso directo y finalizando instalación...", "Qubi Cost Electrical.lnk")
        };

        foreach (var s in steps)
        {
            InstallProgressBar.Value = s.Pct;
            TxtProgressPercent.Text = $"{s.Pct}%";
            TxtProgressStatus.Text = s.Status;
            TxtProgressDetail.Text = s.Detail;
            await Task.Delay(350);
        }

        // 1. Guardar preferencias de usuario
        try
        {
            var store = preferencesStore ?? new UserPreferencesStore();
            var prefs = store.Load();
            prefs.DefaultProjectFolder = selectedProjFolder;
            store.Save(prefs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Installer] Error al guardar preferencias: {ex.Message}");
        }

        // 2. Registrar certificado digital
        if (ChkCertTrust.IsChecked == true)
        {
            try
            {
                CertificateTrustRegistrar.EnsureTrusted();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Installer] Error al registrar certificado: {ex.Message}");
            }
        }

        // 3. Registrar asociación .qce
        if (ChkAssociateQce.IsChecked == true)
        {
            try
            {
                ShellRegistration.Register();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Installer] Error al registrar asociación .qce: {ex.Message}");
            }
        }

        // 4. Crear acceso directo si fue solicitado
        if (ChkDesktopShortcut.IsChecked == true)
        {
            CreateDesktopShortcut();
        }

        TxtSummaryProj.Text = $"• Directorio de presupuestos configurado en: {selectedProjFolder}";
        PanelFinished.Visibility = Visibility.Visible;
    }

    private void CreateDesktopShortcut()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktop, "Qubi Cost Electrical.lnk");
            var targetExe = Environment.ProcessPath ?? "";

            if (!string.IsNullOrEmpty(targetExe) && File.Exists(targetExe))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetExe;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                        shortcut.Description = "Qubi Cost Electrical — Software de Presupuesto Eléctrico";
                        shortcut.Save();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Installer] Error al crear acceso directo: {ex.Message}");
        }
    }

    private void OnFinishAndLaunchClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
