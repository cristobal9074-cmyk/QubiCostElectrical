using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Antes de centralizar esta regla en ProjectInfoRow.IsPrecioVencido, el umbral de "precio
/// vencido" estaba hardcodeado por separado en Features/Cubicacion/CubicacionRightPanelVm.cs
/// (180 días fijos, ignorando PriceExpiryCriticalDays) y en
/// Application/Services/PriceTraceabilityService.cs (6 meses, un número distinto de nuevo).
/// </summary>
public sealed class ProjectInfoRowPrecioVencidoTests
{
    [Fact]
    public void IsPrecioVencido_UsesConfiguredThreshold()
    {
        var project = new ProjectInfoRow { PriceExpiryCriticalDays = 30 };
        var now = AppClock.Now.Date;

        Assert.False(project.IsPrecioVencido(now.AddDays(-17))); // dentro del umbral de 30 días
        Assert.True(project.IsPrecioVencido(now.AddDays(-61)));  // más allá del umbral de 30 días
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void EffectivePriceExpiryCriticalDays_FallsBackTo180_WhenMisconfigured(int configured)
    {
        var project = new ProjectInfoRow { PriceExpiryCriticalDays = configured };

        Assert.Equal(180, project.EffectivePriceExpiryCriticalDays);
    }

    [Fact]
    public void EffectivePriceExpiryCriticalDays_UsesConfiguredValue_WhenPositive()
    {
        var project = new ProjectInfoRow { PriceExpiryCriticalDays = 90 };

        Assert.Equal(90, project.EffectivePriceExpiryCriticalDays);
    }
}
