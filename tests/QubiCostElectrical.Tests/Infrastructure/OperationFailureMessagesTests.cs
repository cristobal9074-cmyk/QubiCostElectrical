using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class OperationFailureMessagesTests
{
    [Fact]
    public void ToUserMessage_ProjectOperationException_ReturnsUserMessage()
    {
        var ex = new ProjectOperationException(
            ProjectOperationKind.ExcelImport,
            "El archivo Excel no se pudo interpretar.");

        Assert.Equal("El archivo Excel no se pudo interpretar.", OperationFailureMessages.ToUserMessage(ex));
    }

    [Fact]
    public void ToUserMessage_WrappedProjectOperationException_ReturnsInnerUserMessage()
    {
        var ex = new InvalidOperationException(
            "fallo",
            new ProjectOperationException(ProjectOperationKind.ExcelExport, "Cierra Excel e intenta de nuevo."));

        Assert.Equal("Cierra Excel e intenta de nuevo.", OperationFailureMessages.ToUserMessage(ex));
    }

    [Fact]
    public void ToUserMessage_GenericIOException_ReturnsFileInUseHint()
    {
        var ex = new IOException("sharing violation");

        string message = OperationFailureMessages.ToUserMessage(ex);

        Assert.Contains("abierto", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(OperationFailureMessages.IsFileInUse(ex));
        Assert.False(SqliteStorageMessages.IsDatabaseLocked(ex));
    }

    [Fact]
    public void ToUserMessage_SqliteLockedIOException_ReturnsSqliteHint()
    {
        var ex = new IOException("SQLite Error 5: database is locked.");

        Assert.True(SqliteStorageMessages.IsDatabaseLocked(ex));
        Assert.Contains("SQLite", OperationFailureMessages.ToUserMessage(ex), StringComparison.OrdinalIgnoreCase);
    }
}
