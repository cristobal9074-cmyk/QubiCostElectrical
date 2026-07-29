using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;

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
  public static string? PromptText(string title, string label, string defaultValue = "")
  {
    var win = new Window
    {
      Title = title, Width = 400, Height = 150,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      ResizeMode = ResizeMode.NoResize,
      ShowInTaskbar = false,
    };
    if (Application.Current?.MainWindow != null)
      win.Owner = Application.Current.MainWindow;

    var tb = new TextBox { Text = defaultValue, Margin = new Thickness(8, 4, 8, 4), SelectionStart = 0, SelectionLength = defaultValue.Length };
    var ok = new Button { Content = "Aceptar", IsDefault = true, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
    var cancel = new Button { Content = "Cancelar", IsCancel = true, Width = 90 };

    string? result = null;
    ok.Click += (_, _) => { result = tb.Text; win.DialogResult = true; };
    cancel.Click += (_, _) => win.DialogResult = false;

    var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
    btnRow.Children.Add(ok); btnRow.Children.Add(cancel);

    var panel = new StackPanel { Margin = new Thickness(8) };
    panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
    panel.Children.Add(tb);
    panel.Children.Add(btnRow);

    win.Content = panel;
    win.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
    return win.ShowDialog() == true ? result : null;
  }

  public static string? PromptChoice(string title, string label, IReadOnlyList<string> options, string? defaultValue = null)
  {
    var win = new Window
    {
      Title = title, Width = 400, Height = 150,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      ResizeMode = ResizeMode.NoResize,
      ShowInTaskbar = false,
    };
    if (Application.Current?.MainWindow != null)
      win.Owner = Application.Current.MainWindow;

    var combo = new ComboBox { ItemsSource = options, Margin = new Thickness(8, 4, 8, 4) };
    combo.SelectedItem = defaultValue != null && options.Contains(defaultValue) ? defaultValue : options.FirstOrDefault();
    var ok = new Button { Content = "Aceptar", IsDefault = true, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
    var cancel = new Button { Content = "Cancelar", IsCancel = true, Width = 90 };

    string? result = null;
    ok.Click += (_, _) => { result = combo.SelectedItem as string; win.DialogResult = true; };
    cancel.Click += (_, _) => win.DialogResult = false;

    var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
    btnRow.Children.Add(ok); btnRow.Children.Add(cancel);

    var panel = new StackPanel { Margin = new Thickness(8) };
    panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
    panel.Children.Add(combo);
    panel.Children.Add(btnRow);

    win.Content = panel;
    win.Loaded += (_, _) => combo.Focus();
    return win.ShowDialog() == true ? result : null;
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
          "Solo una persona puede editar el proyecto a la vez. Puede abrirlo en solo lectura para consultar sin bloquear al editor activo.",
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
    return dialog.ShowDialog() == true ? dialog.FileName : null;
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
    return dialog.ShowDialog() == true ? dialog.FileName : null;
  }

  public static string? PromptOpenDocument()
  {
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
      Filter = "Documentos (*.pdf;*.dwg;*.dxf;*.doc;*.docx)|*.pdf;*.dwg;*.dxf;*.doc;*.docx|Todos los archivos (*.*)|*.*"
    };
    return dialog.ShowDialog() == true ? dialog.FileName : null;
  }

  public static string[] PromptOpenDocuments()
  {
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
      Filter = "Documentos (*.pdf;*.dwg;*.dxf;*.doc;*.docx)|*.pdf;*.dwg;*.dxf;*.doc;*.docx|Todos los archivos (*.*)|*.*",
      Multiselect = true
    };
    return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
  }

  public static string? PromptSelectFolder(string title)
  {
    var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
    return dialog.ShowDialog() == true ? dialog.FolderName : null;
  }

  public static string[] PromptSelectFilesInFolder(string folderPath, string title)
  {
    if (!System.IO.Directory.Exists(folderPath) || System.IO.Directory.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories).Length == 0)
    {
      InfoModal(title, "La carpeta seleccionada no tiene archivos.");
      return Array.Empty<string>();
    }

    var win = new SelectFilesFromFolderDialog(folderPath, title);
    if (Application.Current?.MainWindow != null)
      win.Owner = Application.Current.MainWindow;

    return win.ShowDialog() == true ? win.SelectedFiles : Array.Empty<string>();
  }

  public static string? PromptOpenExcel()
  {
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
      Filter = "Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Todos|*.*"
    };
    return dialog.ShowDialog() == true ? dialog.FileName : null;
  }

  public static string? PromptOpenJson(string title = "Abrir archivo JSON")
  {
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
      Title = title,
      Filter = "Biblioteca JSON (*.json)|*.json|Todos los archivos (*.*)|*.*"
    };
    return dialog.ShowDialog() == true ? dialog.FileName : null;
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
    return dialog.ShowDialog() == true ? dialog.FileName : null;
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
