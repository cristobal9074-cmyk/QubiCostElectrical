using System;
using System.IO;

namespace QubiCostElectrical.Infrastructure;

public static class OperationFailureMessages
{
    private const string GenericUserMessage =
        "No pudimos completar la operación por un problema inesperado.";

    public static string ToUserMessage(Exception ex)
    {
        return ToUserMessage(ex, null);
    }

    public static string ToUserMessage(Exception ex, string? errorId)
    {
        if (ex is ProjectOperationException op)
            return AppendErrorId(op.UserMessage, errorId);

        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is ProjectOperationException innerOp)
                return AppendErrorId(innerOp.UserMessage, errorId);
        }

        if (SqliteStorageMessages.IsSchemaNewerThanApp(ex)
            || SqliteStorageMessages.IsMigrationFailure(ex)
            || SqliteStorageMessages.IsDatabaseLocked(ex))
            return AppendErrorId(SqliteStorageMessages.ToUserMessage(ex), errorId);

        if (IsFileInUse(ex))
        {
            return BuildUserFacingMessage(
                "No se pudo guardar el archivo porque está abierto en otra aplicación (por ejemplo Excel) o bloqueado por sincronización en la nube.",
                "Cierra el archivo y vuelve a intentar.",
                errorId);
        }

        return BuildUserFacingMessage(
            GenericUserMessage,
            "Reintenta. Si persiste, guarda una copia del proyecto y abre la carpeta de logs para compartir el ID con soporte.",
            errorId);
    }

    public const string FileInUseUserMessage =
        "No se pudo guardar el archivo porque está abierto en otra aplicación (por ejemplo Excel o Adobe Reader) o bloqueado por sincronización en la nube.\n\nQué hacer ahora: Cierra el archivo en la otra aplicación y vuelve a intentar.";

    public static bool IsFileInUse(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is IOException or UnauthorizedAccessException)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Comprueba si un archivo existe y está bloqueado por otro proceso (por ejemplo Microsoft Excel o Adobe Acrobat).
    /// </summary>
    public static bool IsFileLocked(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string AppendErrorId(string message, string? errorId)
    {
        if (string.IsNullOrWhiteSpace(errorId))
            return message;

        return $"{message}\n\nID de error: {errorId}";
    }

    private static string BuildUserFacingMessage(string message, string nextAction, string? errorId)
    {
        return AppendErrorId($"{message}\n\nQué hacer ahora: {nextAction}", errorId);
    }
}
