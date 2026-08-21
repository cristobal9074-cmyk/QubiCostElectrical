using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class SqliteStorageMessagesTests
{
    [Fact]
    public void ToUserMessage_DatabaseLocked_ReturnsActionableHint()
    {
        var ex = new IOException("SQLite Error 5: database is locked.");

        string message = SqliteStorageMessages.ToUserMessage(ex);

        Assert.Contains("bloqueado", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(SqliteStorageMessages.IsDatabaseLocked(ex));
    }

    [Fact]
    public void IsSchemaNewerThanApp_DetectsInvalidDataException()
    {
        var ex = new InvalidDataException(
            "La base SQLite tiene esquema v99, pero esta aplicación soporta hasta v20. Abre el proyecto con una versión más nueva del programa.");

        Assert.True(SqliteStorageMessages.IsSchemaNewerThanApp(ex));
    }

    [Fact]
    public void IsMigrationFailure_DetectsWrappedMigrationError()
    {
        var ex = new InvalidOperationException(
            "No se pudo actualizar la base de datos del proyecto a la versión 18 (Registro central de antecedentes técnicos del presupuesto).",
            new Exception("SQL logic error"));

        Assert.True(SqliteStorageMessages.IsMigrationFailure(ex));
    }
}
