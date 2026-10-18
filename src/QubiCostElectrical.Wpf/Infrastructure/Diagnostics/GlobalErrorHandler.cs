using System.Diagnostics;
using System.IO;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Security;

namespace QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

public static class GlobalErrorHandler
{
    private static WpfAppHost? host;
    private static IAppLogger? logger;

    public static void Initialize(WpfAppHost appHost)
    {
        host = appHost ?? throw new ArgumentNullException(nameof(appHost));
        logger = WpfAppHost.Logger;
        Application.Current.DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnTaskUnobserved;
    }

    public static void Shutdown()
    {
        Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandled;
        TaskScheduler.UnobservedTaskException -= OnTaskUnobserved;
    }

    private static void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var errorId = logger?.Critical("Dispatcher", "Excepción no controlada en dispatcher", e.Exception) ?? "";
        var recoverable = IsRecoverable(e.Exception);

        var result = ShowErrorDialog(
            "Error inesperado",
            "Ocurrió un error inesperado en la interfaz.",
            errorId,
            e.Exception,
            recoverable);

        switch (result)
        {
            case ErrorAction.Retry when recoverable:
                e.Handled = true;
                break;
            case ErrorAction.SaveAs:
                e.Handled = true;
                TrySaveAs(e.Exception);
                break;
            case ErrorAction.OpenLogs:
                e.Handled = true;
                OpenLogsFolder();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    private static void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            logger?.Critical("AppDomain", "Error crítico no controlado", ex);
        }

        ShowErrorDialog(
            "Error crítico",
            "La aplicación debe cerrarse debido a un error crítico.",
            "",
            e.ExceptionObject as Exception,
            allowRetry: false);

        OpenLogsFolder();
    }

    private static void OnTaskUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var ex = e.Exception;
        logger?.Error("TaskScheduler", "Excepción no observada en tarea", ex);
        e.SetObserved();
    }

    private static ErrorAction ShowErrorDialog(string title, string message, string errorId, Exception? ex, bool allowRetry)
    {
        var msg = message;

        if (ex != null)
            msg += $"\n\nDetalle: {OperationFailureMessages.ToUserMessage(ex, errorId)}";
        else if (!string.IsNullOrEmpty(errorId))
            msg += $"\n\nID de error: {errorId}";

        if (allowRetry)
        {
            var result = AppMessageDialog.Show(
                title,
                msg,
                primaryText: "Reintentar",
                secondaryText: "Guardar como...",
                cancelText: "Cerrar",
                isError: true);

            return result switch
            {
                AppMessageDialogResult.Primary => ErrorAction.Retry,
                AppMessageDialogResult.Secondary => ErrorAction.SaveAs,
                _ => ErrorAction.Close
            };
        }

        if (!string.IsNullOrEmpty(errorId))
        {
            var result = AppMessageDialog.Show(
                title,
                msg,
                primaryText: "Aceptar",
                secondaryText: "Abrir carpeta de logs",
                isError: true);

            return result == AppMessageDialogResult.Secondary
                ? ErrorAction.OpenLogs
                : ErrorAction.Close;
        }

        WpfDialogService.ErrorModal(title, msg);
        return ErrorAction.Close;
    }

    private static bool IsRecoverable(Exception ex)
    {
        // Excepciones verdaderamente fatales a nivel de runtime CLR que corrompen el estado del proceso
        if (ex is OutOfMemoryException or StackOverflowException or AccessViolationException)
            return false;

        return true;
    }

    private static void TrySaveAs(Exception? ex)
    {
        try
        {
            if (host == null || !host.Session.IsDirty)
                return;

            var path = host.FileDialog.PromptSaveFile(
                "Guardar proyecto como", $"{host.Session.ProjectInfo.Nombre}{ProjectFileTypes.CurrentExtension}",
                $"Proyecto Qubi Cost Electrical (*{ProjectFileTypes.CurrentExtension})|*{ProjectFileTypes.CurrentExtension}");
            if (path != null)
            {
                host.ProjectWorkspace.SaveAsAsync(path).GetAwaiter().GetResult();
                WpfDialogService.Info("Respaldo completado", $"El proyecto ha sido guardado exitosamente en:\n{path}");
            }
        }
        catch (Exception saveEx)
        {
            logger?.Error("GlobalErrorHandler", "No se pudo ofrecer Guardar como", saveEx);
            WpfDialogService.Error("Error al guardar", $"No se pudo completar el guardado de emergencia:\n{saveEx.Message}");
        }
    }

    private static void OpenLogsFolder()
    {
        var folder = new LogConfiguration().LogsFolderPath();
        if (!SafeProcessLauncher.TryOpenFolder(folder, out var error))
        {
            logger?.Error("GlobalErrorHandler", $"No se pudo abrir la carpeta de logs: {error}");
        }
    }

    private enum ErrorAction { Retry, SaveAs, OpenLogs, Close }
}
