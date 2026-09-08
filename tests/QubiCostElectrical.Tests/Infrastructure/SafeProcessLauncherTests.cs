using System;
using System.IO;
using QubiCostElectrical.Wpf.Infrastructure.Security;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class SafeProcessLauncherTests : IDisposable
{
    public SafeProcessLauncherTests()
    {
        SafeProcessLauncher.ProcessStarter = _ => null;
    }

    public void Dispose()
    {
        SafeProcessLauncher.ProcessStarter = null;
    }

    [Theory]
    [InlineData("https://www.google.cl")]
    [InlineData("http://localhost:5000")]
    [InlineData("mailto:soporte@qubicost.cl")]
    public void TryOpenUrl_AllowedSchemes_Accepted(string url)
    {
        bool success = SafeProcessLauncher.TryOpenUrl(url, out var error);
        Assert.True(success);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("powershell:Start-Process calc")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("cmd:/c dir")]
    [InlineData("javascript:alert(1)")]
    public void TryOpenUrl_DangerousSchemes_Rejected(string dangerousUrl)
    {
        bool success = SafeProcessLauncher.TryOpenUrl(dangerousUrl, out var error);
        Assert.False(success);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".ps1")]
    [InlineData(".vbs")]
    [InlineData(".js")]
    [InlineData(".scr")]
    [InlineData(".msi")]
    [InlineData(".reg")]
    public void TryOpenFile_BlockedExecutableExtensions_Rejected(string ext)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_payload_{Guid.NewGuid():N}{ext}");
        try
        {
            File.WriteAllText(tempFile, "payload");
            bool success = SafeProcessLauncher.TryOpenFile(tempFile, out var error);
            Assert.False(success);
            Assert.Contains("bloqueada por seguridad", error);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryOpenFile_NonExistentFile_Rejected()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}.pdf");
        bool success = SafeProcessLauncher.TryOpenFile(fakePath, out var error);
        Assert.False(success);
        Assert.Contains("no existe", error);
    }

    [Fact]
    public void TryOpenFolder_NonExistentFolder_Rejected()
    {
        var fakeFolder = Path.Combine(Path.GetTempPath(), $"non_existent_folder_{Guid.NewGuid():N}");
        bool success = SafeProcessLauncher.TryOpenFolder(fakeFolder, out var error);
        Assert.False(success);
        Assert.Contains("no existe", error);
    }
}
