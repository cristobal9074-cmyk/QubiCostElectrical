using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class LastSaveStatusFormatterTests
{
    [Fact]
    public void Format_MinValue_ReturnsSinRegistro()
    {
        Assert.Equal("sin registro", LastSaveStatusFormatter.Format(DateTime.MinValue));
    }

    [Fact]
    public void FormatSavedOkLabel_RecentSave_IncludesGuardadoOk()
    {
        string label = LastSaveStatusFormatter.FormatSavedOkLabel(DateTime.Now.AddSeconds(-5));
        Assert.StartsWith("Guardado OK ·", label);
        Assert.Contains("ahora", label);
    }

    [Fact]
    public void FormatSavedOkLabel_Today_ShowsClockTime()
    {
        var savedAt = DateTime.Now.AddHours(-2);
        string label = LastSaveStatusFormatter.FormatSavedOkLabel(savedAt);
        Assert.StartsWith("Guardado OK ·", label);
        Assert.Contains(savedAt.ToString("HH:mm"), label);
    }
}
