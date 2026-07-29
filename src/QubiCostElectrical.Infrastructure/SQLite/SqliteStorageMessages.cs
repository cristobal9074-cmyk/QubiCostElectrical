using System;
using System.IO;

namespace QubiCostElectrical;

public static class SqliteStorageMessages
{
    public static bool IsDatabaseLocked(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            string message = current.Message ?? "";

            if (current.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
                || message.Contains("SQLite", StringComparison.OrdinalIgnoreCase)
                || message.Contains("database", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("database file is locked", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("busy", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public static bool IsSchemaNewerThanApp(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is InvalidDataException id
                && id.Message.Contains("esquema v", StringComparison.OrdinalIgnoreCase)
                && id.Message.Contains("soporta hasta", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsMigrationFailure(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is InvalidOperationException io
                && io.Message.Contains("actualizar la base de datos del proyecto", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string ToUserMessage(Exception ex)
    {
        if (IsSchemaNewerThanApp(ex))
        {
            return ex.InnerException?.Message ?? ex.Message;
        }

        if (IsMigrationFailure(ex))
        {
            return ex.InnerException?.Message ?? ex.Message;
        }

        if (IsDatabaseLocked(ex))
        {
            return "No se pudo escribir la base SQLite. El archivo puede estar bloqueado por otra aplicación, "
                   + "por sincronización en la nube (Dropbox, OneDrive) o por falta de permisos. "
                   + "Tus cambios siguen en memoria; cierra lo que bloquee el archivo e intenta guardar de nuevo.";
        }

        if (ex is FileNotFoundException)
            return "No se encontró el archivo de base de datos del proyecto.";

        return ex.Message;
    }
}
