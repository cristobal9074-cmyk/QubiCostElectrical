using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class FileLockProtectionTests : IDisposable
{
    private readonly string tempFile;

    public FileLockProtectionTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), $"lock_test_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempFile, "test data");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
        catch
        {
            // best effort cleanup
        }
    }

    [Fact]
    public void IsFileLocked_WhenFileDoesNotExist_ReturnsFalse()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}.xlsx");
        Assert.False(OperationFailureMessages.IsFileLocked(nonExistent));
    }

    [Fact]
    public void IsFileLocked_WhenFileIsNotOpen_ReturnsFalse()
    {
        Assert.False(OperationFailureMessages.IsFileLocked(tempFile));
    }

    [Fact]
    public void IsFileLocked_WhenFileIsOpenExclusivelyByAnotherProcess_ReturnsTrue()
    {
        using (var lockStream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Mientras está abierto con FileShare.None (simulando Excel o Adobe exclusivo):
            Assert.True(OperationFailureMessages.IsFileLocked(tempFile));
        }

        // Una vez cerrado:
        Assert.False(OperationFailureMessages.IsFileLocked(tempFile));
    }

    [Fact]
    public void IsFileInUse_WithIOException_ReturnsTrue()
    {
        var ex = new IOException("The process cannot access the file because it is being used by another process.");
        Assert.True(OperationFailureMessages.IsFileInUse(ex));
    }

    [Fact]
    public void ToUserMessage_WithIOException_ReturnsFriendlyMessage()
    {
        var ex = new IOException("The process cannot access the file because it is being used by another process.");
        var msg = OperationFailureMessages.ToUserMessage(ex);

        Assert.Contains("abierto en otra aplicación", msg);
        Assert.Contains("Excel", msg);
    }
}
