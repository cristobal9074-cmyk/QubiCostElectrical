using System;
using System.IO;

namespace QubiCostElectrical;

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

    public static bool IsFileInUse(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is IOException or UnauthorizedAccessException)
                return true;
        }

        return false;
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
