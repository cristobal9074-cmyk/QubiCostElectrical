using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Infrastructure;

public enum ThreeWayResult { Save, Discard, Cancel }

public enum ProjectLockResolution
{
    Cancel,
    ReadOnly,
    TakeOver
}

public static class WpfDialogService
{
    private static T RestoreFocusAfterDialog<T>(Func<T> action)
    {
        var previous = Keyboard.FocusedElement as IInputElement;
        try
        {
            return action();
        }
        finally
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                previous?.Focus();
                ShellWindow.RestoreWorkspaceGridFocus();
            }, DispatcherPriority.Input);
        }
    }

    public static string? PromptText(string title, string label, string defaultValue = "")
    {
        if (!ShellWindow.CanShowModalDialog)
            return null;

        var win = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            MinHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };
        TryApplyStyle(win, "Dialog.Window");
        if (Application.Current?.MainWindow != null)
            win.Owner = Application.Current.MainWindow;

        var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 12) };
        TryApplyStyle(tb, "Form.TextBox");
        if (tb.Text.Length > 0)
        {
            tb.SelectionStart = 0;
            tb.SelectionLength = defaultValue.Length;
        }

        var ok = new Button { Content = "Aceptar", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 90 };
        TryApplyStyle(ok, "Button.Primary");
        TryApplyStyle(cancel, "Button.Secondary");

        string? result = null;
        ok.Click += (_, _) => { result = tb.Text; win.DialogResult = true; };
        cancel.Click += (_, _) => win.DialogResult = false;

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);

        var titleBlock = new TextBlock { Text = title };
        TryApplyStyle(titleBlock, "Dialog.HeaderTitle");
        var labelBlock = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
        TryApplyStyle(labelBlock, "Dialog.HeaderSubtitle");

        var panel = new StackPanel();
        panel.Children.Add(titleBlock);
        panel.Children.Add(labelBlock);
        panel.Children.Add(tb);
        panel.Children.Add(btnRow);

        var chrome = new Border { Child = panel, Margin = new Thickness(0) };
        TryApplyStyle(chrome, "Dialog.Chrome");
        win.Content = chrome;
        win.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        return RestoreFocusAfterDialog(() => win.ShowDialog() == true ? result : null);
    }

    public static string? PromptChoice(string title, string label, IReadOnlyList<string> options, string? defaultValue = null)
    {
        if (!ShellWindow.CanShowModalDialog)
            return null;

        var win = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            MinHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };
        TryApplyStyle(win, "Dialog.Window");
        if (Application.Current?.MainWindow != null)
            win.Owner = Application.Current.MainWindow;

        var combo = new ComboBox { ItemsSource = options, Margin = new Thickness(0, 0, 0, 12) };
        TryApplyStyle(combo, "Form.ComboBox");
        combo.SelectedItem = defaultValue != null && options.Contains(defaultValue) ? defaultValue : options.FirstOrDefault();
        var ok = new Button { Content = "Aceptar", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 90 };
        TryApplyStyle(ok, "Button.Primary");
        TryApplyStyle(cancel, "Button.Secondary");

        string? result = null;
        ok.Click += (_, _) => { result = combo.SelectedItem as string; win.DialogResult = true; };
        cancel.Click += (_, _) => win.DialogResult = false;

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);

        var titleBlock = new TextBlock { Text = title };
        TryApplyStyle(titleBlock, "Dialog.HeaderTitle");
        var labelBlock = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
        TryApplyStyle(labelBlock, "Dialog.HeaderSubtitle");

        var panel = new StackPanel();
        panel.Children.Add(titleBlock);
        panel.Children.Add(labelBlock);
        panel.Children.Add(combo);
        panel.Children.Add(btnRow);

        var chrome = new Border { Child = panel, Margin = new Thickness(0) };
        TryApplyStyle(chrome, "Dialog.Chrome");
        win.Content = chrome;
        win.Loaded += (_, _) => combo.Focus();
        return RestoreFocusAfterDialog(() => win.ShowDialog() == true ? result : null);
    }

    private static void TryApplyStyle(FrameworkElement element, string resourceKey)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Style style)
            element.Style = style;
    }

    public static bool Confirm(string title, string message, bool isWarning = false)
    {
        var result = AppMessageDialog.Show(
            title,
            message,
            primaryText: "Sí",
            secondaryText: "No",
            isWarning: isWarning);
        return result == AppMessageDialogResult.Primary;
    }

    public static ProjectLockResolution ResolveProjectLock(ProjectLockInspection inspection)
    {
        string holder = inspection.HolderSummary;
        string body = inspection.State switch
        {
            ProjectLockState.RemoteRecent =>
                $"{holder}{Environment.NewLine}{Environment.NewLine}" +
                "Este .qce está en uso (bloqueo SMB/UNC). Solo 1 editor a la vez; puede abrirlo en solo lectura como consultor sin quitar el control al editor activo.",
            ProjectLockState.LocalMachineAlive =>
                $"{holder}{Environment.NewLine}{Environment.NewLine}" +
                "Otro proceso de Qubi Cost en este equipo tiene el control de edición.",
            ProjectLockState.HeartbeatExpired =>
                $"{holder}{Environment.NewLine}{Environment.NewLine}" +
                "El bloqueo anterior dejó de responder. Puede tomar el control de edición o abrir en solo lectura.",
            _ =>
                $"{holder}{Environment.NewLine}{Environment.NewLine}" +
                "No se pudo adquirir el bloqueo de edición exclusivo."
        };

        var result = AppMessageDialog.Show(
            "Proyecto en uso",
            body,
            primaryText: inspection.CanTakeOver ? "Tomar control" : "Abrir solo lectura",
            secondaryText: inspection.CanTakeOver ? "Abrir solo lectura" : null,
            cancelText: "Cancelar",
            isWarning: true);

        return result switch
        {
            AppMessageDialogResult.Primary when inspection.CanTakeOver => ProjectLockResolution.TakeOver,
            AppMessageDialogResult.Primary => ProjectLockResolution.ReadOnly,
            AppMessageDialogResult.Secondary => ProjectLockResolution.ReadOnly,
            _ => ProjectLockResolution.Cancel
        };
    }

    public static void Warning(string title, string message)
    {
        ShellNotificationService.Instance.ShowWarning(title, message);
    }

    public static void Info(string title, string message)
    {
        ShellNotificationService.Instance.ShowInfo(title, message);
    }

    public static void Error(string title, string message) =>
        ErrorModal(title, message);

    public static void ErrorModal(string title, string message) =>
        AppMessageDialog.Show(title, message, primaryText: "Aceptar", isError: true);

    public static void InfoModal(string title, string message) =>
        AppMessageDialog.Show(title, message, primaryText: "Aceptar");

    public static string? PromptSaveFile(string defaultName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Proyecto Qubi Cost Electrical (*.qce)|*.qce",
            FileName = defaultName,
            AddExtension = true,
            DefaultExt = ".qce"
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static ThreeWayResult ConfirmSaveDiscardCancel(string title, string message)
    {
        var result = AppMessageDialog.Show(
            title,
            message,
            primaryText: "Guardar",
            secondaryText: "Descartar",
            cancelText: "Cancelar",
            isWarning: true);

        return result switch
        {
            AppMessageDialogResult.Primary => ThreeWayResult.Save,
            AppMessageDialogResult.Secondary => ThreeWayResult.Discard,
            _ => ThreeWayResult.Cancel
        };
    }

    public static string? PromptOpenFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Proyecto Qubi Cost Electrical (*.qce)|*.qce|Proyecto heredado (*.pepro)|*.pepro|Todos los archivos (*.*)|*.*"
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static string? PromptOpenDocument()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Documentos (*.pdf;*.dwg;*.dxf;*.doc;*.docx)|*.pdf;*.dwg;*.dxf;*.doc;*.docx|Todos los archivos (*.*)|*.*"
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static string[] PromptOpenDocuments()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Documentos (*.pdf;*.dwg;*.dxf;*.doc;*.docx)|*.pdf;*.dwg;*.dxf;*.doc;*.docx|Todos los archivos (*.*)|*.*",
            Multiselect = true
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>());
    }

    public static string[] PromptSelectBlueprintFiles(string title = "Seleccionar planos")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = "Planos y Documentos (*.pdf;*.dwg;*.dxf;*.xlsx;*.docx;*.png;*.jpg;*.zip)|*.pdf;*.dwg;*.dxf;*.xlsx;*.docx;*.png;*.jpg;*.zip|Planos PDF (*.pdf)|*.pdf|Archivos AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf|Imágenes de planos (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*",
            Multiselect = true
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>());
    }

    public static string? PromptSelectFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"{title} (Navegue a la carpeta y pulse Abrir para seleccionarla)",
            Filter = "Todos los archivos y planos (*.*)|*.*|Planos PDF (*.pdf)|*.pdf|Archivos AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf",
            ValidateNames = false,
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "[Seleccionar esta carpeta]"
        };
        return RestoreFocusAfterDialog(() =>
        {
            if (dialog.ShowDialog() != true)
                return null;

            string path = dialog.FileName;
            if (System.IO.Directory.Exists(path))
                return path;
            if (System.IO.File.Exists(path))
                return System.IO.Path.GetDirectoryName(path);

            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                return dir;

            return null;
        });
    }

    public static string[] PromptSelectFilesInFolder(string folderPath, string title)
    {
        if (!ShellWindow.CanShowModalDialog)
            return Array.Empty<string>();

        if (!System.IO.Directory.Exists(folderPath) || System.IO.Directory.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories).Length == 0)
        {
            InfoModal(title, "La carpeta seleccionada no tiene archivos.");
            return Array.Empty<string>();
        }

        var win = new SelectFilesFromFolderDialog(folderPath, title);
        if (Application.Current?.MainWindow != null)
            win.Owner = Application.Current.MainWindow;

        return RestoreFocusAfterDialog(() => win.ShowDialog() == true ? win.SelectedFiles : Array.Empty<string>());
    }

    public static string? PromptOpenExcel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Todos|*.*"
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static string? PromptOpenJson(string title = "Abrir archivo JSON")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = "Biblioteca JSON (*.json)|*.json|Todos los archivos (*.*)|*.*"
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static string? PromptSaveJson(string defaultName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar biblioteca JSON",
            Filter = "Biblioteca JSON (*.json)|*.json",
            FileName = defaultName,
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };
        return RestoreFocusAfterDialog(() => dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public static ExportPriceFreezeDecision PromptExportWithoutFrozenPrices()
    {
        var result = AppMessageDialog.Show(
            "Precios no congelados",
            "Los precios del proyecto no están congelados. La exportación puede reflejar valores que cambien después.\n\n" +
            "Puede congelar los precios ahora para fijar UF, tipo de cambio y valores de materiales, o continuar bajo su responsabilidad.",
            primaryText: "Congelar y continuar",
            secondaryText: "Continuar con riesgo",
            cancelText: "Cancelar",
            isWarning: true);

        return result switch
        {
            AppMessageDialogResult.Primary => ExportPriceFreezeDecision.FreezeAndProceed,
            AppMessageDialogResult.Secondary => ExportPriceFreezeDecision.ProceedWithRisk,
            _ => ExportPriceFreezeDecision.Cancel
        };
    }
}
